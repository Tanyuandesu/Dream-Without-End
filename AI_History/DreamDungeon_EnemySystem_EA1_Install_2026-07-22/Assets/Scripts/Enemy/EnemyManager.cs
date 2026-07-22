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

    public IReadOnlyList<GameObject> ActiveEnemies =>
        activeEnemies;

    public EnemyRunRecord RunRecord => runRecord;

    public EnemyRunRecordSnapshot CurrentRunSnapshot =>
        runRecord != null
            ? runRecord.CreateSnapshot()
            : new EnemyRunRecordSnapshot();

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

        activeEnemies.Remove(enemy);
    }

    public void ClearFloor()
    {
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
                if (runRecord != null)
                {
                    runRecord.MarkSurvivedFloor(identity);
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

        EnemyRuntimeIdentity identity =
            health.GetComponent<EnemyRuntimeIdentity>();

        DamageAttribution attribution =
            health.HasLastAcceptedDamage
                ? health.LastAcceptedDamage.ResolvedAttribution
                : DamageAttribution.Other;

        if (runRecord != null && identity != null)
        {
            runRecord.RegisterDeath(identity, attribution);
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

        activeEnemies.Remove(identity.gameObject);
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
    }
}
