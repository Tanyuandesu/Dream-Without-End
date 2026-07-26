using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side adapter for CombatHit messages.
/// It owns no Rigidbody2D movement and no path selection. Reactions are sent
/// to EnemyStateMachine and displacement requests are sent to EnemyMotor2D.
/// CB2 additionally owns the runtime resistance level for this enemy instance,
/// while all editable decay ratios remain in its EnemyDefinition.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(EnemyMotor2D))]
[RequireComponent(typeof(EnemyStateMachine))]
[RequireComponent(typeof(Health))]
public sealed class EnemyCombatReceiver : MonoBehaviour
{
    private const int RememberedAttackCapacity = 32;
    private const float MinimumScaledDuration = 0.001f;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private Health health;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private EnemyStateMachine stateMachine;

    [Header("CB2 repeated-push resistance (read only during Play Mode)")]
    [SerializeField] private bool hasAcceptedDecayPush;
    [SerializeField] private int currentKnockbackResistanceLevel;
    [SerializeField] private int highestKnockbackResistanceLevel;
    [SerializeField] private float lastQualifyingPushAt = -1f;
    [SerializeField] private float nextResistanceRecoveryAt = -1f;
    [SerializeField] private int qualifyingDecayPushCount;
    [SerializeField] private int fullStrengthPushCount;
    [SerializeField] private int decayedPushCount;
    [SerializeField] private int resistanceAdvanceCount;
    [SerializeField] private int resistanceRecoveryStepCount;
    [SerializeField] private float lastAppliedDistanceMultiplier = 1f;
    [SerializeField] private float lastAppliedStaggerMultiplier = 1f;
    [SerializeField] private float lowestAppliedDistanceMultiplier = 1f;
    [SerializeField] private float lowestAppliedStaggerMultiplier = 1f;
    [SerializeField] private float lastBaseDisplacementDistance;
    [SerializeField] private float lastResolvedDisplacementDistance;
    [SerializeField] private float lastBaseReactionDuration;
    [SerializeField] private float lastResolvedReactionDuration;

    [Header("Runtime diagnostics")]
    [SerializeField] private bool initialized;
    [SerializeField] private CombatAttackId lastAcceptedAttackId;
    [SerializeField] private CombatActionKind lastAcceptedActionKind;
    [SerializeField] private int acceptedHitCount;
    [SerializeField] private int acceptedNonlethalPushCount;
    [SerializeField] private int duplicateRejectCount;
    [SerializeField] private int busyRejectCount;
    [SerializeField] private CombatHit lastAcceptedHit;

    private readonly Queue<int> rememberedAttackOrder =
        new Queue<int>(RememberedAttackCapacity);

    private readonly HashSet<int> rememberedAttackIds =
        new HashSet<int>();

    public bool IsInitialized => initialized;
    public EnemyRuntimeContext Context => context;
    public EnemyDefinition Definition => definition;
    public Health Health => health;
    public EnemyMotor2D Motor => motor;
    public EnemyStateMachine StateMachine => stateMachine;
    public CombatAttackId LastAcceptedAttackId => lastAcceptedAttackId;
    public CombatActionKind LastAcceptedActionKind =>
        lastAcceptedActionKind;

    public int AcceptedHitCount => acceptedHitCount;
    public int AcceptedNonlethalPushCount =>
        acceptedNonlethalPushCount;

    public int DuplicateRejectCount => duplicateRejectCount;
    public int BusyRejectCount => busyRejectCount;
    public CombatHit LastAcceptedHit => lastAcceptedHit;

    public bool HasAcceptedDecayPush => hasAcceptedDecayPush;
    public int CurrentKnockbackResistanceLevel =>
        currentKnockbackResistanceLevel;

    public int HighestKnockbackResistanceLevel =>
        highestKnockbackResistanceLevel;

    public int MaximumKnockbackResistanceLevel =>
        GetResistanceSettings() != null
            ? GetResistanceSettings().MaximumResistanceLevel
            : 0;

