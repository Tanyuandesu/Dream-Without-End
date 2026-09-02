using UnityEngine;

/// <summary>
/// Authoritative persistent runtime audio service.
///
/// SYS4 responsibilities:
/// - Master volume is applied globally through AudioListener.volume.
/// - BGM volume is applied on the dedicated persistent BGM AudioSource.
/// - SFX volume is applied on the dedicated persistent SFX AudioSource.
/// - Master remains the global authority, so effective BGM/SFX volume is
///   Master multiplied by the corresponding channel volume.
/// - Provides stable BGM/SFX playback interfaces without owning any concrete music assets.
///
/// Concrete AudioClips are intentionally supplied by callers later (Title/Game/Ending,
/// etc.), so replacing music does not require changing this service.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string RuntimeObjectName = "AudioSystem_Runtime";
    private const string BgmSourceName = "BGM_Source";
    private const string SfxSourceName = "SFX_Source";

    private SystemSettingsManager settings;
    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private bool initialized;

#if UNITY_EDITOR
    private AudioClip debugSilentClip;
#endif

    public AudioSource BgmSource
    {
        get
        {
            EnsureInitialized();
            return bgmSource;
        }
    }

    public AudioSource SfxSource
    {
        get
        {
            EnsureInitialized();
            return sfxSource;
        }
    }

    public AudioClip CurrentBgmClip
    {
        get
        {
            EnsureInitialized();
            return bgmSource != null ? bgmSource.clip : null;
        }
    }

    public bool IsBgmPlaying
    {
        get
        {
            EnsureInitialized();
            return bgmSource != null && bgmSource.isPlaying;
        }
    }

    public float MasterVolume
    {
        get
        {
            EnsureInitialized();
            return settings != null ? settings.MasterVolume : 1f;
        }
    }

    public float BgmVolume
    {
        get
        {
            EnsureInitialized();
            return settings != null ? settings.BgmVolume : 1f;
        }
    }

    public float SfxVolume
    {
        get
        {
            EnsureInitialized();
            return settings != null ? settings.SfxVolume : 1f;
        }
    }

    public float EffectiveBgmVolume => MasterVolume * BgmVolume;
    public float EffectiveSfxVolume => MasterVolume * SfxVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static AudioManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        AudioManager existing = FindFirstObjectByType<AudioManager>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureInitialized();
            return existing;
        }

        GameObject audioObject = new GameObject(RuntimeObjectName);
        return audioObject.AddComponent<AudioManager>();
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
        UnsubscribeFromSettings();

#if UNITY_EDITOR
        if (debugSilentClip != null)
        {
            Destroy(debugSilentClip);
            debugSilentClip = null;
        }
