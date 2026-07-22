using System;
using UnityEngine;

/// <summary>
/// Observable lifecycle and perception state owner for one enemy.
///
/// EA3 preserves the accepted state vocabulary while replacing the old chase
/// locomotion with EnemyNavigationAgent.
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

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyNavigationAgent navigationAgent;

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
    public bool LogsStateTransitions => logStateTransitions;

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
            navigationAgent.IsInitialized;

        previousState = EnemyRuntimeState.Spawn;
        currentState = EnemyRuntimeState.Spawn;
        stateEnteredAt = Time.time;
        transitionCount = 0;
        lastTransitionReason = initialized
            ? "Runtime initialized"
            : "Runtime references incomplete";

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

    private void Update()
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

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

    private void FixedUpdate()
    {
        if (!initialized ||
            currentState == EnemyRuntimeState.Dead)
        {
            return;
        }

        navigationAgent.TickFixed(currentState);

        if (currentState ==
                EnemyRuntimeState.InvestigateLastKnownPosition &&
            !context.HasLastKnownTargetPosition)
        {
            TransitionTo(
                EnemyRuntimeState.Idle,
                "Last known position reached");
        }
    }

    private void HandleDied(Health health)
    {
        if (!initialized)
        {
            return;
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
                "[EnemyStateMachine/EA3] " +
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

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }
}
