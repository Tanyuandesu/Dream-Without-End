using TMPro;
using UnityEngine;

/// <summary>
/// Binds a TMP text component to one localization key.
/// Existing visible text refreshes automatically whenever SYS2 changes the
/// current language.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TMP_Text targetText;
    private LocalizationManager localization;

    public string LocalizationKey => localizationKey;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

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
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null)
        {
            return;
        }

        localization = localization != null
            ? localization
            : LocalizationManager.GetOrCreate();

        targetText.text = localization != null
            ? localization.GetText(localizationKey)
            : $"[MISSING: {localizationKey}]";
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        Refresh();
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
