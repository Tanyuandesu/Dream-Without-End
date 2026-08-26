using System;
using System.Collections.Generic;
using UnityEngine;

public enum DreamProceduralRoomArchetype
{
    EdgeHeavy = 0,
    TwinBanks = 1,
    OffsetBlocks = 2,
    Scattered = 3
}

[Serializable]
public sealed class DreamProceduralDoorLane
{
    [SerializeField] private DreamRoomDoorDirection direction;
    [SerializeField] private List<Vector2Int> localInsideCells = new List<Vector2Int>();

    public DreamRoomDoorDirection Direction => direction;
    public IReadOnlyList<Vector2Int> LocalInsideCells => localInsideCells;

    public DreamProceduralDoorLane(DreamRoomDoorDirection direction, IEnumerable<Vector2Int> cells)
    {
        this.direction = direction;
        if (cells != null) localInsideCells.AddRange(cells);
    }
}

public sealed class DreamProceduralRoomLayout
{
    public Vector2Int SizeInCells { get; }
    public int Seed { get; }
    public DreamProceduralRoomArchetype Archetype { get; }
    public HashSet<Vector2Int> BlockedCells { get; }
    public HashSet<Vector2Int> ReservedMainRouteCells { get; }
    public List<DreamProceduralDoorLane> UsedDoorLanes { get; }

    public int TotalCells => SizeInCells.x * SizeInCells.y;
    public int WalkableCellCount => TotalCells - BlockedCells.Count;
    public float BlockedRatio => TotalCells <= 0 ? 0f : (float)BlockedCells.Count / TotalCells;

    public DreamProceduralRoomLayout(
        Vector2Int size,
        int seed,
        DreamProceduralRoomArchetype archetype,
        HashSet<Vector2Int> blocked,
        HashSet<Vector2Int> reserved,
        List<DreamProceduralDoorLane> doors)
    {
        SizeInCells = size;
        Seed = seed;
        Archetype = archetype;
        BlockedCells = blocked != null ? new HashSet<Vector2Int>(blocked) : new HashSet<Vector2Int>();
        ReservedMainRouteCells = reserved != null ? new HashSet<Vector2Int>(reserved) : new HashSet<Vector2Int>();
        UsedDoorLanes = doors != null ? new List<DreamProceduralDoorLane>(doors) : new List<DreamProceduralDoorLane>();
    }
}

/// <summary>
/// P10.12A-1：13x9 中精度房间纯数据生成核。
/// 当前故意不修改 DungeonLayout、不创建 Collider、不接 Production_Main。
/// 下一阶段会让 BlockedCells 与 Collider 同源消费这里的结果。
/// </summary>
public static class DreamProceduralRoomKernelP1012A1
{
    public static readonly Vector2Int MediumSize = new Vector2Int(13, 9);
    public const float MinimumBlockedRatio = 0.15f;
    public const float MaximumBlockedRatio = 0.35f;

    private static readonly Vector2Int[] Cardinal =
    {
        Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
    };

    private static readonly Vector2Int[] ObstacleSizes =
    {
        new Vector2Int(1,1), new Vector2Int(1,2), new Vector2Int(2,1),
        new Vector2Int(2,2), new Vector2Int(1,3), new Vector2Int(3,1)
    };

    public static int DeriveRoomSeed(int floorSeed, int roomIndex, int shellTypeHash, int socketMask)
    {
        unchecked
        {
            uint v = (uint)floorSeed;
            v ^= (uint)(roomIndex + 1) * 0x9E3779B9u;
            v ^= (uint)shellTypeHash * 0x85EBCA6Bu;
            v ^= (uint)socketMask * 0xC2B2AE35u;
            v ^= v >> 16; v *= 0x7FEB352Du;
            v ^= v >> 15; v *= 0x846CA68Bu;
            v ^= v >> 16;
            return (int)v;
        }
    }

