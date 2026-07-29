using System;
using System.Collections.Generic;
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

    [Header("T5A patrol, search and return loop")]
    [SerializeField] private bool extendedBehaviorLoopActive;
    [SerializeField] private Vector2 activeBehaviorDestination;
    [SerializeField] private Vector2 searchCenter;
    [SerializeField] private float searchEndsAt;
    [SerializeField] private float nextBehaviorDecisionAt;
    [SerializeField] private bool patrolPausePending;
    [SerializeField] private int patrolDestinationSequence;
    [SerializeField] private int searchDestinationSequence;
    [SerializeField] private int patrolDestinationCount;
    [SerializeField] private int searchDestinationCount;
    [SerializeField] private int returnHomeCount;

    [Header("T5B perception and chase leash")]
    [SerializeField] private bool chasePathCostBlockedUntilTargetLost;
    [SerializeField] private int chasePathCostRejectCount;
    [SerializeField] private float lastChasePathCostRejectedAt = -1f;

    [Header("T5C local alert relay")]
    [SerializeField] private float nextAlertBroadcastAt;
    [SerializeField] private int alertBroadcastCount;
    [SerializeField] private int alertRecipientCount;
    [SerializeField] private int alertReceivedCount;
    [SerializeField] private float lastAlertBroadcastAt = -1f;
    [SerializeField] private float lastAlertReceivedAt = -1f;
    [SerializeField] private Vector2 lastAlertPosition;
    [SerializeField] private string lastAlertSourceId = string.Empty;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyNavigationAgent navigationAgent;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private EnemyManager alertManager;

    [Header("Optional diagnostics")]
    [Tooltip(
        "Logs only actual state changes. It never logs once per frame.")]
    [SerializeField] private bool logStateTransitions;

    private const float BehaviorRetryDelay = 0.25f;
    private readonly List<Vector2Int> behaviorCandidates =
        new List<Vector2Int>();

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
    public bool UsesExtendedBehaviorLoop => extendedBehaviorLoopActive;
    public Vector2 ActiveBehaviorDestination => activeBehaviorDestination;
    public Vector2 SearchCenter => searchCenter;
    public float SearchRemainingTime => currentState ==
            EnemyRuntimeState.SearchLastKnownPosition
        ? Mathf.Max(0f, searchEndsAt - Time.time)
        : 0f;
    public int PatrolDestinationCount => patrolDestinationCount;
    public int SearchDestinationCount => searchDestinationCount;
    public int ReturnHomeCount => returnHomeCount;
    public bool IsChasePathCostBlocked =>
        chasePathCostBlockedUntilTargetLost;
    public int ChasePathCostRejectCount =>
        chasePathCostRejectCount;
    public float LastChasePathCostRejectedAt =>
        lastChasePathCostRejectedAt;
    public bool SupportsLocalAlertRelay => true;
    public EnemyManager AlertManager => alertManager;
    public int AlertBroadcastCount => alertBroadcastCount;
    public int AlertRecipientCount => alertRecipientCount;
    public int AlertReceivedCount => alertReceivedCount;
    public float LastAlertBroadcastAt => lastAlertBroadcastAt;
    public float LastAlertReceivedAt => lastAlertReceivedAt;
    public Vector2 LastAlertPosition => lastAlertPosition;
    public string LastAlertSourceId => lastAlertSourceId;

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

        extendedBehaviorLoopActive = initialized;
        activeBehaviorDestination = Vector2.zero;
        searchCenter = Vector2.zero;
        searchEndsAt = 0f;
        nextBehaviorDecisionAt = 0f;
        patrolPausePending = false;
        patrolDestinationSequence = 0;
        searchDestinationSequence = 0;
        patrolDestinationCount = 0;
        searchDestinationCount = 0;
        returnHomeCount = 0;
        chasePathCostBlockedUntilTargetLost = false;
        chasePathCostRejectCount = 0;
        lastChasePathCostRejectedAt = -1f;
        nextAlertBroadcastAt = 0f;
        alertBroadcastCount = 0;
        alertRecipientCount = 0;
        alertReceivedCount = 0;
        lastAlertBroadcastAt = -1f;
        lastAlertReceivedAt = -1f;
        lastAlertPosition = Vector2.zero;
        lastAlertSourceId = string.Empty;

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
            EnemyRuntimeState.Patrol,
            "T5A patrol loop ready");
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

    /// <summary>
    /// Receives a local last-known-position alert. This does not grant direct
    /// target detection: the recipient moves to the reported position in the
    /// Alert state and must still acquire the player with its own perception
    /// settings before entering Chase.
    /// </summary>
    public bool TryReceiveAlert(
        Vector2 targetPosition,
        EnemyStateMachine source)
    {
        if (!initialized ||
            source == null ||
            source == this ||
            currentState == EnemyRuntimeState.Dead ||
            currentState == EnemyRuntimeState.Chase ||
            combatReactionActive ||
            chasePathCostBlockedUntilTargetLost ||
            context == null ||
            navigationAgent == null)
        {
            return false;
        }

        context.SetLastKnownTargetPosition(targetPosition);
        alertReceivedCount++;
        lastAlertReceivedAt = Time.time;
        lastAlertPosition = targetPosition;
        lastAlertSourceId = source.Context != null
            ? source.Context.EnemyId
            : source.gameObject.name;

        if (currentState == EnemyRuntimeState.Alert)
        {
            ApplyAlertDestination();
            return true;
        }

        TransitionTo(
            EnemyRuntimeState.Alert,
            "T5C alert received from " + lastAlertSourceId);

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

        EvaluateBehaviorState();
    }

    private void FixedUpdate()
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

        navigationAgent.TickFixed(currentState);
    }

    private void EvaluateBehaviorState()
    {
        EnemyDetection detection = context.Detection;

        if (chasePathCostBlockedUntilTargetLost &&
            (detection == null || !detection.IsTargetDetected))
        {
            chasePathCostBlockedUntilTargetLost = false;
        }

        if (currentState == EnemyRuntimeState.Chase &&
            navigationAgent.LastFailureReason ==
                EnemyPathFailureReason.PathCostLimitExceeded)
        {
            chasePathCostBlockedUntilTargetLost = true;
            chasePathCostRejectCount++;
            lastChasePathCostRejectedAt = Time.time;

            context.ClearLastKnownTargetPosition();
            navigationAgent.StopMovement(
                clearLastKnownPosition: false);

            TransitionTo(
                EnemyRuntimeState.ReturnToHomeOrPatrol,
                "T5B maximum chase path cost exceeded");

            return;
        }

        if (detection != null &&
            detection.IsTargetDetected &&
            !chasePathCostBlockedUntilTargetLost)
        {
            Vector2 observedPosition =
                detection.LastKnownTargetPosition;

            context.SetLastKnownTargetPosition(
                observedPosition);

            navigationAgent.ObserveDetectedTarget(
                observedPosition);

            if (currentState != EnemyRuntimeState.Chase)
            {
                TryBroadcastAlert(observedPosition);
            }

            TransitionTo(
                EnemyRuntimeState.Chase,
                "Target detected");

            return;
        }

        switch (currentState)
        {
            case EnemyRuntimeState.Alert:
                if (!context.HasLastKnownTargetPosition)
                {
                    TransitionTo(
                        EnemyRuntimeState.ReturnToHomeOrPatrol,
                        "Alert target position was cleared");
                }
                else if (navigationAgent.HasReachedDestination)
                {
                    TransitionTo(
                        EnemyRuntimeState.SearchLastKnownPosition,
                        "Alert position reached");
                }
                break;

            case EnemyRuntimeState.Chase:
                if (context.HasLastKnownTargetPosition)
                {
                    TransitionTo(
                        EnemyRuntimeState.InvestigateLastKnownPosition,
                        "Target lost; investigate last known position");
                }
                else
                {
                    TransitionTo(
                        EnemyRuntimeState.ReturnToHomeOrPatrol,
                        "Target lost without a remembered position");
                }
                break;

            case EnemyRuntimeState.InvestigateLastKnownPosition:
                if (!context.HasLastKnownTargetPosition)
                {
                    TransitionTo(
                        EnemyRuntimeState.ReturnToHomeOrPatrol,
                        "Last known target position was cleared");
                }
                else if (navigationAgent.HasReachedDestination)
                {
                    TransitionTo(
                        EnemyRuntimeState.SearchLastKnownPosition,
                        "Last known position reached");
                }
                break;

            case EnemyRuntimeState.SearchLastKnownPosition:
                if (Time.time >= searchEndsAt)
                {
                    context.ClearLastKnownTargetPosition();
                    TransitionTo(
                        EnemyRuntimeState.ReturnToHomeOrPatrol,
                        "Search duration completed");
                }
                else if ((!navigationAgent.HasDesiredDestination ||
                          navigationAgent.HasReachedDestination) &&
                         Time.time >= nextBehaviorDecisionAt)
                {
                    AssignSearchDestination();
                }
                break;

            case EnemyRuntimeState.ReturnToHomeOrPatrol:
                if (navigationAgent.HasReachedDestination ||
                    IsNearHome())
                {
                    TransitionTo(
                        EnemyRuntimeState.Patrol,
                        "Home anchor reached");
                }
                break;

            case EnemyRuntimeState.Patrol:
                if (!navigationAgent.HasDesiredDestination)
                {
                    patrolPausePending = false;
                    AssignPatrolDestination();
                }
                else if (navigationAgent.HasReachedDestination)
                {
                    if (!patrolPausePending)
                    {
                        float pause = context.Definition != null
                            ? Mathf.Max(
                                0f,
                                context.Definition.PatrolPauseDuration)
                            : 0f;

                        patrolPausePending = true;
                        nextBehaviorDecisionAt = Time.time + pause;
                    }

                    if (Time.time >= nextBehaviorDecisionAt)
                    {
                        patrolPausePending = false;
                        AssignPatrolDestination();
                    }
                }
                break;

            case EnemyRuntimeState.Idle:
            case EnemyRuntimeState.Spawn:
                TransitionTo(
                    EnemyRuntimeState.Patrol,
                    "Resume configured patrol loop");
                break;
        }
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

            if (currentState != EnemyRuntimeState.Chase)
            {
                TryBroadcastAlert(observedPosition);
            }

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
                    ? "Combat reaction complete; investigate remembered target"
                    : reason);

            return;
        }

        TransitionTo(
            IsNearHome()
                ? EnemyRuntimeState.Patrol
                : EnemyRuntimeState.ReturnToHomeOrPatrol,
            string.IsNullOrWhiteSpace(reason)
                ? "Combat reaction complete; resume home behaviour"
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

        HandleEnteredState(nextState);

        StateChanged?.Invoke(
            this,
            oldState,
            nextState);
    }

    private void HandleEnteredState(EnemyRuntimeState state)
    {
        if (!initialized || navigationAgent == null)
        {
            return;
        }

        switch (state)
        {
            case EnemyRuntimeState.Alert:
                ApplyAlertDestination();
                break;

            case EnemyRuntimeState.Patrol:
                context.ClearLastKnownTargetPosition();
                patrolPausePending = false;
                nextBehaviorDecisionAt = Time.time;
                AssignPatrolDestination();
                break;

            case EnemyRuntimeState.InvestigateLastKnownPosition:
                if (context.HasLastKnownTargetPosition)
                {
                    activeBehaviorDestination =
                        context.LastKnownTargetPosition;

                    navigationAgent.SetFixedDestination(
                        activeBehaviorDestination);
                }
                break;

            case EnemyRuntimeState.SearchLastKnownPosition:
                searchCenter = context.HasLastKnownTargetPosition
                    ? context.LastKnownTargetPosition
                    : (Vector2)transform.position;

                searchEndsAt = Time.time + Mathf.Max(
                    0f,
                    GetDefinitionSearchDuration());

                nextBehaviorDecisionAt = Time.time;
                AssignSearchDestination();
                break;

            case EnemyRuntimeState.ReturnToHomeOrPatrol:
                context.ClearLastKnownTargetPosition();
                activeBehaviorDestination = context.HomeWorldPosition;
                returnHomeCount++;
                navigationAgent.SetFixedDestination(
                    activeBehaviorDestination);
                break;

            case EnemyRuntimeState.Idle:
            case EnemyRuntimeState.Hit:
            case EnemyRuntimeState.Stunned:
            case EnemyRuntimeState.Dead:
                navigationAgent.StopMovement(
                    clearLastKnownPosition: false);
                break;
        }
    }

    private void AssignPatrolDestination()
    {
        if (Time.time < nextBehaviorDecisionAt)
        {
            return;
        }

        int radius = context.Definition != null
            ? Mathf.Max(0, context.Definition.PatrolRadiusInCells)
            : 0;

        if (!TrySelectBehaviorDestination(
                context.HomeCell,
                radius,
                ref patrolDestinationSequence,
                out Vector2 destination))
        {
            navigationAgent.StopMovement(
                clearLastKnownPosition: false);

            nextBehaviorDecisionAt =
                Time.time + BehaviorRetryDelay;
            return;
        }

        activeBehaviorDestination = destination;
        patrolDestinationCount++;
        patrolPausePending = false;
        navigationAgent.SetFixedDestination(destination);
        nextBehaviorDecisionAt = Time.time;
    }

    private void AssignSearchDestination()
    {
        if (Time.time >= searchEndsAt)
        {
            return;
        }

        int radius = context.Definition != null
            ? Mathf.Max(0, context.Definition.SearchRadiusInCells)
            : 0;

        Vector2Int centerCell =
            context.PathService.WorldToCell(searchCenter);

        if (!TrySelectBehaviorDestination(
                centerCell,
                radius,
                ref searchDestinationSequence,
                out Vector2 destination))
        {
            navigationAgent.StopMovement(
                clearLastKnownPosition: false);

            nextBehaviorDecisionAt =
                Time.time + BehaviorRetryDelay;
            return;
        }

        activeBehaviorDestination = destination;
        searchDestinationCount++;
        navigationAgent.SetFixedDestination(destination);
        nextBehaviorDecisionAt = Time.time;
    }

    private bool TrySelectBehaviorDestination(
        Vector2Int centerCell,
        int radius,
        ref int sequence,
        out Vector2 destination)
    {
        destination = transform.position;

        EnemyPathService service = context.PathService;

        if (service == null || !service.IsInitialized)
        {
            return false;
        }

        behaviorCandidates.Clear();
        Vector2Int currentCell =
            service.WorldToCell(transform.position);

        int safeRadius = Mathf.Max(0, radius);

        for (int x = -safeRadius; x <= safeRadius; x++)
        {
            int remaining = safeRadius - Mathf.Abs(x);

            for (int y = -remaining; y <= remaining; y++)
            {
                Vector2Int candidate =
                    centerCell + new Vector2Int(x, y);

                if (!service.IsWalkable(candidate) ||
                    !service.AreCellsReachable(
                        currentCell,
                        candidate))
                {
                    continue;
                }

                behaviorCandidates.Add(candidate);
            }
        }

        if (behaviorCandidates.Count == 0)
        {
            return false;
        }

        int seed = StableHash(
            context.Identity != null
                ? context.Identity.InstanceId
                : context.EnemyId);

        int startIndex = PositiveModulo(
            unchecked(seed + sequence * 1103515245),
            behaviorCandidates.Count);

        sequence++;

        for (int i = 0; i < behaviorCandidates.Count; i++)
        {
            Vector2Int candidate = behaviorCandidates[
                (startIndex + i) % behaviorCandidates.Count];

            if (behaviorCandidates.Count > 1 &&
                candidate == currentCell)
            {
                continue;
            }

            destination = service.CellToWorld(candidate);
            return true;
        }

        destination = service.CellToWorld(
            behaviorCandidates[startIndex]);

        return true;
    }

    private void TryBroadcastAlert(Vector2 observedPosition)
    {
        EnemyDefinition definition = context != null
            ? context.Definition
            : null;

        if (definition == null ||
            !definition.BroadcastsAlert ||
            definition.AlertRadius <= 0f ||
            Time.time < nextAlertBroadcastAt)
        {
            return;
        }

        if (alertManager == null)
        {
            alertManager =
                UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        }

        if (alertManager == null)
        {
            return;
        }

        int delivered = alertManager.BroadcastAlert(
            this,
            observedPosition,
            definition.AlertRadius);

        alertBroadcastCount++;
        alertRecipientCount += delivered;
        lastAlertBroadcastAt = Time.time;
        lastAlertPosition = observedPosition;
        nextAlertBroadcastAt = Time.time + Mathf.Max(
            0f,
            definition.AlertBroadcastCooldown);
    }

    private void ApplyAlertDestination()
    {
        if (context == null ||
            navigationAgent == null ||
            !context.HasLastKnownTargetPosition)
        {
            return;
        }

        activeBehaviorDestination =
            context.LastKnownTargetPosition;

        navigationAgent.SetFixedDestination(
            activeBehaviorDestination);
    }

    private bool IsNearHome()
    {
        if (context == null)
        {
            return true;
        }

        float tolerance = context.Definition != null
            ? Mathf.Max(
                0.05f,
                context.Definition.LastPositionTolerance)
            : 0.15f;

        return Vector2.Distance(
                   transform.position,
                   context.HomeWorldPosition) <= tolerance;
    }

    private float GetDefinitionSearchDuration()
    {
        return context != null && context.Definition != null
            ? context.Definition.SearchDuration
            : 0f;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            string safeValue = value ?? string.Empty;

            for (int i = 0; i < safeValue.Length; i++)
            {
                hash = hash * 31 + safeValue[i];
            }

            return hash;
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        int result = value % modulus;
        return result < 0 ? result + modulus : result;
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

        if (alertManager == null)
        {
            alertManager =
                UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
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
