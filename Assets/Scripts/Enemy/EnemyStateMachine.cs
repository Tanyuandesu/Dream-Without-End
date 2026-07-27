using System;
using UnityEngine;

/// <summary>
/// Observable lifecycle and perception state owner for one enemy.
///
/// EA3 preserves the accepted state vocabulary while replacing the old chase
/// locomotion with EnemyNavigationAgent. CB4 sequences motor-owned knockback,
/// a post-displacement pause and a temporary navigation-speed recovery.
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

    [Header("CB4 combat reaction sequence")]
    [SerializeField] private bool combatReactionActive;
    [SerializeField] private CombatAttackId activeReactionAttackId;
    [SerializeField] private CombatReactionKind activeReactionKind;
    [SerializeField] private bool combatReactionTimerStarted;
    [SerializeField] private float pendingCombatReactionDuration;
    [SerializeField] private float combatReactionEndsAt;
    [SerializeField] private bool pursuitRecoveryQueued;
    [SerializeField] private float queuedPursuitSpeedMultiplier = 1f;
    [SerializeField] private float queuedPursuitDuration;
    [SerializeField] private int combatReactionStartCount;
    [SerializeField] private int combatReactionCompleteCount;
    [SerializeField] private int postDisplacementPauseStartCount;
    [SerializeField] private int postDisplacementPauseCompleteCount;
    [SerializeField] private int pursuitRecoveryRequestCount;
    [SerializeField] private int pursuitRecoveryAcceptedCount;
    [SerializeField] private int pursuitRecoverySkippedCount;
    [SerializeField] private float lastPostDisplacementPauseStartedAt = -1f;
    [SerializeField] private float lastPostDisplacementPauseCompletedAt = -1f;
    [SerializeField] private float lastPursuitRecoveryPauseCompletedAt = -1f;
    [SerializeField] private float lastPostDisplacementPauseDuration;
    [SerializeField] private float lastPursuitRecoveryRequestedAt = -1f;
    [SerializeField] private float lastPursuitRecoverySpeedMultiplier = 1f;
    [SerializeField] private float lastPursuitRecoveryDuration;

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

    public bool IsWaitingForCombatDisplacement =>
        combatReactionActive &&
        !combatReactionTimerStarted &&
        motor != null &&
        motor.IsCombatDisplacementActive;

    public bool IsPostDisplacementPauseActive =>
        combatReactionActive &&
        combatReactionTimerStarted;

    public float PendingCombatReactionDuration =>
        pendingCombatReactionDuration;

    public float CombatReactionRemainingTime =>
        combatReactionActive && combatReactionTimerStarted
            ? Mathf.Max(0f, combatReactionEndsAt - Time.time)
            : combatReactionActive
                ? Mathf.Max(0f, pendingCombatReactionDuration)
                : 0f;

    public int CombatReactionStartCount =>
        combatReactionStartCount;

    public int CombatReactionCompleteCount =>
        combatReactionCompleteCount;

    public int PostDisplacementPauseStartCount =>
        postDisplacementPauseStartCount;

    public int PostDisplacementPauseCompleteCount =>
        postDisplacementPauseCompleteCount;

    public int PursuitRecoveryRequestCount =>
        pursuitRecoveryRequestCount;

    public int PursuitRecoveryAcceptedCount =>
        pursuitRecoveryAcceptedCount;

    public int PursuitRecoverySkippedCount =>
        pursuitRecoverySkippedCount;

    public float LastPostDisplacementPauseStartedAt =>
        lastPostDisplacementPauseStartedAt;

    public float LastPostDisplacementPauseCompletedAt =>
        lastPostDisplacementPauseCompletedAt;

    public float LastPursuitRecoveryPauseCompletedAt =>
        lastPursuitRecoveryPauseCompletedAt;

    public float LastPostDisplacementPauseDuration =>
        lastPostDisplacementPauseDuration;

    public float LastPursuitRecoveryRequestedAt =>
        lastPursuitRecoveryRequestedAt;

    public float LastPursuitRecoverySpeedMultiplier =>
        lastPursuitRecoverySpeedMultiplier;

    public float LastPursuitRecoveryDuration =>
        lastPursuitRecoveryDuration;

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
        postDisplacementPauseStartCount = 0;
        postDisplacementPauseCompleteCount = 0;
        pursuitRecoveryRequestCount = 0;
        pursuitRecoveryAcceptedCount = 0;
        pursuitRecoverySkippedCount = 0;
        lastPostDisplacementPauseStartedAt = -1f;
        lastPostDisplacementPauseCompletedAt = -1f;
        lastPursuitRecoveryPauseCompletedAt = -1f;
        lastPostDisplacementPauseDuration = 0f;
        lastPursuitRecoveryRequestedAt = -1f;
        lastPursuitRecoverySpeedMultiplier = 1f;
        lastPursuitRecoveryDuration = 0f;

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
    /// Requests a temporary Hit or Stunned interruption. When the motor is
    /// already performing knockback, the reaction timer is deferred until that
    /// movement ends. The remembered target is preserved for a fresh route.
    /// </summary>
    public bool TryBeginCombatReaction(
        CombatReactionRequest request)
    {
        return TryBeginCombatReaction(
            request,
            shouldQueuePursuitRecovery: false);
    }

    public bool TryBeginCombatReaction(
        CombatReactionRequest request,
        bool shouldQueuePursuitRecovery)
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead ||
            !request.IsValid)
        {
            return false;
        }

        if (request.CancelTimedNavigationSpeed &&
            motor != null &&
            motor.IsTimedNavigationSpeedActive)
        {
            motor.CancelTimedNavigationSpeedMultiplier(
                TimedNavigationSpeedEndReason.CancelledByOwner);
        }

        if (combatReactionActive &&
            request.ExtendExistingReaction)
        {
            pendingCombatReactionDuration = Mathf.Max(
                pendingCombatReactionDuration,
                request.Duration);
        }
        else
        {
            pendingCombatReactionDuration = request.Duration;
        }

        combatReactionActive = true;
        activeReactionAttackId = request.AttackId;
        activeReactionKind = StrongerReaction(
            activeReactionKind,
            request.Kind);

        QueuePursuitRecovery(
            shouldQueuePursuitRecovery,
            request.AttackId);

        combatReactionStartCount++;
        combatReactionTimerStarted = false;
        combatReactionEndsAt = -1f;

        navigationAgent.StopMovement(
            clearLastKnownPosition: false);

        TransitionTo(
            MapReactionState(activeReactionKind),
            string.IsNullOrWhiteSpace(request.Reason)
                ? "Combat reaction " + activeReactionKind
                : request.Reason);

        if (motor == null ||
            !motor.IsCombatDisplacementActive)
        {
            BeginPostDisplacementPause();
        }

        return true;
    }

    /// <summary>
    /// Ends a reaction after motor displacement and the full configured pause.
    /// A queued pursuit recovery starts only after navigation state resumes.
    /// </summary>
    public bool TryCompleteCombatReaction(
        bool force = false,
        string reason = null)
    {
        if (!combatReactionActive)
        {
            return false;
        }

        if (force)
        {
            ResetCombatReactionFields();
            combatReactionCompleteCount++;
            ResumeAfterCombatReaction(reason);
            return true;
        }

        if (motor != null &&
            motor.IsCombatDisplacementActive)
        {
            return false;
        }

        if (!combatReactionTimerStarted)
        {
            BeginPostDisplacementPause();
        }

        if (Time.time < combatReactionEndsAt)
        {
            return false;
        }

        bool shouldStartPursuitRecovery = pursuitRecoveryQueued;
        float pursuitSpeedMultiplier = queuedPursuitSpeedMultiplier;
        float pursuitDuration = queuedPursuitDuration;

        lastPostDisplacementPauseCompletedAt = Time.time;

        if (shouldStartPursuitRecovery)
        {
            lastPursuitRecoveryPauseCompletedAt =
                lastPostDisplacementPauseCompletedAt;
        }

        postDisplacementPauseCompleteCount++;

        ResetCombatReactionFields();
        combatReactionCompleteCount++;
        ResumeAfterCombatReaction(reason);

        if (shouldStartPursuitRecovery)
        {
            StartPursuitRecovery(
                pursuitSpeedMultiplier,
                pursuitDuration);
        }

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

            motor.CancelTimedNavigationSpeedMultiplier(
                TimedNavigationSpeedEndReason.OwnerDied);
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
                "[EnemyStateMachine/EA3+CB4] " +
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

    private void BeginPostDisplacementPause()
    {
        if (!combatReactionActive ||
            combatReactionTimerStarted)
        {
            return;
        }

        combatReactionTimerStarted = true;
        combatReactionEndsAt =
            Time.time + Mathf.Max(0f, pendingCombatReactionDuration);
        postDisplacementPauseStartCount++;
        lastPostDisplacementPauseStartedAt = Time.time;
        lastPostDisplacementPauseDuration = Mathf.Max(
            0f,
            pendingCombatReactionDuration);
    }

    private void QueuePursuitRecovery(
        bool shouldQueue,
        CombatAttackId attackId)
    {
        pursuitRecoveryQueued = false;
        queuedPursuitSpeedMultiplier = 1f;
        queuedPursuitDuration = 0f;

        if (!shouldQueue ||
            !attackId.IsValid ||
            context == null ||
            context.Definition == null)
        {
            return;
        }

        float duration =
            context.Definition.PostKnockbackPursuitDuration;
        float multiplier =
            context.Definition.PostKnockbackPursuitSpeedMultiplier;

        if (duration <= 0f || multiplier <= 1f)
        {
            pursuitRecoverySkippedCount++;
            return;
        }

        pursuitRecoveryQueued = true;
        queuedPursuitSpeedMultiplier = multiplier;
        queuedPursuitDuration = duration;
    }

    private void StartPursuitRecovery(
        float speedMultiplier,
        float duration)
    {
        pursuitRecoveryRequestCount++;
        lastPursuitRecoveryRequestedAt = Time.time;
        lastPursuitRecoverySpeedMultiplier = speedMultiplier;
        lastPursuitRecoveryDuration = duration;

        if (motor != null &&
            motor.TryBeginTimedNavigationSpeedMultiplier(
                speedMultiplier,
                duration,
                replaceExisting: true))
        {
            pursuitRecoveryAcceptedCount++;
            return;
        }

        pursuitRecoverySkippedCount++;
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
        combatReactionTimerStarted = false;
        pendingCombatReactionDuration = 0f;
        combatReactionEndsAt = -1f;
        pursuitRecoveryQueued = false;
        queuedPursuitSpeedMultiplier = 1f;
        queuedPursuitDuration = 0f;
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }
}
