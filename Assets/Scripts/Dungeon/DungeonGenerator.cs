using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 只負責計算迷宮資料：房間、走廊、出生點與出口。
/// 它不建立任何 GameObject。
/// </summary>
[DisallowMultipleComponent]
public sealed partial class DungeonGenerator : MonoBehaviour
{
    [Header("地圖尺寸")]
    [SerializeField] private int mapWidth = 58;
    [SerializeField] private int mapHeight = 38;

    [Header("房間")]
    [SerializeField] private int desiredRoomCount = 9;
    [SerializeField] private Vector2Int minRoomSize = new Vector2Int(5, 5);
    [SerializeField] private Vector2Int maxRoomSize = new Vector2Int(10, 8);
    [SerializeField] private int roomPadding = 1;
    [SerializeField] private int maxPlacementAttempts = 250;

    [Header("走廊")]
    [SerializeField] private int corridorWidth = 2;
    [SerializeField] private int extraConnections = 2;

    [Header("隨機種子")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int fixedSeed = 12345;

    private readonly List<RectInt> rooms = new List<RectInt>();
    private readonly HashSet<Vector2Int> floorCells = new HashSet<Vector2Int>();

    private System.Random random;

    /// <summary>
    /// 生成一層迷宮資料。floorNumber 會參與種子計算。
    /// </summary>
    public DungeonLayout Generate(int floorNumber)
    {
        ValidateSettings();

        rooms.Clear();
        floorCells.Clear();

        int seed = useRandomSeed
            ? unchecked(Environment.TickCount ^ (floorNumber * 73856093))
            : fixedSeed + floorNumber - 1;

        random = new System.Random(seed);

        GenerateRoomLayout();
        CarveRooms();
        ConnectRooms();
        AddExtraConnections();

        Vector2Int startCell = GetRoomCenter(rooms[0]);
        Vector2Int exitCell = FindFarthestRoomCenter(startCell);

        return new DungeonLayout(
            new List<RectInt>(rooms),
            new HashSet<Vector2Int>(floorCells),
            startCell,
            exitCell,
            seed);
    }

    private void GenerateRoomLayout()
    {
        int attempts = 0;

        while (rooms.Count < desiredRoomCount &&
               attempts < maxPlacementAttempts)
        {
            attempts++;

            int width = random.Next(minRoomSize.x, maxRoomSize.x + 1);
            int height = random.Next(minRoomSize.y, maxRoomSize.y + 1);

            int maxX = mapWidth - width - 2;
            int maxY = mapHeight - height - 2;

            if (maxX <= 2 || maxY <= 2)
            {
                break;
            }

            int x = random.Next(2, maxX + 1);
            int y = random.Next(2, maxY + 1);

            RectInt candidate = new RectInt(x, y, width, height);

            if (!OverlapsExistingRoom(candidate))
            {
                rooms.Add(candidate);
            }
        }

        if (rooms.Count < 2)
        {
            CreateFallbackRooms();
        }
    }

    private void CreateFallbackRooms()
    {
        rooms.Clear();

        int width = Mathf.Clamp(minRoomSize.x, 5, mapWidth / 3);
        int height = Mathf.Clamp(minRoomSize.y, 5, mapHeight / 2);

        rooms.Add(new RectInt(
            3,
            Mathf.Max(3, mapHeight / 2 - height / 2),
            width,
            height));

        rooms.Add(new RectInt(
            Mathf.Max(12, mapWidth - width - 4),
            Mathf.Max(3, mapHeight / 2 - height / 2),
            width,
            height));
    }

    private bool OverlapsExistingRoom(RectInt candidate)
    {
        RectInt expanded = new RectInt(
            candidate.xMin - roomPadding,
            candidate.yMin - roomPadding,
            candidate.width + roomPadding * 2,
            candidate.height + roomPadding * 2);

        for (int i = 0; i < rooms.Count; i++)
        {
            if (expanded.Overlaps(rooms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void CarveRooms()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            RectInt room = rooms[i];

            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin; y < room.yMax; y++)
                {
                    floorCells.Add(new Vector2Int(x, y));
                }
            }
        }
    }

    /// <summary>
    /// 每個新房間連接到先前房間中距離最近的一個。
    /// 因此所有房間必定屬於同一個連通圖。
    /// </summary>
    private void ConnectRooms()
    {
        for (int i = 1; i < rooms.Count; i++)
        {
            Vector2Int currentCenter = GetRoomCenter(rooms[i]);

            int nearestIndex = 0;
            int nearestDistance = int.MaxValue;

            for (int j = 0; j < i; j++)
            {
                int distance = ManhattanDistance(
                    currentCenter,
                    GetRoomCenter(rooms[j]));

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = j;
                }
            }

            CarveLCorridor(
                currentCenter,
                GetRoomCenter(rooms[nearestIndex]));
        }
    }

