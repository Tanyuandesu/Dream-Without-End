using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R8.1 出生格的实际来源。
/// 调用方必须保留这个结果，不能把 Walkable 回退伪装成专用 SpawnPoint。
/// </summary>
public enum DungeonSpawnCellSource
{
    ExplicitSpawnPoint = 0,
    RoomWalkableFallback = 1,
    LayoutFloorFallback = 2
}

/// <summary>
/// 一次纯数据出生格请求。
///
/// Allowed Room Indices 为空时表示使用 Layout 中的全部房间；
/// Reserved Cells 会在构造时复制，解析器不会修改调用方集合。
/// </summary>
public sealed class DungeonSpawnCellRequest
{
    private readonly List<int> allowedRoomIndices =
        new List<int>();

    private readonly HashSet<Vector2Int> reservedCells =
        new HashSet<Vector2Int>();

    public DungeonLayout Layout { get; }
    public DreamRoomSpawnPointKind Kind { get; }
    public IReadOnlyList<int> AllowedRoomIndices =>
        allowedRoomIndices;

    public bool HasExplicitRoomScope =>
        allowedRoomIndices.Count > 0;

    public bool ExcludeStartCell { get; }
    public bool ExcludeExitCell { get; }
    public int MinimumDistanceFromStart { get; }
    public int MinimumDistanceFromExit { get; }
    public bool HasPreferredCell { get; }
    public Vector2Int PreferredCell { get; }
    public bool AllowWalkableFallback { get; }
    public bool AllowLayoutWideFallback { get; }
    public int SelectionSalt { get; }

    public DungeonSpawnCellRequest(
        DungeonLayout layout,
        DreamRoomSpawnPointKind kind,
        IEnumerable<int> allowedRoomIndices,
        int selectionSalt,
        IEnumerable<Vector2Int> reservedCells = null,
        bool excludeStartCell = false,
        bool excludeExitCell = false,
        int minimumDistanceFromStart = 0,
        int minimumDistanceFromExit = 0,
        Vector2Int? preferredCell = null,
        bool allowWalkableFallback = true,
        bool allowLayoutWideFallback = false)
    {
        Layout = layout;
        Kind = kind;
        SelectionSalt = selectionSalt;
        ExcludeStartCell = excludeStartCell;
        ExcludeExitCell = excludeExitCell;
        MinimumDistanceFromStart =
            Mathf.Max(0, minimumDistanceFromStart);
        MinimumDistanceFromExit =
            Mathf.Max(0, minimumDistanceFromExit);
        HasPreferredCell = preferredCell.HasValue;
        PreferredCell = preferredCell.GetValueOrDefault();
        AllowWalkableFallback = allowWalkableFallback;
        AllowLayoutWideFallback = allowLayoutWideFallback;

        CopyRoomIndices(allowedRoomIndices);
        CopyReservedCells(reservedCells);
    }

    public bool IsReserved(Vector2Int cell)
    {
        return reservedCells.Contains(cell);
    }

    private void CopyRoomIndices(
        IEnumerable<int> sourceIndices)
    {
        if (sourceIndices == null)
        {
            return;
        }

        HashSet<int> uniqueIndices = new HashSet<int>();

        foreach (int roomIndex in sourceIndices)
        {
            if (uniqueIndices.Add(roomIndex))
            {
                allowedRoomIndices.Add(roomIndex);
            }
        }

        allowedRoomIndices.Sort();
    }

    private void CopyReservedCells(
        IEnumerable<Vector2Int> sourceCells)
    {
        if (sourceCells == null)
        {
            return;
        }

        reservedCells.UnionWith(sourceCells);
    }
}

/// <summary>
/// 一次成功解析的不可变结果。
/// </summary>
public sealed class DungeonSpawnCellResult
{
    public Vector2Int Cell { get; }
    public DungeonSpawnCellSource Source { get; }
    public int RoomIndex { get; }
    public string SpawnPointId { get; }
    public int CandidateCount { get; }
    public int RejectedCandidateCount { get; }
    public int SelectionSeed { get; }

    public bool UsedExplicitSpawnPoint =>
        Source == DungeonSpawnCellSource.ExplicitSpawnPoint;

