using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-2：中精度房间软装饰 Theme。
///
/// 设计目标：
/// - 所有这里的内容只负责视觉，不拥有 Collider，不修改 BlockedCells。
/// - 后期替换 Sprite / 数量 / SortingOrder 不需要重做导航或房间结构。
/// - 空 Sprite List 仍可运行，会显示调试占位图，方便先验证工程。
/// </summary>
[CreateAssetMenu(
    fileName = "ProceduralMediumSoftDecorTheme",
    menuName = "Dream Dungeon/Procedural Room/Soft Decor Theme")]
public sealed class DreamProceduralRoomDecorThemeP1012A2 :
    ScriptableObject
{
    [SerializeField]
    private string themeId =
        "Procedural_Medium_Default";

    [Header("每个 13x9 房间的目标数量")]
    [Min(0)]
    [SerializeField]
    private int floorClutterCount = 4;

    [Min(0)]
    [SerializeField]
    private int edgePropCount = 2;

    [Min(0)]
    [SerializeField]
    private int nearStructureCount = 3;

    [Min(0)]
    [SerializeField]
    private int foregroundPropCount = 2;

    [Header("可低成本替换的 Sprite 池")]
    [SerializeField]
    private List<Sprite> floorClutterSprites =
        new List<Sprite>();

    [SerializeField]
    private List<Sprite> edgePropSprites =
        new List<Sprite>();

    [SerializeField]
    private List<Sprite> nearStructureSprites =
        new List<Sprite>();

    [SerializeField]
    private List<Sprite> foregroundPropSprites =
        new List<Sprite>();

    [Header("视觉参数")]
    [Range(0f, 0.35f)]
    [SerializeField]
    private float positionJitter = 0.12f;

    [Range(0.1f, 1.5f)]
    [SerializeField]
    private float floorClutterScale = 0.34f;

    [Range(0.1f, 1.5f)]
    [SerializeField]
    private float edgePropScale = 0.48f;

    [Range(0.1f, 1.5f)]
    [SerializeField]
    private float nearStructureScale = 0.42f;

    [Range(0.1f, 1.5f)]
    [SerializeField]
    private float foregroundPropScale = 0.58f;

    [SerializeField]
    private int floorClutterSortingOrder = 1;

    [SerializeField]
    private int edgePropSortingOrder = 2;

    [SerializeField]
    private int nearStructureSortingOrder = 2;

    [SerializeField]
    private int foregroundPropSortingOrder = 30;

    [Header("没有正式 Sprite 时的调试占位色")]
    [SerializeField]
    private Color floorClutterFallbackColor =
        new Color(0.40f, 0.82f, 1f, 0.72f);

    [SerializeField]
    private Color edgePropFallbackColor =
        new Color(1f, 0.82f, 0.25f, 0.72f);

    [SerializeField]
    private Color nearStructureFallbackColor =
        new Color(0.50f, 1f, 0.45f, 0.72f);

    [SerializeField]
    private Color foregroundFallbackColor =
        new Color(0.95f, 0.45f, 1f, 0.78f);

    public string ThemeId =>
        string.IsNullOrWhiteSpace(themeId)
            ? "Procedural_Medium_Default"
            : themeId.Trim();

    public int FloorClutterCount =>
        Mathf.Max(0, floorClutterCount);

    public int EdgePropCount =>
        Mathf.Max(0, edgePropCount);

    public int NearStructureCount =>
        Mathf.Max(0, nearStructureCount);

    public int ForegroundPropCount =>
        Mathf.Max(0, foregroundPropCount);

    public float PositionJitter =>
        Mathf.Clamp(positionJitter, 0f, 0.35f);

    public int GetSortingOrder(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return floorClutterSortingOrder;

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return edgePropSortingOrder;

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return nearStructureSortingOrder;

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return foregroundPropSortingOrder;

            default:
                return 1;
        }
    }

    public float GetScale(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return floorClutterScale;

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return edgePropScale;

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return nearStructureScale;

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return foregroundPropScale;

            default:
                return 0.4f;
        }
    }

    public Color GetFallbackColor(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return floorClutterFallbackColor;

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return edgePropFallbackColor;

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return nearStructureFallbackColor;

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return foregroundFallbackColor;

            default:
                return Color.white;
        }
    }

    public Sprite PickSprite(
        DreamProceduralDecorCategoryP1012A2 category,
        System.Random random)
    {
        List<Sprite> source = GetSpriteList(category);

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
        DreamProceduralDecorCategoryP1012A2 category)
    {
        List<Sprite> source = GetSpriteList(category);

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

    private List<Sprite> GetSpriteList(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return floorClutterSprites;

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return edgePropSprites;

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return nearStructureSprites;

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return foregroundPropSprites;

            default:
                return null;
        }
    }
}

public enum DreamProceduralDecorCategoryP1012A2
{
    FloorClutter = 0,
    EdgeProp = 1,
    NearStructure = 2,
    ForegroundProp = 3
}
