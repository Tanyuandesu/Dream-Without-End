using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次迷宮生成的純資料結果。
/// 不負責顯示，也不依賴 Tilemap 或 Prefab。
/// </summary>
public sealed class DungeonLayout
{
    public List<RectInt> Rooms { get; }
    public HashSet<Vector2Int> FloorCells { get; }

    public Vector2Int StartCell { get; }
    public Vector2Int ExitCell { get; }

    public int Seed { get; }

    public DungeonLayout(
        List<RectInt> rooms,
        HashSet<Vector2Int> floorCells,
        Vector2Int startCell,
        Vector2Int exitCell,
        int seed)
    {
        Rooms = rooms;
        FloorCells = floorCells;
        StartCell = startCell;
        ExitCell = exitCell;
        Seed = seed;
    }
}