    private void AddExtraConnections()
    {
        if (rooms.Count < 3)
        {
            return;
        }

        for (int i = 0; i < extraConnections; i++)
        {
            int first = random.Next(0, rooms.Count);
            int second = random.Next(0, rooms.Count);

            if (first == second)
            {
                i--;
                continue;
            }

            CarveLCorridor(
                GetRoomCenter(rooms[first]),
                GetRoomCenter(rooms[second]));
        }
    }

    private void CarveLCorridor(Vector2Int start, Vector2Int end)
    {
        bool horizontalFirst = random.NextDouble() < 0.5;

        if (horizontalFirst)
        {
            CarveHorizontal(start.x, end.x, start.y);
            CarveVertical(start.y, end.y, end.x);
        }
        else
        {
            CarveVertical(start.y, end.y, start.x);
            CarveHorizontal(start.x, end.x, end.y);
        }
    }

    private void CarveHorizontal(int startX, int endX, int y)
    {
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);
        int startOffset = -(corridorWidth / 2);

        for (int x = minX; x <= maxX; x++)
        {
            for (int i = 0; i < corridorWidth; i++)
            {
                floorCells.Add(
                    new Vector2Int(x, y + startOffset + i));
            }
        }
    }

    private void CarveVertical(int startY, int endY, int x)
    {
        int minY = Mathf.Min(startY, endY);
        int maxY = Mathf.Max(startY, endY);
        int startOffset = -(corridorWidth / 2);

        for (int y = minY; y <= maxY; y++)
        {
            for (int i = 0; i < corridorWidth; i++)
            {
                floorCells.Add(
                    new Vector2Int(x + startOffset + i, y));
            }
        }
    }

    private Vector2Int FindFarthestRoomCenter(Vector2Int start)
    {
        Vector2Int farthest = start;
        int farthestDistance = -1;

        for (int i = 0; i < rooms.Count; i++)
        {
            Vector2Int center = GetRoomCenter(rooms[i]);
            int distance = ManhattanDistance(start, center);

            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthest = center;
            }
        }

        return farthest;
    }

    private static Vector2Int GetRoomCenter(RectInt room)
    {
        return new Vector2Int(
            room.xMin + room.width / 2,
            room.yMin + room.height / 2);
    }

    private static int ManhattanDistance(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }

    private void ValidateSettings()
    {
        mapWidth = Mathf.Max(mapWidth, 24);
        mapHeight = Mathf.Max(mapHeight, 18);

        desiredRoomCount = Mathf.Clamp(desiredRoomCount, 2, 30);
        corridorWidth = Mathf.Clamp(corridorWidth, 1, 4);
        extraConnections = Mathf.Max(0, extraConnections);
        roomPadding = Mathf.Max(0, roomPadding);
        maxPlacementAttempts = Mathf.Max(20, maxPlacementAttempts);

        minRoomSize.x = Mathf.Clamp(
            minRoomSize.x, 3, mapWidth / 2);

        minRoomSize.y = Mathf.Clamp(
            minRoomSize.y, 3, mapHeight / 2);

        maxRoomSize.x = Mathf.Clamp(
            Mathf.Max(maxRoomSize.x, minRoomSize.x),
            minRoomSize.x,
            mapWidth / 2);

        maxRoomSize.y = Mathf.Clamp(
            Mathf.Max(maxRoomSize.y, minRoomSize.y),
            minRoomSize.y,
            mapHeight / 2);
    }
}
