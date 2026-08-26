using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// P10.13：房间视觉／内容精度层级。
/// 这是“房间如何被生产与填充”的分类，不取代 RoomTag 的玩法角色。
/// </summary>
public enum DreamRoomFidelityTier
{
    HighPrecision = 0,
    ProceduralMedium = 1
}

/// <summary>
/// 一个可被混合地牢系统选择和摆放的房间模板。
///
/// 坐标约定：
/// 1. 房间根节点位于房间几何中心。
/// 2. 房间左下角格子是本地格子 (0,0)。
/// 3. 房间尺寸使用格子数表示，不限制为固定档位。
/// 4. 一格在灰盒预览中按 1 个 Unity 单位显示。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamRoomTemplate : MonoBehaviour
{
    private const float PreviewCellSize = 1f;
    private const int MaximumPreviewGridDimension = 64;

    [Header("身份与尺寸")]
    [SerializeField]
    private string templateId = "Room_Template";

    [Tooltip("任意正整数尺寸。X 是宽度，Y 是高度。")]
    [SerializeField]
    private Vector2Int sizeInCells = new Vector2Int(8, 6);

    [Header("随机选择规则")]
    [Min(1)]
    [SerializeField]
    private int randomWeight = 10;

    [Min(1)]
    [SerializeField]
    private int minimumFloor = 1;

    [Tooltip("0 表示不限制最高楼层。")]
    [Min(0)]
    [SerializeField]
    private int maximumFloor;

    [Tooltip("0 表示每层不限制使用次数。")]
    [Min(0)]
    [SerializeField]
    private int maximumInstancesPerFloor;

    [Tooltip(
        "允许未来生成器把此房间旋转 90/180/270 度。" +
        "本阶段只记录规则，不会实际旋转房间。")]
    [SerializeField]
    private bool allowQuarterTurns = true;

    [Header("房间用途（R9.4 分阶段参与正式选择）")]
    [SerializeField]
    private DreamRoomTag roomTags = DreamRoomTag.Standard;

    [Header("房间精度层级（P10.13）")]
    [Tooltip(
        "High Precision：正式美术 Prefab，内部视觉不做程序化替换。" +
        "Procedural Medium：由 P10.12 中精度 Family 在运行时接管内部几何与皮肤。" +
        "此字段只描述精度层级，不替代 Standard/Rare/Core/Special 等 Room Tags。")]
    [SerializeField]
    private DreamRoomFidelityTier roomFidelityTier =
        DreamRoomFidelityTier.HighPrecision;

    [Header("格子覆盖（空列表使用完整矩形默认值）")]
    [Tooltip(
        "空列表表示尺寸矩形内全部格子都被房间占用。" +
        "阶段9制作非矩形房间时再显式填写。")]
    [SerializeField]
    private List<Vector2Int> occupiedCells =
        new List<Vector2Int>();

    [Tooltip(
        "空列表表示 Occupied Cells 中除 Blocked Cells 外全部可行走。")]
    [SerializeField]
    private List<Vector2Int> walkableCells =
        new List<Vector2Int>();

    [Tooltip("内部障碍格；必须同时属于 Occupied Cells。")]
    [SerializeField]
    private List<Vector2Int> blockedCells =
        new List<Vector2Int>();

    [Header("房间层级（当前均可留空）")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private Transform socketsRoot;

    [SerializeField]
    private Transform navigationRoot;

    [SerializeField]
    private Transform spawnPointsRoot;

    [Header("门口 Socket")]
    [Tooltip(
        "开启后，OnValidate 会尝试从 Sockets Root（未指定时从房间根节点）" +
        "重新收集属于本模板的门口。")]
    [SerializeField]
    private bool autoCollectDoorSockets = true;

    [SerializeField]
    private List<DreamRoomDoorSocket> doorSockets =
        new List<DreamRoomDoorSocket>();

    [Header("候选出生点（当前可为空）")]
    [Tooltip(
        "开启后，OnValidate 会从 Spawn Points Root 中收集 " +
        "DreamRoomSpawnPoint。")]
    [SerializeField]
    private bool autoCollectSpawnPoints = true;

    [SerializeField]
    private List<DreamRoomSpawnPoint> spawnPoints =
        new List<DreamRoomSpawnPoint>();

    [Header("Scene 预览")]
    [SerializeField]
    private bool drawCellGrid = true;

    [SerializeField]
    private bool drawDoorCells = true;

    [SerializeField]
    private bool drawCellOverrides = true;

    [SerializeField]
    private bool drawSpawnPoints = true;

    public string TemplateId => templateId;
    public Vector2Int SizeInCells => sizeInCells;
    public int RandomWeight => randomWeight;
    public int MinimumFloor => minimumFloor;
    public int MaximumFloor => maximumFloor;
    public int MaximumInstancesPerFloor => maximumInstancesPerFloor;
    public bool AllowQuarterTurns => allowQuarterTurns;
    public DreamRoomTag RoomTags => roomTags;
    public DreamRoomFidelityTier RoomFidelityTier =>
        roomFidelityTier;
    public IReadOnlyList<Vector2Int> OccupiedCellOverrides =>
        occupiedCells;
    public IReadOnlyList<Vector2Int> WalkableCellOverrides =>
        walkableCells;
    public IReadOnlyList<Vector2Int> BlockedCellOverrides =>
        blockedCells;
    public Transform VisualRoot => visualRoot;
    public Transform SocketsRoot => socketsRoot;
    public Transform NavigationRoot => navigationRoot;
    public Transform SpawnPointsRoot => spawnPointsRoot;
    public IReadOnlyList<DreamRoomDoorSocket> DoorSockets => doorSockets;
    public IReadOnlyList<DreamRoomSpawnPoint> SpawnPoints => spawnPoints;

    public bool UsesRectangularOccupiedCells =>
        occupiedCells == null || occupiedCells.Count == 0;

    public bool UsesDefaultWalkableCells =>
        walkableCells == null || walkableCells.Count == 0;

    public bool HasTag(DreamRoomTag requestedTags)
    {
        return roomTags.HasAll(requestedTags);
    }

    /// <summary>
    /// 判断模板是否允许出现在指定楼层。
    /// MaximumFloor 为 0 时没有最高楼层限制。
    /// </summary>
    public bool CanAppearOnFloor(int floorNumber)
    {
        if (floorNumber < minimumFloor)
        {
            return false;
        }

        return maximumFloor == 0 ||
               floorNumber <= maximumFloor;
    }

    /// <summary>
    /// 返回旋转后的占格尺寸。
    /// 奇数次 90 度旋转会交换宽高。
    /// </summary>
    public Vector2Int GetRotatedSize(int quarterTurns)
    {
        int normalizedTurns =
            ((quarterTurns % 4) + 4) % 4;

        if (normalizedTurns % 2 == 0)
        {
            return sizeInCells;
        }

        return new Vector2Int(
            sizeInCells.y,
            sizeInCells.x);
    }

    /// <summary>
    /// 把左下角起算的本地格子坐标换算为房间根节点下的本地位置。
    /// 房间根节点始终位于几何中心。
    /// </summary>
    public Vector3 GetLocalCellCenter(Vector2Int localCell)
    {
        float centerOffsetX =
            (sizeInCells.x - 1) * 0.5f;

        float centerOffsetY =
            (sizeInCells.y - 1) * 0.5f;

        return new Vector3(
            (localCell.x - centerOffsetX) * PreviewCellSize,
            (localCell.y - centerOffsetY) * PreviewCellSize,
            0f);
    }

    public Vector3 GetWorldCellCenter(Vector2Int localCell)
    {
        return transform.TransformPoint(
            GetLocalCellCenter(localCell));
    }

    public bool ContainsLocalCell(Vector2Int localCell)
    {
        return localCell.x >= 0 &&
               localCell.y >= 0 &&
               localCell.x < sizeInCells.x &&
               localCell.y < sizeInCells.y;
    }

    /// <summary>
    /// 空 Occupied Cells 表示完整尺寸矩形。
    /// results 会先清空，方便生成器复用列表。
    /// </summary>
    public void GetOccupiedCells(List<Vector2Int> results)
    {
        RequireCellResults(results);
        results.Clear();

        if (UsesRectangularOccupiedCells)
        {
            for (int y = 0; y < sizeInCells.y; y++)
            {
                for (int x = 0; x < sizeInCells.x; x++)
                {
                    results.Add(new Vector2Int(x, y));
                }
            }

            return;
        }

        AddUniqueCells(occupiedCells, results);
    }

    public void GetBlockedCells(List<Vector2Int> results)
    {
        RequireCellResults(results);
        results.Clear();
        AddUniqueCells(blockedCells, results);
    }

    /// <summary>
    /// 空 Walkable Cells 表示全部 Occupied Cells 减去 Blocked Cells。
    /// </summary>
    public void GetWalkableCells(List<Vector2Int> results)
    {
        RequireCellResults(results);
        results.Clear();

        if (!UsesDefaultWalkableCells)
        {
            AddUniqueCells(walkableCells, results);

            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (!IsOccupiedCell(results[i]) ||
                    IsBlockedCell(results[i]))
                {
                    results.RemoveAt(i);
                }
            }

            return;
        }

        GetOccupiedCells(results);

        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (IsBlockedCell(results[i]))
            {
                results.RemoveAt(i);
            }
        }
    }

    public bool IsOccupiedCell(Vector2Int localCell)
    {
        if (!ContainsLocalCell(localCell))
        {
            return false;
        }

        return UsesRectangularOccupiedCells ||
               occupiedCells.Contains(localCell);
    }

    public bool IsBlockedCell(Vector2Int localCell)
    {
        return blockedCells != null &&
               blockedCells.Contains(localCell);
    }

    public bool IsWalkableCell(Vector2Int localCell)
    {
        if (!IsOccupiedCell(localCell) ||
            IsBlockedCell(localCell))
        {
            return false;
        }

        return UsesDefaultWalkableCells ||
               walkableCells.Contains(localCell);
    }

    /// <summary>
    /// 计算门口全部占格的几何中心。
    /// 偶数宽门口也会得到位于两格之间的正确中心点。
    /// </summary>
    public Vector3 GetLocalDoorCenter(
        DreamRoomDoorSocket socket)
    {
        if (socket == null)
        {
            return Vector3.zero;
        }

        List<Vector2Int> cells =
            socket.GetLocalInsideCells();

        if (cells.Count == 0)
        {
            return GetLocalCellCenter(
                socket.LocalInsideCell);
        }

        float totalX = 0f;
        float totalY = 0f;

        for (int i = 0; i < cells.Count; i++)
        {
            totalX += cells[i].x;
            totalY += cells[i].y;
        }

        float averageX = totalX / cells.Count;
        float averageY = totalY / cells.Count;

        float centerOffsetX =
            (sizeInCells.x - 1) * 0.5f;

        float centerOffsetY =
            (sizeInCells.y - 1) * 0.5f;

        return new Vector3(
            (averageX - centerOffsetX) * PreviewCellSize,
            (averageY - centerOffsetY) * PreviewCellSize,
            0f);
    }

    public bool TryGetSocket(
        string socketId,
        out DreamRoomDoorSocket socket)
    {
        socket = null;

        if (string.IsNullOrWhiteSpace(socketId))
        {
            return false;
        }

        for (int i = 0; i < doorSockets.Count; i++)
        {
            DreamRoomDoorSocket candidate =
                doorSockets[i];

            if (candidate != null &&
                string.Equals(
                    candidate.SocketId,
                    socketId,
                    StringComparison.OrdinalIgnoreCase))
            {
                socket = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetSpawnPoint(
        string spawnPointId,
        out DreamRoomSpawnPoint spawnPoint)
    {
        spawnPoint = null;

        if (string.IsNullOrWhiteSpace(spawnPointId) ||
            spawnPoints == null)
        {
            return false;
        }

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            DreamRoomSpawnPoint candidate =
                spawnPoints[i];

            if (candidate != null &&
                string.Equals(
                    candidate.SpawnPointId,
                    spawnPointId,
                    StringComparison.OrdinalIgnoreCase))
            {
                spawnPoint = candidate;
                return true;
            }
        }

        return false;
    }

    public void SetAllSocketsOpen(bool open)
    {
        for (int i = 0; i < doorSockets.Count; i++)
        {
            if (doorSockets[i] != null)
            {
                doorSockets[i].SetOpen(open);
            }
        }
    }

    /// <summary>
    /// 从指定的 Sockets Root 收集门口。
    /// 如果没有指定，则从整个房间根节点收集。
    /// 嵌套在其他 DreamRoomTemplate 下的 Socket 会被排除。
    /// </summary>
    [ContextMenu("Refresh Door Sockets")]
    public void RefreshDoorSockets()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(
                this,
                "Refresh Dream Room Door Sockets");
        }
#endif

        CollectDoorSockets(
            preserveExistingWhenNothingFound: true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);

            if (PrefabUtility.IsPartOfPrefabInstance(
                    gameObject))
            {
                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        this);
            }
        }
