using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side adapter for CombatHit messages.
/// It owns no Rigidbody2D movement and no path selection. Reactions are sent
/// to EnemyStateMachine and displacement requests are sent to EnemyMotor2D.
/// CB1 executes the base push only; per-enemy decay and pursuit recovery stay
/// dormant until their dedicated balance phases.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(EnemyMotor2D))]
[RequireComponent(typeof(EnemyStateMachine))]
[RequireComponent(typeof(Health))]
public sealed class EnemyCombatReceiver : MonoBehaviour
{
    private const int RememberedAttackCapacity = 32;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private Health health;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private EnemyStateMachine stateMachine;

    [Header("CB1 runtime diagnostics")]
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

    private void Awake()
    {
        CacheComponents();
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

        rememberedAttackOrder.Clear();
        rememberedAttackIds.Clear();
        lastAcceptedAttackId = default(CombatAttackId);
        lastAcceptedActionKind = CombatActionKind.Unspecified;
        acceptedHitCount = 0;
        acceptedNonlethalPushCount = 0;
        duplicateRejectCount = 0;
        busyRejectCount = 0;
        lastAcceptedHit = default(CombatHit);

        initialized =
            context != null &&
            context.IsInitialized &&
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

        bool displacementAccepted = false;
        bool reactionAccepted = false;
        bool damageAccepted = false;

        if (hit.HasDisplacement)
        {
            if (motor == null ||
                stateMachine == null ||
                motor.IsCombatDisplacementActive)
            {
                busyRejectCount++;
            }
            else
            {
                reactionAccepted = !hit.HasReaction ||
                    stateMachine.TryBeginCombatReaction(
                        hit.Reaction);

                if (reactionAccepted)
                {
                    Collider2D sourceCollider = hit.Source != null
                        ? hit.Source.GetComponent<Collider2D>()
                        : null;

                    displacementAccepted =
                        motor.TryBeginCombatDisplacement(
                            hit.Displacement,
                            sourceCollider,
                            replaceExisting: false);

                    if (!displacementAccepted && hit.HasReaction)
                    {
                        stateMachine.TryCompleteCombatReaction(
                            force: true,
                            reason: "Combat displacement request was rejected");

                        reactionAccepted = false;
                    }
                }
            }
        }
        else if (hit.HasReaction && stateMachine != null)
        {
            reactionAccepted =
                stateMachine.TryBeginCombatReaction(
                    hit.Reaction);
        }

        if (hit.HasDamage)
        {
            damageAccepted = health.ApplyDamage(
                hit.ToDamageInfo());
        }

        bool accepted =
            displacementAccepted ||
            reactionAccepted ||
            damageAccepted;

        if (!accepted)
        {
            return false;
        }

        RememberAttackId(hit.AttackId);

        lastAcceptedAttackId = hit.AttackId;
        lastAcceptedActionKind = hit.ActionKind;
        lastAcceptedHit = hit;
        acceptedHitCount++;

        if (hit.ActionKind == CombatActionKind.NonlethalPush &&
            displacementAccepted)
        {
            acceptedNonlethalPushCount++;
        }

        return true;
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
            health == null ||
            motor == null ||
            stateMachine == null)
        {
            errors.Add(
                prefix +
                "one or more combat receiver references are missing.");
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
}
