using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// DungeonGenerator 的 R6 Door Socket 与格子 A* 走廊扩展。
///
/// 阶段边界：
/// 1. 复用 R5 的房间摆放与连接图。
/// 2. 每条连接分配两个未重复使用的真实 Door Socket。
/// 3. 从两个门外格之间进行四方向 A*，其他房间 Occupied Cells 为障碍。
/// 4. 把扩宽后的路径写入 Connection 与 DungeonLayout.CorridorCells。
/// 5. 仍不实例化 Prefab、不打开 DoorBlocker、不调用 Renderer、不修改旧 Generate()。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("R6 Door Socket 与 A* 走廊（尚未正式渲染）")]
    [Tooltip(
        "程序化走廊的格子宽度。R3 灰盒门宽为 2，" +
        "因此本阶段默认也使用 2。")]
    [Range(1, 4)]
    [SerializeField]
    private int socketCorridorWidth = 2;

    [Tooltip(
        "一次 R5 摆放／连接图无法完成全部 Socket 与走廊时，" +
        "最多用确定性的派生 Seed 重新生成多少次完整布局。" +
        "失败结果不会返回半成品。")]
    [Min(1)]
    [SerializeField]
    private int socketCorridorMaximumLayoutAttempts = 20;

    [Tooltip(
        "单次 A* 最多扩展的状态数。80x50 地图建议保持 20000。")]
    [Min(100)]
    [SerializeField]
    private int socketCorridorMaximumExpandedNodesPerPath = 20000;

    [Tooltip(
        "A* 每次转弯增加的代价。它只让路径更平直，" +
        "不会改变四方向格子连通规则。")]
    [Min(0)]
    [SerializeField]
    private int socketCorridorTurnPenalty = 2;

    public int SocketCorridorWidth =>
        socketCorridorWidth;

    public int SocketCorridorMaximumLayoutAttempts =>
        socketCorridorMaximumLayoutAttempts;

    public int SocketCorridorMaximumExpandedNodesPerPath =>
        socketCorridorMaximumExpandedNodesPerPath;

    public int SocketCorridorTurnPenalty =>
        socketCorridorTurnPenalty;

    private static readonly Vector2Int[] R6CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };

    /// <summary>
    /// 生成 R6 的“房间摆放＋连接图＋Socket＋实际走廊格”布局。
    ///
    /// 同一个外部 Seed 会重复得到完全相同的最终结果。
    /// 第一次优先使用原 Seed；只有当前完整布局不能路由时，
    /// 才使用确定性的派生 Seed 重建整层，不会返回部分走廊。
    /// </summary>
    public bool TryGenerateSocketCorridorLayout(
        int floorNumber,
        int seed,
        out DungeonLayout layout,
        out string report)
    {
        layout = null;

        List<string> configurationErrors =
            R6GetConfigurationErrors(floorNumber);

        if (configurationErrors.Count > 0)
        {
            report = R6BuildConfigurationErrorReport(
                floorNumber,
                seed,
                configurationErrors);

            return false;
        }

        List<string> attemptFailures =
            new List<string>();

        for (int layoutAttemptIndex = 0;
             layoutAttemptIndex <
             socketCorridorMaximumLayoutAttempts;
             layoutAttemptIndex++)
        {
            int graphSeed =
                R6GetGraphSeedForAttempt(
                    seed,
                    layoutAttemptIndex);

            DungeonLayout graphLayout;
            string graphReport;

            if (!TryGenerateTemplateFirstGraphLayout(
                    floorNumber,
                    graphSeed,
                    out graphLayout,
                    out graphReport))
            {
                attemptFailures.Add(
                    "完整尝试 " +
                    (layoutAttemptIndex + 1) +
                    "（R5 Seed " + graphSeed +
                    "）：R5 失败\n" + graphReport);

                continue;
            }

            HashSet<Vector2Int> corridorCells;
            R6RoutingStatistics statistics;
            string routingFailureReason;

            if (!R6TryAssignSocketsAndRouteAllConnections(
                    graphLayout,
                    graphSeed,
                    out corridorCells,
                    out statistics,
                    out routingFailureReason))
            {
                attemptFailures.Add(
                    "完整尝试 " +
                    (layoutAttemptIndex + 1) +
                    "（R5 Seed " + graphSeed +
                    "）：" + routingFailureReason);

                continue;
            }

            DungeonLayout candidateLayout =
                DungeonLayout.CreateHybrid(
                    graphLayout.RoomPlacements,
                    corridorCells,
                    graphLayout.Connections,
                    graphLayout.StartCell,
                    graphLayout.ExitCell,
                    seed);

            List<string> validationErrors =
                candidateLayout.GetValidationErrors();

            validationErrors.AddRange(
                GetSocketCorridorValidationErrors(
                    candidateLayout));

            if (validationErrors.Count > 0)
            {
                attemptFailures.Add(
                    "完整尝试 " +
                    (layoutAttemptIndex + 1) +
                    "（R5 Seed " + graphSeed +
                    "）：完成后数据校验失败\n" +
                    R6JoinErrors(validationErrors));

                continue;
            }

            layout = candidateLayout;
            report = R6BuildSuccessReport(
                layout,
                floorNumber,
                seed,
                graphSeed,
                layoutAttemptIndex + 1,
                statistics) +
                "\n" +
                R941BuildResolvedRoleReport(
                    layout,
                    floorNumber,
                    templateFirstRoomCatalog.CatalogId) +
                "\n" +
                R942BuildResolvedRareReport(
                    layout,
                    floorNumber,
                    templateFirstRoomCatalog) +
                "\n" +
                R943BuildResolvedCoreItemReport(
                    layout,
                    floorNumber,
                    templateFirstRoomCatalog) +
                "\n" +
                R944BuildResolvedSpecialReport(
                    layout,
                    floorNumber,
                    templateFirstRoomCatalog);

            string roleFallbackWarning =
                R941BuildFallbackWarning(
                    layout,
                    floorNumber,
                    templateFirstRoomCatalog.CatalogId);

            if (!string.IsNullOrEmpty(
                    roleFallbackWarning))
            {
                Debug.LogWarning(
                    roleFallbackWarning,
                    this);
            }

            return true;
        }

        report = R6BuildFailureReport(
            floorNumber,
            seed,
            attemptFailures);

        return false;
    }

    /// <summary>
    /// 对一份预期已经完成 R6 的布局执行独立数据校验。
    /// 该方法只读取数据，不修改 Layout、Connection 或 Prefab。
    /// </summary>
    public List<string> GetSocketCorridorValidationErrors(
        DungeonLayout layout)
    {
        List<string> errors = new List<string>();

        if (layout == null)
        {
            errors.Add("R6 Layout 不能为空。");
            return errors;
        }

        if (layout.RoomPlacements.Count < 2)
        {
            errors.Add("R6 至少需要两个 RoomPlacement。");
            return errors;
        }

        if (layout.Connections.Count == 0)
        {
            errors.Add("R6 Connections 不能为空。");
            return errors;
        }

        HashSet<Vector2Int> occupiedRoomCells =
            R6CollectOccupiedRoomCells(
                layout.RoomPlacements);

        HashSet<Vector2Int> expectedCorridorCells =
            new HashSet<Vector2Int>();

        HashSet<string> usedSocketKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                layout.Connections[connectionIndex];

            if (connection == null)
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 是空引用。");
                continue;
            }

            List<string> connectionErrors =
                connection.GetValidationErrors(
                    layout.RoomPlacements.Count,
                    requireAssignedSockets: true,
                    requireCorridor: true);

            for (int errorIndex = 0;
                 errorIndex < connectionErrors.Count;
                 errorIndex++)
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    "：" + connectionErrors[errorIndex]);
            }

            if (connection.RoomAIndex < 0 ||
                connection.RoomBIndex < 0 ||
                connection.RoomAIndex >=
                layout.RoomPlacements.Count ||
                connection.RoomBIndex >=
                layout.RoomPlacements.Count)
            {
                continue;
            }

            DreamRoomPlacement placementA =
                layout.RoomPlacements[
                    connection.RoomAIndex];

            DreamRoomPlacement placementB =
                layout.RoomPlacements[
                    connection.RoomBIndex];

            DreamRoomDoorSocket socketA;
            DreamRoomDoorSocket socketB;

            bool foundSocketA =
                R6TryResolveSocket(
                    placementA,
                    connection.SocketAId,
                    out socketA);

            bool foundSocketB =
                R6TryResolveSocket(
                    placementB,
                    connection.SocketBId,
                    out socketB);

            if (!foundSocketA)
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 的 Room A 找不到 Socket '" +
                    connection.SocketAId + "'。");
            }

            if (!foundSocketB)
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 的 Room B 找不到 Socket '" +
                    connection.SocketBId + "'。");
            }

            if (foundSocketA)
            {
                string keyA = R6BuildSocketKey(
                    connection.RoomAIndex,
                    socketA.SocketId);

                if (!usedSocketKeys.Add(keyA))
                {
                    errors.Add(
                        "Room " + connection.RoomAIndex +
                        " 的 Socket '" + socketA.SocketId +
                        "' 被多条连接重复使用。");
                }

                if (socketA.DoorWidthInCells <
                    socketCorridorWidth)
                {
                    errors.Add(
                        "Connection " + connectionIndex +
                        " 的 Socket A 门宽小于走廊宽度。");
                }
            }

            if (foundSocketB)
            {
                string keyB = R6BuildSocketKey(
                    connection.RoomBIndex,
                    socketB.SocketId);

                if (!usedSocketKeys.Add(keyB))
                {
                    errors.Add(
                        "Room " + connection.RoomBIndex +
                        " 的 Socket '" + socketB.SocketId +
                        "' 被多条连接重复使用。");
                }

                if (socketB.DoorWidthInCells <
                    socketCorridorWidth)
                {
                    errors.Add(
                        "Connection " + connectionIndex +
                        " 的 Socket B 门宽小于走廊宽度。");
                }
            }

            HashSet<Vector2Int> connectionCells =
                new HashSet<Vector2Int>(
                    connection.CorridorCells);

            expectedCorridorCells.UnionWith(
                connectionCells);

            if (connectionCells.Count > 0 &&
                R6CountReachableCells(
                    connectionCells,
                    R6GetFirstCell(connectionCells)) !=
                connectionCells.Count)
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 的 Corridor Cells 不是一个四方向连续区域。");
            }

            foreach (Vector2Int cell in connectionCells)
            {
                if (!R6IsInsideMap(cell))
                {
                    errors.Add(
                        "Connection " + connectionIndex +
                        " 的走廊格 " + cell +
                        " 超出 R4 地图范围。");
                    break;
                }

                if (occupiedRoomCells.Contains(cell))
                {
                    errors.Add(
                        "Connection " + connectionIndex +
                        " 的走廊格 " + cell +
                        " 穿入了房间 Occupied Cells。");
                    break;
                }
            }

            if (foundSocketA &&
                !R6CorridorContainsSocketLane(
                    placementA,
                    socketA,
                    connectionCells))
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 的走廊没有覆盖 Socket A 的门外宽度。");
            }

            if (foundSocketB &&
                !R6CorridorContainsSocketLane(
                    placementB,
                    socketB,
                    connectionCells))
            {
                errors.Add(
                    "Connection " + connectionIndex +
                    " 的走廊没有覆盖 Socket B 的门外宽度。");
            }
        }

        if (!layout.CorridorCells.SetEquals(
                expectedCorridorCells))
        {
            errors.Add(
                "DungeonLayout.CorridorCells 必须等于全部 " +
                "Connection.CorridorCells 的合集。");
        }

        if (layout.FloorCells.Count > 0)
        {
            int reachableFloorCells =
                R6CountReachableCells(
                    layout.FloorCells,
                    layout.StartCell);

            if (reachableFloorCells !=
                layout.FloorCells.Count)
            {
                errors.Add(
                    "最终 FloorCells 没有全部连通：" +
                    reachableFloorCells + "/" +
                    layout.FloorCells.Count + "。");
            }
        }

        return errors;
    }

    private List<string> R6GetConfigurationErrors(
        int floorNumber)
    {
        List<string> errors = new List<string>();

        if (floorNumber < 1)
        {
            errors.Add("Floor Number 必须至少为 1。");
        }

        if (socketCorridorWidth < 1 ||
            socketCorridorWidth > 4)
        {
            errors.Add("Corridor Width 必须位于 1～4。");
        }

        if (!Enum.IsDefined(
                typeof(DungeonCorridorWidthMode),
                socketCorridorWidthMode))
        {
            errors.Add(
                "Corridor Width Mode 不是已知枚举值。");
        }

        if (socketCorridorWidthMode ==
            DungeonCorridorWidthMode.Mixed1And2)
        {
            if (socketCorridorWidth != 2)
            {
                errors.Add(
                    "Mixed1And2 要求 Corridor Width 保持为 2；" +
                    "一格宽仅由 Profile 在安全包络内收窄。");
            }

            if (mixedCorridorDoorApronLength < 1)
            {
                errors.Add(
                    "Mixed Door Apron Length 必须至少为 1。");
            }

            if (mixedCorridorCornerRadius < 0 ||
                mixedCorridorJunctionRadius < 0)
            {
                errors.Add(
                    "Mixed Corner／Junction Radius 不能小于 0。");
            }

            if (mixedCorridorMinimumNarrowRunLength < 1)
            {
                errors.Add(
                    "Mixed Minimum Narrow Run Length 必须至少为 1。");
            }
        }

        if (socketCorridorMaximumLayoutAttempts < 1)
        {
            errors.Add(
                "Maximum Layout Attempts 必须至少为 1。");
        }

        if (socketCorridorMaximumExpandedNodesPerPath < 100)
        {
            errors.Add(
                "Maximum Expanded Nodes Per Path 必须至少为 100。");
        }

        if (socketCorridorTurnPenalty < 0)
        {
            errors.Add("Turn Penalty 不能小于 0。");
        }

        if (templateFirstRoomCatalog == null)
        {
            errors.Add(
                "Template First Room Catalog 不能为空。" +
                "请继续使用 R3 的 RoomCatalog_Graybox。");
            return errors;
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        List<DreamRoomTemplate> eligibleTemplates =
            new List<DreamRoomTemplate>();

        templateFirstRoomCatalog.GetEligibleTemplates(
            floorNumber,
            null,
            eligibleTemplates);

        for (int templateIndex = 0;
             templateIndex < eligibleTemplates.Count;
             templateIndex++)
        {
            DreamRoomTemplate template =
                eligibleTemplates[templateIndex];

            if (template == null)
            {
                continue;
            }

            int compatibleSocketCount = 0;

            IReadOnlyList<DreamRoomDoorSocket>
                templateSockets = template.DoorSockets;

            if (templateSockets == null)
            {
                errors.Add(
                    "模板 '" + template.TemplateId +
                    "' 的 Door Sockets 列表为空引用。");
                continue;
            }

            for (int socketIndex = 0;
                 socketIndex < templateSockets.Count;
                 socketIndex++)
            {
                DreamRoomDoorSocket socket =
                    templateSockets[socketIndex];

                if (socket != null &&
                    socket.DoorWidthInCells >=
                    socketCorridorWidth)
                {
                    compatibleSocketCount++;
                }
            }

            if (compatibleSocketCount == 0)
            {
                errors.Add(
                    "模板 '" + template.TemplateId +
                    "' 没有门宽大于等于 " +
                    socketCorridorWidth +
                    " 的 Socket。");
            }
        }

        return errors;
    }

    private bool R6TryAssignSocketsAndRouteAllConnections(
        DungeonLayout graphLayout,
        int graphSeed,
        out HashSet<Vector2Int> allCorridorCells,
        out R6RoutingStatistics statistics,
        out string failureReason)
    {
        allCorridorCells = new HashSet<Vector2Int>();
        statistics = new R6RoutingStatistics();
        failureReason = string.Empty;

        Dictionary<int, List<Vector2Int>>
            routedCenterlines =
                new Dictionary<int, List<Vector2Int>>();

        IReadOnlyList<DreamRoomPlacement> placements =
            graphLayout.RoomPlacements;

        IReadOnlyList<DreamRoomConnection> connections =
            graphLayout.Connections;

        int[] roomDegrees =
            R6CalculateRoomDegrees(
                placements.Count,
                connections);

        for (int roomIndex = 0;
             roomIndex < placements.Count;
             roomIndex++)
        {
            int compatibleSockets =
                R6CountCompatibleSockets(
                    placements[roomIndex]);

            if (compatibleSockets < roomDegrees[roomIndex])
            {
                failureReason =
                    "Room " + roomIndex + "（" +
                    placements[roomIndex].Template.TemplateId +
                    "）在连接图中的 Degree 为 " +
                    roomDegrees[roomIndex] +
                    "，但只有 " + compatibleSockets +
                    " 个未重复可用且门宽兼容的 Socket。";

                return false;
            }
        }

        HashSet<Vector2Int> occupiedRoomCells =
            R6CollectOccupiedRoomCells(placements);

        HashSet<string> usedSocketKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        List<int> routingOrder =
            R6BuildConnectionRoutingOrder(
                connections,
                roomDegrees);

        for (int orderIndex = 0;
             orderIndex < routingOrder.Count;
             orderIndex++)
        {
            int connectionIndex =
                routingOrder[orderIndex];

            DreamRoomConnection connection =
                graphLayout.Connections[
                    connectionIndex];

            DreamRoomPlacement placementA =
                placements[connection.RoomAIndex];

            DreamRoomPlacement placementB =
                placements[connection.RoomBIndex];

            List<R6SocketPairCandidate> candidates =
                R6BuildSocketPairCandidates(
                    connection.RoomAIndex,
                    placementA,
                    connection.RoomBIndex,
                    placementB,
                    usedSocketKeys);

            if (candidates.Count == 0)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    "（Room " + connection.RoomAIndex +
                    " ↔ Room " + connection.RoomBIndex +
                    "）没有剩余的兼容 Socket Pair。";

                return false;
            }

            bool routed = false;
            int totalExpandedForConnection = 0;
            string lastPathFailure = string.Empty;

            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                R6SocketPairCandidate candidate =
                    candidates[candidateIndex];

                List<Vector2Int> centerline;
                List<Vector2Int> expandedCells;
                int expandedNodeCount;

                int pathSeed = R5DeriveSeed(
                    graphSeed,
                    unchecked(
                        0x6A09E667u +
                        (uint)connectionIndex * 131u +
                        (uint)candidateIndex * 977u));

                if (!R6TryFindCorridorPath(
                        candidate,
                        occupiedRoomCells,
                        pathSeed,
                        out centerline,
                        out expandedCells,
                        out expandedNodeCount,
                        out lastPathFailure))
                {
                    totalExpandedForConnection +=
                        expandedNodeCount;
                    continue;
                }

                totalExpandedForConnection +=
                    expandedNodeCount;

                connection.AssignSockets(
                    candidate.SocketA.Socket.SocketId,
                    candidate.SocketB.Socket.SocketId);

                connection.SetCorridorCells(
                    expandedCells);

                routedCenterlines[connectionIndex] =
                    new List<Vector2Int>(centerline);

                usedSocketKeys.Add(
                    R6BuildSocketKey(
                        connection.RoomAIndex,
                        candidate.SocketA.Socket.SocketId));

                usedSocketKeys.Add(
                    R6BuildSocketKey(
                        connection.RoomBIndex,
                        candidate.SocketB.Socket.SocketId));

                int reusedCellCount = 0;

                for (int cellIndex = 0;
                     cellIndex < expandedCells.Count;
                     cellIndex++)
                {
                    if (!allCorridorCells.Add(
                            expandedCells[cellIndex]))
                    {
                        reusedCellCount++;
                    }
                }

                statistics.TotalExpandedNodes +=
                    totalExpandedForConnection;

                statistics.TotalCenterlineCells +=
                    centerline.Count;

                statistics.ReusedCorridorCells +=
                    reusedCellCount;

                statistics.ConnectionSummaries.Add(
                    new R6ConnectionRoutingSummary(
                        connectionIndex,
                        connection.RoomAIndex,
                        connection.RoomBIndex,
                        candidate.SocketA.Socket.SocketId,
                        candidate.SocketB.Socket.SocketId,
                        placementA.GetRotatedDirection(
                            candidate.SocketA.Socket),
                        placementB.GetRotatedDirection(
                            candidate.SocketB.Socket),
                        centerline.Count,
                        expandedCells.Count,
                        candidateIndex + 1));

                routed = true;
                break;
            }

            if (!routed)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    "（Room " + connection.RoomAIndex +
                    " ↔ Room " + connection.RoomBIndex +
                    "）尝试 " + candidates.Count +
                    " 组 Socket 后仍无合法 A* 路径。最后原因：" +
                    lastPathFailure;

                return false;
            }
        }

        string widthProfileFailure;

        if (!R6TryApplyCorridorWidthProfile(
                graphLayout,
                routedCenterlines,
                occupiedRoomCells,
                allCorridorCells,
                statistics,
                out widthProfileFailure))
        {
            failureReason =
                "Corridor Width Profile 应用失败：" +
                widthProfileFailure;
            return false;
        }

        statistics.UniqueCorridorCells =
            allCorridorCells.Count;

        return true;
    }

    private List<R6SocketPairCandidate>
        R6BuildSocketPairCandidates(
            int roomAIndex,
            DreamRoomPlacement placementA,
            int roomBIndex,
            DreamRoomPlacement placementB,
            HashSet<string> usedSocketKeys)
    {
        List<R6SocketChoice> choicesA =
            R6BuildSocketChoices(
                roomAIndex,
                placementA,
                usedSocketKeys);

        List<R6SocketChoice> choicesB =
            R6BuildSocketChoices(
                roomBIndex,
                placementB,
                usedSocketKeys);

        List<R6SocketPairCandidate> candidates =
            new List<R6SocketPairCandidate>();

        for (int firstIndex = 0;
             firstIndex < choicesA.Count;
             firstIndex++)
        {
            for (int secondIndex = 0;
                 secondIndex < choicesB.Count;
                 secondIndex++)
            {
                R6SocketChoice first =
                    choicesA[firstIndex];

                R6SocketChoice second =
                    choicesB[secondIndex];

                long score =
                    R6GetSocketPairScore(
                        placementA,
                        first,
                        placementB,
                        second);

                candidates.Add(
                    new R6SocketPairCandidate(
                        first,
                        second,
                        score));
            }
        }

        candidates.Sort(
            R6CompareSocketPairCandidates);

        return candidates;
    }

    private List<R6SocketChoice> R6BuildSocketChoices(
        int roomIndex,
        DreamRoomPlacement placement,
        HashSet<string> usedSocketKeys)
    {
        List<R6SocketChoice> choices =
            new List<R6SocketChoice>();

        if (placement == null ||
            placement.Template == null)
        {
            return choices;
        }

        IReadOnlyList<DreamRoomDoorSocket> sockets =
            placement.Template.DoorSockets;

        if (sockets == null)
        {
            return choices;
        }

        for (int socketIndex = 0;
             socketIndex < sockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                sockets[socketIndex];

            if (socket == null ||
                socket.DoorWidthInCells <
                socketCorridorWidth)
            {
                continue;
            }

            string socketKey =
                R6BuildSocketKey(
                    roomIndex,
                    socket.SocketId);

            if (usedSocketKeys.Contains(socketKey))
            {
                continue;
            }

            List<Vector2Int> anchors =
                R6GetCompatibleRouteAnchors(
                    placement,
                    socket);

            for (int anchorIndex = 0;
                 anchorIndex < anchors.Count;
                 anchorIndex++)
            {
                choices.Add(
                    new R6SocketChoice(
                        roomIndex,
                        socket,
                        anchors[anchorIndex],
                        placement.GetRotatedDirection(
                            socket)));
            }
        }

        return choices;
    }

    private List<Vector2Int> R6GetCompatibleRouteAnchors(
        DreamRoomPlacement placement,
        DreamRoomDoorSocket socket)
    {
        List<Vector2Int> outsideCells =
            new List<Vector2Int>();

        placement.GetSocketOutsideCells(
            socket,
            outsideCells);

        HashSet<Vector2Int> outsideSet =
            new HashSet<Vector2Int>(outsideCells);

        DreamRoomDoorDirection direction =
            placement.GetRotatedDirection(socket);

        List<Vector2Int> anchors =
            new List<Vector2Int>();

        for (int candidateIndex = 0;
             candidateIndex < outsideCells.Count;
             candidateIndex++)
        {
            Vector2Int candidate =
                outsideCells[candidateIndex];

            List<Vector2Int> laneCells =
                new List<Vector2Int>();

            R6CollectWidthCells(
                candidate,
                direction.ToCellOffset(),
                socketCorridorWidth,
                laneCells,
                null);

            bool allBelongToDoor = true;

            for (int laneIndex = 0;
                 laneIndex < laneCells.Count;
                 laneIndex++)
            {
                if (!outsideSet.Contains(
                        laneCells[laneIndex]))
                {
                    allBelongToDoor = false;
                    break;
                }
            }

            if (allBelongToDoor &&
                !anchors.Contains(candidate))
            {
                anchors.Add(candidate);
            }
        }

        anchors.Sort(R6CompareCells);
        return anchors;
    }

    private bool R6TryFindCorridorPath(
        R6SocketPairCandidate candidate,
        HashSet<Vector2Int> occupiedRoomCells,
        int pathSeed,
        out List<Vector2Int> centerline,
        out List<Vector2Int> expandedCells,
        out int expandedNodeCount,
        out string failureReason)
    {
        centerline = new List<Vector2Int>();
        expandedCells = new List<Vector2Int>();
        expandedNodeCount = 0;
        failureReason = string.Empty;

        Vector2Int startOutside =
            candidate.SocketA.OutsideAnchor;

        Vector2Int endOutside =
            candidate.SocketB.OutsideAnchor;

        Vector2Int startDirection =
            candidate.SocketA.Direction.ToCellOffset();

        Vector2Int endOutwardDirection =
            candidate.SocketB.Direction.ToCellOffset();

        int initialDirectionIndex =
            R6GetDirectionIndex(startDirection);

        int finalDirectionIndex =
            R6GetDirectionIndex(
                -endOutwardDirection);

        if (initialDirectionIndex < 0 ||
            finalDirectionIndex < 0)
        {
            failureReason = "Socket 方向不是四方向单位向量。";
            return false;
        }

        if (startOutside == endOutside)
        {
            if (initialDirectionIndex !=
                finalDirectionIndex)
            {
                failureReason =
                    "两个 Socket 共用同一门外格，但朝向不能直接衔接。";
                return false;
            }

            centerline.Add(startOutside);

            R6CollectWidthCells(
                startOutside,
                startDirection,
                socketCorridorWidth,
                expandedCells,
                null);

            if (!R6CanUseCells(
                    expandedCells,
                    occupiedRoomCells))
            {
                failureReason =
                    "两个 Socket 的共用门外格没有足够走廊宽度。";

                centerline.Clear();
                expandedCells.Clear();
                return false;
            }

            return true;
        }

        R6PathState initialState =
            new R6PathState(
                startOutside,
                initialDirectionIndex);

        Dictionary<R6PathState, int> gScores =
            new Dictionary<R6PathState, int>();

        Dictionary<R6PathState, R6PathState> parents =
            new Dictionary<R6PathState, R6PathState>();

        R6OpenHeap open = new R6OpenHeap();

        int initialHeuristic =
            R6ManhattanDistance(
                startOutside,
                endOutside) * 10;

        int sequence = 0;

        gScores.Add(initialState, 0);
        open.Push(
            new R6OpenEntry(
                initialState,
                initialHeuristic,
                initialHeuristic,
                sequence++));

        int directionOrderOffset =
            R6PositiveModulo(pathSeed, 4);

        R6PathState foundGoalState =
            default(R6PathState);

        bool foundGoal = false;

        while (open.Count > 0)
        {
            R6OpenEntry currentEntry =
                open.Pop();

            int recordedG;

            if (!gScores.TryGetValue(
                    currentEntry.State,
                    out recordedG) ||
                recordedG + currentEntry.Heuristic !=
                currentEntry.TotalCost)
            {
                continue;
            }

            expandedNodeCount++;

            if (expandedNodeCount >
                socketCorridorMaximumExpandedNodesPerPath)
            {
                failureReason =
                    "A* 超过 Maximum Expanded Nodes Per Path（" +
                    socketCorridorMaximumExpandedNodesPerPath +
                    "）。";
                return false;
            }

            if (currentEntry.State.Cell == endOutside &&
                currentEntry.State.IncomingDirectionIndex ==
                finalDirectionIndex)
            {
                foundGoalState = currentEntry.State;
                foundGoal = true;
                break;
            }

            for (int directionStep = 0;
                 directionStep < 4;
                 directionStep++)
            {
                int directionIndex =
                    (directionOrderOffset +
                     directionStep) % 4;

                if (currentEntry.State.Cell ==
                        startOutside &&
                    directionIndex !=
                        initialDirectionIndex)
                {
                    continue;
                }

                Vector2Int neighbour =
                    currentEntry.State.Cell +
                    R6CardinalDirections[
                        directionIndex];

                if (neighbour == startOutside)
                {
                    continue;
                }

                if (neighbour == endOutside &&
                    directionIndex !=
                    finalDirectionIndex)
                {
                    continue;
                }

                if (!R6CanUseTransition(
                        currentEntry.State.Cell,
                        neighbour,
                        currentEntry.State.
                            IncomingDirectionIndex,
                        directionIndex,
                        occupiedRoomCells))
                {
                    continue;
                }

                int turnCost =
                    currentEntry.State.
                        IncomingDirectionIndex ==
                    directionIndex
                        ? 0
                        : socketCorridorTurnPenalty;

                int tentativeG =
                    recordedG + 10 + turnCost;

                R6PathState neighbourState =
                    new R6PathState(
                        neighbour,
                        directionIndex);

                int existingG;

                if (gScores.TryGetValue(
                        neighbourState,
                        out existingG) &&
                    tentativeG >= existingG)
                {
                    continue;
                }

                gScores[neighbourState] =
                    tentativeG;

                parents[neighbourState] =
                    currentEntry.State;

                int heuristic =
                    R6ManhattanDistance(
                        neighbour,
                        endOutside) * 10;

                open.Push(
                    new R6OpenEntry(
                        neighbourState,
                        tentativeG + heuristic,
                        heuristic,
                        sequence++));
            }
        }

        if (!foundGoal)
        {
            failureReason =
                "A* 在地图范围内找不到不穿房间的路径。";
            return false;
        }

        List<Vector2Int> internalPath =
            R6ReconstructPath(
                initialState,
                foundGoalState,
                parents);

        for (int i = 0;
             i < internalPath.Count;
             i++)
        {
            R6AddCellWithoutDuplicate(
                centerline,
                internalPath[i]);
        }

        R6ExpandCenterline(
            centerline,
            expandedCells);

        for (int cellIndex = 0;
             cellIndex < expandedCells.Count;
             cellIndex++)
        {
            Vector2Int cell =
                expandedCells[cellIndex];

            if (!R6IsInsideMap(cell) ||
                occupiedRoomCells.Contains(cell))
            {
                failureReason =
                    "扩宽后的走廊格 " + cell +
                    " 越界或穿入房间。";

                centerline.Clear();
                expandedCells.Clear();
                return false;
            }
        }

        return true;
    }

    private bool R6CanUseTransition(
        Vector2Int current,
        Vector2Int next,
        int incomingDirectionIndex,
        int outgoingDirectionIndex,
        HashSet<Vector2Int> occupiedRoomCells)
    {
        if (incomingDirectionIndex < 0 ||
            incomingDirectionIndex >=
            R6CardinalDirections.Length ||
            outgoingDirectionIndex < 0 ||
            outgoingDirectionIndex >=
            R6CardinalDirections.Length)
        {
            return false;
        }

        if (R6CardinalDirections[
                incomingDirectionIndex] +
            R6CardinalDirections[
                outgoingDirectionIndex] ==
            Vector2Int.zero)
        {
            return false;
        }

        if (!R6CanUseSegment(
                current,
                next,
                occupiedRoomCells))
        {
            return false;
        }

        if (incomingDirectionIndex ==
            outgoingDirectionIndex)
        {
            return true;
        }

        List<Vector2Int> cornerCells =
            new List<Vector2Int>();

        R6CollectCornerCells(
            current,
            R6CardinalDirections[
                incomingDirectionIndex],
            R6CardinalDirections[
                outgoingDirectionIndex],
            cornerCells,
            null);

        return R6CanUseCells(
            cornerCells,
            occupiedRoomCells);
    }

    private bool R6CanUseSegment(
        Vector2Int first,
        Vector2Int second,
        HashSet<Vector2Int> occupiedRoomCells)
    {
        Vector2Int direction = second - first;

        if (R6GetDirectionIndex(direction) < 0)
        {
            return false;
        }

        List<Vector2Int> segmentCells =
            new List<Vector2Int>();

        R6CollectWidthCells(
            first,
            direction,
            socketCorridorWidth,
            segmentCells,
            null);

        R6CollectWidthCells(
            second,
            direction,
            socketCorridorWidth,
            segmentCells,
            null);

        return R6CanUseCells(
            segmentCells,
            occupiedRoomCells);
    }

    private bool R6CanUseCells(
        List<Vector2Int> cells,
        HashSet<Vector2Int> occupiedRoomCells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (!R6IsInsideMap(cells[i]) ||
                occupiedRoomCells.Contains(cells[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void R6ExpandCenterline(
        List<Vector2Int> centerline,
        List<Vector2Int> results)
    {
        results.Clear();

        HashSet<Vector2Int> used =
            new HashSet<Vector2Int>();

        if (centerline.Count == 1)
        {
            R6CollectWidthCells(
                centerline[0],
                Vector2Int.right,
                socketCorridorWidth,
                results,
                used);
            return;
        }

        for (int segmentIndex = 0;
             segmentIndex < centerline.Count - 1;
             segmentIndex++)
        {
            Vector2Int direction =
                centerline[segmentIndex + 1] -
                centerline[segmentIndex];

            R6CollectWidthCells(
                centerline[segmentIndex],
                direction,
                socketCorridorWidth,
                results,
                used);

            R6CollectWidthCells(
                centerline[segmentIndex + 1],
                direction,
                socketCorridorWidth,
                results,
                used);
        }

        for (int cellIndex = 1;
             cellIndex < centerline.Count - 1;
             cellIndex++)
        {
            Vector2Int incoming =
                centerline[cellIndex] -
                centerline[cellIndex - 1];

            Vector2Int outgoing =
                centerline[cellIndex + 1] -
                centerline[cellIndex];

            if (incoming == outgoing)
            {
                continue;
            }

            R6CollectCornerCells(
                centerline[cellIndex],
                incoming,
                outgoing,
                results,
                used);
        }
    }

    private static void R6CollectWidthCells(
        Vector2Int anchor,
        Vector2Int travelDirection,
        int width,
        List<Vector2Int> results,
        HashSet<Vector2Int> used)
    {
        Vector2Int sideways =
            travelDirection.x != 0
                ? Vector2Int.up
                : Vector2Int.right;

        int startOffset = -(width / 2);

        for (int i = 0; i < width; i++)
        {
            R6AddUniqueCell(
                anchor +
                sideways * (startOffset + i),
                results,
                used);
        }
    }

    private void R6CollectCornerCells(
        Vector2Int anchor,
        Vector2Int incomingDirection,
        Vector2Int outgoingDirection,
        List<Vector2Int> results,
        HashSet<Vector2Int> used)
    {
        Vector2Int incomingSideways =
            incomingDirection.x != 0
                ? Vector2Int.up
                : Vector2Int.right;

        Vector2Int outgoingSideways =
            outgoingDirection.x != 0
                ? Vector2Int.up
                : Vector2Int.right;

        int startOffset = -(socketCorridorWidth / 2);

        for (int first = 0;
             first < socketCorridorWidth;
             first++)
        {
            for (int second = 0;
                 second < socketCorridorWidth;
                 second++)
            {
                Vector2Int cell =
                    anchor +
                    incomingSideways *
                    (startOffset + first) +
                    outgoingSideways *
                    (startOffset + second);

                R6AddUniqueCell(
                    cell,
                    results,
                    used);
            }
        }
    }

    private static void R6AddUniqueCell(
        Vector2Int cell,
        List<Vector2Int> results,
        HashSet<Vector2Int> used)
    {
        if (used == null)
        {
            if (!results.Contains(cell))
            {
                results.Add(cell);
            }

            return;
        }

        if (used.Add(cell))
        {
            results.Add(cell);
        }
    }

    private static List<Vector2Int> R6ReconstructPath(
        R6PathState initialState,
        R6PathState goalState,
        Dictionary<R6PathState, R6PathState> parents)
    {
        List<Vector2Int> reversed =
            new List<Vector2Int>();

        R6PathState current = goalState;
        reversed.Add(current.Cell);

        while (!current.Equals(initialState))
        {
            R6PathState parent;

            if (!parents.TryGetValue(current, out parent))
            {
                break;
            }

            current = parent;
            reversed.Add(current.Cell);
        }

        reversed.Reverse();
        return reversed;
    }

    private bool R6CorridorContainsSocketLane(
        DreamRoomPlacement placement,
        DreamRoomDoorSocket socket,
        HashSet<Vector2Int> corridorCells)
    {
        List<Vector2Int> anchors =
            R6GetCompatibleRouteAnchors(
                placement,
                socket);

        DreamRoomDoorDirection direction =
            placement.GetRotatedDirection(socket);

        for (int anchorIndex = 0;
             anchorIndex < anchors.Count;
             anchorIndex++)
        {
            List<Vector2Int> laneCells =
                new List<Vector2Int>();

            R6CollectWidthCells(
                anchors[anchorIndex],
                direction.ToCellOffset(),
                socketCorridorWidth,
                laneCells,
                null);

            bool containsAll = true;

            for (int laneIndex = 0;
                 laneIndex < laneCells.Count;
                 laneIndex++)
            {
                if (!corridorCells.Contains(
                        laneCells[laneIndex]))
                {
                    containsAll = false;
                    break;
                }
            }

            if (containsAll)
            {
                return true;
            }
        }

        return false;
    }

    private static bool R6TryResolveSocket(
        DreamRoomPlacement placement,
        string socketId,
        out DreamRoomDoorSocket socket)
    {
        socket = null;

        return placement != null &&
               placement.Template != null &&
               placement.Template.TryGetSocket(
                   socketId,
                   out socket);
    }

    private int R6CountCompatibleSockets(
        DreamRoomPlacement placement)
    {
        if (placement == null ||
            placement.Template == null)
        {
            return 0;
        }

        HashSet<string> socketIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<DreamRoomDoorSocket> sockets =
            placement.Template.DoorSockets;

        if (sockets == null)
        {
            return 0;
        }

        for (int i = 0;
             i < sockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                sockets[i];

            if (socket != null &&
                socket.DoorWidthInCells >=
                socketCorridorWidth)
            {
                socketIds.Add(socket.SocketId);
            }
        }

        return socketIds.Count;
    }

    private static int[] R6CalculateRoomDegrees(
        int roomCount,
        IReadOnlyList<DreamRoomConnection> connections)
    {
        int[] degrees = new int[roomCount];

        for (int i = 0; i < connections.Count; i++)
        {
            DreamRoomConnection connection =
                connections[i];

            if (connection == null)
            {
                continue;
            }

            if (connection.RoomAIndex >= 0 &&
                connection.RoomAIndex < roomCount)
            {
                degrees[connection.RoomAIndex]++;
            }

            if (connection.RoomBIndex >= 0 &&
                connection.RoomBIndex < roomCount)
            {
                degrees[connection.RoomBIndex]++;
            }
        }

        return degrees;
    }

    private static List<int> R6BuildConnectionRoutingOrder(
        IReadOnlyList<DreamRoomConnection> connections,
        int[] roomDegrees)
    {
        List<int> indices = new List<int>();

        for (int i = 0; i < connections.Count; i++)
        {
            indices.Add(i);
        }

        indices.Sort(
            delegate(int firstIndex, int secondIndex)
            {
                DreamRoomConnection first =
                    connections[firstIndex];

                DreamRoomConnection second =
                    connections[secondIndex];

                int firstPriority =
                    roomDegrees[first.RoomAIndex] +
                    roomDegrees[first.RoomBIndex];

                int secondPriority =
                    roomDegrees[second.RoomAIndex] +
                    roomDegrees[second.RoomBIndex];

                if (firstPriority != secondPriority)
                {
                    return secondPriority.CompareTo(
                        firstPriority);
                }

                return firstIndex.CompareTo(secondIndex);
            });

        return indices;
    }

    private static HashSet<Vector2Int>
        R6CollectOccupiedRoomCells(
            IReadOnlyList<DreamRoomPlacement> placements)
    {
        HashSet<Vector2Int> cells =
            new HashSet<Vector2Int>();

        List<Vector2Int> placementCells =
            new List<Vector2Int>();

        for (int i = 0; i < placements.Count; i++)
        {
            DreamRoomPlacement placement =
                placements[i];

            if (placement == null)
            {
                continue;
            }

            placement.GetOccupiedGlobalCells(
                placementCells);

            cells.UnionWith(placementCells);
        }

        return cells;
    }

    private static long R6GetSocketPairScore(
        DreamRoomPlacement placementA,
        R6SocketChoice socketA,
        DreamRoomPlacement placementB,
        R6SocketChoice socketB)
    {
        long directDistance =
            R6ManhattanDistance(
                socketA.OutsideAnchor,
                socketB.OutsideAnchor);

        long directionPenaltyA =
            R6GetDirectionPenalty(
                placementA,
                placementB,
                socketA.Direction);

        long directionPenaltyB =
            R6GetDirectionPenalty(
                placementB,
                placementA,
                socketB.Direction);

        return directDistance * 100L +
               (directionPenaltyA +
                directionPenaltyB) * 10L;
    }

    private static long R6GetDirectionPenalty(
        DreamRoomPlacement from,
        DreamRoomPlacement to,
        DreamRoomDoorDirection direction)
    {
        RectInt fromBounds = from.CellBounds;
        RectInt toBounds = to.CellBounds;

        long deltaXTwice =
            (long)toBounds.xMin +
            toBounds.xMax - 1 -
            ((long)fromBounds.xMin +
             fromBounds.xMax - 1);

        long deltaYTwice =
            (long)toBounds.yMin +
            toBounds.yMax - 1 -
            ((long)fromBounds.yMin +
             fromBounds.yMax - 1);

        Vector2Int directionOffset =
            direction.ToCellOffset();

        long dot =
            deltaXTwice * directionOffset.x +
            deltaYTwice * directionOffset.y;

        long total =
            Math.Abs(deltaXTwice) +
            Math.Abs(deltaYTwice);

        return total - dot;
    }

    private static int R6CompareSocketPairCandidates(
        R6SocketPairCandidate first,
        R6SocketPairCandidate second)
    {
        int scoreComparison =
            first.Score.CompareTo(second.Score);

        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        int socketAComparison =
            string.Compare(
                first.SocketA.Socket.SocketId,
                second.SocketA.Socket.SocketId,
                StringComparison.Ordinal);

        if (socketAComparison != 0)
        {
            return socketAComparison;
        }

        int socketBComparison =
            string.Compare(
                first.SocketB.Socket.SocketId,
                second.SocketB.Socket.SocketId,
                StringComparison.Ordinal);

        if (socketBComparison != 0)
        {
            return socketBComparison;
        }

        int anchorAComparison =
            R6CompareCells(
                first.SocketA.OutsideAnchor,
                second.SocketA.OutsideAnchor);

        return anchorAComparison != 0
            ? anchorAComparison
            : R6CompareCells(
                first.SocketB.OutsideAnchor,
                second.SocketB.OutsideAnchor);
    }

    private static int R6CompareCells(
        Vector2Int first,
        Vector2Int second)
    {
        int xComparison =
            first.x.CompareTo(second.x);

        return xComparison != 0
            ? xComparison
            : first.y.CompareTo(second.y);
    }

    private bool R6IsInsideMap(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < templateFirstMapWidth &&
               cell.y < templateFirstMapHeight;
    }

    private static int R6GetDirectionIndex(
        Vector2Int direction)
    {
        for (int i = 0;
             i < R6CardinalDirections.Length;
             i++)
        {
            if (R6CardinalDirections[i] == direction)
            {
                return i;
            }
        }

        return -1;
    }

    private static int R6ManhattanDistance(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }

    private static int R6PositiveModulo(
        int value,
        int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static string R6BuildSocketKey(
        int roomIndex,
        string socketId)
    {
        return roomIndex + "|" +
               (socketId ?? string.Empty).Trim();
    }

    private static void R6AddCellWithoutDuplicate(
        List<Vector2Int> cells,
        Vector2Int cell)
    {
        if (cells.Count == 0 ||
            cells[cells.Count - 1] != cell)
        {
            cells.Add(cell);
        }
    }

    private static Vector2Int R6GetFirstCell(
        HashSet<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            return cell;
        }

        return Vector2Int.zero;
    }

    private static int R6CountReachableCells(
        ICollection<Vector2Int> cells,
        Vector2Int start)
    {
        HashSet<Vector2Int> cellSet =
            cells as HashSet<Vector2Int> ??
            new HashSet<Vector2Int>(cells);

        if (!cellSet.Contains(start))
        {
            return 0;
        }

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int i = 0;
                 i < R6CardinalDirections.Length;
                 i++)
            {
                Vector2Int neighbour =
                    current + R6CardinalDirections[i];

                if (cellSet.Contains(neighbour) &&
                    visited.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return visited.Count;
    }

    private static int R6GetGraphSeedForAttempt(
        int externalSeed,
        int attemptIndex)
    {
        if (attemptIndex == 0)
        {
            return externalSeed;
        }

        return R5DeriveSeed(
            externalSeed,
            unchecked(
                0xBB67AE85u +
                (uint)attemptIndex * 0x9E37u));
    }

    private static string R6BuildConfigurationErrorReport(
        int floorNumber,
        int seed,
        List<string> errors)
    {
        return
            "[DungeonGenerator/R6] Socket Corridor 配置无效\n" +
            "Floor " + floorNumber +
            " | Seed " + seed + "\n" +
            R6JoinErrors(errors) +
            "\n没有生成或修改任何 Layout。";
    }

    private string R6BuildSuccessReport(
        DungeonLayout layout,
        int floorNumber,
        int externalSeed,
        int graphSeed,
        int layoutAttempt,
        R6RoutingStatistics statistics)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R6] Door Socket 与 A* 走廊建立成功");

        builder.AppendLine(
            "Catalog：" +
            templateFirstRoomCatalog.CatalogId +
            " | Floor " + floorNumber +
            " | External Seed " + externalSeed +
            " | R5 Seed " + graphSeed +
            " | 完整尝试 " + layoutAttempt);

        builder.AppendLine(
            "Rooms：" + layout.RoomPlacements.Count +
            " | Connections：" + layout.Connections.Count +
            " | Assigned Socket Pairs：" +
            statistics.ConnectionSummaries.Count);

        builder.AppendLine(
            "Corridor Width：" + socketCorridorWidth +
            " | CorridorCells：" +
            statistics.UniqueCorridorCells +
            " | Shared Cells：" +
            statistics.ReusedCorridorCells +
            " | A* Expanded States：" +
            statistics.TotalExpandedNodes);

        if (statistics.WidthMode ==
            DungeonCorridorWidthMode.Mixed1And2)
        {
            builder.AppendLine(
                "Width Profile：Mixed1And2" +
                " | PrimaryWideConnections：" +
                statistics.PrimaryWideConnections +
                " | MixedConnections：" +
                statistics.MixedConnections +
                " | WideCenterlineCells：" +
                statistics.WideCenterlineCells +
                " | NarrowCenterlineCells：" +
                statistics.NarrowCenterlineCells +
                " | JunctionCells：" +
                statistics.JunctionCellCount +
                " | Uniform2SafetyEnvelope=Preserved");
        }
        else if (statistics.WidthMode ==
                 DungeonCorridorWidthMode.Mixed1To3)
        {
            builder.AppendLine(
                "Width Profile：Mixed1To3" +
                " | PrimaryRouteConnections：" +
                statistics.PrimaryWideConnections +
                " | MixedConnections：" +
                statistics.MixedConnections +
                " | OpenConnections：" +
                statistics.OpenConnections +
                " | Width1Nodes：" +
                statistics.NarrowCenterlineCells +
                " | Width2Nodes：" +
                statistics.WideCenterlineCells +
                " | Width3Nodes：" +
                statistics.OpenCenterlineCells +
                " | OpenCandidates：" +
                statistics.OpenCandidateCount +
                " | OpenAccepted：" +
                statistics.AcceptedOpenCount +
                " | OpenFallbacks：" +
                statistics.FallbackOpenCount +
                " | JunctionCells：" +
                statistics.JunctionCellCount +
                " | BaseSafetyEnvelope=2" +
                " | DoorWidth=2" +
                " | OpenExpansion=Validated");
        }

        for (int i = 0;
             i < statistics.ConnectionSummaries.Count;
             i++)
        {
            R6ConnectionRoutingSummary summary =
                statistics.ConnectionSummaries[i];

            string connectionKind =
                summary.ConnectionIndex <
                layout.RoomPlacements.Count - 1
                    ? "Tree"
                    : "Extra";

            builder.AppendLine(
                "[" + summary.ConnectionIndex +
                "/" + connectionKind + "] Room " +
                summary.RoomAIndex + " " +
                summary.SocketAId + "（" +
                summary.DirectionA + "） ↔ Room " +
                summary.RoomBIndex + " " +
                summary.SocketBId + "（" +
                summary.DirectionB + "）" +
                " | Centerline " +
                summary.CenterlineCellCount +
                " | Cells " +
                summary.ExpandedCellCount +
                " | Pair Try " +
                summary.SocketPairAttempt);
        }

        builder.AppendLine(
            "最终 FloorCells：" + layout.FloorCells.Count +
            " | 从 Start 全部可达：" +
            R6CountReachableCells(
                layout.FloorCells,
                layout.StartCell) + "/" +
            layout.FloorCells.Count);

        builder.Append(
            "阶段边界：未实例化 Prefab | 未打开 DoorBlocker | " +
            "未调用 Renderer | 未修改 R4/R5 与旧 Generate()。" +
            "DoorBlocker 将在阶段7实例化后按 Socket Id 开关。");

        return builder.ToString();
    }

    private string R6BuildFailureReport(
        int floorNumber,
        int seed,
        List<string> attemptFailures)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R6] Door Socket 与 A* 走廊建立失败");

        builder.AppendLine(
            "Floor " + floorNumber +
            " | External Seed " + seed +
            " | 完整尝试上限 " +
            socketCorridorMaximumLayoutAttempts);

        int shownFailures =
            Mathf.Min(5, attemptFailures.Count);

        for (int i = 0; i < shownFailures; i++)
        {
            builder.AppendLine(
                "- " + attemptFailures[i]);
        }

        if (attemptFailures.Count > shownFailures)
        {
            builder.AppendLine(
                "- 其余 " +
                (attemptFailures.Count - shownFailures) +
                " 次失败已省略。");
        }

        builder.Append(
            "结果：没有返回部分 Socket、部分走廊或部分 Layout。" +
            "旧 Generate() 未执行也未修改。");

        return builder.ToString();
    }

    private static string R6JoinErrors(
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.Append(errors[i]);

            if (i < errors.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private struct R6SocketChoice
    {
        public readonly int RoomIndex;
        public readonly DreamRoomDoorSocket Socket;
        public readonly Vector2Int OutsideAnchor;
        public readonly DreamRoomDoorDirection Direction;

        public R6SocketChoice(
            int roomIndex,
            DreamRoomDoorSocket socket,
            Vector2Int outsideAnchor,
            DreamRoomDoorDirection direction)
        {
            RoomIndex = roomIndex;
            Socket = socket;
            OutsideAnchor = outsideAnchor;
            Direction = direction;
        }
    }

    private struct R6SocketPairCandidate
    {
        public readonly R6SocketChoice SocketA;
        public readonly R6SocketChoice SocketB;
        public readonly long Score;

        public R6SocketPairCandidate(
            R6SocketChoice socketA,
            R6SocketChoice socketB,
            long score)
        {
            SocketA = socketA;
            SocketB = socketB;
            Score = score;
        }
    }

    private struct R6PathState : IEquatable<R6PathState>
    {
        public readonly Vector2Int Cell;
        public readonly int IncomingDirectionIndex;

        public R6PathState(
            Vector2Int cell,
            int incomingDirectionIndex)
        {
            Cell = cell;
            IncomingDirectionIndex =
                incomingDirectionIndex;
        }

        public bool Equals(R6PathState other)
        {
            return Cell == other.Cell &&
                   IncomingDirectionIndex ==
                   other.IncomingDirectionIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is R6PathState &&
                   Equals((R6PathState)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Cell.GetHashCode() * 397 ^
                       IncomingDirectionIndex;
            }
        }
    }

    private struct R6OpenEntry
    {
        public readonly R6PathState State;
        public readonly int TotalCost;
        public readonly int Heuristic;
        public readonly int Sequence;

        public R6OpenEntry(
            R6PathState state,
            int totalCost,
            int heuristic,
            int sequence)
        {
            State = state;
            TotalCost = totalCost;
            Heuristic = heuristic;
            Sequence = sequence;
        }
    }

    private sealed class R6OpenHeap
    {
        private readonly List<R6OpenEntry> entries =
            new List<R6OpenEntry>();

        public int Count => entries.Count;

        public void Push(R6OpenEntry entry)
        {
            entries.Add(entry);

            int index = entries.Count - 1;

            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;

                if (!ComesBefore(
                        entries[index],
                        entries[parentIndex]))
                {
                    break;
                }

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        public R6OpenEntry Pop()
        {
            R6OpenEntry result = entries[0];
            int lastIndex = entries.Count - 1;

            entries[0] = entries[lastIndex];
            entries.RemoveAt(lastIndex);

            int index = 0;

            while (index < entries.Count)
            {
                int left = index * 2 + 1;
                int right = left + 1;

                if (left >= entries.Count)
                {
                    break;
                }

                int bestChild = left;

                if (right < entries.Count &&
                    ComesBefore(
                        entries[right],
                        entries[left]))
                {
                    bestChild = right;
                }

                if (!ComesBefore(
                        entries[bestChild],
                        entries[index]))
                {
                    break;
                }

                Swap(index, bestChild);
                index = bestChild;
            }

            return result;
        }

        private static bool ComesBefore(
            R6OpenEntry first,
            R6OpenEntry second)
        {
            if (first.TotalCost != second.TotalCost)
            {
                return first.TotalCost < second.TotalCost;
            }

            if (first.Heuristic != second.Heuristic)
            {
                return first.Heuristic < second.Heuristic;
            }

            return first.Sequence < second.Sequence;
        }

        private void Swap(int firstIndex, int secondIndex)
        {
            R6OpenEntry temporary = entries[firstIndex];
            entries[firstIndex] = entries[secondIndex];
            entries[secondIndex] = temporary;
        }
    }

    private sealed class R6RoutingStatistics
    {
        public readonly List<R6ConnectionRoutingSummary>
            ConnectionSummaries =
                new List<R6ConnectionRoutingSummary>();

        public int TotalExpandedNodes;
        public int TotalCenterlineCells;
        public int ReusedCorridorCells;
        public int UniqueCorridorCells;
        public DungeonCorridorWidthMode WidthMode;
        public int PrimaryWideConnections;
        public int MixedConnections;
        public int WideCenterlineCells;
        public int NarrowCenterlineCells;
        public int JunctionCellCount;
        public int OpenCenterlineCells;
        public int OpenConnections;
        public int OpenCandidateCount;
        public int AcceptedOpenCount;
        public int FallbackOpenCount;
    }

    private struct R6ConnectionRoutingSummary
    {
        public readonly int ConnectionIndex;
        public readonly int RoomAIndex;
        public readonly int RoomBIndex;
        public readonly string SocketAId;
        public readonly string SocketBId;
        public readonly DreamRoomDoorDirection DirectionA;
        public readonly DreamRoomDoorDirection DirectionB;
        public readonly int CenterlineCellCount;
        public readonly int ExpandedCellCount;
        public readonly int SocketPairAttempt;

        public R6ConnectionRoutingSummary(
            int connectionIndex,
            int roomAIndex,
            int roomBIndex,
            string socketAId,
            string socketBId,
            DreamRoomDoorDirection directionA,
            DreamRoomDoorDirection directionB,
            int centerlineCellCount,
            int expandedCellCount,
            int socketPairAttempt)
        {
            ConnectionIndex = connectionIndex;
            RoomAIndex = roomAIndex;
            RoomBIndex = roomBIndex;
            SocketAId = socketAId;
            SocketBId = socketBId;
            DirectionA = directionA;
            DirectionB = directionB;
            CenterlineCellCount = centerlineCellCount;
            ExpandedCellCount = expandedCellCount;
            SocketPairAttempt = socketPairAttempt;
        }
    }
}
