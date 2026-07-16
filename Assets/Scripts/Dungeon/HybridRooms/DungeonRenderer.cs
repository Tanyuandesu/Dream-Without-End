using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 DungeonLayout 的数据转换成可见的地板、墙壁与碰撞体。
///
/// R7.3：
/// 1. 完整保留现有的逐格程序化渲染。
/// 2. Hybrid Layout 会实例化真实 Room Prefab。
/// 3. 所有实例 Socket 先关闭；全部 Connection 解析通过后再统一开门。
/// 4. 任一连接错误都拒绝整批开门，不猜测替代 Socket，也不部分提交。
/// 5. 走廊与走廊墙仍只建立空容器；正式渲染属于 R7.4。
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

    [Header("显示")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Color floorColor =
        new Color(0.16f, 0.17f, 0.21f);
    [SerializeField] private Color wallColor =
        new Color(0.35f, 0.37f, 0.44f);

    private Sprite whiteSprite;

    private const string R73MissingSocketTestId =
        "__R7.3_MISSING_SOCKET_TEST__";

    /// <summary>
    /// 索引与当前 Hybrid Layout 的 RoomPlacements 完全一致。
    /// R7.3 会使用这份“房间索引 → 实例模板”映射打开连接门。
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
    /// R7.3 的 Hybrid 入口。
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
                    "[DungeonRenderer/R7.3] 收到 Procedural fallback Layout。" +
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
                "[DungeonRenderer/R7.3] Hybrid 房间实例化前校验失败。" +
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
    /// 实例化房间 Prefab，并在全部 Connection 解析通过后统一开门。
    /// 本阶段仍不会绘制走廊。
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
                "[DungeonRenderer/R7.3] Hybrid 房间实例化中止。" +
                " Requested=HybridPrefabRooms," +
                " Effective=ProceduralCells" +
                " | 未提交半成品 Rooms。\n" +
                exception,
                this);

            RenderProceduralCells(layout, dungeonRoot);
            return;
        }

        int openedSocketCount;
        string doorFailureReason;

        bool doorStateCommitted =
            TryResolveAndOpenConnectionSockets(
                layout,
                out openedSocketCount,
                out doorFailureReason);

        roomsRoot.gameObject.SetActive(true);

        // R7.3 继续冻结目标层级；这两个容器在 R7.4 前保持为空。
        CreateHybridRoot("Corridors", dungeonRoot);
        CreateHybridRoot("CorridorWalls", dungeonRoot);

        if (!doorStateCommitted)
        {
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

        int closedUnusedSocketCount =
            Mathf.Max(
                0,
                totalSocketCount - openedSocketCount);

        Debug.Log(
            "[DungeonRenderer/R7.3] Hybrid 房间 Prefab 与 Connection 门状态已提交。" +
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
            " | Corridors=Deferred(R7.4)",
            this);
    }

    /// <summary>
    /// 第一阶段只解析并去重所有实例 Socket；第二阶段才统一开门。
    /// 因此任何 Connection 错误都不会留下“前几条已开、后几条失败”
    /// 的半提交状态。
    /// </summary>
    private bool TryResolveAndOpenConnectionSockets(
        DungeonLayout layout,
        out int openedSocketCount,
        out string failureReason)
    {
        openedSocketCount = 0;
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

        List<DreamRoomDoorSocket> resolvedSockets =
            new List<DreamRoomDoorSocket>(
                layout.Connections.Count * 2);

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

        for (int socketIndex = 0;
             socketIndex < resolvedSockets.Count;
             socketIndex++)
        {
            resolvedSockets[socketIndex].SetOpen(true);
        }

        openedSocketCount = resolvedSockets.Count;
        return true;
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
    /// 仍可继续使用这个公开工厂；R7.3 不改变其签名。
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
