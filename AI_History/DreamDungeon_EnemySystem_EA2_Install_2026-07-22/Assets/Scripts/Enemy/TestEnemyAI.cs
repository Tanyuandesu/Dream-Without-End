using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EA2 compatibility adapter for the pre-EA2 chase locomotion.
///
/// EnemyStateMachine now owns state transitions and lifecycle. This component
/// only preserves the established A* request, waypoint following and direct
/// same-cell movement until navigation is replaced in EA3.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPathfinder))]
[RequireComponent(typeof(EnemyDetection))]
public sealed class TestEnemyAI : MonoBehaviour
{
    [Header("EA2 legacy movement settings")]
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float waypointTolerance = 0.035f;
    [SerializeField] private float stopDistance = 0.72f;
    [SerializeField] private float lastPositionTolerance = 0.15f;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;

    [Header("Runtime path snapshot")]
    [SerializeField] private bool initialized;
    [SerializeField] private int currentPathCount;
    [SerializeField] private int pathIndex;
    [SerializeField] private bool hasCurrentWaypoint;
    [SerializeField] private Vector2 currentWaypoint;
    [SerializeField] private bool hasKnownTargetCell;
    [SerializeField] private Vector2Int knownTargetCell;
    [SerializeField] private bool hasQueuedRepath;
    [SerializeField] private Vector2 queuedDestination;

    private Rigidbody2D body;
    private Transform target;
    private EnemyPathfinder pathfinder;
    private EnemyDetection detection;

    private List<Vector2> currentPath =
        new List<Vector2>();

    public bool IsInitialized => initialized;
    public EnemyRuntimeContext Context => context;
    public bool HasActivePath =>
        currentPath != null &&
        pathIndex < currentPath.Count;

    public int RemainingWaypointCount =>
        HasActivePath
            ? currentPath.Count - pathIndex
            : 0;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// EA2 initialization used by EnemySpawner.
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

        target = context != null
            ? context.CurrentTarget
            : null;

        if (context != null)
        {
            body = context.Body;
            pathfinder = context.Pathfinder;
            detection = context.Detection;
        }

        ApplySettings(
            newMoveSpeed,
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance);