    public static bool TryGenerate(
        int seed,
        IReadOnlyList<DreamProceduralDoorLane> usedDoors,
        out DreamProceduralRoomLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = string.Empty;

        List<DreamProceduralDoorLane> doors;
        if (!TryNormalizeDoors(usedDoors, out doors, out failureReason)) return false;

        System.Random random = new System.Random(seed);
        DreamProceduralRoomArchetype archetype =
            (DreamProceduralRoomArchetype)random.Next(0, 4);

        HashSet<Vector2Int> reserved = BuildReservedBackbone(doors);
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        int total = MediumSize.x * MediumSize.y;
        float targetRatio = Mathf.Lerp(0.20f, 0.29f, (float)random.NextDouble());
        int target = Mathf.Clamp(
            Mathf.RoundToInt(total * targetRatio),
            Mathf.CeilToInt(total * MinimumBlockedRatio),
            Mathf.FloorToInt(total * MaximumBlockedRatio));

        for (int attempt = 0; attempt < 400 && blocked.Count < target; attempt++)
        {
            Vector2Int shape = ObstacleSizes[random.Next(ObstacleSizes.Length)];
            Vector2Int origin = ChooseOrigin(random, archetype, shape);
            List<Vector2Int> cells = CollectRect(origin, shape);

            if (!CanPlace(cells, reserved, blocked)) continue;
            if (blocked.Count + cells.Count > Mathf.FloorToInt(total * MaximumBlockedRatio)) continue;

            for (int i = 0; i < cells.Count; i++) blocked.Add(cells[i]);

            if (!AllWalkableConnected(blocked) || !AllDoorsConnected(blocked, doors))
            {
                for (int i = 0; i < cells.Count; i++) blocked.Remove(cells[i]);
            }
        }

        float ratio = (float)blocked.Count / total;
        if (ratio < MinimumBlockedRatio || ratio > MaximumBlockedRatio)
        {
            failureReason = "BlockedRatio 越界：" + (ratio * 100f).ToString("F1") + "%";
            return false;
        }

        if (!AllWalkableConnected(blocked))
        {
            failureReason = "Walkable Cells 不是单一四方向连通区域。";
            return false;
        }

        if (!AllDoorsConnected(blocked, doors))
        {
            failureReason = "Used Socket 没有全部互通。";
            return false;
        }

        foreach (Vector2Int cell in reserved)
        {
            if (blocked.Contains(cell))
            {
                failureReason = "Blocked 侵入 2 Cell 保底主通路：" + cell;
                return false;
            }
        }

        layout = new DreamProceduralRoomLayout(
            MediumSize, seed, archetype, blocked, reserved, doors);
        return true;
    }

    public static bool Validate(DreamProceduralRoomLayout layout, out string failureReason)
    {
        failureReason = string.Empty;
        if (layout == null) { failureReason = "Layout 为空。"; return false; }
        if (layout.SizeInCells != MediumSize) { failureReason = "Prototype 必须是 13x9。"; return false; }
        if (layout.BlockedRatio < MinimumBlockedRatio || layout.BlockedRatio > MaximumBlockedRatio)
        { failureReason = "BlockedRatio 不在 15%～35%。"; return false; }
        if (!AllWalkableConnected(layout.BlockedCells))
        { failureReason = "Walkable Cells 不连通。"; return false; }
        if (!AllDoorsConnected(layout.BlockedCells, layout.UsedDoorLanes))
        { failureReason = "Used Socket 不互通。"; return false; }
        foreach (Vector2Int cell in layout.ReservedMainRouteCells)
            if (layout.BlockedCells.Contains(cell))
            { failureReason = "Blocked 覆盖保底主通路：" + cell; return false; }
        return true;
    }

    public static List<DreamProceduralDoorLane> BuildDefaultDoorSet(bool north, bool east, bool south, bool west)
    {
        List<DreamProceduralDoorLane> result = new List<DreamProceduralDoorLane>();
        if (north) result.Add(new DreamProceduralDoorLane(DreamRoomDoorDirection.North,
            new[] { new Vector2Int(5,8), new Vector2Int(6,8) }));
        if (east) result.Add(new DreamProceduralDoorLane(DreamRoomDoorDirection.East,
            new[] { new Vector2Int(12,3), new Vector2Int(12,4) }));
        if (south) result.Add(new DreamProceduralDoorLane(DreamRoomDoorDirection.South,
            new[] { new Vector2Int(5,0), new Vector2Int(6,0) }));
        if (west) result.Add(new DreamProceduralDoorLane(DreamRoomDoorDirection.West,
            new[] { new Vector2Int(0,3), new Vector2Int(0,4) }));
        return result;
    }

