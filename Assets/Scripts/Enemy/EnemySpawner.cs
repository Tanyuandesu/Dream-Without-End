using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum EnemySpawnMode
{
    BaselineSingleDefinition = 0,
    TemporaryFiveTypeShowcase = 1
}

/// <summary>
/// Definition-driven enemy spawn authority.
/// Enemy type data lives exclusively in EnemyDefinition assets; this
/// component owns only spawn selection, floor navigation service settings
/// and shared temporary-health-bar presentation.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    private const int R83EnemySelectionSalt = 830301;

    private static readonly string[] TemporaryShowcaseEnemyIds =
    {
        "dream_wanderer",
        "dream_scout",
        "dream_hunter",
        "dream_brute",
        "dream_gazer"
    };

    [Header("EA1 enemy data")]
    [Tooltip("Five stable enemy identities available to encounter authoring.")]
    [SerializeField] private EnemyCatalog enemyCatalog;

    [Tooltip(
        "Selects the authoritative enemy spawn input. " +
        "Baseline preserves the existing single-Definition flow. " +
        "Temporary showcase assigns five stable enemy types to five " +
        "non-Start/non-Exit rooms.")]
    [SerializeField] private EnemySpawnMode spawnMode =
        EnemySpawnMode.BaselineSingleDefinition;

    [Tooltip(
        "Baseline mode spawns only this Definition so existing behaviour " +
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

    [Header("EA3 shared navigation service")]
    [Tooltip(
        "EA3 baseline is four-directional. Eight-directional animation is " +
        "independent from this grid topology.")]
    [SerializeField] private EnemyNavigationTopology navigationTopology =
        EnemyNavigationTopology.FourDirections;

    [Range(1, 32)]
    [SerializeField] private int maxPathQueriesPerFrame = 2;

    [Min(64)]
    [SerializeField] private int maxExpandedPathNodesPerQuery = 4096;

    [Range(1, 4)]
    [SerializeField] private int navigationStartRecoveryRadiusInCells = 1;

    [Tooltip(
        "Off in EA3 so every turn remains an explicit cell-center waypoint.")]
    [SerializeField] private bool simplifyCollinearPathWaypoints;

    [Header("戰鬥：敵人臨時血條")]
    [SerializeField]
    private EnemyTemporaryHealthBarSettings temporaryHealthBarSettings =
        EnemyTemporaryHealthBarSettings.CreateDefault();

    private PhysicsMaterial2D frictionlessMaterial;
    private Sprite fallbackSprite;

    public EnemyCatalog Catalog => enemyCatalog;
    public EnemySpawnMode SpawnMode => spawnMode;
    public EnemyDefinition DefaultEnemyDefinition =>
        defaultEnemyDefinition;

    public int ConfiguredEnemyCount => enemyCount;
    public EnemyNavigationTopology NavigationTopology =>
        navigationTopology;

    public int MaxPathQueriesPerFrame => maxPathQueriesPerFrame;
    public bool SimplifiesCollinearPathWaypoints =>
        simplifyCollinearPathWaypoints;

    public EnemyTemporaryHealthBarSettings TemporaryHealthBarSettings
    {
        get
        {
            EnsureTemporaryHealthBarSettings();
            return temporaryHealthBarSettings;
        }
    }

    /// <summary>
    /// T2 authoritative input builder. This method decides only which
    /// EnemyDefinitions belong to the current request. Room selection and
    /// SpawnCell resolution remain separate stages.
    /// </summary>
    public bool TryBuildConfiguredSpawnPlan(
        List<EnemyDefinition> destination,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (destination == null)
        {
            failureReason =
                "Spawn plan destination list is null.";
            return false;
        }

        destination.Clear();

        switch (spawnMode)
        {
            case EnemySpawnMode.BaselineSingleDefinition:
            {
                if (!TryResolveBaselineDefinition(
                        out EnemyDefinition definition,
                        out failureReason))
                {
                    return false;
                }

                for (int i = 0; i < enemyCount; i++)
                {
                    destination.Add(definition);
                }

                return true;
            }

            case EnemySpawnMode.TemporaryFiveTypeShowcase:
                return TryBuildTemporaryShowcasePlan(
                    destination,
                    out failureReason);

            default:
                failureReason =
                    "Enemy Spawn Mode contains an undefined value: " +
                    spawnMode + ".";
                return false;
        }
    }

    private void Awake()
    {
        ValidateSettings();
        CreateFrictionlessMaterial();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    /// <summary>
    /// Single runtime spawn entry. Floor Session Id makes every generated
    /// enemy traceable even when the same floor is regenerated with the same
    /// seed. All spawned enemies must have a valid EnemyDefinition.
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

        List<EnemyDefinition> spawnPlan =
            new List<EnemyDefinition>();
        string planFailureReason;

        if (!TryBuildConfiguredSpawnPlan(
                spawnPlan,
                out planFailureReason))
        {
            Debug.LogWarning(
                "[EnemySpawner/T2] Enemy spawn plan was rejected. " +
                "Reason=" + planFailureReason +
                " | EnemiesSpawned=0",
                this);

            return enemies;
        }

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

        int amount;

        if (spawnMode == EnemySpawnMode.TemporaryFiveTypeShowcase)
        {
            if (candidateRoomIndices.Count < spawnPlan.Count)
            {
                LogR83EnemyRejection(
                    layout,
                    "Temporary Five Type Showcase requires " +
                    spawnPlan.Count +
                    " non-Start/non-Exit rooms, but only " +
                    candidateRoomIndices.Count +
                    " are available. The request was rejected before " +
                    "any enemy was created.",
                    startRoomIndex,
                    exitRoomIndex,
                    runtimeSpawnReservations);

                return enemies;
            }

            amount = spawnPlan.Count;
        }
        else
        {
            amount = Mathf.Clamp(
                spawnPlan.Count,
                0,
                candidateRoomIndices.Count);
        }

        if (spawnPlan.Count > 0 && amount == 0)
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

        EnemyPathService pathService =
            GetOrCreatePathService(
                layout,
                dungeonRoot,
                dungeonRenderer);

        if (pathService == null || !pathService.IsInitialized)
        {
            LogR83EnemyRejection(
                layout,
                "EA3 EnemyPathService 初始化失败，" +
                "未创建任何敌人，避免部分导航栈提交。",
                startRoomIndex,
                exitRoomIndex,
                runtimeSpawnReservations);

            return enemies;
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
                spawnPlan[i],
                floorNumber,
                floorSessionId,
                spawnResult.RoomIndex,
                pathService);

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
            spawnPlan,
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
        int roomIndex,
        EnemyPathService pathService)
    {
        string enemyName = definition.DisplayName;

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
            definition.CountsForEnding);

        CreateHealth(enemy, definition);
        CreateVisual(enemy, definition);
        CreateTemporaryHealthBar(enemy, definition);
        CreatePhysics(enemy, definition);

        EnemyPathfinder pathfinder =
            enemy.AddComponent<EnemyPathfinder>();

        pathfinder.Initialize(pathService);

        EnemyMotor2D motor =
            enemy.AddComponent<EnemyMotor2D>();

        motor.Initialize(
            enemy.GetComponent<Rigidbody2D>(),
            definition.MoveSpeed);

        EnemyDetection detection =
            enemy.AddComponent<EnemyDetection>();

        detection.Initialize(
            player,
            definition.DetectionRadius,
            definition.LoseTargetRadius,
            definition.RequireLineOfSight,
            definition.ObstacleMask,
            definition.ViewAngle,
            motor);

        EnemyRuntimeContext runtimeContext =
            enemy.AddComponent<EnemyRuntimeContext>();

        runtimeContext.Initialize(
            identity,
            definition,
            player,
            enemy.GetComponent<Rigidbody2D>(),
            pathfinder,
            detection,
            enemy.GetComponent<Health>(),
            enemy.GetComponent<EnemyVisual>(),
            roomIndex,
            spawnCell,
            enemy.transform.position);

        EnemyNavigationAgent navigationAgent =
            enemy.AddComponent<EnemyNavigationAgent>();

        navigationAgent.Initialize(
            runtimeContext,
            pathService,
            pathfinder,
            motor,
            definition.WaypointTolerance,
            definition.StopDistance,
            definition.LastPositionTolerance,
            definition.MinimumRepathInterval,
            definition.StuckTimeout,
            definition.StuckMovementThreshold,
            definition.MaximumRecoveryAttempts,
            definition.MaximumRecoverySnapDistance,
            definition.FailedPathRetryDelay,
            definition.MaximumChasePathCost);

        runtimeContext.AttachNavigation(
            pathService,
            navigationAgent,
            motor);

        EnemyMeleeAttackController meleeAttackController = null;
        EnemyProjectileAttackController projectileAttackController = null;

        if (SupportsFormalMeleeAttack(definition))
        {
            meleeAttackController =
                enemy.AddComponent<EnemyMeleeAttackController>();
        }
        else if (SupportsFormalProjectileAttack(definition))
        {
            projectileAttackController =
                enemy.AddComponent<EnemyProjectileAttackController>();
        }

        EnemyStateMachine stateMachine =
            enemy.AddComponent<EnemyStateMachine>();

        stateMachine.Initialize(
            runtimeContext,
            navigationAgent);

        if (meleeAttackController != null)
        {
            meleeAttackController.Initialize(
                runtimeContext,
                stateMachine);
        }

        if (projectileAttackController != null)
        {
            projectileAttackController.Initialize(
                runtimeContext,
                stateMachine);
        }

        EnemyCombatReceiver combatReceiver =
            enemy.AddComponent<EnemyCombatReceiver>();

        combatReceiver.Initialize(
            runtimeContext,
            definition,
            enemy.GetComponent<Health>(),
            motor,
            stateMachine);

        EnemyCombatAnimationBridge animationBridge =
            enemy.AddComponent<EnemyCombatAnimationBridge>();

        animationBridge.Initialize(
            combatReceiver,
            enemy.GetComponent<Health>(),
            enemy.GetComponent<EnemyVisual>());

        // CB6 subscribes after EnemyStateMachine so the Dead transition and
        // motor cancellation happen before runtime hazards are sealed.
        // EnemyManager registers after SpawnTestEnemies returns, so death
        // attribution and active-list removal remain the manager's job.
        EnemyDeathLifecycle deathLifecycle =
            enemy.AddComponent<EnemyDeathLifecycle>();

        deathLifecycle.Initialize(
            enemy.GetComponent<Health>(),
            identity,
            stateMachine,
            motor,
            navigationAgent,
            detection,
            combatReceiver);

        return enemy;
    }

    private EnemyPathService GetOrCreatePathService(
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer)
    {
        if (layout == null ||
            dungeonRoot == null ||
            dungeonRenderer == null)
        {
            return null;
        }

        EnemyPathService service =
            dungeonRoot.GetComponent<EnemyPathService>();

        if (service == null)
        {
            service = dungeonRoot.gameObject.AddComponent<
                EnemyPathService>();
        }

        service.Initialize(
            layout,
            dungeonRenderer.CellSize,
            navigationTopology,
            maxPathQueriesPerFrame,
            maxExpandedPathNodesPerQuery,
            navigationStartRecoveryRadiusInCells,
            simplifyCollinearPathWaypoints);

        return service;
    }

    private void CreateHealth(
        GameObject enemy,
        EnemyDefinition definition)
    {
        Health health =
            enemy.AddComponent<Health>();

        health.Initialize(
            definition.MaxHealth,
            DamageFaction.Enemy,
            definition.DamageInvulnerabilityTime,
            true);
    }

    private static bool SupportsFormalMeleeAttack(
        EnemyDefinition definition)
    {
        return definition != null &&
               definition.AttackMode == EnemyAttackMode.Melee &&
               definition.AttackDamage > 0f &&
               definition.AttackRange > 0f;
    }

    private static bool SupportsFormalProjectileAttack(
        EnemyDefinition definition)
    {
        return definition != null &&
               definition.AttackMode == EnemyAttackMode.Projectile &&
               definition.AttackDamage > 0f &&
               definition.AttackRange > 0f &&
               definition.ProjectileSpeed > 0f &&
               definition.ProjectileLifetime > 0f &&
               definition.ProjectileRadius > 0f;
    }

    private void CreateVisual(
        GameObject enemy,
        EnemyDefinition definition)
    {
        EnemyVisual enemyVisual =
            enemy.AddComponent<EnemyVisual>();

        Sprite configuredSprite = definition.EnemySprite;

        Sprite spriteToUse = configuredSprite != null
            ? configuredSprite
            : GetFallbackSprite();

        enemyVisual.Initialize(
            spriteToUse,
            GetFallbackSprite(),
            definition.VisualWorldHeight,
            definition.VisualOffset,
            definition.VisualColor,
            definition.SortingOrder,
            definition.AnimationProfile);
    }

    private void CreateTemporaryHealthBar(
        GameObject enemy,
        EnemyDefinition definition)
    {
        EnsureTemporaryHealthBarSettings();

        bool enemyHealthBarEnabled =
            definition.TemporaryHealthBarEnabled;

        if (!temporaryHealthBarSettings.Enabled ||
            !enemyHealthBarEnabled)
        {
            return;
        }

        Health health = enemy.GetComponent<Health>();
        EnemyVisual enemyVisual = enemy.GetComponent<EnemyVisual>();

        if (health == null || enemyVisual == null)
        {
            return;
        }

        EnemyTemporaryHealthBar healthBar =
            enemy.AddComponent<EnemyTemporaryHealthBar>();

        healthBar.Initialize(
            health,
            enemyVisual,
            temporaryHealthBarSettings,
            true,
            definition.TemporaryHealthBarSizeMultiplier,
            definition.TemporaryHealthBarOffset);
    }

    private void CreatePhysics(
        GameObject enemy,
        EnemyDefinition definition)
    {
        BoxCollider2D enemyCollider =
            enemy.AddComponent<BoxCollider2D>();

        enemyCollider.size = definition.ColliderSize;
        enemyCollider.offset = definition.ColliderOffset;
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

        bool mustExcludeExitRoom =
            excludeExitRoom ||
            spawnMode == EnemySpawnMode.TemporaryFiveTypeShowcase;

        if (mustExcludeExitRoom && exitRoomIndex < 0)
        {
            failureReason =
                "当前生成模式要求排除 Exit Room，但无法确定 " +
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

            if (mustExcludeExitRoom &&
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

    private bool TryBuildTemporaryShowcasePlan(
        List<EnemyDefinition> destination,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (enemyCatalog == null)
        {
            failureReason =
                "Temporary showcase requires an assigned Enemy Catalog.";
            return false;
        }

        if (enemyCatalog.Count != TemporaryShowcaseEnemyIds.Length)
        {
            failureReason =
                "Temporary showcase requires exactly " +
                TemporaryShowcaseEnemyIds.Length +
                " catalog definitions. Actual=" +
                enemyCatalog.Count + ".";
            return false;
        }

        HashSet<EnemyId> usedIds = new HashSet<EnemyId>();

        for (int i = 0;
             i < TemporaryShowcaseEnemyIds.Length;
             i++)
        {
            string requiredId =
                TemporaryShowcaseEnemyIds[i];
            EnemyDefinition definition;

            if (!enemyCatalog.TryGet(
                    requiredId,
                    out definition) ||
                definition == null)
            {
                failureReason =
                    "Temporary showcase catalog is missing EnemyId '" +
                    requiredId + "'.";
                destination.Clear();
                return false;
            }

            if (!definition.Id.IsValid)
            {
                failureReason =
                    "Temporary showcase contains an invalid EnemyId at " +
                    "slot " + i + ".";
                destination.Clear();
                return false;
            }

            if (!usedIds.Add(definition.Id))
            {
                failureReason =
                    "Temporary showcase contains duplicate EnemyId '" +
                    definition.Id + "'.";
                destination.Clear();
                return false;
            }

            destination.Add(definition);
        }

        return true;
    }

    private bool TryResolveBaselineDefinition(
        out EnemyDefinition definition,
        out string failureReason)
    {
        definition = defaultEnemyDefinition;
        failureReason = string.Empty;

        if (enemyCatalog == null)
        {
            failureReason =
                "Baseline spawn requires an assigned Enemy Catalog.";
            definition = null;
            return false;
        }

        if (definition == null)
        {
            failureReason =
                "Baseline spawn requires an assigned Default Enemy Definition.";
            return false;
        }

        if (!definition.Id.IsValid)
        {
            failureReason =
                "Default Enemy Definition has an invalid EnemyId.";
            definition = null;
            return false;
        }

        if (!enemyCatalog.Contains(definition))
        {
            failureReason =
                "Default Enemy Definition '" + definition.Id +
                "' is not registered in the assigned Enemy Catalog.";
            definition = null;
            return false;
        }

        return true;
    }

    private static string BuildRuntimeInstanceId(
        EnemyDefinition definition,
        int floorNumber,
        int floorSessionId,
        int layoutSeed,
        int enemyIndex,
        Vector2Int spawnCell)
    {
        string enemyId = definition.Id.Value;

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
            " | SpawnMode=" + spawnMode +
            " | ExcludeExitRoom=" +
            (excludeExitRoom ||
             spawnMode == EnemySpawnMode.TemporaryFiveTypeShowcase) +
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
        IReadOnlyList<EnemyDefinition> spawnPlan,
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
            "[EnemySpawner/T3] Enemy room assignment committed.");

        report.AppendLine(
            "Requested=SpawnPoint(Enemy)" +
            " | SpawnMode=" + spawnMode +
            " | RequestedCount=" +
            (spawnPlan != null ? spawnPlan.Count : 0) +
            " | EffectiveCount=" + spawnResults.Count +
            " | Seed=" + layout.Seed +
            " | StartRoomIndex=" + startRoomIndex +
            " | ExitRoomIndex=" + exitRoomIndex +
            " | ExcludeExitRoom=" +
            (excludeExitRoom ||
             spawnMode == EnemySpawnMode.TemporaryFiveTypeShowcase));

        for (int i = 0; i < spawnResults.Count; i++)
        {
            DungeonSpawnCellResult result = spawnResults[i];

            EnemyDefinition definition = spawnPlan[i];

            report.AppendLine(
                "Enemy_" + (i + 1) +
                " EnemyId=" +
                definition.Id.Value +
                " | Requested=SpawnPoint(Enemy)" +
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
        if (!Enum.IsDefined(typeof(EnemySpawnMode), spawnMode))
        {
            spawnMode = EnemySpawnMode.BaselineSingleDefinition;
        }

        enemyCount = Mathf.Max(0, enemyCount);
        maxPathQueriesPerFrame = Mathf.Clamp(
            maxPathQueriesPerFrame > 0
                ? maxPathQueriesPerFrame
                : 2,
            1,
            32);

        maxExpandedPathNodesPerQuery = Mathf.Max(
            64,
            maxExpandedPathNodesPerQuery > 0
                ? maxExpandedPathNodesPerQuery
                : 4096);

        navigationStartRecoveryRadiusInCells = Mathf.Clamp(
            navigationStartRecoveryRadiusInCells > 0
                ? navigationStartRecoveryRadiusInCells
                : 1,
            1,
            4);

        EnsureTemporaryHealthBarSettings();
    }

    private void EnsureTemporaryHealthBarSettings()
    {
        if (temporaryHealthBarSettings == null)
        {
            temporaryHealthBarSettings =
                EnemyTemporaryHealthBarSettings.CreateDefault();
        }

        temporaryHealthBarSettings.EnsureValid();
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
