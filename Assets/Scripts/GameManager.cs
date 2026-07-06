using UnityEngine;

/// <summary>
/// 遊戲與樓層流程協調者。
///
/// 玩家跨樓層保留；
/// 敵人與道具由各自系統管理；
/// 建立下一層前會廣播 RunProgressionContext。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DungeonGenerator))]
[RequireComponent(typeof(DungeonRenderer))]
[RequireComponent(typeof(ExitSpawner))]
[RequireComponent(typeof(CameraManager))]
public sealed class GameManager : MonoBehaviour
{
    [Header("地圖與流程組件")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private DungeonRenderer dungeonRenderer;
    [SerializeField] private ExitSpawner exitSpawner;
    [SerializeField] private CameraManager cameraManager;

    [Header("獨立角色系統")]
    [SerializeField] private PlayerManager playerManager;

    [Header("獨立敵人系統")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("獨立道具系統")]
    [SerializeField] private ItemManager itemManager;

    [Header("啟動")]
    [SerializeField] private bool generateOnStart = true;

    [Header("除錯")]
    [SerializeField] private bool showDebugOverlay = true;

    private Transform currentDungeonRoot;
    private DungeonLayout currentLayout;
    private bool isGenerating;

    public int CurrentFloor { get; private set; }

    public int CurrentSeed =>
        currentLayout != null
            ? currentLayout.Seed
            : 0;

    public ItemProgressSnapshot CurrentItemProgress =>
        itemManager != null
            ? itemManager.CreateProgressSnapshot()
            : new ItemProgressSnapshot(null, 0, -1);

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateNextFloor();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateCurrentFloor();
        }
    }

    public void PlayerReachedExit()
    {
        GenerateNextFloor();
    }

    public void GenerateNextFloor()
    {
        CurrentFloor++;
        GenerateFloor();
    }

    public void RegenerateCurrentFloor()
    {
        if (CurrentFloor <= 0)
        {
            CurrentFloor = 1;
        }

        GenerateFloor();
    }

    private void GenerateFloor()
    {
        if (isGenerating)
        {
            return;
        }

        CacheComponents();

        if (playerManager == null ||
            enemyManager == null ||
            itemManager == null)
        {
            Debug.LogError(
                "GameManager：PlayerManager、EnemyManager 或 ItemManager 未設定。");

            return;
        }

        isGenerating = true;

        RemoveCurrentFloor();

        RunProgressionContext progressionContext =
            new RunProgressionContext(
                CurrentFloor,
                itemManager.CreateProgressSnapshot());

        BroadcastRunProgression(
            progressionContext);

        currentLayout =
            dungeonGenerator.Generate(CurrentFloor);

        currentDungeonRoot =
            new GameObject(
                "GeneratedDungeon_Floor_" + CurrentFloor)
            .transform;

        currentDungeonRoot.SetParent(transform);
        currentDungeonRoot.localPosition = Vector3.zero;

        dungeonRenderer.Render(
            currentLayout,
            currentDungeonRoot);

        Transform player =
            playerManager.PlacePlayer(
                currentLayout.StartCell,
                dungeonRenderer);

        if (player == null)
        {
            Debug.LogError(
                "GameManager：玩家建立失敗。");

            isGenerating = false;
            return;
        }

        exitSpawner.Spawn(
            currentLayout.ExitCell,
            currentDungeonRoot,
            dungeonRenderer,
            this);

        itemManager.SetupFloor(
            CurrentFloor,
            currentLayout,
            currentDungeonRoot,
            dungeonRenderer);

        enemyManager.SetupFloor(
            currentLayout,
            currentDungeonRoot,
            dungeonRenderer,
            player);

        cameraManager.SetTarget(player);

        isGenerating = false;
    }

    private void BroadcastRunProgression(
        RunProgressionContext context)
    {
        MonoBehaviour[] behaviours =
            GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            IRunProgressionConsumer consumer =
                behaviours[i] as IRunProgressionConsumer;

            if (consumer != null)
            {
                consumer.ApplyRunProgression(context);
            }
        }
    }

    private void RemoveCurrentFloor()
    {
        if (enemyManager != null)
        {
            enemyManager.ClearFloor();
        }

        if (itemManager != null)
        {
            itemManager.ClearFloor();
        }

        if (currentDungeonRoot == null)
        {
            return;
        }

        currentDungeonRoot.gameObject.SetActive(false);
        Destroy(currentDungeonRoot.gameObject);
        currentDungeonRoot = null;
    }

    private void CacheComponents()
    {
        dungeonGenerator =
            GetComponent<DungeonGenerator>();

        dungeonRenderer =
            GetComponent<DungeonRenderer>();

        exitSpawner =
            GetComponent<ExitSpawner>();

        cameraManager =
            GetComponent<CameraManager>();

        if (playerManager == null)
        {
            playerManager =
                GetComponentInChildren<PlayerManager>(true);
        }

        if (enemyManager == null)
        {
            enemyManager =
                GetComponentInChildren<EnemyManager>(true);
        }

        if (itemManager == null)
        {
            itemManager =
                GetComponentInChildren<ItemManager>(true);
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        int roomCount =
            currentLayout != null
                ? currentLayout.Rooms.Count
                : 0;

        int enemyCount =
            enemyManager != null
                ? enemyManager.ActiveEnemyCount
                : 0;

        int itemCount =
            itemManager != null
                ? itemManager.CollectedItemCount
                : 0;

        float nextItemChance =
            itemManager != null
                ? itemManager.GetSpawnChanceForFloor(
                    CurrentFloor + 1)
                : 0f;

        GUI.Box(
            new Rect(12f, 12f, 570f, 130f),
            "");

        GUI.Label(
            new Rect(24f, 22f, 540f, 24f),
            "WASD / 方向鍵：移動    R：重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 540f, 24f),
            "黃色方塊：下一層    核心道具：碰觸拾取");

        GUI.Label(
            new Rect(24f, 74f, 540f, 24f),
            "Floor: " + CurrentFloor +
            "    Rooms: " + roomCount +
            "    Enemies: " + enemyCount +
            "    Seed: " + CurrentSeed);

        GUI.Label(
            new Rect(24f, 100f, 540f, 24f),
            "Items: " + itemCount +
            "    Progression Score: " +
            (itemManager != null
                ? itemManager.ProgressionScore
                : 0) +
            "    Next Chance: " +
            Mathf.RoundToInt(nextItemChance * 100f) +
            "%");
    }
}
