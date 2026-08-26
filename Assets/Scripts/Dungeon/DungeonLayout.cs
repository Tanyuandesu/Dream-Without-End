using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次迷宫生成的纯数据结果。
/// 不负责显示，也不依赖 Tilemap、场景实例或 Renderer。
///
/// R2 兼容规则：
/// 1. Rooms、FloorCells、StartCell、ExitCell、Seed 完整保留。
/// 2. 旧五参数构造函数完整保留，当前 DungeonGenerator 无需修改。
/// 3. 新增房间 Prefab 摆放、房间格、走廊格和逻辑连接集合。
/// 4. 旧构造函数建立的布局，其四个混合集合为空，不伪造拆分结果。
/// </summary>
public sealed class DungeonLayout
{
    // 现有系统继续使用的旧数据。
    public List<RectInt> Rooms { get; }
    public HashSet<Vector2Int> FloorCells { get; }

    public Vector2Int StartCell { get; }
    public Vector2Int ExitCell { get; }

    public int Seed { get; }

    // 混合 Prefab 房间系统使用的新数据。
    // RoomCells 是房间提供的可行走格；
    // 房间完整占用轮廓由各 Placement 的 Occupied Cells 保留。
    public List<DreamRoomPlacement> RoomPlacements { get; }
    public HashSet<Vector2Int> RoomCells { get; }
    public HashSet<Vector2Int> CorridorCells { get; }
    public List<DreamRoomConnection> Connections { get; }

    /// <summary>
    /// 只要任一混合集合含有数据，就视为混合布局。
    /// 旧生成器通过五参数构造函数得到的布局始终为 false。
    /// </summary>
    public bool HasHybridRoomData =>
        RoomPlacements.Count > 0 ||
        RoomCells.Count > 0 ||
        CorridorCells.Count > 0 ||
        Connections.Count > 0;

    /// <summary>
    /// 现有 DungeonGenerator 使用的原构造函数。
    /// 签名与字段含义不变，新集合会安全初始化为空。
    /// </summary>
    public DungeonLayout(
        List<RectInt> rooms,
        HashSet<Vector2Int> floorCells,
        Vector2Int startCell,
        Vector2Int exitCell,
        int seed)
        : this(
            rooms,
            floorCells,
            startCell,
            exitCell,
            seed,
            new List<DreamRoomPlacement>(),
            new HashSet<Vector2Int>(),
            new HashSet<Vector2Int>(),
            new List<DreamRoomConnection>())
    {
    }

    /// <summary>
    /// 后续混合生成器使用的完整构造函数。
    /// 调用方仍需保证 FloorCells 等于 RoomCells 与 CorridorCells 的合集；
    /// GetValidationErrors 会检查该约束。
    /// </summary>
    public DungeonLayout(
        List<RectInt> rooms,
        HashSet<Vector2Int> floorCells,
        Vector2Int startCell,
        Vector2Int exitCell,
        int seed,
        List<DreamRoomPlacement> roomPlacements,
        HashSet<Vector2Int> roomCells,
        HashSet<Vector2Int> corridorCells,
        List<DreamRoomConnection> connections)
    {
        Rooms = rooms ?? new List<RectInt>();
        FloorCells = floorCells ??
            new HashSet<Vector2Int>();

        StartCell = startCell;
        ExitCell = exitCell;
        Seed = seed;

        RoomPlacements = roomPlacements ??
            new List<DreamRoomPlacement>();

        RoomCells = roomCells ??
            new HashSet<Vector2Int>();

        CorridorCells = corridorCells ??
            new HashSet<Vector2Int>();

        Connections = connections ??
            new List<DreamRoomConnection>();
    }

