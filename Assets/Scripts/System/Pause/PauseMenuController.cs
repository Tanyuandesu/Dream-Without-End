using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SYS5 pause/UI-navigation shell.
/// Creates a temporary runtime UI for the GameScene so navigation can be
/// validated before final visual assets and the concrete Settings/Items views
/// are implemented in SYS6/SYS7.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private enum PausePage
    {
        Main,
        Items,
        Settings,
        ReturnConfirm
    }

    private const int PauseCanvasSortOrder = 5000;

    private GameFlowManager flowManager;
    private GameObject overlayRoot;
    private GameObject mainPanel;
    private GameObject itemsPanel;
    private GameObject settingsPanel;
    private GameObject returnConfirmPanel;

    private Button resumeButton;
    private Button itemsButton;
    private Button settingsButton;
    private Button saveButton;
    private Button returnButton;
    private LocalizedTMPText saveStatusText;
    private Button itemsBackButton;
    private ItemViewerController itemViewerController;
    private Button settingsBackButton;
    private SettingsMenuController settingsMenuController;
    private Button confirmYesButton;
    private Button confirmNoButton;

    private PausePage currentPage = PausePage.Main;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        TryCreateForActiveScene();
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneLoaded += HandleSceneLoadedStatic;
    }

    private static void HandleSceneLoadedStatic(Scene scene, LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        GameFlowManager flow = GameFlowManager.GetOrCreate();
        if (flow == null)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != flow.GameSceneName)
        {
            return;
        }

        PauseMenuController existing = FindFirstObjectByType<PauseMenuController>();
        if (existing != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("PauseUI_Runtime");
        SceneManager.MoveGameObjectToScene(runtimeObject, activeScene);
        runtimeObject.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        flowManager = GameFlowManager.GetOrCreate();
        BuildRuntimeUi();
        HideOverlay();
    }

    private void OnEnable()
    {
        if (flowManager == null)
        {
            flowManager = GameFlowManager.GetOrCreate();
        }

        if (flowManager != null)
        {
            flowManager.StateChanged += HandleFlowStateChanged;
            HandleFlowStateChanged(flowManager.State);
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
        {
            flowManager.StateChanged -= HandleFlowStateChanged;
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || flowManager == null)
        {
            return;
        }

        if (flowManager.State == GameFlowState.Playing)
        {
            if (flowManager.TryPauseGame())
            {
                ShowPage(PausePage.Main);
            }

            return;
        }

        if (flowManager.State != GameFlowState.Paused)
        {
            return;
        }

        if (currentPage == PausePage.Main)
        {
            ResumeGame();
        }
        else
        {
            ShowPage(PausePage.Main);
        }
    }

    private void HandleFlowStateChanged(GameFlowState state)
    {
        if (state == GameFlowState.Paused)
        {
            if (saveStatusText != null)
            {
                saveStatusText.gameObject.SetActive(false);
            }

            ShowPage(PausePage.Main);
            return;
        }

        HideOverlay();
    }

    public void ResumeGame()
    {
        if (flowManager != null)
        {
            flowManager.TryResumeGame();
        }
    }

    public void ShowItems()
    {
        ShowPage(PausePage.Items);
    }

    public void ShowSettings()
    {
        ShowPage(PausePage.Settings);
    }

    public void ShowReturnConfirmation()
    {
        ShowPage(PausePage.ReturnConfirm);
    }

    public void SaveGame()
    {
        if (flowManager == null ||
            flowManager.State != GameFlowState.Paused)
        {
            return;
        }

        GameManager gameManager =
            FindFirstObjectByType<GameManager>();

        string error = string.Empty;
        bool success =
            gameManager != null &&
            gameManager.TrySaveCurrentRun(out error);

        if (!success && gameManager == null)
        {
            error = "GameManager is not available.";
        }

        if (saveStatusText != null)
        {
            saveStatusText.SetKey(
                success
                    ? "UI_SAVE_SUCCESS"
                    : "UI_SAVE_FAILED");
            saveStatusText.gameObject.SetActive(true);
        }

        if (!success)
        {
            Debug.LogWarning(
                "[SYS9] Pause save failed | " + error,
                this);
        }
    }

    public void ShowMain()
    {
        ShowPage(PausePage.Main);
    }

    public void ConfirmReturnToTitle()
    {
        if (flowManager == null || flowManager.State != GameFlowState.Paused)
        {
            return;
        }

        // Existing GameFlowManager already restores timeScale before scene load.
        // SYS11 will perform the final lifecycle hardening and regression pass.
        flowManager.ReturnToTitle();
    }

    private void ShowPage(PausePage page)
    {
        if (overlayRoot == null)
        {
            return;
        }

        currentPage = page;
        overlayRoot.SetActive(true);

        SetActive(mainPanel, page == PausePage.Main);
        SetActive(itemsPanel, page == PausePage.Items);
        SetActive(settingsPanel, page == PausePage.Settings);
        SetActive(returnConfirmPanel, page == PausePage.ReturnConfirm);

        if (page == PausePage.Settings && settingsMenuController != null)
        {
            settingsMenuController.RefreshFromSettings();
        }


        Button firstButton = page switch
        {
            PausePage.Main => resumeButton,
            PausePage.Items => itemViewerController != null && itemViewerController.FirstSelectableButton != null
                ? itemViewerController.FirstSelectableButton
                : itemsBackButton,
            PausePage.Settings => settingsBackButton,
            PausePage.ReturnConfirm => confirmNoButton,
            _ => null
        };

        Select(firstButton);
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void BuildRuntimeUi()
    {
        GameObject canvasObject = new GameObject(
            "PauseCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PauseCanvasSortOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot = CreateRect("Overlay", canvasObject.transform);
        StretchFull(overlayRoot.GetComponent<RectTransform>());

        Image backdrop = overlayRoot.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);

        mainPanel = CreatePanel("MainPanel", overlayRoot.transform, new Vector2(620f, 720f));
        CreateLocalizedLabel(mainPanel.transform, "UI_PAUSE_TITLE", 44, new Vector2(0f, 275f), new Vector2(520f, 70f));
        resumeButton = CreateButton(mainPanel.transform, "ResumeButton", "UI_PAUSE_RESUME", new Vector2(0f, 165f), ResumeGame);
        itemsButton = CreateButton(mainPanel.transform, "ItemsButton", "UI_PAUSE_ITEMS", new Vector2(0f, 85f), ShowItems);
        settingsButton = CreateButton(mainPanel.transform, "SettingsButton", "UI_PAUSE_SETTINGS", new Vector2(0f, 5f), ShowSettings);
        saveButton = CreateButton(mainPanel.transform, "SaveButton", "UI_PAUSE_SAVE", new Vector2(0f, -75f), SaveGame);
        returnButton = CreateButton(mainPanel.transform, "ReturnButton", "UI_PAUSE_RETURN_TITLE", new Vector2(0f, -155f), ShowReturnConfirmation);
        saveStatusText = CreateLocalizedStatusLabel(
            mainPanel.transform,
            "SaveStatus",
            new Vector2(0f, -245f),
            new Vector2(520f, 48f));
        saveStatusText.gameObject.SetActive(false);

        itemsPanel = CreatePanel("ItemsPanel", overlayRoot.transform, new Vector2(1160f, 700f));
        CreateLocalizedLabel(itemsPanel.transform, "UI_PAUSE_ITEMS", 42, new Vector2(0f, 285f), new Vector2(1020f, 70f));
        itemViewerController = itemsPanel.AddComponent<ItemViewerController>();
        itemViewerController.BuildRuntimeControls();
        itemsBackButton = CreateButton(itemsPanel.transform, "ItemsBackButton", "UI_PAUSE_BACK", new Vector2(0f, -290f), ShowMain, new Vector2(420f, 64f));

        settingsPanel = CreatePanel("SettingsPanel", overlayRoot.transform, new Vector2(980f, 620f));
        CreateLocalizedLabel(settingsPanel.transform, "UI_PAUSE_SETTINGS", 42, new Vector2(0f, 230f), new Vector2(820f, 70f));
        settingsMenuController = settingsPanel.AddComponent<SettingsMenuController>();
        settingsMenuController.BuildRuntimeControls();
        settingsBackButton = CreateButton(settingsPanel.transform, "SettingsBackButton", "UI_PAUSE_BACK", new Vector2(0f, -230f), ShowMain);

        returnConfirmPanel = CreatePanel("ReturnConfirmPanel", overlayRoot.transform, new Vector2(760f, 430f));
        CreateLocalizedLabel(returnConfirmPanel.transform, "UI_PAUSE_RETURN_CONFIRM", 30, new Vector2(0f, 85f), new Vector2(640f, 130f));
        confirmYesButton = CreateButton(returnConfirmPanel.transform, "ConfirmYesButton", "UI_CONFIRM_YES", new Vector2(-145f, -105f), ConfirmReturnToTitle, new Vector2(240f, 64f));
        confirmNoButton = CreateButton(returnConfirmPanel.transform, "ConfirmNoButton", "UI_CONFIRM_NO", new Vector2(145f, -105f), ShowMain, new Vector2(240f, 64f));
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 size)
    {
        GameObject panel = CreateRect(name, parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.17f, 0.96f);
        return panel;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string localizationKey,
        Vector2 position,
        UnityEngine.Events.UnityAction onClick,
        Vector2? size = null)
    {
        GameObject buttonObject = CreateRect(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size ?? new Vector2(420f, 64f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        GameObject labelObject = CreateRect("Label", buttonObject.transform);
        StretchFull(labelObject.GetComponent<RectTransform>());

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        label.raycastTarget = false;

        LocalizedTMPText localized = labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(localizationKey);
        return button;
    }

    private static void CreateLocalizedLabel(
        Transform parent,
        string localizationKey,
        float fontSize,
        Vector2 position,
        Vector2 size)
    {
        GameObject labelObject = CreateRect(localizationKey, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        LocalizedTMPText localized = labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(localizationKey);
    }

    private static LocalizedTMPText CreateLocalizedStatusLabel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        GameObject labelObject = CreateRect(name, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        return labelObject.AddComponent<LocalizedTMPText>();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }

    private static void Select(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
