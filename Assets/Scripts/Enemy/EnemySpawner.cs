using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 測試敵人生成器。
/// 包含外觀、生命、接觸傷害與目前 AI 參數。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [Header("生成數量")]
    [Min(0)]
    [SerializeField] private int enemyCount = 1;
    [SerializeField] private bool spawnNearPlayerFirst = true;
    [SerializeField] private bool excludeExitRoom = true;

    [Header("移動參數")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 3.2f;

    [Range(0.001f, 0.25f)]
    [SerializeField] private float waypointTolerance = 0.035f;

    [Min(0f)]
    [SerializeField] private float stopDistance = 0.72f;

    [Range(0.01f, 1f)]
    [SerializeField] private float lastPositionTolerance = 0.15f;

    [Header("索敵參數")]
    [Min(0.1f)]
    [SerializeField] private float detectionRadius = 20f;

    [Min(0.1f)]
    [SerializeField] private float loseTargetRadius = 24f;

    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("敵人生命")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 30f;

    [Min(0f)]
    [SerializeField] private float damageInvulnerabilityTime =
        0.1f;

    [Header("碰撞傷害")]
    [SerializeField] private bool enableContactDamage = true;

    [Min(0f)]
    [SerializeField] private float contactDamage = 10f;

    [Min(0f)]
    [SerializeField] private float contactDamageCooldown = 0.75f;

    [SerializeField] private DamageFactionMask contactDamageTargets =
        DamageFactionMask.Player;

    [Header("敵人外觀")]
    [SerializeField] private Sprite enemySprite;

    [Min(0.05f)]
    [SerializeField] private float visualWorldHeight = 0.8f;

    [SerializeField] private Vector2 visualOffset =
        Vector2.zero;

    [SerializeField] private Color visualColor =
        new Color(0.2f, 0.85f, 0.25f);

    [SerializeField] private int sortingOrder = 20;

    [Header("敵人碰撞")]
    [SerializeField] private Vector2 colliderSize =
        new Vector2(0.58f, 0.58f);

    [SerializeField] private Vector2 colliderOffset =
        Vector2.zero;

    private PhysicsMaterial2D frictionlessMaterial;
    private Sprite fallbackSprite;

    private void Awake()
    {
        ValidateSettings();
        CreateFrictionlessMaterial();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    public List<GameObject> SpawnTestEnemies(
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player)
    {
        List<GameObject> enemies =
            new List<GameObject>();

        if (layout == null ||
            layout.Rooms == null ||
            layout.Rooms.Count < 2 ||
            dungeonRoot == null ||
            dungeonRenderer == null ||
            player == null)
        {
            return enemies;
        }

        ValidateSettings();
        CreateFrictionlessMaterial();

        List<Vector2Int> candidateCells =
            GetCandidateRoomCenters(layout);

        int amount = Mathf.Clamp(
            enemyCount,
            0,
            candidateCells.Count);

        for (int i = 0; i < amount; i++)
        {
            GameObject enemy = CreateEnemy(
                candidateCells[i],
                i + 1,
                layout,
                dungeonRoot,
                dungeonRenderer,
                player);

            enemies.Add(enemy);
        }

        return enemies;
    }

    private GameObject CreateEnemy(
        Vector2Int spawnCell,
        int index,
        DungeonLayout layout,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        Transform player)
    {
        GameObject enemy =
            new GameObject("TestEnemy_" + index);

        enemy.transform.SetParent(dungeonRoot);
        enemy.transform.position =
            dungeonRenderer.CellToWorld(spawnCell);

        enemy.transform.localScale = Vector3.one;

        CreateHealth(enemy);
        CreateVisual(enemy);
        CreatePhysics(enemy);
        CreateContactDamage(enemy);

        EnemyPathfinder pathfinder =
            enemy.AddComponent<EnemyPathfinder>();

        pathfinder.Initialize(
            layout,
            dungeonRenderer.CellSize);

        EnemyDetection detection =
            enemy.AddComponent<EnemyDetection>();

        detection.Initialize(
            player,
            detectionRadius,
            loseTargetRadius,
            requireLineOfSight,
            obstacleMask);

        TestEnemyAI enemyAI =
            enemy.AddComponent<TestEnemyAI>();

        enemyAI.Initialize(
            player,
            pathfinder,
            detection,
            moveSpeed,
            waypointTolerance,
            stopDistance,
            lastPositionTolerance);

        return enemy;
    }

    private void CreateHealth(GameObject enemy)
    {
        Health health =
            enemy.AddComponent<Health>();

        health.Initialize(
            maxHealth,
            DamageFaction.Enemy,
            damageInvulnerabilityTime,
            true);
    }

    private void CreateContactDamage(GameObject enemy)
    {
        if (!enableContactDamage)
        {
            return;
        }

        ContactDamage2D contactDamageComponent =
            enemy.AddComponent<ContactDamage2D>();

        contactDamageComponent.Initialize(
            contactDamage,
            contactDamageCooldown,
            contactDamageTargets);
    }

    private void CreateVisual(GameObject enemy)
    {
        EnemyVisual enemyVisual =
            enemy.AddComponent<EnemyVisual>();

        Sprite spriteToUse = enemySprite != null
            ? enemySprite
            : GetFallbackSprite();

        enemyVisual.Initialize(
            spriteToUse,
            GetFallbackSprite(),
            visualWorldHeight,
            visualOffset,
            visualColor,
            sortingOrder);
    }

    private void CreatePhysics(GameObject enemy)
    {
        BoxCollider2D enemyCollider =
            enemy.AddComponent<BoxCollider2D>();

        enemyCollider.size = colliderSize;
        enemyCollider.offset = colliderOffset;
        enemyCollider.sharedMaterial =
            frictionlessMaterial;

        Rigidbody2D body =
            enemy.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation =
            RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
        body.sleepMode =
            RigidbodySleepMode2D.NeverSleep;
    }

    private List<Vector2Int> GetCandidateRoomCenters(
        DungeonLayout layout)
    {
        List<Vector2Int> centers =
            new List<Vector2Int>();

        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            RectInt room = layout.Rooms[i];

            Vector2Int center = new Vector2Int(
                room.xMin + room.width / 2,
                room.yMin + room.height / 2);

            if (center == layout.StartCell)
            {
                continue;
            }

            if (excludeExitRoom &&
                center == layout.ExitCell)
            {
                continue;
            }

            centers.Add(center);
        }

        if (spawnNearPlayerFirst)
        {
            centers.Sort((first, second) =>
                Manhattan(layout.StartCell, first)
                    .CompareTo(
                        Manhattan(layout.StartCell, second)));
        }
        else
        {
            Shuffle(
                centers,
                new System.Random(
                    layout.Seed ^ 19349663));
        }

        return centers;
    }

    private static void Shuffle<T>(
        IList<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);

            T temporary = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temporary;
        }
    }

    private static int Manhattan(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
    }

    private void ValidateSettings()
    {
        enemyCount = Mathf.Max(0, enemyCount);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);

        waypointTolerance = Mathf.Clamp(
            waypointTolerance,
            0.001f,
            0.25f);

        stopDistance = Mathf.Max(0f, stopDistance);

        lastPositionTolerance = Mathf.Clamp(
            lastPositionTolerance,
            0.01f,
            1f);

        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        loseTargetRadius = Mathf.Max(
            detectionRadius,
            loseTargetRadius);

        maxHealth = Mathf.Max(1f, maxHealth);
        damageInvulnerabilityTime = Mathf.Max(
            0f,
            damageInvulnerabilityTime);

        contactDamage = Mathf.Max(0f, contactDamage);
        contactDamageCooldown = Mathf.Max(
            0f,
            contactDamageCooldown);

        visualWorldHeight = Mathf.Max(
            0.05f,
            visualWorldHeight);

        colliderSize.x = Mathf.Max(
            0.05f,
            colliderSize.x);

        colliderSize.y = Mathf.Max(
            0.05f,
            colliderSize.y);
    }

    private void CreateFrictionlessMaterial()
    {
        if (frictionlessMaterial != null)
        {
            return;
        }

        frictionlessMaterial =
            new PhysicsMaterial2D("Enemy_NoFriction");

        frictionlessMaterial.friction = 0f;
        frictionlessMaterial.bounciness = 0f;
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

        texture.name = "EnemyFallbackTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        fallbackSprite.name =
            "EnemyFallbackSprite";

        return fallbackSprite;
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }

        if (fallbackSprite != null)
        {
            Texture2D texture =
                fallbackSprite.texture;

            Destroy(fallbackSprite);

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