        ResetMovementState();
        initialized = HasRequiredReferences();
    }

    /// <summary>
    /// Legacy entry retained for compatibility with any external test setup.
    /// It constructs the EA2 context and state machine instead of keeping an
    /// independent Update/FixedUpdate AI loop.
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
        target = newTarget;
        pathfinder = newPathfinder;
        detection = newDetection;
        CacheComponents();

        EnemyRuntimeContext runtimeContext =
            GetComponent<EnemyRuntimeContext>();

        if (runtimeContext == null)
        {
            runtimeContext =
                gameObject.AddComponent<EnemyRuntimeContext>();
        }

        EnemyRuntimeIdentity identity =
            GetComponent<EnemyRuntimeIdentity>();

        EnemyDefinition definition = identity != null
            ? identity.Definition
            : null;

        Vector2Int homeCell = pathfinder != null
            ? pathfinder.WorldToCell(transform.position)
            : Vector2Int.RoundToInt(
                (Vector2)transform.position);

        runtimeContext.Initialize(
            identity,
            definition,
            newTarget,
            body,
            pathfinder,
            detection,
            GetComponent<Health>(),
            GetComponent<EnemyVisual>(),
            identity != null ? identity.RoomIndex : -1,
            identity != null ? identity.SpawnCell : homeCell,
            transform.position);

        Initialize(
            runtimeContext,
            newMoveSpeed,
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance);

        EnemyStateMachine stateMachine =
            GetComponent<EnemyStateMachine>();

        if (stateMachine == null)
        {
            stateMachine =
                gameObject.AddComponent<EnemyStateMachine>();
        }

        stateMachine.Initialize(runtimeContext, this);
    }

    public void ApplySettings(
        float newMoveSpeed,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        waypointTolerance = Mathf.Clamp(
            newWaypointTolerance,
            0.001f,
            0.25f);

        stopDistance = Mathf.Max(0f, newStopDistance);
        lastPositionTolerance = Mathf.Clamp(
            newLastPositionTolerance,
            0.01f,
            1f);
    }

    public void ObserveDetectedTarget(Vector2 observedPosition)
    {
        if (!initialized)
        {
            return;
        }

        context.SetLastKnownTargetPosition(observedPosition);

        Vector2Int targetCell =
            pathfinder.WorldToCell(observedPosition);

        if (hasKnownTargetCell &&
            targetCell == knownTargetCell)
        {
            return;
        }

        knownTargetCell = targetCell;
        hasKnownTargetCell = true;

        RequestRepath(observedPosition);
    }

    public void TickFixed(EnemyRuntimeState state)
    {
        if (!initialized ||
            (state != EnemyRuntimeState.Chase &&
             state !=
                 EnemyRuntimeState.InvestigateLastKnownPosition))
        {
            return;
        }

        if (detection.IsTargetDetected &&
            Vector2.Distance(
                body.position,
                target.position) <= stopDistance)
        {
            return;
        }

        if (!HasActivePath && hasQueuedRepath)
        {
            ApplyQueuedRepath(body.position);
        }

        if (HasActivePath)
        {
            FollowCurrentWaypoint();
            return;
        }

        HandleMovementWithoutPath();
    }

    public void StopMovement(bool clearLastKnownPosition)
    {
        hasKnownTargetCell = false;
        hasQueuedRepath = false;

        if (currentPath == null)
        {
            currentPath = new List<Vector2>();
        }
        else
        {
            currentPath.Clear();
        }

        pathIndex = 0;

        if (context != null)
        {
            context.ClearNavigationDestination();

            if (clearLastKnownPosition)
            {
                context.ClearLastKnownTargetPosition();
            }
        }

        RefreshPathSnapshot();
    }

    private bool HasRequiredReferences()
    {
        return context != null &&
               body != null &&
               target != null &&
               pathfinder != null &&
               detection != null;
    }

    private void RequestRepath(Vector2 destination)
    {
        queuedDestination = destination;
        hasQueuedRepath = true;
        context.SetNavigationDestination(destination);

        if (!HasActivePath)
        {
            ApplyQueuedRepath(body.position);
        }

        RefreshPathSnapshot();
    }

    private void ApplyQueuedRepath(Vector2 startPosition)
    {
        if (!hasQueuedRepath)
        {
            return;
        }

        currentPath = pathfinder.FindPath(
            startPosition,
            queuedDestination);

        if (currentPath == null)
        {
            currentPath = new List<Vector2>();
        }

        pathIndex = 0;
        hasQueuedRepath = false;
        RefreshPathSnapshot();
    }

    private void FollowCurrentWaypoint()
    {
        Vector2 waypoint = currentPath[pathIndex];
        float step = moveSpeed * Time.fixedDeltaTime;

        Vector2 nextPosition = Vector2.MoveTowards(
            body.position,
            waypoint,
            step);

        bool reachedWaypoint =
            Vector2.Distance(
                nextPosition,
                waypoint) <= waypointTolerance;

        if (reachedWaypoint)
        {
            nextPosition = waypoint;
        }

        body.MovePosition(nextPosition);

        if (!reachedWaypoint)
        {
            RefreshPathSnapshot();
            return;
        }

        pathIndex++;

        if (hasQueuedRepath)
        {
            ApplyQueuedRepath(waypoint);
            return;
        }

        RefreshPathSnapshot();
    }

    private void HandleMovementWithoutPath()
    {
        if (!context.HasLastKnownTargetPosition)
        {
            return;
        }

        Vector2 lastKnownPosition =
            context.LastKnownTargetPosition;

        if (pathfinder.AreInSameCell(
                body.position,
                lastKnownPosition))
        {
            Vector2 destination =
                detection.IsTargetDetected
                    ? (Vector2)target.position
                    : lastKnownPosition;

            MoveDirectly(destination);
            return;
        }

        RequestRepath(lastKnownPosition);

        if (!detection.IsTargetDetected &&
            Vector2.Distance(
                body.position,
                lastKnownPosition) <= lastPositionTolerance)
        {
            ClearSearchState();
        }
    }

    private void MoveDirectly(Vector2 destination)
    {
        float distance =
            Vector2.Distance(body.position, destination);

        if (distance <= lastPositionTolerance)
        {
            if (!detection.IsTargetDetected)
            {
                ClearSearchState();
            }

            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            body.position,
            destination,
            moveSpeed * Time.fixedDeltaTime);

        body.MovePosition(nextPosition);
    }

    private void ClearSearchState()
    {
        StopMovement(clearLastKnownPosition: true);
    }

    private void ResetMovementState()
    {
        initialized = false;
        hasKnownTargetCell = false;
        knownTargetCell = default(Vector2Int);
        hasQueuedRepath = false;
        queuedDestination = Vector2.zero;

        if (currentPath == null)
        {
            currentPath = new List<Vector2>();
        }
        else
        {
            currentPath.Clear();
        }

        pathIndex = 0;

        if (context != null)
        {
            context.ClearLastKnownTargetPosition();
            context.ClearNavigationDestination();
        }

        RefreshPathSnapshot();
    }

    private void RefreshPathSnapshot()
    {
        currentPathCount = currentPath != null
            ? currentPath.Count
            : 0;

        hasCurrentWaypoint = HasActivePath;
        currentWaypoint = hasCurrentWaypoint
            ? currentPath[pathIndex]
            : Vector2.zero;
    }

    private void CacheComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (pathfinder == null)
        {
            pathfinder = GetComponent<EnemyPathfinder>();
        }

        if (detection == null)
        {
            detection = GetComponent<EnemyDetection>();
        }

        if (context == null)
        {
            context = GetComponent<EnemyRuntimeContext>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (currentPath == null ||
            currentPath.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Vector3 previous = transform.position;

        for (int i = pathIndex; i < currentPath.Count; i++)
        {
            Vector3 next = currentPath[i];
            Gizmos.DrawLine(previous, next);
            Gizmos.DrawSphere(next, 0.07f);
            previous = next;
        }
    }
}
