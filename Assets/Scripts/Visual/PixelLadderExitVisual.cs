using UnityEngine;

/// <summary>
/// 用程序化矩形拼出一个简易“向下梯子”入口，
/// 让出口在中等房间里不再与普通占位方块混淆。
/// </summary>
[DisallowMultipleComponent]
public sealed class PixelLadderExitVisual : MonoBehaviour
{
    [Header("层级")]
    [SerializeField] private int sortingOrder = 10;

    [Header("颜色")]
    [SerializeField] private Color holeColor =
        new Color(0.10f, 0.10f, 0.13f, 1f);

    [SerializeField] private Color innerShadowColor =
        new Color(0.18f, 0.18f, 0.23f, 1f);

    [SerializeField] private Color woodColor =
        new Color(0.58f, 0.42f, 0.23f, 1f);

    [SerializeField] private Color woodHighlightColor =
        new Color(0.74f, 0.58f, 0.33f, 1f);

    private bool built;

    public void Build(float cellSize, int newSortingOrder)
    {
        sortingOrder = newSortingOrder;

        if (built)
        {
            ClearChildren();
        }

        float size = Mathf.Max(0.25f, cellSize);
        float z = 0f;

        ProceduralSpriteUtility.CreateRect(
            "HoleBackdrop",
            transform,
            new Vector3(0f, 0f, z),
            new Vector2(size * 0.84f, size * 0.84f),
            holeColor,
            sortingOrder);

        ProceduralSpriteUtility.CreateRect(
            "HoleInner",
            transform,
            new Vector3(0f, -size * 0.02f, z),
            new Vector2(size * 0.62f, size * 0.62f),
            innerShadowColor,
            sortingOrder + 1);

        float railWidth = size * 0.10f;
        float railHeight = size * 0.48f;
        float railOffsetX = size * 0.14f;
        float railOffsetY = -size * 0.02f;

        ProceduralSpriteUtility.CreateRect(
            "RailLeft",
            transform,
            new Vector3(-railOffsetX, railOffsetY, z),
            new Vector2(railWidth, railHeight),
            woodColor,
            sortingOrder + 2);

        ProceduralSpriteUtility.CreateRect(
            "RailRight",
            transform,
            new Vector3(railOffsetX, railOffsetY, z),
            new Vector2(railWidth, railHeight),
            woodColor,
            sortingOrder + 2);

        float rungWidth = size * 0.38f;
        float rungHeight = size * 0.08f;
        float firstRungY = size * 0.13f;
        float rungGap = size * 0.16f;

        for (int i = 0; i < 3; i++)
        {
            float rungY = firstRungY - (i * rungGap);

            ProceduralSpriteUtility.CreateRect(
                "Rung_" + i,
                transform,
                new Vector3(0f, rungY, z),
                new Vector2(rungWidth, rungHeight),
                woodHighlightColor,
                sortingOrder + 3);
        }

        built = true;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (Application.isPlaying)
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
