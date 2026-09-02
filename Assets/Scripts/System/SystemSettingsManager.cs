using System;
using UnityEngine;

/// <summary>
/// Authoritative persistent user-settings service.
///
/// This data is intentionally separate from gameplay Save/Load data:
/// deleting or replacing a run save must never reset language or audio
/// preferences. SYS3 Localization and SYS4 Audio should read/subscribe to
/// this manager instead of owning duplicate preference values.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemSettingsManager : MonoBehaviour
{
    private const int CurrentSettingsVersion = 2;

    private const string KeyVersion =
        "DreamDungeon.Settings.Version";
    private const string KeyLanguage =
        "DreamDungeon.Settings.Language";
    private const string KeyMasterVolume =
        "DreamDungeon.Settings.MasterVolume";
    private const string KeyBgmVolume =
        "DreamDungeon.Settings.BgmVolume";
    private const string KeySfxVolume =
        "DreamDungeon.Settings.SfxVolume";

    public const GameLanguage DefaultLanguage =
        GameLanguage.Japanese;
    public const float DefaultMasterVolume = 1f;
    public const float DefaultBgmVolume = 1f;
    public const float DefaultSfxVolume = 1f;

    public static SystemSettingsManager Instance { get; private set; }

    private GameLanguage language = DefaultLanguage;
    private float masterVolume = DefaultMasterVolume;
    private float bgmVolume = DefaultBgmVolume;
    private float sfxVolume = DefaultSfxVolume;
    private bool initialized;

    public GameLanguage Language
    {
        get
        {
            EnsureInitialized();
            return language;
        }
    }

    public float MasterVolume
    {
        get
        {
            EnsureInitialized();
            return masterVolume;
        }
    }

    public float BgmVolume
    {
        get
        {
            EnsureInitialized();
            return bgmVolume;
        }
    }

    public float SfxVolume
    {
        get
        {
            EnsureInitialized();
            return sfxVolume;
        }
    }

    public bool IsInitialized => initialized;

    public event Action<GameLanguage> LanguageChanged;
    public event Action<float> MasterVolumeChanged;
    public event Action<float> BgmVolumeChanged;
    public event Action<float> SfxVolumeChanged;
    public event Action<SystemSettingsSnapshot> SettingsChanged;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static SystemSettingsManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SystemSettingsManager existing =
            FindFirstObjectByType<SystemSettingsManager>();

        if (existing != null)
        {
            Instance = existing;
            existing.EnsureInitialized();
            return existing;
        }

        GameObject settingsObject =
            new GameObject("SystemSettings_Runtime");

        return settingsObject.AddComponent<SystemSettingsManager>();
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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && initialized)
        {
            Flush();
        }
    }

    private void OnApplicationQuit()
    {
        if (initialized)
        {
            Flush();
        }
    }

    public SystemSettingsSnapshot CreateSnapshot()
    {
        EnsureInitialized();

        return new SystemSettingsSnapshot(
            language,
            masterVolume,
            bgmVolume,
            sfxVolume);
    }

    public void SetLanguage(
        GameLanguage value,
        bool persistImmediately = true)
    {
        EnsureInitialized();

        GameLanguage sanitized =
            SanitizeLanguage(value);

        if (language == sanitized)
        {
            return;
        }

        language = sanitized;
        PlayerPrefs.SetInt(KeyLanguage, (int)language);

        if (persistImmediately)
        {
            Flush();
        }

        LanguageChanged?.Invoke(language);
        NotifySettingsChanged();
    }

    public void SetMasterVolume(
        float value,
        bool persistImmediately = true)
    {
        EnsureInitialized();

        float sanitized = SanitizeVolume(value);

        if (Mathf.Approximately(masterVolume, sanitized))
        {
            return;
        }

        masterVolume = sanitized;
        PlayerPrefs.SetFloat(KeyMasterVolume, masterVolume);

        if (persistImmediately)
        {
            Flush();
        }

        MasterVolumeChanged?.Invoke(masterVolume);
        NotifySettingsChanged();
    }

    public void SetBgmVolume(
        float value,
        bool persistImmediately = true)
    {
        EnsureInitialized();

        float sanitized = SanitizeVolume(value);

        if (Mathf.Approximately(bgmVolume, sanitized))
        {
            return;
        }

        bgmVolume = sanitized;
        PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);

        if (persistImmediately)
        {
            Flush();
        }

        BgmVolumeChanged?.Invoke(bgmVolume);
        NotifySettingsChanged();
    }


    public void SetSfxVolume(
        float value,
        bool persistImmediately = true)
    {
        EnsureInitialized();

        float sanitized = SanitizeVolume(value);

        if (Mathf.Approximately(sfxVolume, sanitized))
        {
            return;
        }

        sfxVolume = sanitized;
        PlayerPrefs.SetFloat(KeySfxVolume, sfxVolume);

        if (persistImmediately)
        {
            Flush();
        }

        SfxVolumeChanged?.Invoke(sfxVolume);
        NotifySettingsChanged();
    }

    public void ResetToDefaults(bool persistImmediately = true)
    {
        EnsureInitialized();

        bool languageChanged =
            language != DefaultLanguage;
        bool masterChanged =
            !Mathf.Approximately(
                masterVolume,
                DefaultMasterVolume);
        bool bgmChanged =
            !Mathf.Approximately(
                bgmVolume,
                DefaultBgmVolume);
        bool sfxChanged =
            !Mathf.Approximately(
                sfxVolume,
                DefaultSfxVolume);

        language = DefaultLanguage;
        masterVolume = DefaultMasterVolume;
        bgmVolume = DefaultBgmVolume;
        sfxVolume = DefaultSfxVolume;

        WriteAllToPlayerPrefs();

        if (persistImmediately)
        {
            Flush();
        }

        if (languageChanged)
        {
            LanguageChanged?.Invoke(language);
        }

        if (masterChanged)
        {
            MasterVolumeChanged?.Invoke(masterVolume);
        }

        if (bgmChanged)
        {
            BgmVolumeChanged?.Invoke(bgmVolume);
        }

        if (sfxChanged)
        {
            SfxVolumeChanged?.Invoke(sfxVolume);
        }

        if (languageChanged || masterChanged || bgmChanged || sfxChanged)
        {
            NotifySettingsChanged();
        }
    }

    public void ReloadFromStorage()
    {
        bool wasInitialized = initialized;
        GameLanguage previousLanguage = language;
        float previousMaster = masterVolume;
        float previousBgm = bgmVolume;
        float previousSfx = sfxVolume;

        LoadFromPlayerPrefs();
        initialized = true;

        if (!wasInitialized)
        {
            return;
        }

        if (previousLanguage != language)
        {
            LanguageChanged?.Invoke(language);
        }

        if (!Mathf.Approximately(previousMaster, masterVolume))
        {
            MasterVolumeChanged?.Invoke(masterVolume);
        }

        if (!Mathf.Approximately(previousBgm, bgmVolume))
        {
            BgmVolumeChanged?.Invoke(bgmVolume);
        }

        if (!Mathf.Approximately(previousSfx, sfxVolume))
        {
            SfxVolumeChanged?.Invoke(sfxVolume);
        }

        if (previousLanguage != language ||
            !Mathf.Approximately(previousMaster, masterVolume) ||
            !Mathf.Approximately(previousBgm, bgmVolume) ||
            !Mathf.Approximately(previousSfx, sfxVolume))
        {
            NotifySettingsChanged();
        }
    }

    public void Flush()
    {
        PlayerPrefs.Save();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        LoadFromPlayerPrefs();
        initialized = true;
    }

    private void LoadFromPlayerPrefs()
    {
        int rawLanguage = PlayerPrefs.GetInt(
            KeyLanguage,
            (int)DefaultLanguage);

        language = SanitizeLanguage(rawLanguage);

        masterVolume = SanitizeVolume(
            PlayerPrefs.GetFloat(
                KeyMasterVolume,
                DefaultMasterVolume));

        bgmVolume = SanitizeVolume(
            PlayerPrefs.GetFloat(
                KeyBgmVolume,
                DefaultBgmVolume));

        sfxVolume = SanitizeVolume(
            PlayerPrefs.GetFloat(
                KeySfxVolume,
                DefaultSfxVolume));

        // Write sanitized/default values back so the stored schema is always
        // complete and future systems can rely on all current fields existing.
        WriteAllToPlayerPrefs();
    }

    private void WriteAllToPlayerPrefs()
    {
        PlayerPrefs.SetInt(KeyVersion, CurrentSettingsVersion);
        PlayerPrefs.SetInt(KeyLanguage, (int)language);
        PlayerPrefs.SetFloat(KeyMasterVolume, masterVolume);
        PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);
        PlayerPrefs.SetFloat(KeySfxVolume, sfxVolume);
    }

    private void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke(CreateSnapshot());
    }

    private static GameLanguage SanitizeLanguage(
        GameLanguage value)
    {
        return SanitizeLanguage((int)value);
    }

    private static GameLanguage SanitizeLanguage(int rawValue)
    {
        switch ((GameLanguage)rawValue)
        {
            case GameLanguage.English:
            case GameLanguage.Japanese:
            case GameLanguage.TraditionalChinese:
                return (GameLanguage)rawValue;

            default:
                return DefaultLanguage;
        }
    }

    private static float SanitizeVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 1f;
        }

        return Mathf.Clamp01(value);
    }