    public float LastQualifyingPushAt => lastQualifyingPushAt;
    public float NextResistanceRecoveryAt => nextResistanceRecoveryAt;
    public int QualifyingDecayPushCount => qualifyingDecayPushCount;
    public int FullStrengthPushCount => fullStrengthPushCount;
    public int DecayedPushCount => decayedPushCount;
    public int ResistanceAdvanceCount => resistanceAdvanceCount;
    public int ResistanceRecoveryStepCount =>
        resistanceRecoveryStepCount;

    public float LastAppliedDistanceMultiplier =>
        lastAppliedDistanceMultiplier;

    public float LastAppliedStaggerMultiplier =>
        lastAppliedStaggerMultiplier;

    public float LowestAppliedDistanceMultiplier =>
        lowestAppliedDistanceMultiplier;

    public float LowestAppliedStaggerMultiplier =>
        lowestAppliedStaggerMultiplier;

    public float LastBaseDisplacementDistance =>
        lastBaseDisplacementDistance;

    public float LastResolvedDisplacementDistance =>
        lastResolvedDisplacementDistance;

    public float LastBaseReactionDuration =>
        lastBaseReactionDuration;

    public float LastResolvedReactionDuration =>
        lastResolvedReactionDuration;

    private void Awake()
    {
        CacheComponents();
    }

    private void Update()
    {
        if (!initialized ||
            health == null ||
            health.IsDead)
        {
            return;
        }

        TickKnockbackResistanceRecovery(Time.time);
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        EnemyDefinition newDefinition,
        Health newHealth,
        EnemyMotor2D newMotor,
        EnemyStateMachine newStateMachine)
    {
        context = newContext;
        definition = newDefinition;
        health = newHealth;
        motor = newMotor;
        stateMachine = newStateMachine;

        CacheComponents();

        KnockbackResistanceSettings resistance =
            GetResistanceSettings();

        if (resistance != null)
        {
            resistance.EnsureValid();
        }

        rememberedAttackOrder.Clear();
        rememberedAttackIds.Clear();
        lastAcceptedAttackId = default(CombatAttackId);
        lastAcceptedActionKind = CombatActionKind.Unspecified;
        acceptedHitCount = 0;
        acceptedNonlethalPushCount = 0;
        duplicateRejectCount = 0;
        busyRejectCount = 0;
        lastAcceptedHit = default(CombatHit);

        ResetKnockbackResistanceRuntime();

        initialized =
            context != null &&
            context.IsInitialized &&
            definition != null &&
            resistance != null &&
            health != null &&
            motor != null &&
            motor.IsInitialized &&
            stateMachine != null &&
            stateMachine.IsInitialized;
    }

