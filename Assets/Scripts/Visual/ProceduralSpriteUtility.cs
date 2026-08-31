using UnityEngine;

/// <summary>
/// 为少量运行时占位视觉提供简易程序化 Sprite。
/// 仅服务临时占位美术，不接管正式资源。
/// </summary>
public static class ProceduralSpriteUtility
{
    private static Sprite cachedWhiteSprite;
    private static Sprite cachedSoftCircleSprite;

    public static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite == null)
        {
            Texture2D texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            cachedWhiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        return cachedWhiteSprite;
    }

    public static Sprite GetSoftCircleSprite()
    {
        if (cachedSoftCircleSprite == null)
        {
            const int size = 32;

            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false);

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f);
            float radius = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pixel = new Vector2(x, y);
                    float distance = Vector2.Distance(pixel, center);
                    float normalized = Mathf.Clamp01(distance / radius);

                    float alpha = 1f - normalized;
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);

                    // 让中心更实，边缘更柔。
                    alpha = Mathf.Pow(alpha, 1.4f);

                    texture.SetPixel(
                        x,
                        y,
                        new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            cachedSoftCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
        }

        return cachedSoftCircleSprite;
    }

    public static GameObject CreateRect(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector2 localSize,
        Color color,
        int sortingOrder)
    {
        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        created.transform.localPosition = localPosition;
        created.transform.localRotation = Quaternion.identity;
        created.transform.localScale = new Vector3(
            Mathf.Max(0.001f, localSize.x),
            Mathf.Max(0.001f, localSize.y),
            1f);

        SpriteRenderer renderer =
            created.AddComponent<SpriteRenderer>();

        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return created;
    }
}
