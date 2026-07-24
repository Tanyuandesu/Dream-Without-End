using UnityEngine;

/// <summary>
/// 第一次建立玩家。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("角色圖片")]
    [SerializeField] private Sprite playerSprite;

    [Header("玩家方向动画")]
    [SerializeField]
    private CharacterAnimationProfile animationProfile;

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

    [Min(0f)]
    [SerializeField] private float damageInvulnerabilityTime =
        0.35f;

    [Header("玩家自動回血")]
    [SerializeField] private bool enableRegeneration = true;

    [Tooltip("最後一次受傷後，等待多少秒才進入回血狀態。")]
    [Min(0f)]
    [SerializeField] private float regenerationDelay = 3f;

    [Tooltip("進入回血狀態後，每隔多少秒恢復一次生命。")]
    [Min(0.01f)]
    [SerializeField] private float regenerationInterval = 1f;

    [Tooltip("每次回血恢復多少生命。")]
    [Min(0f)]
    [SerializeField] private float regenerationAmountPerTick = 5f;

    [Tooltip("開啟後，玩家必須先受傷才會開始回血。")]
    [SerializeField] private bool requireDamageBeforeRegeneration =
        true;

    [Header("玩家移動")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 5f;

    private Sprite fallbackSprite;

    private void OnValidate()
    {
        visualWorldHeight = Mathf.Max(0.05f, visualWorldHeight);
        colliderSize.x = Mathf.Max(0.05f, colliderSize.x);
        colliderSize.y = Mathf.Max(0.05f, colliderSize.y);
        maxHealth = Mathf.Max(1f, maxHealth);

        damageInvulnerabilityTime =
            Mathf.Max(0f, damageInvulnerabilityTime);

        regenerationDelay =
            Mathf.Max(0f, regenerationDelay);

        regenerationInterval =
            Mathf.Max(0.01f, regenerationInterval);

        regenerationAmountPerTick =
            Mathf.Max(0f, regenerationAmountPerTick);

        moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    public GameObject Spawn(
        Vector3 worldPosition,
        Transform playerSystemRoot)
    {
        GameObject player =
            new GameObject("Player");

        player.transform.SetParent(playerSystemRoot);
        player.transform.position = worldPosition;
        player.transform.localScale = Vector3.one;
        player.tag = "Player";

        CreateHealth(player);
        CreateRegeneration(player);
        CreatePhysics(player);
        CreateMovement(player);

        SpriteRenderer visualRenderer =
            CreateVisual(player.transform);

        CreateAnimation(
            player,
            visualRenderer);

        CreateCombatController(player);

        return player;
    }

    private void CreateHealth(GameObject player)
    {
        Health health =
            player.AddComponent<Health>();

        health.Initialize(
            maxHealth,
            DamageFaction.Player,
            damageInvulnerabilityTime,
            false);
    }

    private void CreateRegeneration(GameObject player)
    {
        HealthRegeneration regeneration =
            player.AddComponent<HealthRegeneration>();

        regeneration.Initialize(
            enableRegeneration,
            regenerationDelay,
            regenerationInterval,
            regenerationAmountPerTick,
            requireDamageBeforeRegeneration);
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

    private void CreateCombatController(GameObject player)
    {
        PlayerCombatController combatController =
            player.AddComponent<PlayerCombatController>();

        combatController.Initialize(
            player.GetComponent<RuntimeDungeonPlayer>(),
            player.GetComponent<Rigidbody2D>(),
            player.GetComponent<Health>(),
            player.GetComponent<DirectionalSpriteAnimator>());
    }

    private SpriteRenderer CreateVisual(Transform playerRoot)
    {
        GameObject visual =
            new GameObject("Visual");

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

        return renderer;
    }

    private void CreateAnimation(
        GameObject player,
        SpriteRenderer visualRenderer)
    {
        if (animationProfile == null ||
            visualRenderer == null)
        {
            return;
        }

        DirectionalSpriteAnimator animator =
            player.AddComponent<
                DirectionalSpriteAnimator>();

        animator.Initialize(
            animationProfile,
            visualRenderer,
            visualWorldHeight);
    }

    private static void FitSpriteHeight(
        Transform visual,
        Sprite sprite,
        float targetHeight)
    {
        float spriteHeight =
            sprite.bounds.size.y;

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

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name = "PlayerFallbackTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

        fallbackSprite.name =
            "PlayerFallbackSprite";

        return fallbackSprite;
    }

    private void OnDestroy()
    {
        if (fallbackSprite == null)
        {
            return;
        }

        Texture2D texture =
            fallbackSprite.texture;

        Destroy(fallbackSprite);

        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
