using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有測試敵人的可調參數集中在這個組件。
///
/// 修改 Enemy Count 後，需要按 R 或進入下一層，
/// 才會按照新數量重新生成。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [Header("生成數量")]
    [Min(0)]
    [SerializeField] private int enemyCount = 1;

    [Tooltip("開啟後，敵人會優先生成在靠近出生點的房間，方便測試。")]
    [SerializeField] private bool spawnNearPlayerFirst = true;

    [Tooltip("出生房與出口房不會生成敵人。")]
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

    [Tooltip("開啟視線判定時，哪些 Layer 可以阻擋視線。")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("外觀與碰撞")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float visualScale = 0.68f;

    [Tooltip("碰撞體佔一個地圖格子的比例。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float colliderScale = 0.62f;

    [SerializeField] private Color enemyColor =
        new Color(0.2f, 0.85f, 0.25f);

    private PhysicsMaterial2D frictionlessMaterial;

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
        GameObject enemy = dungeonRenderer.CreateSquare(
            "TestEnemy_" + index,
            spawnCell,
            enemyColor,
            dungeonRoot,
            20,
            false,
            visualScale);

        BoxCollider2D enemyCollider =
            enemy.AddComponent<BoxCollider2D>();

        // CreateSquare 會縮放整個根物件。
        // 這裡反向換算，讓世界中的碰撞尺寸仍由 Collider Scale 控制。
        enemyCollider.size = Vector2.one *
            (colliderScale / Mathf.Max(0.01f, visualScale));

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
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

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

        detectionRadius = Mathf.Max(
            0.1f,
            detectionRadius);

        loseTargetRadius = Mathf.Max(
            detectionRadius,
            loseTargetRadius);

        visualScale = Mathf.Clamp(
            visualScale,
            0.1f,
            1.5f);

        colliderScale = Mathf.Clamp(
            colliderScale,
            0.1f,
            1f);
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

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }
    }
}
