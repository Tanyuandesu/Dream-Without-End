using System;
using UnityEngine;

public enum EnemyMeleeAttackPhase
{
    Idle = 0,
    Windup = 10,
    Recovery = 20
}

/// <summary>
/// T6A authoritative formal melee attack executor.
///
/// EnemyStateMachine owns when Attack begins and ends. This component owns
/// only the timed attack sequence and the single damage commit. Every combat
/// value is read from the bound EnemyDefinition, so Inspector authoring remains
/// the sole source of truth and GameplayRole never overrides behaviour.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(EnemyMotor2D))]
[RequireComponent(typeof(Health))]
public sealed class EnemyMeleeAttackController : MonoBehaviour
{
    [Header("T6A formal melee runtime (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private EnemyMeleeAttackPhase phase =
        EnemyMeleeAttackPhase.Idle;
    [SerializeField] private float attackStartedAt = -1f;
    [SerializeField] private float damageCommitAt = -1f;
    [SerializeField] private float recoveryEndsAt = -1f;
    [SerializeField] private float nextAttackAllowedAt;
    [SerializeField] private float lastAttackDistance = -1f;
    [SerializeField] private float lastDamageAmount;
    [SerializeField] private string lastAttackOutcome = "Not initialized";

    [Header("T6A counters")]
    [SerializeField] private int attackStartCount;
    [SerializeField] private int damageCommitCount;
    [SerializeField] private int damageAcceptedCount;
    [SerializeField] private int attackMissCount;
    [SerializeField] private int damageRejectedCount;
    [SerializeField] private int attackCompleteCount;
    [SerializeField] private int attackCancelCount;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private Health sourceHealth;
    [SerializeField] private Health targetHealth;
    [SerializeField] private Collider2D sourceCollider;
    [SerializeField] private Collider2D targetCollider;

    public bool IsInitialized => initialized;
    public EnemyMeleeAttackPhase Phase => phase;
    public bool IsAttackActive => phase != EnemyMeleeAttackPhase.Idle;
    public EnemyRuntimeContext Context => context;
    public EnemyStateMachine StateMachine => stateMachine;
    public Health TargetHealth => targetHealth;

    public float ConfiguredDamage => Definition != null
        ? Mathf.Max(0f, Definition.AttackDamage)
        : 0f;

    public float ConfiguredRange => Definition != null
        ? Mathf.Max(0f, Definition.AttackRange)
        : 0f;

    public float ConfiguredWindup => Definition != null
        ? Mathf.Max(0f, Definition.AttackWindup)
        : 0f;

    public float ConfiguredRecovery => Definition != null
        ? Mathf.Max(0f, Definition.AttackRecovery)
        : 0f;

    public float ConfiguredCooldown => Definition != null
        ? Mathf.Max(0f, Definition.AttackCooldown)
        : 0f;

    public float AttackStartedAt => attackStartedAt;
    public float DamageCommitAt => damageCommitAt;
    public float RecoveryEndsAt => recoveryEndsAt;
    public float NextAttackAllowedAt => nextAttackAllowedAt;
    public float CooldownRemaining => Mathf.Max(
        0f,
        nextAttackAllowedAt - Time.time);

    public float PhaseRemainingTime
    {
        get
        {
            switch (phase)
            {
                case EnemyMeleeAttackPhase.Windup:
                    return Mathf.Max(0f, damageCommitAt - Time.time);

                case EnemyMeleeAttackPhase.Recovery:
                    return Mathf.Max(0f, recoveryEndsAt - Time.time);

                default:
                    return 0f;
            }
        }
    }

    public float LastAttackDistance => lastAttackDistance;
    public float LastDamageAmount => lastDamageAmount;
    public string LastAttackOutcome => lastAttackOutcome;
    public int AttackStartCount => attackStartCount;
    public int DamageCommitCount => damageCommitCount;
    public int DamageAcceptedCount => damageAcceptedCount;
    public int AttackMissCount => attackMissCount;
    public int DamageRejectedCount => damageRejectedCount;
    public int AttackCompleteCount => attackCompleteCount;
    public int AttackCancelCount => attackCancelCount;

    public bool CanBeginAttack =>
        initialized &&
        !IsAttackActive &&
        sourceHealth != null &&
        !sourceHealth.IsDead &&
        targetHealth != null &&
        !targetHealth.IsDead &&
        Definition != null &&
        Definition.AttackMode == EnemyAttackMode.Melee &&
        ConfiguredDamage > 0f &&
        ConfiguredRange > 0f &&
        Time.time >= nextAttackAllowedAt &&
        context != null &&
        context.Detection != null &&
        context.Detection.IsTargetDetected &&
        IsTargetWithinConfiguredRange();

    public event Action<EnemyMeleeAttackController> AttackStarted;
    public event Action<EnemyMeleeAttackController> DamageCommitted;
    public event Action<EnemyMeleeAttackController> AttackCompleted;
    public event Action<EnemyMeleeAttackController> AttackCancelled;

    private EnemyDefinition Definition => context != null
        ? context.Definition
        : null;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        EnemyStateMachine newStateMachine)
    {
        context = newContext;
        stateMachine = newStateMachine;
        CacheComponents();
        RefreshTargetReferences();

        initialized =
            context != null &&
            context.IsInitialized &&
            stateMachine != null &&
            motor != null &&
            motor.IsInitialized &&
            sourceHealth != null &&
            targetHealth != null &&
            Definition != null &&
            Definition.AttackMode == EnemyAttackMode.Melee;

        ResetAttackRuntime();
        nextAttackAllowedAt = 0f;
        attackStartCount = 0;
        damageCommitCount = 0;
        damageAcceptedCount = 0;
        attackMissCount = 0;
        damageRejectedCount = 0;
        attackCompleteCount = 0;
        attackCancelCount = 0;
        lastAttackDistance = -1f;
        lastDamageAmount = 0f;
        lastAttackOutcome = initialized
            ? "T6A formal melee ready"
            : "T6A runtime references incomplete";
    }

    public bool TryBeginAttack()
    {
        RefreshTargetReferences();

        if (!CanBeginAttack)
        {
            return false;
        }

        Vector2 directionToTarget = ResolveDirectionToTarget();

        if (motor != null)
        {
            motor.FaceDirection(directionToTarget);
        }

        phase = EnemyMeleeAttackPhase.Windup;
        attackStartedAt = Time.time;
        damageCommitAt = attackStartedAt + ConfiguredWindup;
        recoveryEndsAt = -1f;

        // Cooldown is the minimum interval between attack starts. Recovery
        // remains an independent state lock, so the longer duration wins.
        nextAttackAllowedAt = attackStartedAt + ConfiguredCooldown;

        attackStartCount++;
        lastAttackDistance = CalculateTargetDistance();
        lastDamageAmount = 0f;
        lastAttackOutcome = "Windup started";

        AttackStarted?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Advances the active sequence. Returns true exactly once when Recovery
    /// finishes and EnemyStateMachine should leave Attack.
    /// </summary>
    public bool TickAttack()
    {
        if (!initialized || !IsAttackActive)
        {
            return false;
        }

        if (phase == EnemyMeleeAttackPhase.Windup &&
            Time.time >= damageCommitAt)
        {
            CommitDamageOnce();
            phase = EnemyMeleeAttackPhase.Recovery;
            recoveryEndsAt = Time.time + ConfiguredRecovery;
        }

        if (phase == EnemyMeleeAttackPhase.Recovery &&
            Time.time >= recoveryEndsAt)
        {
            phase = EnemyMeleeAttackPhase.Idle;
            attackCompleteCount++;
            lastAttackOutcome = string.IsNullOrWhiteSpace(lastAttackOutcome)
                ? "Attack completed"
                : lastAttackOutcome + " | Recovery completed";

            AttackCompleted?.Invoke(this);
            return true;
        }

        return false;
    }

    public bool CancelAttack(string reason)
    {
        if (!IsAttackActive)
        {
            return false;
        }

        attackCancelCount++;
        lastAttackOutcome = string.IsNullOrWhiteSpace(reason)
            ? "Attack cancelled"
            : reason;

        ResetAttackRuntime();
        AttackCancelled?.Invoke(this);
        return true;
    }

    public bool IsTargetWithinConfiguredRange()
    {
        return CalculateTargetDistance() <= ConfiguredRange;
    }

    private void CommitDamageOnce()
    {
        damageCommitCount++;
        RefreshTargetReferences();
        lastAttackDistance = CalculateTargetDistance();
        lastDamageAmount = 0f;

        if (targetHealth == null || targetHealth.IsDead)
        {
            attackMissCount++;
            lastAttackOutcome = "Miss: target health unavailable";
            DamageCommitted?.Invoke(this);
            return;
        }

        EnemyDetection detection = context != null
            ? context.Detection
            : null;

        if (detection == null || !detection.IsTargetDetected)
        {
            attackMissCount++;
            lastAttackOutcome = "Miss: target no longer detected";
            DamageCommitted?.Invoke(this);
            return;
        }

        if (lastAttackDistance > ConfiguredRange)
        {
            attackMissCount++;
            lastAttackOutcome =
                "Miss: target left attack range (" +
                lastAttackDistance.ToString("0.###") + ")";
            DamageCommitted?.Invoke(this);
            return;
        }

        if (Definition.RequireLineOfSight &&
            !detection.HasClearLineOfSightToTarget)
        {
            attackMissCount++;
            lastAttackOutcome = "Miss: line of sight blocked";
            DamageCommitted?.Invoke(this);
            return;
        }

        Vector2 direction = ResolveDirectionToTarget();
        Vector2 hitPoint = targetCollider != null
            ? targetCollider.ClosestPoint(transform.position)
            : (Vector2)targetHealth.transform.position;

        DamageInfo damageInfo = new DamageInfo(
            ConfiguredDamage,
            gameObject,
            DamageFaction.Enemy,
            DamageAttribution.Enemy,
            hitPoint,
            direction);

        if (targetHealth.ApplyDamage(damageInfo))
        {
            damageAcceptedCount++;
            lastDamageAmount = ConfiguredDamage;
            lastAttackOutcome =
                "Hit accepted: " + ConfiguredDamage.ToString("0.###");
        }
        else
        {
            damageRejectedCount++;
            lastAttackOutcome =
                "Damage rejected by target Health";
        }

        DamageCommitted?.Invoke(this);
    }

    private float CalculateTargetDistance()
    {
        RefreshTargetReferences();

        if (targetHealth == null)
        {
            return float.PositiveInfinity;
        }

        Vector2 sourcePosition = sourceCollider != null
            ? sourceCollider.bounds.center
            : (Vector2)transform.position;

        Vector2 closestTargetPoint = targetCollider != null
            ? targetCollider.ClosestPoint(sourcePosition)
            : (Vector2)targetHealth.transform.position;

        return Vector2.Distance(
            sourcePosition,
            closestTargetPoint);
    }

    private Vector2 ResolveDirectionToTarget()
    {
        if (targetHealth == null)
        {
            return motor != null
                ? motor.FacingDirection
                : Vector2.down;
        }

        Vector2 direction =
            (Vector2)targetHealth.transform.position -
            (Vector2)transform.position;

        return direction.sqrMagnitude > 0.000001f
            ? direction.normalized
            : motor != null
                ? motor.FacingDirection
                : Vector2.down;
    }

    private void RefreshTargetReferences()
    {
        Transform target = context != null
            ? context.CurrentTarget
            : null;

        if (target == null)
        {
            targetHealth = null;
            targetCollider = null;
            return;
        }

        if (targetHealth == null ||
            !targetHealth.transform.IsChildOf(target) &&
            targetHealth.transform != target)
        {
            targetHealth = target.GetComponentInParent<Health>();

            if (targetHealth == null)
            {
                targetHealth = target.GetComponentInChildren<Health>();
            }
        }

        if (targetCollider == null ||
            targetHealth == null ||
            !targetCollider.transform.IsChildOf(targetHealth.transform) &&
            targetCollider.transform != targetHealth.transform)
        {
            targetCollider = targetHealth != null
                ? targetHealth.GetComponentInChildren<Collider2D>()
                : target.GetComponentInChildren<Collider2D>();
        }
    }

    private void CacheComponents()
    {
        if (context == null)
        {
            context = GetComponent<EnemyRuntimeContext>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<EnemyStateMachine>();
        }

        if (motor == null)
        {
            motor = GetComponent<EnemyMotor2D>();
        }

        if (sourceHealth == null)
        {
            sourceHealth = GetComponent<Health>();
        }

        if (sourceCollider == null)
        {
            sourceCollider = motor != null
                ? motor.BodyCollider
                : GetComponent<Collider2D>();
        }
    }

    private void ResetAttackRuntime()
    {
        phase = EnemyMeleeAttackPhase.Idle;
        attackStartedAt = -1f;
        damageCommitAt = -1f;
        recoveryEndsAt = -1f;
    }

    private void OnDisable()
    {
        if (IsAttackActive)
        {
            CancelAttack("Attack cancelled because component was disabled");
        }
    }

    private void OnDrawGizmosSelected()
    {
        float range = Application.isPlaying
            ? ConfiguredRange
            : 0.9f;

        if (range <= 0f)
        {
            return;
        }

        Gizmos.DrawWireSphere(transform.position, range);

        if (Application.isPlaying && targetHealth != null)
        {
            Gizmos.DrawLine(
                transform.position,
                targetHealth.transform.position);
        }
    }
}
