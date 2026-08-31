using TMPro;
using UnityEngine;

/// <summary>
/// Global font routing for localized TMP text.
///
/// English may intentionally be left empty so each TMP component keeps its
/// original font/style. Japanese and Traditional Chinese use dedicated CJK
/// font assets so regional glyphs render correctly.
/// </summary>
[CreateAssetMenu(
    fileName = "LocalizationFontProfile",
    menuName = "Dream Dungeon/System/Localization Font Profile")]
public sealed class LocalizationFontProfile : ScriptableObject
{
    public const string ResourcePath = "Localization/LocalizationFontProfile";

    [Header("Optional English override")]
    [SerializeField] private TMP_FontAsset englishFont;

    [Header("CJK fonts")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private TMP_FontAsset traditionalChineseFont;

    public TMP_FontAsset EnglishFont => englishFont;
    public TMP_FontAsset JapaneseFont => japaneseFont;
    public TMP_FontAsset TraditionalChineseFont => traditionalChineseFont;

    public TMP_FontAsset ResolveFont(
        GameLanguage language,
        TMP_FontAsset originalFont)
    {
        switch (language)
        {
            case GameLanguage.Japanese:
                return japaneseFont != null
                    ? japaneseFont
                    : originalFont;

            case GameLanguage.TraditionalChinese:
                return traditionalChineseFont != null
                    ? traditionalChineseFont
                    : originalFont;

            case GameLanguage.English:
            default:
                return englishFont != null
                    ? englishFont
                    : originalFont;
        }
    }

    public static LocalizationFontProfile LoadRuntimeProfile()
    {
        return Resources.Load<LocalizationFontProfile>(ResourcePath);
    }
}
