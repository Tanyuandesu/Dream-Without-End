using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 適用於目前迷宮規模的四方向 A*。
///
/// 特點：
/// 1. 只把 DungeonLayout.FloorCells 視為可通行格。
/// 2. 回傳每一格中心的世界座標。
/// 3. 不再強迫移動中的敵人返回目前格子中心。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPathfinder : MonoBehaviour
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private DungeonLayout layout;
    private float cellSize = 1f;

    public void Initialize(
        DungeonLayout dungeonLayout,
        float dungeonCellSize)
    {
        layout = dungeonLayout;
        cellSize = Mathf.Max(0.01f, dungeonCellSize);
    }

    /// <summary>
    /// 從世界座標尋找到目標所在格。
    /// 回傳的第一個點是起點格之後的下一個格子中心。
    /// </summary>
    public List<Vector2> FindPath(
        Vector2 startWorld,
        Vector2 targetWorld)
    {
        List<Vector2> emptyPath = new List<Vector2>();

        if (layout == null ||
            layout.FloorCells == null ||
            layout.FloorCells.Count == 0)
        {
            return emptyPath;
        }

        Vector2Int start =
            FindNearestWalkableCell(WorldToCell(startWorld));

        Vector2Int goal =
            FindNearestWalkableCell(WorldToCell(targetWorld));

        // 已在同一格時，不需要 A*。
        // TestEnemyAI 會在同格內直接朝玩家或最後位置移動。
        if (start == goal)
        {
            return emptyPath;
        }

        List<Vector2Int> openSet =
            new List<Vector2Int> { start };

        HashSet<Vector2Int> closedSet =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();

        Dictionary<Vector2Int, int> gScore =
            new Dictionary<Vector2Int, int>
            {
                [start] = 0
            };

        while (openSet.Count > 0)
        {
            Vector2Int current =
                GetLowestScoreCell(openSet, gScore, goal);

            if (current == goal)
            {
                List<Vector2Int> cellPath =
                    ReconstructCellPath(
                        cameFrom,
                        current,
                        start);

                return ConvertToWorldPath(cellPath);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbour =
                    current + Directions[i];

                if (!layout.FloorCells.Contains(neighbour) ||
                    closedSet.Contains(neighbour))
                {
                    continue;
                }

                int tentativeG =
                    GetScore(gScore, current) + 1;

                if (tentativeG >= GetScore(gScore, neighbour))
                {
                    continue;
                }

                cameFrom[neighbour] = current;
                gScore[neighbour] = tentativeG;

                if (!openSet.Contains(neighbour))
                {
                    openSet.Add(neighbour);
                }
            }
        }

        return emptyPath;
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / cellSize),
            Mathf.RoundToInt(worldPosition.y / cellSize));
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(
            cell.x * cellSize,
            cell.y * cellSize);
    }

    public bool AreInSameCell(
        Vector2 firstWorld,
        Vector2 secondWorld)
    {
        return WorldToCell(firstWorld) ==
               WorldToCell(secondWorld);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return layout != null &&
               layout.FloorCells != null &&
               layout.FloorCells.Contains(cell);
    }

    private Vector2Int GetLowestScoreCell(
        List<Vector2Int> openSet,
        Dictionary<Vector2Int, int> gScore,
        Vector2Int goal)
    {
        Vector2Int best = openSet[0];
        int bestH = Heuristic(best, goal);
        int bestF = GetScore(gScore, best) + bestH;

        for (int i = 1; i < openSet.Count; i++)
        {
            Vector2Int candidate = openSet[i];
            int candidateH = Heuristic(candidate, goal);
            int candidateF =
                GetScore(gScore, candidate) + candidateH;

            if (candidateF < bestF ||
                (candidateF == bestF &&
                 candidateH < bestH))
            {
                best = candidate;
                bestH = candidateH;
                bestF = candidateF;
            }
        }

        return best;
    }

    private static int GetScore(
        Dictionary<Vector2Int, int> scores,
        Vector2Int cell)
    {
        return scores.TryGetValue(cell, out int score)
            ? score
            : int.MaxValue;
    }

    private static int Heuristic(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }

    private static List<Vector2Int> ReconstructCellPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        Vector2Int start)
    {
        List<Vector2Int> path =
            new List<Vector2Int> { current };

        while (cameFrom.TryGetValue(
            current,
            out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();

        // A* 路徑包含起點格。
        // 敵人已經位於起點，因此把它移除。
        if (path.Count > 0 && path[0] == start)
        {
            path.RemoveAt(0);
        }

        return path;
    }

    private List<Vector2> ConvertToWorldPath(
        List<Vector2Int> cellPath)
    {
        List<Vector2> worldPath =
            new List<Vector2>(cellPath.Count);

        for (int i = 0; i < cellPath.Count; i++)
        {
            worldPath.Add(CellToWorld(cellPath[i]));
        }

        return worldPath;
    }

    private Vector2Int FindNearestWalkableCell(
        Vector2Int requestedCell)
    {
        if (layout.FloorCells.Contains(requestedCell))
        {
            return requestedCell;
        }

        Vector2Int nearest = requestedCell;
        int nearestDistance = int.MaxValue;

        foreach (Vector2Int floorCell in layout.FloorCells)
        {
            int distance =
                Heuristic(requestedCell, floorCell);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = floorCell;
            }
        }

        return nearest;
    }
}
