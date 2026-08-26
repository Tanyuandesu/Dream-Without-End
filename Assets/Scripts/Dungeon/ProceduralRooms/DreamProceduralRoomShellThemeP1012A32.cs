using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-3.2：中精度房间 Shell / Floor / Wall / Socket Transition 的可替换视觉 Theme。
///
/// Theme 永远不拥有碰撞与导航：
/// - Floor / Wall 只是 SpriteRenderer。
/// - Socket Transition 只是门口视觉过渡。
/// - 真实 Wall / Door / R2B Hard Structure 碰撞继续由既有系统负责。
///
/// 资源约定：
/// - Floor / Transition：正方形 1 Cell 基础图。
/// - Wall：标准朝向为水平墙段，运行时对 E/W 自动旋转 90°。
/// </summary>
[CreateAssetMenu(
    fileName = "ProceduralMediumShellTheme",
    menuName = "Dream Dungeon/Procedural Room/Shell Floor Wall Theme")]
public sealed class DreamProceduralRoomShellThemeP1012A32 :
    ScriptableObject
{
    [SerializeField]
    private string themeId =
        "Procedural_Medium_Shell_Default";

    [Header("可替换 Sprite Pool")]
    [SerializeField]
    private List<Sprite> floorSprites =
        new List<Sprite>();

    [Tooltip("标准朝向为水平墙段。")]
    [SerializeField]
    private List<Sprite> wallSprites =
        new List<Sprite>();

    [SerializeField]
    private List<Sprite> transitionSprites =
        new List<Sprite>();

    [Header("Floor")]
    [Range(0.85f, 1.15f)]
    [SerializeField]
    private float floorScale = 1.02f;

    [SerializeField]
    private int floorSortingOrder = -10;

    [Header("Wall")]
    [Range(0.08f, 0.65f)]
    [SerializeField]
    private float wallThickness = 0.22f;

    [Range(0.85f, 1.20f)]
    [SerializeField]
    private float wallLength = 1.03f;

    [SerializeField]
    private int wallSortingOrder = 5;

    [Header("Socket Transition")]
    [Tooltip("门内延伸多少格。推荐 1~2。")]
    [Range(0, 3)]
    [SerializeField]
    private int transitionInsideDepth = 2;

    [Tooltip("门外覆盖多少格，用于压住 Corridor 第一格的接缝。推荐 1。")]
    [Range(0, 2)]
    [SerializeField]
    private int transitionOutsideDepth = 1;

    [Range(0.85f, 1.15f)]
    [SerializeField]
    private float transitionScale = 1.03f;

    [SerializeField]
    private int transitionSortingOrder = -9;

    [Header("空 Pool 调试占位色")]
    [SerializeField]
    private Color floorFallbackColor =
        new Color(0.24f, 0.31f, 0.40f, 1f);

    [SerializeField]
    private Color wallFallbackColor =
        new Color(0.10f, 0.13f, 0.18f, 1f);

    [SerializeField]
    private Color transitionFallbackColor =
        new Color(0.30f, 0.38f, 0.46f, 1f);

    public string ThemeId =>
        string.IsNullOrWhiteSpace(themeId)
            ? "Procedural_Medium_Shell_Default"
            : themeId.Trim();

    public float FloorScale =>
        Mathf.Clamp(floorScale, 0.85f, 1.15f);

    public int FloorSortingOrder =>
        floorSortingOrder;

    public float WallThickness =>
        Mathf.Clamp(wallThickness, 0.08f, 0.65f);

    public float WallLength =>
        Mathf.Clamp(wallLength, 0.85f, 1.20f);

    public int WallSortingOrder =>
        wallSortingOrder;

    public int TransitionInsideDepth =>
        Mathf.Clamp(transitionInsideDepth, 0, 3);

    public int TransitionOutsideDepth =>
        Mathf.Clamp(transitionOutsideDepth, 0, 2);

    public float TransitionScale =>
        Mathf.Clamp(transitionScale, 0.85f, 1.15f);

    public int TransitionSortingOrder =>
        transitionSortingOrder;

    public Color FloorFallbackColor =>
        floorFallbackColor;

    public Color WallFallbackColor =>
        wallFallbackColor;

    public Color TransitionFallbackColor =>
        transitionFallbackColor;

    public Sprite PickFloor(
        System.Random random)
    {
        return Pick(
            floorSprites,
            random);
    }

    public Sprite PickWall(
        System.Random random)
    {
        return Pick(
            wallSprites,
            random);
    }

    public Sprite PickTransition(
        System.Random random)
    {
        return Pick(
            transitionSprites,
            random);
    }

    public int FloorSpriteCount =>
        CountValid(floorSprites);

    public int WallSpriteCount =>
        CountValid(wallSprites);

    public int TransitionSpriteCount =>
        CountValid(transitionSprites);

    private static Sprite Pick(
        List<Sprite> source,
        System.Random random)
    {
        if (source == null ||
            source.Count == 0)
        {
            return null;
        }

        List<Sprite> valid =
            new List<Sprite>();

        for (int i = 0;
             i < source.Count;
             i++)
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

    private static int CountValid(
        List<Sprite> source)
    {
        if (source == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < source.Count;
             i++)
        {
            if (source[i] != null)
            {
                count++;
            }
        }

        return count;
    }
}
