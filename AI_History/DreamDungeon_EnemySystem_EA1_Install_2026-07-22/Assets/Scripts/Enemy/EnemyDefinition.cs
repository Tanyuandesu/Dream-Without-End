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

    [Tooltip("Maximum A* path cost from HomeAnchor used by future patrol.")]
    [Min(0)]
    [SerializeField] private int patrolRadiusInCells = 6;

    [Tooltip("Maximum A* path cost from HomeAnchor used by future chase.")]
    [Min(1)]
    [SerializeField] private int maximumChasePathCost = 32;

    [Min(0f)]
    [SerializeField] private float searchDuration = 3f;

    [Header("Perception")]
    [Min(0.1f)]
    [SerializeField] private float detectionRadius = 20f;

    [Min(0.1f)]
    [SerializeField] private float loseTargetRadius = 24f;

    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Range(1f, 360f)]
    [SerializeField] private float viewAngle = 360f;

    [SerializeField] private bool broadcastsAlert;

    [Min(0f)]
    [SerializeField] private float alertRadius = 0f;

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
    public int MaximumChasePathCost => maximumChasePathCost;
    public float SearchDuration => searchDuration;

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
        searchDuration = Mathf.Max(0f, searchDuration);

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