#endif

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Plays or replaces the current background music.
    /// Passing null is treated as StopBgm().
    /// </summary>
    public void PlayBgm(
        AudioClip clip,
        bool loop = true,
        bool restartIfSameClip = false)
    {
        EnsureInitialized();

        if (clip == null)
        {
            StopBgm();
            return;
        }

        bool sameClip = bgmSource.clip == clip;
        bgmSource.loop = loop;

        if (sameClip && !restartIfSameClip && bgmSource.isPlaying)
        {
            return;
        }

        if (!sameClip)
        {
            bgmSource.Stop();
            bgmSource.clip = clip;
        }

        bgmSource.Play();
    }

    public void StopBgm()
    {
        EnsureInitialized();

        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PauseBgm()
    {
        EnsureInitialized();

        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    public void ResumeBgm()
    {
        EnsureInitialized();

        if (bgmSource.clip != null && !bgmSource.isPlaying)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary>
    /// Plays a non-spatial one-shot SFX through the persistent SFX channel.
    /// The channel is controlled by SYS2 SFX volume and by global Master volume.
    /// volumeScale remains a per-sound multiplier for future balancing.
    /// </summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        EnsureInitialized();

        if (clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void StopAllSfx()
    {
        EnsureInitialized();
        sfxSource.Stop();
    }

    /// <summary>
    /// Re-applies the current SYS2 settings immediately.
    /// Normally this is automatic via settings events; the public method is useful
    /// for recovery/tests and future systems that intentionally rebuild audio state.
    /// </summary>
    public void RefreshVolumes()
    {
        EnsureInitialized();
        ApplyVolumes();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        EnsureAudioSources();

        settings = SystemSettingsManager.GetOrCreate();
        SubscribeToSettings();
        ApplyVolumes();

        initialized = true;
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            Transform existingBgm = transform.Find(BgmSourceName);
            if (existingBgm != null)
            {
                bgmSource = existingBgm.GetComponent<AudioSource>();
            }

            if (bgmSource == null)
            {
                GameObject bgmObject = new GameObject(BgmSourceName);
                bgmObject.transform.SetParent(transform, false);
                bgmSource = bgmObject.AddComponent<AudioSource>();
            }
        }

        if (sfxSource == null)
        {
            Transform existingSfx = transform.Find(SfxSourceName);
            if (existingSfx != null)
            {
                sfxSource = existingSfx.GetComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                GameObject sfxObject = new GameObject(SfxSourceName);
                sfxObject.transform.SetParent(transform, false);
                sfxSource = sfxObject.AddComponent<AudioSource>();
            }
        }

        ConfigureBgmSource(bgmSource);
        ConfigureSfxSource(sfxSource);
    }

    private static void ConfigureBgmSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 1f;
    }

    private static void ConfigureSfxSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
    }

    private void SubscribeToSettings()
    {
        if (settings == null)
        {
            return;
        }

        settings.MasterVolumeChanged -= HandleMasterVolumeChanged;
        settings.BgmVolumeChanged -= HandleBgmVolumeChanged;
        settings.SfxVolumeChanged -= HandleSfxVolumeChanged;

        settings.MasterVolumeChanged += HandleMasterVolumeChanged;
        settings.BgmVolumeChanged += HandleBgmVolumeChanged;
        settings.SfxVolumeChanged += HandleSfxVolumeChanged;
    }

    private void UnsubscribeFromSettings()
    {
        if (settings == null)
        {
            return;
        }

        settings.MasterVolumeChanged -= HandleMasterVolumeChanged;
        settings.BgmVolumeChanged -= HandleBgmVolumeChanged;
        settings.SfxVolumeChanged -= HandleSfxVolumeChanged;
    }

    private void HandleMasterVolumeChanged(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    private void HandleBgmVolumeChanged(float value)
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(value);
        }
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(value);
        }
    }

    private void ApplyVolumes()
    {
        float master = settings != null
            ? Mathf.Clamp01(settings.MasterVolume)
            : 1f;
        float bgm = settings != null
            ? Mathf.Clamp01(settings.BgmVolume)
            : 1f;
        float sfx = settings != null
            ? Mathf.Clamp01(settings.SfxVolume)
            : 1f;

        AudioListener.volume = master;

        if (bgmSource != null)
        {
            bgmSource.volume = bgm;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = sfx;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("SYS4 Debug/Print Audio State")]
    private void DebugPrintAudioState()
    {
        EnsureInitialized();

        string clipName = CurrentBgmClip != null
            ? CurrentBgmClip.name
            : "<none>";

        Debug.Log(
            "[SYS4] Audio" +
            " | Master=" + MasterVolume.ToString("0.00") +
            " | Listener=" + AudioListener.volume.ToString("0.00") +
            " | BGMSetting=" + BgmVolume.ToString("0.00") +
            " | BGMSource=" + bgmSource.volume.ToString("0.00") +
            " | EffectiveBGM=" + EffectiveBgmVolume.ToString("0.00") +
            " | SFXSetting=" + SfxVolume.ToString("0.00") +
            " | SFXSource=" + sfxSource.volume.ToString("0.00") +
            " | EffectiveSFX=" + EffectiveSfxVolume.ToString("0.00") +
            " | Clip=" + clipName +
            " | Playing=" + IsBgmPlaying,
            this);
    }

    [ContextMenu("SYS4 Debug/Play Silent BGM Probe")]
    private void DebugPlaySilentBgmProbe()
    {
        EnsureInitialized();

        if (debugSilentClip == null)
        {
            // A short silent clip proves the playback/replacement interface without
            // requiring any real music asset or producing an unwanted test sound.
            debugSilentClip = AudioClip.Create(
                "SYS4_SilentBGM_Probe",
                44100,
                1,
                44100,
                false);
        }

        PlayBgm(debugSilentClip, true, true);

        Debug.Log(
            "[SYS4] Silent BGM probe started" +
            " | Clip=" + CurrentBgmClip.name +
            " | Playing=" + IsBgmPlaying +
            " | Loop=" + bgmSource.loop,
            this);
    }

    [ContextMenu("SYS4 Debug/Stop BGM Probe")]
    private void DebugStopBgmProbe()
    {
        StopBgm();

        Debug.Log(
            "[SYS4] BGM stopped" +
            " | Clip=" + (CurrentBgmClip == null ? "<none>" : CurrentBgmClip.name) +
            " | Playing=" + IsBgmPlaying,
            this);
    }
#endif
}
