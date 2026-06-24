using UnityEngine;

/// <summary>
/// 單隻敵人的外觀控制。
///
/// Enemy 根物件只負責碰撞、移動與 AI；
/// Visual 子物件只負責 Sprite、尺寸、偏移與顏色。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyVisual : MonoBehaviour
{
    [Header("目前外觀")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite currentSprite;
    [SerializeField] private float visualWorldHeight = 0.8f;
    [SerializeField] private Vector2 visualOffset = Vector2.zero;
    [SerializeField] private Color visualColor = Color.white;
    [SerializeField] private int sortingOrder = 20;

    public SpriteRenderer Renderer => spriteRenderer;

    public void Initialize(
        Sprite sprite,
        Sprite fallbackSprite,
        float worldHeight,
        Vector2 offset,
        Color color,
        int order)
    {
        EnsureVisualObject();

        currentSprite = sprite != null
            ? sprite
            : fallbackSprite;

        visualWorldHeight = Mathf.Max(0.05f, worldHeight);
        visualOffset = offset;
        visualColor = color;
        sortingOrder = order;

        ApplyAppearance();
    }

    /// <summary>
    /// 運行時替換敵人圖片。
    /// </summary>
    public void SetSprite(Sprite newSprite)
    {
        currentSprite = newSprite;
        ApplyAppearance();
    }

    public void SetColor(Color newColor)
    {
        visualColor = newColor;
        ApplyAppearance();
    }

    public void SetWorldHeight(float newWorldHeight)
    {
        visualWorldHeight = Mathf.Max(0.05f, newWorldHeight);
        ApplyAppearance();
    }

    public void SetOffset(Vector2 newOffset)
    {
        visualOffset = newOffset;
        ApplyAppearance();
    }

    private void EnsureVisualObject()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        Transform existingVisual =
            transform.Find("Visual");

        GameObject visualObject;

        if (existingVisual != null)
        {
            visualObject = existingVisual.gameObject;
        }
        else
        {
            visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform);
        }

        visualObject.transform.localRotation =
            Quaternion.identity;

        spriteRenderer =
            visualObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer =
                visualObject.AddComponent<SpriteRenderer>();
        }
    }

    private void ApplyAppearance()
    {
        EnsureVisualObject();

        spriteRenderer.sprite = currentSprite;
        spriteRenderer.color = visualColor;
        spriteRenderer.sortingOrder = sortingOrder;

        spriteRenderer.transform.localPosition =
            visualOffset;

        spriteRenderer.transform.localRotation =
            Quaternion.identity;

        if (currentSprite == null)
        {
            spriteRenderer.transform.localScale =
                Vector3.one;

            return;
        }

        float spriteHeight =
            currentSprite.bounds.size.y;

        if (spriteHeight <= 0.0001f)
        {
            spriteRenderer.transform.localScale =
                Vector3.one;

            return;
        }

        float uniformScale =
            visualWorldHeight / spriteHeight;

        spriteRenderer.transform.localScale =
            new Vector3(
                uniformScale,
                uniformScale,
                1f);
    }
}