    /// <summary>
    /// 从房间摆放和走廊格建立一份内部一致的混合布局。
    ///
    /// Rooms 会保存每个摆放的旋转后矩形边界，供旧接口继续读取；
    /// RoomCells 来自所有 Placement 的真实 Walkable Cells；
    /// Placement 的 Occupied Cells 仍用于房间碰撞和非矩形轮廓；
    /// FloorCells 自动等于 RoomCells 与 CorridorCells 的合集。
    /// </summary>
    public static DungeonLayout CreateHybrid(
        IEnumerable<DreamRoomPlacement> roomPlacements,
        IEnumerable<Vector2Int> corridorCells,
        IEnumerable<DreamRoomConnection> connections,
        Vector2Int startCell,
        Vector2Int exitCell,
        int seed)
    {
        if (roomPlacements == null)
        {
            throw new ArgumentNullException(
                nameof(roomPlacements));
        }

        List<DreamRoomPlacement> placementList =
            new List<DreamRoomPlacement>(roomPlacements);

        List<RectInt> legacyRoomBounds =
            new List<RectInt>(placementList.Count);

        HashSet<Vector2Int> collectedRoomCells =
            new HashSet<Vector2Int>();

        List<Vector2Int> placementCells =
            new List<Vector2Int>();

        for (int i = 0; i < placementList.Count; i++)
        {
            DreamRoomPlacement placement =
                placementList[i];

            if (placement == null)
            {
                continue;
            }

            legacyRoomBounds.Add(placement.CellBounds);

            placement.GetWalkableGlobalCells(
                placementCells);

            collectedRoomCells.UnionWith(
                placementCells);
        }

        HashSet<Vector2Int> collectedCorridorCells =
            corridorCells == null
                ? new HashSet<Vector2Int>()
                : new HashSet<Vector2Int>(corridorCells);

        HashSet<Vector2Int> combinedFloorCells =
            new HashSet<Vector2Int>(collectedRoomCells);

        combinedFloorCells.UnionWith(
            collectedCorridorCells);

        List<DreamRoomConnection> connectionList =
            connections == null
                ? new List<DreamRoomConnection>()
                : new List<DreamRoomConnection>(connections);

        return new DungeonLayout(
            legacyRoomBounds,
            combinedFloorCells,
            startCell,
            exitCell,
            seed,
            placementList,
            collectedRoomCells,
            collectedCorridorCells,
            connectionList);
    }

    public bool IsRoomCell(Vector2Int cell)
    {
        return RoomCells.Contains(cell);
    }

    public bool IsCorridorCell(Vector2Int cell)
    {
        return CorridorCells.Contains(cell);
    }

    /// <summary>
    /// 返回布局数据中的内部矛盾。
    /// 这只做读取与报告，不会自动改写任何集合。
    /// </summary>
    public List<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();

        if (FloorCells.Count == 0)
        {
            errors.Add("FloorCells 不能为空。");
        }
        else
        {
            if (!FloorCells.Contains(StartCell))
            {
                errors.Add(
                    "StartCell " + StartCell +
                    " 不属于 FloorCells。");
            }

            if (!FloorCells.Contains(ExitCell))
            {
                errors.Add(
                    "ExitCell " + ExitCell +
                    " 不属于 FloorCells。");
            }
        }

        if (!HasHybridRoomData)
        {
            return errors;
        }

