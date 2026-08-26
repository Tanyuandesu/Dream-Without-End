using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-3.1：中精度 Hard Structure 的可替换视觉 Theme。
///
/// 重要契约：
/// - Theme 只拥有 Sprite / Scale / SortingOrder。
/// - Theme 不拥有 Collider、BlockedCells、FloorCells 或 A*。
/// - 同一组 R2B BlockedCells 换任何 Theme，玩法几何必须完全不变。
///
/// 为降低后期换皮成本，只要求 6 种拓扑素材；方向由运行时旋转解决。
/// </summary>
[CreateAssetMenu(
    fileName = "ProceduralMediumStructuralSkinTheme",
    menuName = "Dream Dungeon/Procedural Room/Structural Skin Theme")]
public sealed class DreamProceduralRoomStructuralSkinThemeP1012A31 :
    ScriptableObject
{
    [SerializeField]
    private string themeId =
        "Procedural_Medium_Structural_Default";

    [Header("拓扑 Sprite Pool")]
    [Tooltip("0 个四向邻居。")]
    [SerializeField]
    private List<Sprite> isolatedSprites =
        new List<Sprite>();

    [Tooltip("1 个四向邻居。标准朝向：连接 North。")]
    [SerializeField]
    private List<Sprite> endSprites =
        new List<Sprite>();

    [Tooltip("2 个相对邻居。标准朝向：North-South。")]
    [SerializeField]
    private List<Sprite> straightSprites =
        new List<Sprite>();

    [Tooltip("2 个相邻邻居。标准朝向：North + East。")]
    [SerializeField]
    private List<Sprite> cornerSprites =
        new List<Sprite>();

    [Tooltip("3 个邻居。标准朝向：North + East + West，也就是缺 South。")]
    [SerializeField]
    private List<Sprite> tJunctionSprites =
        new List<Sprite>();

    [Tooltip("4 个邻居。")]
    [SerializeField]
    private List<Sprite> crossSprites =
        new List<Sprite>();

    [Header("视觉参数")]
    [Range(0.5f, 1.35f)]
    [SerializeField]
    private float scale = 1f;

    [SerializeField]
    private int sortingOrder = 8;

    [Range(0f, 0.2f)]
    [SerializeField]
    private float positionJitter = 0f;

    [Header("空 Sprite Pool 的调试占位色")]
    [SerializeField]
    private Color isolatedFallback =
        new Color(0.80f, 0.58f, 0.32f, 0.92f);

    [SerializeField]
    private Color endFallback =
        new Color(0.70f, 0.50f, 0.28f, 0.92f);

    [SerializeField]
    private Color straightFallback =
        new Color(0.62f, 0.45f, 0.26f, 0.92f);

    [SerializeField]
    private Color cornerFallback =
        new Color(0.68f, 0.48f, 0.26f, 0.92f);

    [SerializeField]
    private Color tJunctionFallback =
        new Color(0.56f, 0.40f, 0.24f, 0.92f);

    [SerializeField]
    private Color crossFallback =
        new Color(0.48f, 0.35f, 0.22f, 0.92f);

    public string ThemeId =>
        string.IsNullOrWhiteSpace(themeId)
            ? "Procedural_Medium_Structural_Default"
            : themeId.Trim();

    public float Scale =>
        Mathf.Clamp(scale, 0.5f, 1.35f);

    public int SortingOrder => sortingOrder;

    public float PositionJitter =>
        Mathf.Clamp(positionJitter, 0f, 0.2f);

    public Sprite PickSprite(
        DreamProceduralStructureTopologyP1012A31 topology,
        System.Random random)
    {
        List<Sprite> source =
            GetSpriteList(topology);

        if (source == null ||
            source.Count == 0)
        {
            return null;
        }

        List<Sprite> valid =
            new List<Sprite>();

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
            {
                valid.Add(source[i]);
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        return valid[
            random.Next(0, valid.Count)];
    }

    public int GetSpriteCount(
        DreamProceduralStructureTopologyP1012A31 topology)
    {
        List<Sprite> source =
            GetSpriteList(topology);

        if (source == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    public Color GetFallbackColor(
        DreamProceduralStructureTopologyP1012A31 topology)
    {
        switch (topology)
        {
            case DreamProceduralStructureTopologyP1012A31.Isolated:
                return isolatedFallback;

            case DreamProceduralStructureTopologyP1012A31.End:
                return endFallback;

            case DreamProceduralStructureTopologyP1012A31.Straight:
                return straightFallback;

            case DreamProceduralStructureTopologyP1012A31.Corner:
                return cornerFallback;

            case DreamProceduralStructureTopologyP1012A31.TJunction:
                return tJunctionFallback;

            case DreamProceduralStructureTopologyP1012A31.Cross:
                return crossFallback;

            default:
                return Color.white;
        }
    }

    private List<Sprite> GetSpriteList(
        DreamProceduralStructureTopologyP1012A31 topology)
    {
        switch (topology)
        {
            case DreamProceduralStructureTopologyP1012A31.Isolated:
                return isolatedSprites;

            case DreamProceduralStructureTopologyP1012A31.End:
                return endSprites;

            case DreamProceduralStructureTopologyP1012A31.Straight:
                return straightSprites;

            case DreamProceduralStructureTopologyP1012A31.Corner:
                return cornerSprites;

            case DreamProceduralStructureTopologyP1012A31.TJunction:
                return tJunctionSprites;

            case DreamProceduralStructureTopologyP1012A31.Cross:
                return crossSprites;

            default:
                return null;
        }
    }
}

public enum DreamProceduralStructureTopologyP1012A31
{
    Isolated = 0,
    End = 1,
    Straight = 2,
    Corner = 3,
    TJunction = 4,
    Cross = 5
}