    public bool TryReceiveCombatHit(CombatHit hit)
    {
        if (!initialized ||
            !hit.AttackId.IsValid ||
            health == null ||
            health.IsDead)
        {
            return false;
        }

        if (rememberedAttackIds.Contains(hit.AttackId.Value))
        {
            duplicateRejectCount++;
            return false;
        }

        if (health.Faction != DamageFaction.Neutral &&
            hit.SourceFaction == health.Faction)
        {
            return false;
        }

        float acceptedAt = Time.time;
        TickKnockbackResistanceRecovery(acceptedAt);

        KnockbackResolution resolution =
            ResolveKnockbackResistance(hit, acceptedAt);

        CombatHit resolvedHit = ApplyKnockbackResolution(
            hit,
            resolution);

        bool displacementAccepted = false;
        bool reactionAccepted = false;
        bool damageAccepted = false;

        if (resolvedHit.HasDisplacement)
        {
            if (motor == null ||
                stateMachine == null ||
                motor.IsCombatDisplacementActive)
            {
                busyRejectCount++;
            }
            else
            {
                reactionAccepted = !resolvedHit.HasReaction ||
                    stateMachine.TryBeginCombatReaction(
                        resolvedHit.Reaction);

                if (reactionAccepted)
                {
                    Collider2D sourceCollider =
                        resolvedHit.Source != null
                            ? resolvedHit.Source.GetComponent<Collider2D>()
                            : null;

                    displacementAccepted =
                        motor.TryBeginCombatDisplacement(
                            resolvedHit.Displacement,
                            sourceCollider,
                            replaceExisting: false);

                    if (!displacementAccepted &&
                        resolvedHit.HasReaction)
                    {
                        stateMachine.TryCompleteCombatReaction(
                            force: true,
                            reason: "Combat displacement request was rejected");

                        reactionAccepted = false;
                    }
                }
            }
        }
        else if (resolvedHit.HasReaction &&
                 stateMachine != null)
        {
            reactionAccepted =
                stateMachine.TryBeginCombatReaction(
                    resolvedHit.Reaction);
        }

        if (resolvedHit.HasDamage)
        {
            damageAccepted = health.ApplyDamage(
                resolvedHit.ToDamageInfo());
        }

        bool accepted =
            displacementAccepted ||
            reactionAccepted ||
            damageAccepted;

        if (!accepted)
        {
            return false;
        }

        bool acceptedKnockbackEffect =
            displacementAccepted || reactionAccepted;

        if (resolution.IsQualifying && acceptedKnockbackEffect)
        {
            CommitKnockbackResolution(
                resolution,
                hit,
                resolvedHit,
                acceptedAt);
        }

        RememberAttackId(resolvedHit.AttackId);

        lastAcceptedAttackId = resolvedHit.AttackId;
        lastAcceptedActionKind = resolvedHit.ActionKind;
        lastAcceptedHit = resolvedHit;
        acceptedHitCount++;

        if (resolvedHit.ActionKind ==
                CombatActionKind.NonlethalPush &&
            acceptedKnockbackEffect)
        {
            acceptedNonlethalPushCount++;
        }

        return true;
    }

    /// <summary>
    /// Applies every elapsed recovery step. Recovery is time based and does
    /// not depend on receiving another hit, so the Inspector and audit expose
    /// the actual current resistance while the enemy is left alone.
    /// </summary>
    public void TickKnockbackResistanceRecovery(float now)
    {
        if (!hasAcceptedDecayPush ||
            currentKnockbackResistanceLevel <= 0)
        {
            return;
        }

        KnockbackResistanceSettings settings =
            GetResistanceSettings();

        if (settings == null)
        {
            return;
        }

        settings.EnsureValid();

        if (nextResistanceRecoveryAt < 0f)
        {
            nextResistanceRecoveryAt =
                lastQualifyingPushAt + settings.RecoveryDelay;
        }

        if (now < nextResistanceRecoveryAt)
        {
            return;
        }

        float interval = Mathf.Max(
            0.05f,
            settings.RecoveryStepInterval);

        int elapsedSteps = 1 + Mathf.FloorToInt(
            (now - nextResistanceRecoveryAt) / interval);

        int appliedSteps = Mathf.Min(
            elapsedSteps,
            currentKnockbackResistanceLevel);

        currentKnockbackResistanceLevel -= appliedSteps;
        resistanceRecoveryStepCount += appliedSteps;

        if (currentKnockbackResistanceLevel <= 0)
        {
            currentKnockbackResistanceLevel = 0;
            nextResistanceRecoveryAt = -1f;
            return;
        }

        nextResistanceRecoveryAt += elapsedSteps * interval;
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        string prefix = gameObject.name + ": ";

        if (!initialized)
        {
            errors.Add(prefix + "EnemyCombatReceiver is not initialized.");
        }

        if (context == null ||
            definition == null ||
            health == null ||
            motor == null ||
            stateMachine == null)
        {
            errors.Add(
                prefix +
                "one or more combat receiver references are missing.");
        }

        KnockbackResistanceSettings resistance =
            GetResistanceSettings();

        if (resistance == null)
        {
            errors.Add(
                prefix +
                "Knockback Resistance settings are missing.");
        }
        else if (resistance.DecayTierCount <
                 KnockbackResistanceSettings.MinimumDecayTierCount)
        {
            errors.Add(
                prefix +
                "Knockback Resistance has fewer than three decay tiers.");
        }

        if (context != null && context.Definition != definition)
        {
            errors.Add(
                prefix +
                "combat receiver definition does not match runtime context.");
        }

        if (context != null && context.Health != health)
        {
            errors.Add(
                prefix +
                "combat receiver and runtime context reference different Health components.");
        }

        if (stateMachine != null && stateMachine.Motor != motor)
        {
            errors.Add(
                prefix +
                "combat receiver and state machine reference different EnemyMotor2D components.");
        }
    }

