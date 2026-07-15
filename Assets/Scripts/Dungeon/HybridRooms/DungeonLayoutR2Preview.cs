using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R2 的临时布局数据诊断器。
///
/// 它只在内存中构造 Legacy/Hybrid DungeonLayout 并绘制 Gizmo，
/// 不调用 DungeonGenerator、不实例化 Prefab，也不修改正式地图。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonLayoutR2Preview : MonoBehaviour
{
    [Header("两个 R1 房间 Prefab")]
    [SerializeField]
    private DreamRoomTemplate firstRoomTemplate;

    [SerializeField]
    private DreamRoomTemplate secondRoomTemplate;

    [Header("诊断摆放")]
    [SerializeField]
    private Vector2Int firstMinimumCell =
        Vector2Int.zero;

    [SerializeField]
    private Vector2Int secondMinimumCell =
        new Vector2Int(13, 0);

    [Range(0, 3)]
    [SerializeField]
    private int secondClockwiseQuarterTurns = 1;

    [Min(0.01f)]
    [SerializeField]
    private float cellSize = 1f;

    [Header("Scene 预览")]
    [SerializeField]
    private bool drawRoomCells = true;

    [SerializeField]
    private bool drawCorridorCells = true;

    [ContextMenu("Validate R2 Layout Contract")]
    public void ValidateR2LayoutContract()
    {
        List<string> errors = new List<string>();

        DungeonLayout hybridLayout =
            BuildHybridSample(errors);

        if (hybridLayout != null)
        {
            AppendErrors(
                "Hybrid",
                hybridLayout.GetValidationErrors(),
                errors);

            ValidateHybridExpectations(
                hybridLayout,
                errors);
        }

        DungeonLayout legacyLayout =
            BuildLegacySample();

        AppendErrors(
            "Legacy",
            legacyLayout.GetValidationErrors(),
            errors);

        ValidateLegacyExpectations(
            legacyLayout,
            errors);

        if (hybridLayout != null)
        {
            ValidateBrokenUnionIsDetected(
                hybridLayout,
                errors);
        }

        if (errors.Count > 0)
        {
            LogErrors(errors);
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[DungeonLayoutR2Preview] R2 布局协议校验通过");

        report.AppendLine(
            "Legacy：Rooms " + legacyLayout.Rooms.Count +
            " | FloorCells " + legacyLayout.FloorCells.Count +
            " | HasHybridRoomData " +
            legacyLayout.HasHybridRoomData);

        report.AppendLine(
            "Hybrid：RoomPlacements " +
            hybridLayout.RoomPlacements.Count +
            " | RoomCells " +
            hybridLayout.RoomCells.Count +
            " | CorridorCells " +
            hybridLayout.CorridorCells.Count +
            " | FloorCells " +
            hybridLayout.FloorCells.Count +
            " | Connections " +
            hybridLayout.Connections.Count);

        report.AppendLine(
            "FloorCells = RoomCells ∪ CorridorCells：通过");

        report.AppendLine(
            "旧五参数构造函数与错误检测：通过");

        report.AppendLine(
            "本测试只建立内存数据，没有生成或摆放任何 GameObject。");

        Debug.Log(report.ToString(), this);
    }

    private DungeonLayout BuildHybridSample(
        List<string> errors)
    {
        if (firstRoomTemplate == null)
        {
            errors.Add("First Room Template 不能为空。");
        }

        if (secondRoomTemplate == null)
        {
            errors.Add("Second Room Template 不能为空。");
        }

        if (errors.Count > 0)
        {
            return null;
        }

        DreamRoomPlacement firstPlacement =
            new DreamRoomPlacement(
                firstRoomTemplate,
                firstMinimumCell,
                0);

        DreamRoomPlacement secondPlacement =
            new DreamRoomPlacement(
                secondRoomTemplate,
                secondMinimumCell,
                secondClockwiseQuarterTurns);

        if (firstPlacement.OverlapsBounds(
                secondPlacement))
        {
            errors.Add(
                "两个诊断房间发生重叠。请恢复 README 中的默认坐标。");
            return null;
        }

        List<Vector2Int> corridorCells =
            BuildGapCorridor(
                firstPlacement.CellBounds,
                secondPlacement.CellBounds,
                errors);

        if (corridorCells.Count == 0)
        {
            return null;
        }

        DreamRoomConnection connection =
            new DreamRoomConnection(0, 1);

        // R2 只证明 Connection 可以进入 Layout。
        // Socket Id 会在蓝图阶段 6 再分配。
        connection.SetCorridorCells(corridorCells);

        List<DreamRoomPlacement> placements =
            new List<DreamRoomPlacement>
            {
                firstPlacement,
                secondPlacement
            };

        List<DreamRoomConnection> connections =
            new List<DreamRoomConnection>
            {
                connection
            };

        Vector2Int startCell =
            GetTemplateCenterGlobalCell(
                firstPlacement);

        Vector2Int exitCell =
            GetTemplateCenterGlobalCell(
                secondPlacement);

        return DungeonLayout.CreateHybrid(
            placements,
            corridorCells,
            connections,
            startCell,
            exitCell,
            seed: 24680);
    }

    private static DungeonLayout BuildLegacySample()
    {
        List<RectInt> rooms = new List<RectInt>
        {
            new RectInt(0, 0, 2, 2)
        };

        HashSet<Vector2Int> floorCells =
            new HashSet<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1)
            };

        // 这就是当前 DungeonGenerator 仍在调用的旧签名。
        return new DungeonLayout(
            rooms,
            floorCells,
            new Vector2Int(0, 0),
            new Vector2Int(1, 1),
            seed: 13579);
    }

    private static List<Vector2Int> BuildGapCorridor(
        RectInt first,
        RectInt second,
        List<string> errors)
    {
        List<Vector2Int> cells =
            new List<Vector2Int>();

        if (first.xMax <= second.xMin ||
            second.xMax <= first.xMin)
        {
            RectInt left =
                first.xMin < second.xMin
                    ? first
                    : second;

            RectInt right =
                first.xMin < second.xMin
                    ? second
                    : first;

            int overlapMinY =
                Mathf.Max(left.yMin, right.yMin);

            int overlapMaxY =
                Mathf.Min(left.yMax, right.yMax);

            if (overlapMinY < overlapMaxY)
            {
                int y =
                    (overlapMinY + overlapMaxY - 1) / 2;

                for (int x = left.xMax;
                     x < right.xMin;
                     x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }

                return cells;
            }
        }

        if (first.yMax <= second.yMin ||
            second.yMax <= first.yMin)
        {
            RectInt bottom =
                first.yMin < second.yMin
                    ? first
                    : second;

            RectInt top =
                first.yMin < second.yMin
                    ? second
                    : first;

            int overlapMinX =
                Mathf.Max(bottom.xMin, top.xMin);

            int overlapMaxX =
                Mathf.Min(bottom.xMax, top.xMax);

            if (overlapMinX < overlapMaxX)
            {
                int x =
                    (overlapMinX + overlapMaxX - 1) / 2;

                for (int y = bottom.yMax;
                     y < top.yMin;
                     y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }

                return cells;
            }
        }

        errors.Add(
            "诊断器只处理横向或纵向有重叠投影的两个分离房间。" +
            "请恢复默认 Minimum Cell。");

        return cells;
    }

    private static Vector2Int GetTemplateCenterGlobalCell(
        DreamRoomPlacement placement)
    {
        Vector2Int size =
            placement.Template.SizeInCells;

        Vector2Int originalCenterCell =
            new Vector2Int(
                size.x / 2,
                size.y / 2);

        return placement.OriginalToGlobalCell(
            originalCenterCell);
    }

    private static void ValidateHybridExpectations(
        DungeonLayout layout,
        List<string> errors)
    {
        if (!layout.HasHybridRoomData)
        {
            errors.Add("Hybrid 示例没有被识别为混合布局。");
        }

        if (layout.RoomPlacements.Count != 2)
        {
            errors.Add("Hybrid 示例应保存 2 个 RoomPlacements。");
        }

        if (layout.Rooms.Count !=
            layout.RoomPlacements.Count)
        {
            errors.Add("Rooms 没有保留 Placement 的兼容边界。");
        }

        HashSet<Vector2Int> expectedFloorCells =
            new HashSet<Vector2Int>(layout.RoomCells);

        expectedFloorCells.UnionWith(
            layout.CorridorCells);

        if (!layout.FloorCells.SetEquals(
                expectedFloorCells))
        {
            errors.Add("Hybrid 示例的 FloorCells 合集不正确。");
        }

        if (layout.Connections.Count != 1)
        {
            errors.Add("Hybrid 示例应保存 1 条 Connection。");
        }
        else if (layout.Connections[0]
                     .HasAssignedSockets)
        {
            errors.Add(
                "R2 示例不应提前分配 Socket；该工作属于蓝图阶段 6。");
        }
    }

    private static void ValidateLegacyExpectations(
        DungeonLayout layout,
        List<string> errors)
    {
        if (layout.HasHybridRoomData)
        {
            errors.Add("旧五参数构造函数不应产生混合布局数据。");
        }

        if (layout.RoomPlacements.Count != 0 ||
            layout.RoomCells.Count != 0 ||
            layout.CorridorCells.Count != 0 ||
            layout.Connections.Count != 0)
        {
            errors.Add("旧布局的四个混合集合必须安全初始化为空。");
        }

        if (layout.Rooms.Count != 1 ||
            layout.FloorCells.Count != 4)
        {
            errors.Add("旧布局的 Rooms 或 FloorCells 被改变。");
        }
    }

    private static void ValidateBrokenUnionIsDetected(
        DungeonLayout validLayout,
        List<string> errors)
    {
        HashSet<Vector2Int> deliberatelyWrongFloorCells =
            new HashSet<Vector2Int>(validLayout.RoomCells);

        DungeonLayout brokenLayout =
            new DungeonLayout(
                new List<RectInt>(validLayout.Rooms),
                deliberatelyWrongFloorCells,
                validLayout.StartCell,
                validLayout.ExitCell,
                validLayout.Seed,
                new List<DreamRoomPlacement>(
                    validLayout.RoomPlacements),
                new HashSet<Vector2Int>(
                    validLayout.RoomCells),
                new HashSet<Vector2Int>(
                    validLayout.CorridorCells),
                new List<DreamRoomConnection>(
                    validLayout.Connections));

        List<string> detectedErrors =
            brokenLayout.GetValidationErrors();

        if (detectedErrors.Count == 0)
        {
            errors.Add(
                "DungeonLayout 没有检测出故意破坏的 FloorCells 合集。");
        }
    }

    private static void AppendErrors(
        string prefix,
        List<string> source,
        List<string> destination)
    {
        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(
                prefix + "：" + source[i]);
        }
    }

    private void LogErrors(List<string> errors)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[DungeonLayoutR2Preview] R2 布局协议校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        Debug.LogError(report.ToString(), this);
    }

    private void OnValidate()
    {
        secondClockwiseQuarterTurns =
            DreamRoomPlacement.NormalizeQuarterTurns(
                secondClockwiseQuarterTurns);

        cellSize = Mathf.Max(0.01f, cellSize);
    }

    private void OnDrawGizmosSelected()
    {
        List<string> errors = new List<string>();
        DungeonLayout layout = BuildHybridSample(errors);

        if (layout == null || errors.Count > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            return;
        }

        if (drawRoomCells)
        {
            DrawCells(
                layout.RoomCells,
                new Color(0.25f, 0.85f, 1f, 0.65f),
                0.78f);
        }

        if (drawCorridorCells)
        {
            DrawCells(
                layout.CorridorCells,
                new Color(1f, 0.85f, 0.25f, 0.95f),
                0.58f);
        }

        Gizmos.color = new Color(0.3f, 1f, 0.4f);
        Gizmos.DrawSphere(
            CellToWorld(layout.StartCell),
            cellSize * 0.2f);

        Gizmos.color = new Color(1f, 0.35f, 0.8f);
        Gizmos.DrawSphere(
            CellToWorld(layout.ExitCell),
            cellSize * 0.2f);
    }

    private void DrawCells(
        IEnumerable<Vector2Int> cells,
        Color color,
        float sizeMultiplier)
    {
        Gizmos.color = color;

        foreach (Vector2Int cell in cells)
        {
            Gizmos.DrawWireCube(
                CellToWorld(cell),
                new Vector3(
                    cellSize * sizeMultiplier,
                    cellSize * sizeMultiplier,
                    0.04f));
        }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.position +
               new Vector3(
                   cell.x * cellSize,
                   cell.y * cellSize,
                   0f);
    }
}
