using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最簡單的 2D 接觸傷害。
///
/// 支援 Collision 與 Trigger。
/// 持續接觸時依照 Hit Cooldown 再次造成傷害，
/// 不會每個物理幀瘋狂扣血。
/// CB6 在傷害來源死亡時立即停用本元件，避免幀末銷毀前的屍體傷害。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ContactDamage2D : MonoBehaviour
{
    [Header("接觸傷害")]
    [Min(0f)]
    [SerializeField] private float damage = 10f;

    [Min(0f)]
    [SerializeField] private float hitCooldown = 0.75f;

    [SerializeField] private DamageFactionMask targetFactions =
        DamageFactionMask.Player;

    [Header("CB6 death safety (read only during Play Mode)")]
    [SerializeField] private bool disabledBecauseSourceDied;
    [SerializeField] private int sourceDeathDisableCount;

    private readonly Dictionary<int, float>
        nextHitTimeByTarget =
            new Dictionary<int, float>();

    private Health sourceHealth;
    private bool subscribedToSourceHealth;

    public float Damage => damage;
    public float HitCooldown => hitCooldown;
    public DamageFactionMask TargetFactions => targetFactions;

    public bool DisabledBecauseSourceDied =>
        disabledBecauseSourceDied;

    public int SourceDeathDisableCount =>
        sourceDeathDisableCount;

    private void Awake()
    {
        CacheSourceHealth();
        SubscribeToSourceHealth();
    }

    private void OnEnable()
    {
        CacheSourceHealth();
        SubscribeToSourceHealth();

        if (sourceHealth != null && sourceHealth.IsDead)
        {
            DisableForSourceDeath();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromSourceHealth();
        nextHitTimeByTarget.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSourceHealth();
    }

    public void Initialize(
        float newDamage,
        float newHitCooldown,
        DamageFactionMask newTargetFactions)
    {
        damage = Mathf.Max(0f, newDamage);
        hitCooldown = Mathf.Max(0f, newHitCooldown);
        targetFactions = newTargetFactions;

        CacheSourceHealth();
        SubscribeToSourceHealth();

        disabledBecauseSourceDied =
            sourceHealth != null && sourceHealth.IsDead;
        sourceDeathDisableCount = 0;

        if (disabledBecauseSourceDied)
        {
            DisableForSourceDeath();
        }
    }

    /// <summary>
    /// Explicit CB6 shutdown entry used by EnemyDeathLifecycle. The health
    /// subscription provides the same protection when this component is used
    /// outside the standard enemy spawner.
    /// </summary>
    public void DisableForSourceDeath()
    {
        if (!disabledBecauseSourceDied)
        {
            disabledBecauseSourceDied = true;
            sourceDeathDisableCount++;
        }

        nextHitTimeByTarget.Clear();

        if (enabled)
        {
            enabled = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(
                transform.position);

        TryDamage(collision.collider, hitPoint);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Vector2 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(
                transform.position);

        TryDamage(collision.collider, hitPoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(
            other,
            other.ClosestPoint(transform.position));
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(
            other,
            other.ClosestPoint(transform.position));
    }

    private void TryDamage(
        Collider2D other,
        Vector2 hitPoint)
    {
        if (damage <= 0f ||
            other == null ||
            disabledBecauseSourceDied ||
            (sourceHealth != null && sourceHealth.IsDead))
        {
            return;
        }

        Health targetHealth =
            other.GetComponentInParent<Health>();

        if (targetHealth == null ||
            targetHealth.IsDead ||
            targetHealth.gameObject == gameObject)
        {
            return;
        }

        if (!targetFactions.Contains(
            targetHealth.Faction))
        {
            return;
        }

        int targetId =
            targetHealth.GetInstanceID();

        if (nextHitTimeByTarget.TryGetValue(
                targetId,
                out float nextAllowedTime) &&
            Time.time < nextAllowedTime)
        {
            return;
        }

        DamageFaction sourceFaction =
            sourceHealth != null
                ? sourceHealth.Faction
                : DamageFaction.Neutral;

        Vector2 direction =
            ((Vector2)targetHealth.transform.position -
             (Vector2)transform.position).normalized;

        DamageInfo damageInfo = new DamageInfo(
            damage,
            gameObject,
            sourceFaction,
            hitPoint,
            direction);

        if (targetHealth.ApplyDamage(damageInfo))
        {
            nextHitTimeByTarget[targetId] =
                Time.time + hitCooldown;
        }
    }

    private void CacheSourceHealth()
    {
        if (sourceHealth == null)
        {
            sourceHealth = GetComponent<Health>();
        }
    }

    private void SubscribeToSourceHealth()
    {
        if (subscribedToSourceHealth || sourceHealth == null)
        {
            return;
        }

        sourceHealth.Died += HandleSourceDied;
        subscribedToSourceHealth = true;
    }

    private void UnsubscribeFromSourceHealth()
    {
        if (!subscribedToSourceHealth || sourceHealth == null)
        {
            subscribedToSourceHealth = false;
            return;
        }

        sourceHealth.Died -= HandleSourceDied;
        subscribedToSourceHealth = false;
    }

    private void HandleSourceDied(Health deadHealth)
    {
        DisableForSourceDeath();
    }
}
