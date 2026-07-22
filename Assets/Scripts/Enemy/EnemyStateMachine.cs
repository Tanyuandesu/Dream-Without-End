using System;
using UnityEngine;

/// <summary>
/// Observable lifecycle and perception state owner for one enemy.
///
/// EA2 deliberately preserves the old chase movement through TestEnemyAI as a
/// temporary locomotion adapter. EA3 replaces its path implementation without
/// changing this state contract.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRuntimeContext))]
[RequireComponent(typeof(TestEnemyAI))]
[RequireComponent(typeof(Health))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    [Header("EA2 runtime state (read only during Play Mode)")]
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
    [SerializeField] private TestEnemyAI legacyChaseAdapter;

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
            legacyChaseAdapter != null)
        {
            Initialize(context, legacyChaseAdapter);
        }
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        TestEnemyAI newLegacyChaseAdapter)
    {
        UnsubscribeFromHealth();

        context = newContext;
        legacyChaseAdapter = newLegacyChaseAdapter;

        CacheComponents();

        initialized =
            context != null &&
            context.IsInitialized &&
            legacyChaseAdapter != null &&
            legacyChaseAdapter.IsInitialized;

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
            "EA2 baseline ready");
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

            legacyChaseAdapter.ObserveDetectedTarget(
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

        legacyChaseAdapter.TickFixed(currentState);

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

        legacyChaseAdapter.StopMovement(
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
                "[EnemyStateMachine/EA2] " +
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

        if (legacyChaseAdapter == null)
        {
            legacyChaseAdapter = GetComponent<TestEnemyAI>();
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