    internal DungeonSpawnCellResult(
        Vector2Int cell,
        DungeonSpawnCellSource source,
        int roomIndex,
        string spawnPointId,
        int candidateCount,
        int rejectedCandidateCount,
        int selectionSeed)
    {
        Cell = cell;
        Source = source;
        RoomIndex = roomIndex;
        SpawnPointId = spawnPointId ?? string.Empty;
        CandidateCount = candidateCount;
        RejectedCandidateCount = rejectedCandidateCount;
        SelectionSeed = selectionSeed;
    }

    public override string ToString()
    {
        return
            "Cell=" + Cell +
            " | Source=" + Source +
            " | RoomIndex=" + RoomIndex +
            " | SpawnPointId=" +
            (string.IsNullOrEmpty(SpawnPointId)
                ? "None"
                : SpawnPointId) +
            " | Candidates=" + CandidateCount +
            " | Rejected=" + RejectedCandidateCount +
            " | SelectionSeed=" + SelectionSeed;
    }
}

/// <summary>
/// R8.1 统一安全出生格解析器。
///
/// 规则：
/// 1. 只返回 Layout.FloorCells 中的格子。
/// 2. 专用 SpawnPoint 必须同时是 Placement 的真实 Walkable Cell。
/// 3. 可排除 Start、Exit、已占用格和最小曼哈顿距离。
/// 4. 有合法专用点时按 RandomWeight 确定性选择。
/// 5. 没有合法专用点时，明确回退到房间真实 Walkable Cells。
/// 6. 所有候选先稳定排序，禁止依赖 HashSet 遍历顺序。
/// 7. 本类只读取输入，不修改 Layout、Prefab 或场景对象。
/// </summary>
public static class DungeonSpawnCellResolver
{
    public static bool TryResolve(
        DungeonSpawnCellRequest request,
        out DungeonSpawnCellResult result,
        out string failureReason)
    {
        result = null;
        failureReason = string.Empty;

        if (request == null)
        {
            failureReason = "Spawn Cell Request 不能为空。";
            return false;
        }

        DungeonLayout layout = request.Layout;

        if (layout == null)
        {
            failureReason = "DungeonLayout 不能为空。";
            return false;
        }

        if (layout.FloorCells == null ||
            layout.FloorCells.Count == 0)
        {
            failureReason = "Layout.FloorCells 不能为空。";
            return false;
        }

        List<int> roomIndices;
        int rejectedRoomIndexCount;

        if (!TryResolveRoomIndices(
                request,
                out roomIndices,
                out rejectedRoomIndexCount,
                out failureReason))
        {
            return false;
        }

        List<DungeonSpawnCellCandidate> explicitCandidates =
            new List<DungeonSpawnCellCandidate>();

        int matchingExplicitPointCount;
        int rejectedExplicitPointCount;

        CollectExplicitCandidates(
            request,
            roomIndices,
            explicitCandidates,
            out matchingExplicitPointCount,
            out rejectedExplicitPointCount);

        if (explicitCandidates.Count > 0)
        {
            DungeonSpawnCellCandidate selectedExplicit;
            int selectionSeed;

            if (!TrySelectCandidateForDiagnostics(
                    explicitCandidates,
                    layout.Seed,
                    request.Kind,
                    request.SelectionSalt,
                    out selectedExplicit,
                    out selectionSeed,
                    out failureReason))
            {
                return false;
            }

            result = CreateResult(
                selectedExplicit,
                explicitCandidates.Count,
                rejectedRoomIndexCount +
                rejectedExplicitPointCount,
                selectionSeed);

            return true;
        }

        if (!request.AllowWalkableFallback)
        {
            failureReason =
                "没有合法的 " + request.Kind +
                " SpawnPoint，且请求禁止 Walkable 回退。" +
                " MatchingExplicit=" +
                matchingExplicitPointCount +
                "，RejectedExplicit=" +
                rejectedExplicitPointCount + "。";

            return false;
        }

        List<DungeonSpawnCellCandidate> fallbackCandidates =
            new List<DungeonSpawnCellCandidate>();

        HashSet<Vector2Int> usedFallbackCells =
            new HashSet<Vector2Int>();

        int rejectedFallbackCellCount = 0;

        CollectRoomFallbackCandidates(
            request,
            roomIndices,
            fallbackCandidates,
            usedFallbackCells,
            ref rejectedFallbackCellCount);

        if (fallbackCandidates.Count == 0 &&
            request.AllowLayoutWideFallback)
        {
            CollectLayoutFallbackCandidates(
                request,
                fallbackCandidates,
                usedFallbackCells,
                ref rejectedFallbackCellCount);
        }

        if (fallbackCandidates.Count == 0)
        {
            failureReason =
                "没有合法出生格。" +
                " Kind=" + request.Kind +
                " | MatchingExplicit=" +
                matchingExplicitPointCount +
                " | RejectedExplicit=" +
                rejectedExplicitPointCount +
                " | RejectedFallback=" +
                rejectedFallbackCellCount +
                " | Reserved／Start／Exit／距离条件可能排除了全部候选。";

            return false;
        }

        NarrowFallbacksToPreferredCell(
            request,
            fallbackCandidates);

        DungeonSpawnCellCandidate selectedFallback;
        int fallbackSelectionSeed;

        if (!TrySelectCandidateForDiagnostics(
                fallbackCandidates,
                layout.Seed,
                request.Kind,
                request.SelectionSalt,
                out selectedFallback,
                out fallbackSelectionSeed,
                out failureReason))
        {
            return false;
        }

        result = CreateResult(
            selectedFallback,
            fallbackCandidates.Count,
            rejectedRoomIndexCount +
            rejectedExplicitPointCount +
            rejectedFallbackCellCount,
            fallbackSelectionSeed);

        return true;
    }

