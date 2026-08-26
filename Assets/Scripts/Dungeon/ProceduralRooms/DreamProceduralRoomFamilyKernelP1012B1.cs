using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12B-1：尺寸无关的 Procedural Room Family Kernel。
///
/// 当前用途：离线/Editor Audit。
/// 尚未替换现行 13x9 R2B 权威 Kernel。
///
/// 约束：
/// - 2 Cell Door Lane
/// - 中央 2x2 Hub
/// - Used Socket -> Hub 的 2 Cell 主通路
/// - Blocked 只放在边界内一格之外
/// - 每次接受障碍后检查 Walkable 与 Used Socket 连通
/// </summary>
public static class
    DreamProceduralRoomFamilyKernelP1012B1
{
    private static readonly Vector2Int[]
        Cardinal =
        {
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left,
            Vector2Int.down
        };

    public static bool TryGenerate(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        int seed,
        IReadOnlyList<DreamProceduralDoorLane>
            usedDoors,
        out DreamProceduralRoomLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = string.Empty;

        if (profile == null)
        {
            failureReason =
                "Family Profile 为空。";
            return false;
        }

        List<DreamProceduralDoorLane> doors;

        if (!TryNormalizeDoors(
                profile,
                usedDoors,
                out doors,
                out failureReason))
        {
            return false;
        }

        HashSet<Vector2Int> reserved =
            BuildReservedBackbone(
                profile,
                doors);

        System.Random random =
            new System.Random(seed);

        DreamProceduralRoomArchetype archetype =
            (DreamProceduralRoomArchetype)
            random.Next(0, 4);

        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>();

        int total =
            profile.SizeInCells.x *
            profile.SizeInCells.y;

        float targetRatio =
            Mathf.Lerp(
                profile.TargetBlockedRatioMin,
                profile.TargetBlockedRatioMax,
                (float)random.NextDouble());

        int minimumBlocked =
            Mathf.CeilToInt(
                total *
                profile.MinimumBlockedRatio);

        int maximumBlocked =
            Mathf.FloorToInt(
                total *
                profile.MaximumBlockedRatio);

        int target =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    total *
                    targetRatio),
                minimumBlocked,
                maximumBlocked);

        int attempts =
            Mathf.Max(
                480,
                total * 6);

        for (int attempt = 0;
             attempt < attempts &&
             blocked.Count < target;
             attempt++)
        {
            Vector2Int shape =
                ChooseObstacleSize(
                    profile,
                    random);

            Vector2Int origin;

            if (!TryChooseOrigin(
                    profile,
                    random,
                    archetype,
                    shape,
                    out origin))
            {
                continue;
            }

            List<Vector2Int> cells =
                CollectRect(
                    origin,
                    shape);

            if (!CanPlace(
                    profile,
                    cells,
                    reserved,
                    blocked))
            {
                continue;
            }

            if (blocked.Count +
                cells.Count >
                maximumBlocked)
            {
                continue;
            }

            for (int i = 0;
                 i < cells.Count;
                 i++)
            {
                blocked.Add(
                    cells[i]);
            }

            if (!AllWalkableConnected(
                    profile,
                    blocked) ||
                !AllDoorsConnected(
                    profile,
                    blocked,
                    doors))
            {
                for (int i = 0;
                     i < cells.Count;
                     i++)
                {
                    blocked.Remove(
                        cells[i]);
                }
            }
        }

        // P10.12B-1.1：
        // Small 08x06 + NEWS 的极端组合里，2 Cell 主骨架占据空间较大，
        // 矩形障碍随机阶段极少数 Seed 会停在 7/48 = 14.6%，
        // 只差 1 Cell 才达到 15% 下限。
        //
        // 不降低统一的 15% 标准，而是进入一个确定性的“最小密度补齐”阶段：
        // 仅尝试 1x1 Cell，每次加入后仍重新验证全 Walkable / Used Socket 连通。
        // 因此这是生成完整性修补，不改变导航契约。
        if (blocked.Count < minimumBlocked)
        {
            TryFillMinimumWithSingleCells(
                profile,
                blocked,
                reserved,
                doors,
                minimumBlocked,
                random);
        }

        float ratio =
            total <= 0
                ? 0f
                : (float)blocked.Count /
                  total;

        if (ratio <
                profile.MinimumBlockedRatio ||
            ratio >
                profile.MaximumBlockedRatio)
        {
            failureReason =
                profile.FamilyId +
                " BlockedRatio 越界：" +
                (ratio * 100f).ToString("F1") +
                "% | Blocked=" +
                blocked.Count +
                " | Total=" +
                total +
                " | Reserved=" +
                reserved.Count;
            return false;
        }

        if (!AllWalkableConnected(
                profile,
                blocked))
        {
            failureReason =
                profile.FamilyId +
                " Walkable Cells 不连通。";
            return false;
        }

        if (!AllDoorsConnected(
                profile,
                blocked,
                doors))
        {
            failureReason =
                profile.FamilyId +
                " Used Socket 不互通。";
            return false;
        }

        foreach (Vector2Int cell in
                 reserved)
        {
            if (blocked.Contains(cell))
            {
                failureReason =
                    profile.FamilyId +
                    " Blocked 侵入 2 Cell 主通路：" +
                    cell;
                return false;
            }
        }

        layout =
            new DreamProceduralRoomLayout(
                profile.SizeInCells,
                seed,
                archetype,
                blocked,
                reserved,
                doors);

        return true;
    }

    public static bool Validate(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        DreamProceduralRoomLayout layout,
        out string failureReason)
    {
        failureReason =
            string.Empty;

        if (profile == null)
        {
            failureReason =
                "Profile 为空。";
            return false;
        }

        if (layout == null)
        {
            failureReason =
                "Layout 为空。";
            return false;
        }

        if (layout.SizeInCells !=
            profile.SizeInCells)
        {
            failureReason =
                "Layout Size 与 Profile 不一致：" +
                layout.SizeInCells +
                " / " +
                profile.SizeInCells;
            return false;
        }

        if (layout.BlockedRatio <
                profile.MinimumBlockedRatio ||
            layout.BlockedRatio >
                profile.MaximumBlockedRatio)
        {
            failureReason =
                "BlockedRatio 不在 Profile 范围：" +
                (layout.BlockedRatio * 100f)
                    .ToString("F1") +
                "%。";
            return false;
        }

        if (!AllWalkableConnected(
                profile,
                layout.BlockedCells))
        {
            failureReason =
                "Walkable Cells 不连通。";
            return false;
        }

        if (!AllDoorsConnected(
                profile,
                layout.BlockedCells,
                layout.UsedDoorLanes))
        {
            failureReason =
                "Used Socket 不互通。";
            return false;
        }

        foreach (Vector2Int reserved in
                 layout.ReservedMainRouteCells)
        {
            if (layout.BlockedCells.Contains(
                    reserved))
            {
                failureReason =
                    "Blocked 覆盖保底主通路：" +
                    reserved;
                return false;
            }
        }

        return true;
    }

    public static int DeriveRoomSeed(
        int floorSeed,
        int roomIndex,
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        int socketMask)
    {
        if (profile == null)
        {
            return floorSeed;
        }

        return
            DreamProceduralRoomKernelP1012A1
                .DeriveRoomSeed(
                    floorSeed,
                    roomIndex,
                    profile.ShellTypeHash,
                    socketMask);
    }

    private static bool TryNormalizeDoors(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        IReadOnlyList<DreamProceduralDoorLane>
            input,
        out List<DreamProceduralDoorLane>
            doors,
        out string reason)
    {
        doors =
            new List<DreamProceduralDoorLane>();

        reason =
            string.Empty;

        if (input == null ||
            input.Count == 0)
        {
            reason =
                "至少需要一个 Used Socket。";
            return false;
        }

        HashSet<Vector2Int> used =
            new HashSet<Vector2Int>();

        for (int i = 0;
             i < input.Count;
             i++)
        {
            DreamProceduralDoorLane lane =
                input[i];

            if (lane == null ||
                lane.LocalInsideCells == null ||
                lane.LocalInsideCells.Count != 2)
            {
                reason =
                    profile.FamilyId +
                    " 每个 Used Socket 必须正好占 2 Cell。";
                return false;
            }

            List<Vector2Int> cells =
                new List<Vector2Int>(
                    lane.LocalInsideCells);

            cells.Sort(
                CompareCells);

            for (int c = 0;
                 c < cells.Count;
                 c++)
            {
                Vector2Int cell =
                    cells[c];

                if (!Inside(
                        profile,
                        cell))
                {
                    reason =
                        "Door Cell 超出 " +
                        profile.SizeInCells +
                        "：" +
                        cell;
                    return false;
                }

                if (!OnExpectedBoundary(
                        profile,
                        lane.Direction,
                        cell))
                {
                    reason =
                        "Door Cell " +
                        cell +
                        " 不在 " +
                        lane.Direction +
                        " 边界。";
                    return false;
                }

                if (!used.Add(cell))
                {
                    reason =
                        "Used Socket 共享 Door Cell：" +
                        cell;
                    return false;
                }
            }

            doors.Add(
                new DreamProceduralDoorLane(
                    lane.Direction,
                    cells));
        }

        return true;
    }

    private static HashSet<Vector2Int>
        BuildReservedBackbone(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            IReadOnlyList<DreamProceduralDoorLane>
                doors)
    {
        HashSet<Vector2Int> reserved =
            new HashSet<Vector2Int>();

        Vector2Int hubMin =
            profile.HubMinimum;

        Vector2Int hubMax =
            profile.HubMaximum;

        for (int x = hubMin.x;
             x <= hubMax.x;
             x++)
        {
            for (int y = hubMin.y;
                 y <= hubMax.y;
                 y++)
            {
                reserved.Add(
                    new Vector2Int(x, y));
            }
        }

        for (int i = 0;
             i < doors.Count;
             i++)
        {
            DreamProceduralDoorLane lane =
                doors[i];

            List<Vector2Int> cells =
                new List<Vector2Int>(
                    lane.LocalInsideCells);

            cells.Sort(
                CompareCells);

            if (lane.Direction ==
                    DreamRoomDoorDirection.North ||
                lane.Direction ==
                    DreamRoomDoorDirection.South)
            {
                int x0 =
                    cells[0].x;

                int x1 =
                    cells[1].x;

                int doorY =
                    cells[0].y;

                int minY =
                    Math.Min(
                        doorY,
                        hubMin.y);

                int maxY =
                    Math.Max(
                        doorY,
                        hubMax.y);

                for (int y = minY;
                     y <= maxY;
                     y++)
                {
                    reserved.Add(
                        new Vector2Int(
                            x0,
                            y));

                    reserved.Add(
                        new Vector2Int(
                            x1,
                            y));
                }

                int minX =
                    Math.Min(
                        Math.Min(
                            x0,
                            x1),
                        hubMin.x);

                int maxX =
                    Math.Max(
                        Math.Max(
                            x0,
                            x1),
                        hubMax.x);

                for (int x = minX;
                     x <= maxX;
                     x++)
                {
                    reserved.Add(
                        new Vector2Int(
                            x,
                            hubMin.y));

                    reserved.Add(
                        new Vector2Int(
                            x,
                            hubMax.y));
                }
            }
            else
            {
                int y0 =
                    cells[0].y;

                int y1 =
                    cells[1].y;

                int doorX =
                    cells[0].x;

                int minX =
                    Math.Min(
                        doorX,
                        hubMin.x);

                int maxX =
                    Math.Max(
                        doorX,
                        hubMax.x);

                for (int x = minX;
                     x <= maxX;
                     x++)
                {
                    reserved.Add(
                        new Vector2Int(
                            x,
                            y0));

                    reserved.Add(
                        new Vector2Int(
                            x,
                            y1));
                }

                int minY =
                    Math.Min(
                        Math.Min(
                            y0,
                            y1),
                        hubMin.y);

                int maxY =
                    Math.Max(
                        Math.Max(
                            y0,
                            y1),
                        hubMax.y);

                for (int y = minY;
                     y <= maxY;
                     y++)
                {
                    reserved.Add(
                        new Vector2Int(
                            hubMin.x,
                            y));

                    reserved.Add(
                        new Vector2Int(
                            hubMax.x,
                            y));
                }
            }
        }

        reserved.RemoveWhere(
            delegate(Vector2Int cell)
            {
                return !Inside(
                    profile,
                    cell);
            });

        return reserved;
    }

    private static void
        TryFillMinimumWithSingleCells(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            HashSet<Vector2Int> blocked,
            HashSet<Vector2Int> reserved,
            IReadOnlyList<DreamProceduralDoorLane>
                doors,
            int minimumBlocked,
            System.Random random)
    {
        if (blocked.Count >= minimumBlocked)
        {
            return;
        }

        List<Vector2Int> candidates =
            new List<Vector2Int>();

        for (int x = 1;
             x < profile.SizeInCells.x - 1;
             x++)
        {
            for (int y = 1;
                 y < profile.SizeInCells.y - 1;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y);

                if (reserved.Contains(cell) ||
                    blocked.Contains(cell))
                {
                    continue;
                }

                candidates.Add(cell);
            }
        }

        // Fisher-Yates，使用同一 Seed Random，确保完全可复现。
        for (int i = candidates.Count - 1;
             i > 0;
             i--)
        {
            int swap =
                random.Next(
                    0,
                    i + 1);

            Vector2Int temp =
                candidates[i];

            candidates[i] =
                candidates[swap];

            candidates[swap] =
                temp;
        }

        for (int i = 0;
             i < candidates.Count &&
             blocked.Count < minimumBlocked;
             i++)
        {
            Vector2Int cell =
                candidates[i];

            blocked.Add(cell);

            if (!AllWalkableConnected(
                    profile,
                    blocked) ||
                !AllDoorsConnected(
                    profile,
                    blocked,
                    doors))
            {
                blocked.Remove(cell);
            }
        }
    }

    private static Vector2Int
        ChooseObstacleSize(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            System.Random random)
    {
        return
            profile.ObstacleSizes[
                random.Next(
                    0,
                    profile.ObstacleSizes.Count)];
    }

    private static bool TryChooseOrigin(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        System.Random random,
        DreamProceduralRoomArchetype archetype,
        Vector2Int shape,
        out Vector2Int origin)
    {
        origin =
            Vector2Int.zero;

        int maxX =
            profile.SizeInCells.x -
            1 -
            shape.x;

        int maxY =
            profile.SizeInCells.y -
            1 -
            shape.y;

        if (maxX < 1 ||
            maxY < 1)
        {
            return false;
        }

        int x =
            random.Next(
                1,
                maxX + 1);

        int y =
            random.Next(
                1,
                maxY + 1);

        if (archetype ==
                DreamProceduralRoomArchetype.EdgeHeavy &&
            random.NextDouble() < 0.70)
        {
            if (random.Next(2) == 0)
            {
                y =
                    random.Next(2) == 0
                        ? 1
                        : maxY;
            }
            else
            {
                x =
                    random.Next(2) == 0
                        ? 1
                        : maxX;
            }
        }
        else if (archetype ==
                     DreamProceduralRoomArchetype.TwinBanks &&
                 random.NextDouble() < 0.75)
        {
            int leftMax =
                Mathf.Max(
                    2,
                    profile.SizeInCells.x /
                    3);

            int rightMin =
                Mathf.Clamp(
                    profile.SizeInCells.x *
                    2 /
                    3 -
                    shape.x,
                    1,
                    maxX);

            if (random.Next(2) == 0)
            {
                x =
                    random.Next(
                        1,
                        Mathf.Min(
                            maxX,
                            leftMax) +
                        1);
            }
            else
            {
                x =
                    random.Next(
                        rightMin,
                        maxX + 1);
            }
        }
        else if (archetype ==
                     DreamProceduralRoomArchetype.OffsetBlocks &&
                 random.NextDouble() < 0.65)
        {
            int lowerMax =
                Mathf.Max(
                    2,
                    profile.SizeInCells.y /
                    2);

            int upperMin =
                Mathf.Clamp(
                    profile.SizeInCells.y /
                    2 -
                    shape.y +
                    1,
                    1,
                    maxY);

            if (random.Next(2) == 0)
            {
                y =
                    random.Next(
                        1,
                        Mathf.Min(
                            maxY,
                            lowerMax) +
                        1);
            }
            else
            {
                y =
                    random.Next(
                        upperMin,
                        maxY + 1);
            }
        }

        origin =
            new Vector2Int(
                x,
                y);

        return true;
    }

    private static List<Vector2Int>
        CollectRect(
            Vector2Int origin,
            Vector2Int size)
    {
        List<Vector2Int> cells =
            new List<Vector2Int>(
                size.x *
                size.y);

        for (int x = 0;
             x < size.x;
             x++)
        {
            for (int y = 0;
                 y < size.y;
                 y++)
            {
                cells.Add(
                    origin +
                    new Vector2Int(
                        x,
                        y));
            }
        }

        return cells;
    }

    private static bool CanPlace(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        IReadOnlyList<Vector2Int> cells,
        HashSet<Vector2Int> reserved,
        HashSet<Vector2Int> blocked)
    {
        for (int i = 0;
             i < cells.Count;
             i++)
        {
            Vector2Int cell =
                cells[i];

            // 中精度 Hard Structure 不占外围格，
            // 让 Shell / Socket / DoorBlocker 始终保持独立。
            if (cell.x <= 0 ||
                cell.y <= 0 ||
                cell.x >=
                    profile.SizeInCells.x - 1 ||
                cell.y >=
                    profile.SizeInCells.y - 1)
            {
                return false;
            }

            if (reserved.Contains(
                    cell) ||
                blocked.Contains(
                    cell))
            {
                return false;
            }
        }

        return true;
    }

    private static bool
        AllDoorsConnected(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            HashSet<Vector2Int> blocked,
            IReadOnlyList<DreamProceduralDoorLane>
                doors)
    {
        if (doors == null ||
            doors.Count == 0)
        {
            return false;
        }

        Vector2Int start =
            doors[0]
                .LocalInsideCells[0];

        HashSet<Vector2Int> reachable =
            Reachable(
                profile,
                blocked,
                start);

        for (int d = 0;
             d < doors.Count;
             d++)
        {
            for (int c = 0;
                 c <
                 doors[d]
                     .LocalInsideCells
                     .Count;
                 c++)
            {
                if (!reachable.Contains(
                        doors[d]
                            .LocalInsideCells[c]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool
        AllWalkableConnected(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            HashSet<Vector2Int> blocked)
    {
        Vector2Int first =
            default(Vector2Int);

        bool found =
            false;

        int walkable =
            0;

        for (int x = 0;
             x <
             profile.SizeInCells.x;
             x++)
        {
            for (int y = 0;
                 y <
                 profile.SizeInCells.y;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x,
                        y);

                if (blocked.Contains(
                        cell))
                {
                    continue;
                }

                walkable++;

                if (!found)
                {
                    first = cell;
                    found = true;
                }
            }
        }

        return
            found &&
            Reachable(
                profile,
                blocked,
                first).Count ==
            walkable;
    }

    private static HashSet<Vector2Int>
        Reachable(
            DreamProceduralRoomFamilyProfileP1012B1
                profile,
            HashSet<Vector2Int> blocked,
            Vector2Int start)
    {
        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        if (!Inside(
                profile,
                start) ||
            blocked.Contains(
                start))
        {
            return visited;
        }

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current =
                queue.Dequeue();

            for (int i = 0;
                 i < Cardinal.Length;
                 i++)
            {
                Vector2Int next =
                    current +
                    Cardinal[i];

                if (!Inside(
                        profile,
                        next) ||
                    blocked.Contains(
                        next) ||
                    !visited.Add(
                        next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return visited;
    }

    private static bool Inside(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        Vector2Int cell)
    {
        return
            cell.x >= 0 &&
            cell.y >= 0 &&
            cell.x <
                profile.SizeInCells.x &&
            cell.y <
                profile.SizeInCells.y;
    }

    private static bool OnExpectedBoundary(
        DreamProceduralRoomFamilyProfileP1012B1
            profile,
        DreamRoomDoorDirection direction,
        Vector2Int cell)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return
                    cell.y ==
                    profile.SizeInCells.y - 1;

            case DreamRoomDoorDirection.East:
                return
                    cell.x ==
                    profile.SizeInCells.x - 1;

            case DreamRoomDoorDirection.South:
                return
                    cell.y == 0;

            case DreamRoomDoorDirection.West:
                return
                    cell.x == 0;

            default:
                return false;
        }
    }

    private static int CompareCells(
        Vector2Int a,
        Vector2Int b)
    {
        int x =
            a.x.CompareTo(
                b.x);

        return
            x != 0
                ? x
                : a.y.CompareTo(
                    b.y);
    }
}
