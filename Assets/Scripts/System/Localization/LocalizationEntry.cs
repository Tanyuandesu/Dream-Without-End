using System;

/// <summary>
/// One stable localization key and its supported language values.
/// Content files may be split by category; LocalizationManager merges them
/// into one runtime dictionary and rejects duplicate keys.
/// </summary>
[Serializable]
public sealed class LocalizationEntry
{
    public string key;
    public string english;
    public string japanese;
    public string traditionalChinese;

    public string Get(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English:
                return english;
            case GameLanguage.Japanese:
                return japanese;
            case GameLanguage.TraditionalChinese:
                return traditionalChinese;
            default:
                return null;
        }
    }
}
