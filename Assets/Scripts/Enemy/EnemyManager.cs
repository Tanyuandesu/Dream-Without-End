using System.Collections.Generic;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 管理當前樓層的所有敵人。
///
/// 現階段負責：
/// 1. 接收新樓層資料
/// 2. 呼叫 EnemySpawner 生成敵人
/// 3. 保存目前敵人清單
/// 4. 換層時清理引用
///
/// 它暫時不控制單隻敵人的移動、索敵或尋路。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySpawner))]
public sealed class EnemyManager : MonoBehaviour
{
    [Header("敵人系統")]
    [SerializeField] private EnemySpawner enemySpawner;
    private readonly List<GameObject> activeEnemies =
        new List<GameObject>();
    private DungeonLayout currentLayout;
    private Transform currentFloorRoot;
    private DungeonRenderer dungeonRenderer;
    private Transform player;
    public IReadOnlyList<GameObject> ActiveEnemies =>
        activeEnemies;
    public int ActiveEnemyCount =>
        activeEnemies.Count;
    private void Reset()
    {
        CacheComponents();
    }
    private void Awake()
    {
        CacheComponents();
    }
    /// <summary>
    /// 每次新迷宮建立完成後，由 GameManager 呼叫。
    /// </summary>
    public void SetupFloor(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform)
    {
        ClearFloor();
        if (layout == null)
        {
            Debug.LogError(
                "EnemyManager：DungeonLayout 為空。");
            return;
        }
        if (floorRoot == null)
        {
            Debug.LogError(
                "EnemyManager：FloorRoot 為空。");
            return;
        }
        if (renderer == null)
        {
            Debug.LogError(
                "EnemyManager：DungeonRenderer 為空。");
            return;
        }
        if (playerTransform == null)
        {
            Debug.LogError(
                "EnemyManager：玩家引用為空。");
            return;
        }
        CacheComponents();
        currentLayout = layout;
        currentFloorRoot = floorRoot;
        dungeonRenderer = renderer;
        player = playerTransform;
        List<GameObject> spawnedEnemies =
            enemySpawner.SpawnTestEnemies(
                currentLayout,
                currentFloorRoot,
                dungeonRenderer,
                player);
        activeEnemies.AddRange(spawnedEnemies);
    }
    /// <summary>
    /// 換層前清除 EnemyManager 保存的引用。
    ///
    /// 敵人本身是 FloorRoot 的子物件，
    /// GameManager 銷毀樓層時會一起銷毀。
    /// </summary>
    public void ClearFloor()
    {
        activeEnemies.Clear();
        currentLayout = null;
        currentFloorRoot = null;
        dungeonRenderer = null;
        player = null;
    }
    /// <summary>
    /// 以後敵人死亡時，可以呼叫此方法。
    /// </summary>
    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }
        activeEnemies.Remove(enemy);
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