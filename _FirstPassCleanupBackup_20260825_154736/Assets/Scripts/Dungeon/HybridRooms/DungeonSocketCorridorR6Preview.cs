using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R6 Door Socket 与 A* 走廊的独立验证器和 Scene Gizmo。
///
/// 本组件没有 Awake、Start 或 Update；只有主动执行组件菜单时，
/// 才会在内存中生成 R6 数据。它不会实例化房间或调用 Renderer。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonSocketCorridorR6Preview : MonoBehaviour
{
    [Header("R6 诊断目标")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [Min(1)]
    [SerializeField]
    private int previewFloorNumber = 1;

    [SerializeField]
    private int previewFixedSeed = 12345;

    [Tooltip(
        "从 Fixed Seed 往后检查多少个 Seed，" +
        "确认最终 Socket 与走廊结果能够变化。")]
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
    private bool drawTreeCorridors = true;

    [SerializeField]
    private bool drawExtraCorridors = true;

    [SerializeField]
    private bool drawUsedSockets = true;

    [SerializeField]
    private bool drawStartAndExit = true;

    [SerializeField]
    private Color treeCorridorColor =
        new Color(0.2f, 0.9f, 1f, 0.55f);

    [SerializeField]
    private Color extraCorridorColor =
        new Color(1f, 0.58f, 0.12f, 0.6f);

    [SerializeField]
    private Color socketColor =
        new Color(1f, 0.95f, 0.25f, 1f);

    private DungeonLayout lastPreviewLayout;

    [ContextMenu("Validate R6 Door Sockets And AStar Corridors")]
    public void ValidateR6DoorSocketsAndAStarCorridors()
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError(
                "[DungeonSocketCorridorR6Preview] " +
                "Dungeon Generator 不能为空。" +
                "请把本组件放在 GameManager 上，" +
                "或拖入其 DungeonGenerator。",
                this);

            return;
        }

        DungeonLayout primaryLayout;
        string generationReport;

        if (!dungeonGenerator.
                TryGenerateSocketCorridorLayout(
                    previewFloorNumber,
                    previewFixedSeed,
                    out primaryLayout,
                    out generationReport))
        {
            lastPreviewLayout = null;

            Debug.LogError(
                "[DungeonSocketCorridorR6Preview] " +
                "R6 正式校验失败\n" +
                generationReport,
                this);

            return;
        }

        List<string> errors = new List<string>();

        R6AppendErrors(
            "DungeonLayout",
            primaryLayout.GetValidationErrors(),
            errors);

        R6AppendErrors(
            "R6 Contract",
            dungeonGenerator.
                GetSocketCorridorValidationErrors(
                    primaryLayout),
            errors);

        R6PreviewMetrics metrics;

        R6CollectAndValidateMetrics(
            primaryLayout,
            out metrics,
            errors);

        string primarySignature =
            R6BuildLayoutSignature(primaryLayout);

        R6ValidateSameSeed(
            primarySignature,
            errors);

        int changedSeed;

        R6ValidateChangedSeed(
            primarySignature,
            out changedSeed,
            errors);

        lastPreviewLayout = primaryLayout;

        if (errors.Count > 0)
        {
            Debug.LogError(
                R6BuildFailureReport(
                    errors,
                    generationReport),
                this);

            return;
        }

        Debug.Log(
            R6BuildSuccessReport(
                primaryLayout,
                metrics,
                changedSeed,
                generationReport),
            this);
    }

    private void R6CollectAndValidateMetrics(
        DungeonLayout layout,
        out R6PreviewMetrics metrics,
        List<string> errors)
    {
        metrics = new R6PreviewMetrics();

        if (layout == null)
        {
            errors.Add("R6 Layout 为空。");
            return;
        }

        metrics.RoomCount =
            layout.RoomPlacements.Count;

        metrics.ConnectionCount =
            layout.Connections.Count;

        metrics.CorridorCellCount =
            layout.CorridorCells.Count;

        HashSet<string> socketKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int totalConnectionCorridorCells = 0;

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                layout.Connections[connectionIndex];

            if (connection == null)
            {
                continue;
            }

            if (connection.HasAssignedSockets)
            {
                metrics.AssignedConnectionCount++;
            }

            if (connection.HasCorridor)
            {
                metrics.RoutedConnectionCount++;
            }

            totalConnectionCorridorCells +=
                connection.CorridorCells.Count;

            if (connection.RoomAIndex >= 0 &&
                connection.RoomAIndex <
                layout.RoomPlacements.Count)
            {
                socketKeys.Add(
                    connection.RoomAIndex + "|" +
                    connection.SocketAId);
            }

            if (connection.RoomBIndex >= 0 &&
                connection.RoomBIndex <
                layout.RoomPlacements.Count)
            {
                socketKeys.Add(
                    connection.RoomBIndex + "|" +
                    connection.SocketBId);
            }
        }

        metrics.UniqueUsedSocketCount =
            socketKeys.Count;

        metrics.SharedCorridorCellCount =
            Mathf.Max(
                0,
                totalConnectionCorridorCells -
                layout.CorridorCells.Count);

        metrics.ReachableFloorCellCount =
            R6CountReachableCells(
                layout.FloorCells,
                layout.StartCell);

        if (metrics.RoomCount !=
            dungeonGenerator.TemplateFirstDesiredRoomCount)
        {
            errors.Add(
                "RoomPlacements 应为 " +
                dungeonGenerator.TemplateFirstDesiredRoomCount +
                "，实际为 " + metrics.RoomCount + "。");
        }

        if (metrics.AssignedConnectionCount !=
            metrics.ConnectionCount)
        {
            errors.Add(
                "只有 " + metrics.AssignedConnectionCount +
                "/" + metrics.ConnectionCount +
                " 条连接分配了 Socket Pair。");
        }

        if (metrics.RoutedConnectionCount !=
            metrics.ConnectionCount)
        {
            errors.Add(
                "只有 " + metrics.RoutedConnectionCount +
                "/" + metrics.ConnectionCount +
                " 条连接保存了 Corridor Cells。");
        }

        int expectedUsedSockets =
            metrics.ConnectionCount * 2;

        if (metrics.UniqueUsedSocketCount !=
            expectedUsedSockets)
        {
            errors.Add(
                "预期使用 " + expectedUsedSockets +
                " 个互不重复的 Socket，实际唯一键为 " +
                metrics.UniqueUsedSocketCount + "。");
        }

        if (metrics.CorridorCellCount == 0)
        {
            errors.Add("CorridorCells 不能为空。");
        }

        if (metrics.ReachableFloorCellCount !=
            layout.FloorCells.Count)
        {
            errors.Add(
                "从 StartCell 只能到达 " +
                metrics.ReachableFloorCellCount + "/" +
                layout.FloorCells.Count +
                " 个最终 Floor Cell。");
        }
    }

    private void R6ValidateSameSeed(
        string primarySignature,
        List<string> errors)
    {
        DungeonLayout repeatedLayout;
        string repeatedReport;

        if (!dungeonGenerator.
                TryGenerateSocketCorridorLayout(
                    previewFloorNumber,
                    previewFixedSeed,
                    out repeatedLayout,
                    out repeatedReport))
        {
            errors.Add(
                "相同 Seed 的第二次 R6 生成失败。\n" +
                repeatedReport);
            return;
        }

        if (!string.Equals(
                primarySignature,
                R6BuildLayoutSignature(repeatedLayout),
                StringComparison.Ordinal))
        {
            errors.Add(
                "相同 Floor 与 Seed 得到了不同的房间、" +
                "Socket Pair 或 Corridor Cells。");
        }
    }

    private void R6ValidateChangedSeed(
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
                    TryGenerateSocketCorridorLayout(
                        previewFloorNumber,
                        candidateSeed,
                        out candidateLayout,
                        out candidateReport))
            {
                continue;
            }

            if (!string.Equals(
                    primarySignature,
                    R6BuildLayoutSignature(
                        candidateLayout),
                    StringComparison.Ordinal))
            {
                changedSeed = candidateSeed;
                return;
            }
        }

        errors.Add(
            "检查了 " + changedSeedSearchCount +
            " 个其他 Seed，但没有找到不同的 R6 最终结果。");
    }

    private static string R6BuildLayoutSignature(
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

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                layout.Connections[connectionIndex];

            builder.Append(connection.RoomAIndex);
            builder.Append('-');
            builder.Append(connection.RoomBIndex);
            builder.Append('|');
            builder.Append(connection.SocketAId);
            builder.Append('-');
            builder.Append(connection.SocketBId);
            builder.Append('|');

            List<Vector2Int> cells =
                new List<Vector2Int>(
                    connection.CorridorCells);

            cells.Sort(R6CompareCells);

            for (int cellIndex = 0;
                 cellIndex < cells.Count;
                 cellIndex++)
            {
                builder.Append(cells[cellIndex].x);
                builder.Append(',');
                builder.Append(cells[cellIndex].y);
                builder.Append('/');
            }

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

    private string R6BuildSuccessReport(
        DungeonLayout layout,
        R6PreviewMetrics metrics,
        int changedSeed,
        string generationReport)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonSocketCorridorR6Preview] " +
            "R6 Door Socket 与 A* 走廊正式校验通过");

        builder.AppendLine(
            "Rooms：" + metrics.RoomCount +
            " | Connections：" +
            metrics.ConnectionCount +
            " | Socket Pairs：" +
            metrics.AssignedConnectionCount +
            " | 唯一已用 Socket：" +
            metrics.UniqueUsedSocketCount);

        builder.AppendLine(
            "Routed Connections：" +
            metrics.RoutedConnectionCount + "/" +
            metrics.ConnectionCount +
            " | Corridor Width：" +
            dungeonGenerator.SocketCorridorWidth +
            " | CorridorCells：" +
            metrics.CorridorCellCount +
            " | Shared：" +
            metrics.SharedCorridorCellCount);

        builder.AppendLine(
            "门外宽度接触：全部通过 | Socket 不重复：通过 | " +
            "走廊四方向连续：通过");

        builder.AppendLine(
            "穿越房间：0 | 越出地图：0 | Floor 全部可达：" +
            metrics.ReachableFloorCellCount + "/" +
            layout.FloorCells.Count);

        builder.AppendLine(
            "相同 Seed 重现：通过 | 改变 Seed：" +
            changedSeed + " 得到不同最终结果");

        builder.AppendLine(
            "阶段边界：未实例化 Prefab | 未开 DoorBlocker | " +
            "未调用 Renderer | 未修改旧 Generate()");

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static string R6BuildFailureReport(
        List<string> errors,
        string generationReport)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonSocketCorridorR6Preview] R6 正式校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine("- " + errors[i]);
        }

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static void R6AppendErrors(
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

    private static int R6CountReachableCells(
        HashSet<Vector2Int> cells,
        Vector2Int start)
    {
        if (cells == null ||
            !cells.Contains(start))
        {
            return 0;
        }

        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left,
            Vector2Int.down
        };

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int neighbour =
                    current + directions[i];

                if (cells.Contains(neighbour) &&
                    visited.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return visited.Count;
    }

    private static int R6CompareCells(
        Vector2Int first,
        Vector2Int second)
    {
        int xComparison =
            first.x.CompareTo(second.x);

        return xComparison != 0
            ? xComparison
            : first.y.CompareTo(second.y);
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
            R6DrawMapBounds(origin, safeCellSize);
        }

        if (lastPreviewLayout == null)
        {
            return;
        }

        if (drawRoomBounds)
        {
            R6DrawRooms(origin, safeCellSize);
        }

        R6DrawCorridors(origin, safeCellSize);

        if (drawUsedSockets)
        {
            R6DrawUsedSockets(origin, safeCellSize);
        }

        if (drawStartAndExit)
        {
            Gizmos.color =
                new Color(0.2f, 1f, 0.35f, 1f);

            Gizmos.DrawSphere(
                R6CellToWorld(
                    lastPreviewLayout.StartCell,
                    origin,
                    safeCellSize),
                safeCellSize * 0.24f);

            Gizmos.color =
                new Color(1f, 0.25f, 0.75f, 1f);

            Gizmos.DrawSphere(
                R6CellToWorld(
                    lastPreviewLayout.ExitCell,
                    origin,
                    safeCellSize),
                safeCellSize * 0.24f);
        }
    }

    private void R6DrawMapBounds(
        Vector3 origin,
        float safeCellSize)
    {
        Vector3 center =
            origin +
            new Vector3(
                (dungeonGenerator.TemplateFirstMapWidth - 1) *
                0.5f * safeCellSize,
                (dungeonGenerator.TemplateFirstMapHeight - 1) *
                0.5f * safeCellSize,
                0f);

        Vector3 size =
            new Vector3(
                dungeonGenerator.TemplateFirstMapWidth *
                safeCellSize,
                dungeonGenerator.TemplateFirstMapHeight *
                safeCellSize,
                0f);

        Gizmos.color =
            new Color(1f, 0.85f, 0.2f, 1f);

        Gizmos.DrawWireCube(center, size);
    }

    private void R6DrawRooms(
        Vector3 origin,
        float safeCellSize)
    {
        for (int roomIndex = 0;
             roomIndex <
             lastPreviewLayout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                lastPreviewLayout.RoomPlacements[
                    roomIndex];

            RectInt bounds = placement.CellBounds;

            Vector3 center =
                origin +
                new Vector3(
                    (bounds.xMin +
                     (bounds.width - 1) * 0.5f) *
                    safeCellSize,
                    (bounds.yMin +
                     (bounds.height - 1) * 0.5f) *
                    safeCellSize,
                    0f);

            Vector3 size =
                new Vector3(
                    bounds.width * safeCellSize,
                    bounds.height * safeCellSize,
                    0f);

            Gizmos.color =
                R6GetRoomColor(roomIndex);

            Gizmos.DrawWireCube(center, size);
        }
    }

    private void R6DrawCorridors(
        Vector3 origin,
        float safeCellSize)
    {
        int treeConnectionCount =
            Mathf.Max(
                0,
                lastPreviewLayout.RoomPlacements.Count - 1);

        for (int connectionIndex = 0;
             connectionIndex <
             lastPreviewLayout.Connections.Count;
             connectionIndex++)
        {
            bool isTree =
                connectionIndex < treeConnectionCount;

            if ((isTree && !drawTreeCorridors) ||
                (!isTree && !drawExtraCorridors))
            {
                continue;
            }

            DreamRoomConnection connection =
                lastPreviewLayout.Connections[
                    connectionIndex];

            Gizmos.color =
                isTree
                    ? treeCorridorColor
                    : extraCorridorColor;

            for (int cellIndex = 0;
                 cellIndex < connection.CorridorCells.Count;
                 cellIndex++)
            {
                Gizmos.DrawCube(
                    R6CellToWorld(
                        connection.CorridorCells[cellIndex],
                        origin,
                        safeCellSize),
                    new Vector3(
                        safeCellSize * 0.78f,
                        safeCellSize * 0.78f,
                        safeCellSize * 0.04f));
            }
        }
    }

    private void R6DrawUsedSockets(
        Vector3 origin,
        float safeCellSize)
    {
        Gizmos.color = socketColor;

        for (int connectionIndex = 0;
             connectionIndex <
             lastPreviewLayout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                lastPreviewLayout.Connections[
                    connectionIndex];

            if (connection == null ||
                !connection.HasAssignedSockets)
            {
                continue;
            }

            R6DrawSocket(
                connection.RoomAIndex,
                connection.SocketAId,
                origin,
                safeCellSize);

            R6DrawSocket(
                connection.RoomBIndex,
                connection.SocketBId,
                origin,
                safeCellSize);
        }
    }

    private void R6DrawSocket(
        int roomIndex,
        string socketId,
        Vector3 origin,
        float safeCellSize)
    {
        if (roomIndex < 0 ||
            roomIndex >=
            lastPreviewLayout.RoomPlacements.Count)
        {
            return;
        }

        DreamRoomPlacement placement =
            lastPreviewLayout.RoomPlacements[
                roomIndex];

        DreamRoomDoorSocket socket;

        if (placement.Template == null ||
            !placement.Template.TryGetSocket(
                socketId,
                out socket))
        {
            return;
        }

        List<Vector2Int> insideCells =
            new List<Vector2Int>();

        List<Vector2Int> outsideCells =
            new List<Vector2Int>();

        placement.GetSocketInsideCells(
            socket,
            insideCells);

        placement.GetSocketOutsideCells(
            socket,
            outsideCells);

        for (int i = 0; i < insideCells.Count; i++)
        {
            Vector3 insideWorld =
                R6CellToWorld(
                    insideCells[i],
                    origin,
                    safeCellSize);

            Vector3 outsideWorld =
                R6CellToWorld(
                    outsideCells[i],
                    origin,
                    safeCellSize);

            Gizmos.DrawLine(
                insideWorld,
                outsideWorld);

            Gizmos.DrawWireCube(
                outsideWorld,
                new Vector3(
                    safeCellSize * 0.42f,
                    safeCellSize * 0.42f,
                    0f));
        }
    }

    private static Vector3 R6CellToWorld(
        Vector2Int cell,
        Vector3 origin,
        float safeCellSize)
    {
        return origin +
               new Vector3(
                   cell.x * safeCellSize,
                   cell.y * safeCellSize,
                   0f);
    }

    private static Color R6GetRoomColor(int roomIndex)
    {
        Color[] colors =
        {
            new Color(0.35f, 0.8f, 1f, 1f),
            new Color(0.7f, 1f, 0.35f, 1f),
            new Color(0.75f, 0.55f, 1f, 1f),
            new Color(1f, 0.85f, 0.3f, 1f)
        };

        return colors[
            Mathf.Abs(roomIndex) % colors.Length];
    }

    private sealed class R6PreviewMetrics
    {
        public int RoomCount;
        public int ConnectionCount;
        public int AssignedConnectionCount;
        public int RoutedConnectionCount;
        public int UniqueUsedSocketCount;
        public int CorridorCellCount;
        public int SharedCorridorCellCount;
        public int ReachableFloorCellCount;
    }
}
