using UnityEngine;

/// <summary>
/// 遊戲與樓層流程協調者。
///
/// 玩家由 PlayerManager 管理，只生成一次；
/// 敵人由 EnemyManager 管理，每層重新生成。
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
    [Tooltip("拖入 PlayerSystem 上的 PlayerManager。")]
    [SerializeField] private PlayerManager playerManager;

    [Header("獨立敵人系統")]
    [Tooltip("拖入 EnemySystem 上的 EnemyManager。")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("啟動")]
    [SerializeField] private bool generateOnStart = true;

    private Transform currentDungeonRoot;
    private DungeonLayout currentLayout;
    private bool isGenerating;

    public int CurrentFloor { get; private set; }

    public int CurrentSeed =>
        currentLayout != null
            ? currentLayout.Seed
            : 0;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();

        if (playerManager == null)
        {
            Debug.LogError(
                "GameManager：找不到 PlayerManager。" +
                "請建立子物件 PlayerSystem，掛上 PlayerManager。");
        }

        if (enemyManager == null)
        {
            Debug.LogError(
                "GameManager：找不到 EnemyManager。" +
                "請確認 EnemySystem 已正確設定。");
        }
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
            enemyManager == null)
        {
            Debug.LogError(
                "GameManager：PlayerManager 或 EnemyManager 尚未設定。");

            return;
        }

        isGenerating = true;

        RemoveCurrentFloor();

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

        // 玩家由 PlayerSystem 保存。
        // 第一次生成，之後只移動到新出生點。
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

        enemyManager.SetupFloor(
            currentLayout,
            currentDungeonRoot,
            dungeonRenderer,
            player);

        cameraManager.SetTarget(player);

        isGenerating = false;
    }

    private void RemoveCurrentFloor()
    {
        if (enemyManager != null)
        {
            enemyManager.ClearFloor();
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
    }

    private void OnGUI()
    {
        int roomCount =
            currentLayout != null
                ? currentLayout.Rooms.Count
                : 0;

        int enemyCount =
            enemyManager != null
                ? enemyManager.ActiveEnemyCount
                : 0;

        GUI.Box(new Rect(12f, 12f, 450f, 105f), "");

        GUI.Label(
            new Rect(24f, 22f, 420f, 24f),
            "WASD / 方向鍵：移動    R：重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 420f, 24f),
            "玩家跨樓層保留，黃色方塊進入下一個迷宮");

        GUI.Label(
            new Rect(24f, 74f, 420f, 24f),
            "Floor: " + CurrentFloor +
            "    Rooms: " + roomCount +
            "    Enemies: " + enemyCount +
            "    Seed: " + CurrentSeed);
    }
}
