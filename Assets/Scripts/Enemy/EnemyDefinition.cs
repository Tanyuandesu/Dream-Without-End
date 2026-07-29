using System.Collections.Generic;
using UnityEngine;

public enum EnemyGameplayRole
{
    Wanderer = 0,
    Scout = 1,
    Hunter = 2,
    Brute = 3,
    Gazer = 4
}

public enum EnemyAttackMode
{
    Melee = 0,
    Projectile = 1
}

/// <summary>
/// Authoritative data for one real enemy type.
/// EA1 consumes the legacy-compatible fields immediately; patrol, active
/// attacks and special behaviour fields are stable inputs for later phases.
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyDefinition",
    menuName = "Dream Dungeon/Enemy System/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [Header("Stable identity")]
    [SerializeField] private EnemyId enemyId =
        new EnemyId("enemy_unassigned");

    [SerializeField] private string displayName =
        "Unassigned Enemy";

    [SerializeField] private EnemyGameplayRole gameplayRole =
        EnemyGameplayRole.Wanderer;

    [Tooltip("NPCs and decorative actors must leave this disabled.")]
    [SerializeField] private bool countsForEnding = true;

    [TextArea(2, 5)]
    [SerializeField] private string gameplaySummary = string.Empty;

    [Header("Vitals")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 30f;

    [Min(0f)]
    [SerializeField] private float damageInvulnerabilityTime = 0.1f;

    [Header("Movement and navigation")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 3.2f;

    [Range(0.001f, 0.25f)]
    [SerializeField] private float waypointTolerance = 0.035f;

    [Min(0f)]
    [SerializeField] private float stopDistance = 0.72f;

    [Range(0.01f, 1f)]
    [SerializeField] private float lastPositionTolerance = 0.15f;

    [Tooltip("Maximum patrol selection radius around HomeAnchor, measured in grid cells.")]
    [Min(0)]
    [SerializeField] private int patrolRadiusInCells = 6;

    [Tooltip("Pause after reaching a patrol point before choosing the next one.")]
    [Min(0f)]
    [SerializeField] private float patrolPauseDuration = 0.35f;

    [Tooltip(
        "Maximum A* route cost accepted while tracking the player. " +
        "If a chase route exceeds this limit, the enemy gives up and " +
        "returns home. Measured in grid-cell movement cost.")]
    [Min(1)]
    [SerializeField] private int maximumChasePathCost = 32;

    [Tooltip("Radius around the last known target cell used by Search movement.")]
    [Min(0)]
    [SerializeField] private int searchRadiusInCells = 2;

    [Min(0f)]
    [SerializeField] private float searchDuration = 3f;

    [Header("EA3 navigation stability")]
    [Min(0.02f)]
    [SerializeField] private float minimumRepathInterval = 0.08f;

    [Min(0.1f)]
    [SerializeField] private float stuckTimeout = 0.75f;

    [Min(0.001f)]
    [SerializeField] private float stuckMovementThreshold = 0.015f;

    [Range(1, 8)]
    [SerializeField] private int maximumRecoveryAttempts = 3;

    [Min(0.05f)]
    [SerializeField] private float maximumRecoverySnapDistance = 0.8f;

    [Min(0.05f)]
    [SerializeField] private float failedPathRetryDelay = 0.5f;

    [Header("Perception")]
    [Min(0.1f)]
    [SerializeField] private float detectionRadius = 20f;

    [Min(0.1f)]
    [SerializeField] private float loseTargetRadius = 24f;

    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Tooltip(
        "Initial target-acquisition cone in degrees. Once acquired, " +
        "Lose Target Radius and line of sight control retention.")]
    [Range(1f, 360f)]
    [SerializeField] private float viewAngle = 360f;

    [SerializeField] private bool broadcastsAlert;

    [Min(0f)]
    [SerializeField] private float alertRadius = 0f;

    [Header("Nonlethal knockback response")]
    [SerializeField]
    private KnockbackResistanceSettings knockbackResistance =
        KnockbackResistanceSettings.CreateDefault();

    [Tooltip(
        "Base pause after collision-safe knockback movement finishes. " +
        "Each resistance tier scales this value with its Stagger Multiplier.")]
    [Min(0f)]
    [SerializeField]
    private float postKnockbackPauseDuration = 0.22f;

    [Tooltip(
        "Duration of the temporary navigation speed bonus after the " +
        "post-knockback pause completes. Zero disables the bonus.")]
    [Min(0f)]
    [SerializeField]
    private float postKnockbackPursuitDuration = 0.5f;

    [Tooltip(
        "Temporary navigation speed multiplier after the pause. " +
        "One disables acceleration without changing the rest of the sequence.")]
    [Min(1f)]
    [SerializeField]
    private float postKnockbackPursuitSpeedMultiplier = 1.2f;

    [SerializeField, HideInInspector]
    private bool cb4KnockbackRecoveryInitialized;

    [Header("Direct-attack weak hit response")]
    [Tooltip(
        "Per-enemy multiplier for the player's weak direct-attack nudge. " +
        "Zero disables direct-attack movement for this enemy.")]
    [Range(0f, 2f)]
    [SerializeField]
    private float directAttackWeakDisplacementMultiplier = 1f;

    [Tooltip(
        "Per-enemy multiplier for the player's weak direct-attack Hit pause. " +
        "Zero disables the pause for this enemy.")]
    [Range(0f, 2f)]
    [SerializeField]
    private float directAttackWeakHitPauseMultiplier = 1f;

    [SerializeField, HideInInspector]
    private bool cb7DirectHitResponseInitialized;

    [Header("Temporary health bar presentation")]
    [Tooltip("Per-enemy switch layered on top of the EnemySpawner master switch.")]
    [SerializeField]
    private bool temporaryHealthBarEnabled = true;

    [Tooltip("Per-enemy multiplier for the shared health bar width and height.")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float temporaryHealthBarSizeMultiplier = 1f;

    [Tooltip("Additional local-space offset applied after the shared health bar offset.")]
    [SerializeField]
    private Vector2 temporaryHealthBarOffset = Vector2.zero;

    [SerializeField, HideInInspector]
    private bool cb95TemporaryHealthBarInitialized;

    [Header("Formal attack contract (activated in combat phase)")]
    [SerializeField] private EnemyAttackMode attackMode =
        EnemyAttackMode.Melee;

    [Min(0f)]
    [SerializeField] private float attackDamage = 5f;

    [Min(0f)]
    [SerializeField] private float attackRange = 0.9f;

    [Min(0f)]
    [SerializeField] private float attackWindup = 0.25f;

    [Min(0f)]
    [SerializeField] private float attackRecovery = 0.45f;

    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.75f;

    [Min(0f)]
    [SerializeField] private float projectileSpeed = 0f;

    [Header("EA1 compatibility contact damage")]
    [Tooltip(
        "Temporary bridge that preserves the CA1 baseline. " +
        "The formal Attack state replaces it in a later phase.")]
    [SerializeField] private bool enableLegacyContactDamage = true;

    [Min(0f)]
    [SerializeField] private float legacyContactDamage = 5f;

    [Min(0f)]
    [SerializeField] private float legacyContactDamageCooldown = 0.75f;

    [SerializeField] private DamageFactionMask legacyContactDamageTargets =
        DamageFactionMask.Player;

    [Header("Presentation")]
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private CharacterAnimationProfile animationProfile;

    [Min(0.05f)]
    [SerializeField] private float visualWorldHeight = 0.8f;

    [SerializeField] private Vector2 visualOffset = Vector2.zero;
    [SerializeField] private Color visualColor = Color.white;
    [SerializeField] private int sortingOrder = 20;

    [Header("Collision")]
    [SerializeField] private Vector2 colliderSize =
        new Vector2(0.58f, 0.58f);

    [SerializeField] private Vector2 colliderOffset = Vector2.zero;

    public EnemyId Id => enemyId;
    public string DisplayName => displayName;
    public EnemyGameplayRole GameplayRole => gameplayRole;
    public bool CountsForEnding => countsForEnding;
    public string GameplaySummary => gameplaySummary;

    public float MaxHealth => maxHealth;
    public float DamageInvulnerabilityTime => damageInvulnerabilityTime;
    public float MoveSpeed => moveSpeed;
    public float WaypointTolerance => waypointTolerance;
    public float StopDistance => stopDistance;
    public float LastPositionTolerance => lastPositionTolerance;
    public int PatrolRadiusInCells => patrolRadiusInCells;
    public float PatrolPauseDuration => patrolPauseDuration;
    public int MaximumChasePathCost => maximumChasePathCost;
    public int SearchRadiusInCells => searchRadiusInCells;
    public float MinimumRepathInterval =>
        minimumRepathInterval > 0f
            ? minimumRepathInterval
            : 0.08f;

    public float StuckTimeout =>
        stuckTimeout > 0f
            ? stuckTimeout
            : 0.75f;

    public float StuckMovementThreshold =>
        stuckMovementThreshold > 0f
            ? stuckMovementThreshold
            : 0.015f;

    public int MaximumRecoveryAttempts =>
        maximumRecoveryAttempts > 0
            ? maximumRecoveryAttempts
            : 3;

    public float MaximumRecoverySnapDistance =>
        maximumRecoverySnapDistance > 0f
            ? maximumRecoverySnapDistance
            : 0.8f;

    public float FailedPathRetryDelay =>
        failedPathRetryDelay > 0f
            ? failedPathRetryDelay
            : 0.5f;

    public float SearchDuration => searchDuration;

    public KnockbackResistanceSettings KnockbackResistance
    {
        get
        {
            EnsureKnockbackSettings();
            return knockbackResistance;
        }
    }

    public float PostKnockbackPauseDuration
    {
        get
        {
            EnsureKnockbackRecoverySettings();
            return postKnockbackPauseDuration;
        }
    }

    public float PostKnockbackPursuitDuration
    {
        get
        {
            EnsureKnockbackRecoverySettings();
            return postKnockbackPursuitDuration;
        }
    }

    public float PostKnockbackPursuitSpeedMultiplier
    {
        get
        {
            EnsureKnockbackRecoverySettings();
            return postKnockbackPursuitSpeedMultiplier;
        }
    }

    public float DirectAttackWeakDisplacementMultiplier
    {
        get
        {
            EnsureDirectHitResponseSettings();
            return directAttackWeakDisplacementMultiplier;
        }
    }

    public float DirectAttackWeakHitPauseMultiplier
    {
        get
        {
            EnsureDirectHitResponseSettings();
            return directAttackWeakHitPauseMultiplier;
        }
    }

    public bool TemporaryHealthBarEnabled
    {
        get
        {
            EnsureTemporaryHealthBarSettings();
            return temporaryHealthBarEnabled;
        }
    }

    public float TemporaryHealthBarSizeMultiplier
    {
        get
        {
            EnsureTemporaryHealthBarSettings();
            return temporaryHealthBarSizeMultiplier;
        }
    }

    public Vector2 TemporaryHealthBarOffset
    {
        get
        {
            EnsureTemporaryHealthBarSettings();
            return temporaryHealthBarOffset;
        }
    }

    public float DetectionRadius => detectionRadius;
    public float LoseTargetRadius => loseTargetRadius;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask ObstacleMask => obstacleMask;
    public float ViewAngle => viewAngle;
    public bool BroadcastsAlert => broadcastsAlert;
    public float AlertRadius => alertRadius;

    public EnemyAttackMode AttackMode => attackMode;
    public float AttackDamage => attackDamage;
    public float AttackRange => attackRange;
    public float AttackWindup => attackWindup;
    public float AttackRecovery => attackRecovery;
    public float AttackCooldown => attackCooldown;
    public float ProjectileSpeed => projectileSpeed;

    public bool EnableLegacyContactDamage =>
        enableLegacyContactDamage;

    public float LegacyContactDamage => legacyContactDamage;
    public float LegacyContactDamageCooldown =>
        legacyContactDamageCooldown;

    public DamageFactionMask LegacyContactDamageTargets =>
        legacyContactDamageTargets;

    public Sprite EnemySprite => enemySprite;
    public CharacterAnimationProfile AnimationProfile =>
        animationProfile;

    public float VisualWorldHeight => visualWorldHeight;
    public Vector2 VisualOffset => visualOffset;
    public Color VisualColor => visualColor;
    public int SortingOrder => sortingOrder;
    public Vector2 ColliderSize => colliderSize;
    public Vector2 ColliderOffset => colliderOffset;

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        if (!enemyId.IsValid)
        {
            errors.Add(
                name + ": EnemyId is empty or contains unsupported characters.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add(name + ": Display Name is empty.");
        }

        if (loseTargetRadius < detectionRadius)
        {
            errors.Add(
                name + ": Lose Target Radius must be at least Detection Radius.");
        }

        if (patrolRadiusInCells < 0)
        {
            errors.Add(name + ": Patrol Radius In Cells cannot be negative.");
        }

        if (patrolPauseDuration < 0f)
        {
            errors.Add(name + ": Patrol Pause Duration cannot be negative.");
        }

        if (searchRadiusInCells < 0)
        {
            errors.Add(name + ": Search Radius In Cells cannot be negative.");
        }

        if (searchDuration < 0f)
        {
            errors.Add(name + ": Search Duration cannot be negative.");
        }

        if (animationProfile == null)
        {
            errors.Add(
                name + ": Animation Profile is not assigned. " +
                "Static sprite fallback remains valid, but CA1 animation will be absent.");
        }

        if (attackMode == EnemyAttackMode.Projectile &&
            projectileSpeed <= 0f)
        {
            errors.Add(
                name + ": Projectile attack requires Projectile Speed above zero.");
        }

        if (postKnockbackPauseDuration < 0f)
        {
            errors.Add(
                name + ": Post Knockback Pause Duration cannot be negative.");
        }

        if (postKnockbackPursuitDuration < 0f)
        {
            errors.Add(
                name + ": Post Knockback Pursuit Duration cannot be negative.");
        }

        if (postKnockbackPursuitSpeedMultiplier < 1f)
        {
            errors.Add(
                name + ": Post Knockback Pursuit Speed Multiplier must be at least one.");
        }

        if (directAttackWeakDisplacementMultiplier < 0f ||
            directAttackWeakDisplacementMultiplier > 2f)
        {
            errors.Add(
                name + ": Direct Attack Weak Displacement Multiplier must be within 0..2.");
        }

        if (directAttackWeakHitPauseMultiplier < 0f ||
            directAttackWeakHitPauseMultiplier > 2f)
        {
            errors.Add(
                name + ": Direct Attack Weak Hit Pause Multiplier must be within 0..2.");
        }

        if (temporaryHealthBarSizeMultiplier <= 0f)
        {
            errors.Add(
                name + ": Temporary Health Bar Size Multiplier must be above zero.");
        }

        EnsureKnockbackSettings();
        EnsureKnockbackRecoverySettings();
        EnsureDirectHitResponseSettings();
        EnsureTemporaryHealthBarSettings();
        knockbackResistance.CollectValidationErrors(
            errors,
            name);
    }

    private void EnsureKnockbackSettings()
    {
        if (knockbackResistance == null)
        {
            knockbackResistance =
                KnockbackResistanceSettings.CreateDefault();
        }
    }


    private void EnsureKnockbackRecoverySettings()
    {
        if (!cb4KnockbackRecoveryInitialized)
        {
            // Older EnemyDefinition assets had no post-displacement pause.
            // Give them CB4's safe default while preserving any pursuit values
            // already authored during CB2/CB3.
            if (postKnockbackPauseDuration <= 0f)
            {
                postKnockbackPauseDuration = 0.22f;
            }

            cb4KnockbackRecoveryInitialized = true;
        }

        postKnockbackPauseDuration = Mathf.Max(
            0f,
            postKnockbackPauseDuration);

        postKnockbackPursuitDuration = Mathf.Max(
            0f,
            postKnockbackPursuitDuration);

        postKnockbackPursuitSpeedMultiplier = Mathf.Max(
            1f,
            postKnockbackPursuitSpeedMultiplier);
    }


    private void EnsureDirectHitResponseSettings()
    {
        if (!cb7DirectHitResponseInitialized)
        {
            // Existing assets predate CB7. Preserve deliberately authored
            // zeroes only after the initialization marker has been written.
            if (directAttackWeakDisplacementMultiplier <= 0f &&
                directAttackWeakHitPauseMultiplier <= 0f)
            {
                directAttackWeakDisplacementMultiplier = 1f;
                directAttackWeakHitPauseMultiplier = 1f;
            }

            cb7DirectHitResponseInitialized = true;
        }

        directAttackWeakDisplacementMultiplier = Mathf.Clamp(
            directAttackWeakDisplacementMultiplier,
            0f,
            2f);

        directAttackWeakHitPauseMultiplier = Mathf.Clamp(
            directAttackWeakHitPauseMultiplier,
            0f,
            2f);
    }

    private void EnsureTemporaryHealthBarSettings()
    {
        if (!cb95TemporaryHealthBarInitialized)
        {
            temporaryHealthBarEnabled = true;

            if (temporaryHealthBarSizeMultiplier <= 0f)
            {
                temporaryHealthBarSizeMultiplier = 1f;
            }

            cb95TemporaryHealthBarInitialized = true;
        }

        temporaryHealthBarSizeMultiplier = Mathf.Clamp(
            temporaryHealthBarSizeMultiplier,
            0.1f,
            3f);
    }

    private void OnValidate()
    {
        enemyId = EnemyId.From(enemyId.Value);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }

        maxHealth = Mathf.Max(1f, maxHealth);
        damageInvulnerabilityTime =
            Mathf.Max(0f, damageInvulnerabilityTime);

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

        patrolRadiusInCells = Mathf.Max(0, patrolRadiusInCells);
        maximumChasePathCost = Mathf.Max(1, maximumChasePathCost);
        minimumRepathInterval = minimumRepathInterval > 0f
            ? Mathf.Max(0.02f, minimumRepathInterval)
            : 0.08f;

        stuckTimeout = stuckTimeout > 0f
            ? Mathf.Max(0.1f, stuckTimeout)
            : 0.75f;

        stuckMovementThreshold = stuckMovementThreshold > 0f
            ? Mathf.Max(0.001f, stuckMovementThreshold)
            : 0.015f;

        maximumRecoveryAttempts = maximumRecoveryAttempts > 0
            ? Mathf.Clamp(maximumRecoveryAttempts, 1, 8)
            : 3;

        maximumRecoverySnapDistance =
            maximumRecoverySnapDistance > 0f
                ? Mathf.Max(
                    0.05f,
                    maximumRecoverySnapDistance)
                : 0.8f;

        failedPathRetryDelay = failedPathRetryDelay > 0f
            ? Mathf.Max(0.05f, failedPathRetryDelay)
            : 0.5f;

        searchDuration = Mathf.Max(0f, searchDuration);

        EnsureKnockbackSettings();
        knockbackResistance.EnsureValid();
        EnsureKnockbackRecoverySettings();
        EnsureDirectHitResponseSettings();
        EnsureTemporaryHealthBarSettings();

        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        loseTargetRadius = Mathf.Max(
            detectionRadius,
            loseTargetRadius);

        viewAngle = Mathf.Clamp(viewAngle, 1f, 360f);
        alertRadius = Mathf.Max(0f, alertRadius);

        attackDamage = Mathf.Max(0f, attackDamage);
        attackRange = Mathf.Max(0f, attackRange);
        attackWindup = Mathf.Max(0f, attackWindup);
        attackRecovery = Mathf.Max(0f, attackRecovery);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);

        legacyContactDamage = Mathf.Max(0f, legacyContactDamage);
        legacyContactDamageCooldown =
            Mathf.Max(0f, legacyContactDamageCooldown);

        visualWorldHeight = Mathf.Max(0.05f, visualWorldHeight);
        colliderSize.x = Mathf.Max(0.05f, colliderSize.x);
        colliderSize.y = Mathf.Max(0.05f, colliderSize.y);
    }
}