        ValidateHybridCollections(errors);
        return errors;
    }

    private void ValidateHybridCollections(
        List<string> errors)
    {
        if (RoomPlacements.Count == 0)
        {
            errors.Add(
                "混合布局至少需要一个 RoomPlacement。");
        }

        HashSet<Vector2Int> expectedRoomCells =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> occupiedRoomCells =
            new HashSet<Vector2Int>();

        List<Vector2Int> placementCells =
            new List<Vector2Int>();

        List<Vector2Int> placementOccupiedCells =
            new List<Vector2Int>();

        int validPlacementCount = 0;

        for (int i = 0; i < RoomPlacements.Count; i++)
        {
            DreamRoomPlacement placement =
                RoomPlacements[i];

            if (placement == null)
            {
                errors.Add(
                    "RoomPlacements 的 Element " +
                    i + " 是空引用。");
                continue;
            }

            validPlacementCount++;

            List<string> placementErrors =
                placement.GetValidationErrors();

            for (int errorIndex = 0;
                 errorIndex < placementErrors.Count;
                 errorIndex++)
            {
                errors.Add(
                    "RoomPlacement " + i + "：" +
                    placementErrors[errorIndex]);
            }

            placement.GetOccupiedGlobalCells(
                placementOccupiedCells);

            bool foundOverlap = false;

            for (int cellIndex = 0;
                 cellIndex < placementOccupiedCells.Count;
                 cellIndex++)
            {
                if (!occupiedRoomCells.Add(
                        placementOccupiedCells[cellIndex]))
                {
                    foundOverlap = true;
                }
            }

            placement.GetWalkableGlobalCells(
                placementCells);

            for (int cellIndex = 0;
                 cellIndex < placementCells.Count;
                 cellIndex++)
            {
                expectedRoomCells.Add(
                    placementCells[cellIndex]);
            }

            if (foundOverlap)
            {
                errors.Add(
                    "RoomPlacement " + i +
                    " 与此前房间的 Occupied Cells 重叠。");
            }
        }

        if (Rooms.Count != validPlacementCount)
        {
            errors.Add(
                "混合布局中的 Rooms 数量必须与有效 RoomPlacements 数量一致。" +
                "当前 Rooms=" + Rooms.Count +
                "，Placements=" + validPlacementCount + "。");
        }
        else
        {
            ValidateLegacyRoomBounds(errors);
        }

        if (!RoomCells.SetEquals(expectedRoomCells))
        {
            errors.Add(
                "RoomCells 与所有 RoomPlacements 的 Walkable Cells 合集不一致。");
        }

        HashSet<Vector2Int> expectedFloorCells =
            new HashSet<Vector2Int>(RoomCells);

        expectedFloorCells.UnionWith(CorridorCells);

        if (!FloorCells.SetEquals(expectedFloorCells))
        {
            errors.Add(
                "FloorCells 必须等于 RoomCells 与 CorridorCells 的合集。");
        }

        ValidateConnections(errors);
    }

    private void ValidateLegacyRoomBounds(
        List<string> errors)
    {
        int boundsIndex = 0;

        for (int placementIndex = 0;
             placementIndex < RoomPlacements.Count;
             placementIndex++)
        {
            DreamRoomPlacement placement =
                RoomPlacements[placementIndex];

            if (placement == null)
            {
                continue;
            }

            if (!Rooms[boundsIndex].Equals(
                    placement.CellBounds))
            {
                errors.Add(
                    "Rooms 的 Element " + boundsIndex +
                    " 与对应 RoomPlacement 的 CellBounds 不一致。");
            }

            boundsIndex++;
        }
    }

    private void ValidateConnections(
        List<string> errors)
    {
        HashSet<Vector2Int> usedRoomPairs =
            new HashSet<Vector2Int>();

        for (int i = 0; i < Connections.Count; i++)
        {
            DreamRoomConnection connection =
                Connections[i];

            if (connection == null)
            {
                errors.Add(
                    "Connections 的 Element " +
                    i + " 是空引用。");
                continue;
            }

            List<string> connectionErrors =
                connection.GetValidationErrors(
                    RoomPlacements.Count,
                    requireAssignedSockets: false,
                    requireCorridor: false);

            for (int errorIndex = 0;
                 errorIndex < connectionErrors.Count;
                 errorIndex++)
            {
                errors.Add(
                    "Connection " + i + "：" +
                    connectionErrors[errorIndex]);
            }

            if (connection.RoomAIndex >= 0 &&
                connection.RoomBIndex >= 0 &&
                connection.RoomAIndex !=
                connection.RoomBIndex)
            {
                Vector2Int normalizedPair =
                    new Vector2Int(
                        Mathf.Min(
                            connection.RoomAIndex,
                            connection.RoomBIndex),
                        Mathf.Max(
                            connection.RoomAIndex,
                            connection.RoomBIndex));

                if (!usedRoomPairs.Add(normalizedPair))
                {
                    errors.Add(
                        "Connection " + i +
                        " 与已有连接重复引用同一对房间。");
                }
            }

            if (!connection.HasCorridor)
            {
                continue;
            }

            for (int cellIndex = 0;
                 cellIndex < connection.CorridorCells.Count;
                 cellIndex++)
            {
                Vector2Int cell =
                    connection.CorridorCells[cellIndex];

                if (!CorridorCells.Contains(cell))
                {
                    errors.Add(
                        "Connection " + i +
                        " 的 Corridor Cell " + cell +
                        " 没有收录在布局 CorridorCells 中。");
                    break;
                }
            }
        }
    }
}
