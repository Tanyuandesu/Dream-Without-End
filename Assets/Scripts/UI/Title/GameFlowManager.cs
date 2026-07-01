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

    [Header("流程")]
    [SerializeField] private bool showCursorOnTitle = true;
    [SerializeField] private bool hideCursorDuringGame = false;

    private bool isLoading;

    public GameFlowState State { get; private set; }
    public bool IsLoading => isLoading;

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

        GameObject flowObject = new GameObject("AppFlow_Runtime");
        return flowObject.AddComponent<GameFlowManager>();
    }

    public void StartNewGame()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadSceneRoutine(gameSceneName, 0f));
        }
    }

    public void ReturnToTitle()
    {
        ReturnToTitleAfter(0f);
    }

    public void ReturnToTitleAfter(float delay)
    {
        if (!isLoading)
        {
            StartCoroutine(
                LoadSceneRoutine(
                    titleSceneName,
                    Mathf.Max(0f, delay)));
        }
    }

    public bool TryBeginGameOver()
    {
        if (isLoading ||
            State == GameFlowState.GameOver ||
            State == GameFlowState.Title)
        {
            return false;
        }

        SetState(GameFlowState.GameOver);
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
            Debug.LogError("GameFlowManager：場景名稱為空。");
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
                "GameFlowManager：無法載入場景 " + sceneName);
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
