using UnityEngine;

/// <summary>
/// 程序化走廊的可替换表现层。
///
/// 当前临时 Gray Stone Profile 只使用确定性的明暗变化；
/// 未来可以把 PixelLab 图块按四方向邻接 Mask 填入 0～15 槽位，
/// 不需要改动 FloorCells、碰撞、Socket 或敌人 A*。
/// </summary>
[CreateAssetMenu(
    fileName = "CorridorVisualProfile",
    menuName = "Dream Dungeon/Corridor Visual Profile")]
public sealed class DungeonCorridorVisualProfile :
    ScriptableObject
{
    public const int NorthBit = 1;
    public const int EastBit = 2;
    public const int SouthBit = 4;
    public const int WestBit = 8;
    public const int CardinalMaskCount = 16;

    [Header("身份")]
    [SerializeField]
    private string profileId = "GrayStone_Temporary_C1";

    [Header("临时灰石地板")]
    [SerializeField]
    private Color corridorFloorColor =
        new Color(0.17f, 0.18f, 0.21f, 1f);

    [Range(0f, 0.2f)]
    [SerializeField]
    private float floorBrightnessVariation = 0.025f;

    [Header("临时灰石墙")]
    [SerializeField]
    private Color corridorWallColor =
        new Color(0.32f, 0.33f, 0.37f, 1f);

    [Tooltip("墙格南侧邻接地板时，视为画面上沿受光面。")]
    [Range(0.5f, 1.5f)]
    [SerializeField]
    private float northWallBrightness = 1.16f;

    [Tooltip("墙格东西侧邻接地板时的侧墙明度。")]
    [Range(0.5f, 1.5f)]
    [SerializeField]
    private float sideWallBrightness = 0.98f;

    [Tooltip("墙格北侧邻接地板时，视为画面下沿阴影面。")]
    [Range(0.5f, 1.5f)]
    [SerializeField]
    private float southWallBrightness = 0.78f;

    [Tooltip("只与地板斜向相邻的外角墙明度。")]
    [Range(0.5f, 1.5f)]
    [SerializeField]
    private float diagonalWallBrightness = 0.88f;

    [Range(0f, 0.2f)]
    [SerializeField]
    private float wallBrightnessVariation = 0.055f;

    [Header("墙体视觉厚度（C2）")]
    [Tooltip(
        "只把墙体朝走廊外侧的可见部分收进；0 保持整格显示，" +
        "0.30 表示外侧留空 30%。内侧视觉边界与整格碰撞均不移动。")]
    [Range(0f, 0.45f)]
    [SerializeField]
    private float wallOuterVisualInset;

    [SerializeField]
    private int deterministicVariationSalt = 173;

    [Header("后续正式图块（四方向邻接 Mask 0～15）")]
    [Tooltip(
        "索引位：North=1、East=2、South=4、West=8。" +
        "槽位为空时继续使用运行时白方块与颜色。")]
    [SerializeField]
    private Sprite[] floorSpritesByCardinalMask =
        new Sprite[CardinalMaskCount];

    [Tooltip(
        "索引位表示该墙格四方向上邻接走廊地板的位置。" +
        "槽位为空时继续使用运行时白方块与颜色。")]
    [SerializeField]
    private Sprite[] wallSpritesByFloorMask =
        new Sprite[CardinalMaskCount];

    public string ProfileId =>
        string.IsNullOrWhiteSpace(profileId)
            ? name
            : profileId.Trim();

    public Color CorridorFloorColor =>
        corridorFloorColor;

    public Color CorridorWallColor =>
        corridorWallColor;

    public float FloorBrightnessVariation =>
        floorBrightnessVariation;

    public float WallBrightnessVariation =>
        wallBrightnessVariation;

    /// <summary>
    /// 墙格朝外侧留空的比例。Renderer 只把这个值应用到视觉子物体；
    /// 墙格根物体与 BoxCollider2D 始终保持完整一格。
    /// </summary>
    public float WallOuterVisualInset =>
        wallOuterVisualInset;

    public int DeterministicVariationSalt =>
        deterministicVariationSalt;

    public Color EvaluateFloorColor(
        Vector2Int cell,
        int cardinalMask,
        int layoutSeed)
    {
        float variation = RangedVariation(
            cell,
            cardinalMask,
            layoutSeed,
            floorBrightnessVariation,
            0x45D9F3B);

        return MultiplyRgb(
            corridorFloorColor,
            1f + variation);
    }

    public Color EvaluateWallColor(
        Vector2Int cell,
        int adjacentFloorMask,
        int layoutSeed)
    {
        float directionalBrightness =
            ResolveWallDirectionalBrightness(
                adjacentFloorMask);

        float variation = RangedVariation(
            cell,
            adjacentFloorMask,
            layoutSeed,
            wallBrightnessVariation,
            unchecked((int)0x9E3779B9u));

        return MultiplyRgb(
            corridorWallColor,
            directionalBrightness *
            (1f + variation));
    }

    public Sprite GetFloorSprite(int cardinalMask)
    {
        return GetSprite(
            floorSpritesByCardinalMask,
            cardinalMask);
    }

    public Sprite GetWallSprite(int adjacentFloorMask)
    {
        return GetSprite(
            wallSpritesByFloorMask,
            adjacentFloorMask);
    }

    private float ResolveWallDirectionalBrightness(
        int adjacentFloorMask)
    {
        // 墙格南侧有地板，代表该墙位于通道北侧／画面上沿。
        if ((adjacentFloorMask & SouthBit) != 0)
        {
            return northWallBrightness;
        }

        // 墙格北侧有地板，代表该墙位于通道南侧／画面下沿。
        if ((adjacentFloorMask & NorthBit) != 0)
        {
            return southWallBrightness;
        }

        if ((adjacentFloorMask &
             (EastBit | WestBit)) != 0)
        {
            return sideWallBrightness;
        }

        return diagonalWallBrightness;
    }

    private float RangedVariation(
        Vector2Int cell,
        int mask,
        int layoutSeed,
        float amplitude,
        int channelSalt)
    {
        if (amplitude <= 0f)
        {
            return 0f;
        }

        uint hash = HashCell(
            cell,
            mask,
            layoutSeed,
            deterministicVariationSalt,
            channelSalt);

        float zeroToOne =
            (hash & 0x00FFFFFFu) /
            16777215f;

        return (zeroToOne * 2f - 1f) *
               amplitude;
    }

    private static uint HashCell(
        Vector2Int cell,
        int mask,
        int layoutSeed,
        int profileSalt,
        int channelSalt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)cell.x) * 16777619u;
            hash = (hash ^ (uint)cell.y) * 16777619u;
            hash = (hash ^ (uint)mask) * 16777619u;
            hash = (hash ^ (uint)layoutSeed) * 16777619u;
            hash = (hash ^ (uint)profileSalt) * 16777619u;
            hash = (hash ^ (uint)channelSalt) * 16777619u;

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static Color MultiplyRgb(
        Color source,
        float multiplier)
    {
        return new Color(
            Mathf.Clamp01(source.r * multiplier),
            Mathf.Clamp01(source.g * multiplier),
            Mathf.Clamp01(source.b * multiplier),
            source.a);
    }

    private static Sprite GetSprite(
        Sprite[] sprites,
        int mask)
    {
        int index = mask & 0x0F;

        return sprites != null &&
               sprites.Length == CardinalMaskCount
            ? sprites[index]
            : null;
    }

    private void OnValidate()
    {
        floorBrightnessVariation = Mathf.Clamp(
            floorBrightnessVariation,
            0f,
            0.2f);

        wallBrightnessVariation = Mathf.Clamp(
            wallBrightnessVariation,
            0f,
            0.2f);

        wallOuterVisualInset = Mathf.Clamp(
            wallOuterVisualInset,
            0f,
            0.45f);

        northWallBrightness = Mathf.Clamp(
            northWallBrightness,
            0.5f,
            1.5f);

        sideWallBrightness = Mathf.Clamp(
            sideWallBrightness,
            0.5f,
            1.5f);

        southWallBrightness = Mathf.Clamp(
            southWallBrightness,
            0.5f,
            1.5f);

        diagonalWallBrightness = Mathf.Clamp(
            diagonalWallBrightness,
            0.5f,
            1.5f);

        EnsureSpriteArray(ref floorSpritesByCardinalMask);
        EnsureSpriteArray(ref wallSpritesByFloorMask);
    }

    private static void EnsureSpriteArray(ref Sprite[] sprites)
    {
        if (sprites != null &&
            sprites.Length == CardinalMaskCount)
        {
            return;
        }

        Sprite[] resized =
            new Sprite[CardinalMaskCount];

        if (sprites != null)
        {
            int copied = Mathf.Min(
                sprites.Length,
                resized.Length);

            for (int i = 0; i < copied; i++)
            {
                resized[i] = sprites[i];
            }
        }

        sprites = resized;
    }
}
