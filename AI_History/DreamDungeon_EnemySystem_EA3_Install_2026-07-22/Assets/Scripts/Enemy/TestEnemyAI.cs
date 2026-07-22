using UnityEngine;

/// <summary>
/// EA3 compatibility bridge for code and audits that still identify the old
/// TestEnemyAI component.
///
/// It owns no Update, FixedUpdate, A* data or Rigidbody movement. Every public
/// movement call is forwarded to EnemyNavigationAgent, so only the EA3 stack
/// can execute navigation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPathfinder))]
[RequireComponent(typeof(EnemyDetection))]
public sealed class TestEnemyAI : MonoBehaviour
{
    [Header("EA3 compatibility bridge")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyNavigationAgent navigationAgent;
    [SerializeField] private bool initialized;

    public bool IsInitialized =>
        initialized &&
        context != null &&
        navigationAgent != null &&
        navigationAgent.IsInitialized;

    public EnemyRuntimeContext Context => context;
    public EnemyNavigationAgent NavigationAgent => navigationAgent;
    public bool HasActivePath =>
        navigationAgent != null &&
        navigationAgent.HasActivePath;

    public int RemainingWaypointCount =>
        navigationAgent != null
            ? navigationAgent.RemainingWaypointCount
            : 0;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        EnemyNavigationAgent newNavigationAgent)
    {
        context = newContext;
        navigationAgent = newNavigationAgent;
        CacheComponents();

        initialized =
            context != null &&
            context.IsInitialized &&
            navigationAgent != null &&
            navigationAgent.IsInitialized;
    }

    /// <summary>
    /// Retains the EA2 setup signature. Production spawning initializes the
    /// agent first, so these values are already authoritative there.
    /// </summary>
    public void Initialize(
        EnemyRuntimeContext newContext,
        float newMoveSpeed,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance)
    {
        context = newContext;
        CacheComponents();

        if (navigationAgent == null)
        {
            TryCreateNavigationStack(
                newMoveSpeed,
                newWaypointTolerance,
                newStopDistance,
                newLastPositionTolerance);
        }

        Initialize(context, navigationAgent);
    }

    /// <summary>
    /// Retains the pre-EA2 isolated-test entry and builds the EA3 stack around
    /// the supplied target, detection and already initialized pathfinder.
    /// </summary>
    public void Initialize(
        Transform newTarget,
        EnemyPathfinder newPathfinder,
        EnemyDetection newDetection,
        float newMoveSpeed,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance)
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        EnemyRuntimeIdentity identity =
            GetComponent<EnemyRuntimeIdentity>();

        EnemyDefinition definition = identity != null
            ? identity.Definition
            : null;

        EnemyPathfinder resolvedPathfinder = newPathfinder != null
            ? newPathfinder
            : GetComponent<EnemyPathfinder>();

        EnemyDetection resolvedDetection = newDetection != null
            ? newDetection
            : GetComponent<EnemyDetection>();

        EnemyRuntimeContext runtimeContext =
            GetComponent<EnemyRuntimeContext>();

        if (runtimeContext == null)
        {
            runtimeContext =
                gameObject.AddComponent<EnemyRuntimeContext>();
        }

        Vector2Int homeCell = resolvedPathfinder != null
            ? resolvedPathfinder.WorldToCell(transform.position)
            : Vector2Int.RoundToInt(
                (Vector2)transform.position);

        runtimeContext.Initialize(
            identity,
            definition,
            newTarget,
            body,
            resolvedPathfinder,
            resolvedDetection,
            GetComponent<Health>(),
            GetComponent<EnemyVisual>(),
            identity != null ? identity.RoomIndex : -1,
            identity != null ? identity.SpawnCell : homeCell,
            transform.position);

        context = runtimeContext;
        navigationAgent = GetComponent<EnemyNavigationAgent>();

        TryCreateNavigationStack(
            newMoveSpeed,
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance);

        Initialize(runtimeContext, navigationAgent);

        EnemyStateMachine stateMachine =
            GetComponent<EnemyStateMachine>();

        if (stateMachine == null)
        {
            stateMachine =
                gameObject.AddComponent<EnemyStateMachine>();
        }

        stateMachine.Initialize(
            runtimeContext,
            navigationAgent);
    }

    public void ApplySettings(
        float newMoveSpeed,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance)
    {
        if (navigationAgent == null)
        {
            TryCreateNavigationStack(
                newMoveSpeed,
                newWaypointTolerance,
                newStopDistance,
                newLastPositionTolerance);
        }

        EnemyMotor2D motor = navigationAgent != null
            ? navigationAgent.Motor
            : GetComponent<EnemyMotor2D>();

        if (motor != null)
        {
            motor.ApplySpeed(newMoveSpeed);
        }

        if (navigationAgent != null)
        {
            navigationAgent.ApplyLegacyMovementSettings(
                newWaypointTolerance,
                newStopDistance,
                newLastPositionTolerance);
        }
    }

    public void ObserveDetectedTarget(Vector2 observedPosition)
    {
        if (navigationAgent != null)
        {
            navigationAgent.ObserveDetectedTarget(
                observedPosition);
        }
    }

    public void TickFixed(EnemyRuntimeState state)
    {
        if (navigationAgent != null)
        {
            navigationAgent.TickFixed(state);
        }
    }

    public void StopMovement(bool clearLastKnownPosition)
    {
        if (navigationAgent != null)
        {
            navigationAgent.StopMovement(
                clearLastKnownPosition);
        }
    }

    private void TryCreateNavigationStack(
        float newMoveSpeed,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance)
    {
        CacheComponents();

        EnemyPathfinder pathfinder =
            GetComponent<EnemyPathfinder>();

        EnemyPathService service = pathfinder != null
            ? pathfinder.Service
            : null;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        EnemyMotor2D motor = GetComponent<EnemyMotor2D>();

        if (motor == null)
        {
            motor = gameObject.AddComponent<EnemyMotor2D>();
        }

        motor.Initialize(body, newMoveSpeed);

        if (navigationAgent == null)
        {
            navigationAgent =
                gameObject.AddComponent<EnemyNavigationAgent>();
        }

        navigationAgent.Initialize(
            context,
            service,
            pathfinder,
            motor,
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance,
            0.08f,
            0.75f,
            0.015f,
            3,
            0.8f,
            0.5f,
            0);

        if (context != null)
        {
            context.AttachNavigation(
                service,
                navigationAgent,
                motor);
        }
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
}
