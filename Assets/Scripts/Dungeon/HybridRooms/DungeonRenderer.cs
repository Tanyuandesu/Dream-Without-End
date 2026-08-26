using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 DungeonLayout 的数据转换成可见的地板、墙壁与碰撞体。
///
/// R7.4：
/// 1. 完整保留现有的逐格程序化渲染。
/// 2. Hybrid Layout 会实例化真实 Room Prefab。
/// 3. 所有实例 Socket 先关闭；全部 Connection 解析通过后再统一开门。
/// 4. 只把 CorridorCells 渲染成走廊地板，并从边界建立走廊墙。
/// 5. 墙候选会排除最终 FloorCells 与房间 Occupied Global Cells。
/// 6. 门与走廊统一提交；任一错误都不留下部分开门或半条走廊。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRenderer : MonoBehaviour
{
    [Header("渲染模式")]
    [SerializeField]
    private DungeonRenderMode renderMode =
        DungeonRenderMode.ProceduralCells;

    [Tooltip(
        "Hybrid 请求已由 GameManager 明确回退为 Procedural Layout 时，" +
        "额外输出 Renderer 回退日志。")]
    [SerializeField]
    private bool logHybridFallback = true;

#if UNITY_EDITOR
    [Header("R7.3 受控失败测试")]
    [Tooltip(
        "正常运行必须关闭。开启时，Renderer 只在解析阶段把 " +
        "Connection 0 的 Socket A Id " +
        "替换为不存在的测试 Id；不会修改 Layout、Prefab Asset 或 R6 数据。")]
    [SerializeField]
    private bool r73InjectMissingSocketForControlledFailure;
#endif

#if UNITY_EDITOR
    [Header("R7.4 受控失败测试")]
    [Tooltip(
        "正常运行必须关闭。开启时，Renderer 只在本地走廊预检副本中" +
        "加入一个房间 Occupied Cell；不会修改 Layout、Connection、" +
        "Prefab Asset 或 R6 数据。")]
    [SerializeField]
    private bool r74InjectInvalidCorridorCellForControlledFailure;
#endif

    [Header("显示")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Color floorColor =
        new Color(0.16f, 0.17f, 0.21f);
    [SerializeField] private Color wallColor =
        new Color(0.35f, 0.37f, 0.44f);

    [Tooltip(
        "可选的走廊表现层。None 时逐字保持 R7.4 平面色基线；" +
        "临时灰石 Profile 只改变 Sprite 与颜色，不改变碰撞或 FloorCells。")]
    [SerializeField]
    private DungeonCorridorVisualProfile corridorVisualProfile;

    private Sprite whiteSprite;

    private const string R73MissingSocketTestId =
        "__R7.3_MISSING_SOCKET_TEST__";

    /// <summary>
    /// 索引与当前 Hybrid Layout 的 RoomPlacements 完全一致。
    /// R7.3/R7.4 使用这份“房间索引 → 实例模板”映射打开连接门。
    /// </summary>
    private readonly List<DreamRoomTemplate>
        hybridRoomInstanceTemplates =
            new List<DreamRoomTemplate>();

    private static readonly Vector2Int[] EightDirections =
    {
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1,  1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  1),
        new Vector2Int(-1, -1)
    };

    public float CellSize => cellSize;
    public DungeonRenderMode RenderMode => renderMode;
    public DungeonCorridorVisualProfile CorridorVisualProfile =>
        corridorVisualProfile;

    private void Awake()
    {
        cellSize = Mathf.Max(0.25f, cellSize);
        CreateWhiteSprite();
    }

    public void Render(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        hybridRoomInstanceTemplates.Clear();

        if (layout == null)
        {
            Debug.LogError(
                "DungeonRenderer 收到空的 DungeonLayout。",
                this);
            return;
        }

        if (dungeonRoot == null)
        {
            Debug.LogError(
                "DungeonRenderer 收到空的 dungeonRoot。",
                this);
            return;
        }

        switch (renderMode)
        {
            case DungeonRenderMode.HybridPrefabRooms:
                RenderHybridPrefabRoomsOrFallback(
                    layout,
                    dungeonRoot);
                break;

            case DungeonRenderMode.ProceduralCells:
            default:
                RenderProceduralCells(
                    layout,
                    dungeonRoot);
                break;
        }
    }

    /// <summary>
    /// 当前正式工作的旧地图路径。
    /// 后续混合模式的开发不会修改这个方法的职责。
    /// </summary>
    private void RenderProceduralCells(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        BuildFloors(layout, dungeonRoot);
        BuildWalls(layout, dungeonRoot);
    }

    /// <summary>
    /// R7.4 的 Hybrid 入口。
    ///
    /// R7.1 的生成失败会把一个旧 Procedural Layout 交给 Renderer；
    /// 这种情况必须明确回退，不能把零个 Placement 当成 Hybrid 成功。
    /// </summary>
    private void RenderHybridPrefabRoomsOrFallback(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        if (!layout.HasHybridRoomData ||
            layout.RoomPlacements.Count == 0)
        {
            if (logHybridFallback)
            {
                Debug.LogWarning(
                    "[DungeonRenderer/R7.4] 收到 Procedural fallback Layout。" +
                    " Requested=HybridPrefabRooms," +
                    " Effective=ProceduralCells" +
                    " | HasHybridRoomData=" +
                    layout.HasHybridRoomData +
                    " | RoomPlacements=" +
                    layout.RoomPlacements.Count +
                    " | 使用旧逐格 Renderer，未实例化 Room Prefab。",
                    this);
            }

            RenderProceduralCells(layout, dungeonRoot);
            return;
        }

        string validationFailure;

        if (!TryValidateHybridRoomPlacements(
                layout,
                out validationFailure))
        {
            Debug.LogError(
                "[DungeonRenderer/R7.4] Hybrid 房间实例化前校验失败。" +
                " Requested=HybridPrefabRooms," +
                " Effective=ProceduralCells" +
                " | " + validationFailure +
                " | 已明确使用旧逐格 Renderer，避免空白层。",
                this);

            RenderProceduralCells(layout, dungeonRoot);
            return;
        }

        RenderHybridRoomPrefabs(layout, dungeonRoot);
    }

    private bool TryValidateHybridRoomPlacements(
        DungeonLayout layout,
        out string failureReason)
    {
        failureReason = string.Empty;

        for (int roomIndex = 0;
             roomIndex < layout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null)
            {
                failureReason =
                    "RoomPlacement " + roomIndex +
                    " 是空引用。";
                return false;
            }

            if (placement.Template == null)
            {
                failureReason =
                    "RoomPlacement " + roomIndex +
                    " 的 Template 是空引用。";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 实例化房间 Prefab，预检并暂存走廊几何，最后统一开门与显示。
    /// 任何失败都保持实例门关闭，且不会暴露半条走廊。
    /// </summary>
    private void RenderHybridRoomPrefabs(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform roomsRoot =
            CreateHybridRoot("Rooms", dungeonRoot);

        // 建立完成前保持隐藏；若某个 Prefab 运行时实例化失败，
        // 不会把半套房间暴露给玩家。
        roomsRoot.gameObject.SetActive(false);

        Vector3 gridCellZeroWorldPosition =
            CellToWorld(Vector2Int.zero);

        int totalSocketCount = 0;

        try
        {
            for (int roomIndex = 0;
                 roomIndex < layout.RoomPlacements.Count;
                 roomIndex++)
            {
                DreamRoomPlacement placement =
                    layout.RoomPlacements[roomIndex];

                DreamRoomTemplate assetTemplate =
                    placement.Template;

                GameObject roomInstance = Instantiate(
                    assetTemplate.gameObject,
                    roomsRoot);

                DreamRoomTemplate instanceTemplate =
                    roomInstance.GetComponent<
                        DreamRoomTemplate>();

                if (instanceTemplate == null)
                {
                    throw new MissingComponentException(
                        "RoomPlacement " + roomIndex +
                        " 实例 '" + roomInstance.name +
                        "' 的根节点缺少 DreamRoomTemplate。");
                }

                roomInstance.name =
                    assetTemplate.TemplateId +
                    "_Instance_" + roomIndex;

                placement.ApplyPose(
                    roomInstance.transform,
                    gridCellZeroWorldPosition,
                    cellSize);

                // 灰盒与后续正式房间都以一格一单位制作；
                // Cell Size 只参与位置换算，不缩放 Prefab 根节点。
                roomInstance.transform.localScale =
                    Vector3.one;

                // 必须操作实例组件，绝不能修改 assetTemplate。
                instanceTemplate.SetAllSocketsOpen(false);

                totalSocketCount +=
                    instanceTemplate.DoorSockets.Count;

                hybridRoomInstanceTemplates.Add(
                    instanceTemplate);
            }
        }
        catch (System.Exception exception)
        {
            hybridRoomInstanceTemplates.Clear();
            roomsRoot.gameObject.SetActive(false);
            Destroy(roomsRoot.gameObject);

            Debug.LogError(
                "[DungeonRenderer/R7.4] Hybrid 房间实例化中止。" +
                " Requested=HybridPrefabRooms," +
                " Effective=ProceduralCells" +
                " | 未提交半成品 Rooms。\n" +
                exception,
                this);

            RenderProceduralCells(layout, dungeonRoot);
            return;
        }

        List<DreamRoomDoorSocket> resolvedSockets;
        string doorFailureReason;

        bool doorSocketsResolved =
            TryResolveConnectionSockets(
                layout,
                out resolvedSockets,
                out doorFailureReason);

        if (!doorSocketsResolved)
        {
            roomsRoot.gameObject.SetActive(true);
            CreateEmptyHybridCorridorRoots(dungeonRoot);

            Debug.LogError(
                "[DungeonRenderer/R7.3] Connection 门状态提交被拒绝。\n" +
                doorFailureReason + "\n" +
                "Requested=HybridPrefabRooms, " +
                "Effective=HybridPrefabRooms, " +
                "DoorActivation=Rejected" +
                " | RoomInstances=" +
                hybridRoomInstanceTemplates.Count + "/" +
                layout.RoomPlacements.Count +
                " | Connections=0/" +
                layout.Connections.Count +
                " | OpenedSockets=0" +
                " | ClosedSockets=" + totalSocketCount +
                " | 所有实例门保持关闭；没有猜测替代 Socket；" +
                "Prefab Asset 未修改。",
                this);

            return;
        }

        List<Vector2Int> corridorFloorCells;
        List<Vector2Int> corridorWallCells;
        int occupiedRoomCellCount;
        string corridorFailureReason;

        bool corridorPlanValid =
            TryBuildHybridCorridorRenderPlan(
                layout,
                out corridorFloorCells,
                out corridorWallCells,
                out occupiedRoomCellCount,
                out corridorFailureReason);

        if (!corridorPlanValid)
        {
            roomsRoot.gameObject.SetActive(true);
            CreateEmptyHybridCorridorRoots(dungeonRoot);

            Debug.LogError(
                "[DungeonRenderer/R7.4] 走廊渲染提交被拒绝。\n" +
                corridorFailureReason + "\n" +
                "Requested=HybridPrefabRooms, " +
                "Effective=HybridPrefabRooms, " +
                "CorridorRender=Rejected, " +
                "DoorActivation=Rejected" +
                " | RoomInstances=" +
                hybridRoomInstanceTemplates.Count + "/" +
                layout.RoomPlacements.Count +
                " | Connections=0/" +
                layout.Connections.Count +
                " | OpenedSockets=0" +
                " | ClosedSockets=" + totalSocketCount +
                " | CorridorFloors=0/" +
                layout.CorridorCells.Count +
                " | CorridorWalls=0" +
                " | 所有实例门保持关闭；走廊容器为空；" +
                "Layout 与 Prefab Asset 未修改。",
                this);

            return;
        }

        Transform corridorsRoot;
        Transform corridorWallsRoot;
        string geometryFailureReason;

        bool geometryReady =
            TryCreateHybridCorridorGeometry(
                corridorFloorCells,
                corridorWallCells,
                layout.Seed,
                dungeonRoot,
                out corridorsRoot,
                out corridorWallsRoot,
                out geometryFailureReason);

        if (!geometryReady)
        {
            roomsRoot.gameObject.SetActive(true);
            CreateEmptyHybridCorridorRoots(dungeonRoot);

            Debug.LogError(
                "[DungeonRenderer/R7.4] 走廊几何建立中止。" +
                " Requested=HybridPrefabRooms," +
                " Effective=HybridPrefabRooms," +
                " CorridorRender=ExceptionRejected," +
                " DoorActivation=Rejected" +
                " | OpenedSockets=0" +
                " | ClosedSockets=" + totalSocketCount +
                " | 未提交暂存走廊。\n" +
                geometryFailureReason,
                this);

            return;
        }

        string doorCommitFailureReason;

        if (!TryOpenResolvedConnectionSockets(
                resolvedSockets,
                out doorCommitFailureReason))
        {
            DiscardHybridCorridorGeometry(
                corridorsRoot,
                corridorWallsRoot);

            SetAllHybridInstanceSocketsClosed();
            roomsRoot.gameObject.SetActive(true);
            CreateEmptyHybridCorridorRoots(dungeonRoot);

            Debug.LogError(
                "[DungeonRenderer/R7.4] 门与走廊统一提交中止。" +
                " Requested=HybridPrefabRooms," +
                " Effective=HybridPrefabRooms," +
                " CorridorRender=Rejected," +
                " DoorActivation=Rejected" +
                " | OpenedSockets=0" +
                " | ClosedSockets=" + totalSocketCount +
                " | 已回滚所有实例门并丢弃暂存走廊。\n" +
                doorCommitFailureReason,
                this);

            return;
        }

        int openedSocketCount = resolvedSockets.Count;

        corridorsRoot.name = "Corridors";
        corridorWallsRoot.name = "CorridorWalls";

        roomsRoot.gameObject.SetActive(true);
        corridorsRoot.gameObject.SetActive(true);
        corridorWallsRoot.gameObject.SetActive(true);

        int closedUnusedSocketCount =
            Mathf.Max(
                0,
                totalSocketCount - openedSocketCount);

        Debug.Log(
            "[DungeonRenderer/R7.4] Hybrid 房间、Connection 门与程序化走廊已提交。" +
            " Requested=HybridPrefabRooms," +
            " Effective=HybridPrefabRooms" +
            " | RoomInstances=" +
            hybridRoomInstanceTemplates.Count + "/" +
            layout.RoomPlacements.Count +
            " | Connections=" +
            (openedSocketCount / 2) + "/" +
            layout.Connections.Count +
            " | TotalSockets=" + totalSocketCount +
            " | OpenedSockets=" + openedSocketCount +
            " | ClosedUnusedSockets=" +
            closedUnusedSocketCount +
            " | SocketTarget=InstanceOnly" +
            " | CorridorSource=Layout.CorridorCells" +
            " | CorridorFloors=" +
            corridorFloorCells.Count + "/" +
            layout.CorridorCells.Count +
            " | CorridorFloorColliders=0" +
            " | CorridorWalls=" +
            corridorWallCells.Count +
            " | CorridorWallColliders=" +
            corridorWallCells.Count +
            " | OccupiedRoomCells=" +
            occupiedRoomCellCount +
            " | FloorSorting=-10" +
            " | WallSorting=0" +
            " | SharedCorridorCells=Deduplicated" +
            (corridorVisualProfile == null
                ? string.Empty
                : " | CorridorVisualProfile=" +
                  corridorVisualProfile.ProfileId +
                  " | VisualSkinSlots=CardinalMask16"),
            this);
    }

    /// <summary>
    /// 这里只解析并去重所有实例 Socket，不改变门状态。
    /// R7.4 会在走廊几何暂存完成后才统一开门。
    /// </summary>
    private bool TryResolveConnectionSockets(
        DungeonLayout layout,
        out List<DreamRoomDoorSocket> resolvedSockets,
        out string failureReason)
    {
        resolvedSockets =
            new List<DreamRoomDoorSocket>();
        failureReason = string.Empty;

        if (layout.Connections.Count == 0)
        {
            failureReason = BuildConnectionDoorFailure(
                -1,
                "Both",
                -1,
                "None",
                "None",
                "Connections 为空，不能提交门状态。");
            return false;
        }

        resolvedSockets.Capacity =
            layout.Connections.Count * 2;

        HashSet<DreamRoomDoorSocket> uniqueSockets =
            new HashSet<DreamRoomDoorSocket>();

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                layout.Connections[connectionIndex];

            if (connection == null)
            {
                failureReason = BuildConnectionDoorFailure(
                    connectionIndex,
                    "Both",
                    -1,
                    "None",
                    "None",
                    "Connection 是空引用。");
                return false;
            }

            bool injectMissingSocket =
                ShouldInjectR73ControlledFailure(
                    connectionIndex);

            string socketAId = injectMissingSocket
                ? R73MissingSocketTestId
                : connection.SocketAId;

            string socketANote = injectMissingSocket
                ? "受控失败已注入；原 SocketId='" +
                  connection.SocketAId + "'。"
                : string.Empty;

            DreamRoomDoorSocket socketA;

            if (!TryResolveConnectionSocket(
                    connectionIndex,
                    "A",
                    connection.RoomAIndex,
                    socketAId,
                    socketANote,
                    out socketA,
                    out failureReason))
            {
                return false;
            }

            if (!uniqueSockets.Add(socketA))
            {
                failureReason = BuildConnectionDoorFailure(
                    connectionIndex,
                    "A",
                    connection.RoomAIndex,
                    hybridRoomInstanceTemplates[
                        connection.RoomAIndex].TemplateId,
                    socketA.SocketId,
                    "同一个实例 Socket 被多条 Connection 重复引用。");
                return false;
            }

            resolvedSockets.Add(socketA);

            DreamRoomDoorSocket socketB;

            if (!TryResolveConnectionSocket(
                    connectionIndex,
                    "B",
                    connection.RoomBIndex,
                    connection.SocketBId,
                    string.Empty,
                    out socketB,
                    out failureReason))
            {
                return false;
            }

            if (!uniqueSockets.Add(socketB))
            {
                failureReason = BuildConnectionDoorFailure(
                    connectionIndex,
                    "B",
                    connection.RoomBIndex,
                    hybridRoomInstanceTemplates[
                        connection.RoomBIndex].TemplateId,
                    socketB.SocketId,
                    "同一个实例 Socket 被多条 Connection 重复引用。");
                return false;
            }

            resolvedSockets.Add(socketB);
        }

        int expectedSocketCount =
            layout.Connections.Count * 2;

        if (resolvedSockets.Count != expectedSocketCount ||
            uniqueSockets.Count != expectedSocketCount)
        {
            failureReason = BuildConnectionDoorFailure(
                -1,
                "Both",
                -1,
                "None",
                "None",
                "解析后的唯一 Socket 数量不正确。Resolved=" +
                resolvedSockets.Count + "，Unique=" +
                uniqueSockets.Count + "，Expected=" +
                expectedSocketCount + "。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从权威 CorridorCells 建立纯数据渲染计划。
    /// 走廊地板只来自 CorridorCells；墙候选来自走廊八方向边界，
    /// 并排除最终 FloorCells 与所有房间 Occupied Global Cells。
    /// </summary>
    private bool TryBuildHybridCorridorRenderPlan(
        DungeonLayout layout,
        out List<Vector2Int> corridorFloorCells,
        out List<Vector2Int> corridorWallCells,
        out int occupiedRoomCellCount,
        out string failureReason)
    {
        corridorFloorCells = new List<Vector2Int>();
        corridorWallCells = new List<Vector2Int>();
        occupiedRoomCellCount = 0;
        failureReason = string.Empty;

        if (layout.CorridorCells.Count == 0)
        {
            failureReason =
                "CorridorCells 为空，不能建立 R7.4 走廊。";
            return false;
        }

        HashSet<Vector2Int> occupiedRoomCells =
            new HashSet<Vector2Int>();

        List<Vector2Int> placementCells =
            new List<Vector2Int>();

        bool foundControlledFailureCell = false;
        Vector2Int controlledFailureCell =
            Vector2Int.zero;

        for (int roomIndex = 0;
             roomIndex < layout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null)
            {
                failureReason =
                    "RoomPlacement " + roomIndex +
                    " 是空引用，无法收集 Occupied Global Cells。";
                return false;
            }

            placement.GetOccupiedGlobalCells(
                placementCells);

            for (int cellIndex = 0;
                 cellIndex < placementCells.Count;
                 cellIndex++)
            {
                Vector2Int occupiedCell =
                    placementCells[cellIndex];

                occupiedRoomCells.Add(occupiedCell);

                if (!foundControlledFailureCell &&
                    layout.FloorCells.Contains(occupiedCell) &&
                    !layout.CorridorCells.Contains(occupiedCell))
                {
                    controlledFailureCell = occupiedCell;
                    foundControlledFailureCell = true;
                }
            }
        }

        occupiedRoomCellCount =
            occupiedRoomCells.Count;

        corridorFloorCells.AddRange(
            layout.CorridorCells);

        bool controlledFailureInjected =
            ShouldInjectR74ControlledFailure();

        if (controlledFailureInjected)
        {
            if (!foundControlledFailureCell)
            {
                failureReason =
                    "R7.4 受控失败无法取得一个属于最终 FloorCells 的" +
                    "房间 Occupied Cell；未修改 Layout。";
                return false;
            }

            // 只改本地计划副本，用真实房间占格验证拒绝路径。
            corridorFloorCells.Add(
                controlledFailureCell);
        }

        corridorFloorCells.Sort(
            CompareCellCoordinates);

        HashSet<Vector2Int> uniqueCorridorFloorCells =
            new HashSet<Vector2Int>();

        for (int cellIndex = 0;
             cellIndex < corridorFloorCells.Count;
             cellIndex++)
        {
            Vector2Int corridorCell =
                corridorFloorCells[cellIndex];

            if (!uniqueCorridorFloorCells.Add(
                    corridorCell))
            {
                failureReason =
                    BuildCorridorRenderFailure(
                        corridorCell,
                        layout.FloorCells.Contains(
                            corridorCell),
                        occupiedRoomCells.Contains(
                            corridorCell),
                        "走廊渲染计划出现重复格。" +
                        ControlledFailureNote(
                            controlledFailureInjected,
                            corridorCell,
                            controlledFailureCell));
                return false;
            }

            if (!layout.FloorCells.Contains(
                    corridorCell))
            {
                failureReason =
                    BuildCorridorRenderFailure(
                        corridorCell,
                        false,
                        occupiedRoomCells.Contains(
                            corridorCell),
                        "Corridor Cell 不属于最终 FloorCells。" +
                        ControlledFailureNote(
                            controlledFailureInjected,
                            corridorCell,
                            controlledFailureCell));
                return false;
            }

            if (occupiedRoomCells.Contains(
                    corridorCell))
            {
                failureReason =
                    BuildCorridorRenderFailure(
                        corridorCell,
                        true,
                        true,
                        "Corridor Cell 与房间 Occupied Global Cell 重叠。" +
                        ControlledFailureNote(
                            controlledFailureInjected,
                            corridorCell,
                            controlledFailureCell));
                return false;
            }
        }

        if (uniqueCorridorFloorCells.Count !=
            layout.CorridorCells.Count)
        {
            failureReason =
                "唯一走廊地板数量不等于 Layout.CorridorCells。" +
                " Unique=" +
                uniqueCorridorFloorCells.Count +
                "，Layout=" +
                layout.CorridorCells.Count + "。";
            return false;
        }

        HashSet<Vector2Int> uniqueWallCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int corridorCell in
                 layout.CorridorCells)
        {
            for (int directionIndex = 0;
                 directionIndex < EightDirections.Length;
                 directionIndex++)
            {
                Vector2Int wallCandidate =
                    corridorCell +
                    EightDirections[directionIndex];

                if (layout.FloorCells.Contains(
                        wallCandidate) ||
                    occupiedRoomCells.Contains(
                        wallCandidate))
                {
                    continue;
                }

                uniqueWallCells.Add(wallCandidate);
            }
        }

        if (uniqueWallCells.Count == 0)
        {
            failureReason =
                "走廊边界没有产生任何合法墙候选。";
            return false;
        }

        corridorWallCells.AddRange(uniqueWallCells);
        corridorWallCells.Sort(CompareCellCoordinates);

        for (int wallIndex = 0;
             wallIndex < corridorWallCells.Count;
             wallIndex++)
        {
            Vector2Int wallCell =
                corridorWallCells[wallIndex];

            if (layout.FloorCells.Contains(wallCell) ||
                occupiedRoomCells.Contains(wallCell))
            {
                failureReason =
                    BuildCorridorRenderFailure(
                        wallCell,
                        layout.FloorCells.Contains(wallCell),
                        occupiedRoomCells.Contains(wallCell),
                        "内部错误：走廊墙候选没有通过排除规则。");
                return false;
            }
        }

        return true;
    }

    private bool TryCreateHybridCorridorGeometry(
        IReadOnlyList<Vector2Int> corridorFloorCells,
        IReadOnlyList<Vector2Int> corridorWallCells,
        int layoutSeed,
        Transform dungeonRoot,
        out Transform corridorsRoot,
        out Transform corridorWallsRoot,
        out string failureReason)
    {
        corridorsRoot = null;
        corridorWallsRoot = null;
        failureReason = string.Empty;

        try
        {
            corridorsRoot =
                CreateHybridRoot(
                    "Corridors_Staging_R7.4",
                    dungeonRoot);

            corridorWallsRoot =
                CreateHybridRoot(
                    "CorridorWalls_Staging_R7.4",
                    dungeonRoot);

            corridorsRoot.gameObject.SetActive(false);
            corridorWallsRoot.gameObject.SetActive(false);

            HashSet<Vector2Int> corridorFloorSet =
                new HashSet<Vector2Int>(
                    corridorFloorCells);

            for (int floorIndex = 0;
                 floorIndex < corridorFloorCells.Count;
                 floorIndex++)
            {
                Vector2Int floorCell =
                    corridorFloorCells[floorIndex];

                int floorMask =
                    BuildCardinalNeighbourMask(
                        floorCell,
                        corridorFloorSet);

                Color resolvedFloorColor =
                    corridorVisualProfile == null
                        ? floorColor
                        : corridorVisualProfile
                            .EvaluateFloorColor(
                                floorCell,
                                floorMask,
                                layoutSeed);

                GameObject floorObject = CreateSquare(
                    "CorridorFloor_" +
                    floorCell.x + "_" + floorCell.y,
                    floorCell,
                    resolvedFloorColor,
                    corridorsRoot,
                    -10,
                    false,
                    1f);

                if (corridorVisualProfile != null)
                {
                    ApplyOptionalSprite(
                        floorObject,
                        corridorVisualProfile
                            .GetFloorSprite(floorMask));
                }
            }

            for (int wallIndex = 0;
                 wallIndex < corridorWallCells.Count;
                 wallIndex++)
            {
                Vector2Int wallCell =
                    corridorWallCells[wallIndex];

                int adjacentFloorMask =
                    BuildCardinalNeighbourMask(
                        wallCell,
                        corridorFloorSet);

                Color resolvedWallColor =
                    corridorVisualProfile == null
                        ? wallColor
                        : corridorVisualProfile
                            .EvaluateWallColor(
                                wallCell,
                                adjacentFloorMask,
                                layoutSeed);

                Sprite resolvedWallSprite =
                    corridorVisualProfile == null
                        ? null
                        : corridorVisualProfile
                            .GetWallSprite(
                                adjacentFloorMask);

                if (corridorVisualProfile != null &&
                    corridorVisualProfile
                        .WallOuterVisualInset > 0f)
                {
                    CreateInsetCorridorWall(
                        "CorridorWall_" +
                        wallCell.x + "_" + wallCell.y,
                        wallCell,
                        resolvedWallColor,
                        resolvedWallSprite,
                        corridorWallsRoot,
                        corridorFloorSet,
                        corridorVisualProfile
                            .WallOuterVisualInset);
                }
                else
                {
                    GameObject wallObject = CreateSquare(
                        "CorridorWall_" +
                        wallCell.x + "_" + wallCell.y,
                        wallCell,
                        resolvedWallColor,
                        corridorWallsRoot,
                        0,
                        true,
                        1f);

                    ApplyOptionalSprite(
                        wallObject,
                        resolvedWallSprite);
                }
            }

            return true;
        }
        catch (System.Exception exception)
        {
            DiscardHybridCorridorGeometry(
                corridorsRoot,
                corridorWallsRoot);

            corridorsRoot = null;
            corridorWallsRoot = null;
            failureReason = exception.ToString();
            return false;
        }
    }

    private static int BuildCardinalNeighbourMask(
        Vector2Int cell,
        HashSet<Vector2Int> neighbours)
    {
        int mask = 0;

        if (neighbours.Contains(cell + Vector2Int.up))
        {
            mask |= DungeonCorridorVisualProfile.NorthBit;
        }

        if (neighbours.Contains(cell + Vector2Int.right))
        {
            mask |= DungeonCorridorVisualProfile.EastBit;
        }

        if (neighbours.Contains(cell + Vector2Int.down))
        {
            mask |= DungeonCorridorVisualProfile.SouthBit;
        }

        if (neighbours.Contains(cell + Vector2Int.left))
        {
            mask |= DungeonCorridorVisualProfile.WestBit;
        }

        return mask;
    }

    private static void ApplyOptionalSprite(
        GameObject target,
        Sprite sprite)
    {
        if (target == null || sprite == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer =
            target.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
    }

    /// <summary>
    /// C2 把墙碰撞根与可见子物体分离。根物体仍占完整一格；
    /// 子物体只从没有走廊地板的一侧收进，面向走廊的内缘不动。
    /// </summary>
    private GameObject CreateInsetCorridorWall(
        string objectName,
        Vector2Int wallCell,
        Color color,
        Sprite optionalSprite,
        Transform parent,
        HashSet<Vector2Int> corridorFloorSet,
        float outerInset)
    {
        if (whiteSprite == null)
        {
            CreateWhiteSprite();
        }

        float inset = Mathf.Clamp(
            outerInset,
            0f,
            0.45f);

        GameObject wallRoot =
            new GameObject(objectName);

        wallRoot.transform.SetParent(parent);
        wallRoot.transform.position =
            CellToWorld(wallCell);
        wallRoot.transform.localScale =
            new Vector3(cellSize, cellSize, 1f);

        BoxCollider2D collider =
            wallRoot.AddComponent<BoxCollider2D>();

        collider.size = Vector2.one;
        collider.offset = Vector2.zero;

        int inwardX;
        int inwardY;

        ResolveWallVisualInwardAxes(
            wallCell,
            corridorFloorSet,
            out inwardX,
            out inwardY);

        float scaleX =
            inwardX == 0 ? 1f : 1f - inset;
        float scaleY =
            inwardY == 0 ? 1f : 1f - inset;

        GameObject visual =
            new GameObject("WallVisual_C2");

        visual.transform.SetParent(
            wallRoot.transform,
            false);

        visual.transform.localPosition =
            new Vector3(
                inwardX * inset * 0.5f,
                inwardY * inset * 0.5f,
                0f);

        visual.transform.localScale =
            new Vector3(scaleX, scaleY, 1f);

        SpriteRenderer spriteRenderer =
            visual.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite =
            optionalSprite == null
                ? whiteSprite
                : optionalSprite;

        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 0;

        return wallRoot;
    }

    private static void ResolveWallVisualInwardAxes(
        Vector2Int wallCell,
        HashSet<Vector2Int> corridorFloorSet,
        out int inwardX,
        out int inwardY)
    {
        bool positiveX = false;
        bool negativeX = false;
        bool positiveY = false;
        bool negativeY = false;

        for (int directionIndex = 0;
             directionIndex < EightDirections.Length;
             directionIndex++)
        {
            Vector2Int direction =
                EightDirections[directionIndex];

            if (!corridorFloorSet.Contains(
                    wallCell + direction))
            {
                continue;
            }

            positiveX |= direction.x > 0;
            negativeX |= direction.x < 0;
            positiveY |= direction.y > 0;
            negativeY |= direction.y < 0;
        }

        inwardX = positiveX == negativeX
            ? 0
            : positiveX ? 1 : -1;

        inwardY = positiveY == negativeY
            ? 0
            : positiveY ? 1 : -1;
    }

    private bool TryOpenResolvedConnectionSockets(
        IReadOnlyList<DreamRoomDoorSocket> resolvedSockets,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (resolvedSockets == null)
        {
            failureReason =
                "解析后的 Socket 列表是空引用。";
            return false;
        }

        try
        {
            for (int socketIndex = 0;
                 socketIndex < resolvedSockets.Count;
                 socketIndex++)
            {
                DreamRoomDoorSocket socket =
                    resolvedSockets[socketIndex];

                if (socket == null)
                {
                    throw new MissingReferenceException(
                        "Resolved Socket " + socketIndex +
                        " 在提交前变成空引用。");
                }

                socket.SetOpen(true);
            }

            return true;
        }
        catch (System.Exception exception)
        {
            SetAllHybridInstanceSocketsClosed();
            failureReason = exception.ToString();
            return false;
        }
    }

    private void SetAllHybridInstanceSocketsClosed()
    {
        for (int roomIndex = 0;
             roomIndex < hybridRoomInstanceTemplates.Count;
             roomIndex++)
        {
            DreamRoomTemplate instanceTemplate =
                hybridRoomInstanceTemplates[roomIndex];

            if (instanceTemplate != null)
            {
                instanceTemplate.SetAllSocketsOpen(false);
            }
        }
    }

    private void CreateEmptyHybridCorridorRoots(
        Transform dungeonRoot)
    {
        CreateHybridRoot("Corridors", dungeonRoot);
        CreateHybridRoot("CorridorWalls", dungeonRoot);
    }

    private void DiscardHybridCorridorGeometry(
        Transform corridorsRoot,
        Transform corridorWallsRoot)
    {
        if (corridorsRoot != null)
        {
            corridorsRoot.gameObject.SetActive(false);
            Destroy(corridorsRoot.gameObject);
        }

        if (corridorWallsRoot != null)
        {
            corridorWallsRoot.gameObject.SetActive(false);
            Destroy(corridorWallsRoot.gameObject);
        }
    }

    private bool ShouldInjectR74ControlledFailure()
    {
#if UNITY_EDITOR
        return
            r74InjectInvalidCorridorCellForControlledFailure;
#else
        return false;
#endif
    }

    private string ControlledFailureNote(
        bool controlledFailureInjected,
        Vector2Int inspectedCell,
        Vector2Int controlledFailureCell)
    {
        return controlledFailureInjected &&
               inspectedCell == controlledFailureCell
            ? " R7.4 受控失败已注入；只修改本地预检副本。"
            : string.Empty;
    }

    private string BuildCorridorRenderFailure(
        Vector2Int cell,
        bool belongsToFinalFloor,
        bool belongsToOccupiedRoom,
        string reason)
    {
        return
            "Cell=(" + cell.x + "," + cell.y + ")" +
            " | InFinalFloorCells=" +
            belongsToFinalFloor +
            " | InRoomOccupiedCells=" +
            belongsToOccupiedRoom +
            " | Reason=" + ReadableValue(reason);
    }

    private static int CompareCellCoordinates(
        Vector2Int first,
        Vector2Int second)
    {
        int yComparison = first.y.CompareTo(second.y);

        return yComparison != 0
            ? yComparison
            : first.x.CompareTo(second.x);
    }

    private bool TryResolveConnectionSocket(
        int connectionIndex,
        string connectionSide,
        int roomIndex,
        string socketId,
        string resolutionNote,
        out DreamRoomDoorSocket socket,
        out string failureReason)
    {
        socket = null;
        failureReason = string.Empty;

        if (roomIndex < 0 ||
            roomIndex >= hybridRoomInstanceTemplates.Count)
        {
            failureReason = BuildConnectionDoorFailure(
                connectionIndex,
                connectionSide,
                roomIndex,
                "OutOfRange",
                socketId,
                "Room Index 超出实例列表范围 0～" +
                (hybridRoomInstanceTemplates.Count - 1) + "。" +
                resolutionNote);
            return false;
        }

        DreamRoomTemplate instanceTemplate =
            hybridRoomInstanceTemplates[roomIndex];

        string templateId = instanceTemplate == null
            ? "None"
            : instanceTemplate.TemplateId;

        if (instanceTemplate == null)
        {
            failureReason = BuildConnectionDoorFailure(
                connectionIndex,
                connectionSide,
                roomIndex,
                templateId,
                socketId,
                "房间实例模板是空引用。" + resolutionNote);
            return false;
        }

        if (string.IsNullOrWhiteSpace(socketId))
        {
            failureReason = BuildConnectionDoorFailure(
                connectionIndex,
                connectionSide,
                roomIndex,
                templateId,
                socketId,
                "Connection 缺少 Socket Id。" + resolutionNote);
            return false;
        }

        if (!instanceTemplate.TryGetSocket(
                socketId,
                out socket) ||
            socket == null)
        {
            failureReason = BuildConnectionDoorFailure(
                connectionIndex,
                connectionSide,
                roomIndex,
                templateId,
                socketId,
                "实例 TryGetSocket 返回 false。" +
                resolutionNote);
            return false;
        }

        return true;
    }

    private bool ShouldInjectR73ControlledFailure(
        int connectionIndex)
    {
#if UNITY_EDITOR
        return
            r73InjectMissingSocketForControlledFailure &&
            connectionIndex == 0;
#else
        return false;
#endif
    }

    private string BuildConnectionDoorFailure(
        int connectionIndex,
        string connectionSide,
        int roomIndex,
        string templateId,
        string socketId,
        string reason)
    {
        return
            "ConnectionIndex=" + connectionIndex +
            " | Side=" + ReadableValue(connectionSide) +
            " | RoomIndex=" + roomIndex +
            " | TemplateId=" + ReadableValue(templateId) +
            " | SocketId=" + ReadableValue(socketId) +
            " | Reason=" + ReadableValue(reason);
    }

    private string ReadableValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<empty>"
            : value;
    }

    private Transform CreateHybridRoot(
        string rootName,
        Transform dungeonRoot)
    {
        Transform createdRoot =
            new GameObject(rootName).transform;

        // 与旧逐格对象保持同一世界格原点契约。
        createdRoot.SetParent(dungeonRoot);
        createdRoot.position =
            CellToWorld(Vector2Int.zero);
        createdRoot.rotation = Quaternion.identity;
        createdRoot.localScale = Vector3.one;

        return createdRoot;
    }

    private void BuildFloors(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform floorRoot =
            new GameObject("Floors").transform;

        floorRoot.SetParent(dungeonRoot);

        foreach (Vector2Int cell in layout.FloorCells)
        {
            CreateSquare(
                "Floor_" + cell.x + "_" + cell.y,
                cell,
                floorColor,
                floorRoot,
                -10,
                false,
                1f);
        }
    }

    private void BuildWalls(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform wallRoot =
            new GameObject("Walls").transform;

        wallRoot.SetParent(dungeonRoot);

        HashSet<Vector2Int> wallCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int floorCell in layout.FloorCells)
        {
            for (int i = 0;
                 i < EightDirections.Length;
                 i++)
            {
                Vector2Int neighbour =
                    floorCell + EightDirections[i];

                if (!layout.FloorCells.Contains(neighbour))
                {
                    wallCells.Add(neighbour);
                }
            }
        }

        foreach (Vector2Int wallCell in wallCells)
        {
            CreateSquare(
                "Wall_" + wallCell.x + "_" + wallCell.y,
                wallCell,
                wallColor,
                wallRoot,
                0,
                true,
                1f);
        }
    }

    /// <summary>
    /// PlayerManager、EnemyManager、ItemManager 与 ExitSpawner
    /// 仍可继续使用这个公开工厂；R7.4 不改变其签名。
    /// </summary>
    public GameObject CreateSquare(
        string objectName,
        Vector2Int cell,
        Color color,
        Transform parent,
        int sortingOrder,
        bool addCollider,
        float scaleMultiplier)
    {
        if (whiteSprite == null)
        {
            CreateWhiteSprite();
        }

        GameObject createdObject =
            new GameObject(objectName);

        createdObject.transform.SetParent(parent);
        createdObject.transform.position =
            CellToWorld(cell);

        createdObject.transform.localScale =
            new Vector3(
                cellSize * scaleMultiplier,
                cellSize * scaleMultiplier,
                1f);

        SpriteRenderer spriteRenderer =
            createdObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = whiteSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            BoxCollider2D collider =
                createdObject.AddComponent<BoxCollider2D>();

            collider.size = Vector2.one;
        }

        return createdObject;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            cell.x * cellSize,
            cell.y * cellSize,
            0f);
    }

    private void CreateWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return;
        }

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "RuntimeDungeonWhiteTexture";

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        whiteSprite.name =
            "RuntimeDungeonWhiteSprite";
    }

    private void OnDestroy()
    {
        if (whiteSprite == null)
        {
            return;
        }

        Texture2D texture = whiteSprite.texture;
        Destroy(whiteSprite);

        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
