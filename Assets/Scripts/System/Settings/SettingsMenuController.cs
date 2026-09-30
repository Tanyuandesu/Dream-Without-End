using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsMenuController : MonoBehaviour
{
    private SystemSettingsManager settings;
    private Slider masterSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private TextMeshProUGUI masterValueLabel;
    private TextMeshProUGUI bgmValueLabel;
    private TextMeshProUGUI sfxValueLabel;
    private Button englishButton;
    private Button japaneseButton;
    private Button traditionalChineseButton;
    private Button normalVisualButton;
    private Button prototypeVisualButton;
    private bool built;

    private void Awake()
    {
        settings = SystemSettingsManager.GetOrCreate();
    }

    private void OnEnable()
    {
        settings = settings != null ? settings : SystemSettingsManager.GetOrCreate();
        Subscribe();
        RefreshFromSettings();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (settings != null)
        {
            settings.Flush();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void BuildRuntimeControls()
    {
        if (built)
        {
            RefreshFromSettings();
            return;
        }

        built = true;

        CreateLocalizedLabel(transform, "UI_SETTINGS_MASTER_VOLUME", 26f,
            new Vector2(-250f, 190f), new Vector2(260f, 54f), TextAlignmentOptions.MidlineLeft);
        masterSlider = CreateSlider(transform, "MasterVolumeSlider", new Vector2(70f, 190f));
        masterValueLabel = CreateValueLabel(transform, "MasterVolumeValue", new Vector2(300f, 190f));

        CreateLocalizedLabel(transform, "UI_SETTINGS_BGM_VOLUME", 26f,
            new Vector2(-250f, 115f), new Vector2(260f, 54f), TextAlignmentOptions.MidlineLeft);
        bgmSlider = CreateSlider(transform, "BgmVolumeSlider", new Vector2(70f, 115f));
        bgmValueLabel = CreateValueLabel(transform, "BgmVolumeValue", new Vector2(300f, 115f));

        CreateLocalizedLabel(transform, "UI_SETTINGS_SFX_VOLUME", 26f,
            new Vector2(-250f, 40f), new Vector2(260f, 54f), TextAlignmentOptions.MidlineLeft);
        sfxSlider = CreateSlider(transform, "SfxVolumeSlider", new Vector2(70f, 40f));
        sfxValueLabel = CreateValueLabel(transform, "SfxVolumeValue", new Vector2(300f, 40f));

        CreateLocalizedLabel(transform, "UI_SETTINGS_LANGUAGE", 26f,
            new Vector2(-250f, -55f), new Vector2(260f, 54f), TextAlignmentOptions.MidlineLeft);

        englishButton = CreateLanguageButton(transform, "EnglishButton", "UI_LANGUAGE_ENGLISH",
            GameLanguage.English, new Vector2(-55f, -55f));
        japaneseButton = CreateLanguageButton(transform, "JapaneseButton", "UI_LANGUAGE_JAPANESE",
            GameLanguage.Japanese, new Vector2(145f, -55f));
        traditionalChineseButton = CreateLanguageButton(transform, "TraditionalChineseButton",
            "UI_LANGUAGE_TRADITIONAL_CHINESE", GameLanguage.TraditionalChinese, new Vector2(345f, -55f));

        CreateLocalizedLabel(transform, "UI_SETTINGS_VISUAL_MODE", 26f,
            new Vector2(-250f, -140f), new Vector2(260f, 54f), TextAlignmentOptions.MidlineLeft);
        normalVisualButton = CreateVisualModeButton(transform, "VisualNormalButton",
            "UI_VISUAL_NORMAL", GameVisualMode.Normal, new Vector2(20f, -140f));
        prototypeVisualButton = CreateVisualModeButton(transform, "VisualPrototypeButton",
            "UI_VISUAL_PROTOTYPE", GameVisualMode.Prototype, new Vector2(260f, -140f));

        masterSlider.onValueChanged.AddListener(HandleMasterSliderChanged);
        bgmSlider.onValueChanged.AddListener(HandleBgmSliderChanged);
        sfxSlider.onValueChanged.AddListener(HandleSfxSliderChanged);

        RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        if (!built)
        {
            return;
        }

        settings = settings != null ? settings : SystemSettingsManager.GetOrCreate();
        if (settings == null)
        {
            return;
        }

        masterSlider?.SetValueWithoutNotify(settings.MasterVolume);
        bgmSlider?.SetValueWithoutNotify(settings.BgmVolume);
        sfxSlider?.SetValueWithoutNotify(settings.SfxVolume);
        RefreshMasterValue(settings.MasterVolume);
        RefreshBgmValue(settings.BgmVolume);
        RefreshSfxValue(settings.SfxVolume);
        RefreshLanguageButtons(settings.Language);
        RefreshVisualModeButtons(settings.VisualMode);
    }

    public void FlushPendingChanges()
    {
        settings?.Flush();
    }

    private void Subscribe()
    {
        if (settings == null)
        {
            return;
        }

        Unsubscribe();
        settings.MasterVolumeChanged += HandleMasterVolumeChanged;
        settings.BgmVolumeChanged += HandleBgmVolumeChanged;
        settings.SfxVolumeChanged += HandleSfxVolumeChanged;
        settings.LanguageChanged += HandleLanguageChanged;
        settings.VisualModeChanged += HandleVisualModeChanged;
    }

    private void Unsubscribe()
    {
        if (settings == null)
        {
            return;
        }

        settings.MasterVolumeChanged -= HandleMasterVolumeChanged;
        settings.BgmVolumeChanged -= HandleBgmVolumeChanged;
        settings.SfxVolumeChanged -= HandleSfxVolumeChanged;
        settings.LanguageChanged -= HandleLanguageChanged;
        settings.VisualModeChanged -= HandleVisualModeChanged;
    }

    private void HandleMasterSliderChanged(float value)
    {
        settings?.SetMasterVolume(value, false);
    }

    private void HandleBgmSliderChanged(float value)
    {
        settings?.SetBgmVolume(value, false);
    }

    private void HandleSfxSliderChanged(float value)
    {
        settings?.SetSfxVolume(value, false);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        masterSlider?.SetValueWithoutNotify(value);
        RefreshMasterValue(value);
    }

    private void HandleBgmVolumeChanged(float value)
    {
        bgmSlider?.SetValueWithoutNotify(value);
        RefreshBgmValue(value);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        sfxSlider?.SetValueWithoutNotify(value);
        RefreshSfxValue(value);
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        RefreshLanguageButtons(language);
    }

    private void HandleVisualModeChanged(GameVisualMode mode)
    {
        RefreshVisualModeButtons(mode);
    }

    private void SetVisualMode(GameVisualMode mode)
    {
        settings?.SetVisualMode(mode, true);
    }

    private void RefreshVisualModeButtons(GameVisualMode mode)
    {
        if (normalVisualButton != null)
            normalVisualButton.interactable = mode != GameVisualMode.Normal;
        if (prototypeVisualButton != null)
            prototypeVisualButton.interactable = mode != GameVisualMode.Prototype;
    }

    private void SetLanguage(GameLanguage language)
    {
        settings?.SetLanguage(language, true);
    }

    private void RefreshMasterValue(float value)
    {
        if (masterValueLabel != null)
        {
            masterValueLabel.text = FormatPercent(value);
        }
    }

    private void RefreshBgmValue(float value)
    {
        if (bgmValueLabel != null)
        {
            bgmValueLabel.text = FormatPercent(value);
        }
    }

    private void RefreshSfxValue(float value)
    {
        if (sfxValueLabel != null)
        {
            sfxValueLabel.text = FormatPercent(value);
        }
    }

    private void RefreshLanguageButtons(GameLanguage language)
    {
        if (englishButton != null) englishButton.interactable = language != GameLanguage.English;
        if (japaneseButton != null) japaneseButton.interactable = language != GameLanguage.Japanese;
        if (traditionalChineseButton != null)
            traditionalChineseButton.interactable = language != GameLanguage.TraditionalChinese;
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private Button CreateLanguageButton(Transform parent, string name, string localizationKey,
        GameLanguage language, Vector2 position)
    {
        Button button = CreateButton(parent, name, localizationKey, position, new Vector2(180f, 56f));
        button.onClick.AddListener(() => SetLanguage(language));
        return button;
    }

    private Button CreateVisualModeButton(Transform parent, string name,
        string localizationKey, GameVisualMode mode, Vector2 position)
    {
        Button button = CreateButton(parent, name, localizationKey, position,
            new Vector2(210f, 56f));
        button.onClick.AddListener(() => SetVisualMode(mode));
        return button;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 position)
    {
        GameObject root = CreateRect(name, parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = new Vector2(320f, 42f);

        Slider slider = root.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject background = CreateRect("Background", root.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.offsetMin = new Vector2(0f, -6f);
        backgroundRect.offsetMax = new Vector2(0f, 6f);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.25f, 0.27f, 0.30f, 1f);

        GameObject fillArea = CreateRect("Fill Area", root.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(6f, -6f);
        fillAreaRect.offsetMax = new Vector2(-6f, 6f);

        GameObject fill = CreateRect("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.72f, 0.78f, 0.82f, 1f);

        GameObject handleArea = CreateRect("Handle Slide Area", root.transform);
        StretchFull(handleArea.GetComponent<RectTransform>());

        GameObject handle = CreateRect("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(26f, 38f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static TextMeshProUGUI CreateValueLabel(Transform parent, string name, Vector2 position)
    {
        GameObject labelObject = CreateRect(name, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(100f, 54f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string localizationKey,
        Vector2 position, Vector2 size)
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

        GameObject labelObject = CreateRect("Label", buttonObject.transform);
        StretchFull(labelObject.GetComponent<RectTransform>());

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 21f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        label.raycastTarget = false;

        LocalizedTMPText localized = labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(localizationKey);
        return button;
    }

    private static void CreateLocalizedLabel(Transform parent, string localizationKey, float fontSize,
        Vector2 position, Vector2 size, TextAlignmentOptions alignment)
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
        label.alignment = alignment;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        LocalizedTMPText localized = labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(localizationKey);
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
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
}
