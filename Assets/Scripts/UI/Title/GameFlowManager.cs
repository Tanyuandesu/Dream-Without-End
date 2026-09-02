using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("場景名稱")]
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string endingSceneName = "EndingScene";

    [Header("流程")]
    [SerializeField] private bool showCursorOnTitle = true;
    [SerializeField] private bool showCursorOnEnding = true;
    [SerializeField] private bool hideCursorDuringGame = false;

    private bool isLoading;

    public GameFlowState State { get; private set; }
    public bool IsLoading => isLoading;

    /// <summary>
    /// SYS1 authoritative gameplay-input gate. Runtime gameplay input is
    /// accepted only while the application flow is actively Playing.
    /// When no flow manager exists, input remains allowed for isolated
    /// component/test scenes that intentionally do not use the app flow.
    /// </summary>
    public bool IsGameplayInputAllowed =>
        !isLoading && State == GameFlowState.Playing;

    public static bool AllowsGameplayInput =>
        Instance == null || Instance.IsGameplayInputAllowed;

    public string TitleSceneName => titleSceneName;
    public string GameSceneName => gameSceneName;
    public string EndingSceneName => endingSceneName;

    public event Action<GameFlowState> StateChanged;
    public event Action<string> SceneLoadStarted;
    public event Action<string> SceneLoadCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        UpdateStateFromScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    public static GameFlowManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject flowObject =
            new GameObject("AppFlow_Runtime");

        return flowObject.AddComponent<GameFlowManager>();
    }

    public void StartNewGame()
    {
        if (isLoading)
        {
            return;
        }

        RunLaunchContext.RequestNewGame();
        LoadGameAfter(0f);
    }

    /// <summary>
    /// SYS8 Continue entry. It validates/loads the save before GameScene is
    /// requested, then queues a detached launch snapshot for SYS9 to consume.
    /// Title UI wiring remains intentionally deferred to SYS10.
    /// </summary>
    public bool TryStartContinueGame()
    {
        if (isLoading)
        {
            return false;
        }

        SaveSystemManager saveSystem =
            SaveSystemManager.GetOrCreate();

        if (!saveSystem.TryLoadSave(
                out SaveGameData data,
                out string error))
        {
            Debug.LogWarning(
                "[SYS8] Continue rejected | " + error,
                this);
            return false;
        }

        RunLaunchContext.RequestContinue(data);
        LoadGameAfter(0f);
        return true;
    }

    /// <summary>
    /// Enters the global paused state. SYS5 will bind UI/ESC controls to
    /// this API; SYS1 only establishes the authoritative state boundary.
    /// </summary>
    public bool TryPauseGame()
    {
        if (isLoading || State != GameFlowState.Playing)
        {
            return false;
        }

        SetState(GameFlowState.Paused);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        return true;
    }

    public bool TryResumeGame()
    {
        if (isLoading || State != GameFlowState.Paused)
        {
            return false;
        }

        Time.timeScale = 1f;
        SetState(GameFlowState.Playing);
        ApplyGameplayCursorState();
        return true;
    }

    /// <summary>
    /// Reserves one authoritative dialogue state for the later NPC/dialogue
    /// phase. Dialogue does not alter timeScale here; its world-freeze policy
    /// remains a dialogue-system decision while gameplay input is still gated.
    /// </summary>
    public bool TryBeginDialogue()
    {
        if (isLoading || State != GameFlowState.Playing)
        {
            return false;
        }

        SetState(GameFlowState.Dialogue);
        return true;
    }

    public bool TryEndDialogue()
    {
        if (isLoading || State != GameFlowState.Dialogue)
        {
            return false;
        }

        SetState(GameFlowState.Playing);
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("SYS8 Debug/Queue New Game Request (No Load)")]
    private void DebugQueueNewGameRequest()
    {
        RunLaunchContext.RequestNewGame();
        DebugPrintLaunchRequest();
    }

    [ContextMenu("SYS8 Debug/Queue Continue Request From Save (No Load)")]
    private void DebugQueueContinueRequest()
    {
        SaveSystemManager saveSystem =
            SaveSystemManager.GetOrCreate();

        if (!saveSystem.TryLoadSave(
                out SaveGameData data,
                out string error))
        {
            Debug.LogWarning(
                "[SYS8] Cannot queue Continue | " + error,
                this);
            return;
        }

        RunLaunchContext.RequestContinue(data);
        DebugPrintLaunchRequest();
    }

    [ContextMenu("SYS9 Debug/Start Continue From Save")]
    private void DebugStartContinueFromSave()
    {
        bool success = TryStartContinueGame();

        Debug.Log(
            "[SYS9] Debug start Continue=" + success,
            this);
    }

    [ContextMenu("SYS8 Debug/Print Pending Launch Request")]
    private void DebugPrintLaunchRequest()
    {
        if (!RunLaunchContext.TryPeek(
                out RunLaunchRequest request))
        {
            Debug.Log(
                "[SYS8] Launch request | Mode=None",
                this);
            return;
        }

        SaveGameData data = request.SaveData;

        Debug.Log(
            "[SYS8] Launch request | Mode=" + request.Mode +
            (data == null
                ? string.Empty
                : " | Floor=" + data.floorIndex +
                  " | HP=" + data.currentHP.ToString("0.##") +
                  " | Items=" + data.collectedItemIds.Count +
                  " | Kills=" + data.killCount),
            this);
    }

    // Editor-only dialogue gate probes remain useful until the future NPC/dialogue
    // system provides its own authoring and regression surface.
    [ContextMenu("SYS1 Debug/Begin Dialogue Gate")]
    private void DebugBeginDialogue()
    {
        Debug.Log(
            "[SYS1] Dialogue begin request: " + TryBeginDialogue() +
            " | State=" + State +
            " | timeScale=" + Time.timeScale,
            this);
    }

    [ContextMenu("SYS1 Debug/End Dialogue Gate")]
    private void DebugEndDialogue()
    {
        Debug.Log(
            "[SYS1] Dialogue end request: " + TryEndDialogue() +
            " | State=" + State +
            " | timeScale=" + Time.timeScale,
            this);
    }

    [ContextMenu("SYS11 Debug/Print Lifecycle Audit")]
    private void DebugPrintLifecycleAudit()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        int flowCount =
            FindObjectsByType<GameFlowManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int settingsCount =
            FindObjectsByType<SystemSettingsManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int localizationCount =
            FindObjectsByType<LocalizationManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int audioCount =
            FindObjectsByType<AudioManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int saveCount =
            FindObjectsByType<SaveSystemManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int pauseCount =
            FindObjectsByType<PauseMenuController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int gameManagerCount =
            FindObjectsByType<GameManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;
        int titleControllerCount =
            FindObjectsByType<TitleScreenController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID).Length;

        bool titleSceneClean =
            activeScene.name != titleSceneName ||
            (pauseCount == 0 && gameManagerCount == 0);

        bool gameSceneClean =
            activeScene.name != gameSceneName ||
            (pauseCount == 1 && gameManagerCount == 1);

        bool persistentSingletonsClean =
            flowCount == 1 &&
            settingsCount == 1 &&
            localizationCount == 1 &&
            audioCount == 1 &&
            saveCount == 1;

        bool timeScaleClean =
            State == GameFlowState.Paused
                ? Mathf.Approximately(Time.timeScale, 0f)
                : Mathf.Approximately(Time.timeScale, 1f);

        bool launchCleanOnTitle =
            activeScene.name != titleSceneName ||
            !RunLaunchContext.HasPendingRequest;

        bool overall =
            titleSceneClean &&
            gameSceneClean &&
            persistentSingletonsClean &&
            timeScaleClean &&
            launchCleanOnTitle;

        Debug.Log(
            "[SYS11] Lifecycle audit | PASS=" + overall +
            " | Scene=" + activeScene.name +
            " | State=" + State +
            " | Loading=" + isLoading +
            " | TimeScale=" + Time.timeScale.ToString("0.##") +
            " | CursorVisible=" + Cursor.visible +
            " | CursorLock=" + Cursor.lockState +
            " | PendingLaunch=" + RunLaunchContext.PendingMode +
            "\nPersistentCounts" +
            " | Flow=" + flowCount +
            " | Settings=" + settingsCount +
            " | Localization=" + localizationCount +
            " | Audio=" + audioCount +
            " | Save=" + saveCount +
            "\nSceneCounts" +
            " | PauseUI=" + pauseCount +
            " | GameManager=" + gameManagerCount +
            " | TitleController=" + titleControllerCount +
            "\nChecks" +
            " | PersistentSingletons=" + persistentSingletonsClean +
            " | TitleSceneClean=" + titleSceneClean +
            " | GameSceneClean=" + gameSceneClean +
            " | TimeScaleClean=" + timeScaleClean +
            " | LaunchCleanOnTitle=" + launchCleanOnTitle,
            this);
    }
