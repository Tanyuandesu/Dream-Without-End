using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 描述一个已经被选择、旋转并放到全局地牢格子中的房间。
///
/// 坐标约定：
/// 1. Minimum Cell 是旋转后占格矩形的左下格。
/// 2. Quarter Turns 按顺时针 0/90/180/270 度计算。
/// 3. 全局整数格坐标代表格子中心，与 DungeonRenderer.CellToWorld 一致。
/// 4. 本类只保存和换算数据，不实例化 Prefab，也不修改当前地图。
/// </summary>
[Serializable]
public sealed class DreamRoomPlacement
{
    [SerializeField]
    private DreamRoomTemplate template;

    [SerializeField]
    private Vector2Int minimumCell;

    [Range(0, 3)]
    [SerializeField]
    private int clockwiseQuarterTurns;

    // P10.12A-1 R2B：仅存在于本次运行时 RoomPlacement 的程序化几何覆盖。
    // 不写回 DreamRoomTemplate asset，不参与 Prefab 序列化。
    [NonSerialized]
    private HashSet<Vector2Int> runtimeProceduralBlockedLocalCells;

    [NonSerialized]
    private int runtimeProceduralSeed;

    [NonSerialized]
    private DreamProceduralRoomArchetype runtimeProceduralArchetype;

    [NonSerialized]
    private string runtimeProceduralSourceId = string.Empty;

    [NonSerialized]
    private bool runtimeProceduralDebugVisible;

    public DreamRoomTemplate Template => template;
    public Vector2Int MinimumCell => minimumCell;

    public int ClockwiseQuarterTurns =>
        NormalizeQuarterTurns(clockwiseQuarterTurns);

    public float ClockwiseRotationDegrees =>
        ClockwiseQuarterTurns * 90f;

    public bool HasRuntimeProceduralOverride =>
        runtimeProceduralBlockedLocalCells != null &&
        runtimeProceduralBlockedLocalCells.Count > 0;

    public int RuntimeProceduralSeed =>
        runtimeProceduralSeed;

    public DreamProceduralRoomArchetype RuntimeProceduralArchetype =>
        runtimeProceduralArchetype;

    public string RuntimeProceduralSourceId =>
        runtimeProceduralSourceId ?? string.Empty;

    public bool RuntimeProceduralDebugVisible =>
        runtimeProceduralDebugVisible;

    public int RuntimeProceduralBlockedCellCount =>
        runtimeProceduralBlockedLocalCells == null
            ? 0
            : runtimeProceduralBlockedLocalCells.Count;

    /// <summary>
    /// Unity 的正 Z 角度是逆时针，所以顺时针使用负角度。
    /// </summary>
    public Quaternion WorldRotation =>
        Quaternion.Euler(
            0f,
            0f,
            -ClockwiseRotationDegrees);

    public Vector2Int SizeInCells =>
        template == null
            ? Vector2Int.zero
            : template.GetRotatedSize(
                ClockwiseQuarterTurns);

    public RectInt CellBounds
    {
        get
        {
            Vector2Int size = SizeInCells;

            return new RectInt(
                minimumCell.x,
                minimumCell.y,
                size.x,
                size.y);
        }
    }

    public DreamRoomPlacement(
        DreamRoomTemplate roomTemplate,
        Vector2Int roomMinimumCell,
        int roomClockwiseQuarterTurns)
    {
        template = roomTemplate;
        minimumCell = roomMinimumCell;
        clockwiseQuarterTurns =
            NormalizeQuarterTurns(
                roomClockwiseQuarterTurns);
    }

    public static int NormalizeQuarterTurns(int quarterTurns)
    {
        return ((quarterTurns % 4) + 4) % 4;
    }

    /// <summary>
    /// 把未旋转 Prefab 中以左下角为 (0,0) 的格子，
    /// 转换为旋转后占格矩形中的本地格子。
    /// </summary>
    public static Vector2Int RotateCellClockwise(
        Vector2Int originalCell,
        Vector2Int originalSize,
        int quarterTurns)
    {
        switch (NormalizeQuarterTurns(quarterTurns))
        {
            case 0:
                return originalCell;

            case 1:
                return new Vector2Int(
                    originalCell.y,
                    originalSize.x - 1 - originalCell.x);

            case 2:
                return new Vector2Int(
                    originalSize.x - 1 - originalCell.x,
                    originalSize.y - 1 - originalCell.y);

            case 3:
                return new Vector2Int(
                    originalSize.y - 1 - originalCell.y,
                    originalCell.x);

            default:
                return originalCell;
        }
    }