#if UNITY_EDITOR
    [ContextMenu("SYS2 Debug/Print Current Settings")]
    private void DebugPrintCurrentSettings()
    {
        SystemSettingsSnapshot snapshot = CreateSnapshot();

        Debug.Log(
            "[SYS2] Settings" +
            " | Language=" + snapshot.Language +
            " | Master=" + snapshot.MasterVolume.ToString("0.00") +
            " | BGM=" + snapshot.BgmVolume.ToString("0.00") +
            " | SFX=" + snapshot.SfxVolume.ToString("0.00"),
            this);
    }

    [ContextMenu("SYS2 Debug/Set Persistence Test Values")]
    private void DebugSetPersistenceTestValues()
    {
        SetLanguage(GameLanguage.TraditionalChinese, false);
        SetMasterVolume(0.37f, false);
        SetBgmVolume(0.62f, false);
        SetSfxVolume(0.24f, false);
        Flush();

        Debug.Log(
            "[SYS2] Persistence test values saved" +
            " | Language=" + language +
            " | Master=" + masterVolume.ToString("0.00") +
            " | BGM=" + bgmVolume.ToString("0.00") +
            " | SFX=" + sfxVolume.ToString("0.00"),
            this);
    }

    [ContextMenu("SYS2 Debug/Reset To Defaults")]
    private void DebugResetToDefaults()
    {
        ResetToDefaults(true);

        Debug.Log(
            "[SYS2] Defaults restored" +
            " | Language=" + language +
            " | Master=" + masterVolume.ToString("0.00") +
            " | BGM=" + bgmVolume.ToString("0.00") +
            " | SFX=" + sfxVolume.ToString("0.00"),
            this);
    }
#endif
}
