using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 測試敵人生成器。
/// 包含外觀、生命、接觸傷害與目前 AI 參數。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    private const int R83EnemySelectionSalt = 830301;

    [Header("EA1 enemy data")]
    [Tooltip("Five stable enemy identities available to encounter authoring.")]
    [SerializeField] private EnemyCatalog enemyCatalog;

    [Tooltip(
        "EA1 currently spawns only this Definition so baseline behaviour " +
        "remains unchanged.")]
    [SerializeField] private EnemyDefinition defaultEnemyDefinition;

    [Header("生成數量")]
    [Min(0)]
    [SerializeField] private int enemyCount = 1;
    [SerializeField] private bool spawnNearPlayerFirst = true;
    [SerializeField] private bool excludeExitRoom = true;

    [Header("R8.3 受控失败测试")]
    [Tooltip(
        "只用于 R8.3 受控失败：把全部 FloorCells 视为已保留，" +
        "强制 Enemy Spawn Cell 解析被拒绝。正常运行必须关闭；" +
        "不会修改 Layout、Prefab 或敌人 AI。")]
    [SerializeField]
    private bool r83InjectNoLegalEnemyCellForControlledFailure;

    [Header("移動參數")]
    [Min(0.1f)]
    [SerializeField, HideInInspector] private float moveSpeed = 3.2f;

    [Range(0.001f, 0.25f)]
    [SerializeField, HideInInspector]
    private float waypointTolerance = 0.035f;

    [Min(0f)]
    [SerializeField, HideInInspector] private float stopDistance = 0.72f;

    [Range(0.01f, 1f)]
    [SerializeField, HideInInspector]
    private float lastPositionTolerance = 0.15f;

    [Header("索敵參數")]
    [Min(0.1f)]
    [SerializeField, HideInInspector] private float detectionRadius = 20f;

    [Min(0.1f)]
    [SerializeField, HideInInspector] private float loseTargetRadius = 24f;

    [SerializeField, HideInInspector]
    private bool requireLineOfSight = false;

    [SerializeField, HideInInspector] private LayerMask obstacleMask = ~0;

    [Header("敵人生命")]
    [Min(1f)]
    [SerializeField, HideInInspector] private float maxHealth = 30f;

    [Min(0f)]
    [SerializeField, HideInInspector]
    private float damageInvulnerabilityTime =
        0.1f;

    [Header("碰撞傷害")]
    [SerializeField, HideInInspector]
    private bool enableContactDamage = true;

    [Min(0f)]
    [SerializeField, HideInInspector] private float contactDamage = 10f;

    [Min(0f)]
    [SerializeField, HideInInspector]
    private float contactDamageCooldown = 0.75f;

    [SerializeField, HideInInspector]
    private DamageFactionMask contactDamageTargets =
        DamageFactionMask.Player;

    [Header("敵人外觀")]
    [SerializeField, HideInInspector] private Sprite enemySprite;

    [Header("敌人方向动画")]
    [Tooltip(
        "当 Animation Profiles 为空时使用。")]
    [SerializeField, HideInInspector]
    private CharacterAnimationProfile defaultAnimationProfile;

    [Tooltip(
        "未来可放入多种敌人的动画配置。" +
        "CA1 会按生成顺序循环选取，不改变 AI 或数值。")]
    [SerializeField, HideInInspector]
    private List<CharacterAnimationProfile> animationProfiles =
        new List<CharacterAnimationProfile>();

    [Min(0.05f)]
    [SerializeField, HideInInspector]
    private float visualWorldHeight = 0.8f;

    [SerializeField, HideInInspector]
    private Vector2 visualOffset =
        Vector2.zero;

    [SerializeField, HideInInspector]
    private Color visualColor =
        new Color(0.2f, 0.85f, 0.25f);

    [SerializeField, HideInInspector] private int sortingOrder = 20;

    [Header("敵人碰撞")]
    [SerializeField, HideInInspector]
    private Vector2 colliderSize =
        new Vector2(0.58f, 0.58f);

    [SerializeField, HideInInspector]
    private Vector2 colliderOffset =
        Vector2.zero;

    private PhysicsMaterial2D frictionlessMaterial;
    private Sprite fallbackSprite;
    private bool loggedMissingDefinition;
    private bool loggedDefinitionOutsideCatalog;

    public EnemyCatalog Catalog => enemyCatalog;
    public EnemyDefinition DefaultEnemyDefinition =>
        defaultEnemyDefinition;

    public int ConfiguredEnemyCount => enemyCount;

    private void Awake()
    {
        ValidateSettings();
        CreateFrictionlessMaterial();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    public List<GameObject> SpawnTestEnemies(
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player)
    {
        return SpawnTestEnemies(
            layout,
            dungeonRoot,
            dungeonRenderer,
            player,
            null);
    }

    /// <summary>
    /// R8.3 运行时入口。旧四参数入口完整保留；
    /// 先解析全部安全格，再一次性实例化，避免部分提交。
    /// 本方法只改变生成位置来源，不改变任何 AI 参数或组件。
    /// </summary>
    public List<GameObject> SpawnTestEnemies(
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        return SpawnTestEnemies(
            layout,
            dungeonRoot,
            dungeonRenderer,
            player,
            runtimeSpawnReservations,
            0,
            0);
    }

    /// <summary>
    /// EA1 runtime entry. Floor Session Id makes every generated enemy
    /// traceable even when the same floor is regenerated with the same seed.
    /// </summary>
    public List<GameObject> SpawnTestEnemies(
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player,
        ISet<Vector2Int> runtimeSpawnReservations,
        int floorNumber,
        int floorSessionId)
    {
        List<GameObject> enemies =
            new List<GameObject>();

        if (layout == null ||
            layout.Rooms == null ||
            layout.Rooms.Count < 2 ||
            dungeonRoot == null ||
            dungeonRenderer == null ||
            player == null)
        {
            return enemies;
        }

        ValidateSettings();
        CreateFrictionlessMaterial();

        EnemyDefinition definition =
            ResolveDefaultDefinition();

        List<int> candidateRoomIndices;
        int startRoomIndex;
        int exitRoomIndex;
        string roomFailureReason;

        if (!TryGetCandidateRoomIndices(
                layout,
                out candidateRoomIndices,
                out startRoomIndex,
                out exitRoomIndex,
                out roomFailureReason))
        {
            LogR83EnemyRejection(
                layout,
                roomFailureReason,
                startRoomIndex,
                exitRoomIndex,
                runtimeSpawnReservations);

            return enemies;
        }

        int amount = Mathf.Clamp(
            enemyCount,
            0,
            candidateRoomIndices.Count);

        if (enemyCount > 0 && amount == 0)
        {
            LogR83EnemyRejection(
                layout,
                "排除 Start Room 与当前 Exit Room 策略后，" +
                "没有可用于 Enemy 的房间。",
                startRoomIndex,
                exitRoomIndex,
                runtimeSpawnReservations);

            return enemies;
        }

        HashSet<Vector2Int> requestReservations =
            runtimeSpawnReservations == null
                ? new HashSet<Vector2Int>()
                : new HashSet<Vector2Int>(
                    runtimeSpawnReservations);

        requestReservations.Add(layout.StartCell);
        requestReservations.Add(layout.ExitCell);

        if (r83InjectNoLegalEnemyCellForControlledFailure)
        {
            requestReservations.UnionWith(
                layout.FloorCells);
        }

        List<DungeonSpawnCellResult> spawnResults =
            new List<DungeonSpawnCellResult>(amount);

        for (int i = 0; i < amount; i++)
        {
            int roomIndex = candidateRoomIndices[i];

            DungeonSpawnCellRequest request =
                new DungeonSpawnCellRequest(
                    layout,
                    DreamRoomSpawnPointKind.Enemy,
                    new int[] { roomIndex },
                    selectionSalt:
                        BuildR83EnemySelectionSalt(
                            roomIndex,
                            i),
                    reservedCells: requestReservations,
                    excludeStartCell: true,
                    excludeExitCell: true,
                    preferredCell:
                        GetRoomBoundsCenter(
                            layout,
                            roomIndex),
                    allowWalkableFallback: true,
                    allowLayoutWideFallback: false);

            DungeonSpawnCellResult spawnResult;
            string failureReason;

            if (!DungeonSpawnCellResolver.TryResolve(
                    request,
                    out spawnResult,
                    out failureReason))
            {
                LogR83EnemyRejection(
                    layout,
                    "Enemy_" + (i + 1) +
                    " RoomIndex=" + roomIndex +
                    " | " + failureReason,
                    startRoomIndex,
                    exitRoomIndex,
                    runtimeSpawnReservations);

                return enemies;
            }

            spawnResults.Add(spawnResult);
            requestReservations.Add(spawnResult.Cell);
        }

        for (int i = 0; i < spawnResults.Count; i++)
        {
            DungeonSpawnCellResult spawnResult =
                spawnResults[i];

            GameObject enemy = CreateEnemy(
                spawnResult.Cell,
                i + 1,
                layout,
                dungeonRoot,
                dungeonRenderer,
                player,
                definition,
                floorNumber,
                floorSessionId,
                spawnResult.RoomIndex);

            enemies.Add(enemy);

            if (runtimeSpawnReservations != null)
            {
                runtimeSpawnReservations.Add(
                    spawnResult.Cell);
            }
        }

        LogR83EnemyCommit(
            layout,
            spawnResults,
            startRoomIndex,
            exitRoomIndex,
            runtimeSpawnReservations);

        return enemies;
    }

    private GameObject CreateEnemy(
        Vector2Int spawnCell,
        int index,
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player,
        EnemyDefinition definition,
        int floorNumber,
        int floorSessionId,
        int roomIndex)
    {
        string enemyName = definition != null
            ? definition.DisplayName
            : "LegacyEnemy";

        GameObject enemy =
            new GameObject(enemyName + "_" + index);

        enemy.transform.SetParent(dungeonRoot);
        enemy.transform.position =
            dungeonRenderer.CellToWorld(spawnCell);

        enemy.transform.localScale = Vector3.one;

        EnemyRuntimeIdentity identity =
            enemy.AddComponent<EnemyRuntimeIdentity>();

        identity.Initialize(
            BuildRuntimeInstanceId(
                definition,
                floorNumber,
                floorSessionId,
                layout.Seed,
                index,
                spawnCell),
            definition,
            floorNumber,
            floorSessionId,
            roomIndex,
            spawnCell,
            definition == null || definition.CountsForEnding);

        CreateHealth(enemy, definition);
        CreateVisual(enemy, index, definition);
        CreatePhysics(enemy, definition);
        CreateContactDamage(enemy, definition);

        EnemyPathfinder pathfinder =
            enemy.AddComponent<EnemyPathfinder>();

        pathfinder.Initialize(
            layout,
            dungeonRenderer.CellSize);

        EnemyDetection detection =
            enemy.AddComponent<EnemyDetection>();

        detection.Initialize(
            player,
            definition != null
                ? definition.DetectionRadius
                : detectionRadius,
            definition != null
                ? definition.LoseTargetRadius
                : loseTargetRadius,
            definition != null
                ? definition.RequireLineOfSight
                : requireLineOfSight,
            definition != null
                ? definition.ObstacleMask
                : obstacleMask);

        TestEnemyAI enemyAI =
            enemy.AddComponent<TestEnemyAI>();

        enemyAI.Initialize(
            player,
            pathfinder,
            detection,
            definition != null
                ? definition.MoveSpeed
                : moveSpeed,
            definition != null
                ? definition.WaypointTolerance
                : waypointTolerance,
            definition != null
                ? definition.StopDistance
                : stopDistance,
            definition != null
                ? definition.LastPositionTolerance
                : lastPositionTolerance);

        return enemy;
    }

    private void CreateHealth(
        GameObject enemy,
        EnemyDefinition definition)
    {
        Health health =
            enemy.AddComponent<Health>();

        health.Initialize(
            definition != null
                ? definition.MaxHealth
                : maxHealth,
            DamageFaction.Enemy,
            definition != null
                ? definition.DamageInvulnerabilityTime
                : damageInvulnerabilityTime,
            true);
    }

    private void CreateContactDamage(
        GameObject enemy,
        EnemyDefinition definition)
    {
        bool shouldEnable = definition != null
            ? definition.EnableLegacyContactDamage
            : enableContactDamage;

        if (!shouldEnable)
        {
            return;
        }

        ContactDamage2D contactDamageComponent =
            enemy.AddComponent<ContactDamage2D>();

        contactDamageComponent.Initialize(
            definition != null
                ? definition.LegacyContactDamage
                : contactDamage,
            definition != null
                ? definition.LegacyContactDamageCooldown
                : contactDamageCooldown,
            definition != null
                ? definition.LegacyContactDamageTargets
                : contactDamageTargets);
    }

    private void CreateVisual(
        GameObject enemy,
        int enemyIndex,
        EnemyDefinition definition)
    {
        EnemyVisual enemyVisual =
            enemy.AddComponent<EnemyVisual>();

        Sprite configuredSprite = definition != null
            ? definition.EnemySprite
            : enemySprite;

        Sprite spriteToUse = configuredSprite != null
            ? configuredSprite
            : GetFallbackSprite();

        enemyVisual.Initialize(
            spriteToUse,
            GetFallbackSprite(),
            definition != null
                ? definition.VisualWorldHeight
                : visualWorldHeight,
            definition != null
                ? definition.VisualOffset
                : visualOffset,
            definition != null
                ? definition.VisualColor
                : visualColor,
            definition != null
                ? definition.SortingOrder
                : sortingOrder,
            GetAnimationProfile(enemyIndex, definition));
    }

    private CharacterAnimationProfile GetAnimationProfile(
        int enemyIndex,
        EnemyDefinition definition)
    {
        if (definition != null)
        {
            return definition.AnimationProfile;
        }

        if (animationProfiles != null &&
            animationProfiles.Count > 0)
        {
            int safeIndex = Mathf.Abs(enemyIndex - 1) %
                            animationProfiles.Count;

            CharacterAnimationProfile selected =
                animationProfiles[safeIndex];

            if (selected != null)
            {
                return selected;
            }
        }

        return defaultAnimationProfile;
    }

    private void CreatePhysics(
        GameObject enemy,
        EnemyDefinition definition)
    {
        BoxCollider2D enemyCollider =
            enemy.AddComponent<BoxCollider2D>();

        enemyCollider.size = definition != null
            ? definition.ColliderSize
            : colliderSize;

        enemyCollider.offset = definition != null
            ? definition.ColliderOffset
            : colliderOffset;
        enemyCollider.sharedMaterial =
            frictionlessMaterial;

        Rigidbody2D body =
            enemy.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation =
            RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
        body.sleepMode =
            RigidbodySleepMode2D.NeverSleep;
    }

    private bool TryGetCandidateRoomIndices(
        DungeonLayout layout,
        out List<int> candidateRoomIndices,
        out int startRoomIndex,
        out int exitRoomIndex,
        out string failureReason)
    {
        candidateRoomIndices = new List<int>();
        startRoomIndex = FindRoomContainingCell(
            layout,
            layout.StartCell);
        exitRoomIndex = FindRoomContainingCell(
            layout,
            layout.ExitCell);
        failureReason = string.Empty;

        if (startRoomIndex < 0)
        {
            failureReason =
                "无法确定 StartCell 所属房间，" +
                "不能保证排除 Start Room。";

            return false;
        }

        if (excludeExitRoom && exitRoomIndex < 0)
        {
            failureReason =
                "Exclude Exit Room 已开启，但无法确定 " +
                "ExitCell 所属房间。";

            return false;
        }

        int roomCount = Mathf.Max(
            layout.Rooms.Count,
            layout.RoomPlacements.Count);

        for (int roomIndex = 0;
             roomIndex < roomCount;
             roomIndex++)
        {
            if (roomIndex == startRoomIndex)
            {
                continue;
            }

            if (excludeExitRoom &&
                roomIndex == exitRoomIndex)
            {
                continue;
            }

            candidateRoomIndices.Add(roomIndex);
        }

        if (spawnNearPlayerFirst)
        {
            candidateRoomIndices.Sort(
                (firstRoomIndex, secondRoomIndex) =>
                {
                    int firstDistance = Manhattan(
                        layout.StartCell,
                        GetRoomBoundsCenter(
                            layout,
                            firstRoomIndex));

                    int secondDistance = Manhattan(
                        layout.StartCell,
                        GetRoomBoundsCenter(
                            layout,
                            secondRoomIndex));

                    int distanceComparison =
                        firstDistance.CompareTo(
                            secondDistance);

                    return distanceComparison != 0
                        ? distanceComparison
                        : firstRoomIndex.CompareTo(
                            secondRoomIndex);
                });
        }
        else
        {
            Shuffle(
                candidateRoomIndices,
                new System.Random(
                    layout.Seed ^ 19349663));
        }

        return true;
    }

    private static int FindRoomContainingCell(
        DungeonLayout layout,
        Vector2Int cell)
    {
        for (int roomIndex = 0;
             roomIndex < layout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null ||
                placement.Template == null ||
                !placement.ContainsBoundsCell(cell))
            {
                continue;
            }

            Vector2Int localCell =
                placement.GlobalToOriginalCell(cell);

            if (placement.Template.IsWalkableCell(localCell))
            {
                return roomIndex;
            }
        }

        for (int roomIndex = 0;
             roomIndex < layout.Rooms.Count;
             roomIndex++)
        {
            RectInt room = layout.Rooms[roomIndex];

            if (cell.x >= room.xMin &&
                cell.x < room.xMax &&
                cell.y >= room.yMin &&
                cell.y < room.yMax &&
                layout.FloorCells.Contains(cell))
            {
                return roomIndex;
            }
        }

        return -1;
    }

    private static Vector2Int GetRoomBoundsCenter(
        DungeonLayout layout,
        int roomIndex)
    {
        RectInt bounds;

        if (roomIndex >= 0 &&
            roomIndex < layout.RoomPlacements.Count &&
            layout.RoomPlacements[roomIndex] != null)
        {
            bounds =
                layout.RoomPlacements[roomIndex].CellBounds;
        }
        else if (roomIndex >= 0 &&
                 roomIndex < layout.Rooms.Count)
        {
            bounds = layout.Rooms[roomIndex];
        }
        else
        {
            return layout.StartCell;
        }

        return new Vector2Int(
            bounds.xMin + bounds.width / 2,
            bounds.yMin + bounds.height / 2);
    }

    private static int BuildR83EnemySelectionSalt(
        int roomIndex,
        int enemyIndex)
    {
        unchecked
        {
            int hash = R83EnemySelectionSalt;
            hash = hash * 31 + roomIndex;
            hash = hash * 31 + enemyIndex;
            return hash;
        }
    }

    private EnemyDefinition ResolveDefaultDefinition()
    {
        if (defaultEnemyDefinition == null)
        {
            if (!loggedMissingDefinition)
            {
                loggedMissingDefinition = true;

                Debug.LogWarning(
                    "[EnemySpawner/EA1] Default Enemy Definition is missing. " +
                    "Legacy serialized values will be used for this session.",
                    this);
            }

            return null;
        }

        if (enemyCatalog != null &&
            !enemyCatalog.Contains(defaultEnemyDefinition) &&
            !loggedDefinitionOutsideCatalog)
        {
            loggedDefinitionOutsideCatalog = true;

            Debug.LogWarning(
                "[EnemySpawner/EA1] Default Enemy Definition '" +
                defaultEnemyDefinition.Id +
                "' is not registered in the assigned Enemy Catalog.",
                this);
        }

        return defaultEnemyDefinition;
    }

    private static string BuildRuntimeInstanceId(
        EnemyDefinition definition,
        int floorNumber,
        int floorSessionId,
        int layoutSeed,
        int enemyIndex,
        Vector2Int spawnCell)
    {
        string enemyId = definition != null
            ? definition.Id.Value
            : "legacy_enemy";

        return "Floor" + Mathf.Max(0, floorNumber) +
               "_Session" + Mathf.Max(0, floorSessionId) +
               "_Seed" + layoutSeed +
               "_" + enemyId +
               "_" + enemyIndex +
               "_Cell" + spawnCell.x + "x" + spawnCell.y;
    }

    private void LogR83EnemyRejection(
        DungeonLayout layout,
        string failureReason,
        int startRoomIndex,
        int exitRoomIndex,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        Debug.LogWarning(
            "[EnemySpawner/R8.3] Enemy SpawnCell 提交被拒绝。" +
            "\nRequested=SpawnPoint(Enemy)" +
            " | Effective=Rejected" +
            " | ControlledFailure=" +
            r83InjectNoLegalEnemyCellForControlledFailure +
            " | Seed=" + (layout != null ? layout.Seed : 0) +
            "\nStartRoomIndex=" + startRoomIndex +
            " | ExitRoomIndex=" + exitRoomIndex +
            " | ExcludeExitRoom=" + excludeExitRoom +
            " | SharedReserved=" +
            (runtimeSpawnReservations != null
                ? runtimeSpawnReservations.Count
                : 0) +
            "\nReason=" + failureReason +
            "\nEnemiesSpawned=0" +
            " | PartialCommit=None" +
            " | LayoutMutation=None" +
            " | PrefabMutation=None" +
            " | EnemyAI=Unchanged",
            this);
    }

    private void LogR83EnemyCommit(
        DungeonLayout layout,
        List<DungeonSpawnCellResult> spawnResults,
        int startRoomIndex,
        int exitRoomIndex,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        StringBuilder report = new StringBuilder();
        HashSet<Vector2Int> uniqueCells =
            new HashSet<Vector2Int>();
        int floorCellMembershipCount = 0;

        for (int i = 0; i < spawnResults.Count; i++)
        {
            DungeonSpawnCellResult result = spawnResults[i];

            uniqueCells.Add(result.Cell);

            if (layout.FloorCells.Contains(result.Cell))
            {
                floorCellMembershipCount++;
            }
        }

        report.AppendLine(
            "[EnemySpawner/R8.3] Enemy SpawnCell 已提交。");

        report.AppendLine(
            "Requested=SpawnPoint(Enemy)" +
            " | RequestedCount=" + enemyCount +
            " | EffectiveCount=" + spawnResults.Count +
            " | Seed=" + layout.Seed +
            " | StartRoomIndex=" + startRoomIndex +
            " | ExitRoomIndex=" + exitRoomIndex +
            " | ExcludeExitRoom=" + excludeExitRoom);

        for (int i = 0; i < spawnResults.Count; i++)
        {
            DungeonSpawnCellResult result = spawnResults[i];

            report.AppendLine(
                "Enemy_" + (i + 1) +
                " Requested=SpawnPoint(Enemy)" +
                " | Effective=" + result.Source +
                " | RoomIndex=" + result.RoomIndex +
                " | Cell=" + result.Cell +
                " | SpawnPointId=" +
                (string.IsNullOrEmpty(result.SpawnPointId)
                    ? "None"
                    : result.SpawnPointId) +
                " | Candidates=" + result.CandidateCount +
                " | Rejected=" +
                result.RejectedCandidateCount +
                " | SelectionSeed=" + result.SelectionSeed);
        }

        report.Append(
            "FloorCellsMembership=" +
            floorCellMembershipCount +
            "/" + spawnResults.Count +
            " | UniqueCells=" + uniqueCells.Count +
            "/" + spawnResults.Count +
            " | SharedReserved=" +
            (runtimeSpawnReservations != null
                ? runtimeSpawnReservations.Count
                : spawnResults.Count + 2) +
            " | LayoutMutation=None" +
            " | EnemyAI=Unchanged");

        Debug.Log(report.ToString(), this);
    }

    private static void Shuffle<T>(
        IList<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);

            T temporary = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temporary;
        }
    }

    private static int Manhattan(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }

    private void ValidateSettings()
    {
        enemyCount = Mathf.Max(0, enemyCount);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);

        waypointTolerance = Mathf.Clamp(
            waypointTolerance,
            0.001f,
            0.25f);

        stopDistance = Mathf.Max(0f, stopDistance);

        lastPositionTolerance = Mathf.Clamp(
            lastPositionTolerance,
            0.01f,
            1f);

        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        loseTargetRadius = Mathf.Max(
            detectionRadius,
            loseTargetRadius);

        maxHealth = Mathf.Max(1f, maxHealth);
        damageInvulnerabilityTime = Mathf.Max(
            0f,
            damageInvulnerabilityTime);

        contactDamage = Mathf.Max(0f, contactDamage);
        contactDamageCooldown = Mathf.Max(
            0f,
            contactDamageCooldown);

        visualWorldHeight = Mathf.Max(
            0.05f,
            visualWorldHeight);

        colliderSize.x = Mathf.Max(
            0.05f,
            colliderSize.x);

        colliderSize.y = Mathf.Max(
            0.05f,
            colliderSize.y);
    }

    private void CreateFrictionlessMaterial()
    {
        if (frictionlessMaterial != null)
        {
            return;
        }

        frictionlessMaterial =
            new PhysicsMaterial2D("Enemy_NoFriction");

        frictionlessMaterial.friction = 0f;
        frictionlessMaterial.bounciness = 0f;
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false);

        texture.name = "EnemyFallbackTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        fallbackSprite.name =
            "EnemyFallbackSprite";

        return fallbackSprite;
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }

        if (fallbackSprite != null)
        {
            Texture2D texture =
                fallbackSprite.texture;

            Destroy(fallbackSprite);

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
