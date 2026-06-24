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

    private readonly List<GameObject> activeEnemies =
        new List<GameObject>();

    public IReadOnlyList<GameObject> ActiveEnemies =>
        activeEnemies;

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

        List<GameObject> spawnedEnemies =
            enemySpawner.SpawnTestEnemies(
                layout,
                floorRoot,
                renderer,
                playerTransform);

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
        }

        activeEnemies.Clear();
    }

    private void HandleEnemyDied(Health health)
    {
        if (health == null)
        {
            return;
        }

        health.Died -= HandleEnemyDied;
        activeEnemies.Remove(health.gameObject);
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
}
