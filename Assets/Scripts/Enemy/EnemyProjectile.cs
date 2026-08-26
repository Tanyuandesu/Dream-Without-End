using System;
using UnityEngine;

public enum EnemyProjectileTerminationReason
{
    HitAccepted = 0,
    DamageRejected = 1,
    ObstacleImpact = 2,
    LifetimeExpired = 3
}

/// <summary>
/// Runtime projectile spawned by EnemyProjectileAttackController.
/// T6B.2 keeps one authoritative motion path, performs a deterministic swept
/// test against the known player target, and separately sweeps solid geometry.
/// The visible core and halo are children, so presentation scaling never
/// shrinks the Rigidbody2D or CircleCollider2D root.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class EnemyProjectile : MonoBehaviour
{
    private const int SweepResultCapacity = 32;

    [Header("T6B runtime contract")]
    [SerializeField] private bool initialized;
    [SerializeField] private string projectileInstanceId = string.Empty;
    [SerializeField] private float damage;
    [SerializeField] private float speed;
    [SerializeField] private float lifetime;
    [SerializeField] private float radius;
    [SerializeField] private Vector2 direction = Vector2.right;
    [SerializeField] private float spawnedAt;
    [SerializeField] private float travelledDistance;
    [SerializeField] private bool terminated;
    [SerializeField] private EnemyProjectileTerminationReason terminationReason;
    [SerializeField] private string lastOutcome = "Not initialized";

    [Header("T6B.2 swept-impact diagnostics")]
    [SerializeField] private int targetSweepQueryCount;
    [SerializeField] private int obstacleSweepQueryCount;
    [SerializeField] private int obstacleSweepCandidateCount;
    [SerializeField] private string lastSweepHitName = string.Empty;
    [SerializeField] private Vector2 lastWorldPosition;
    [SerializeField] private bool visibleCoreReady;
    [SerializeField] private bool visibleHaloReady;

    [Header("Runtime references")]
    [SerializeField] private GameObject source;
    [SerializeField] private Health targetHealth;
    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CircleCollider2D hitCollider;
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private SpriteRenderer haloRenderer;

    private LayerMask obstacleMask;

    private readonly RaycastHit2D[] obstacleSweepHits =
        new RaycastHit2D[SweepResultCapacity];

    public bool IsInitialized => initialized;
    public string ProjectileInstanceId => projectileInstanceId;
    public float Damage => damage;
    public float Speed => speed;
    public float Lifetime => lifetime;
    public float Radius => radius;
    public Vector2 Direction => direction;
    public float Age => initialized ? Mathf.Max(0f, Time.time - spawnedAt) : 0f;
    public float TravelledDistance => travelledDistance;
    public bool IsTerminated => terminated;
    public EnemyProjectileTerminationReason TerminationReason => terminationReason;
    public string LastOutcome => lastOutcome;
    public bool UsesSweptCollision => true;
    public bool UsesExplicitTargetSweep => true;
    public int SweepQueryCount => targetSweepQueryCount + obstacleSweepQueryCount;
    public int SweepCandidateCount => obstacleSweepCandidateCount;
    public string LastSweepHitName => lastSweepHitName;
    public Vector2 LastWorldPosition => lastWorldPosition;
    public bool VisibleCoreReady => visibleCoreReady;
    public bool VisibleHaloReady => visibleHaloReady;

    public event Action<EnemyProjectile, EnemyProjectileTerminationReason>
        Terminated;

    private static Sprite cachedCircleSprite;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        string newProjectileInstanceId,
        GameObject newSource,
        Health newTargetHealth,
        Collider2D newTargetCollider,
        Vector2 launchPosition,
        Vector2 launchDirection,
        float newDamage,
        float newSpeed,
        float newLifetime,
        float newRadius,
        float visualWorldSize,
        Color visualColor,
        int sortingOrder,
        LayerMask newObstacleMask)
    {
        CacheComponents();

        projectileInstanceId = string.IsNullOrWhiteSpace(
            newProjectileInstanceId)
                ? "enemy_projectile"
                : newProjectileInstanceId;

        source = newSource;
        targetHealth = newTargetHealth;
        targetCollider = newTargetCollider;
        damage = Mathf.Max(0f, newDamage);
        speed = Mathf.Max(0.01f, newSpeed);
        lifetime = Mathf.Max(0.05f, newLifetime);
        radius = Mathf.Max(0.01f, newRadius);
        obstacleMask = newObstacleMask;

        direction = launchDirection.sqrMagnitude > 0.0001f
            ? launchDirection.normalized
            : Vector2.right;

        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(
            launchPosition.x,
            launchPosition.y,
            source != null ? source.transform.position.z : 0f);

        body.position = launchPosition;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        hitCollider.isTrigger = true;
        hitCollider.radius = radius;
        hitCollider.offset = Vector2.zero;

        ConfigureVisuals(
            visualWorldSize,
            visualColor,
            sortingOrder);

        IgnoreSourceColliders();

        spawnedAt = Time.time;
        travelledDistance = 0f;
        terminated = false;
        terminationReason = EnemyProjectileTerminationReason.LifetimeExpired;
        lastOutcome = "Projectile active";
        targetSweepQueryCount = 0;
        obstacleSweepQueryCount = 0;
        obstacleSweepCandidateCount = 0;
        lastSweepHitName = string.Empty;
        lastWorldPosition = launchPosition;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || terminated)
        {
            return;
        }

        if (Time.time - spawnedAt >= lifetime)
        {
            Terminate(
                EnemyProjectileTerminationReason.LifetimeExpired,
                "Projectile lifetime expired");
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        Vector2 start = body.position;
        Vector2 end = start + direction * step;

        bool targetHit = TryFindTargetImpact(
            start,
            end,
            out float targetDistance,
            out Vector2 targetImpactPoint);

        bool obstacleHit = TryFindObstacleImpact(
            start,
            step,
            out float obstacleDistance,
            out Collider2D obstacleCollider);

        if (targetHit &&
            (!obstacleHit || targetDistance <= obstacleDistance))
        {
            MoveImmediately(targetImpactPoint, targetDistance);
            ResolvePlayerHit(targetHealth, targetCollider);
            return;
        }

        if (obstacleHit && obstacleCollider != null)
        {
            float safeDistance = Mathf.Max(0f, obstacleDistance - 0.001f);
            Vector2 obstacleImpactPoint = start + direction * safeDistance;
            MoveImmediately(obstacleImpactPoint, safeDistance);
            lastSweepHitName = obstacleCollider.gameObject.name;

            Terminate(
                EnemyProjectileTerminationReason.ObstacleImpact,
                "Projectile hit obstacle " +
                obstacleCollider.gameObject.name);
            return;
        }

        body.MovePosition(end);
        travelledDistance += step;
        lastWorldPosition = end;
    }

    private bool TryFindTargetImpact(
        Vector2 start,
        Vector2 end,
        out float impactDistance,
        out Vector2 impactPoint)
    {
        impactDistance = float.PositiveInfinity;
        impactPoint = end;
        targetSweepQueryCount++;

        if (targetHealth == null || targetHealth.IsDead)
        {
            return false;
        }

        Bounds targetBounds;

        if (targetCollider != null && targetCollider.enabled)
        {
            targetBounds = targetCollider.bounds;
        }
        else
        {
            Vector3 targetPosition = targetHealth.transform.position;
            targetBounds = new Bounds(
                targetPosition,
                new Vector3(0.52f, 0.58f, 0.1f));
        }

        Vector2 expandedMin = new Vector2(
            targetBounds.min.x - radius,
            targetBounds.min.y - radius);

        Vector2 expandedMax = new Vector2(
            targetBounds.max.x + radius,
            targetBounds.max.y + radius);

        if (!TryIntersectSegmentAabb(
                start,
                end,
                expandedMin,
                expandedMax,
                out float enterT))
        {
            return false;
        }

        enterT = Mathf.Clamp01(enterT);
        Vector2 segment = end - start;
        impactPoint = start + segment * enterT;
        impactDistance = segment.magnitude * enterT;
        lastSweepHitName = targetHealth.gameObject.name;
        return true;
    }

    private bool TryFindObstacleImpact(
        Vector2 start,
        float distance,
        out float impactDistance,
        out Collider2D obstacleCollider)
    {
        impactDistance = float.PositiveInfinity;
        obstacleCollider = null;

        if (distance <= 0f)
        {
            return false;
        }

        obstacleSweepQueryCount++;

        int hitCount = Physics2D.CircleCastNonAlloc(
            start,
            radius,
            direction,
            obstacleSweepHits,
            distance,
            obstacleMask.value);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = obstacleSweepHits[i].collider;

            if (ShouldIgnoreCollider(candidate) ||
                candidate.isTrigger)
            {
                continue;
            }

            Health health = candidate.GetComponentInParent<Health>();

            if (health != null)
            {
                continue;
            }

            obstacleSweepCandidateCount++;
            float candidateDistance =
                Mathf.Max(0f, obstacleSweepHits[i].distance);

            if (candidateDistance >= impactDistance)
            {
                continue;
            }

            impactDistance = candidateDistance;
            obstacleCollider = candidate;
        }

        return obstacleCollider != null;
    }

    private static bool TryIntersectSegmentAabb(
        Vector2 start,
        Vector2 end,
        Vector2 boundsMin,
        Vector2 boundsMax,
        out float enterT)
    {
        enterT = 0f;
        float exitT = 1f;
        Vector2 delta = end - start;

        if (!ClipAxis(
                start.x,
                delta.x,
                boundsMin.x,
                boundsMax.x,
                ref enterT,
                ref exitT))
        {
            return false;
        }

        return ClipAxis(
            start.y,
            delta.y,
            boundsMin.y,
            boundsMax.y,
            ref enterT,
            ref exitT);
    }

    private static bool ClipAxis(
        float start,
        float delta,
        float minimum,
        float maximum,
        ref float enterT,
        ref float exitT)
    {
        if (Mathf.Abs(delta) <= 0.000001f)
        {
            return start >= minimum && start <= maximum;
        }

        float inverse = 1f / delta;
        float first = (minimum - start) * inverse;
        float second = (maximum - start) * inverse;

        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        enterT = Mathf.Max(enterT, first);
        exitT = Mathf.Min(exitT, second);
        return enterT <= exitT && exitT >= 0f && enterT <= 1f;
    }

    private bool ShouldIgnoreCollider(Collider2D candidate)
    {
        if (candidate == null ||
            candidate == hitCollider ||
            candidate.transform == transform ||
            candidate.transform.IsChildOf(transform))
        {
            return true;
        }

        if (targetCollider != null && candidate == targetCollider)
        {
            return true;
        }

        return source != null &&
               (candidate.gameObject == source ||
                candidate.transform.IsChildOf(source.transform));
    }

    private void ResolvePlayerHit(
        Health playerHealth,
        Collider2D playerCollider)
    {
        if (playerHealth == null || playerHealth.IsDead)
        {
            Terminate(
                EnemyProjectileTerminationReason.DamageRejected,
                "Projectile target unavailable at impact");
            return;
        }

        Vector2 hitPoint = playerCollider != null
            ? playerCollider.ClosestPoint(body.position)
            : body.position;

        DamageInfo info = new DamageInfo(
            damage,
            source,
            DamageFaction.Enemy,
            DamageAttribution.Enemy,
            hitPoint,
            direction);

        if (playerHealth.ApplyDamage(info))
        {
            Terminate(
                EnemyProjectileTerminationReason.HitAccepted,
                "Projectile hit accepted: " +
                damage.ToString("0.###"));
        }
        else
        {
            Terminate(
                EnemyProjectileTerminationReason.DamageRejected,
                "Projectile damage rejected by target Health");
        }
    }

    private void MoveImmediately(Vector2 position, float distanceAdded)
    {
        body.position = position;
        transform.position = new Vector3(
            position.x,
            position.y,
            transform.position.z);
        travelledDistance += Mathf.Max(0f, distanceAdded);
        lastWorldPosition = position;
    }

    private void Terminate(
        EnemyProjectileTerminationReason reason,
        string outcome)
    {
        if (terminated)
        {
            return;
        }

        terminated = true;
        terminationReason = reason;
        lastOutcome = string.IsNullOrWhiteSpace(outcome)
            ? reason.ToString()
            : outcome;

        Terminated?.Invoke(this, reason);
        Destroy(gameObject);
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (hitCollider == null)
        {
            hitCollider = GetComponent<CircleCollider2D>();
        }
    }

    private void ConfigureVisuals(
        float visualWorldSize,
        Color visualColor,
        int sortingOrder)
    {
        Sprite circleSprite = GetCircleSprite();
        float safeSize = Mathf.Max(0.08f, visualWorldSize);
        float spriteHeight = circleSprite != null
            ? Mathf.Max(0.0001f, circleSprite.bounds.size.y)
            : 1f;
        float coreScale = safeSize / spriteHeight;

        Transform core = transform.Find("ProjectileVisual");

        if (core == null)
        {
            GameObject coreObject = new GameObject("ProjectileVisual");
            core = coreObject.transform;
            core.SetParent(transform, false);
        }

        core.localPosition = Vector3.zero;
        core.localRotation = Quaternion.identity;
        core.localScale = new Vector3(coreScale, coreScale, 1f);

        coreRenderer = core.GetComponent<SpriteRenderer>();

        if (coreRenderer == null)
        {
            coreRenderer = core.gameObject.AddComponent<SpriteRenderer>();
        }

        coreRenderer.sprite = circleSprite;
        coreRenderer.color = visualColor;
        coreRenderer.sortingOrder = sortingOrder;
        coreRenderer.enabled = true;
        visibleCoreReady =
            coreRenderer.sprite != null &&
            coreRenderer.color.a > 0.001f;

        Transform halo = transform.Find("ProjectileHalo");

        if (halo == null)
        {
            GameObject haloObject = new GameObject("ProjectileHalo");
            halo = haloObject.transform;
            halo.SetParent(transform, false);
        }

        halo.localPosition = Vector3.zero;
        halo.localRotation = Quaternion.identity;
        float haloScale = coreScale * 1.85f;
        halo.localScale = new Vector3(haloScale, haloScale, 1f);

        haloRenderer = halo.GetComponent<SpriteRenderer>();

        if (haloRenderer == null)
        {
            haloRenderer = halo.gameObject.AddComponent<SpriteRenderer>();
        }

        Color haloColor = visualColor;
        haloColor.a = Mathf.Clamp01(visualColor.a * 0.32f);
        haloRenderer.sprite = circleSprite;
        haloRenderer.color = haloColor;
        haloRenderer.sortingOrder = sortingOrder - 1;
        haloRenderer.enabled = true;
        visibleHaloReady =
            haloRenderer.sprite != null &&
            haloRenderer.color.a > 0.001f;
    }

    private void IgnoreSourceColliders()
    {
        if (source == null || hitCollider == null)
        {
            return;
        }

        Collider2D[] sourceColliders =
            source.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < sourceColliders.Length; i++)
        {
            Collider2D sourceCollider = sourceColliders[i];

            if (sourceCollider != null)
            {
                Physics2D.IgnoreCollision(
                    hitCollider,
                    sourceCollider,
                    true);
            }
        }
    }

    private static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null)
        {
            return cachedCircleSprite;
        }

        const int size = 24;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false);

        texture.name = "EnemyProjectileCircle_Runtime";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32 transparent = new Color32(255, 255, 255, 0);
        Color32 opaque = new Color32(255, 255, 255, 255);
        float center = (size - 1) * 0.5f;
        float radiusPixels = size * 0.43f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                bool inside = dx * dx + dy * dy <=
                              radiusPixels * radiusPixels;

                texture.SetPixel(x, y, inside ? opaque : transparent);
            }
        }

        texture.Apply(false, true);

        cachedCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        cachedCircleSprite.name = "EnemyProjectileCircle_Runtime";
        cachedCircleSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedCircleSprite;
    }
}
