using UnityEngine;

/// <summary>
/// 第一次建立玩家。
/// Player 根物件負責生命、碰撞與移動；
/// Visual 子物件只負責角色圖片。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("角色圖片")]
    [Tooltip("把你的 2D 角色 Sprite 拖到這裡。留空時使用測試方塊。")]
    [SerializeField] private Sprite playerSprite;

    [Min(0.05f)]
    [SerializeField] private float visualWorldHeight = 0.9f;

    [SerializeField] private Vector2 visualOffset =
        new Vector2(0f, 0.12f);

    [SerializeField] private int sortingOrder = 20;

    [Header("測試方塊外觀")]
    [SerializeField] private Color fallbackColor =
        new Color(0.25f, 0.95f, 0.65f);

    [Header("玩家碰撞")]
    [SerializeField] private Vector2 colliderSize =
        new Vector2(0.52f, 0.58f);

    [SerializeField] private Vector2 colliderOffset =
        new Vector2(0f, -0.08f);

    [Header("玩家生命")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("玩家受到一次傷害後的無敵時間。")]
    [Min(0f)]
    [SerializeField] private float damageInvulnerabilityTime =
        0.35f;

    [Header("玩家移動")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 5f;

    private Sprite fallbackSprite;

    private void OnValidate()
    {
        visualWorldHeight =
            Mathf.Max(0.05f, visualWorldHeight);

        colliderSize.x =
            Mathf.Max(0.05f, colliderSize.x);

        colliderSize.y =
            Mathf.Max(0.05f, colliderSize.y);

        maxHealth = Mathf.Max(1f, maxHealth);
        damageInvulnerabilityTime =
            Mathf.Max(0f, damageInvulnerabilityTime);

        moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    public GameObject Spawn(
        Vector3 worldPosition,
        Transform playerSystemRoot)
    {
        GameObject player = new GameObject("Player");

        player.transform.SetParent(playerSystemRoot);
        player.transform.position = worldPosition;
        player.transform.localScale = Vector3.one;
        player.tag = "Player";

        CreateHealth(player);
        CreatePhysics(player);
        CreateMovement(player);
        CreateVisual(player.transform);

        return player;
    }

    private void CreateHealth(GameObject player)
    {
        Health health = player.AddComponent<Health>();

        health.Initialize(
            maxHealth,
            DamageFaction.Player,
            damageInvulnerabilityTime,
            false);
    }

    private void CreatePhysics(GameObject player)
    {
        BoxCollider2D playerCollider =
            player.AddComponent<BoxCollider2D>();

        playerCollider.size = colliderSize;
        playerCollider.offset = colliderOffset;

        Rigidbody2D body =
            player.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation =
            RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
    }

    private void CreateMovement(GameObject player)
    {
        RuntimeDungeonPlayer controller =
            player.AddComponent<RuntimeDungeonPlayer>();

        controller.Initialize(moveSpeed);
    }

    private void CreateVisual(Transform playerRoot)
    {
        GameObject visual = new GameObject("Visual");

        visual.transform.SetParent(playerRoot);
        visual.transform.localPosition = visualOffset;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer renderer =
            visual.AddComponent<SpriteRenderer>();

        renderer.sortingOrder = sortingOrder;

        if (playerSprite != null)
        {
            renderer.sprite = playerSprite;
            renderer.color = Color.white;

            FitSpriteHeight(
                visual.transform,
                playerSprite,
                visualWorldHeight);
        }
        else
        {
            renderer.sprite = GetFallbackSprite();
            renderer.color = fallbackColor;

            visual.transform.localScale =
                new Vector3(
                    visualWorldHeight,
                    visualWorldHeight,
                    1f);
        }
    }

    private static void FitSpriteHeight(
        Transform visual,
        Sprite sprite,
        float targetHeight)
    {
        float spriteHeight = sprite.bounds.size.y;

        if (spriteHeight <= 0.0001f)
        {
            visual.localScale = Vector3.one;
            return;
        }

        float uniformScale =
            targetHeight / spriteHeight;

        visual.localScale =
            new Vector3(
                uniformScale,
                uniformScale,
                1f);
    }

    private Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false);

        texture.name = "PlayerFallbackTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        fallbackSprite.name = "PlayerFallbackSprite";

        return fallbackSprite;
    }

    private void OnDestroy()
    {
        if (fallbackSprite == null)
        {
            return;
        }

        Texture2D texture = fallbackSprite.texture;
        Destroy(fallbackSprite);

        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