#endif
    }

    /// <summary>
    /// Prefab Asset 的简略 Inspector 并不总会把子层级加载成可遍历对象。
    /// 因此只有在确实找到属于本模板的 Socket 时才替换已保存列表，
    /// 避免 OnValidate 在资产上下文中把正确列表误清空。
    /// </summary>
    private void CollectDoorSockets(
        bool preserveExistingWhenNothingFound)
    {
        if (doorSockets == null)
        {
            doorSockets =
                new List<DreamRoomDoorSocket>();
        }

        Transform searchRoot =
            socketsRoot != null
                ? socketsRoot
                : transform;

        DreamRoomDoorSocket[] foundSockets =
            searchRoot.GetComponentsInChildren<
                DreamRoomDoorSocket>(true);

        List<DreamRoomDoorSocket> collectedSockets =
            new List<DreamRoomDoorSocket>();

        for (int i = 0; i < foundSockets.Length; i++)
        {
            DreamRoomDoorSocket foundSocket =
                foundSockets[i];

            DreamRoomTemplate owner =
                foundSocket.GetComponentInParent<
                    DreamRoomTemplate>();

            if (owner == this)
            {
                collectedSockets.Add(foundSocket);
            }
        }

        if (collectedSockets.Count == 0 &&
            preserveExistingWhenNothingFound &&
            doorSockets.Count > 0)
        {
            return;
        }

        doorSockets.Clear();
        doorSockets.AddRange(collectedSockets);
    }

    [ContextMenu("Refresh Spawn Points")]
    public void RefreshSpawnPoints()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(
                this,
                "Refresh Dream Room Spawn Points");
        }