    private static bool TryNormalizeDoors(
        IReadOnlyList<DreamProceduralDoorLane> input,
        out List<DreamProceduralDoorLane> doors,
        out string reason)
    {
        doors = new List<DreamProceduralDoorLane>();
        reason = string.Empty;
        if (input == null || input.Count == 0) { reason = "至少需要一个 Used Socket。"; return false; }

        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        for (int i = 0; i < input.Count; i++)
        {
            DreamProceduralDoorLane lane = input[i];
            if (lane == null || lane.LocalInsideCells == null || lane.LocalInsideCells.Count != 2)
            { reason = "P10.12A-1 每个 Used Socket 必须正好占 2 Cell。"; return false; }

            List<Vector2Int> cells = new List<Vector2Int>(lane.LocalInsideCells);
            cells.Sort(CompareCells);
            for (int c = 0; c < cells.Count; c++)
            {
                Vector2Int cell = cells[c];
                if (!Inside(cell)) { reason = "Door Cell 超出 13x9：" + cell; return false; }
                if (!OnExpectedBoundary(lane.Direction, cell))
                { reason = "Door Cell " + cell + " 不在 " + lane.Direction + " 边界。"; return false; }
                if (!used.Add(cell)) { reason = "Used Socket 共享 Door Cell：" + cell; return false; }
            }
            doors.Add(new DreamProceduralDoorLane(lane.Direction, cells));
        }
        return true;
    }

    private static HashSet<Vector2Int> BuildReservedBackbone(IReadOnlyList<DreamProceduralDoorLane> doors)
    {
        HashSet<Vector2Int> r = new HashSet<Vector2Int>();
        // 13x9 的中央 2x2 Hub = x 5..6, y 3..4。
        for (int x = 5; x <= 6; x++) for (int y = 3; y <= 4; y++) r.Add(new Vector2Int(x,y));

        for (int i = 0; i < doors.Count; i++)
        {
            DreamProceduralDoorLane lane = doors[i];
            List<Vector2Int> cells = new List<Vector2Int>(lane.LocalInsideCells);
            cells.Sort(CompareCells);

            if (lane.Direction == DreamRoomDoorDirection.North || lane.Direction == DreamRoomDoorDirection.South)
            {
                int x0 = cells[0].x, x1 = cells[1].x, doorY = cells[0].y;
                int minY = Math.Min(doorY, 3), maxY = Math.Max(doorY, 4);
                for (int y = minY; y <= maxY; y++) { r.Add(new Vector2Int(x0,y)); r.Add(new Vector2Int(x1,y)); }
                int minX = Math.Min(Math.Min(x0,x1),5), maxX = Math.Max(Math.Max(x0,x1),6);
                for (int x = minX; x <= maxX; x++) { r.Add(new Vector2Int(x,3)); r.Add(new Vector2Int(x,4)); }
            }
            else
            {
                int y0 = cells[0].y, y1 = cells[1].y, doorX = cells[0].x;
                int minX = Math.Min(doorX,5), maxX = Math.Max(doorX,6);
                for (int x = minX; x <= maxX; x++) { r.Add(new Vector2Int(x,y0)); r.Add(new Vector2Int(x,y1)); }
                int minY = Math.Min(Math.Min(y0,y1),3), maxY = Math.Max(Math.Max(y0,y1),4);
                for (int y = minY; y <= maxY; y++) { r.Add(new Vector2Int(5,y)); r.Add(new Vector2Int(6,y)); }
            }
        }
        r.RemoveWhere(cell => !Inside(cell));
        return r;
    }