    private KnockbackResolution ResolveKnockbackResistance(
        CombatHit hit,
        float now)
    {
        KnockbackResistanceSettings settings =
            GetResistanceSettings();

        if (!hit.CountsTowardKnockbackDecay ||
            settings == null)
        {
            return KnockbackResolution.Unmodified;
        }

        settings.EnsureValid();

        int proposedLevel = currentKnockbackResistanceLevel;
        bool advanced = false;

        if (hasAcceptedDecayPush)
        {
            float elapsed = Mathf.Max(
                0f,
                now - lastQualifyingPushAt);

            if (elapsed <= settings.DecayBuildWindow)
            {
                int advancedLevel = Mathf.Min(
                    proposedLevel + 1,
                    settings.MaximumResistanceLevel);

                advanced = advancedLevel > proposedLevel;
                proposedLevel = advancedLevel;
            }
        }

        KnockbackDecayTier tier =
            settings.GetTierForResistanceLevel(proposedLevel);

        return new KnockbackResolution(
            true,
            proposedLevel,
            advanced,
            tier.DistanceMultiplier,
            tier.StaggerMultiplier);
    }

    private static CombatHit ApplyKnockbackResolution(
        CombatHit hit,
        KnockbackResolution resolution)
    {
        if (!resolution.IsQualifying)
        {
            return hit;
        }

        CombatDisplacementRequest displacement =
            default(CombatDisplacementRequest);

        if (hit.HasDisplacement)
        {
            float resolvedDistance =
                hit.Displacement.Distance *
                resolution.DistanceMultiplier;

            if (resolvedDistance > 0f)
            {
                // Scaling duration with distance keeps the original launch
                // speed. Lower tiers travel less rather than drifting slowly.
                float resolvedDuration = Mathf.Max(
                    MinimumScaledDuration,
                    hit.Displacement.Duration *
                    resolution.DistanceMultiplier);

                displacement = new CombatDisplacementRequest(
                    hit.Displacement.AttackId,
                    hit.Displacement.Direction,
                    resolvedDistance,
                    resolvedDuration);
            }
        }

        CombatReactionRequest reaction =
            default(CombatReactionRequest);

        if (hit.HasReaction)
        {
            reaction = new CombatReactionRequest(
                hit.Reaction.AttackId,
                hit.Reaction.Kind,
                hit.Reaction.Duration *
                    resolution.StaggerMultiplier,
                hit.Reaction.ExtendExistingReaction,
                hit.Reaction.Reason);
        }

        return new CombatHit(
            hit.AttackId,
            hit.ActionKind,
            hit.Source,
            hit.SourceFaction,
            hit.Attribution,
            hit.HitPoint,
            hit.Direction,
            hit.Damage,
            displacement,
            reaction,
            hit.CountsTowardKnockbackDecay,
            hit.TriggersPursuitRecovery);
    }