    public static Vector2Int UnrotateCellClockwise(
        Vector2Int rotatedCell,
        Vector2Int originalSize,
        int quarterTurns)
    {
        switch (NormalizeQuarterTurns(quarterTurns))
        {
            case 0:
                return rotatedCell;

            case 1:
                return new Vector2Int(
                    originalSize.x - 1 - rotatedCell.y,
                    rotatedCell.x);

            case 2:
                return new Vector2Int(
                    originalSize.x - 1 - rotatedCell.x,
                    originalSize.y - 1 - rotatedCell.y);

            case 3:
                return new Vector2Int(
                    rotatedCell.y,
                    originalSize.y - 1 - rotatedCell.x);

            default:
                return rotatedCell;
        }
    }

    public Vector2Int OriginalToRotatedCell(
        Vector2Int originalLocalCell)
    {
        if (template == null)
        {
            return originalLocalCell;
        }

        return RotateCellClockwise(
            originalLocalCell,
            template.SizeInCells,
            ClockwiseQuarterTurns);
    }

    public Vector2Int OriginalToGlobalCell(
        Vector2Int originalLocalCell)
    {
        return minimumCell +
               OriginalToRotatedCell(originalLocalCell);
    }

    public Vector2Int GlobalToOriginalCell(
        Vector2Int globalCell)
    {
        Vector2Int rotatedCell =
            globalCell - minimumCell;

        if (template == null)
        {
            return rotatedCell;
        }

        return UnrotateCellClockwise(
            rotatedCell,
            template.SizeInCells,
            ClockwiseQuarterTurns);
    }

    public DreamRoomDoorDirection GetRotatedDirection(
        DreamRoomDoorSocket socket)
    {
        if (socket == null)
        {
            return DreamRoomDoorDirection.North;
        }

        return socket.Direction.RotateClockwise(
            ClockwiseQuarterTurns);
    }

    public void GetSocketInsideCells(
        DreamRoomDoorSocket socket,
        List<Vector2Int> results)
    {
        RequireResults(results);
        results.Clear();

        if (socket == null)
        {
            return;
        }

        List<Vector2Int> originalCells =
            socket.GetLocalInsideCells();

        for (int i = 0; i < originalCells.Count; i++)
        {
            results.Add(
                OriginalToGlobalCell(originalCells[i]));
        }
    }

    /// <summary>
    /// 返回门外紧邻房间边界的全局格子。
    /// 阶段6的 Socket 匹配和走廊寻路会使用这些格子。
    /// </summary>
    public void GetSocketOutsideCells(
        DreamRoomDoorSocket socket,
        List<Vector2Int> results)
    {
        RequireResults(results);
        GetSocketInsideCells(socket, results);

        if (socket == null)
        {
            return;
        }

        Vector2Int outsideOffset =
            GetRotatedDirection(socket).ToCellOffset();

        for (int i = 0; i < results.Count; i++)
        {
            results[i] += outsideOffset;
        }
    }

    public void GetOccupiedGlobalCells(
        List<Vector2Int> results)
    {
        RequireResults(results);

        if (template == null)
        {
            results.Clear();
            return;
        }

        template.GetOccupiedCells(results);
        TransformLocalCellsToGlobal(results);
    }

    public bool TryApplyRuntimeProceduralOverride(
        IEnumerable<Vector2Int> blockedLocalCells,
        int seed,
        DreamProceduralRoomArchetype archetype,
        string sourceId,
        bool drawDebug,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (template == null)
        {
            failureReason =
                "Runtime Procedural Override 需要有效 Room Template。";
            return false;
        }

        if (blockedLocalCells == null)
        {
            failureReason =
                "Runtime Procedural Blocked Cells 不能为空。";
            return false;
        }

        HashSet<Vector2Int> candidate =
            new HashSet<Vector2Int>();

        foreach (Vector2Int cell in blockedLocalCells)
        {
            if (!template.ContainsLocalCell(cell))
            {
                failureReason =
                    "Runtime Procedural Blocked Cell 超出模板：" +
                    cell + "。";
                return false;
            }

            if (!template.IsWalkableCell(cell))
            {
                failureReason =
                    "Runtime Procedural Blocked Cell 必须来自模板原始 Walkable Cells：" +
                    cell + "。";
                return false;
            }

            candidate.Add(cell);
        }

        if (candidate.Count == 0)
        {
            failureReason =
                "Runtime Procedural Override 至少需要一个 Blocked Cell。";
            return false;
        }

        runtimeProceduralBlockedLocalCells = candidate;
        runtimeProceduralSeed = seed;
        runtimeProceduralArchetype = archetype;
        runtimeProceduralSourceId =
            string.IsNullOrWhiteSpace(sourceId)
                ? "ProceduralRuntime"
                : sourceId.Trim();
        runtimeProceduralDebugVisible = drawDebug;
        return true;
    }

