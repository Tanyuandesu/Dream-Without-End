using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// R1 数据协议诊断器。
///
/// 只读取 Room Prefab 并绘制 Gizmo，不实例化房间、不创建碰撞、
/// 不调用 DungeonGenerator，也不会改变当前迷宫。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamRoomDataPreview : MonoBehaviour
{
    private const int MaximumPreviewDimension = 64;

    [Header("输入房间 Prefab")]
    [Tooltip("拖入带有 DreamRoomTemplate 的 Prefab 资产。")]
    [SerializeField]
    private DreamRoomTemplate roomTemplatePrefab;

    [Header("诊断摆放")]
    [Tooltip("旋转后占格矩形的全局左下格。")]
    [SerializeField]
    private Vector2Int minimumCell = Vector2Int.zero;

    [Tooltip("0=0°，1=顺时针90°，2=180°，3=顺时针270°。")]
    [Range(0, 3)]
    [SerializeField]
    private int clockwiseQuarterTurns;

    [Min(0.01f)]
    [SerializeField]
    private float cellSize = 1f;

    [Header("Scene 预览")]
    [SerializeField]
    private bool drawCellGrid = true;

    [SerializeField]
    private bool drawOccupiedCells = true;

    [SerializeField]
    private bool drawSocketCells = true;

    public DreamRoomTemplate RoomTemplatePrefab =>
        roomTemplatePrefab;

    public Vector2Int MinimumCell => minimumCell;

    public int ClockwiseQuarterTurns =>
        DreamRoomPlacement.NormalizeQuarterTurns(
            clockwiseQuarterTurns);

    /// <summary>
    /// 此诊断对象的位置代表全局格子 (0,0) 的世界中心。
    /// </summary>
    public Vector3 GridCellZeroWorldPosition =>
        transform.position;

    [ContextMenu("Validate R1 Data Contract")]
    public void ValidateR1DataContract()
    {
        GameObject loadedPrefabRoot;
        DreamRoomTemplate template =
            ResolveRoomTemplate(
                forceLoadPrefabContents: true,
                out loadedPrefabRoot);

        try
        {
            if (template == null)
            {
                Debug.LogError(
                    "[DreamRoomDataPreview] Room Template Prefab 不能为空。",
                    this);
                return;
            }

            List<string> errors =
                template.GetValidationErrors();

            StringBuilder report = new StringBuilder();
            report.AppendLine(
                "[DreamRoomDataPreview] R1 数据协议校验");
            report.AppendLine(
                "Template: " + template.TemplateId);

            int turnCount =
                template.AllowQuarterTurns ? 4 : 1;

            for (int turns = 0; turns < turnCount; turns++)
            {
                DreamRoomPlacement placement =
                    new DreamRoomPlacement(
                        template,
                        minimumCell,
                        turns);

                ValidatePlacement(
                    placement,
                    GetAvailableSockets(template),
                    errors);

                Vector2Int size = placement.SizeInCells;
                List<Vector2Int> occupied =
                    new List<Vector2Int>();
                List<Vector2Int> walkable =
                    new List<Vector2Int>();
                List<Vector2Int> blocked =
                    new List<Vector2Int>();

                placement.GetOccupiedGlobalCells(occupied);
                placement.GetWalkableGlobalCells(walkable);
                placement.GetBlockedGlobalCells(blocked);

                report.AppendLine(
                    "- CW " + (turns * 90) +
                    "°：" + size.x + "x" + size.y +
                    " | Occupied " + occupied.Count +
                    " | Walkable " + walkable.Count +
                    " | Blocked " + blocked.Count);
            }

            ValidateConnectionRecord(
                GetAvailableSockets(template),
                errors,
                report);

            if (errors.Count > 0)
            {
                LogErrors(errors);
                return;
            }

            if (!template.AllowQuarterTurns)
            {
                report.AppendLine(
                    "模板未允许 Quarter Turns，因此只验证 0°。");
            }

            report.AppendLine(
                "结果：旋转、占格、门口、出生点与连接记录全部通过。");

            Debug.Log(report.ToString(), this);
        }
        finally
        {
            ReleaseResolvedTemplate(loadedPrefabRoot);
        }
    }

    private void ValidatePlacement(
        DreamRoomPlacement placement,
        IReadOnlyList<DreamRoomDoorSocket> sockets,
        List<string> errors)
    {
        List<string> placementErrors =
            placement.GetValidationErrors();

        for (int i = 0; i < placementErrors.Count; i++)
        {
            errors.Add(
                "CW " + placement.ClockwiseRotationDegrees +
                "°：" + placementErrors[i]);
        }

        if (placement.Template == null)
        {
            return;
        }

        ValidateRectangleRoundTrip(placement, errors);
        ValidateCellSets(placement, errors);
        ValidateSockets(placement, sockets, errors);
        ValidateSpawnPoints(placement, errors);
    }

    private static void ValidateRectangleRoundTrip(
        DreamRoomPlacement placement,
        List<string> errors)
    {
        Vector2Int originalSize =
            placement.Template.SizeInCells;
        Vector2Int rotatedSize =
            placement.SizeInCells;

        HashSet<Vector2Int> rotatedCells =
            new HashSet<Vector2Int>();

        for (int y = 0; y < originalSize.y; y++)
        {
            for (int x = 0; x < originalSize.x; x++)
            {
                Vector2Int original =
                    new Vector2Int(x, y);

                Vector2Int rotated =
                    DreamRoomPlacement.RotateCellClockwise(
                        original,
                        originalSize,
                        placement.ClockwiseQuarterTurns);

                if (rotated.x < 0 ||
                    rotated.y < 0 ||
                    rotated.x >= rotatedSize.x ||
                    rotated.y >= rotatedSize.y)
                {
                    errors.Add(
                        "CW " + placement.ClockwiseRotationDegrees +
                        "°：格子 " + original +
                        " 旋转后超出边界。");
                    continue;
                }

                if (!rotatedCells.Add(rotated))
                {
                    errors.Add(
                        "CW " + placement.ClockwiseRotationDegrees +
                        "°：旋转后出现重复格 " + rotated + "。");
                }

                Vector2Int restored =
                    DreamRoomPlacement.UnrotateCellClockwise(
                        rotated,
                        originalSize,
                        placement.ClockwiseQuarterTurns);

                if (restored != original)
                {
                    errors.Add(
                        "CW " + placement.ClockwiseRotationDegrees +
                        "°：格子往返失败 " + original +
                        " -> " + rotated +
                        " -> " + restored + "。");
                }
            }
        }

        int expectedCount =
            originalSize.x * originalSize.y;

        if (rotatedCells.Count != expectedCount)
        {
            errors.Add(
                "CW " + placement.ClockwiseRotationDegrees +
                "°：矩形唯一格数应为 " + expectedCount +
                "，实际为 " + rotatedCells.Count + "。");
        }
    }

    private static void ValidateCellSets(
        DreamRoomPlacement placement,
        List<string> errors)
    {
        List<Vector2Int> occupied =
            new List<Vector2Int>();
        List<Vector2Int> walkable =
            new List<Vector2Int>();
        List<Vector2Int> blocked =
            new List<Vector2Int>();

        placement.GetOccupiedGlobalCells(occupied);
        placement.GetWalkableGlobalCells(walkable);
        placement.GetBlockedGlobalCells(blocked);

        HashSet<Vector2Int> occupiedSet =
            new HashSet<Vector2Int>(occupied);
        HashSet<Vector2Int> walkableSet =
            new HashSet<Vector2Int>(walkable);
        HashSet<Vector2Int> blockedSet =
            new HashSet<Vector2Int>(blocked);

        if (occupiedSet.Count != occupied.Count)
        {
            errors.Add("旋转后的 Occupied Cells 存在重复格。");
        }

        if (walkableSet.Count != walkable.Count)
        {
            errors.Add("旋转后的 Walkable Cells 存在重复格。");
        }

        if (blockedSet.Count != blocked.Count)
        {
            errors.Add("旋转后的 Blocked Cells 存在重复格。");
        }

        for (int i = 0; i < occupied.Count; i++)
        {
            if (!placement.ContainsBoundsCell(occupied[i]))
            {
                errors.Add(
                    "Occupied Cell " + occupied[i] +
                    " 超出旋转后房间边界。");
            }
        }

        for (int i = 0; i < walkable.Count; i++)
        {
            if (!occupiedSet.Contains(walkable[i]))
            {
                errors.Add(
                    "Walkable Cell " + walkable[i] +
                    " 不属于 Occupied Cells。");
            }

            if (blockedSet.Contains(walkable[i]))
            {
                errors.Add(
                    "格子 " + walkable[i] +
                    " 同时属于 Walkable 与 Blocked。");
            }
        }

        for (int i = 0; i < blocked.Count; i++)
        {
            if (!occupiedSet.Contains(blocked[i]))
            {
                errors.Add(
                    "Blocked Cell " + blocked[i] +
                    " 不属于 Occupied Cells。");
            }
        }
    }

    private static void ValidateSockets(
        DreamRoomPlacement placement,
        IReadOnlyList<DreamRoomDoorSocket> sockets,
        List<string> errors)
    {
        if (sockets == null || sockets.Count == 0)
        {
            errors.Add("Prefab 没有可读取的 Door Socket。");
            return;
        }

        List<Vector2Int> inside =
            new List<Vector2Int>();
        List<Vector2Int> outside =
            new List<Vector2Int>();

        for (int i = 0; i < sockets.Count; i++)
        {
            DreamRoomDoorSocket socket = sockets[i];

            if (socket == null)
            {
                errors.Add("Door Sockets 中存在空引用。");
                continue;
            }

            placement.GetSocketInsideCells(socket, inside);
            placement.GetSocketOutsideCells(socket, outside);

            if (inside.Count != socket.DoorWidthInCells ||
                outside.Count != inside.Count)
            {
                errors.Add(
                    "Socket '" + socket.SocketId +
                    "' 的旋转后占格数量不正确。");
                continue;
            }

            DreamRoomDoorDirection direction =
                placement.GetRotatedDirection(socket);

            Vector2Int offset = direction.ToCellOffset();

            for (int cellIndex = 0;
                 cellIndex < inside.Count;
                 cellIndex++)
            {
                Vector2Int insideCell = inside[cellIndex];
                Vector2Int outsideCell = outside[cellIndex];

                if (!placement.ContainsOccupiedGlobalCell(
                        insideCell))
                {
                    errors.Add(
                        "Socket '" + socket.SocketId +
                        "' 的门内格 " + insideCell +
                        " 不属于旋转后 Occupied Cells。");
                }
                else if (!IsCellOnBoundary(
                             insideCell,
                             placement.CellBounds,
                             direction))
                {
                    errors.Add(
                        "Socket '" + socket.SocketId +
                        "' 的门内格不在旋转后的 " +
                        direction + " 边界。");
                }

                if (outsideCell != insideCell + offset)
                {
                    errors.Add(
                        "Socket '" + socket.SocketId +
                        "' 的门外格没有紧邻门内格。");
                }

                if (placement.ContainsBoundsCell(outsideCell))
                {
                    errors.Add(
                        "Socket '" + socket.SocketId +
                        "' 的门外格仍位于房间边界内。");
                }
            }
        }
    }

    private static void ValidateSpawnPoints(
        DreamRoomPlacement placement,
        List<string> errors)
    {
        IReadOnlyList<DreamRoomSpawnPoint> points =
            placement.Template.SpawnPoints;

        if (points == null)
        {
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            DreamRoomSpawnPoint point = points[i];

            if (point == null)
            {
                continue;
            }

            Vector2Int globalCell =
                placement.GetSpawnPointGlobalCell(point);

            if (!placement.ContainsOccupiedGlobalCell(globalCell) ||
                !placement.Template.IsWalkableCell(point.LocalCell))
            {
                errors.Add(
                    "Spawn Point '" + point.SpawnPointId +
                    "' 旋转后不在可行走格中。");
            }
        }
    }

    private static void ValidateConnectionRecord(
        IReadOnlyList<DreamRoomDoorSocket> sockets,
        List<string> errors,
        StringBuilder report)
    {
        if (sockets == null || sockets.Count == 0 ||
            sockets[0] == null)
        {
            errors.Add("没有 Socket，无法验证 Connection 记录。");
            return;
        }

        string firstSocketId = sockets[0].SocketId;
        string secondSocketId =
            sockets.Count > 1 && sockets[1] != null
                ? sockets[1].SocketId
                : firstSocketId;

        DreamRoomConnection connection =
            new DreamRoomConnection(0, 1);

        connection.AssignSockets(
            firstSocketId,
            secondSocketId);

        connection.SetCorridorCells(
            new[]
            {
                new Vector2Int(10, 10),
                new Vector2Int(11, 10),
                new Vector2Int(11, 10)
            });

        List<string> connectionErrors =
            connection.GetValidationErrors(
                roomCount: 2,
                requireAssignedSockets: true,
                requireCorridor: true);

        for (int i = 0; i < connectionErrors.Count; i++)
        {
            errors.Add(
                "Connection：" + connectionErrors[i]);
        }

        int otherRoomIndex;

        if (!connection.TryGetOtherRoomIndex(
                0,
                out otherRoomIndex) ||
            otherRoomIndex != 1)
        {
            errors.Add("Connection 无法从 Room 0 找到 Room 1。");
        }

        if (connection.CorridorCells.Count != 2)
        {
            errors.Add("Connection 没有去除重复 Corridor Cell。");
        }

        report.AppendLine(
            "- Connection：Room 0 -> Room 1" +
            " | Socket " + connection.SocketAId +
            " / " + connection.SocketBId +
            " | Corridor Cells " +
            connection.CorridorCells.Count);
    }

    private static bool IsCellOnBoundary(
        Vector2Int cell,
        RectInt bounds,
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return cell.y == bounds.yMax - 1;

            case DreamRoomDoorDirection.East:
                return cell.x == bounds.xMax - 1;

            case DreamRoomDoorDirection.South:
                return cell.y == bounds.yMin;

            case DreamRoomDoorDirection.West:
                return cell.x == bounds.xMin;

            default:
                return false;
        }
    }

    private DreamRoomTemplate ResolveRoomTemplate(
        bool forceLoadPrefabContents,
        out GameObject loadedPrefabRoot)
    {
        loadedPrefabRoot = null;

        if (roomTemplatePrefab == null)
        {
            return null;
        }

#if UNITY_EDITOR
        bool isPrefabAsset =
            !Application.isPlaying &&
            PrefabUtility.IsPartOfPrefabAsset(
                roomTemplatePrefab.gameObject);

        if (isPrefabAsset &&
            (forceLoadPrefabContents ||
             !HasUsableSavedSockets(roomTemplatePrefab)))
        {
            string assetPath =
                AssetDatabase.GetAssetPath(
                    roomTemplatePrefab.gameObject);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                loadedPrefabRoot =
                    PrefabUtility.LoadPrefabContents(assetPath);

                DreamRoomTemplate loadedTemplate =
                    loadedPrefabRoot.GetComponent<
                        DreamRoomTemplate>();

                if (loadedTemplate != null)
                {
                    return loadedTemplate;
                }

                PrefabUtility.UnloadPrefabContents(
                    loadedPrefabRoot);
                loadedPrefabRoot = null;
            }
        }