    /// <summary>
    /// 由 R8.1 诊断器直接验证稳定排序与权重选择。
    /// 正式 Manager 应调用 TryResolve，而不是绕过安全过滤。
    /// </summary>
    internal static bool TrySelectCandidateForDiagnostics(
        IList<DungeonSpawnCellCandidate> sourceCandidates,
        int layoutSeed,
        DreamRoomSpawnPointKind kind,
        int selectionSalt,
        out DungeonSpawnCellCandidate selected,
        out int selectionSeed,
        out string failureReason)
    {
        selected = null;
        selectionSeed = CombineSeed(
            layoutSeed,
            selectionSalt,
            (int)kind);
        failureReason = string.Empty;

        if (sourceCandidates == null ||
            sourceCandidates.Count == 0)
        {
            failureReason = "候选出生格不能为空。";
            return false;
        }

        List<DungeonSpawnCellCandidate> candidates =
            new List<DungeonSpawnCellCandidate>();

        for (int i = 0; i < sourceCandidates.Count; i++)
        {
            DungeonSpawnCellCandidate candidate =
                sourceCandidates[i];

            if (candidate != null && candidate.Weight > 0)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            failureReason = "候选出生格全部为空或权重无效。";
            return false;
        }

        candidates.Sort(CompareCandidates);

        long totalWeight = 0L;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Weight;
        }

        if (totalWeight <= 0L)
        {
            failureReason = "候选总权重必须大于 0。";
            return false;
        }

        System.Random random =
            new System.Random(selectionSeed);

        double roll = random.NextDouble() * totalWeight;
        long accumulatedWeight = 0L;

        for (int i = 0; i < candidates.Count; i++)
        {
            accumulatedWeight += candidates[i].Weight;

            if (roll < accumulatedWeight)
            {
                selected = candidates[i];
                return true;
            }
        }

