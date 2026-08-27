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
        LoadGameAfter(0f);
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
    // SYS1 verification hooks. These do not ship in player builds and let the
    // current stage be tested before the real Pause UI exists in SYS5.
    [ContextMenu("SYS1 Debug/Pause Game")]
    private void DebugPauseGame()
    {
        Debug.Log(
            "[SYS1] Pause request: " + TryPauseGame() +
            " | State=" + State +
            " | timeScale=" + Time.timeScale,
            this);
    }

    [ContextMenu("SYS1 Debug/Resume Game")]
    private void DebugResumeGame()
    {
        Debug.Log(
            "[SYS1] Resume request: " + TryResumeGame() +
            " | State=" + State +
            " | timeScale=" + Time.timeScale,
            this);
    }

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

            if (hideCursorDuringGame)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

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