    private void CommitKnockbackResolution(
        KnockbackResolution resolution,
        CombatHit baseHit,
        CombatHit resolvedHit,
        float acceptedAt)
    {
        KnockbackResistanceSettings settings =
            GetResistanceSettings();

        if (settings == null)
        {
            return;
        }

        currentKnockbackResistanceLevel =
            resolution.ProposedLevel;

        highestKnockbackResistanceLevel = Mathf.Max(
            highestKnockbackResistanceLevel,
            currentKnockbackResistanceLevel);

        if (resolution.Advanced)
        {
            resistanceAdvanceCount++;
        }

        qualifyingDecayPushCount++;

        if (currentKnockbackResistanceLevel <= 0)
        {
            fullStrengthPushCount++;
        }
        else
        {
            decayedPushCount++;
        }

        hasAcceptedDecayPush = true;
        lastQualifyingPushAt = acceptedAt;
        nextResistanceRecoveryAt =
            acceptedAt + settings.RecoveryDelay;

        lastAppliedDistanceMultiplier =
            resolution.DistanceMultiplier;

        lastAppliedStaggerMultiplier =
            resolution.StaggerMultiplier;

        lowestAppliedDistanceMultiplier = Mathf.Min(
            lowestAppliedDistanceMultiplier,
            resolution.DistanceMultiplier);

        lowestAppliedStaggerMultiplier = Mathf.Min(
            lowestAppliedStaggerMultiplier,
            resolution.StaggerMultiplier);

        lastBaseDisplacementDistance = baseHit.HasDisplacement
            ? baseHit.Displacement.Distance
            : 0f;

        lastResolvedDisplacementDistance =
            resolvedHit.HasDisplacement
                ? resolvedHit.Displacement.Distance
                : 0f;

        lastBaseReactionDuration = baseHit.HasReaction
            ? baseHit.Reaction.Duration
            : 0f;

        lastResolvedReactionDuration =
            resolvedHit.HasReaction
                ? resolvedHit.Reaction.Duration
                : 0f;
    }

    private KnockbackResistanceSettings GetResistanceSettings()
    {
        return definition != null
            ? definition.KnockbackResistance
            : null;
    }

    private void ResetKnockbackResistanceRuntime()
    {
        hasAcceptedDecayPush = false;
        currentKnockbackResistanceLevel = 0;
        highestKnockbackResistanceLevel = 0;
        lastQualifyingPushAt = -1f;
        nextResistanceRecoveryAt = -1f;
        qualifyingDecayPushCount = 0;
        fullStrengthPushCount = 0;
        decayedPushCount = 0;
        resistanceAdvanceCount = 0;
        resistanceRecoveryStepCount = 0;
        lastAppliedDistanceMultiplier = 1f;
        lastAppliedStaggerMultiplier = 1f;
        lowestAppliedDistanceMultiplier = 1f;
        lowestAppliedStaggerMultiplier = 1f;
        lastBaseDisplacementDistance = 0f;
        lastResolvedDisplacementDistance = 0f;
        lastBaseReactionDuration = 0f;
        lastResolvedReactionDuration = 0f;
    }

    private void RememberAttackId(CombatAttackId attackId)
    {
        if (!attackId.IsValid ||
            rememberedAttackIds.Contains(attackId.Value))
        {
            return;
        }

        rememberedAttackIds.Add(attackId.Value);
        rememberedAttackOrder.Enqueue(attackId.Value);

        while (rememberedAttackOrder.Count >
               RememberedAttackCapacity)
        {
            int expiredId = rememberedAttackOrder.Dequeue();
            rememberedAttackIds.Remove(expiredId);
        }
    }

    private void CacheComponents()
    {
        if (context == null)
        {
            context = GetComponent<EnemyRuntimeContext>();
        }

        if (definition == null && context != null)
        {
            definition = context.Definition;
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (motor == null)
        {
            motor = GetComponent<EnemyMotor2D>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<EnemyStateMachine>();
        }
    }

    private readonly struct KnockbackResolution
    {
        public static KnockbackResolution Unmodified =>
            new KnockbackResolution(
                isQualifying: false,
                proposedLevel: 0,
                advanced: false,
                distanceMultiplier: 1f,
                staggerMultiplier: 1f);

        public readonly bool IsQualifying;
        public readonly int ProposedLevel;
        public readonly bool Advanced;
        public readonly float DistanceMultiplier;
        public readonly float StaggerMultiplier;

        public KnockbackResolution(
            bool isQualifying,
            int proposedLevel,
            bool advanced,
            float distanceMultiplier,
            float staggerMultiplier)
        {
            IsQualifying = isQualifying;
            ProposedLevel = Mathf.Max(0, proposedLevel);
            Advanced = advanced;
            DistanceMultiplier = Mathf.Clamp(
                distanceMultiplier,
                0f,
                2f);

            StaggerMultiplier = Mathf.Clamp(
                staggerMultiplier,
                0f,
                2f);
        }
    }
}
