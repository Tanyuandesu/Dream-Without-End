using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative runtime localization service.
///
/// Text content lives in JSON TextAssets under Resources/Localization.
/// This manager reads the current language from SystemSettingsManager and
/// never owns a duplicate language preference.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalizationManager : MonoBehaviour
{
    private const string ResourceFolder = "Localization";
    private const string MissingPrefix = "[MISSING: ";
    private const string MissingSuffix = "]";

    public static LocalizationManager Instance { get; private set; }

    private readonly Dictionary<string, LocalizationEntry> entries =
        new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

    private SystemSettingsManager settings;
    private bool initialized;
    private int loadedTableCount;

    public GameLanguage CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return settings != null
                ? settings.Language
                : SystemSettingsManager.DefaultLanguage;
        }
    }

    public int LoadedKeyCount
    {
        get
        {
            EnsureInitialized();
            return entries.Count;
        }
    }

    public int LoadedTableCount
    {
        get
        {
            EnsureInitialized();
            return loadedTableCount;
        }
    }

    public event Action<GameLanguage> LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static LocalizationManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        LocalizationManager existing =
            FindFirstObjectByType<LocalizationManager>();

        if (existing != null)
        {
            Instance = existing;
            existing.EnsureInitialized();
            return existing;
        }

        GameObject runtimeObject =
            new GameObject("Localization_Runtime");

        return runtimeObject.AddComponent<LocalizationManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (settings != null)
        {
            settings.LanguageChanged -= HandleSettingsLanguageChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public string GetText(string key)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(key))
        {
            return BuildMissingMarker("EMPTY_KEY");
        }

        string normalizedKey = key.Trim();

        if (!entries.TryGetValue(normalizedKey, out LocalizationEntry entry) ||
            entry == null)
        {
            return BuildMissingMarker(normalizedKey);
        }

        string localized = ResolveWithFallback(entry, CurrentLanguage);

        if (string.IsNullOrEmpty(localized))
        {
            return BuildMissingMarker(normalizedKey);
        }

        return localized;
    }

    public bool TryGetText(string key, out string text)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(key))
        {
            text = BuildMissingMarker("EMPTY_KEY");
            return false;
        }

        string normalizedKey = key.Trim();

        if (!entries.TryGetValue(normalizedKey, out LocalizationEntry entry) ||
            entry == null)
        {
            text = BuildMissingMarker(normalizedKey);
            return false;
        }

        text = ResolveWithFallback(entry, CurrentLanguage);

        if (string.IsNullOrEmpty(text))
        {
            text = BuildMissingMarker(normalizedKey);
            return false;
        }

        return true;
    }

    public void ReloadTables()
    {
        entries.Clear();
        loadedTableCount = 0;

        TextAsset[] tables = Resources.LoadAll<TextAsset>(ResourceFolder);
        Array.Sort(
            tables,
            (a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < tables.Length; i++)
        {
            LoadTable(tables[i]);
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        settings = SystemSettingsManager.GetOrCreate();

        if (settings != null)
        {
            settings.LanguageChanged += HandleSettingsLanguageChanged;
        }

        ReloadTables();
    }

    private void LoadTable(TextAsset tableAsset)
    {
        if (tableAsset == null)
        {
            return;
        }

        LocalizationTableData table;

        try
        {
            table = JsonUtility.FromJson<LocalizationTableData>(tableAsset.text);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SYS3] Failed to parse localization table '{tableAsset.name}'.\n{exception}",
                this);
            return;
        }

        if (table == null || table.entries == null)
        {
            Debug.LogWarning(
                $"[SYS3] Localization table '{tableAsset.name}' has no entries.",
                this);
            loadedTableCount++;
            return;
        }

        loadedTableCount++;

        for (int i = 0; i < table.entries.Length; i++)
        {
            LocalizationEntry entry = table.entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                Debug.LogWarning(
                    $"[SYS3] Ignored empty localization key in '{tableAsset.name}' at index {i}.",
                    this);
                continue;
            }

            string normalizedKey = entry.key.Trim();

            if (entries.ContainsKey(normalizedKey))
            {
                Debug.LogError(
                    $"[SYS3] Duplicate localization key '{normalizedKey}' found in '{tableAsset.name}'. First definition remains authoritative.",
                    this);
                continue;
            }

            entries.Add(normalizedKey, entry);
        }
    }

    private void HandleSettingsLanguageChanged(GameLanguage language)
    {
        LanguageChanged?.Invoke(language);

#if UNITY_EDITOR
        Debug.Log(
            $"[SYS3] Language changed | Language={language}",
            this);
#endif
    }

    private static string ResolveWithFallback(
        LocalizationEntry entry,
        GameLanguage requestedLanguage)
    {
        string requested = entry.Get(requestedLanguage);
        if (!string.IsNullOrEmpty(requested))
        {
            return requested;
        }

        // Japanese is the project default and therefore the first fallback.
        string japanese = entry.Get(GameLanguage.Japanese);
        if (!string.IsNullOrEmpty(japanese))
        {
            return japanese;
        }

        string english = entry.Get(GameLanguage.English);
        if (!string.IsNullOrEmpty(english))
        {
            return english;
        }

        string traditionalChinese =
            entry.Get(GameLanguage.TraditionalChinese);
        if (!string.IsNullOrEmpty(traditionalChinese))
        {
            return traditionalChinese;
        }

        return null;
    }

    private static string BuildMissingMarker(string key)
    {
        return MissingPrefix + key + MissingSuffix;
    }

#if UNITY_EDITOR
    [ContextMenu("SYS3 Debug/Print Localization Summary")]
    private void DebugPrintSummary()
    {
        Debug.Log(
            $"[SYS3] Localization | Language={CurrentLanguage} | Tables={LoadedTableCount} | Keys={LoadedKeyCount}",
            this);
    }

    [ContextMenu("SYS3 Debug/Print Test Greeting")]
    private void DebugPrintTestGreeting()
    {
        Debug.Log(
            $"[SYS3] SYS3_TEST_GREETING => {GetText("SYS3_TEST_GREETING")}",
            this);
    }

    [ContextMenu("SYS3 Debug/Print Missing-Key Test")]
    private void DebugPrintMissingKey()
    {
        Debug.Log(
            $"[SYS3] Missing key test => {GetText("SYS3_KEY_DOES_NOT_EXIST")}",
            this);
    }

    [ContextMenu("SYS3 Debug/Print Fallback Test")]
    private void DebugPrintFallback()
    {
        Debug.Log(
            $"[SYS3] SYS3_TEST_FALLBACK => {GetText("SYS3_TEST_FALLBACK")}",
            this);
    }

    [ContextMenu("SYS3 Debug/Set Language/English")]
    private void DebugSetEnglish()
    {
        settings.SetLanguage(GameLanguage.English);
    }

    [ContextMenu("SYS3 Debug/Set Language/Japanese")]
    private void DebugSetJapanese()
    {
        settings.SetLanguage(GameLanguage.Japanese);
    }

    [ContextMenu("SYS3 Debug/Set Language/Traditional Chinese")]
    private void DebugSetTraditionalChinese()
    {
        settings.SetLanguage(GameLanguage.TraditionalChinese);
    }
#endif
}