    public void ClearRuntimeProceduralOverride()
    {
        if (runtimeProceduralBlockedLocalCells != null)
        {
            runtimeProceduralBlockedLocalCells.Clear();
        }

        runtimeProceduralBlockedLocalCells = null;
        runtimeProceduralSeed = 0;
        runtimeProceduralArchetype =
            DreamProceduralRoomArchetype.EdgeHeavy;
        runtimeProceduralSourceId = string.Empty;
        runtimeProceduralDebugVisible = false;
    }

    public void GetRuntimeProceduralBlockedLocalCells(
        List<Vector2Int> results)
    {
        RequireResults(results);
        results.Clear();

        if (runtimeProceduralBlockedLocalCells == null)
        {
            return;
        }

        results.AddRange(
            runtimeProceduralBlockedLocalCells);

        results.Sort(
            delegate(Vector2Int a, Vector2Int b)
            {
                int x = a.x.CompareTo(b.x);
                return x != 0
                    ? x
                    : a.y.CompareTo(b.y);
            });
    }

    public bool IsRuntimeProceduralBlockedLocalCell(
        Vector2Int localCell)
    {
        return
            runtimeProceduralBlockedLocalCells != null &&
            runtimeProceduralBlockedLocalCells.Contains(localCell);
    }

    public void GetWalkableGlobalCells(
        List<Vector2Int> results)
    {
        RequireResults(results);

        if (template == null)
        {
            results.Clear();
            return;
        }

        template.GetWalkableCells(results);

        if (runtimeProceduralBlockedLocalCells != null &&
            runtimeProceduralBlockedLocalCells.Count > 0)
        {
            for (int i = results.Count - 1;
                 i >= 0;
                 i--)
            {
                if (runtimeProceduralBlockedLocalCells.Contains(
                        results[i]))
                {
                    results.RemoveAt(i);
                }
            }
        }

        TransformLocalCellsToGlobal(results);
    }

    public void GetBlockedGlobalCells(
        List<Vector2Int> results)
    {
        RequireResults(results);

        if (template == null)
        {
            results.Clear();
            return;
        }

        template.GetBlockedCells(results);

        if (runtimeProceduralBlockedLocalCells != null)
        {
            foreach (Vector2Int runtimeBlocked in
                     runtimeProceduralBlockedLocalCells)
            {
                if (!results.Contains(runtimeBlocked))
                {
                    results.Add(runtimeBlocked);
                }
            }
        }

        TransformLocalCellsToGlobal(results);
    }

    public Vector2Int GetSpawnPointGlobalCell(
        DreamRoomSpawnPoint spawnPoint)
    {
        return spawnPoint == null
            ? minimumCell
            : OriginalToGlobalCell(spawnPoint.LocalCell);
    }

    public bool ContainsBoundsCell(Vector2Int globalCell)
    {
        Vector2Int size = SizeInCells;

        return globalCell.x >= minimumCell.x &&
               globalCell.y >= minimumCell.y &&
               globalCell.x < minimumCell.x + size.x &&
               globalCell.y < minimumCell.y + size.y;
    }

    public bool ContainsOccupiedGlobalCell(
        Vector2Int globalCell)
    {
        if (!ContainsBoundsCell(globalCell) ||
            template == null)
        {
            return false;
        }

        return template.IsOccupiedCell(
            GlobalToOriginalCell(globalCell));
    }

    public RectInt GetPaddedBounds(int padding)
    {
        int safePadding = Mathf.Max(0, padding);
        RectInt bounds = CellBounds;

        return new RectInt(
            bounds.xMin - safePadding,
            bounds.yMin - safePadding,
            bounds.width + safePadding * 2,
            bounds.height + safePadding * 2);
    }

    public bool OverlapsBounds(DreamRoomPlacement other)
    {
        if (!CanCompare(other))
        {
            return false;
        }

        return RectanglesOverlap(
            CellBounds,
            other.CellBounds);
    }

    /// <summary>
    /// 把当前房间边界向外扩展 padding，再与另一个房间比较。
    /// 阶段4可直接使用该规则保持房间间距。
    /// </summary>
    public bool OverlapsWithPadding(
        DreamRoomPlacement other,
        int padding)
    {
        if (!CanCompare(other))
        {
            return false;
        }

        return RectanglesOverlap(
            GetPaddedBounds(padding),
            other.CellBounds);
    }

