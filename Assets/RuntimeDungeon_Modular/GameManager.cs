using UnityEngine;

/// <summary>
/// 整體流程協調者。
/// RequireComponent 讓主要模組在編輯模式中直接出現在 Inspector。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DungeonGenerator))]
[RequireComponent(typeof(DungeonRenderer))]
[RequireComponent(typeof(PlayerSpawner))]
[RequireComponent(typeof(ExitSpawner))]
[RequireComponent(typeof(CameraManager))]
[RequireComponent(typeof(EnemySpawner))]
public sealed class GameManager : MonoBehaviour
{
    [Header("系統組件")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private DungeonRenderer dungeonRenderer;
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private ExitSpawner exitSpawner;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private EnemySpawner enemySpawner;

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

        isGenerating = true;
        CacheComponents();

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

        GameObject player = playerSpawner.Spawn(
            currentLayout.StartCell,
            currentDungeonRoot,
            dungeonRenderer);

        exitSpawner.Spawn(
            currentLayout.ExitCell,
            currentDungeonRoot,
            dungeonRenderer,
            this);

        enemySpawner.SpawnTestEnemies(
            currentLayout,
            currentDungeonRoot,
            dungeonRenderer,
            player.transform);

        cameraManager.SetTarget(player.transform);

        isGenerating = false;
    }

    private void RemoveCurrentFloor()
    {
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

        playerSpawner =
            GetComponent<PlayerSpawner>();

        exitSpawner =
            GetComponent<ExitSpawner>();

        cameraManager =
            GetComponent<CameraManager>();

        enemySpawner =
            GetComponent<EnemySpawner>();
    }

    private void OnGUI()
    {
        int roomCount =
            currentLayout != null
                ? currentLayout.Rooms.Count
                : 0;

        GUI.Box(new Rect(12f, 12f, 390f, 105f), "");

        GUI.Label(
            new Rect(24f, 22f, 360f, 24f),
            "WASD / 方向鍵：移動    R：按新參數重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 360f, 24f),
            "黃色方塊：進入下一個隨機迷宮");

        GUI.Label(
            new Rect(24f, 74f, 360f, 24f),
            "Floor: " + CurrentFloor +
            "    Rooms: " + roomCount +
            "    Seed: " + CurrentSeed);
    }
}
