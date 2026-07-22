using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// DungeonGenerator 的 R5 房间连接图扩展。
///
/// 阶段边界：
/// 1. 复用 R4 已经通过校验的 RoomPlacements。
/// 2. 先建立 N-1 条最小生成树连接，保证全部房间连通。
/// 3. 再加入少量额外连接形成环路。
/// 4. 只保存 Room Index；Socket Id、Corridor Cells 仍保持为空。
/// 5. 不实例化 Prefab，不调用 Renderer，不修改旧 Generate()。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("R5 房间连接图（尚未分配门与走廊）")]
    [Tooltip(
        "最小生成树之外至少加入多少条连接。" +
        "默认 1，确保图中至少形成一个环路。")]
    [Min(0)]
    [SerializeField]
    private int roomGraphMinimumExtraConnections = 1;

    [Tooltip(
        "最小生成树之外最多加入多少条连接。" +
        "实际数量由固定 Seed 在最小值和最大值之间选择。")]
    [Min(0)]
    [SerializeField]
    private int roomGraphMaximumExtraConnections = 2;

    public int RoomGraphMinimumExtraConnections =>
        roomGraphMinimumExtraConnections;

    public int RoomGraphMaximumExtraConnections =>
        roomGraphMaximumExtraConnections;

    /// <summary>
    /// 生成 R5 的“房间摆放＋逻辑连接图”布局。
    ///
    /// 成功结果：
    /// - RoomPlacements 来自 R4；
    /// - Connections 含 N-1 条树连接和 1～2 条额外连接；
    /// - Socket Id 与 Corridor Cells 为空；
    /// - Start/Exit 位于两个房间的 Walkable Cell；
    /// - Exit 房间是从 Start 沿连接图最短路径距离最远的候选房。
    /// </summary>
    public bool TryGenerateTemplateFirstGraphLayout(
        int floorNumber,
        int seed,
        out DungeonLayout layout,
        out string report)
    {
        layout = null;

        if (roomGraphMinimumExtraConnections < 0 ||
            roomGraphMaximumExtraConnections < 0 ||
            roomGraphMaximumExtraConnections <
            roomGraphMinimumExtraConnections)
        {
            report =
                "[DungeonGenerator/R5] 房间连接图配置无效\n" +
                "Minimum Extra Connections 必须大于等于 0，" +
                "Maximum 必须大于等于 Minimum。";

            return false;
        }

        DungeonLayout placementLayout;
        string placementReport;

        if (!TryGenerateTemplateFirstLayout(
                floorNumber,
                seed,
                out placementLayout,
                out placementReport))
        {
            report =
                "[DungeonGenerator/R5] R4 房间放置失败，" +
                "无法建立连接图\n" +
                placementReport;

            return false;
        }

        int roomCount =
            placementLayout.RoomPlacements.Count;

        if (roomCount < 2)
        {
            report =
                "[DungeonGenerator/R5] 至少需要两个 RoomPlacement " +
                "才能建立连接图。";

            return false;
        }

        int possibleExtraConnections =
            R5GetMaximumPossibleExtraConnections(
                roomCount);

        if (roomGraphMinimumExtraConnections >
            possibleExtraConnections)
        {
            report =
                "[DungeonGenerator/R5] Extra Connections 配置无法满足。\n" +
                "房间 " + roomCount +
                " 个时，生成树之外最多只有 " +
                possibleExtraConnections +
                " 条不重复连接，但 Minimum 设置为 " +
                roomGraphMinimumExtraConnections + "。";

            return false;
        }

        int effectiveMaximumExtraConnections =
            Mathf.Min(
                roomGraphMaximumExtraConnections,
                possibleExtraConnections);

        List<DreamRoomConnection> connections;
        int extraConnectionCount;
        int graphRandomSeed;
        string graphFailureReason;

        if (!R5TryBuildConnectionGraph(
                placementLayout.RoomPlacements,
                seed,
                effectiveMaximumExtraConnections,
                out connections,
                out extraConnectionCount,
                out graphRandomSeed,
                out graphFailureReason))
        {
            report =
                "[DungeonGenerator/R5] 房间连接图建立失败\n" +
                graphFailureReason;

            return false;
        }

        int startRoomIndex =
            R5ChooseStartRoomIndex(
                placementLayout.RoomPlacements,
                seed);

        long[] graphDistances;
        int[] graphHops;

        R5CalculateShortestGraphPaths(
            placementLayout.RoomPlacements,
            connections,
            startRoomIndex,
            out graphDistances,
            out graphHops);

        int exitRoomIndex =
            R5ChooseFarthestExitRoomIndex(
                placementLayout.RoomPlacements,
                startRoomIndex,
                graphDistances,
                graphHops);

        if (startRoomIndex < 0 ||
            exitRoomIndex < 0 ||
            startRoomIndex == exitRoomIndex)
        {
            report =
                "[DungeonGenerator/R5] 无法选择两个不同的起始房和出口房。";

            return false;
        }

        Vector2Int startCell;
        Vector2Int exitCell;

        if (!R5TryGetRepresentativeWalkableCell(
                placementLayout.RoomPlacements[
                    startRoomIndex],
                out startCell) ||
            !R5TryGetRepresentativeWalkableCell(
                placementLayout.RoomPlacements[
                    exitRoomIndex],
                out exitCell))
        {
            report =
                "[DungeonGenerator/R5] 起始房或出口房没有 Walkable Cell。";

            return false;
        }

        layout = DungeonLayout.CreateHybrid(
            placementLayout.RoomPlacements,
            new Vector2Int[0],
            connections,
            startCell,
            exitCell,
            seed);

        List<string> errors =
            layout.GetValidationErrors();

        R5AppendGraphValidationErrors(
            layout,
            startRoomIndex,
            exitRoomIndex,
            effectiveMaximumExtraConnections,
            errors);

        if (errors.Count > 0)
        {
            report = R5BuildGraphErrorReport(errors);
            layout = null;
            return false;
        }

        report = R5BuildGraphSuccessReport(
            layout,
            floorNumber,
            seed,
            graphRandomSeed,
            startRoomIndex,
            exitRoomIndex,
            graphDistances[exitRoomIndex],
            graphHops[exitRoomIndex],
            extraConnectionCount);

        return true;
    }

    private bool R5TryBuildConnectionGraph(
        IReadOnlyList<DreamRoomPlacement> placements,
        int seed,
        int effectiveMaximumExtraConnections,
        out List<DreamRoomConnection> connections,
        out int extraConnectionCount,
        out int graphRandomSeed,
        out string failureReason)
    {
        connections = new List<DreamRoomConnection>();
        extraConnectionCount = 0;
        graphRandomSeed = R5DeriveSeed(
            seed,
            0xA511E9B3u);

        failureReason = string.Empty;

        HashSet<Vector2Int> usedPairs =
            new HashSet<Vector2Int>();

        if (!R5TryBuildMinimumSpanningTree(
                placements,
                connections,
                usedPairs,
                out failureReason))
        {
            return false;
        }

        List<R5GraphEdge> extraCandidates =
            R5BuildUnusedGraphEdges(
                placements,
                usedPairs);

        int effectiveMinimum =
            Mathf.Min(
                roomGraphMinimumExtraConnections,
                extraCandidates.Count);

        int effectiveMaximum =
            Mathf.Min(
                effectiveMaximumExtraConnections,
                extraCandidates.Count);

        System.Random graphRandom =
            new System.Random(graphRandomSeed);

        extraConnectionCount =
            effectiveMinimum == effectiveMaximum
                ? effectiveMinimum
                : graphRandom.Next(
                    effectiveMinimum,
                    effectiveMaximum + 1);

        for (int i = 0;
             i < extraConnectionCount;
             i++)
        {
            int selectedIndex =
                graphRandom.Next(
                    0,
                    extraCandidates.Count);

            R5GraphEdge edge =
                extraCandidates[selectedIndex];

            extraCandidates.RemoveAt(
                selectedIndex);

            connections.Add(
                new DreamRoomConnection(
                    edge.RoomAIndex,
                    edge.RoomBIndex));
        }

        return true;
    }

    private static bool R5TryBuildMinimumSpanningTree(
        IReadOnlyList<DreamRoomPlacement> placements,
        List<DreamRoomConnection> connections,
        HashSet<Vector2Int> usedPairs,
        out string failureReason)
    {
        failureReason = string.Empty;

        int roomCount = placements.Count;
        bool[] inTree = new bool[roomCount];
        inTree[0] = true;

        int roomsInTree = 1;

        while (roomsInTree < roomCount)
        {
            bool foundEdge = false;
            R5GraphEdge bestEdge = default(R5GraphEdge);

            for (int connectedIndex = 0;
                 connectedIndex < roomCount;
                 connectedIndex++)
            {
                if (!inTree[connectedIndex])
                {
                    continue;
                }

                for (int outsideIndex = 0;
                     outsideIndex < roomCount;
                     outsideIndex++)
                {
                    if (inTree[outsideIndex])
                    {
                        continue;
                    }

                    R5GraphEdge candidate =
                        R5CreateGraphEdge(
                            placements,
                            connectedIndex,
                            outsideIndex);

                    if (!foundEdge ||
                        R5GraphEdgeComesBefore(
                            candidate,
                            bestEdge))
                    {
                        bestEdge = candidate;
                        foundEdge = true;
                    }
                }
            }

            if (!foundEdge)
            {
                failureReason =
                    "最小生成树无法找到连接到剩余房间的边。";

                return false;
            }

            connections.Add(
                new DreamRoomConnection(
                    bestEdge.RoomAIndex,
                    bestEdge.RoomBIndex));

            usedPairs.Add(
                new Vector2Int(
                    bestEdge.RoomAIndex,
                    bestEdge.RoomBIndex));

            int newlyConnectedRoom =
                inTree[bestEdge.RoomAIndex]
                    ? bestEdge.RoomBIndex
                    : bestEdge.RoomAIndex;

            if (!inTree[newlyConnectedRoom])
            {
                inTree[newlyConnectedRoom] = true;
                roomsInTree++;
            }
        }

        return connections.Count == roomCount - 1;
    }

    private static List<R5GraphEdge>
        R5BuildUnusedGraphEdges(
            IReadOnlyList<DreamRoomPlacement> placements,
            HashSet<Vector2Int> usedPairs)
    {
        List<R5GraphEdge> edges =
            new List<R5GraphEdge>();

        for (int firstIndex = 0;
             firstIndex < placements.Count;
             firstIndex++)
        {
            for (int secondIndex = firstIndex + 1;
                 secondIndex < placements.Count;
                 secondIndex++)
            {
                Vector2Int pair = new Vector2Int(
                    firstIndex,
                    secondIndex);

                if (usedPairs.Contains(pair))
                {
                    continue;
                }

                edges.Add(
                    R5CreateGraphEdge(
                        placements,
                        firstIndex,
                        secondIndex));
            }
        }

        return edges;
    }

    private static R5GraphEdge R5CreateGraphEdge(
        IReadOnlyList<DreamRoomPlacement> placements,
        int firstRoomIndex,
        int secondRoomIndex)
    {
        int roomAIndex = Mathf.Min(
            firstRoomIndex,
            secondRoomIndex);

        int roomBIndex = Mathf.Max(
            firstRoomIndex,
            secondRoomIndex);

        long weight =
            R5GetCenterDistanceTwice(
                placements[roomAIndex],
                placements[roomBIndex]);

        return new R5GraphEdge(
            roomAIndex,
            roomBIndex,
            weight);
    }

    private static bool R5GraphEdgeComesBefore(
        R5GraphEdge first,
        R5GraphEdge second)
    {
        if (first.Weight != second.Weight)
        {
            return first.Weight < second.Weight;
        }

        if (first.RoomAIndex != second.RoomAIndex)
        {
            return first.RoomAIndex < second.RoomAIndex;
        }

        return first.RoomBIndex < second.RoomBIndex;
    }

    private static long R5GetCenterDistanceTwice(
        DreamRoomPlacement first,
        DreamRoomPlacement second)
    {
        RectInt firstBounds = first.CellBounds;
        RectInt secondBounds = second.CellBounds;

        long firstCenterXTwice =
            (long)firstBounds.xMin +
            firstBounds.xMax - 1;

        long firstCenterYTwice =
            (long)firstBounds.yMin +
            firstBounds.yMax - 1;

        long secondCenterXTwice =
            (long)secondBounds.xMin +
            secondBounds.xMax - 1;

        long secondCenterYTwice =
            (long)secondBounds.yMin +
            secondBounds.yMax - 1;

        return Math.Abs(
                   firstCenterXTwice -
                   secondCenterXTwice) +
               Math.Abs(
                   firstCenterYTwice -
                   secondCenterYTwice);
    }

    private static int R5ChooseStartRoomIndex(
        IReadOnlyList<DreamRoomPlacement> placements,
        int seed)
    {
        List<int> candidates = new List<int>();

        if (!R941CollectPlacementCandidatesForRole(
            placements,
            R941RequiredRoomRole.Start,
            excludeRoomIndex: -1,
            results: candidates))
        {
            return -1;
        }

        System.Random startRandom =
            new System.Random(
                R5DeriveSeed(
                    seed,
                    0x63D83595u));

        return candidates[
            startRandom.Next(
                0,
                candidates.Count)];
    }

    private static int R5ChooseFarthestExitRoomIndex(
        IReadOnlyList<DreamRoomPlacement> placements,
        int startRoomIndex,
        long[] graphDistances,
        int[] graphHops)
    {
        List<int> candidates = new List<int>();

        if (!R941CollectPlacementCandidatesForRole(
            placements,
            R941RequiredRoomRole.Exit,
            excludeRoomIndex: startRoomIndex,
            results: candidates))
        {
            return -1;
        }

        int selectedRoomIndex = -1;
        long farthestDistance = long.MinValue;
        int farthestHopCount = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            int roomIndex = candidates[i];
            long distance = graphDistances[roomIndex];
            int hops = graphHops[roomIndex];

            if (distance == long.MaxValue)
            {
                continue;
            }

            if (distance > farthestDistance ||
                (distance == farthestDistance &&
                 hops > farthestHopCount) ||
                (distance == farthestDistance &&
                 hops == farthestHopCount &&
                 (selectedRoomIndex < 0 ||
                  roomIndex < selectedRoomIndex)))
            {
                selectedRoomIndex = roomIndex;
                farthestDistance = distance;
                farthestHopCount = hops;
            }
        }

        return selectedRoomIndex;
    }

    private static void R5CollectTaggedRoomIndices(
        IReadOnlyList<DreamRoomPlacement> placements,
        DreamRoomTag tag,
        int excludeRoomIndex,
        bool excludeSpecialRooms,
        List<int> results)
    {
        results.Clear();

        for (int i = 0; i < placements.Count; i++)
        {
            if (i == excludeRoomIndex)
            {
                continue;
            }

            DreamRoomPlacement placement = placements[i];

            if (placement == null ||
                placement.Template == null ||
                !placement.Template.HasTag(tag))
            {
                continue;
            }

            if (excludeSpecialRooms &&
                placement.Template.HasTag(
                    DreamRoomTag.Special))
            {
                continue;
            }

            results.Add(i);
        }
    }

    private static void R5CalculateShortestGraphPaths(
        IReadOnlyList<DreamRoomPlacement> placements,
        IReadOnlyList<DreamRoomConnection> connections,
        int startRoomIndex,
        out long[] distances,
        out int[] hops)
    {
        int roomCount = placements.Count;

        distances = new long[roomCount];
        hops = new int[roomCount];

        bool[] visited = new bool[roomCount];

        for (int i = 0; i < roomCount; i++)
        {
            distances[i] = long.MaxValue;
            hops[i] = int.MaxValue;
        }

        if (startRoomIndex < 0 ||
            startRoomIndex >= roomCount)
        {
            return;
        }

        distances[startRoomIndex] = 0;
        hops[startRoomIndex] = 0;

        for (int step = 0; step < roomCount; step++)
        {
            int currentRoomIndex = -1;

            for (int candidateIndex = 0;
                 candidateIndex < roomCount;
                 candidateIndex++)
            {
                if (visited[candidateIndex] ||
                    distances[candidateIndex] ==
                    long.MaxValue)
                {
                    continue;
                }

                if (currentRoomIndex < 0 ||
                    distances[candidateIndex] <
                    distances[currentRoomIndex] ||
                    (distances[candidateIndex] ==
                     distances[currentRoomIndex] &&
                     hops[candidateIndex] <
                     hops[currentRoomIndex]) ||
                    (distances[candidateIndex] ==
                     distances[currentRoomIndex] &&
                     hops[candidateIndex] ==
                     hops[currentRoomIndex] &&
                     candidateIndex < currentRoomIndex))
                {
                    currentRoomIndex = candidateIndex;
                }
            }

            if (currentRoomIndex < 0)
            {
                break;
            }

            visited[currentRoomIndex] = true;

            for (int connectionIndex = 0;
                 connectionIndex < connections.Count;
                 connectionIndex++)
            {
                DreamRoomConnection connection =
                    connections[connectionIndex];

                int otherRoomIndex;

                if (connection == null ||
                    !connection.TryGetOtherRoomIndex(
                        currentRoomIndex,
                        out otherRoomIndex) ||
                    otherRoomIndex < 0 ||
                    otherRoomIndex >= roomCount ||
                    visited[otherRoomIndex])
                {
                    continue;
                }

                long edgeWeight =
                    R5GetCenterDistanceTwice(
                        placements[currentRoomIndex],
                        placements[otherRoomIndex]);

                long candidateDistance =
                    distances[currentRoomIndex] +
                    edgeWeight;

                int candidateHops =
                    hops[currentRoomIndex] + 1;

                if (candidateDistance <
                    distances[otherRoomIndex] ||
                    (candidateDistance ==
                     distances[otherRoomIndex] &&
                     candidateHops <
                     hops[otherRoomIndex]))
                {
                    distances[otherRoomIndex] =
                        candidateDistance;

                    hops[otherRoomIndex] =
                        candidateHops;
                }
            }
        }
    }

    private void R5AppendGraphValidationErrors(
        DungeonLayout layout,
        int startRoomIndex,
        int exitRoomIndex,
        int effectiveMaximumExtraConnections,
        List<string> errors)
    {
        int roomCount = layout.RoomPlacements.Count;
        int treeConnectionCount = roomCount - 1;

        int minimumConnectionCount =
            treeConnectionCount +
            roomGraphMinimumExtraConnections;

        int maximumConnectionCount =
            treeConnectionCount +
            effectiveMaximumExtraConnections;

        if (layout.Connections.Count <
                minimumConnectionCount ||
            layout.Connections.Count >
                maximumConnectionCount)
        {
            errors.Add(
                "R5 Connections 数量应在 " +
                minimumConnectionCount + "～" +
                maximumConnectionCount +
                "，实际为 " +
                layout.Connections.Count + "。");
        }

        HashSet<Vector2Int> usedPairs =
            new HashSet<Vector2Int>();

        int[] degrees = new int[roomCount];

        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            if (connection == null)
            {
                continue;
            }

            if (connection.HasAssignedSockets)
            {
                errors.Add(
                    "Connection " + i +
                    " 提前分配了 Socket；这属于阶段6。");
            }

            if (connection.HasCorridor)
            {
                errors.Add(
                    "Connection " + i +
                    " 提前保存了 Corridor Cells；这属于阶段6。");
            }

            int roomA = connection.RoomAIndex;
            int roomB = connection.RoomBIndex;

            if (roomA < 0 || roomB < 0 ||
                roomA >= roomCount ||
                roomB >= roomCount ||
                roomA == roomB)
            {
                continue;
            }

            Vector2Int pair = new Vector2Int(
                Mathf.Min(roomA, roomB),
                Mathf.Max(roomA, roomB));

            if (!usedPairs.Add(pair))
            {
                errors.Add(
                    "Connection " + i +
                    " 与此前连接重复。");
            }

            degrees[roomA]++;
            degrees[roomB]++;
        }

        for (int i = 0; i < degrees.Length; i++)
        {
            if (degrees[i] == 0)
            {
                errors.Add(
                    "Room " + i +
                    " 是没有任何连接的孤立房间。");
            }
        }

        int reachableRoomCount =
            R5CountReachableRooms(
                roomCount,
                layout.Connections,
                startRoomIndex,
                layout.Connections.Count);

        if (reachableRoomCount != roomCount)
        {
            errors.Add(
                "从起始房只能到达 " +
                reachableRoomCount + "/" +
                roomCount + " 个房间。");
        }

        int treeReachableRoomCount =
            R5CountReachableRooms(
                roomCount,
                layout.Connections,
                0,
                Mathf.Min(
                    treeConnectionCount,
                    layout.Connections.Count));

        if (treeReachableRoomCount != roomCount)
        {
            errors.Add(
                "前 N-1 条生成树连接没有独立覆盖全部房间。" +
                "实际覆盖 " +
                treeReachableRoomCount + "/" +
                roomCount + "。");
        }

        int cycleRank =
            layout.Connections.Count -
            roomCount + 1;

        int extraConnectionCount =
            layout.Connections.Count -
            treeConnectionCount;

        if (cycleRank != extraConnectionCount)
        {
            errors.Add(
                "连通图的环路秩 " + cycleRank +
                " 与 Extra Connections " +
                extraConnectionCount + " 不一致。");
        }

        if (startRoomIndex < 0 ||
            exitRoomIndex < 0 ||
            startRoomIndex >= roomCount ||
            exitRoomIndex >= roomCount ||
            startRoomIndex == exitRoomIndex)
        {
            errors.Add("起始房和出口房索引无效或相同。");
            return;
        }

        long[] distances;
        int[] hops;

        R5CalculateShortestGraphPaths(
            layout.RoomPlacements,
            layout.Connections,
            startRoomIndex,
            out distances,
            out hops);

        int expectedExitRoomIndex =
            R5ChooseFarthestExitRoomIndex(
                layout.RoomPlacements,
                startRoomIndex,
                distances,
                hops);

        if (exitRoomIndex != expectedExitRoomIndex)
        {
            errors.Add(
                "Exit Room " + exitRoomIndex +
                " 不是从 Start Room " +
                startRoomIndex +
                " 沿图最短路径距离最远的候选房；" +
                "预期为 " +
                expectedExitRoomIndex + "。");
        }
    }

    private static int R5CountReachableRooms(
        int roomCount,
        IReadOnlyList<DreamRoomConnection> connections,
        int startRoomIndex,
        int connectionLimit)
    {
        if (startRoomIndex < 0 ||
            startRoomIndex >= roomCount)
        {
            return 0;
        }

        bool[] visited = new bool[roomCount];
        Queue<int> queue = new Queue<int>();

        visited[startRoomIndex] = true;
        queue.Enqueue(startRoomIndex);

        int visitedCount = 1;
        int safeLimit = Mathf.Min(
            connectionLimit,
            connections.Count);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            for (int i = 0; i < safeLimit; i++)
            {
                DreamRoomConnection connection =
                    connections[i];

                int other;

                if (connection == null ||
                    !connection.TryGetOtherRoomIndex(
                        current,
                        out other) ||
                    other < 0 ||
                    other >= roomCount ||
                    visited[other])
                {
                    continue;
                }

                visited[other] = true;
                visitedCount++;
                queue.Enqueue(other);
            }
        }

        return visitedCount;
    }

    private static bool R5TryGetRepresentativeWalkableCell(
        DreamRoomPlacement placement,
        out Vector2Int representativeCell)
    {
        representativeCell = Vector2Int.zero;

        if (placement == null)
        {
            return false;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        placement.GetWalkableGlobalCells(
            walkableCells);

        if (walkableCells.Count == 0)
        {
            return false;
        }

        RectInt bounds = placement.CellBounds;

        long centerXTwice =
            (long)bounds.xMin +
            bounds.xMax - 1;

        long centerYTwice =
            (long)bounds.yMin +
            bounds.yMax - 1;

        long bestDistance = long.MaxValue;

        for (int i = 0; i < walkableCells.Count; i++)
        {
            Vector2Int cell = walkableCells[i];

            long distance =
                Math.Abs(
                    (long)cell.x * 2 -
                    centerXTwice) +
                Math.Abs(
                    (long)cell.y * 2 -
                    centerYTwice);

            if (distance < bestDistance ||
                (distance == bestDistance &&
                 (cell.x < representativeCell.x ||
                  (cell.x == representativeCell.x &&
                   cell.y < representativeCell.y))))
            {
                bestDistance = distance;
                representativeCell = cell;
            }
        }

        return true;
    }

    private static int R5GetMaximumPossibleExtraConnections(
        int roomCount)
    {
        long completeGraphConnections =
            (long)roomCount *
            (roomCount - 1) / 2;

        long treeConnections =
            Mathf.Max(0, roomCount - 1);

        long extra =
            Math.Max(
                0L,
                completeGraphConnections -
                treeConnections);

        return extra > int.MaxValue
            ? int.MaxValue
            : (int)extra;
    }

    private static int R5DeriveSeed(
        int baseSeed,
        uint salt)
    {
        unchecked
        {
            uint value = (uint)baseSeed ^ salt;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (int)value;
        }
    }

    private static string R5FormatDistance(
        long doubledCellDistance)
    {
        long wholeCells =
            doubledCellDistance / 2;

        return doubledCellDistance % 2 == 0
            ? wholeCells.ToString()
            : wholeCells + ".5";
    }

    private string R5BuildGraphSuccessReport(
        DungeonLayout layout,
        int floorNumber,
        int seed,
        int graphRandomSeed,
        int startRoomIndex,
        int exitRoomIndex,
        long exitGraphDistance,
        int exitGraphHops,
        int extraConnectionCount)
    {
        int treeConnectionCount =
            layout.RoomPlacements.Count - 1;

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R5] 房间连接图建立成功");

        builder.AppendLine(
            "Catalog：" +
            TemplateFirstRoomCatalog.CatalogId +
            " | Floor " + floorNumber +
            " | Seed " + seed +
            " | Graph Seed " +
            graphRandomSeed);

        builder.AppendLine(
            "Rooms：" +
            layout.RoomPlacements.Count +
            " | Tree Connections：" +
            treeConnectionCount +
            " | Extra Connections：" +
            extraConnectionCount +
            " | Total：" +
            layout.Connections.Count);

        builder.AppendLine(
            "Start Room：" +
            startRoomIndex +
            " | Exit Room：" +
            exitRoomIndex +
            " | 图最短路径距离：" +
            R5FormatDistance(exitGraphDistance) +
            " 格 | Hops：" +
            exitGraphHops);

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            long distance =
                R5GetCenterDistanceTwice(
                    layout.RoomPlacements[
                        connection.RoomAIndex],
                    layout.RoomPlacements[
                        connection.RoomBIndex]);

            builder.AppendLine(
                "  [" +
                (i < treeConnectionCount
                    ? "Tree " + i
                    : "Extra " +
                      (i - treeConnectionCount)) +
                "] Room " +
                connection.RoomAIndex +
                " ↔ Room " +
                connection.RoomBIndex +
                " | Center Distance " +
                R5FormatDistance(distance));
        }

        builder.AppendLine(
            "阶段边界：Socket A/B 空 | CorridorCells 0 | " +
            "未实例化 Prefab。门口与走廊将在阶段6处理。");

        return builder.ToString();
    }

    private static string R5BuildGraphErrorReport(
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R5] 房间连接图校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(
                "- " + errors[i]);
        }

        builder.AppendLine("没有返回无效或不连通的部分图。");
        return builder.ToString();
    }

    [Serializable]
    private struct R5GraphEdge
    {
        public int RoomAIndex { get; }
        public int RoomBIndex { get; }
        public long Weight { get; }

        public R5GraphEdge(
            int roomAIndex,
            int roomBIndex,
            long weight)
        {
            RoomAIndex = roomAIndex;
            RoomBIndex = roomBIndex;
            Weight = weight;
        }
    }
}
