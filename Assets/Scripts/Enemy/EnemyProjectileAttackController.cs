using System;
using UnityEngine;

public enum EnemyProjectileAttackPhase
{
    Idle = 0,
    Windup = 10,
    Recovery = 20
}

/// <summary>
/// T6B formal projectile attack sequence for one projectile-profile enemy.
/// Definition values remain authoritative; the controller owns only runtime
/// timing, projectile creation and diagnostic counters.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(EnemyMotor2D))]
[RequireComponent(typeof(Health))]
public sealed class EnemyProjectileAttackController : MonoBehaviour
{
    [Header("T6B runtime sequence (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private EnemyProjectileAttackPhase phase =
        EnemyProjectileAttackPhase.Idle;
    [SerializeField] private float attackStartedAt = -1f;
    [SerializeField] private float projectileSpawnAt = -1f;
    [SerializeField] private float recoveryEndsAt = -1f;
    [SerializeField] private float nextAttackAllowedAt;
    [SerializeField] private float lastAttackDistance = -1f;
    [SerializeField] private Vector2 lastAimDirection = Vector2.right;
    [SerializeField] private string lastAttackOutcome = "Not initialized";
    [SerializeField] private string lastProjectileInstanceId = string.Empty;

    [Header("T6B activity counters")]
    [SerializeField] private int attackStartCount;
    [SerializeField] private int projectileSpawnCount;
    [SerializeField] private int projectileHitCount;
    [SerializeField] private int projectileMissCount;
    [SerializeField] private int damageRejectedCount;
    [SerializeField] private int obstacleImpactCount;
    [SerializeField] private int projectileExpiredCount;
    [SerializeField] private int attackCompleteCount;
    [SerializeField] private int attackCancelCount;
    [SerializeField] private int activeProjectileCount;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private Health sourceHealth;
    [SerializeField] private Health targetHealth;
    [SerializeField] private Collider2D sourceCollider;
    [SerializeField] private Collider2D targetCollider;

    private int projectileSequence;

    public bool IsInitialized => initialized;
    public EnemyProjectileAttackPhase Phase => phase;
    public bool IsAttackActive => phase != EnemyProjectileAttackPhase.Idle;
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
    public float ConfiguredProjectileSpeed => Definition != null
        ? Mathf.Max(0f, Definition.ProjectileSpeed)
        : 0f;
    public float ConfiguredProjectileLifetime => Definition != null
        ? Mathf.Max(0f, Definition.ProjectileLifetime)
        : 0f;
    public float ConfiguredProjectileRadius => Definition != null
        ? Mathf.Max(0f, Definition.ProjectileRadius)
        : 0f;
    public float ConfiguredProjectileVisualSize => Definition != null
        ? Mathf.Max(0f, Definition.ProjectileVisualSize)
        : 0f;
    public float ConfiguredMinimumRange => Definition != null
        ? Mathf.Max(0f, Definition.ProjectileMinimumRange)
        : 0f;
    public int ConfiguredRetreatSearchRadiusInCells => Definition != null
        ? Mathf.Max(0, Definition.ProjectileRetreatSearchRadiusInCells)
        : 0;
    public Color ConfiguredProjectileColor => Definition != null
        ? Definition.ProjectileColor
        : Color.white;

    public float AttackStartedAt => attackStartedAt;
    public float ProjectileSpawnAt => projectileSpawnAt;
    public float RecoveryEndsAt => recoveryEndsAt;
    public float NextAttackAllowedAt => nextAttackAllowedAt;
    public float CooldownRemaining => Mathf.Max(0f, nextAttackAllowedAt - Time.time);
    public float LastAttackDistance => lastAttackDistance;
    public Vector2 LastAimDirection => lastAimDirection;
    public string LastAttackOutcome => lastAttackOutcome;
    public string LastProjectileInstanceId => lastProjectileInstanceId;
    public int AttackStartCount => attackStartCount;
    public int ProjectileSpawnCount => projectileSpawnCount;
    public int ProjectileHitCount => projectileHitCount;
    public int ProjectileMissCount => projectileMissCount;
    public int DamageRejectedCount => damageRejectedCount;
    public int ObstacleImpactCount => obstacleImpactCount;
    public int ProjectileExpiredCount => projectileExpiredCount;
    public int AttackCompleteCount => attackCompleteCount;
    public int AttackCancelCount => attackCancelCount;
    public int ActiveProjectileCount => activeProjectileCount;

    public float PhaseRemainingTime
    {
        get
        {
            switch (phase)
            {
                case EnemyProjectileAttackPhase.Windup:
                    return Mathf.Max(0f, projectileSpawnAt - Time.time);
                case EnemyProjectileAttackPhase.Recovery:
                    return Mathf.Max(0f, recoveryEndsAt - Time.time);
                default:
                    return 0f;
            }
        }
    }

    public bool IsTargetBelowPreferredMinimumRange =>
        CalculateTargetDistance() < ConfiguredMinimumRange;

    public bool CanBeginAttack =>
        initialized &&
        !IsAttackActive &&
        sourceHealth != null &&
        !sourceHealth.IsDead &&
        targetHealth != null &&
        !targetHealth.IsDead &&
        Definition != null &&
        Definition.AttackMode == EnemyAttackMode.Projectile &&
        ConfiguredDamage > 0f &&
        ConfiguredRange > 0f &&
        ConfiguredProjectileSpeed > 0f &&
        ConfiguredProjectileLifetime > 0f &&
        ConfiguredProjectileRadius > 0f &&
        Time.time >= nextAttackAllowedAt &&
        context != null &&
        context.Detection != null &&
        context.Detection.IsTargetDetected &&
        IsTargetWithinConfiguredRange() &&
        HasRequiredLineOfSight();

    public event Action<EnemyProjectileAttackController> AttackStarted;
    public event Action<EnemyProjectileAttackController> ProjectileSpawned;
    public event Action<EnemyProjectileAttackController> AttackCompleted;
    public event Action<EnemyProjectileAttackController> AttackCancelled;

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
            Definition.AttackMode == EnemyAttackMode.Projectile;

        ResetAttackRuntime();
        nextAttackAllowedAt = 0f;
        projectileSequence = 0;
        attackStartCount = 0;
        projectileSpawnCount = 0;
        projectileHitCount = 0;
        projectileMissCount = 0;
        damageRejectedCount = 0;
        obstacleImpactCount = 0;
        projectileExpiredCount = 0;
        attackCompleteCount = 0;
        attackCancelCount = 0;
        activeProjectileCount = 0;
        lastAttackDistance = -1f;
        lastAimDirection = Vector2.right;
        lastProjectileInstanceId = string.Empty;
        lastAttackOutcome = initialized
            ? "T6B formal projectile ready"
            : "T6B runtime references incomplete";
    }

    public bool TryBeginAttack()
    {
        RefreshTargetReferences();

        if (!CanBeginAttack)
        {
            return false;
        }

        FaceCurrentTarget();
        phase = EnemyProjectileAttackPhase.Windup;
        attackStartedAt = Time.time;
        projectileSpawnAt = attackStartedAt + ConfiguredWindup;
        recoveryEndsAt = -1f;
        nextAttackAllowedAt = attackStartedAt + ConfiguredCooldown;
        attackStartCount++;
        lastAttackDistance = CalculateTargetDistance();
        lastAttackOutcome = "Projectile windup started";

        AttackStarted?.Invoke(this);
        return true;
    }

    public bool TickAttack()
    {
        if (!initialized || !IsAttackActive)
        {
            return false;
        }

        FaceCurrentTarget();

        if (phase == EnemyProjectileAttackPhase.Windup &&
            Time.time >= projectileSpawnAt)
        {
            SpawnProjectileOnce();
            phase = EnemyProjectileAttackPhase.Recovery;
            recoveryEndsAt = Time.time + ConfiguredRecovery;
        }

        if (phase == EnemyProjectileAttackPhase.Recovery &&
            Time.time >= recoveryEndsAt)
        {
            phase = EnemyProjectileAttackPhase.Idle;
            attackCompleteCount++;
            lastAttackOutcome = string.IsNullOrWhiteSpace(lastAttackOutcome)
                ? "Projectile attack completed"
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
            ? "Projectile attack cancelled"
            : reason;

        ResetAttackRuntime();
        AttackCancelled?.Invoke(this);
        return true;
    }

    public bool IsTargetWithinConfiguredRange()
    {
        return CalculateTargetDistance() <= ConfiguredRange;
    }

    private void SpawnProjectileOnce()
    {
        RefreshTargetReferences();
        lastAttackDistance = CalculateTargetDistance();

        EnemyDetection detection = context != null
            ? context.Detection
            : null;

        if (targetHealth == null || targetHealth.IsDead)
        {
            projectileMissCount++;
            lastAttackOutcome = "Miss: target health unavailable";
            return;
        }

        if (detection == null || !detection.IsTargetDetected)
        {
            projectileMissCount++;
            lastAttackOutcome = "Miss: target no longer detected";
            return;
        }

        if (lastAttackDistance > ConfiguredRange)
        {
            projectileMissCount++;
            lastAttackOutcome =
                "Miss: target left projectile range (" +
                lastAttackDistance.ToString("0.###") + ")";
            return;
        }

        if (!HasRequiredLineOfSight())
        {
            projectileMissCount++;
            lastAttackOutcome = "Miss: projectile line of sight blocked";
            return;
        }

        Vector2 aimDirection = ResolveDirectionToTarget();
        lastAimDirection = aimDirection;
        motor.FaceDirection(aimDirection);

        Vector2 launchOrigin = ResolveLaunchOrigin(aimDirection);
        projectileSequence++;

        string ownerId = context != null && context.Identity != null
            ? context.Identity.InstanceId
            : gameObject.name;

        string projectileId =
            ownerId + "_projectile_" + projectileSequence.ToString("D3");

        GameObject projectileObject = new GameObject(projectileId);
        projectileObject.layer = gameObject.layer;

        Transform projectileParent = transform.parent;
        projectileObject.transform.SetParent(projectileParent, true);

        projectileObject.AddComponent<Rigidbody2D>();
        projectileObject.AddComponent<CircleCollider2D>();

        EnemyProjectile projectile =
            projectileObject.AddComponent<EnemyProjectile>();

        projectile.Terminated += HandleProjectileTerminated;
        projectile.Initialize(
            projectileId,
            gameObject,
            targetHealth,
            targetCollider,
            launchOrigin,
            aimDirection,
            ConfiguredDamage,
            ConfiguredProjectileSpeed,
            ConfiguredProjectileLifetime,
            ConfiguredProjectileRadius,
            ConfiguredProjectileVisualSize,
            ConfiguredProjectileColor,
            Definition != null ? Definition.SortingOrder + 2 : 22,
            Definition != null
                ? Definition.ObstacleMask
                : (LayerMask)~0);

        projectileSpawnCount++;
        activeProjectileCount++;
        lastProjectileInstanceId = projectileId;
        lastAttackOutcome = "Projectile spawned";
        ProjectileSpawned?.Invoke(this);
    }

    private void HandleProjectileTerminated(
        EnemyProjectile projectile,
        EnemyProjectileTerminationReason reason)
    {
        if (projectile != null)
        {
            projectile.Terminated -= HandleProjectileTerminated;
        }

        activeProjectileCount = Mathf.Max(0, activeProjectileCount - 1);

        switch (reason)
        {
            case EnemyProjectileTerminationReason.HitAccepted:
                projectileHitCount++;
                lastAttackOutcome = "Projectile hit accepted";
                break;
            case EnemyProjectileTerminationReason.DamageRejected:
                damageRejectedCount++;
                lastAttackOutcome = "Projectile damage rejected";
                break;
            case EnemyProjectileTerminationReason.ObstacleImpact:
                obstacleImpactCount++;
                projectileMissCount++;
                lastAttackOutcome = "Projectile blocked by obstacle";
                break;
            case EnemyProjectileTerminationReason.LifetimeExpired:
                projectileExpiredCount++;
                projectileMissCount++;
                lastAttackOutcome = "Projectile expired";
                break;
        }
    }

    private bool HasRequiredLineOfSight()
    {
        EnemyDetection detection = context != null
            ? context.Detection
            : null;

        return Definition == null ||
               !Definition.RequireLineOfSight ||
               (detection != null && detection.HasClearLineOfSightToTarget);
    }

    private void FaceCurrentTarget()
    {
        if (motor == null)
        {
            return;
        }

        Vector2 direction = ResolveDirectionToTarget();

        if (direction.sqrMagnitude > 0.0001f)
        {
            lastAimDirection = direction;
            motor.FaceDirection(direction);
        }
    }

    private Vector2 ResolveDirectionToTarget()
    {
        RefreshTargetReferences();

        if (targetHealth == null)
        {
            return motor != null
                ? motor.FacingDirection
                : Vector2.right;
        }

        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.bounds.center
            : transform.position;

        Vector2 targetPoint = targetCollider != null
            ? targetCollider.bounds.center
            : targetHealth.transform.position;

        Vector2 direction = targetPoint - sourcePoint;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (motor != null ? motor.FacingDirection : Vector2.right);
    }

    private Vector2 ResolveLaunchOrigin(Vector2 aimDirection)
    {
        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.bounds.center
            : transform.position;

        float sourceExtent = 0.3f;

        if (sourceCollider != null)
        {
            sourceExtent = Mathf.Max(
                sourceCollider.bounds.extents.x,
                sourceCollider.bounds.extents.y);
        }

        return sourcePoint + aimDirection *
            (sourceExtent + ConfiguredProjectileRadius + 0.04f);
    }

    private float CalculateTargetDistance()
    {
        RefreshTargetReferences();

        if (targetHealth == null)
        {
            return float.PositiveInfinity;
        }

        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.ClosestPoint(targetHealth.transform.position)
            : (Vector2)transform.position;

        Vector2 targetPoint = targetCollider != null
            ? targetCollider.ClosestPoint(transform.position)
            : (Vector2)targetHealth.transform.position;

        return Vector2.Distance(sourcePoint, targetPoint);
    }

    private void RefreshTargetReferences()
    {
        if (context == null || context.CurrentTarget == null)
        {
            targetHealth = null;
            targetCollider = null;
            return;
        }

        if (targetHealth == null ||
            targetHealth.transform != context.CurrentTarget)
        {
            targetHealth =
                context.CurrentTarget.GetComponentInParent<Health>();
        }

        if (targetCollider == null ||
            targetCollider.transform != context.CurrentTarget)
        {
            targetCollider =
                context.CurrentTarget.GetComponentInParent<Collider2D>();
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
            sourceCollider = GetComponent<Collider2D>();
        }
    }

    private void ResetAttackRuntime()
    {
        phase = EnemyProjectileAttackPhase.Idle;
        attackStartedAt = -1f;
        projectileSpawnAt = -1f;
        recoveryEndsAt = -1f;
    }
}
