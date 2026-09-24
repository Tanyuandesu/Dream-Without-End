using UnityEngine;

/// <summary>
/// Lightweight player projectile for Blood Shot.
/// It reuses the existing CombatHit / EnemyCombatReceiver contract and does
/// not depend on enemy projectile code or enemy targeting assumptions.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBloodProjectile : MonoBehaviour
{
    private const int SweepCapacity = 32;

    private static Sprite fallbackSprite;

    private readonly RaycastHit2D[] sweepHits =
        new RaycastHit2D[SweepCapacity];

    private GameObject owner;
    private CombatAttackId attackId;
    private Vector2 direction;
    private float speed;
    private float radius;
    private float expiresAt;
    private float damage;
    private float displacementDistance;
    private float displacementDuration;
    private float stunDuration;
    private bool initialized;

    public static PlayerBloodProjectile Spawn(
        GameObject owner,
        CombatAttackId attackId,
        Vector2 position,
        Vector2 direction,
        BloodShotSettings settings)
    {
        if (owner == null || !attackId.IsValid || settings == null)
        {
            return null;
        }

        GameObject go = new GameObject("PlayerBloodProjectile");
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = settings.ProjectileSprite != null
            ? settings.ProjectileSprite
            : GetFallbackSprite();
        renderer.color = settings.ProjectileColor;
        renderer.sortingOrder = 40;

        FitVisualSize(
            go.transform,
            renderer.sprite,
            settings.ProjectileVisualSize);

        PlayerBloodProjectile projectile =
            go.AddComponent<PlayerBloodProjectile>();

        projectile.Initialize(
            owner,
            attackId,
            direction,
            settings);

        return projectile;
    }

    private void Initialize(
        GameObject newOwner,
        CombatAttackId newAttackId,
        Vector2 newDirection,
        BloodShotSettings settings)
    {
        owner = newOwner;
        attackId = newAttackId;
        direction = newDirection.sqrMagnitude > 0.0001f
            ? newDirection.normalized
            : Vector2.down;
        speed = settings.ProjectileSpeed;
        radius = settings.ProjectileRadius;
        expiresAt = Time.time + settings.ProjectileLifetime;
        damage = settings.Damage;
        displacementDistance = settings.DisplacementDistance;
        displacementDuration = settings.DisplacementDuration;
        stunDuration = settings.StunDuration;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || owner == null || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 start = transform.position;
        float distance = speed * Time.fixedDeltaTime;
        Vector2 end = start + direction * distance;

        int hitCount = Physics2D.CircleCastNonAlloc(
            start,
            radius,
            direction,
            sweepHits,
            distance,
            Physics2D.AllLayers);

        float bestDistance = float.PositiveInfinity;
        Collider2D bestCollider = null;
        EnemyCombatReceiver bestReceiver = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = sweepHits[i].collider;

            if (candidate == null || candidate.isTrigger || IsOwnedByPlayer(candidate))
            {
                continue;
            }

            float candidateDistance = Mathf.Max(0f, sweepHits[i].distance);

            EnemyCombatReceiver receiver =
                candidate.GetComponentInParent<EnemyCombatReceiver>();

            if (receiver != null && receiver.IsInitialized &&
                receiver.Health != null && !receiver.Health.IsDead)
            {
                if (candidateDistance < bestDistance)
                {
                    bestDistance = candidateDistance;
                    bestCollider = candidate;
                    bestReceiver = receiver;
                }

                continue;
            }

            Rigidbody2D attachedBody = candidate.attachedRigidbody;
            bool blocksShot = attachedBody == null ||
                              attachedBody.bodyType != RigidbodyType2D.Dynamic;

            if (blocksShot && candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestCollider = candidate;
                bestReceiver = null;
            }
        }

        if (bestCollider == null)
        {
            transform.position = end;
            return;
        }

        Vector2 impactPoint = start + direction * bestDistance;
        transform.position = impactPoint;

        if (bestReceiver != null)
        {
            ApplyHit(bestReceiver, impactPoint);
        }

        Destroy(gameObject);
    }

    private void ApplyHit(
        EnemyCombatReceiver receiver,
        Vector2 hitPoint)
    {
        CombatDisplacementRequest displacement =
            default(CombatDisplacementRequest);

        if (displacementDistance > 0f)
        {
            displacement = new CombatDisplacementRequest(
                attackId,
                direction,
                displacementDistance,
                displacementDuration,
                shouldCancelTimedNavigationSpeed: true);
        }

        CombatReactionRequest reaction =
            default(CombatReactionRequest);

        if (stunDuration > 0f)
        {
            reaction = new CombatReactionRequest(
                attackId,
                CombatReactionKind.Stunned,
                stunDuration,
                shouldExtendExistingReaction: true,
                shouldCancelTimedNavigationSpeed: true,
                newReason: "CB11 Blood Shot stun");
        }

        CombatHit hit = new CombatHit(
            attackId,
            CombatActionKind.BloodShot,
            owner,
            DamageFaction.Player,
            DamageAttribution.Player,
            hitPoint,
            direction,
            damage,
            displacement,
            reaction,
            shouldCountTowardKnockbackDecay: false,
            shouldTriggerPursuitRecovery: false);

        receiver.TryReceiveCombatHit(hit);
    }

    private bool IsOwnedByPlayer(Collider2D collider)
    {
        if (collider == null || owner == null)
        {
            return false;
        }

        Transform t = collider.transform;
        Transform root = owner.transform;
        return t == root || t.IsChildOf(root);
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "BloodShotFallbackTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        fallbackSprite.name = "BloodShotFallbackSprite";
        return fallbackSprite;
    }

    private static void FitVisualSize(
        Transform target,
        Sprite sprite,
        float worldSize)
    {
        if (target == null || sprite == null)
        {
            return;
        }

        float maxDimension = Mathf.Max(
            sprite.bounds.size.x,
            sprite.bounds.size.y);

        if (maxDimension <= 0.0001f)
        {
            return;
        }

        float scale = worldSize / maxDimension;
        target.localScale = new Vector3(scale, scale, 1f);
    }
}