#endif

        CollectSpawnPoints(
            preserveExistingWhenNothingFound: true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);

            if (PrefabUtility.IsPartOfPrefabInstance(
                    gameObject))
            {
                PrefabUtility
                    .RecordPrefabInstancePropertyModifications(
                        this);
            }
        }
#endif
    }

    /// <summary>
    /// 与 Socket 收集使用相同的 Prefab Asset 保护：
    /// 简略 Inspector 没有加载子层级时，不清空已经保存的引用。
    /// </summary>
    private void CollectSpawnPoints(
        bool preserveExistingWhenNothingFound)
    {
        if (spawnPoints == null)
        {
            spawnPoints =
                new List<DreamRoomSpawnPoint>();
        }

        Transform searchRoot =
            spawnPointsRoot != null
                ? spawnPointsRoot
                : transform;

        DreamRoomSpawnPoint[] foundPoints =
            searchRoot.GetComponentsInChildren<
                DreamRoomSpawnPoint>(true);

        List<DreamRoomSpawnPoint> collectedPoints =
            new List<DreamRoomSpawnPoint>();

        for (int i = 0; i < foundPoints.Length; i++)
        {
            DreamRoomSpawnPoint foundPoint =
                foundPoints[i];

            DreamRoomTemplate owner =
                foundPoint.GetComponentInParent<
                    DreamRoomTemplate>();

            if (owner == this)
            {
                collectedPoints.Add(foundPoint);
            }
        }

        if (collectedPoints.Count == 0 &&
            preserveExistingWhenNothingFound &&
            spawnPoints.Count > 0)
        {
            return;
        }

        spawnPoints.Clear();
        spawnPoints.AddRange(collectedPoints);
    }

    /// <summary>
    /// 返回会阻止模板进入正式生成流程的配置错误。
    /// </summary>
    public List<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();

        if (string.IsNullOrWhiteSpace(templateId))
        {
            errors.Add("Template Id 不能为空。");
        }

        if (sizeInCells.x < 1 || sizeInCells.y < 1)
        {
            errors.Add("Size In Cells 的 X 与 Y 必须都大于 0。");
        }

        if (randomWeight < 1)
        {
            errors.Add("Random Weight 必须至少为 1。");
        }

        if (minimumFloor < 1)
        {
            errors.Add("Minimum Floor 必须至少为 1。");
        }

        if (maximumFloor > 0 &&
            maximumFloor < minimumFloor)
        {
            errors.Add(
                "Maximum Floor 为非 0 值时，不能小于 Minimum Floor。");
        }

        ValidateRootReference(
            "Visual Root",
            visualRoot,
            errors);

        ValidateRootReference(
            "Sockets Root",
            socketsRoot,
            errors);

        ValidateRootReference(
            "Navigation Root",
            navigationRoot,
            errors);

        ValidateRootReference(
            "Spawn Points Root",
            spawnPointsRoot,
            errors);

        ValidateCellOverrides(errors);
        ValidateSpawnPoints(errors);

        if (doorSockets == null ||
            doorSockets.Count == 0)
        {
            errors.Add(
                "模板至少需要一个 DreamRoomDoorSocket。");

            return errors;
        }

        HashSet<string> usedIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < doorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket =
                doorSockets[i];

            if (socket == null)
            {
                errors.Add(
                    "Door Sockets 列表中存在空引用，" +
                    "请执行 Refresh Door Sockets。");

                continue;
            }

            string readableId =
                string.IsNullOrWhiteSpace(socket.SocketId)
                    ? socket.gameObject.name
                    : socket.SocketId;

            DreamRoomTemplate owner =
                socket.GetComponentInParent<
                    DreamRoomTemplate>();

            if (owner != this)
            {
                errors.Add(
                    "Socket '" + readableId +
                    "' 不属于当前 DreamRoomTemplate。");

                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    socket.SocketId))
            {
                errors.Add(
                    "Socket '" + socket.gameObject.name +
                    "' 的 Socket Id 不能为空。");
            }
            else if (!usedIds.Add(socket.SocketId))
            {
                errors.Add(
                    "Socket Id '" + socket.SocketId +
                    "' 重复。每个房间内必须唯一。");
            }

            ValidateSocketCells(
                socket,
                readableId,
                errors);
        }

        return errors;
    }

    /// <summary>
    /// 在 Console 中输出一次清晰的人工验收结果。
    /// </summary>
    [ContextMenu("Validate Room Template")]
    public void ValidateAndLog()
    {
        NormalizeSerializedValues();

        if (autoCollectDoorSockets)
        {
            RefreshDoorSockets();
        }

        if (autoCollectSpawnPoints)
        {
            RefreshSpawnPoints();
        }

        List<string> errors =
            GetValidationErrors();

        if (errors.Count == 0)
        {
            Debug.Log(
                "[DreamRoomTemplate] 校验通过：" +
                templateId +
                " | 尺寸 " +
                sizeInCells.x +
                "x" +
                sizeInCells.y +
                " | 门口 " +
                doorSockets.Count +
                " | 出生点 " +
                spawnPoints.Count,
                this);

            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomTemplate] 校验失败：" +
            templateId);

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        Debug.LogError(report.ToString(), this);
    }

    private void Reset()
    {
        templateId = gameObject.name;
        NormalizeSerializedValues();
        CollectDoorSockets(
            preserveExistingWhenNothingFound: false);
        CollectSpawnPoints(
            preserveExistingWhenNothingFound: false);
    }

    private void OnValidate()
    {
        NormalizeSerializedValues();

        if (autoCollectDoorSockets)
        {
            CollectDoorSockets(
                preserveExistingWhenNothingFound: true);
        }

        if (autoCollectSpawnPoints)
        {
            CollectSpawnPoints(
                preserveExistingWhenNothingFound: true);
        }
    }

    private void NormalizeSerializedValues()
    {
        sizeInCells = new Vector2Int(
            Mathf.Max(1, sizeInCells.x),
            Mathf.Max(1, sizeInCells.y));

        randomWeight = Mathf.Max(1, randomWeight);
        minimumFloor = Mathf.Max(1, minimumFloor);
        maximumFloor = Mathf.Max(0, maximumFloor);
        maximumInstancesPerFloor =
            Mathf.Max(0, maximumInstancesPerFloor);

        if (string.IsNullOrWhiteSpace(templateId))
        {
            templateId = gameObject.name;
        }

        if (doorSockets == null)
        {
            doorSockets =
                new List<DreamRoomDoorSocket>();
        }

        if (occupiedCells == null)
        {
            occupiedCells = new List<Vector2Int>();
        }

        if (walkableCells == null)
        {
            walkableCells = new List<Vector2Int>();
        }

        if (blockedCells == null)
        {
            blockedCells = new List<Vector2Int>();
        }

        if (spawnPoints == null)
        {
            spawnPoints =
                new List<DreamRoomSpawnPoint>();
        }
    }

    private void ValidateRootReference(
        string fieldName,
        Transform referencedRoot,
        List<string> errors)
    {
        if (referencedRoot == null ||
            referencedRoot == transform)
        {
            return;
        }

        if (!referencedRoot.IsChildOf(transform))
        {
            errors.Add(
                fieldName +
                " 必须是当前房间根节点或其子节点。");
        }
    }

    private void ValidateCellOverrides(List<string> errors)
    {
        ValidateCellList(
            "Occupied Cells",
            occupiedCells,
            errors);

        ValidateCellList(
            "Walkable Cells",
            walkableCells,
            errors);

        ValidateCellList(
            "Blocked Cells",
            blockedCells,
            errors);

        if (blockedCells != null)
        {
            for (int i = 0; i < blockedCells.Count; i++)
            {
                Vector2Int cell = blockedCells[i];

                if (ContainsLocalCell(cell) &&
                    !IsOccupiedCell(cell))
                {
                    errors.Add(
                        "Blocked Cells 的格子 " +
                        FormatCell(cell) +
                        " 不属于 Occupied Cells。");
                }
            }
        }

        if (walkableCells != null &&
            walkableCells.Count > 0)
        {
            for (int i = 0; i < walkableCells.Count; i++)
            {
                Vector2Int cell = walkableCells[i];

                if (!ContainsLocalCell(cell))
                {
                    continue;
                }

                if (!IsOccupiedCell(cell))
                {
                    errors.Add(
                        "Walkable Cells 的格子 " +
                        FormatCell(cell) +
                        " 不属于 Occupied Cells。");
                }
                else if (IsBlockedCell(cell))
                {
                    errors.Add(
                        "格子 " + FormatCell(cell) +
                        " 不能同时属于 Walkable Cells 和 Blocked Cells。");
                }
            }
        }
    }

    private void ValidateCellList(
        string listName,
        List<Vector2Int> cells,
        List<string> errors)
    {
        if (cells == null)
        {
            return;
        }

        HashSet<Vector2Int> usedCells =
            new HashSet<Vector2Int>();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];

            if (!ContainsLocalCell(cell))
            {
                errors.Add(
                    listName + " 的格子 " +
                    FormatCell(cell) +
                    " 超出房间尺寸。");
            }

            if (!usedCells.Add(cell))
            {
                errors.Add(
                    listName + " 中的格子 " +
                    FormatCell(cell) +
                    " 重复。");
            }
        }
    }

    private void ValidateSpawnPoints(List<string> errors)
    {
        if (spawnPoints == null)
        {
            return;
        }

        HashSet<string> usedIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            DreamRoomSpawnPoint point = spawnPoints[i];

            if (point == null)
            {
                errors.Add(
                    "Spawn Points 列表中存在空引用，请执行 Refresh Spawn Points。");
                continue;
            }

            DreamRoomTemplate owner =
                point.GetComponentInParent<
                    DreamRoomTemplate>();

            if (owner != this)
            {
                errors.Add(
                    "Spawn Point '" + point.name +
                    "' 不属于当前 DreamRoomTemplate。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    point.SpawnPointId))
            {
                errors.Add(
                    "Spawn Point '" + point.name +
                    "' 的 Spawn Point Id 不能为空。");
            }
            else if (!usedIds.Add(point.SpawnPointId))
            {
                errors.Add(
                    "Spawn Point Id '" +
                    point.SpawnPointId +
                    "' 重复。每个房间内必须唯一。");
            }

            if (!IsWalkableCell(point.LocalCell))
            {
                errors.Add(
                    "Spawn Point '" + point.name +
                    "' 的 Local Cell " +
                    FormatCell(point.LocalCell) +
                    " 不是房间内的可行走格。");
            }

            if (point.RandomWeight < 1)
            {
                errors.Add(
                    "Spawn Point '" + point.name +
                    "' 的 Random Weight 必须至少为 1。");
            }
        }
    }

    private void ValidateSocketCells(
        DreamRoomDoorSocket socket,
        string readableId,
        List<string> errors)
    {
        if (!ContainsLocalCell(
                socket.LocalInsideCell))
        {
            errors.Add(
                "Socket '" + readableId +
                "' 的 Local Inside Cell " +
                FormatCell(socket.LocalInsideCell) +
                " 超出房间尺寸。");
        }

        List<Vector2Int> occupiedCells =
            socket.GetLocalInsideCells();

        bool foundOutsideCell = false;
        Vector2Int firstOutsideCell = Vector2Int.zero;
        bool foundWrongBoundaryCell = false;
        Vector2Int firstWrongBoundaryCell =
            Vector2Int.zero;
        bool foundNonWalkableCell = false;
        Vector2Int firstNonWalkableCell =
            Vector2Int.zero;

        for (int i = 0; i < occupiedCells.Count; i++)
        {
            Vector2Int occupiedCell =
                occupiedCells[i];

            if (!ContainsLocalCell(occupiedCell))
            {
                if (!foundOutsideCell)
                {
                    foundOutsideCell = true;
                    firstOutsideCell = occupiedCell;
                }

                continue;
            }

            if (!IsCellOnExpectedBoundary(
                    occupiedCell,
                    socket.Direction) &&
                !foundWrongBoundaryCell)
            {
                foundWrongBoundaryCell = true;
                firstWrongBoundaryCell = occupiedCell;
            }

            if (!IsWalkableCell(occupiedCell) &&
                !foundNonWalkableCell)
            {
                foundNonWalkableCell = true;
                firstNonWalkableCell = occupiedCell;
            }
        }

        if (foundOutsideCell)
        {
            errors.Add(
                "Socket '" + readableId +
                "' 的门宽延伸到了房间外：" +
                FormatCell(firstOutsideCell) + ".");
        }

        if (foundWrongBoundaryCell)
        {
            errors.Add(
                "Socket '" + readableId +
                "' 的格子 " +
                FormatCell(firstWrongBoundaryCell) +
                " 不在 " +
                socket.Direction +
                " 边界上。");
        }

        if (foundNonWalkableCell)
        {
            errors.Add(
                "Socket '" + readableId +
                "' 的门内格 " +
                FormatCell(firstNonWalkableCell) +
                " 必须属于 Walkable Cells。");
        }
    }

    private bool IsCellOnExpectedBoundary(
        Vector2Int cell,
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return cell.y == sizeInCells.y - 1;

            case DreamRoomDoorDirection.East:
                return cell.x == sizeInCells.x - 1;

            case DreamRoomDoorDirection.South:
                return cell.y == 0;

            case DreamRoomDoorDirection.West:
                return cell.x == 0;

            default:
                return false;
        }
    }

    private static string FormatCell(Vector2Int cell)
    {
        return "(" + cell.x + "," + cell.y + ")";
    }

    private static void RequireCellResults(
        List<Vector2Int> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }
    }

    private static void AddUniqueCells(
        List<Vector2Int> source,
        List<Vector2Int> destination)
    {
        if (source == null)
        {
            return;
        }

        HashSet<Vector2Int> usedCells =
            new HashSet<Vector2Int>(destination);

        for (int i = 0; i < source.Count; i++)
        {
            if (usedCells.Add(source[i]))
            {
                destination.Add(source[i]);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        int width = Mathf.Max(1, sizeInCells.x);
        int height = Mathf.Max(1, sizeInCells.y);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;

        DrawRoomBounds(width, height);

        if (drawCellGrid &&
            width <= MaximumPreviewGridDimension &&
            height <= MaximumPreviewGridDimension)
        {
            DrawRoomGrid(width, height);
        }

        DrawLocalOriginCell();

        if (drawCellOverrides)
        {
            DrawCellOverrideGizmos();
        }

        if (drawDoorCells)
        {
            DrawCollectedDoorSockets();
        }

        if (drawSpawnPoints)
        {
            DrawCollectedSpawnPoints();
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawRoomBounds(
        int width,
        int height)
    {
        Gizmos.color =
            new Color(0.25f, 0.75f, 1f, 1f);

        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(
                width * PreviewCellSize,
                height * PreviewCellSize,
                0.02f));
    }

    private static void DrawRoomGrid(
        int width,
        int height)
    {
        float left =
            -width * PreviewCellSize * 0.5f;

        float right =
            width * PreviewCellSize * 0.5f;

        float bottom =
            -height * PreviewCellSize * 0.5f;

        float top =
            height * PreviewCellSize * 0.5f;

        Gizmos.color =
            new Color(0.25f, 0.75f, 1f, 0.28f);

        for (int x = 1; x < width; x++)
        {
            float xPosition =
                left + x * PreviewCellSize;

            Gizmos.DrawLine(
                new Vector3(xPosition, bottom, 0f),
                new Vector3(xPosition, top, 0f));
        }

        for (int y = 1; y < height; y++)
        {
            float yPosition =
                bottom + y * PreviewCellSize;

            Gizmos.DrawLine(
                new Vector3(left, yPosition, 0f),
                new Vector3(right, yPosition, 0f));
        }
    }

    private void DrawLocalOriginCell()
    {
        Vector3 originCellCenter =
            GetLocalCellCenter(Vector2Int.zero);

        Gizmos.color =
            new Color(1f, 0.85f, 0.2f, 1f);

        Gizmos.DrawWireCube(
            originCellCenter,
            new Vector3(0.82f, 0.82f, 0.04f));
    }

    private void DrawCollectedDoorSockets()
    {
        if (doorSockets == null)
        {
            return;
        }

        for (int i = 0; i < doorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket =
                doorSockets[i];

            if (socket == null)
            {
                continue;
            }

            Color directionColor =
                GetDirectionColor(socket.Direction);

            Gizmos.color = directionColor;

            List<Vector2Int> occupiedCells =
                socket.GetLocalInsideCells();

            for (int cellIndex = 0;
                 cellIndex < occupiedCells.Count;
                 cellIndex++)
            {
                Gizmos.DrawWireCube(
                    GetLocalCellCenter(
                        occupiedCells[cellIndex]),
                    new Vector3(0.72f, 0.72f, 0.05f));
            }

            Vector3 localDoorCenter =
                GetLocalDoorCenter(socket);

            Gizmos.DrawSphere(
                localDoorCenter,
                0.13f);

            Vector2Int cellDirection =
                socket.Direction.ToCellOffset();

            Vector3 localDirection =
                new Vector3(
                    cellDirection.x,
                    cellDirection.y,
                    0f);

            Gizmos.DrawLine(
                localDoorCenter,
                localDoorCenter +
                localDirection * 0.8f);
        }
    }

    private void DrawCellOverrideGizmos()
    {
        if (occupiedCells != null &&
            occupiedCells.Count > 0)
        {
            DrawCellList(
                occupiedCells,
                new Color(0.78f, 0.78f, 0.78f, 0.65f),
                0.9f);
        }

        if (walkableCells != null &&
            walkableCells.Count > 0)
        {
            DrawCellList(
                walkableCells,
                new Color(0.35f, 1f, 0.45f, 0.8f),
                0.62f);
        }

        if (blockedCells != null &&
            blockedCells.Count > 0)
        {
            DrawCellList(
                blockedCells,
                new Color(1f, 0.25f, 0.25f, 0.95f),
                0.48f);
        }
    }

    private void DrawCellList(
        List<Vector2Int> cells,
        Color color,
        float size)
    {
        Gizmos.color = color;

        for (int i = 0; i < cells.Count; i++)
        {
            if (!ContainsLocalCell(cells[i]))
            {
                continue;
            }

            Gizmos.DrawWireCube(
                GetLocalCellCenter(cells[i]),
                new Vector3(size, size, 0.06f));
        }
    }

    private void DrawCollectedSpawnPoints()
    {
        if (spawnPoints == null)
        {
            return;
        }

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            DreamRoomSpawnPoint point = spawnPoints[i];

            if (point == null)
            {
                continue;
            }

            Vector3 center =
                GetLocalCellCenter(point.LocalCell);

            Gizmos.color =
                GetSpawnPointColor(point.Kind);

            Gizmos.DrawWireSphere(center, 0.2f);
            Gizmos.DrawLine(
                center + Vector3.left * 0.24f,
                center + Vector3.right * 0.24f);
            Gizmos.DrawLine(
                center + Vector3.down * 0.24f,
                center + Vector3.up * 0.24f);
        }
    }

    private static Color GetSpawnPointColor(
        DreamRoomSpawnPointKind kind)
    {
        switch (kind)
        {
            case DreamRoomSpawnPointKind.Player:
                return new Color(0.25f, 0.95f, 1f);

            case DreamRoomSpawnPointKind.Exit:
                return new Color(1f, 0.78f, 0.15f);

            case DreamRoomSpawnPointKind.Enemy:
                return new Color(1f, 0.3f, 0.3f);

            case DreamRoomSpawnPointKind.Item:
                return new Color(0.45f, 1f, 0.45f);

            default:
                return Color.white;
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
