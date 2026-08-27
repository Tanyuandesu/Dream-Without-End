using System;

[Serializable]
public struct SystemSettingsSnapshot
{
    public GameLanguage Language;
    public float MasterVolume;
    public float BgmVolume;

    public SystemSettingsSnapshot(
        GameLanguage language,
        float masterVolume,
        float bgmVolume)
    {
        Language = language;
        MasterVolume = masterVolume;
        BgmVolume = bgmVolume;
    }
}
