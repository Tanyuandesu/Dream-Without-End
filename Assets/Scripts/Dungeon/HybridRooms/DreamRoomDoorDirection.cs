using UnityEngine;

/// <summary>
/// 房间门口朝向。
/// 数值顺序按顺时针排列，方便以后处理 90 度旋转。
/// </summary>
public enum DreamRoomDoorDirection
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

/// <summary>
/// 门口朝向的通用换算。
/// 这里只处理纯数据，不依赖 DungeonGenerator 或 DungeonRenderer。
/// </summary>
public static class DreamRoomDoorDirectionUtility
{
    public static Vector2Int ToCellOffset(
        this DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return Vector2Int.up;

            case DreamRoomDoorDirection.East:
                return Vector2Int.right;

            case DreamRoomDoorDirection.South:
                return Vector2Int.down;

            case DreamRoomDoorDirection.West:
                return Vector2Int.left;

            default:
                return Vector2Int.zero;
        }
    }

    public static DreamRoomDoorDirection Opposite(
        this DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return DreamRoomDoorDirection.South;

            case DreamRoomDoorDirection.East:
                return DreamRoomDoorDirection.West;

            case DreamRoomDoorDirection.South:
                return DreamRoomDoorDirection.North;

            case DreamRoomDoorDirection.West:
                return DreamRoomDoorDirection.East;

            default:
                return direction;
        }
    }

    public static Vector2Int PerpendicularCellOffset(
        this DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
            case DreamRoomDoorDirection.South:
                return Vector2Int.right;

            case DreamRoomDoorDirection.East:
            case DreamRoomDoorDirection.West:
                return Vector2Int.up;

            default:
                return Vector2Int.zero;
        }
    }

    public static DreamRoomDoorDirection RotateClockwise(
        this DreamRoomDoorDirection direction,
        int quarterTurns)
    {
        int normalizedTurns =
            ((quarterTurns % 4) + 4) % 4;

        int rotatedValue =
            ((int)direction + normalizedTurns) % 4;

        return (DreamRoomDoorDirection)rotatedValue;
    }
}