#endif

        return roomTemplatePrefab;
    }

    private static IReadOnlyList<DreamRoomDoorSocket>
        GetAvailableSockets(DreamRoomTemplate template)
    {
        if (template == null)
        {
            return new DreamRoomDoorSocket[0];
        }

        if (HasUsableSavedSockets(template))
        {
            return template.DoorSockets;
        }

        DreamRoomDoorSocket[] foundSockets =
            template.GetComponentsInChildren<
                DreamRoomDoorSocket>(true);

        List<DreamRoomDoorSocket> ownedSockets =
            new List<DreamRoomDoorSocket>();

        for (int i = 0; i < foundSockets.Length; i++)
        {
            DreamRoomDoorSocket socket = foundSockets[i];

            if (socket != null &&
                socket.GetComponentInParent<
                    DreamRoomTemplate>() == template)
            {
                ownedSockets.Add(socket);
            }
        }

        return ownedSockets;
    }

    private static bool HasUsableSavedSockets(
        DreamRoomTemplate template)
    {
        if (template == null ||
            template.DoorSockets == null ||
            template.DoorSockets.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < template.DoorSockets.Count; i++)
        {
            if (template.DoorSockets[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private static void ReleaseResolvedTemplate(
        GameObject loadedPrefabRoot)
    {
#if UNITY_EDITOR
        if (loadedPrefabRoot != null)
        {
            PrefabUtility.UnloadPrefabContents(
                loadedPrefabRoot);
        }
#endif
    }

    private void LogErrors(List<string> errors)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[DreamRoomDataPreview] R1 数据协议校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        Debug.LogError(report.ToString(), this);
    }

    private void OnValidate()
    {
        clockwiseQuarterTurns =
            DreamRoomPlacement.NormalizeQuarterTurns(
                clockwiseQuarterTurns);

        cellSize = Mathf.Max(0.01f, cellSize);
    }

    private void OnDrawGizmosSelected()
    {
        GameObject loadedPrefabRoot;
        DreamRoomTemplate template =
            ResolveRoomTemplate(
                forceLoadPrefabContents: false,
                out loadedPrefabRoot);

        try
        {
            if (template == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
                return;
            }

            DreamRoomPlacement placement =
                new DreamRoomPlacement(
                    template,
                    minimumCell,
                    ClockwiseQuarterTurns);

            DrawBoundsAndGrid(placement);

            if (drawOccupiedCells)
            {
                DrawOccupied(placement);
            }

            if (drawSocketCells)
            {
                DrawSockets(
                    placement,
                    GetAvailableSockets(template));
            }

            Gizmos.color = new Color(1f, 0.65f, 0.15f);
            Gizmos.DrawSphere(
                placement.GetRoomRootWorldPosition(
                    GridCellZeroWorldPosition,
                    cellSize),
                Mathf.Max(0.08f, cellSize * 0.1f));
        }
        finally
        {
            ReleaseResolvedTemplate(loadedPrefabRoot);
        }
    }

    private void DrawBoundsAndGrid(
        DreamRoomPlacement placement)
    {
        Vector2Int size = placement.SizeInCells;

        if (size.x < 1 || size.y < 1)
        {
            return;
        }

        float left = GridCellZeroWorldPosition.x +
            (placement.MinimumCell.x - 0.5f) * cellSize;
        float bottom = GridCellZeroWorldPosition.y +
            (placement.MinimumCell.y - 0.5f) * cellSize;
        float right = left + size.x * cellSize;
        float top = bottom + size.y * cellSize;
        float z = GridCellZeroWorldPosition.z;

        Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(left, bottom, z),
            new Vector3(right, bottom, z));
        Gizmos.DrawLine(
            new Vector3(right, bottom, z),
            new Vector3(right, top, z));
        Gizmos.DrawLine(
            new Vector3(right, top, z),
            new Vector3(left, top, z));
        Gizmos.DrawLine(
            new Vector3(left, top, z),
            new Vector3(left, bottom, z));

        if (!drawCellGrid ||
            size.x > MaximumPreviewDimension ||
            size.y > MaximumPreviewDimension)
        {
            return;
        }

        Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.25f);

        for (int x = 1; x < size.x; x++)
        {
            float worldX = left + x * cellSize;
            Gizmos.DrawLine(
                new Vector3(worldX, bottom, z),
                new Vector3(worldX, top, z));
        }

        for (int y = 1; y < size.y; y++)
        {
            float worldY = bottom + y * cellSize;
            Gizmos.DrawLine(
                new Vector3(left, worldY, z),
                new Vector3(right, worldY, z));
        }
    }

    private void DrawOccupied(DreamRoomPlacement placement)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        placement.GetOccupiedGlobalCells(cells);

        Gizmos.color = new Color(0.45f, 1f, 0.5f, 0.55f);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 center =
                placement.GetGlobalCellWorldCenter(
                    cells[i],
                    GridCellZeroWorldPosition,
                    cellSize);

            Gizmos.DrawWireCube(
                center,
                new Vector3(
                    cellSize * 0.72f,
                    cellSize * 0.72f,
                    0.02f));
        }
    }

    private void DrawSockets(
        DreamRoomPlacement placement,
        IReadOnlyList<DreamRoomDoorSocket> sockets)
    {
        if (sockets == null)
        {
            return;
        }

        List<Vector2Int> inside = new List<Vector2Int>();
        List<Vector2Int> outside = new List<Vector2Int>();

        for (int i = 0; i < sockets.Count; i++)
        {
            DreamRoomDoorSocket socket = sockets[i];

            if (socket == null)
            {
                continue;
            }

            placement.GetSocketInsideCells(socket, inside);
            placement.GetSocketOutsideCells(socket, outside);

            Gizmos.color =
                GetDirectionColor(
                    placement.GetRotatedDirection(socket));

            for (int cellIndex = 0;
                 cellIndex < inside.Count;
                 cellIndex++)
            {
                Vector3 insideCenter =
                    placement.GetGlobalCellWorldCenter(
                        inside[cellIndex],
                        GridCellZeroWorldPosition,
                        cellSize);

                Vector3 outsideCenter =
                    placement.GetGlobalCellWorldCenter(
                        outside[cellIndex],
                        GridCellZeroWorldPosition,
                        cellSize);

                Gizmos.DrawWireCube(
                    insideCenter,
                    new Vector3(
                        cellSize * 0.88f,
                        cellSize * 0.88f,
                        0.04f));
                Gizmos.DrawLine(insideCenter, outsideCenter);
                Gizmos.DrawWireCube(
                    outsideCenter,
                    new Vector3(
                        cellSize * 0.44f,
                        cellSize * 0.44f,
                        0.04f));
            }
        }
    }

    private static Color GetDirectionColor(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Color(0.25f, 0.9f, 1f);
            case DreamRoomDoorDirection.East:
                return new Color(1f, 0.8f, 0.2f);
            case DreamRoomDoorDirection.South:
                return new Color(1f, 0.35f, 0.8f);
            case DreamRoomDoorDirection.West:
                return new Color(0.35f, 1f, 0.45f);
            default:
                return Color.white;
        }
    }
}
