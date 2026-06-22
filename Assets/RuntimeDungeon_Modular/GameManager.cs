using UnityEngine;

/// <summary>
/// 整體流程的協調者。
/// 它不生成房間細節，只負責呼叫各個組件。
///
/// 空場景用法：
/// 1. 建立 Empty GameObject。
/// 2. 只掛上 GameManager。
/// 3. 按 Play。
///
/// GameManager 會自動補上其餘五個組件。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    [Header("系統組件")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private DungeonRenderer dungeonRenderer;
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private ExitSpawner exitSpawner;
    [SerializeField] private CameraManager cameraManager;

    [Header("啟動")]
    [SerializeField] private bool generateOnStart = true;

    private Transform currentDungeonRoot;
    private DungeonLayout currentLayout;
    private bool isGenerating;

    public int CurrentFloor { get; private set; }
    public int CurrentSeed =>
        currentLayout != null ? currentLayout.Seed : 0;

    private void Reset()
    {
        CacheOrCreateComponents();
    }

    private void Awake()
    {
        CacheOrCreateComponents();
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

    /// <summary>
    /// 出口碰撞器會呼叫此方法。
    /// </summary>
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
        CacheOrCreateComponents();

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

    /// <summary>
    /// 只掛 GameManager 也能運作。
    /// 缺少的組件會自動加到同一個 GameObject。
    /// </summary>
    private void CacheOrCreateComponents()
    {
        dungeonGenerator = GetOrAdd(dungeonGenerator);
        dungeonRenderer = GetOrAdd(dungeonRenderer);
        playerSpawner = GetOrAdd(playerSpawner);
        exitSpawner = GetOrAdd(exitSpawner);
        cameraManager = GetOrAdd(cameraManager);
    }

    private T GetOrAdd<T>(T current)
        where T : Component
    {
        if (current != null)
        {
            return current;
        }

        T existing = GetComponent<T>();

        if (existing != null)
        {
            return existing;
        }

        return gameObject.AddComponent<T>();
    }

    private void OnGUI()
    {
        int roomCount =
            currentLayout != null
                ? currentLayout.Rooms.Count
                : 0;

        GUI.Box(new Rect(12f, 12f, 360f, 105f), "");

        GUI.Label(
            new Rect(24f, 22f, 330f, 24f),
            "WASD / 方向鍵：移動    R：重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 330f, 24f),
            "黃色方塊：進入下一個隨機迷宮");

        GUI.Label(
            new Rect(24f, 74f, 330f, 24f),
            "Floor: " + CurrentFloor +
            "    Rooms: " + roomCount +
            "    Seed: " + CurrentSeed);
    }
}