        selected = candidates[candidates.Count - 1];
        return true;
    }

    private static bool TryResolveRoomIndices(
        DungeonSpawnCellRequest request,
        out List<int> roomIndices,
        out int rejectedRoomIndexCount,
        out string failureReason)
    {
        roomIndices = new List<int>();
        rejectedRoomIndexCount = 0;
        failureReason = string.Empty;

        DungeonLayout layout = request.Layout;

        int roomCount = Mathf.Max(
            layout.Rooms.Count,
            layout.RoomPlacements.Count);

        if (!request.HasExplicitRoomScope)
        {
            for (int i = 0; i < roomCount; i++)
            {
                roomIndices.Add(i);
            }

            return true;
        }

        for (int i = 0;
             i < request.AllowedRoomIndices.Count;
             i++)
        {
            int requestedIndex =
                request.AllowedRoomIndices[i];

            if (requestedIndex < 0 ||
                requestedIndex >= roomCount)
            {
                rejectedRoomIndexCount++;
                continue;
            }

            roomIndices.Add(requestedIndex);
        }

        if (roomIndices.Count > 0)
        {
            return true;
        }

        failureReason =
            "Allowed Room Indices 全部越界。" +
            " Layout Room Count=" + roomCount +
            "，Rejected=" + rejectedRoomIndexCount + "。";

        return false;
    }

    private static void CollectExplicitCandidates(
        DungeonSpawnCellRequest request,
        List<int> roomIndices,
        List<DungeonSpawnCellCandidate> results,
        out int matchingPointCount,
        out int rejectedPointCount)
    {
        matchingPointCount = 0;
        rejectedPointCount = 0;

        HashSet<Vector2Int> usedCells =
            new HashSet<Vector2Int>();

        DungeonLayout layout = request.Layout;

        for (int roomListIndex = 0;
             roomListIndex < roomIndices.Count;
             roomListIndex++)
        {
            int roomIndex = roomIndices[roomListIndex];

            if (roomIndex < 0 ||
                roomIndex >= layout.RoomPlacements.Count)
            {
                continue;
            }

            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null ||
                placement.Template == null)
            {
                continue;
            }

            IReadOnlyList<DreamRoomSpawnPoint> points =
                placement.Template.SpawnPoints;

            if (points == null)
            {
                continue;
            }

            for (int pointIndex = 0;
                 pointIndex < points.Count;
                 pointIndex++)
            {
                DreamRoomSpawnPoint point = points[pointIndex];

                if (point == null || point.Kind != request.Kind)
                {
                    continue;
                }

                matchingPointCount++;

                Vector2Int cell =
                    placement.GetSpawnPointGlobalCell(point);

                if (!placement.Template.IsWalkableCell(
                        point.LocalCell) ||
                    !IsLegalCell(request, cell) ||
                    !usedCells.Add(cell))
                {
                    rejectedPointCount++;
                    continue;
                }

                string readableId =
                    string.IsNullOrWhiteSpace(
                        point.SpawnPointId)
                        ? point.name
                        : point.SpawnPointId;

                results.Add(
                    new DungeonSpawnCellCandidate(
                        cell,
                        Mathf.Max(1, point.RandomWeight),
                        roomIndex,
                        readableId,
                        DungeonSpawnCellSource
                            .ExplicitSpawnPoint));
            }
        }
    }

    private static void CollectRoomFallbackCandidates(
        DungeonSpawnCellRequest request,
        List<int> roomIndices,
        List<DungeonSpawnCellCandidate> results,
        HashSet<Vector2Int> usedCells,
        ref int rejectedCellCount)
    {
        DungeonLayout layout = request.Layout;
        List<Vector2Int> placementCells =
            new List<Vector2Int>();

        for (int roomListIndex = 0;
             roomListIndex < roomIndices.Count;
             roomListIndex++)
        {
            int roomIndex = roomIndices[roomListIndex];

            if (roomIndex >= 0 &&
                roomIndex < layout.RoomPlacements.Count &&
                layout.RoomPlacements[roomIndex] != null)
            {
                layout.RoomPlacements[roomIndex]
                    .GetWalkableGlobalCells(placementCells);

                for (int cellIndex = 0;
                     cellIndex < placementCells.Count;
                     cellIndex++)
                {
                    TryAddFallbackCandidate(
                        request,
                        placementCells[cellIndex],
                        roomIndex,
                        DungeonSpawnCellSource
                            .RoomWalkableFallback,
                        results,
                        usedCells,
                        ref rejectedCellCount);
                }

                continue;
            }

            if (roomIndex < 0 ||
                roomIndex >= layout.Rooms.Count)
            {
                continue;
            }

            RectInt room = layout.Rooms[roomIndex];

            for (int y = room.yMin; y < room.yMax; y++)
            {
                for (int x = room.xMin; x < room.xMax; x++)
                {
                    TryAddFallbackCandidate(
                        request,
                        new Vector2Int(x, y),
                        roomIndex,
                        DungeonSpawnCellSource
                            .RoomWalkableFallback,
                        results,
                        usedCells,
                        ref rejectedCellCount);
                }
            }
        }
    }

    private static void CollectLayoutFallbackCandidates(
        DungeonSpawnCellRequest request,
        List<DungeonSpawnCellCandidate> results,
        HashSet<Vector2Int> usedCells,
        ref int rejectedCellCount)
    {
        foreach (Vector2Int cell in request.Layout.FloorCells)
        {
            TryAddFallbackCandidate(
                request,
                cell,
                -1,
                DungeonSpawnCellSource.LayoutFloorFallback,
                results,
                usedCells,
                ref rejectedCellCount);
        }
    }

    private static void TryAddFallbackCandidate(
        DungeonSpawnCellRequest request,
        Vector2Int cell,
        int roomIndex,
        DungeonSpawnCellSource source,
        List<DungeonSpawnCellCandidate> results,
        HashSet<Vector2Int> usedCells,
        ref int rejectedCellCount)
    {
        if (!IsLegalCell(request, cell) ||
            !usedCells.Add(cell))
        {
            rejectedCellCount++;
            return;
        }

        results.Add(
            new DungeonSpawnCellCandidate(
                cell,
                1,
                roomIndex,
                source.ToString(),
                source));
    }

    private static bool IsLegalCell(
        DungeonSpawnCellRequest request,
        Vector2Int cell)
    {
        DungeonLayout layout = request.Layout;

        if (!layout.FloorCells.Contains(cell) ||
            request.IsReserved(cell))
        {
            return false;
        }

        if (request.ExcludeStartCell &&
            cell == layout.StartCell)
        {
            return false;
        }

        if (request.ExcludeExitCell &&
            cell == layout.ExitCell)
        {
            return false;
        }

        if (Manhattan(cell, layout.StartCell) <
            request.MinimumDistanceFromStart)
        {
            return false;
        }

        if (Manhattan(cell, layout.ExitCell) <
            request.MinimumDistanceFromExit)
        {
            return false;
        }

        return true;
    }

    private static void NarrowFallbacksToPreferredCell(
        DungeonSpawnCellRequest request,
        List<DungeonSpawnCellCandidate> candidates)
    {
        if (!request.HasPreferredCell ||
            candidates.Count <= 1)
        {
            return;
        }

        int nearestDistance = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            nearestDistance = Mathf.Min(
                nearestDistance,
                Manhattan(
                    candidates[i].Cell,
                    request.PreferredCell));
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (Manhattan(
                    candidates[i].Cell,
                    request.PreferredCell) !=
                nearestDistance)
            {
                candidates.RemoveAt(i);
            }
        }
    }

    private static DungeonSpawnCellResult CreateResult(
        DungeonSpawnCellCandidate selected,
        int candidateCount,
        int rejectedCandidateCount,
        int selectionSeed)
    {
        return new DungeonSpawnCellResult(
            selected.Cell,
            selected.Source,
            selected.RoomIndex,
            selected.SourceId,
            candidateCount,
            rejectedCandidateCount,
            selectionSeed);
    }

    private static int CompareCandidates(
        DungeonSpawnCellCandidate first,
        DungeonSpawnCellCandidate second)
    {
        int xComparison = first.Cell.x.CompareTo(second.Cell.x);

        if (xComparison != 0)
        {
            return xComparison;
        }

        int yComparison = first.Cell.y.CompareTo(second.Cell.y);

        if (yComparison != 0)
        {
            return yComparison;
        }

        int roomComparison =
            first.RoomIndex.CompareTo(second.RoomIndex);

        if (roomComparison != 0)
        {
            return roomComparison;
        }

        return string.Compare(
            first.SourceId,
            second.SourceId,
            StringComparison.Ordinal);
    }

    private static int CombineSeed(
        int first,
        int second,
        int third)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + first;
            hash = hash * 31 + second;
            hash = hash * 31 + third;
            return hash;
        }
    }

    private static int Manhattan(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }
}

/// <summary>
/// 解析器内部候选数据；internal 只允许同一游戏程序集中的 R8.1 自检使用。
/// </summary>
internal sealed class DungeonSpawnCellCandidate
{
    public Vector2Int Cell { get; }
    public int Weight { get; }
    public int RoomIndex { get; }
    public string SourceId { get; }
    public DungeonSpawnCellSource Source { get; }

    public DungeonSpawnCellCandidate(
        Vector2Int cell,
        int weight,
        int roomIndex,
        string sourceId,
        DungeonSpawnCellSource source)
    {
        Cell = cell;
        Weight = Mathf.Max(1, weight);
        RoomIndex = roomIndex;
        SourceId = sourceId ?? string.Empty;
        Source = source;
    }
}
