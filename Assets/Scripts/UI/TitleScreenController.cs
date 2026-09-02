using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SYS10 title integration.
/// Reuses the existing TitleScene buttons, binds them to the authoritative
/// flow/save/settings/localization systems, and builds the title Settings
/// overlay at runtime so the Scene does not need new serialized UI wiring.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleScreenController : MonoBehaviour
{
    [Header("主要按鈕")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("可選拓展按鈕")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("面板")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    private GameFlowManager flowManager;
    private SaveSystemManager saveSystem;
    private GameObject runtimeSettingsOverlay;
    private SettingsMenuController runtimeSettingsController;
    private Button runtimeSettingsBackButton;
    private bool listenersAdded;

    private void Awake()
    {
        flowManager = GameFlowManager.GetOrCreate();
        saveSystem = SaveSystemManager.GetOrCreate();

        ResolveExistingTitleButtons();
        BindTitleLocalization();
        EnsureRuntimeSettingsOverlay();
    }

    private void OnEnable()
    {
        AddListeners();

        if (flowManager != null)
        {
            flowManager.SceneLoadCompleted -= HandleSceneLoadCompleted;
            flowManager.SceneLoadCompleted += HandleSceneLoadCompleted;
        }

        ShowMainMenu();
    }

    private void Start()
    {
        SelectStartButton();
    }

    private void Update()
    {
        if (runtimeSettingsOverlay != null &&
            runtimeSettingsOverlay.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            ShowMainMenu();
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
        {
            flowManager.SceneLoadCompleted -= HandleSceneLoadCompleted;
        }

        RemoveListeners();
    }

    private void HandleSceneLoadCompleted(string sceneName)
    {
        if (flowManager == null ||
            sceneName != flowManager.TitleSceneName)
        {
            return;
        }

        // The TitleScene object's OnEnable can run while GameFlowManager is
        // still in its Loading state. Refresh once more after the persistent
        // flow manager has completed the scene transition, otherwise a valid
        // save can leave Continue disabled until the next Play Mode session.
        SetMainButtonsInteractable(true);
        RefreshContinueAvailability();
        SelectStartButton();

#if UNITY_EDITOR
        Debug.Log(
            "[SYS10.3] Title load completed | ValidSave=" +
            (saveSystem != null && saveSystem.HasValidSave()) +
            " | ContinueInteractable=" +
            (continueButton != null && continueButton.interactable),
            this);
#endif
    }

    public void StartGame()
    {
        if (flowManager == null || flowManager.IsLoading)
        {
            return;
        }

        SetMainButtonsInteractable(false);

        // SYS10 contract: starting a new run never deletes/overwrites the old
        // save. The old file changes only when the player explicitly saves.
        flowManager.StartNewGame();
    }

    public void ContinueGame()
    {
        if (flowManager == null || flowManager.IsLoading)
        {
            return;
        }

        if (saveSystem == null || !saveSystem.HasValidSave())
        {
            RefreshContinueAvailability();
            return;
        }

        SetMainButtonsInteractable(false);

        if (!flowManager.TryStartContinueGame())
        {
            SetMainButtonsInteractable(true);
            RefreshContinueAvailability();
        }
    }

    public void QuitGame()
    {
        flowManager?.QuitGame();
    }

    public void ShowMainMenu()
    {
        SetPanelActive(mainPanel, true);
        SetPanelActive(optionsPanel, false);
        SetPanelActive(creditsPanel, false);
        SetPanelActive(runtimeSettingsOverlay, false);

        runtimeSettingsController?.FlushPendingChanges();
        SetMainButtonsInteractable(true);
        RefreshContinueAvailability();
        SelectStartButton();
    }

    public void ShowOptions()
    {
        if (runtimeSettingsOverlay != null)
        {
            SetPanelActive(runtimeSettingsOverlay, true);
            runtimeSettingsController?.RefreshFromSettings();
            SelectButton(runtimeSettingsBackButton);
            return;
        }

        // Compatibility fallback if a handcrafted options panel is ever wired.
        SetPanelActive(mainPanel, false);
        SetPanelActive(optionsPanel, true);
        SetPanelActive(creditsPanel, false);
    }

    public void ShowCredits()
    {
        SetPanelActive(mainPanel, false);
        SetPanelActive(optionsPanel, false);
        SetPanelActive(runtimeSettingsOverlay, false);
        SetPanelActive(creditsPanel, true);
    }

    private void ResolveExistingTitleButtons()
    {
        Button[] buttons =
            FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];

            if (candidate == null)
            {
                continue;
            }

            string objectName = candidate.gameObject.name.Trim();

            if (startButton == null && objectName == "StartButton")
            {
                startButton = candidate;
            }
            else if (continueButton == null && objectName == "ContinueButton")
            {
                continueButton = candidate;
            }
            else if (optionsButton == null && objectName == "SettingButton")
            {
                optionsButton = candidate;
            }
            else if (quitButton == null && objectName == "QuitButton")
            {
                quitButton = candidate;
            }
        }
    }

    private void BindTitleLocalization()
    {
        BindLocalizedLabel(startButton, "UI_TITLE_START");
        BindLocalizedLabel(continueButton, "UI_TITLE_CONTINUE");
        BindLocalizedLabel(optionsButton, "UI_TITLE_SETTINGS");
        BindLocalizedLabel(quitButton, "UI_TITLE_QUIT");
    }

    private static void BindLocalizedLabel(
        Button button,
        string localizationKey)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI label =
            button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label == null)
        {
            return;
        }

        // SYS10.2: the original TitleScene typography was tuned for short
        // English labels (50px + very large character spacing). CJK strings
        // become oversized or wrap. Normalize only the title button labels;
        // the scene/button geometry itself stays untouched.
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 38f;
        label.characterSpacing = 1f;
        label.wordSpacing = 0f;
        label.lineSpacing = 0f;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.Center;

        LocalizedTMPText localized =
            label.GetComponent<LocalizedTMPText>();

        if (localized == null)
        {
            localized = label.gameObject.AddComponent<LocalizedTMPText>();
        }

        localized.SetKey(localizationKey);
    }

    private void EnsureRuntimeSettingsOverlay()
    {
        if (runtimeSettingsOverlay != null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning(
                "[SYS10] Title Settings overlay skipped: no Canvas found.",
                this);
            return;
        }

        runtimeSettingsOverlay =
            new GameObject(
                "TitleSettings_Runtime",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        runtimeSettingsOverlay.layer = 5;
        runtimeSettingsOverlay.transform.SetParent(
            canvas.transform,
            false);

        RectTransform overlayRect =
            runtimeSettingsOverlay.GetComponent<RectTransform>();
        StretchFull(overlayRect);

        Image overlayImage =
            runtimeSettingsOverlay.GetComponent<Image>();
        overlayImage.color =
            new Color(0.035f, 0.04f, 0.055f, 0.96f);
        overlayImage.raycastTarget = true;

        CreateLocalizedText(
            runtimeSettingsOverlay.transform,
            "SettingsTitle",
            "UI_TITLE_SETTINGS",
            40f,
            new Vector2(0f, 285f),
            new Vector2(700f, 70f));

        GameObject content =
            CreateRect(
                "SettingsContent",
                runtimeSettingsOverlay.transform);

        RectTransform contentRect =
            content.GetComponent<RectTransform>();
        contentRect.anchorMin =
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(900f, 470f);

        runtimeSettingsController =
            content.AddComponent<SettingsMenuController>();
        runtimeSettingsController.BuildRuntimeControls();

        runtimeSettingsBackButton =
            CreateLocalizedButton(
                runtimeSettingsOverlay.transform,
                "BackButton",
                "UI_PAUSE_BACK",
                new Vector2(0f, -300f),
                new Vector2(260f, 64f));

        runtimeSettingsOverlay.SetActive(false);
    }

    private void AddListeners()
    {
        if (listenersAdded)
        {
            return;
        }

        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(ShowOptions);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCredits);

        if (optionsBackButton != null)
            optionsBackButton.onClick.AddListener(ShowMainMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(ShowMainMenu);

        if (runtimeSettingsBackButton != null)
            runtimeSettingsBackButton.onClick.AddListener(ShowMainMenu);

        listenersAdded = true;
    }

    private void RemoveListeners()
    {
        if (!listenersAdded)
        {
            return;
        }

        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);

        if (continueButton != null)
            continueButton.onClick.RemoveListener(ContinueGame);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(ShowOptions);

        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(ShowCredits);

        if (optionsBackButton != null)
            optionsBackButton.onClick.RemoveListener(ShowMainMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(ShowMainMenu);

        if (runtimeSettingsBackButton != null)
            runtimeSettingsBackButton.onClick.RemoveListener(ShowMainMenu);

        listenersAdded = false;
    }

    private void RefreshContinueAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        bool canContinue =
            flowManager != null &&
            !flowManager.IsLoading &&
            saveSystem != null &&
            saveSystem.HasValidSave();

        continueButton.interactable = canContinue;
    }

    private void SetMainButtonsInteractable(bool interactable)
    {
        if (startButton != null)
            startButton.interactable = interactable;

        if (optionsButton != null)
            optionsButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;

        if (continueButton != null)
        {
            continueButton.interactable =
                interactable &&
                saveSystem != null &&
                saveSystem.HasValidSave();
        }
    }

    private void SelectStartButton()
    {
        SelectButton(startButton);
    }

    private static void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(
            button.gameObject);
    }

    private static Button CreateLocalizedButton(
        Transform parent,
        string name,
        string localizationKey,
        Vector2 position,
        Vector2 size)
    {
        GameObject buttonObject = CreateRect(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        CreateLocalizedText(
            buttonObject.transform,
            "Label",
            localizationKey,
            24f,
            Vector2.zero,
            Vector2.zero,
            true);

        return button;
    }

    private static void CreateLocalizedText(
        Transform parent,
        string name,
        string localizationKey,
        float fontSize,
        Vector2 position,
        Vector2 size,
        bool stretch = false)
    {
        GameObject labelObject = CreateRect(name, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();

        if (stretch)
        {
            StretchFull(rect);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        TextMeshProUGUI label =
            labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        LocalizedTMPText localized =
            labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(localizationKey);
    }

    private static GameObject CreateRect(
        string name,
        Transform parent)
    {
        GameObject obj =
            new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetPanelActive(
        GameObject panel,
        bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("SYS10 Debug/Print Title Integration State")]
    private void DebugPrintTitleIntegrationState()
    {
        Debug.Log(
            "[SYS10] Title integration" +
            " | Start=" + (startButton != null) +
            " | Continue=" + (continueButton != null) +
            " | Settings=" + (optionsButton != null) +
            " | Quit=" + (quitButton != null) +
            " | ValidSave=" +
            (saveSystem != null && saveSystem.HasValidSave()) +
            " | ContinueInteractable=" +
            (continueButton != null && continueButton.interactable) +
            " | RuntimeSettings=" +
            (runtimeSettingsOverlay != null),
            this);
    }
#endif
}
