using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在 R6 已求得的双格安全中心线上应用可切换的宽度表现。
///
/// Mixed1And2 不改写房间图、Socket 选择或 A* 中心线：
/// 1. 主路径保持双格宽，避免主要战斗通路形成结构性堵塞。
/// 2. 支路的门口、转角、交叉口保持双格宽。
/// 3. 只有足够长的支路直线中段收窄为一格。
/// 4. 混合结果必须是原双格安全包络的子集。
///
/// 因此 EnemyPathService 仍读取最终 FloorCells 与合法门边；
/// 本类不会复制或替换敌人 A*。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("Corridor Pass C1：一／两格混合宽度")]
    [Tooltip(
        "Uniform2 保留 R6～R9.4 已封板基线；" +
        "Mixed1And2 只收窄支路直线中段。")]
    [SerializeField]
    private DungeonCorridorWidthMode socketCorridorWidthMode =
        DungeonCorridorWidthMode.Uniform2;

    [Tooltip(
        "Mixed1And2 下，门外从两端各保留多少个双格中心线节点。")]
    [Min(1)]
    [SerializeField]
    private int mixedCorridorDoorApronLength = 2;

    [Tooltip(
        "Mixed1And2 下，转角前后各保留多少个双格中心线节点。")]
    [Min(0)]
    [SerializeField]
    private int mixedCorridorCornerRadius = 1;

    [Tooltip(
        "Mixed1And2 下，交叉／汇合点前后各保留多少个双格中心线节点。")]
    [Min(0)]
    [SerializeField]
    private int mixedCorridorJunctionRadius = 1;

    [Tooltip(
        "连续一格宽中心线少于此长度时，自动恢复为双格，" +
        "避免产生短促的视觉锯齿和单格陷阱。")]
    [Min(1)]
    [SerializeField]
    private int mixedCorridorMinimumNarrowRunLength = 3;

    [Tooltip(
        "保持 Start 到 Exit 的最短房间图路径为双格宽。" +
        "建议开启，避免主干道成为长期单列堵点。")]
    [SerializeField]
    private bool mixedCorridorKeepPrimaryRouteWide = true;

    public DungeonCorridorWidthMode SocketCorridorWidthMode =>
        socketCorridorWidthMode;

    public int MixedCorridorDoorApronLength =>
        mixedCorridorDoorApronLength;

    public int MixedCorridorCornerRadius =>
        mixedCorridorCornerRadius;

    public int MixedCorridorJunctionRadius =>
        mixedCorridorJunctionRadius;

    public int MixedCorridorMinimumNarrowRunLength =>
        mixedCorridorMinimumNarrowRunLength;

    public bool MixedCorridorKeepPrimaryRouteWide =>
        mixedCorridorKeepPrimaryRouteWide;

    private bool R6TryApplyCorridorWidthProfile(
        DungeonLayout graphLayout,
        Dictionary<int, List<Vector2Int>> routedCenterlines,
        HashSet<Vector2Int> occupiedRoomCells,
        HashSet<Vector2Int> allCorridorCells,
        R6RoutingStatistics statistics,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (socketCorridorWidthMode ==
            DungeonCorridorWidthMode.Uniform2)
        {
            statistics.WidthMode =
                DungeonCorridorWidthMode.Uniform2;
            return true;
        }

        if (socketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1And2)
        {
            failureReason =
                "未知 Corridor Width Mode：" +
                socketCorridorWidthMode + "。";
            return false;
        }

        if (graphLayout == null ||
            routedCenterlines == null ||
            routedCenterlines.Count !=
                graphLayout.Connections.Count)
        {
            failureReason =
                "Mixed1And2 缺少完整的 Connection 中心线。";
            return false;
        }

        HashSet<int> primaryConnections;

        if (!R6TryFindPrimaryRouteConnections(
                graphLayout,
                out primaryConnections,
                out failureReason))
        {
            return false;
        }

        HashSet<Vector2Int> junctionCells =
            R6CollectCenterlineJunctionCells(
                routedCenterlines);

        Dictionary<int, List<Vector2Int>>
            mixedCellsByConnection =
                new Dictionary<int, List<Vector2Int>>();

        Dictionary<int, int> wideCenterlineCounts =
            new Dictionary<int, int>();

        Dictionary<int, int> narrowCenterlineCounts =
            new Dictionary<int, int>();

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            List<Vector2Int> centerline;

            if (!routedCenterlines.TryGetValue(
                    connectionIndex,
                    out centerline) ||
                centerline == null ||
                centerline.Count == 0)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 没有可应用宽度 Profile 的中心线。";
                return false;
            }

            bool keepWholeConnectionWide =
                mixedCorridorKeepPrimaryRouteWide &&
                primaryConnections.Contains(
                    connectionIndex);

            bool[] wideMask =
                R6BuildMixedWideMask(
                    centerline,
                    junctionCells,
                    keepWholeConnectionWide);

            List<Vector2Int> mixedCells =
                new List<Vector2Int>();

            R6ExpandCenterlineWithWidthMask(
                centerline,
                wideMask,
                mixedCells);

            DreamRoomConnection connection =
                graphLayout.Connections[connectionIndex];

            HashSet<Vector2Int> uniformEnvelope =
                new HashSet<Vector2Int>(
                    connection.CorridorCells);

            for (int cellIndex = 0;
                 cellIndex < mixedCells.Count;
                 cellIndex++)
            {
                Vector2Int cell = mixedCells[cellIndex];

                if (!uniformEnvelope.Contains(cell))
                {
                    failureReason =
                        "Connection " + connectionIndex +
                        " 的 Mixed Cell " + cell +
                        " 超出原双格安全包络。";
                    return false;
                }

                if (!R6IsInsideMap(cell) ||
                    occupiedRoomCells.Contains(cell))
                {
                    failureReason =
                        "Connection " + connectionIndex +
                        " 的 Mixed Cell " + cell +
                        " 越界或穿入房间。";
                    return false;
                }
            }

            HashSet<Vector2Int> uniqueMixed =
                new HashSet<Vector2Int>(mixedCells);

            if (uniqueMixed.Count == 0 ||
                R6CountReachableCells(
                    uniqueMixed,
                    R6GetFirstCell(uniqueMixed)) !=
                uniqueMixed.Count)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 的 Mixed Cells 不是四方向连续区域。";
                return false;
            }

            int wideCount = 0;

            for (int maskIndex = 0;
                 maskIndex < wideMask.Length;
                 maskIndex++)
            {
                if (wideMask[maskIndex])
                {
                    wideCount++;
                }
            }

            mixedCellsByConnection.Add(
                connectionIndex,
                mixedCells);

            wideCenterlineCounts.Add(
                connectionIndex,
                wideCount);

            narrowCenterlineCounts.Add(
                connectionIndex,
                centerline.Count - wideCount);
        }

        allCorridorCells.Clear();
        statistics.ReusedCorridorCells = 0;
        statistics.WideCenterlineCells = 0;
        statistics.NarrowCenterlineCells = 0;
        statistics.PrimaryWideConnections = 0;
        statistics.MixedConnections = 0;

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                graphLayout.Connections[connectionIndex];

            List<Vector2Int> mixedCells =
                mixedCellsByConnection[connectionIndex];

            connection.SetCorridorCells(mixedCells);

            int reused = 0;

            for (int cellIndex = 0;
                 cellIndex < mixedCells.Count;
                 cellIndex++)
            {
                if (!allCorridorCells.Add(
                        mixedCells[cellIndex]))
                {
                    reused++;
                }
            }

            statistics.ReusedCorridorCells += reused;
            statistics.WideCenterlineCells +=
                wideCenterlineCounts[connectionIndex];
            statistics.NarrowCenterlineCells +=
                narrowCenterlineCounts[connectionIndex];

            if (primaryConnections.Contains(
                    connectionIndex) &&
                mixedCorridorKeepPrimaryRouteWide)
            {
                statistics.PrimaryWideConnections++;
            }

            if (narrowCenterlineCounts[connectionIndex] > 0)
            {
                statistics.MixedConnections++;
            }

            R6ReplaceExpandedCellCount(
                statistics,
                connectionIndex,
                mixedCells.Count);
        }

        statistics.WidthMode =
            DungeonCorridorWidthMode.Mixed1And2;
        statistics.JunctionCellCount =
            junctionCells.Count;

        return true;
    }

    private bool[] R6BuildMixedWideMask(
        List<Vector2Int> centerline,
        HashSet<Vector2Int> junctionCells,
        bool keepWholeConnectionWide)
    {
        bool[] wide = new bool[centerline.Count];

        if (keepWholeConnectionWide)
        {
            for (int i = 0; i < wide.Length; i++)
            {
                wide[i] = true;
            }

            return wide;
        }

        int apronLength = Mathf.Clamp(
            mixedCorridorDoorApronLength,
            1,
            centerline.Count);

        for (int i = 0; i < apronLength; i++)
        {
            wide[i] = true;
            wide[centerline.Count - 1 - i] = true;
        }

        for (int i = 1; i < centerline.Count - 1; i++)
        {
            Vector2Int incoming =
                centerline[i] - centerline[i - 1];

            Vector2Int outgoing =
                centerline[i + 1] - centerline[i];

            if (incoming != outgoing)
            {
                R6MarkWideRadius(
                    wide,
                    i,
                    mixedCorridorCornerRadius);
            }

            if (junctionCells.Contains(centerline[i]))
            {
                R6MarkWideRadius(
                    wide,
                    i,
                    mixedCorridorJunctionRadius);
            }
        }

        if (centerline.Count > 0 &&
            junctionCells.Contains(centerline[0]))
        {
            R6MarkWideRadius(
                wide,
                0,
                mixedCorridorJunctionRadius);
        }

        if (centerline.Count > 1 &&
            junctionCells.Contains(
                centerline[centerline.Count - 1]))
        {
            R6MarkWideRadius(
                wide,
                centerline.Count - 1,
                mixedCorridorJunctionRadius);
        }

        R6PromoteShortNarrowRuns(wide);
        return wide;
    }

    private static void R6MarkWideRadius(
        bool[] wide,
        int centerIndex,
        int radius)
    {
        int first = Mathf.Max(0, centerIndex - radius);
        int last = Mathf.Min(
            wide.Length - 1,
            centerIndex + radius);

        for (int i = first; i <= last; i++)
        {
            wide[i] = true;
        }
    }

    private void R6PromoteShortNarrowRuns(bool[] wide)
    {
        int index = 0;

        while (index < wide.Length)
        {
            if (wide[index])
            {
                index++;
                continue;
            }

            int first = index;

            while (index < wide.Length && !wide[index])
            {
                index++;
            }

            int length = index - first;

            if (length >=
                mixedCorridorMinimumNarrowRunLength)
            {
                continue;
            }

            for (int promoteIndex = first;
                 promoteIndex < index;
                 promoteIndex++)
            {
                wide[promoteIndex] = true;
            }
        }
    }

    private void R6ExpandCenterlineWithWidthMask(
        List<Vector2Int> centerline,
        bool[] wideMask,
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

            int width =
                wideMask[segmentIndex] ||
                wideMask[segmentIndex + 1]
                    ? socketCorridorWidth
                    : 1;

            R6CollectWidthCells(
                centerline[segmentIndex],
                direction,
                width,
                results,
                used);

            R6CollectWidthCells(
                centerline[segmentIndex + 1],
                direction,
                width,
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

            int width = wideMask[cellIndex]
                ? socketCorridorWidth
                : 1;

            R6CollectCornerCellsWithWidth(
                centerline[cellIndex],
                incoming,
                outgoing,
                width,
                results,
                used);
        }
    }

    private static void R6CollectCornerCellsWithWidth(
        Vector2Int anchor,
        Vector2Int incomingDirection,
        Vector2Int outgoingDirection,
        int width,
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

        int startOffset = -(width / 2);

        for (int first = 0; first < width; first++)
        {
            for (int second = 0;
                 second < width;
                 second++)
            {
                R6AddUniqueCell(
                    anchor +
                    incomingSideways *
                        (startOffset + first) +
                    outgoingSideways *
                        (startOffset + second),
                    results,
                    used);
            }
        }
    }

    private static HashSet<Vector2Int>
        R6CollectCenterlineJunctionCells(
            Dictionary<int, List<Vector2Int>>
                routedCenterlines)
    {
        Dictionary<Vector2Int, int> useCount =
            new Dictionary<Vector2Int, int>();

        HashSet<Vector2Int> allCenterlineCells =
            new HashSet<Vector2Int>();

        foreach (KeyValuePair<int, List<Vector2Int>> pair
                 in routedCenterlines)
        {
            HashSet<Vector2Int> usedByConnection =
                new HashSet<Vector2Int>();

            List<Vector2Int> centerline = pair.Value;

            for (int i = 0; i < centerline.Count; i++)
            {
                Vector2Int cell = centerline[i];
                allCenterlineCells.Add(cell);

                if (!usedByConnection.Add(cell))
                {
                    continue;
                }

                int existing;
                useCount.TryGetValue(cell, out existing);
                useCount[cell] = existing + 1;
            }
        }

        HashSet<Vector2Int> junctionCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int cell in allCenterlineCells)
        {
            int owners;

            if (useCount.TryGetValue(cell, out owners) &&
                owners > 1)
            {
                junctionCells.Add(cell);
                continue;
            }

            int neighbourCount = 0;

            for (int directionIndex = 0;
                 directionIndex <
                    R6CardinalDirections.Length;
                 directionIndex++)
            {
                if (allCenterlineCells.Contains(
                        cell +
                        R6CardinalDirections[
                            directionIndex]))
                {
                    neighbourCount++;
                }
            }

            if (neighbourCount >= 3)
            {
                junctionCells.Add(cell);
            }
        }

        return junctionCells;
    }

    private static bool R6TryFindPrimaryRouteConnections(
        DungeonLayout layout,
        out HashSet<int> primaryConnections,
        out string failureReason)
    {
        primaryConnections = new HashSet<int>();
        failureReason = string.Empty;

        int startRoom = R6FindRoomContainingCell(
            layout.RoomPlacements,
            layout.StartCell);

        int exitRoom = R6FindRoomContainingCell(
            layout.RoomPlacements,
            layout.ExitCell);

        if (startRoom < 0 || exitRoom < 0)
        {
            failureReason =
                "Mixed1And2 无法解析 Start／Exit 所属房间。";
            return false;
        }

        if (startRoom == exitRoom)
        {
            return true;
        }

        int roomCount = layout.RoomPlacements.Count;
        bool[] visited = new bool[roomCount];
        int[] parentRoom = new int[roomCount];
        int[] parentConnection = new int[roomCount];

        for (int i = 0; i < roomCount; i++)
        {
            parentRoom[i] = -1;
            parentConnection[i] = -1;
        }

        Queue<int> queue = new Queue<int>();
        visited[startRoom] = true;
        queue.Enqueue(startRoom);

        while (queue.Count > 0 && !visited[exitRoom])
        {
            int currentRoom = queue.Dequeue();

            for (int connectionIndex = 0;
                 connectionIndex < layout.Connections.Count;
                 connectionIndex++)
            {
                DreamRoomConnection connection =
                    layout.Connections[connectionIndex];

                int otherRoom;

                if (connection == null ||
                    !connection.TryGetOtherRoomIndex(
                        currentRoom,
                        out otherRoom) ||
                    otherRoom < 0 ||
                    otherRoom >= roomCount ||
                    visited[otherRoom])
                {
                    continue;
                }

                visited[otherRoom] = true;
                parentRoom[otherRoom] = currentRoom;
                parentConnection[otherRoom] =
                    connectionIndex;
                queue.Enqueue(otherRoom);
            }
        }

        if (!visited[exitRoom])
        {
            failureReason =
                "Mixed1And2 的房间图中 Start 无法到达 Exit。";
            return false;
        }

        int room = exitRoom;

        while (room != startRoom)
        {
            int connectionIndex =
                parentConnection[room];

            if (connectionIndex < 0)
            {
                failureReason =
                    "Mixed1And2 重建主路径时缺少父 Connection。";
                return false;
            }

            primaryConnections.Add(connectionIndex);
            room = parentRoom[room];
        }

        return true;
    }

    private static int R6FindRoomContainingCell(
        IReadOnlyList<DreamRoomPlacement> placements,
        Vector2Int targetCell)
    {
        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int roomIndex = 0;
             roomIndex < placements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                placements[roomIndex];

            if (placement == null)
            {
                continue;
            }

            placement.GetWalkableGlobalCells(
                walkableCells);

            if (walkableCells.Contains(targetCell))
            {
                return roomIndex;
            }
        }

        return -1;
    }

    private static void R6ReplaceExpandedCellCount(
        R6RoutingStatistics statistics,
        int connectionIndex,
        int expandedCellCount)
    {
        for (int summaryIndex = 0;
             summaryIndex <
                statistics.ConnectionSummaries.Count;
             summaryIndex++)
        {
            R6ConnectionRoutingSummary summary =
                statistics.ConnectionSummaries[
                    summaryIndex];

            if (summary.ConnectionIndex !=
                connectionIndex)
            {
                continue;
            }

            statistics.ConnectionSummaries[
                summaryIndex] =
                new R6ConnectionRoutingSummary(
                    summary.ConnectionIndex,
                    summary.RoomAIndex,
                    summary.RoomBIndex,
                    summary.SocketAId,
                    summary.SocketBId,
                    summary.DirectionA,
                    summary.DirectionB,
                    summary.CenterlineCellCount,
                    expandedCellCount,
                    summary.SocketPairAttempt);

            return;
        }
    }
}
