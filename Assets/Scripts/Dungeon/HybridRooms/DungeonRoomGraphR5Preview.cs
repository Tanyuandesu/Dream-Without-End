using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R5 房间连接图的独立验证器和 Scene Gizmo。
///
/// 本组件没有运行时生命周期方法；只有主动执行组件菜单时，
/// 才会在内存中生成 R4 摆放和 R5 连接图。
/// 不实例化 Prefab，也不生成 Door Socket 或走廊。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRoomGraphR5Preview : MonoBehaviour
{
    [Header("R5 诊断目标")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [Min(1)]
    [SerializeField]
    private int previewFloorNumber = 1;

    [SerializeField]
    private int previewFixedSeed = 12345;

    [Tooltip(
        "从 Fixed Seed 往后检查多少个 Seed，" +
        "确认房间摆放或连接图能够发生变化。")]
    [Range(1, 32)]
    [SerializeField]
    private int changedSeedSearchCount = 8;

    [Header("Scene Gizmo")]
    [Min(0.05f)]
    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private bool drawMapBounds = true;

    [SerializeField]
    private bool drawRoomBounds = true;

    [SerializeField]
    private bool drawRoomPadding;

    [SerializeField]
    private bool drawTreeConnections = true;

    [SerializeField]
    private bool drawExtraConnections = true;

    [SerializeField]
    private Color treeConnectionColor =
        new Color(0.25f, 0.9f, 1f, 1f);

    [SerializeField]
    private Color extraConnectionColor =
        new Color(1f, 0.6f, 0.15f, 1f);

    private DungeonLayout lastPreviewLayout;

    [ContextMenu("Validate R5 Room Connection Graph")]
    public void ValidateR5RoomConnectionGraph()
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError(
                "[DungeonRoomGraphR5Preview] Dungeon Generator 不能为空。" +
                "请把本组件放在 GameManager 上，或拖入其 DungeonGenerator。",
                this);

            return;
        }

        DungeonLayout primaryLayout;
        string generationReport;

        if (!dungeonGenerator.
                TryGenerateTemplateFirstGraphLayout(
                    previewFloorNumber,
                    previewFixedSeed,
                    out primaryLayout,
                    out generationReport))
        {
            lastPreviewLayout = null;

            Debug.LogError(
                "[DungeonRoomGraphR5Preview] R5 正式校验失败\n" +
                generationReport,
                this);

            return;
        }

        List<string> errors = new List<string>();

        R5AppendErrors(
            "DungeonLayout",
            primaryLayout.GetValidationErrors(),
            errors);

        R5PreviewMetrics metrics;

        R5ValidateGraphContract(
            primaryLayout,
            out metrics,
            errors);

        string primarySignature =
            R5BuildLayoutSignature(primaryLayout);

        R5ValidateSameSeed(
            primarySignature,
            errors);

        int changedSeed;

        R5ValidateChangedSeed(
            primarySignature,
            out changedSeed,
            errors);

        R5ValidateBrokenGraphDetection(
            primaryLayout,
            errors);

        lastPreviewLayout = primaryLayout;

        if (errors.Count > 0)
        {
            Debug.LogError(
                R5BuildFailureReport(
                    errors,
                    generationReport),
                this);

            return;
        }

        Debug.Log(
            R5BuildSuccessReport(
                primaryLayout,
                metrics,
                changedSeed,
                generationReport),
            this);
    }

    private void R5ValidateGraphContract(
        DungeonLayout layout,
        out R5PreviewMetrics metrics,
        List<string> errors)
    {
        metrics = new R5PreviewMetrics();

        if (layout == null)
        {
            errors.Add("R5 Layout 为空。");
            return;
        }

        int roomCount = layout.RoomPlacements.Count;
        int treeConnectionCount =
            Mathf.Max(0, roomCount - 1);

        int extraConnectionCount =
            layout.Connections.Count -
            treeConnectionCount;

        metrics.RoomCount = roomCount;
        metrics.TreeConnectionCount =
            treeConnectionCount;

        metrics.ExtraConnectionCount =
            extraConnectionCount;

        metrics.CycleRank =
            layout.Connections.Count -
            roomCount + 1;

        if (roomCount !=
            dungeonGenerator.TemplateFirstDesiredRoomCount)
        {
            errors.Add(
                "RoomPlacements 应为 " +
                dungeonGenerator.TemplateFirstDesiredRoomCount +
                "，实际为 " + roomCount + "。");
        }

        if (extraConnectionCount <
                dungeonGenerator.
                    RoomGraphMinimumExtraConnections ||
            extraConnectionCount >
                dungeonGenerator.
                    RoomGraphMaximumExtraConnections)
        {
            errors.Add(
                "Extra Connections 应在 " +
                dungeonGenerator.
                    RoomGraphMinimumExtraConnections +
                "～" +
                dungeonGenerator.
                    RoomGraphMaximumExtraConnections +
                "，实际为 " +
                extraConnectionCount + "。");
        }

        HashSet<Vector2Int> usedPairs =
            new HashSet<Vector2Int>();

        int[] degrees = new int[roomCount];

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            if (connection == null)
            {
                errors.Add(
                    "Connection " + i +
                    " 是空引用。");
                continue;
            }

            List<string> connectionErrors =
                connection.GetValidationErrors(
                    roomCount,
                    requireAssignedSockets: false,
                    requireCorridor: false);

            R5AppendErrors(
                "Connection " + i,
                connectionErrors,
                errors);

            if (connection.HasAssignedSockets)
            {
                errors.Add(
                    "Connection " + i +
                    " 提前分配了 Socket。");
            }

            if (connection.HasCorridor)
            {
                errors.Add(
                    "Connection " + i +
                    " 提前保存了 Corridor Cells。");
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
                    " 重复连接 Room " +
                    pair.x + " 与 Room " +
                    pair.y + "。");
            }

            degrees[roomA]++;
            degrees[roomB]++;
        }

        int isolatedRoomCount = 0;

        for (int i = 0; i < degrees.Length; i++)
        {
            if (degrees[i] == 0)
            {
                isolatedRoomCount++;
                errors.Add(
                    "Room " + i +
                    " 没有任何连接，是孤立房间。");
            }
        }

        metrics.IsolatedRoomCount =
            isolatedRoomCount;

        metrics.StartRoomIndex =
            R5FindRoomContainingWalkableCell(
                layout,
                layout.StartCell);

        metrics.ExitRoomIndex =
            R5FindRoomContainingWalkableCell(
                layout,
                layout.ExitCell);

        metrics.ReachableRoomCount =
            R5CountReachableRooms(
                roomCount,
                layout.Connections,
                metrics.StartRoomIndex,
                layout.Connections.Count);

        if (metrics.ReachableRoomCount != roomCount)
        {
            errors.Add(
                "从 Start Room 只能到达 " +
                metrics.ReachableRoomCount + "/" +
                roomCount + " 个房间。");
        }

        int treeReachableRoomCount =
            R5CountReachableRooms(
                roomCount,
                layout.Connections,
                0,
                treeConnectionCount);

        if (treeReachableRoomCount != roomCount)
        {
            errors.Add(
                "前 N-1 条 Tree Connections 只能覆盖 " +
                treeReachableRoomCount + "/" +
                roomCount + " 个房间。");
        }

        if (metrics.CycleRank !=
            extraConnectionCount)
        {
            errors.Add(
                "Cycle Rank " +
                metrics.CycleRank +
                " 与 Extra Connections " +
                extraConnectionCount +
                " 不一致。");
        }

        if (metrics.CycleRank <
            dungeonGenerator.
                RoomGraphMinimumExtraConnections)
        {
            errors.Add(
                "图中环路数量不足。Cycle Rank=" +
                metrics.CycleRank + "。");
        }

        R5ValidateStartRoomCandidate(
            layout,
            metrics.StartRoomIndex,
            errors);

        R5ValidateExitIsGraphFarthest(
            layout,
            metrics,
            errors);
    }

    private static int R5FindRoomContainingWalkableCell(
        DungeonLayout layout,
        Vector2Int targetCell)
    {
        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null)
            {
                continue;
            }

            placement.GetWalkableGlobalCells(
                walkableCells);

            for (int cellIndex = 0;
                 cellIndex < walkableCells.Count;
                 cellIndex++)
            {
                if (walkableCells[cellIndex] ==
                    targetCell)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static void R5ValidateStartRoomCandidate(
        DungeonLayout layout,
        int startRoomIndex,
        List<string> errors)
    {
        if (startRoomIndex < 0 ||
            startRoomIndex >=
            layout.RoomPlacements.Count)
        {
            errors.Add(
                "StartCell 不属于任何 RoomPlacement 的 Walkable Cells。");
            return;
        }

        bool hasTaggedStartCandidate = false;
        bool hasOrdinaryStandardCandidate = false;
        bool hasAnyStandardCandidate = false;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                layout.RoomPlacements[i].Template;

            if (template == null)
            {
                continue;
            }

            hasTaggedStartCandidate |=
                template.HasTag(
                    DreamRoomTag.StartCandidate);

            bool isStandard =
                template.HasTag(
                    DreamRoomTag.Standard);

            hasAnyStandardCandidate |= isStandard;

            hasOrdinaryStandardCandidate |=
                isStandard &&
                !template.HasTag(
                    DreamRoomTag.Special);
        }

        DreamRoomTemplate selectedTemplate =
            layout.RoomPlacements[
                startRoomIndex].Template;

        if (hasTaggedStartCandidate &&
            !selectedTemplate.HasTag(
                DreamRoomTag.StartCandidate))
        {
            errors.Add(
                "存在 StartCandidate 标签房，但 Start Room 没有使用它。");
        }
        else if (!hasTaggedStartCandidate &&
                 hasOrdinaryStandardCandidate &&
                 (!selectedTemplate.HasTag(
                      DreamRoomTag.Standard) ||
                  selectedTemplate.HasTag(
                      DreamRoomTag.Special)))
        {
            errors.Add(
                "存在普通 Standard 房，但 Start Room 没有从中选择。");
        }
        else if (!hasTaggedStartCandidate &&
                 !hasOrdinaryStandardCandidate &&
                 hasAnyStandardCandidate &&
                 !selectedTemplate.HasTag(
                     DreamRoomTag.Standard))
        {
            errors.Add(
                "存在 Standard 房，但 Start Room 没有从中选择。");
        }
    }

    private static void R5ValidateExitIsGraphFarthest(
        DungeonLayout layout,
        R5PreviewMetrics metrics,
        List<string> errors)
    {
        if (metrics.StartRoomIndex < 0 ||
            metrics.ExitRoomIndex < 0 ||
            metrics.StartRoomIndex ==
            metrics.ExitRoomIndex)
        {
            errors.Add(
                "Start Room 与 Exit Room 无效或相同。");
            return;
        }

        long[] distances;
        int[] hops;

        R5CalculateShortestPaths(
            layout,
            metrics.StartRoomIndex,
            out distances,
            out hops);

        bool hasExitCandidate = false;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            if (i != metrics.StartRoomIndex &&
                layout.RoomPlacements[i].Template.
                    HasTag(DreamRoomTag.ExitCandidate))
            {
                hasExitCandidate = true;
                break;
            }
        }

        int expectedExitRoomIndex = -1;
        long farthestDistance = long.MinValue;
        int farthestHops = int.MinValue;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            if (i == metrics.StartRoomIndex ||
                (hasExitCandidate &&
                 !layout.RoomPlacements[i].Template.
                    HasTag(DreamRoomTag.ExitCandidate)) ||
                distances[i] == long.MaxValue)
            {
                continue;
            }

            if (distances[i] > farthestDistance ||
                (distances[i] == farthestDistance &&
                 hops[i] > farthestHops) ||
                (distances[i] == farthestDistance &&
                 hops[i] == farthestHops &&
                 (expectedExitRoomIndex < 0 ||
                  i < expectedExitRoomIndex)))
            {
                expectedExitRoomIndex = i;
                farthestDistance = distances[i];
                farthestHops = hops[i];
            }
        }

        metrics.ExitGraphDistanceTwice =
            distances[metrics.ExitRoomIndex];

        metrics.ExitGraphHops =
            hops[metrics.ExitRoomIndex];

        if (metrics.ExitRoomIndex !=
            expectedExitRoomIndex)
        {
            errors.Add(
                "Exit Room " +
                metrics.ExitRoomIndex +
                " 不是图最短路径距离最远的候选房；" +
                "预期为 " +
                expectedExitRoomIndex + "。");
        }
    }

    private static void R5CalculateShortestPaths(
        DungeonLayout layout,
        int startRoomIndex,
        out long[] distances,
        out int[] hops)
    {
        int roomCount = layout.RoomPlacements.Count;

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
            int current = -1;

            for (int candidate = 0;
                 candidate < roomCount;
                 candidate++)
            {
                if (visited[candidate] ||
                    distances[candidate] ==
                    long.MaxValue)
                {
                    continue;
                }

                if (current < 0 ||
                    distances[candidate] <
                    distances[current] ||
                    (distances[candidate] ==
                     distances[current] &&
                     hops[candidate] < hops[current]) ||
                    (distances[candidate] ==
                     distances[current] &&
                     hops[candidate] == hops[current] &&
                     candidate < current))
                {
                    current = candidate;
                }
            }

            if (current < 0)
            {
                break;
            }

            visited[current] = true;

            for (int i = 0;
                 i < layout.Connections.Count;
                 i++)
            {
                DreamRoomConnection connection =
                    layout.Connections[i];

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

                long candidateDistance =
                    distances[current] +
                    R5GetCenterDistanceTwice(
                        layout.RoomPlacements[current],
                        layout.RoomPlacements[other]);

                int candidateHops =
                    hops[current] + 1;

                if (candidateDistance <
                    distances[other] ||
                    (candidateDistance ==
                     distances[other] &&
                     candidateHops < hops[other]))
                {
                    distances[other] =
                        candidateDistance;

                    hops[other] = candidateHops;
                }
            }
        }
    }

    private static long R5GetCenterDistanceTwice(
        DreamRoomPlacement first,
        DreamRoomPlacement second)
    {
        RectInt firstBounds = first.CellBounds;
        RectInt secondBounds = second.CellBounds;

        long firstX =
            (long)firstBounds.xMin +
            firstBounds.xMax - 1;

        long firstY =
            (long)firstBounds.yMin +
            firstBounds.yMax - 1;

        long secondX =
            (long)secondBounds.xMin +
            secondBounds.xMax - 1;

        long secondY =
            (long)secondBounds.yMin +
            secondBounds.yMax - 1;

        return Math.Abs(firstX - secondX) +
               Math.Abs(firstY - secondY);
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

    private static void R5ValidateBrokenGraphDetection(
        DungeonLayout layout,
        List<string> errors)
    {
        if (layout == null ||
            layout.RoomPlacements.Count < 2)
        {
            return;
        }

        int isolatedRoomIndex =
            layout.RoomPlacements.Count - 1;

        List<DreamRoomConnection> brokenConnections =
            new List<DreamRoomConnection>();

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            if (connection != null &&
                !connection.ConnectsRoom(
                    isolatedRoomIndex))
            {
                brokenConnections.Add(connection);
            }
        }

        int reachable = R5CountReachableRooms(
            layout.RoomPlacements.Count,
            brokenConnections,
            0,
            brokenConnections.Count);

        if (reachable ==
            layout.RoomPlacements.Count)
        {
            errors.Add(
                "故意移除 Room " +
                isolatedRoomIndex +
                " 的全部连接后，孤立房检测仍错误地判定全图连通。");
        }
    }

    private void R5ValidateSameSeed(
        string primarySignature,
        List<string> errors)
    {
        DungeonLayout repeatedLayout;
        string repeatedReport;

        if (!dungeonGenerator.
                TryGenerateTemplateFirstGraphLayout(
                    previewFloorNumber,
                    previewFixedSeed,
                    out repeatedLayout,
                    out repeatedReport))
        {
            errors.Add(
                "相同 Seed 的第二次 R5 生成失败。\n" +
                repeatedReport);
            return;
        }

        if (!string.Equals(
                primarySignature,
                R5BuildLayoutSignature(
                    repeatedLayout),
                StringComparison.Ordinal))
        {
            errors.Add(
                "相同 Floor 与 Seed 得到了不同的房间图、" +
                "起始房或出口房。");
        }
    }

    private void R5ValidateChangedSeed(
        string primarySignature,
        out int changedSeed,
        List<string> errors)
    {
        changedSeed = previewFixedSeed;

        for (int offset = 1;
             offset <= changedSeedSearchCount;
             offset++)
        {
            int candidateSeed = unchecked(
                previewFixedSeed + offset);

            DungeonLayout candidateLayout;
            string candidateReport;

            if (!dungeonGenerator.
                    TryGenerateTemplateFirstGraphLayout(
                        previewFloorNumber,
                        candidateSeed,
                        out candidateLayout,
                        out candidateReport))
            {
                continue;
            }

            if (!string.Equals(
                    primarySignature,
                    R5BuildLayoutSignature(
                        candidateLayout),
                    StringComparison.Ordinal))
            {
                changedSeed = candidateSeed;
                return;
            }
        }

        errors.Add(
            "检查了 " + changedSeedSearchCount +
            " 个其他 Seed，但没有找到不同的 R5 图结果。");
    }

    private static string R5BuildLayoutSignature(
        DungeonLayout layout)
    {
        if (layout == null)
        {
            return "null";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            builder.Append(
                placement.Template.TemplateId);
            builder.Append('|');
            builder.Append(placement.MinimumCell.x);
            builder.Append(',');
            builder.Append(placement.MinimumCell.y);
            builder.Append('|');
            builder.Append(
                placement.ClockwiseQuarterTurns);
            builder.Append(';');
        }

        builder.Append("C:");

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            builder.Append(
                connection.RoomAIndex);
            builder.Append('-');
            builder.Append(
                connection.RoomBIndex);
            builder.Append(';');
        }

        builder.Append("S:");
        builder.Append(layout.StartCell.x);
        builder.Append(',');
        builder.Append(layout.StartCell.y);
        builder.Append("|E:");
        builder.Append(layout.ExitCell.x);
        builder.Append(',');
        builder.Append(layout.ExitCell.y);

        return builder.ToString();
    }

    private static string R5BuildSuccessReport(
        DungeonLayout layout,
        R5PreviewMetrics metrics,
        int changedSeed,
        string generationReport)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonRoomGraphR5Preview] " +
            "R5 房间连接图正式校验通过");

        builder.AppendLine(
            "Rooms：" + metrics.RoomCount +
            " | Tree：" +
            metrics.TreeConnectionCount +
            " | Extra：" +
            metrics.ExtraConnectionCount +
            " | Total：" +
            layout.Connections.Count +
            " | Cycle Rank：" +
            metrics.CycleRank);

        builder.AppendLine(
            "全图连通：" +
            metrics.ReachableRoomCount + "/" +
            metrics.RoomCount +
            " | 孤立房：" +
            metrics.IsolatedRoomCount +
            " | 前 N-1 条 Tree 独立连通：通过");

        builder.AppendLine(
            "Start Room：" +
            metrics.StartRoomIndex +
            " | Exit Room：" +
            metrics.ExitRoomIndex +
            " | 图最远检查：通过 | Distance：" +
            R5FormatDistance(
                metrics.ExitGraphDistanceTwice) +
            " 格 | Hops：" +
            metrics.ExitGraphHops);

        builder.AppendLine(
            "相同 Seed 重现：通过 | 改变 Seed：" +
            changedSeed + " 得到不同图结果");

        builder.AppendLine(
            "故意孤立房检测：通过 | 重复边／自连接／越界：通过");

        builder.AppendLine(
            "阶段边界：Socket A/B 空 | CorridorCells 0 | " +
            "未实例化 Prefab | 未修改 R4 与旧 Generate()");

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static string R5BuildFailureReport(
        List<string> errors,
        string generationReport)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonRoomGraphR5Preview] R5 正式校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(
                "- " + errors[i]);
        }

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static string R5FormatDistance(
        long doubledDistance)
    {
        long whole = doubledDistance / 2;

        return doubledDistance % 2 == 0
            ? whole.ToString()
            : whole + ".5";
    }

    private static void R5AppendErrors(
        string scope,
        List<string> source,
        List<string> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(
                scope + "：" + source[i]);
        }
    }

    private void Reset()
    {
        dungeonGenerator =
            GetComponent<DungeonGenerator>();
    }

    private void OnDrawGizmosSelected()
    {
        if (dungeonGenerator == null)
        {
            return;
        }

        float safeCellSize =
            Mathf.Max(0.05f, cellSize);

        Vector3 origin =
            transform.position -
            new Vector3(
                (dungeonGenerator.TemplateFirstMapWidth - 1) *
                0.5f * safeCellSize,
                (dungeonGenerator.TemplateFirstMapHeight - 1) *
                0.5f * safeCellSize,
                0f);

        if (drawMapBounds)
        {
            R5DrawMapBounds(origin, safeCellSize);
        }

        if (lastPreviewLayout == null)
        {
            return;
        }

        if (drawRoomBounds)
        {
            R5DrawRooms(origin, safeCellSize);
        }

        int treeConnectionCount =
            Mathf.Max(
                0,
                lastPreviewLayout.RoomPlacements.Count - 1);

        for (int i = 0;
             i < lastPreviewLayout.Connections.Count;
             i++)
        {
            bool isTreeConnection =
                i < treeConnectionCount;

            if ((isTreeConnection &&
                 !drawTreeConnections) ||
                (!isTreeConnection &&
                 !drawExtraConnections))
            {
                continue;
            }

            DreamRoomConnection connection =
                lastPreviewLayout.Connections[i];

            if (connection == null ||
                connection.RoomAIndex < 0 ||
                connection.RoomBIndex < 0 ||
                connection.RoomAIndex >=
                    lastPreviewLayout.
                        RoomPlacements.Count ||
                connection.RoomBIndex >=
                    lastPreviewLayout.
                        RoomPlacements.Count)
            {
                continue;
            }

            Gizmos.color = isTreeConnection
                ? treeConnectionColor
                : extraConnectionColor;

            Gizmos.DrawLine(
                R5GetRoomCenterWorld(
                    lastPreviewLayout.RoomPlacements[
                        connection.RoomAIndex],
                    origin,
                    safeCellSize),
                R5GetRoomCenterWorld(
                    lastPreviewLayout.RoomPlacements[
                        connection.RoomBIndex],
                    origin,
                    safeCellSize));
        }

        Gizmos.color = new Color(
            0.25f, 1f, 0.35f, 1f);

        R5DrawCellMarker(
            lastPreviewLayout.StartCell,
            origin,
            safeCellSize);

        Gizmos.color = new Color(
            1f, 0.25f, 0.8f, 1f);

        R5DrawCellMarker(
            lastPreviewLayout.ExitCell,
            origin,
            safeCellSize);
    }

    private void R5DrawMapBounds(
        Vector3 origin,
        float safeCellSize)
    {
        RectInt mapBounds = new RectInt(
            0,
            0,
            dungeonGenerator.TemplateFirstMapWidth,
            dungeonGenerator.TemplateFirstMapHeight);

        Gizmos.color = new Color(
            0.85f, 0.9f, 1f, 0.85f);

        R5DrawWireRect(
            mapBounds,
            origin,
            safeCellSize);
    }

    private void R5DrawRooms(
        Vector3 origin,
        float safeCellSize)
    {
        for (int i = 0;
             i < lastPreviewLayout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                lastPreviewLayout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                continue;
            }

            Color color = R5GetTemplateColor(
                placement.Template.TemplateId);

            if (drawRoomPadding)
            {
                Color paddingColor = color;
                paddingColor.a = 0.28f;
                Gizmos.color = paddingColor;

                R5DrawWireRect(
                    placement.GetPaddedBounds(
                        dungeonGenerator.
                            TemplateFirstRoomPadding),
                    origin,
                    safeCellSize);
            }

            Vector3 center;
            Vector3 size;

            R5GetWorldRect(
                placement.CellBounds,
                origin,
                safeCellSize,
                out center,
                out size);

            Color fillColor = color;
            fillColor.a = 0.16f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(center, size);

            color.a = 0.9f;
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }
    }

    private static Vector3 R5GetRoomCenterWorld(
        DreamRoomPlacement placement,
        Vector3 origin,
        float safeCellSize)
    {
        RectInt bounds = placement.CellBounds;

        return origin +
               new Vector3(
                   (bounds.xMin +
                    (bounds.width - 1) * 0.5f) *
                   safeCellSize,
                   (bounds.yMin +
                    (bounds.height - 1) * 0.5f) *
                   safeCellSize,
                   0f);
    }

    private static void R5DrawCellMarker(
        Vector2Int cell,
        Vector3 origin,
        float safeCellSize)
    {
        Gizmos.DrawSphere(
            origin +
            new Vector3(
                cell.x * safeCellSize,
                cell.y * safeCellSize,
                0f),
            safeCellSize * 0.4f);
    }

    private static void R5DrawWireRect(
        RectInt rect,
        Vector3 origin,
        float safeCellSize)
    {
        Vector3 center;
        Vector3 size;

        R5GetWorldRect(
            rect,
            origin,
            safeCellSize,
            out center,
            out size);

        Gizmos.DrawWireCube(center, size);
    }

    private static void R5GetWorldRect(
        RectInt rect,
        Vector3 origin,
        float safeCellSize,
        out Vector3 center,
        out Vector3 size)
    {
        center = origin +
                 new Vector3(
                     (rect.xMin +
                      (rect.width - 1) * 0.5f) *
                     safeCellSize,
                     (rect.yMin +
                      (rect.height - 1) * 0.5f) *
                     safeCellSize,
                     0f);

        size = new Vector3(
            rect.width * safeCellSize,
            rect.height * safeCellSize,
            0f);
    }

    private static Color R5GetTemplateColor(
        string templateId)
    {
        uint hash = 2166136261u;
        string safeId = templateId ?? string.Empty;

        for (int i = 0; i < safeId.Length; i++)
        {
            hash ^= safeId[i];
            hash *= 16777619u;
        }

        return Color.HSVToRGB(
            (hash % 360u) / 360f,
            0.55f,
            0.92f);
    }

    [Serializable]
    private sealed class R5PreviewMetrics
    {
        public int RoomCount;
        public int TreeConnectionCount;
        public int ExtraConnectionCount;
        public int CycleRank;
        public int ReachableRoomCount;
        public int IsolatedRoomCount;
        public int StartRoomIndex;
        public int ExitRoomIndex;
        public long ExitGraphDistanceTwice;
        public int ExitGraphHops;
    }
}
