using UnityEngine;

/// <summary>
/// 单只敌人的外观控制。
///
/// Enemy 根物件只负责碰撞、移动与 AI；
/// Visual 子物件只负责 Sprite、尺寸、偏移、颜色与方向动画。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyVisual : MonoBehaviour
{
    [Header("目前外观")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite currentSprite;
    [SerializeField] private float visualWorldHeight = 0.8f;
    [SerializeField] private Vector2 visualOffset = Vector2.zero;
    [SerializeField] private Color visualColor = Color.white;
    [SerializeField] private int sortingOrder = 20;

    [Header("方向动画")]
    [SerializeField]
    private CharacterAnimationProfile animationProfile;

    [SerializeField]
    private DirectionalSpriteAnimator spriteAnimator;

    public SpriteRenderer Renderer => spriteRenderer;
    public DirectionalSpriteAnimator Animator => spriteAnimator;

    public void Initialize(
        Sprite sprite,
        Sprite fallbackSprite,
        float worldHeight,
        Vector2 offset,
        Color color,
        int order)
    {
        Initialize(
            sprite,
            fallbackSprite,
            worldHeight,
            offset,
            color,
            order,
            null);
    }

    public void Initialize(
        Sprite sprite,
        Sprite fallbackSprite,
        float worldHeight,
        Vector2 offset,
        Color color,
        int order,
        CharacterAnimationProfile profile)
    {
        EnsureVisualObject();

        currentSprite = sprite != null
            ? sprite
            : fallbackSprite;

        visualWorldHeight = Mathf.Max(0.05f, worldHeight);
        visualOffset = offset;
        visualColor = color;
        sortingOrder = order;
        animationProfile = profile;

        ApplyAppearance();
        ApplyAnimationProfile();
    }

    /// <summary>
    /// 运行时替换敌人静态图片。
    /// 有动画配置时，这张图片只作为缺失动画的备用图。
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

        if (spriteAnimator != null)
        {
            spriteAnimator.SetVisualWorldHeight(
                visualWorldHeight);
        }
    }

    public void SetOffset(Vector2 newOffset)
    {
        visualOffset = newOffset;
        ApplyAppearance();
    }

    public void SetAnimationProfile(
        CharacterAnimationProfile newProfile)
    {
        animationProfile = newProfile;
        ApplyAnimationProfile();
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

    private void ApplyAnimationProfile()
    {
        if (animationProfile == null ||
            spriteRenderer == null)
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.SetProfile(null);
            }

            return;
        }

        if (spriteAnimator == null)
        {
            spriteAnimator =
                GetComponent<DirectionalSpriteAnimator>();
        }

        if (spriteAnimator == null)
        {
            spriteAnimator =
                gameObject.AddComponent<
                    DirectionalSpriteAnimator>();
        }

        spriteAnimator.Initialize(
            animationProfile,
            spriteRenderer,
            visualWorldHeight);
    }
}
