using System;

[Serializable]
public struct SystemSettingsSnapshot
{
    public GameLanguage Language;
    public float MasterVolume;
    public float BgmVolume;
    public float SfxVolume;

    public SystemSettingsSnapshot(
        GameLanguage language,
        float masterVolume,
        float bgmVolume,
        float sfxVolume)
    {
        Language = language;
        MasterVolume = masterVolume;
        BgmVolume = bgmVolume;
        SfxVolume = sfxVolume;
    }
}