    private static Vector2Int ChooseOrigin(System.Random random, DreamProceduralRoomArchetype archetype, Vector2Int shape)
    {
        int maxX = MediumSize.x - 1 - shape.x;
        int maxY = MediumSize.y - 1 - shape.y;
        int x = random.Next(1, maxX + 1);
        int y = random.Next(1, maxY + 1);

        if (archetype == DreamProceduralRoomArchetype.EdgeHeavy && random.NextDouble() < 0.70)
        {
            if (random.Next(2) == 0) y = random.Next(2) == 0 ? 1 : maxY;
            else x = random.Next(2) == 0 ? 1 : maxX;
        }
        else if (archetype == DreamProceduralRoomArchetype.TwinBanks && random.NextDouble() < 0.75)
        {
            x = random.Next(2) == 0 ? random.Next(1, 4) : random.Next(Math.Max(1, 8 - shape.x), maxX + 1);
        }
        else if (archetype == DreamProceduralRoomArchetype.OffsetBlocks && random.NextDouble() < 0.65)
        {
            y = random.Next(2) == 0 ? random.Next(1, 4) : random.Next(Math.Max(1, 5 - shape.y), maxY + 1);
        }
        return new Vector2Int(x,y);
    }

    private static List<Vector2Int> CollectRect(Vector2Int origin, Vector2Int size)
    {
        List<Vector2Int> cells = new List<Vector2Int>(size.x * size.y);
        for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) cells.Add(origin + new Vector2Int(x,y));
        return cells;
    }

    private static bool CanPlace(IReadOnlyList<Vector2Int> cells, HashSet<Vector2Int> reserved, HashSet<Vector2Int> blocked)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int c = cells[i];
            if (c.x <= 0 || c.y <= 0 || c.x >= MediumSize.x - 1 || c.y >= MediumSize.y - 1) return false;
            if (reserved.Contains(c) || blocked.Contains(c)) return false;
        }
        return true;
    }

    private static bool AllDoorsConnected(HashSet<Vector2Int> blocked, IReadOnlyList<DreamProceduralDoorLane> doors)
    {
        if (doors == null || doors.Count == 0) return false;
        Vector2Int start = doors[0].LocalInsideCells[0];
        HashSet<Vector2Int> reachable = Reachable(blocked, start);
        for (int d = 0; d < doors.Count; d++)
            for (int c = 0; c < doors[d].LocalInsideCells.Count; c++)
                if (!reachable.Contains(doors[d].LocalInsideCells[c])) return false;
        return true;
    }

    private static bool AllWalkableConnected(HashSet<Vector2Int> blocked)
    {
        Vector2Int first = default(Vector2Int);
        bool found = false;
        int walkable = 0;
        for (int x = 0; x < MediumSize.x; x++) for (int y = 0; y < MediumSize.y; y++)
        {
            Vector2Int c = new Vector2Int(x,y);
            if (blocked.Contains(c)) continue;
            walkable++;
            if (!found) { first = c; found = true; }
        }
        return found && Reachable(blocked, first).Count == walkable;
    }

    private static HashSet<Vector2Int> Reachable(HashSet<Vector2Int> blocked, Vector2Int start)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        if (!Inside(start) || blocked.Contains(start)) return visited;
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        visited.Add(start); q.Enqueue(start);
        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            for (int i = 0; i < Cardinal.Length; i++)
            {
                Vector2Int next = cur + Cardinal[i];
                if (!Inside(next) || blocked.Contains(next) || !visited.Add(next)) continue;
                q.Enqueue(next);
            }
        }
        return visited;
    }

    private static bool Inside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < MediumSize.x && c.y < MediumSize.y;

    private static bool OnExpectedBoundary(DreamRoomDoorDirection direction, Vector2Int cell)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North: return cell.y == MediumSize.y - 1;
            case DreamRoomDoorDirection.East: return cell.x == MediumSize.x - 1;
            case DreamRoomDoorDirection.South: return cell.y == 0;
            case DreamRoomDoorDirection.West: return cell.x == 0;
            default: return false;
        }
    }

    private static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int x = a.x.CompareTo(b.x);
        return x != 0 ? x : a.y.CompareTo(b.y);
    }
}
