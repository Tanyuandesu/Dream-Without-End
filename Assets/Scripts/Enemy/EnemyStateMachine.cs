using System;
using UnityEngine;

/// <summary>
/// Observable lifecycle and perception state owner for one enemy.
///
/// EA3 preserves the accepted state vocabulary while replacing the old chase
/// locomotion with EnemyNavigationAgent. CB0 activates the formal Hit/Stunned
/// interruption boundary without changing current baseline behaviour.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(EnemyNavigationAgent))]
[RequireComponent(typeof(Health))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    [Header("EA2/EA3 runtime state (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private EnemyRuntimeState currentState =
        EnemyRuntimeState.Spawn;

    [SerializeField] private EnemyRuntimeState previousState =
        EnemyRuntimeState.Spawn;

    [SerializeField] private float stateEnteredAt;
    [SerializeField] private int transitionCount;
    [SerializeField] private string lastTransitionReason =
        "Not initialized";

    [Header("CB0 combat reaction contract")]
    [SerializeField] private bool combatReactionActive;
    [SerializeField] private CombatAttackId activeReactionAttackId;
    [SerializeField] private CombatReactionKind activeReactionKind;
    [SerializeField] private float combatReactionEndsAt;
    [SerializeField] private int combatReactionStartCount;
    [SerializeField] private int combatReactionCompleteCount;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyNavigationAgent navigationAgent;
    [SerializeField] private EnemyMotor2D motor;

    [Header("Optional diagnostics")]
    [Tooltip(
        "Logs only actual state changes. It never logs once per frame.")]
    [SerializeField] private bool logStateTransitions;

    private Health subscribedHealth;

    public bool IsInitialized => initialized;
    public EnemyRuntimeState CurrentState => currentState;
    public EnemyRuntimeState PreviousState => previousState;
    public float StateEnteredAt => stateEnteredAt;
    public float StateElapsedTime =>
        Mathf.Max(0f, Time.time - stateEnteredAt);

    public int TransitionCount => transitionCount;
    public string LastTransitionReason => lastTransitionReason;
    public EnemyRuntimeContext Context => context;
    public EnemyNavigationAgent NavigationAgent => navigationAgent;
    public EnemyMotor2D Motor => motor;
    public bool LogsStateTransitions => logStateTransitions;

    public bool IsCombatReactionActive => combatReactionActive;
    public CombatAttackId ActiveReactionAttackId =>
        activeReactionAttackId;

    public CombatReactionKind ActiveReactionKind =>
        activeReactionKind;

    public float CombatReactionRemainingTime =>
        combatReactionActive
            ? Mathf.Max(0f, combatReactionEndsAt - Time.time)
            : 0f;

    public int CombatReactionStartCount =>
        combatReactionStartCount;

    public int CombatReactionCompleteCount =>
        combatReactionCompleteCount;

    public event Action<
        EnemyStateMachine,
        EnemyRuntimeState,
        EnemyRuntimeState> StateChanged;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (!initialized &&
            context != null &&
            navigationAgent != null)
        {
            Initialize(context, navigationAgent);
        }
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        EnemyNavigationAgent newNavigationAgent)
    {
        UnsubscribeFromHealth();

        context = newContext;
        navigationAgent = newNavigationAgent;

        CacheComponents();

        initialized =
            context != null &&
            context.IsInitialized &&
            navigationAgent != null &&
            navigationAgent.IsInitialized &&
            motor != null &&
            motor.IsInitialized;

        previousState = EnemyRuntimeState.Spawn;
        currentState = EnemyRuntimeState.Spawn;
        stateEnteredAt = Time.time;
        transitionCount = 0;
        lastTransitionReason = initialized
            ? "Runtime initialized"
            : "Runtime references incomplete";

        ResetCombatReactionFields();
        combatReactionStartCount = 0;
        combatReactionCompleteCount = 0;

        if (!initialized)
        {
            return;
        }

        SubscribeToHealth();

        if (subscribedHealth != null &&
            subscribedHealth.IsDead)
        {
            TransitionTo(
                EnemyRuntimeState.Dead,
                "Health was already dead at initialization");

            return;
        }

        TransitionTo(
            EnemyRuntimeState.Idle,
            "EA3 navigation ready");
    }

    /// <summary>
    /// EA2 source-compatibility overload. The bridge owns no movement and
    /// exposes the EA3 agent that now performs navigation.
    /// </summary>
    public void Initialize(
        EnemyRuntimeContext newContext,
        TestEnemyAI legacyBridge)
    {
        EnemyNavigationAgent resolvedAgent =
            legacyBridge != null
                ? legacyBridge.NavigationAgent
                : GetComponent<EnemyNavigationAgent>();

        Initialize(newContext, resolvedAgent);
    }

    /// <summary>
    /// Requests a temporary Hit or Stunned interruption. It stops navigation
    /// but preserves the remembered target so recovery can request a fresh
    /// route from the enemy's post-displacement position.
    /// </summary>
    public bool TryBeginCombatReaction(
        CombatReactionRequest request)
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead ||
            !request.IsValid)
        {
            return false;
        }

        float requestedEndTime =
            Time.time + request.Duration;

        if (combatReactionActive &&
            request.ExtendExistingReaction)
        {
            combatReactionEndsAt = Mathf.Max(
                combatReactionEndsAt,
                requestedEndTime);
        }
        else
        {
            combatReactionEndsAt = requestedEndTime;
        }

        combatReactionActive = true;
        activeReactionAttackId = request.AttackId;
        activeReactionKind = StrongerReaction(
            activeReactionKind,
            request.Kind);

        combatReactionStartCount++;

        navigationAgent.StopMovement(
            clearLastKnownPosition: false);

        TransitionTo(
            MapReactionState(activeReactionKind),
            string.IsNullOrWhiteSpace(request.Reason)
                ? "Combat reaction " + activeReactionKind
                : request.Reason);

        return true;
    }

    /// <summary>
    /// Ends a reaction when its timer and any motor-owned displacement have
    /// finished. Force is reserved for death or explicit teardown.
    /// </summary>
    public bool TryCompleteCombatReaction(
        bool force = false,
        string reason = null)
    {
        if (!combatReactionActive)
        {
            return false;
        }

        if (!force)
        {
            if (Time.time < combatReactionEndsAt)
            {
                return false;
            }

            if (motor != null &&
                motor.IsCombatDisplacementActive)
            {
                return false;
            }
        }

        ResetCombatReactionFields();
        combatReactionCompleteCount++;
        ResumeAfterCombatReaction(reason);
        return true;
    }

    private void Update()
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

        if (combatReactionActive)
        {
            TryCompleteCombatReaction();
            return;
        }

        EvaluatePerceptionState();
    }

    private void FixedUpdate()
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

        navigationAgent.TickFixed(currentState);

        if (combatReactionActive)
        {
            return;
        }

        if (currentState ==
                EnemyRuntimeState.InvestigateLastKnownPosition &&
            !context.HasLastKnownTargetPosition)
        {
            TransitionTo(
                EnemyRuntimeState.Idle,
                "Last known position reached");
        }
    }

    private void EvaluatePerceptionState()
    {
        EnemyDetection detection = context.Detection;

        if (detection != null &&
            detection.IsTargetDetected)
        {
            Vector2 observedPosition =
                detection.LastKnownTargetPosition;

            context.SetLastKnownTargetPosition(
                observedPosition);

            navigationAgent.ObserveDetectedTarget(
                observedPosition);

            TransitionTo(
                EnemyRuntimeState.Chase,
                "Target detected");

            return;
        }

        if (context.HasLastKnownTargetPosition)
        {
            TransitionTo(
                EnemyRuntimeState.InvestigateLastKnownPosition,
                "Target lost; continue to last known position");

            return;
        }

        TransitionTo(
            EnemyRuntimeState.Idle,
            "No detected or remembered target");
    }

    private void ResumeAfterCombatReaction(string reason)
    {
        if (currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

        EnemyDetection detection = context != null
            ? context.Detection
            : null;

        if (detection != null &&
            detection.IsTargetDetected)
        {
            Vector2 observedPosition =
                detection.LastKnownTargetPosition;

            context.SetLastKnownTargetPosition(
                observedPosition);

            navigationAgent.ObserveDetectedTarget(
                observedPosition);

            TransitionTo(
                EnemyRuntimeState.Chase,
                string.IsNullOrWhiteSpace(reason)
                    ? "Combat reaction complete; target detected"
                    : reason);

            return;
        }

        if (context != null &&
            context.HasLastKnownTargetPosition)
        {
            TransitionTo(
                EnemyRuntimeState.InvestigateLastKnownPosition,
                string.IsNullOrWhiteSpace(reason)
                    ? "Combat reaction complete; repath to last known target"
                    : reason);

            return;
        }

        TransitionTo(
            EnemyRuntimeState.Idle,
            string.IsNullOrWhiteSpace(reason)
                ? "Combat reaction complete"
                : reason);
    }

    private void HandleDied(Health health)
    {
        if (!initialized)
        {
            return;
        }

        ResetCombatReactionFields();

        if (motor != null)
        {
            motor.CancelCombatDisplacement(
                CombatDisplacementEndReason.OwnerDied);
        }

        navigationAgent.StopMovement(
            clearLastKnownPosition: false);

        context.ClearNavigationDestination();

        TransitionTo(
            EnemyRuntimeState.Dead,
            "Health reached zero");
    }

    private void TransitionTo(
        EnemyRuntimeState nextState,
        string reason)
    {
        if (currentState == nextState)
        {
            return;
        }

        EnemyRuntimeState oldState = currentState;

        previousState = oldState;
        currentState = nextState;
        stateEnteredAt = Time.time;
        transitionCount++;
        lastTransitionReason = string.IsNullOrWhiteSpace(reason)
            ? "Unspecified"
            : reason;

        if (logStateTransitions)
        {
            string runtimeEnemyId = context != null
                ? context.EnemyId
                : "unknown_enemy";

            Debug.Log(
                "[EnemyStateMachine/EA3+CB0] " +
                runtimeEnemyId +
                " | " + oldState +
                " -> " + nextState +
                " | Reason=" + lastTransitionReason,
                this);
        }

        StateChanged?.Invoke(
            this,
            oldState,
            nextState);
    }

    private void CacheComponents()
    {
        if (context == null)
        {
            context = GetComponent<EnemyRuntimeContext>();
        }

        if (navigationAgent == null)
        {
            navigationAgent =
                GetComponent<EnemyNavigationAgent>();
        }

        if (motor == null)
        {
            motor = navigationAgent != null
                ? navigationAgent.Motor
                : GetComponent<EnemyMotor2D>();
        }
    }

    private void SubscribeToHealth()
    {
        subscribedHealth = context != null
            ? context.Health
            : GetComponent<Health>();

        if (subscribedHealth != null)
        {
            subscribedHealth.Died += HandleDied;
        }
    }

    private void UnsubscribeFromHealth()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.Died -= HandleDied;
        }

        subscribedHealth = null;
    }

    private static CombatReactionKind StrongerReaction(
        CombatReactionKind current,
        CombatReactionKind requested)
    {
        return (int)requested > (int)current
            ? requested
            : current;
    }

    private static EnemyRuntimeState MapReactionState(
        CombatReactionKind kind)
    {
        return kind == CombatReactionKind.Stunned
            ? EnemyRuntimeState.Stunned
            : EnemyRuntimeState.Hit;
    }

    private void ResetCombatReactionFields()
    {
        combatReactionActive = false;
        activeReactionAttackId = default(CombatAttackId);
        activeReactionKind = CombatReactionKind.None;
        combatReactionEndsAt = 0f;
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }
}