#endif

    public void LoadGameAfter(float delay)
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(
            LoadSceneRoutine(
                gameSceneName,
                Mathf.Max(0f, delay)));
    }

    public void ReturnToTitle()
    {
        ReturnToTitleAfter(0f);
    }

    public void ReturnToTitleAfter(float delay)
    {
        if (isLoading)
        {
            return;
        }

        RunLaunchContext.Clear();

        StartCoroutine(
            LoadSceneRoutine(
                titleSceneName,
                Mathf.Max(0f, delay)));
    }

    public void LoadEnding()
    {
        LoadEndingAfter(0f);
    }

    public void LoadEndingAfter(float delay)
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(
            LoadSceneRoutine(
                endingSceneName,
                Mathf.Max(0f, delay)));
    }

    public bool TryBeginGameOver()
    {
        if (isLoading ||
            State == GameFlowState.GameOver ||
            State == GameFlowState.Victory ||
            State == GameFlowState.Title ||
            State == GameFlowState.Ending)
        {
            return false;
        }

        SetState(GameFlowState.GameOver);
        return true;
    }

    public bool TryBeginVictory()
    {
        if (isLoading ||
            State == GameFlowState.Victory ||
            State == GameFlowState.GameOver ||
            State == GameFlowState.Title ||
            State == GameFlowState.Ending)
        {
            return false;
        }

        SetState(GameFlowState.Victory);
        return true;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadSceneRoutine(
        string sceneName,
        float delay)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "GameFlowManager：場景名稱為空。");

            yield break;
        }

        isLoading = true;
        SetState(GameFlowState.Loading);
        SceneLoadStarted?.Invoke(sceneName);

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        Time.timeScale = 1f;

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError(
                "GameFlowManager：無法載入場景 " +
                sceneName);

            isLoading = false;
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        isLoading = false;
        UpdateStateFromScene(scene.name);
        SceneLoadCompleted?.Invoke(scene.name);
    }

    private void UpdateStateFromScene(string sceneName)
    {
        if (sceneName == titleSceneName)
        {
            RunLaunchContext.Clear();
            SetState(GameFlowState.Title);
            Time.timeScale = 1f;

            if (showCursorOnTitle)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            return;
        }

        if (sceneName == gameSceneName)
        {
            SetState(GameFlowState.Playing);
            ApplyGameplayCursorState();
            return;
        }

        if (sceneName == endingSceneName)
        {
            SetState(GameFlowState.Ending);
            Time.timeScale = 1f;

            if (showCursorOnEnding)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            return;
        }

        SetState(GameFlowState.Loading);
    }

    private void ApplyGameplayCursorState()
    {
        if (hideCursorDuringGame)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void SetState(GameFlowState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;
        StateChanged?.Invoke(State);
    }
}