    /// <summary>
    /// 只比较真实 Occupied Cells，为阶段9非矩形房间预留。
    /// </summary>
    public bool OverlapsOccupiedCells(
        DreamRoomPlacement other)
    {
        if (!CanCompare(other))
        {
            return false;
        }

        List<Vector2Int> firstCells =
            new List<Vector2Int>();

        List<Vector2Int> secondCells =
            new List<Vector2Int>();

        GetOccupiedGlobalCells(firstCells);
        other.GetOccupiedGlobalCells(secondCells);

        if (firstCells.Count > secondCells.Count)
        {
            List<Vector2Int> temporary = firstCells;
            firstCells = secondCells;
            secondCells = temporary;
        }

        HashSet<Vector2Int> firstSet =
            new HashSet<Vector2Int>(firstCells);

        for (int i = 0; i < secondCells.Count; i++)
        {
            if (firstSet.Contains(secondCells[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// gridCellZeroWorldPosition 是全局格子 (0,0) 的世界中心。
    /// </summary>
    public Vector3 GetRoomRootWorldPosition(
        Vector3 gridCellZeroWorldPosition,
        float cellSize)
    {
        Vector2Int size = SizeInCells;
        float safeCellSize = Mathf.Max(0.0001f, cellSize);

        float centerCellX =
            minimumCell.x + (size.x - 1) * 0.5f;

        float centerCellY =
            minimumCell.y + (size.y - 1) * 0.5f;

        return gridCellZeroWorldPosition +
               new Vector3(
                   centerCellX * safeCellSize,
                   centerCellY * safeCellSize,
                   0f);
    }

    public Vector3 GetGlobalCellWorldCenter(
        Vector2Int globalCell,
        Vector3 gridCellZeroWorldPosition,
        float cellSize)
    {
        float safeCellSize = Mathf.Max(0.0001f, cellSize);

        return gridCellZeroWorldPosition +
               new Vector3(
                   globalCell.x * safeCellSize,
                   globalCell.y * safeCellSize,
                   0f);
    }

    public void ApplyPose(
        Transform roomInstance,
        Vector3 gridCellZeroWorldPosition,
        float cellSize)
    {
        if (roomInstance == null)
        {
            throw new ArgumentNullException(
                nameof(roomInstance));
        }

        roomInstance.position =
            GetRoomRootWorldPosition(
                gridCellZeroWorldPosition,
                cellSize);

        roomInstance.rotation = WorldRotation;
    }

    public List<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();

        if (template == null)
        {
            errors.Add("Room Template Prefab 不能为空。");
            return errors;
        }

        if (ClockwiseQuarterTurns != 0 &&
            !template.AllowQuarterTurns)
        {
            errors.Add(
                "模板 '" + template.TemplateId +
                "' 未允许 Quarter Turns，不能旋转 " +
                ClockwiseRotationDegrees + " 度。");
        }

        Vector2Int size = SizeInCells;

        if (size.x < 1 || size.y < 1)
        {
            errors.Add("旋转后的房间尺寸必须为正数。");
        }

        if (runtimeProceduralBlockedLocalCells != null)
        {
            foreach (Vector2Int runtimeBlocked in
                     runtimeProceduralBlockedLocalCells)
            {
                if (!template.ContainsLocalCell(runtimeBlocked) ||
                    !template.IsWalkableCell(runtimeBlocked))
                {
                    errors.Add(
                        "Runtime Procedural Blocked Cell 非法：" +
                        runtimeBlocked + "。");
                    break;
                }
            }
        }

        return errors;
    }

    public override string ToString()
    {
        string readableTemplate =
            template == null
                ? "None"
                : template.TemplateId;

        Vector2Int size = SizeInCells;

        return readableTemplate +
               " | Min " + minimumCell +
               " | CW " + ClockwiseRotationDegrees +
               "° | Size " + size.x + "x" + size.y;
    }

    private void TransformLocalCellsToGlobal(
        List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i] = OriginalToGlobalCell(cells[i]);
        }
    }

    private bool CanCompare(DreamRoomPlacement other)
    {
        return other != null &&
               template != null &&
               other.template != null;
    }

    private static bool RectanglesOverlap(
        RectInt first,
        RectInt second)
    {
        return first.xMin < second.xMax &&
               first.xMax > second.xMin &&
               first.yMin < second.yMax &&
               first.yMax > second.yMin;
    }

    private static void RequireResults(
        List<Vector2Int> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }
    }
}
