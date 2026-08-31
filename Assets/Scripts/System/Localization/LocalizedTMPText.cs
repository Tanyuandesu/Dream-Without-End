using TMPro;
using UnityEngine;

/// <summary>
/// Binds a TMP text component to one localization key.
/// Visible text and its language-appropriate font refresh automatically
/// whenever SYS2 changes the current language.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TMP_Text targetText;
    private TMP_FontAsset originalFont;
    private LocalizationManager localization;
    private LocalizationFontProfile fontProfile;

    public string LocalizationKey => localizationKey;

    private void Awake()
    {
        EnsureTargetText();
        CaptureOriginalFont();
        EnsureFontProfile();
    }

    private void OnEnable()
    {
        EnsureTargetText();
        CaptureOriginalFont();
        EnsureFontProfile();

        localization = LocalizationManager.GetOrCreate();

        if (localization != null)
        {
            localization.LanguageChanged += HandleLanguageChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (localization != null)
        {
            localization.LanguageChanged -= HandleLanguageChanged;
        }
    }

    public void SetKey(string key)
    {
        localizationKey = key;
        Refresh();
    }

    public void Refresh()
    {
        EnsureTargetText();

        if (targetText == null)
        {
            return;
        }

        CaptureOriginalFont();
        EnsureFontProfile();

        localization = localization != null
            ? localization
            : LocalizationManager.GetOrCreate();

        GameLanguage language = localization != null
            ? localization.CurrentLanguage
            : SystemSettingsManager.DefaultLanguage;

        ApplyFont(language);

        targetText.text = localization != null
            ? localization.GetText(localizationKey)
            : $"[MISSING: {localizationKey}]";
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        Refresh();
    }

    private void EnsureTargetText()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private void CaptureOriginalFont()
    {
        if (originalFont == null && targetText != null)
        {
            originalFont = targetText.font;
        }
    }

    private void EnsureFontProfile()
    {
        if (fontProfile == null)
        {
            fontProfile = LocalizationFontProfile.LoadRuntimeProfile();
        }
    }

    private void ApplyFont(GameLanguage language)
    {
        if (targetText == null)
        {
            return;
        }

        TMP_FontAsset resolvedFont = fontProfile != null
            ? fontProfile.ResolveFont(language, originalFont)
            : originalFont;

        if (resolvedFont != null && targetText.font != resolvedFont)
        {
            targetText.font = resolvedFont;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Refresh();
    }
#endif
}
