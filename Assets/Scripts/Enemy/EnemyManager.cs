using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理當前樓層的敵人清單。
/// 敵人死亡時會自動從清單移除。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySpawner))]
public sealed class EnemyManager : MonoBehaviour
{
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("EA1 run combat record")]
    [SerializeField] private EnemyRunRecord runRecord =
        new EnemyRunRecord();

    private readonly List<GameObject> activeEnemies =
        new List<GameObject>();

    private int floorSessionSerial;

    [Header("CB6 death and floor lifecycle diagnostics (read only)")]
    [SerializeField] private int registeredEnemyCount;
    [SerializeField] private int deathNotificationCount;
    [SerializeField] private int recordedPlayerDeathCount;
    [SerializeField] private int recordedOtherDeathCount;
    [SerializeField] private int unregisterCount;
    [SerializeField] private int duplicateDeathRecordCount;
    [SerializeField] private int unexpectedRemovalCount;
    [SerializeField] private int floorSetupCount;
    [SerializeField] private int floorClearCount;
    [SerializeField] private int lastClearSurvivorCount;
    [SerializeField] private string lastDeathInstanceId = string.Empty;
    [SerializeField] private DamageAttribution lastDeathAttribution =
        DamageAttribution.Unspecified;

    [Header("T5C local alert diagnostics (read only)")]
    [SerializeField] private int alertBroadcastCount;
    [SerializeField] private int alertDeliveryCount;
    [SerializeField] private string lastAlertSourceId = string.Empty;
    [SerializeField] private Vector2 lastAlertPosition;
    [SerializeField] private float lastAlertRadius;

    public IReadOnlyList<GameObject> ActiveEnemies =>
        activeEnemies;

    public EnemyRunRecord RunRecord => runRecord;

    public EnemyRunRecordSnapshot CurrentRunSnapshot =>
        runRecord != null
            ? runRecord.CreateSnapshot()
            : new EnemyRunRecordSnapshot();

    public int RegisteredEnemyCount => registeredEnemyCount;
    public int DeathNotificationCount => deathNotificationCount;
    public int RecordedPlayerDeathCount => recordedPlayerDeathCount;
    public int RecordedOtherDeathCount => recordedOtherDeathCount;
    public int UnregisterCount => unregisterCount;
    public int DuplicateDeathRecordCount => duplicateDeathRecordCount;
    public int UnexpectedRemovalCount => unexpectedRemovalCount;
    public int FloorSetupCount => floorSetupCount;
    public int FloorClearCount => floorClearCount;
    public int LastClearSurvivorCount => lastClearSurvivorCount;
    public string LastDeathInstanceId => lastDeathInstanceId;
    public DamageAttribution LastDeathAttribution => lastDeathAttribution;
    public int AlertBroadcastCount => alertBroadcastCount;
    public int AlertDeliveryCount => alertDeliveryCount;
    public string LastAlertSourceId => lastAlertSourceId;
    public Vector2 LastAlertPosition => lastAlertPosition;
    public float LastAlertRadius => lastAlertRadius;

    public int ActiveEnemyCount
    {
        get
        {
            RemoveDestroyedReferences();
            return activeEnemies.Count;
        }
    }

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    public void SetupFloor(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform)
    {
        SetupFloor(
            layout,
            floorRoot,
            renderer,
            playerTransform,
            null);
    }

    /// <summary>
    /// R8.3 运行时入口。旧四参数入口完整保留；
    /// GameManager 会传入已经包含 Player／Exit／Item 的保留格。
    /// </summary>
    public void SetupFloor(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        SetupFloor(
            0,
            layout,
            floorRoot,
            renderer,
            playerTransform,
            runtimeSpawnReservations);
    }

    /// <summary>
    /// EA1 entry that records the gameplay floor separately from the unique
    /// floor session. Regenerating the same floor therefore cannot duplicate
    /// enemy instance identities.
    /// </summary>
    public void SetupFloor(
        int floorNumber,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        ClearFloor();
        ResetFloorAlertDiagnostics();

        if (layout == null ||
            floorRoot == null ||
            renderer == null ||
            playerTransform == null)
        {
            Debug.LogError(
                "EnemyManager：新樓層資料不完整。");

            return;
        }

        CacheComponents();

        floorSessionSerial++;

        if (runRecord == null)
        {
            runRecord = new EnemyRunRecord();
        }

        runRecord.BeginFloor(
            floorNumber,
            floorSessionSerial,
            layout.Seed);

        floorSetupCount++;

        List<GameObject> spawnedEnemies =
            enemySpawner.SpawnTestEnemies(
                layout,
                floorRoot,
                renderer,
                playerTransform,
                runtimeSpawnReservations,
                floorNumber,
                floorSessionSerial);

        for (int i = 0;
             i < spawnedEnemies.Count;
             i++)
        {
            RegisterEnemy(spawnedEnemies[i]);
        }
    }

    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null ||
            activeEnemies.Contains(enemy))
        {
            return;
        }

        activeEnemies.Add(enemy);
        registeredEnemyCount++;

        Health health = enemy.GetComponent<Health>();

        if (health != null)
        {
            health.Died += HandleEnemyDied;
        }

        EnemyRuntimeIdentity identity =
            enemy.GetComponent<EnemyRuntimeIdentity>();

        if (identity != null)
        {
            identity.DestroyedUnexpectedly +=
                HandleEnemyDestroyedUnexpectedly;

            if (runRecord == null)
            {
                runRecord = new EnemyRunRecord();
            }

            runRecord.RegisterSpawn(identity);
        }
        else
        {
            Debug.LogWarning(
                "[EnemyManager/EA1] Registered enemy has no " +
                "EnemyRuntimeIdentity. Ending statistics cannot include it.",
                enemy);
        }
    }

    /// <summary>
    /// Delivers one event-driven last-known-position alert to living enemies
    /// in the current floor roster. Recipients decide how to react through
    /// their own EnemyStateMachine and EnemyDefinition values.
    /// </summary>
    public int BroadcastAlert(
        EnemyStateMachine source,
        Vector2 targetPosition,
        float radius)
    {
        if (source == null || radius <= 0f)
        {
            return 0;
        }

        RemoveDestroyedReferences();

        float radiusSquared = radius * radius;
        int delivered = 0;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            GameObject enemy = activeEnemies[i];

            if (enemy == null)
            {
                continue;
            }

            EnemyStateMachine recipient =
                enemy.GetComponent<EnemyStateMachine>();

            if (recipient == null || recipient == source)
            {
                continue;
            }

            Vector2 offset =
                (Vector2)recipient.transform.position -
                (Vector2)source.transform.position;

            if (offset.sqrMagnitude > radiusSquared)
            {
                continue;
            }

            if (recipient.TryReceiveAlert(
                    targetPosition,
                    source))
            {
                delivered++;
            }
        }

        alertBroadcastCount++;
        alertDeliveryCount += delivered;
        lastAlertSourceId = source.Context != null
            ? source.Context.EnemyId
            : source.gameObject.name;
        lastAlertPosition = targetPosition;
        lastAlertRadius = radius;

        return delivered;
    }

    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        Health health = enemy.GetComponent<Health>();

        if (health != null)
        {
            health.Died -= HandleEnemyDied;
        }

        EnemyRuntimeIdentity identity =
            enemy.GetComponent<EnemyRuntimeIdentity>();

        if (identity != null)
        {
            identity.DestroyedUnexpectedly -=
                HandleEnemyDestroyedUnexpectedly;
        }

        if (activeEnemies.Remove(enemy))
        {
            unregisterCount++;
        }
    }

    public void ClearFloor()
    {
        lastClearSurvivorCount = 0;

        if (activeEnemies.Count > 0)
        {
            floorClearCount++;
        }

        for (int i = 0;
             i < activeEnemies.Count;
             i++)
        {
            GameObject enemy = activeEnemies[i];

            if (enemy == null)
            {
                continue;
            }

            Health health = enemy.GetComponent<Health>();

            if (health != null)
            {
                health.Died -= HandleEnemyDied;
            }

            EnemyRuntimeIdentity identity =
                enemy.GetComponent<EnemyRuntimeIdentity>();

            if (identity != null)
            {
                if (runRecord != null &&
                    runRecord.MarkSurvivedFloor(identity))
                {
                    lastClearSurvivorCount++;
                }

                identity.DestroyedUnexpectedly -=
                    HandleEnemyDestroyedUnexpectedly;
            }
        }

        activeEnemies.Clear();

        if (runRecord != null)
        {
            runRecord.FinalizeCurrentFloor();
        }
    }

    private void HandleEnemyDied(Health health)
    {
        if (health == null)
        {
            return;
        }

        deathNotificationCount++;

        EnemyRuntimeIdentity identity =
            health.GetComponent<EnemyRuntimeIdentity>();

        DamageAttribution attribution =
            health.HasLastAcceptedDamage
                ? health.LastAcceptedDamage.ResolvedAttribution
                : DamageAttribution.Other;

        lastDeathInstanceId = identity != null
            ? identity.InstanceId
            : health.gameObject.name;

        lastDeathAttribution = attribution;

        bool recorded = false;

        if (runRecord != null && identity != null)
        {
            recorded = runRecord.RegisterDeath(identity, attribution);
        }

        if (recorded)
        {
            if (attribution == DamageAttribution.Player)
            {
                recordedPlayerDeathCount++;
            }
            else
            {
                recordedOtherDeathCount++;
            }
        }
        else
        {
            duplicateDeathRecordCount++;
        }

        UnregisterEnemy(health.gameObject);
    }

    private void HandleEnemyDestroyedUnexpectedly(
        EnemyRuntimeIdentity identity)
    {
        if (identity == null)
        {
            return;
        }

        if (runRecord != null)
        {
            runRecord.MarkRemovedWithoutDeath(identity);
        }

        Health health = identity.GetComponent<Health>();

        if (health != null)
        {
            health.Died -= HandleEnemyDied;
        }

        identity.DestroyedUnexpectedly -=
            HandleEnemyDestroyedUnexpectedly;

        if (activeEnemies.Remove(identity.gameObject))
        {
            unregisterCount++;
        }

        unexpectedRemovalCount++;
    }

    private void ResetFloorAlertDiagnostics()
    {
        alertBroadcastCount = 0;
        alertDeliveryCount = 0;
        lastAlertSourceId = string.Empty;
        lastAlertPosition = Vector2.zero;
        lastAlertRadius = 0f;
    }

    private void RemoveDestroyedReferences()
    {
        activeEnemies.RemoveAll(
            enemy => enemy == null);
    }

    private void CacheComponents()
    {
        if (enemySpawner == null)
        {
            enemySpawner =
                GetComponent<EnemySpawner>();
        }
    }

    public void ResetRunRecord()
    {
        RemoveDestroyedReferences();

        if (activeEnemies.Count > 0)
        {
            Debug.LogWarning(
                "[EnemyManager/EA1] Run record was not reset because a floor " +
                "is active. Reset before generating the first floor.",
                this);

            return;
        }

        if (runRecord == null)
        {
            runRecord = new EnemyRunRecord();
        }

        runRecord.ResetRun();
        floorSessionSerial = 0;

        registeredEnemyCount = 0;
        deathNotificationCount = 0;
        recordedPlayerDeathCount = 0;
        recordedOtherDeathCount = 0;
        unregisterCount = 0;
        duplicateDeathRecordCount = 0;
        unexpectedRemovalCount = 0;
        floorSetupCount = 0;
        floorClearCount = 0;
        lastClearSurvivorCount = 0;
        lastDeathInstanceId = string.Empty;
        lastDeathAttribution = DamageAttribution.Unspecified;
        ResetFloorAlertDiagnostics();
    }
}
