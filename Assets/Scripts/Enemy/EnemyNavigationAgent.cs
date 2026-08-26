using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-enemy navigation state and waypoint follower.
///
/// Path computation belongs to the shared EnemyPathService and physical
/// movement belongs to EnemyMotor2D. This agent only owns request timing,
/// destination changes, waypoint progress and stuck recovery.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPathfinder))]
[RequireComponent(typeof(EnemyMotor2D))]
public sealed class EnemyNavigationAgent : MonoBehaviour
{
    [Header("EA3 navigation settings")]
    [SerializeField] private float waypointTolerance = 0.035f;
    [SerializeField] private float stopDistance = 0.72f;
    [SerializeField] private float lastPositionTolerance = 0.15f;

    [Min(0.02f)]
    [SerializeField] private float minimumRepathInterval = 0.08f;

    [Min(0.1f)]
    [SerializeField] private float stuckTimeout = 0.75f;

    [Min(0.001f)]
    [SerializeField] private float stuckMovementThreshold = 0.015f;

    [Range(1, 8)]
    [SerializeField] private int maximumRecoveryAttempts = 3;

    [Min(0.05f)]
    [SerializeField] private float maximumRecoverySnapDistance = 0.8f;

    [Min(0.05f)]
    [SerializeField] private float failedRequestRetryDelay = 0.5f;

    [Tooltip(
        "Maximum A* path cost used while tracking a detected target. " +
        "Zero means unlimited; EnemySpawner supplies EnemyDefinition." +
        "MaximumChasePathCost in T5B.")]
    [SerializeField] private int maximumPathCostInCells;

    [Header("Runtime references")]
    [SerializeField] private EnemyRuntimeContext context;
    [SerializeField] private EnemyPathService pathService;
    [SerializeField] private EnemyPathfinder pathfinder;
    [SerializeField] private EnemyMotor2D motor;

    [Header("Runtime request snapshot")]
    [SerializeField] private bool initialized;
    [SerializeField] private EnemyNavigationStatus navigationStatus =
        EnemyNavigationStatus.Uninitialized;

    [SerializeField] private EnemyNavigationIntent navigationIntent =
        EnemyNavigationIntent.None;

    [SerializeField] private bool destinationReached;
    [SerializeField] private int activeMaximumPathCostInCells;
    [SerializeField] private bool hasDesiredDestination;
    [SerializeField] private Vector2 desiredDestination;
    [SerializeField] private bool hasKnownTargetCell;
    [SerializeField] private Vector2Int knownTargetCell;
    [SerializeField] private bool hasQueuedRepath;
    [SerializeField] private bool waitingAtWaypointForRepath;
    [SerializeField] private bool pathRequestPending;

    [Tooltip(
        "T6B.3 external movement hold owned by EnemyStateMachine while a " +
        "melee target remains inside the engagement hysteresis envelope.")]
    [SerializeField] private bool externalMovementHoldActive;

    [SerializeField] private int pendingRequestId;
    [SerializeField] private bool pendingRequestUsesWaypointAnchor;
    [SerializeField] private Vector2Int pendingStartCell;
    [SerializeField] private float nextAllowedRepathAt;
    [SerializeField] private float retryFailedRequestAt;

    [Header("Runtime path snapshot")]
    [SerializeField] private int currentPathCount;
    [SerializeField] private int pathIndex;
    [SerializeField] private bool hasCurrentWaypoint;
    [SerializeField] private Vector2 currentWaypoint;
    [SerializeField] private int lastPathCost;
    [SerializeField] private int lastExpandedNodeCount;
    [SerializeField] private bool hasBufferedReplacementPath;
    [SerializeField] private Vector2Int bufferedReplacementStartCell;
    [SerializeField] private int bufferedReplacementPathCount;

    [Header("Runtime failure and recovery snapshot")]
    [SerializeField] private EnemyPathFailureReason lastFailureReason;
    [SerializeField] private string lastFailureDetails = string.Empty;
    [SerializeField] private float noProgressDuration;
    [SerializeField] private bool isStuck;
    [SerializeField] private int recoveryAttemptCount;
    [SerializeField] private int totalRecoveryCount;
    [SerializeField] private int submittedRequestCount;
    [SerializeField] private int acceptedPathCount;
    [SerializeField] private int failedPathCount;
    [SerializeField] private int prefetchedRequestCount;
    [SerializeField] private int seamlessPathSwapCount;
    [SerializeField] private int repathWaitCount;

    [Header("Optional diagnostics")]
    [SerializeField] private bool showPathGizmos = true;

    private Rigidbody2D body;
    private Transform target;
    private EnemyDetection detection;
    private readonly List<Vector2> currentPath =
        new List<Vector2>();

    private readonly List<Vector2> bufferedReplacementPath =
        new List<Vector2>();

    private Vector2 progressReferencePosition;
    private bool movementExpectedLastTick;
    private Vector2Int pendingGoalCell;

    public bool IsInitialized => initialized;
    public EnemyRuntimeContext Context => context;
    public EnemyPathService PathService => pathService;
    public EnemyPathfinder Pathfinder => pathfinder;
    public EnemyMotor2D Motor => motor;
    public EnemyNavigationStatus NavigationStatus => navigationStatus;
    public EnemyNavigationIntent NavigationIntent => navigationIntent;
    public bool HasDesiredDestination => hasDesiredDestination;
    public Vector2 DesiredDestination => desiredDestination;
    public bool HasReachedDestination => destinationReached;
    public bool SupportsExtendedBehaviorStates => true;
    public bool HasActivePath => pathIndex < currentPath.Count;
    public int RemainingWaypointCount => HasActivePath
        ? currentPath.Count - pathIndex
        : 0;

    public bool IsPathRequestPending => pathRequestPending;
    public int PendingRequestId => pendingRequestId;
    public bool HasQueuedRepath => hasQueuedRepath;
    public EnemyPathFailureReason LastFailureReason => lastFailureReason;
    public string LastFailureDetails => lastFailureDetails;
    public bool IsStuck => isStuck;
    public float NoProgressDuration => noProgressDuration;
    public int RecoveryAttemptCount => recoveryAttemptCount;
    public int TotalRecoveryCount => totalRecoveryCount;
    public int SubmittedRequestCount => submittedRequestCount;
    public int AcceptedPathCount => acceptedPathCount;
    public int FailedPathCount => failedPathCount;
    public int PrefetchedRequestCount => prefetchedRequestCount;
    public int SeamlessPathSwapCount => seamlessPathSwapCount;
    public int RepathWaitCount => repathWaitCount;
    public int ConfiguredMaximumPathCostInCells =>
        maximumPathCostInCells;
    public int ActiveMaximumPathCostInCells =>
        activeMaximumPathCostInCells;
    public int LastPathCost => lastPathCost;
    public bool IsExternalMovementHoldActive =>
        externalMovementHoldActive;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        EnemyRuntimeContext newContext,
        EnemyPathService newPathService,
        EnemyPathfinder newPathfinder,
        EnemyMotor2D newMotor,
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance,
        float newMinimumRepathInterval,
        float newStuckTimeout,
        float newStuckMovementThreshold,
        int newMaximumRecoveryAttempts,
        float newMaximumRecoverySnapDistance,
        float newFailedRequestRetryDelay,
        int newMaximumPathCostInCells)
    {
        CancelPendingRequest();

        context = newContext;
        pathService = newPathService;
        pathfinder = newPathfinder;
        motor = newMotor;
        CacheComponents();

        target = context != null
            ? context.CurrentTarget
            : null;

        if (context != null)
        {
            body = context.Body;
            detection = context.Detection;
        }

        ApplySettings(
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance,
            newMinimumRepathInterval,
            newStuckTimeout,
            newStuckMovementThreshold,
            newMaximumRecoveryAttempts,
            newMaximumRecoverySnapDistance,
            newFailedRequestRetryDelay,
            newMaximumPathCostInCells);

        activeMaximumPathCostInCells = maximumPathCostInCells;
        ResetNavigationState();

        initialized =
            context != null &&
            context.IsInitialized &&
            pathService != null &&
            pathService.IsInitialized &&
            pathfinder != null &&
            motor != null &&
            motor.IsInitialized &&
            body != null &&
            target != null &&
            detection != null;

        navigationStatus = initialized
            ? EnemyNavigationStatus.Idle
            : EnemyNavigationStatus.Uninitialized;
    }

    public void ApplySettings(
        float newWaypointTolerance,
        float newStopDistance,
        float newLastPositionTolerance,
        float newMinimumRepathInterval,
        float newStuckTimeout,
        float newStuckMovementThreshold,
        int newMaximumRecoveryAttempts,
        float newMaximumRecoverySnapDistance,
        float newFailedRequestRetryDelay,
        int newMaximumPathCostInCells)
    {
        waypointTolerance = Mathf.Clamp(
            newWaypointTolerance,
            0.001f,
            0.25f);

        stopDistance = Mathf.Max(0f, newStopDistance);
        lastPositionTolerance = Mathf.Clamp(
            newLastPositionTolerance,
            0.01f,
            1f);

        minimumRepathInterval = Mathf.Max(
            0.02f,
            newMinimumRepathInterval);

        stuckTimeout = Mathf.Max(0.1f, newStuckTimeout);
        stuckMovementThreshold = Mathf.Max(
            0.001f,
            newStuckMovementThreshold);

        maximumRecoveryAttempts = Mathf.Clamp(
            newMaximumRecoveryAttempts,
            1,
            8);

        maximumRecoverySnapDistance = Mathf.Max(
            0.05f,
            newMaximumRecoverySnapDistance);

        failedRequestRetryDelay = Mathf.Max(
            0.05f,
            newFailedRequestRetryDelay);

        maximumPathCostInCells = Mathf.Max(
            0,
            newMaximumPathCostInCells);
    }

    public bool SetFixedDestination(
        Vector2 destination,
        int maximumPathCost = 0)
    {
        if (!initialized)
        {
            return false;
        }

        ClearFailureSnapshot();
        navigationIntent = EnemyNavigationIntent.FixedDestination;
        activeMaximumPathCostInCells = Mathf.Max(0, maximumPathCost);
        destinationReached = false;

        SetDesiredDestination(
            destination,
            allowImmediatePrefetch: false);

        return true;
    }

    public void ObserveDetectedTarget(Vector2 observedPosition)
    {
        if (!initialized)
        {
            return;
        }

        if (navigationIntent != EnemyNavigationIntent.TrackingTarget)
        {
            ClearFailureSnapshot();
        }

        navigationIntent = EnemyNavigationIntent.TrackingTarget;
        activeMaximumPathCostInCells = maximumPathCostInCells;
        destinationReached = false;
        context.SetLastKnownTargetPosition(observedPosition);

        SetDesiredDestination(
            observedPosition,
            allowImmediatePrefetch: true);
    }

    private void SetDesiredDestination(
        Vector2 destination,
        bool allowImmediatePrefetch)
    {
        desiredDestination = destination;
        hasDesiredDestination = true;
        context.SetNavigationDestination(destination);

        Vector2Int targetCell =
            pathService.WorldToCell(destination);

        bool cellChanged =
            !hasKnownTargetCell ||
            targetCell != knownTargetCell;

        if (!cellChanged)
        {
            return;
        }

        if (!allowImmediatePrefetch)
        {
            CancelPendingRequest();
            currentPath.Clear();
            pathIndex = 0;
            waitingAtWaypointForRepath = false;
            motor.Stop();
            ResetProgressTracking();
        }

        knownTargetCell = targetCell;
        hasKnownTargetCell = true;
        hasQueuedRepath = true;
        DiscardBufferedReplacementPath();
        RefreshPathSnapshot();

        if (pathRequestPending && !HasActivePath)
        {
            CancelPendingRequest();
        }

        if (allowImmediatePrefetch &&
            !pathRequestPending &&
            HasActivePath &&
            Time.time >= nextAllowedRepathAt)
        {
            TrySubmitPathRequest(
                preferActiveWaypointAnchor: true);
        }
    }

    public void SetExternalMovementHold(bool active)
    {
        if (externalMovementHoldActive == active)
        {
            return;
        }

        externalMovementHoldActive = active;

        if (!active || motor == null)
        {
            return;
        }

        motor.Stop();
        ResetProgressTracking();
        navigationStatus = EnemyNavigationStatus.Stopped;
    }

    public void TickFixed(EnemyRuntimeState state)
    {
        if (!initialized)
        {
            return;
        }

        if (externalMovementHoldActive)
        {
            motor.Stop();
            ResetProgressTracking();
            navigationStatus = EnemyNavigationStatus.Stopped;
            return;
        }

        if (!IsMovementState(state))
        {
            motor.Stop();
            ResetProgressTracking();
            navigationStatus = EnemyNavigationStatus.Idle;
            return;
        }

        if (EvaluateMovementProgressAndRecover())
        {
            return;
        }

        if (state == EnemyRuntimeState.Chase &&
            navigationIntent == EnemyNavigationIntent.TrackingTarget &&
            detection.IsTargetDetected &&
            Vector2.Distance(
                body.position,
                target.position) <= stopDistance)
        {
            motor.Stop();
            ResetProgressTracking();
            navigationStatus = EnemyNavigationStatus.Stopped;
            return;
        }

        EnsureDesiredDestinationFromContext(state);

        if (!hasDesiredDestination)
        {
            motor.Stop();
            ResetProgressTracking();
            navigationStatus = EnemyNavigationStatus.Idle;
            return;
        }

        if (navigationStatus == EnemyNavigationStatus.Failed &&
            Time.time < retryFailedRequestAt)
        {
            motor.Stop();
            return;
        }

        if (hasBufferedReplacementPath &&
            IsAtCellCenter(bufferedReplacementStartCell))
        {
            ApplyBufferedReplacementPath();
        }

        if (pathRequestPending &&
            (!HasActivePath || waitingAtWaypointForRepath))
        {
            motor.Stop();
            navigationStatus = EnemyNavigationStatus.WaitingForPath;
            return;
        }

        if (waitingAtWaypointForRepath || !HasActivePath)
        {
            if (ShouldMoveDirectlyWithinGoalCell())
            {
                MoveDirectlyWithinGoalCell();
                return;
            }

            if (hasQueuedRepath || !HasActivePath)
            {
                TrySubmitPathRequest(
                    preferActiveWaypointAnchor: HasActivePath);
                return;
            }
        }

        if (hasQueuedRepath &&
            HasActivePath &&
            !pathRequestPending)
        {
            TrySubmitPathRequest(
                preferActiveWaypointAnchor: true);
        }

        if (HasActivePath)
        {
            FollowCurrentWaypoint();
            return;
        }

        motor.Stop();
        navigationStatus = EnemyNavigationStatus.Idle;
    }

    private static bool IsMovementState(EnemyRuntimeState state)
    {
        return state == EnemyRuntimeState.Patrol ||
               state == EnemyRuntimeState.Alert ||
               state == EnemyRuntimeState.Chase ||
               state == EnemyRuntimeState.InvestigateLastKnownPosition ||
               state == EnemyRuntimeState.SearchLastKnownPosition ||
               state == EnemyRuntimeState.ReturnToHomeOrPatrol;
    }

    private void MarkDestinationReached()
    {
        CancelPendingRequest();
        currentPath.Clear();
        DiscardBufferedReplacementPath();
        pathIndex = 0;
        hasCurrentWaypoint = false;
        currentWaypoint = Vector2.zero;
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;
        destinationReached = true;
        motor.Stop();
        ResetProgressTracking();

        if (context != null)
        {
            context.ClearNavigationDestination();
        }

        navigationStatus = EnemyNavigationStatus.Stopped;
        RefreshPathSnapshot();
    }

    public void StopMovement(bool clearLastKnownPosition)
    {
        CancelPendingRequest();
        currentPath.Clear();
        pathIndex = 0;
        hasCurrentWaypoint = false;
        currentWaypoint = Vector2.zero;
        navigationIntent = EnemyNavigationIntent.None;
        destinationReached = false;
        activeMaximumPathCostInCells = maximumPathCostInCells;
        hasDesiredDestination = false;
        hasKnownTargetCell = false;
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;
        DiscardBufferedReplacementPath();
        isStuck = false;
        recoveryAttemptCount = 0;
        motor.Stop();
        ResetProgressTracking();

        if (context != null)
        {
            context.ClearNavigationDestination();

            if (clearLastKnownPosition)
            {
                context.ClearLastKnownTargetPosition();
            }
        }

        navigationStatus = EnemyNavigationStatus.Idle;
        RefreshPathSnapshot();
    }

    private void EnsureDesiredDestinationFromContext(
        EnemyRuntimeState state)
    {
        if (hasDesiredDestination ||
            !context.HasLastKnownTargetPosition)
        {
            return;
        }

        if (state != EnemyRuntimeState.Chase &&
            state != EnemyRuntimeState.Alert &&
            state != EnemyRuntimeState.InvestigateLastKnownPosition)
        {
            return;
        }

        Vector2 rememberedPosition =
            context.LastKnownTargetPosition;

        navigationIntent = state == EnemyRuntimeState.Chase
            ? EnemyNavigationIntent.TrackingTarget
            : EnemyNavigationIntent.FixedDestination;

        SetDesiredDestination(
            rememberedPosition,
            allowImmediatePrefetch: false);
    }

    private bool ShouldMoveDirectlyWithinGoalCell()
    {
        return hasDesiredDestination &&
               pathService.AreInSameCell(
                   body.position,
                   desiredDestination);
    }

    private void MoveDirectlyWithinGoalCell()
    {
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;

        Vector2 destination =
            navigationIntent == EnemyNavigationIntent.TrackingTarget &&
            detection.IsTargetDetected
                ? (Vector2)target.position
                : desiredDestination;

        float distance = Vector2.Distance(
            body.position,
            destination);

        if (distance <= lastPositionTolerance)
        {
            MarkDestinationReached();
            return;
        }

        navigationStatus = EnemyNavigationStatus.DirectMovement;
        movementExpectedLastTick = true;
        motor.MoveTowards(destination, lastPositionTolerance);
    }

    private void TrySubmitPathRequest(
        bool preferActiveWaypointAnchor)
    {
        if (!hasDesiredDestination)
        {
            navigationStatus = EnemyNavigationStatus.Idle;
            return;
        }

        if (Time.time < nextAllowedRepathAt)
        {
            if (!HasActivePath)
            {
                motor.Stop();
                navigationStatus =
                    EnemyNavigationStatus.WaitingForRepathWindow;
            }

            return;
        }

        bool useWaypointAnchor =
            preferActiveWaypointAnchor && HasActivePath;

        Vector2 requestStartPosition = useWaypointAnchor
            ? currentPath[pathIndex]
            : body.position;

        EnemyPathFailureReason rejectionReason;
        string rejectionDetails;
        int requestId;

        bool accepted = pathService.TryRequestPath(
            this,
            requestStartPosition,
            desiredDestination,
            activeMaximumPathCostInCells,
            HandlePathResult,
            out requestId,
            out rejectionReason,
            out rejectionDetails);

        nextAllowedRepathAt =
            Time.time + minimumRepathInterval;

        if (!accepted)
        {
            RegisterFailure(
                rejectionReason,
                rejectionDetails);

            return;
        }

        pendingRequestId = requestId;
        pendingRequestUsesWaypointAnchor = useWaypointAnchor;
        pendingStartCell =
            pathService.WorldToCell(requestStartPosition);
        pendingGoalCell = knownTargetCell;
        pathRequestPending = true;
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;
        submittedRequestCount++;

        if (useWaypointAnchor)
        {
            prefetchedRequestCount++;
            navigationStatus = EnemyNavigationStatus.FollowingPath;
        }
        else
        {
            navigationStatus = EnemyNavigationStatus.WaitingForPath;
            motor.Stop();
        }
    }

    private void HandlePathResult(EnemyPathResult result)
    {
        if (!initialized || result == null ||
            result.RequestId != pendingRequestId)
        {
            return;
        }

        pathRequestPending = false;
        pendingRequestId = 0;
        lastPathCost = result.PathCost;
        lastExpandedNodeCount = result.ExpandedNodeCount;

        if (hasKnownTargetCell &&
            pendingGoalCell != knownTargetCell)
        {
            hasQueuedRepath = true;
            waitingAtWaypointForRepath = false;
            pendingRequestUsesWaypointAnchor = false;

            navigationStatus = HasActivePath
                ? EnemyNavigationStatus.FollowingPath
                : EnemyNavigationStatus.WaitingForRepathWindow;

            return;
        }

        if (!result.Success)
        {
            failedPathCount++;

            if (pendingRequestUsesWaypointAnchor && HasActivePath)
            {
                pendingRequestUsesWaypointAnchor = false;
                hasQueuedRepath = hasDesiredDestination;
                lastFailureReason = result.FailureReason;
                lastFailureDetails = result.Details;
                retryFailedRequestAt =
                    Time.time + failedRequestRetryDelay;
                navigationStatus =
                    EnemyNavigationStatus.FollowingPath;

                return;
            }

            RegisterFailure(
                result.FailureReason,
                result.Details);

            return;
        }

        if (result.StartCellAdjusted)
        {
            Vector2 resolvedStartPosition =
                pathService.CellToWorld(
                    result.ResolvedStartCell);

            float recoveryDistance = Vector2.Distance(
                body.position,
                resolvedStartPosition);

            if (recoveryDistance >
                maximumRecoverySnapDistance)
            {
                RegisterFailure(
                    EnemyPathFailureReason.RecoveryDistanceExceeded,
                    "Adjusted start cell is " + recoveryDistance +
                    " world units away; recovery snap was rejected.");

                return;
            }

            motor.SnapToRecoveryCell(resolvedStartPosition);
            totalRecoveryCount++;
        }

        if (pendingRequestUsesWaypointAnchor &&
            !IsAtCellCenter(result.ResolvedStartCell))
        {
            BufferReplacementPath(result);
            pendingRequestUsesWaypointAnchor = false;
            hasQueuedRepath = false;
            waitingAtWaypointForRepath = false;
            navigationStatus = EnemyNavigationStatus.FollowingPath;
            return;
        }

        pendingRequestUsesWaypointAnchor = false;
        ApplyAcceptedPath(result);
    }

    private void ApplyAcceptedPath(EnemyPathResult result)
    {
        DiscardBufferedReplacementPath();

        currentPath.Clear();

        for (int i = 0; i < result.WorldPath.Count; i++)
        {
            currentPath.Add(result.WorldPath[i]);
        }

        pathIndex = 0;
        acceptedPathCount++;
        lastFailureReason = EnemyPathFailureReason.None;
        lastFailureDetails = string.Empty;
        retryFailedRequestAt = 0f;
        isStuck = false;
        waitingAtWaypointForRepath = false;
        navigationStatus = HasActivePath
            ? EnemyNavigationStatus.FollowingPath
            : EnemyNavigationStatus.DirectMovement;

        RefreshPathSnapshot();
    }

    private void BufferReplacementPath(EnemyPathResult result)
    {
        bufferedReplacementPath.Clear();

        for (int i = 0; i < result.WorldPath.Count; i++)
        {
            bufferedReplacementPath.Add(result.WorldPath[i]);
        }

        bufferedReplacementStartCell = result.ResolvedStartCell;
        bufferedReplacementPathCount =
            bufferedReplacementPath.Count;
        hasBufferedReplacementPath = true;
        acceptedPathCount++;
        lastFailureReason = EnemyPathFailureReason.None;
        lastFailureDetails = string.Empty;
        retryFailedRequestAt = 0f;
        isStuck = false;
    }

    private void ApplyBufferedReplacementPath()
    {
        currentPath.Clear();
        currentPath.AddRange(bufferedReplacementPath);
        pathIndex = 0;
        hasBufferedReplacementPath = false;
        bufferedReplacementPath.Clear();
        bufferedReplacementPathCount = 0;
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;
        seamlessPathSwapCount++;
        navigationStatus = HasActivePath
            ? EnemyNavigationStatus.FollowingPath
            : EnemyNavigationStatus.DirectMovement;
        RefreshPathSnapshot();
    }

    private void DiscardBufferedReplacementPath()
    {
        hasBufferedReplacementPath = false;
        bufferedReplacementPath.Clear();
        bufferedReplacementPathCount = 0;
    }

    private bool IsAtCellCenter(Vector2Int cell)
    {
        return Vector2.Distance(
                   body.position,
                   pathService.CellToWorld(cell)) <=
               waypointTolerance;
    }

    private void FollowCurrentWaypoint()
    {
        Vector2 waypoint = currentPath[pathIndex];
        navigationStatus = EnemyNavigationStatus.FollowingPath;
        movementExpectedLastTick = true;

        bool reachedWaypoint = motor.MoveTowards(
            waypoint,
            waypointTolerance);

        if (!reachedWaypoint)
        {
            RefreshPathSnapshot();
            return;
        }

        Vector2Int reachedCell =
            pathService.WorldToCell(waypoint);

        pathIndex++;

        if (hasBufferedReplacementPath)
        {
            if (bufferedReplacementStartCell == reachedCell)
            {
                ApplyBufferedReplacementPath();
                return;
            }

            DiscardBufferedReplacementPath();
            hasQueuedRepath = hasDesiredDestination;
        }

        // The prefetched request normally completes before this anchor is
        // reached. If the shared service is under load, wait at the anchor
        // instead of advancing along the old path. Otherwise the returned
        // path would begin behind the agent and cause a snap/back-track.
        if (pathRequestPending &&
            pendingRequestUsesWaypointAnchor &&
            pendingStartCell == reachedCell)
        {
            waitingAtWaypointForRepath = true;
            repathWaitCount++;
            motor.Stop();
            RefreshPathSnapshot();
            return;
        }

        if (hasQueuedRepath)
        {
            if (!pathRequestPending && HasActivePath)
            {
                TrySubmitPathRequest(
                    preferActiveWaypointAnchor: true);
                RefreshPathSnapshot();
                return;
            }

            if (pathRequestPending &&
                pendingStartCell != reachedCell &&
                HasActivePath)
            {
                RefreshPathSnapshot();
                return;
            }

            waitingAtWaypointForRepath = true;
            repathWaitCount++;
            motor.Stop();
            RefreshPathSnapshot();
            return;
        }

        if (!HasActivePath)
        {
            waitingAtWaypointForRepath = false;
            RefreshPathSnapshot();

            if (ShouldMoveDirectlyWithinGoalCell())
            {
                MoveDirectlyWithinGoalCell();
            }

            return;
        }

        RefreshPathSnapshot();
    }

    /// <summary>
    /// Returns true when recovery consumed the current fixed tick.
    /// </summary>
    private bool EvaluateMovementProgressAndRecover()
    {
        if (!movementExpectedLastTick)
        {
            progressReferencePosition = body.position;
            noProgressDuration = 0f;
            isStuck = false;
            return false;
        }

        float movedDistance = Vector2.Distance(
            body.position,
            progressReferencePosition);

        movementExpectedLastTick = false;

        if (movedDistance >= stuckMovementThreshold)
        {
            progressReferencePosition = body.position;
            noProgressDuration = 0f;
            isStuck = false;
            recoveryAttemptCount = 0;
            lastFailureReason = EnemyPathFailureReason.None;
            lastFailureDetails = string.Empty;
            return false;
        }

        noProgressDuration += Time.fixedDeltaTime;

        if (noProgressDuration < stuckTimeout)
        {
            return false;
        }

        BeginStuckRecovery();
        return true;
    }

    private void BeginStuckRecovery()
    {
        isStuck = true;
        recoveryAttemptCount++;
        totalRecoveryCount++;
        lastFailureReason = EnemyPathFailureReason.StuckDetected;
        lastFailureDetails =
            "No movement above " + stuckMovementThreshold +
            " for " + noProgressDuration + " seconds.";

        CancelPendingRequest();
        currentPath.Clear();
        DiscardBufferedReplacementPath();
        pathIndex = 0;
        waitingAtWaypointForRepath = false;
        hasQueuedRepath = hasDesiredDestination;
        motor.Stop();

        if (recoveryAttemptCount > maximumRecoveryAttempts)
        {
            RegisterFailure(
                EnemyPathFailureReason.RecoveryAttemptsExhausted,
                "Stuck recovery exceeded " +
                maximumRecoveryAttempts + " consecutive attempts.");

            return;
        }

        if (!pathService.TryFindNearestWalkableCell(
                body.position,
                1,
                out Vector2Int recoveryCell))
        {
            RegisterFailure(
                EnemyPathFailureReason.RecoveryCellUnavailable,
                "No walkable recovery cell exists within one grid cell.");

            return;
        }

        Vector2 recoveryPosition =
            pathService.CellToWorld(recoveryCell);

        float recoveryDistance = Vector2.Distance(
            body.position,
            recoveryPosition);

        if (recoveryDistance > maximumRecoverySnapDistance)
        {
            RegisterFailure(
                EnemyPathFailureReason.RecoveryDistanceExceeded,
                "Nearest recovery cell is " + recoveryDistance +
                " world units away; maximum is " +
                maximumRecoverySnapDistance + ".");

            return;
        }

        if (recoveryDistance > waypointTolerance)
        {
            motor.SnapToRecoveryCell(recoveryPosition);
        }

        progressReferencePosition = body.position;
        noProgressDuration = 0f;
        isStuck = false;
        nextAllowedRepathAt =
            Time.time + minimumRepathInterval;

        navigationStatus = EnemyNavigationStatus.Recovering;
        RefreshPathSnapshot();
    }

    private void ClearFailureSnapshot()
    {
        lastFailureReason = EnemyPathFailureReason.None;
        lastFailureDetails = string.Empty;
        retryFailedRequestAt = 0f;

        if (navigationStatus == EnemyNavigationStatus.Failed)
        {
            navigationStatus = EnemyNavigationStatus.Idle;
        }
    }

    private void RegisterFailure(
        EnemyPathFailureReason reason,
        string details)
    {
        currentPath.Clear();
        DiscardBufferedReplacementPath();
        pathIndex = 0;
        waitingAtWaypointForRepath = false;
        hasQueuedRepath = hasDesiredDestination;
        lastFailureReason = reason;
        lastFailureDetails = details ?? string.Empty;
        retryFailedRequestAt =
            Time.time + failedRequestRetryDelay;

        navigationStatus = EnemyNavigationStatus.Failed;
        motor.Stop();
        ResetProgressTracking();
        RefreshPathSnapshot();
    }

    private void CancelPendingRequest()
    {
        if (pathRequestPending && pathService != null)
        {
            pathService.CancelRequest(pendingRequestId);
        }

        pathRequestPending = false;
        pendingRequestId = 0;
        pendingRequestUsesWaypointAnchor = false;
        pendingStartCell = default(Vector2Int);
    }

    private void ResetNavigationState()
    {
        currentPath.Clear();
        DiscardBufferedReplacementPath();
        pathIndex = 0;
        navigationIntent = EnemyNavigationIntent.None;
        destinationReached = false;
        activeMaximumPathCostInCells = maximumPathCostInCells;
        externalMovementHoldActive = false;
        hasDesiredDestination = false;
        desiredDestination = Vector2.zero;
        hasKnownTargetCell = false;
        knownTargetCell = default(Vector2Int);
        hasQueuedRepath = false;
        waitingAtWaypointForRepath = false;
        pathRequestPending = false;
        pendingRequestId = 0;
        pendingRequestUsesWaypointAnchor = false;
        pendingStartCell = default(Vector2Int);
        nextAllowedRepathAt = 0f;
        retryFailedRequestAt = 0f;
        lastPathCost = 0;
        lastExpandedNodeCount = 0;
        lastFailureReason = EnemyPathFailureReason.None;
        lastFailureDetails = string.Empty;
        noProgressDuration = 0f;
        isStuck = false;
        recoveryAttemptCount = 0;
        totalRecoveryCount = 0;
        submittedRequestCount = 0;
        acceptedPathCount = 0;
        failedPathCount = 0;
        prefetchedRequestCount = 0;
        seamlessPathSwapCount = 0;
        repathWaitCount = 0;
        progressReferencePosition = body != null
            ? body.position
            : (Vector2)transform.position;

        movementExpectedLastTick = false;

        if (context != null)
        {
            context.ClearLastKnownTargetPosition();
            context.ClearNavigationDestination();
        }

        RefreshPathSnapshot();
    }

    private void ResetProgressTracking()
    {
        movementExpectedLastTick = false;
        noProgressDuration = 0f;
        isStuck = false;
        progressReferencePosition = body != null
            ? body.position
            : (Vector2)transform.position;
    }

    private void RefreshPathSnapshot()
    {
        currentPathCount = currentPath.Count;
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

        if (motor == null)
        {
            motor = GetComponent<EnemyMotor2D>();
        }

        if (context == null)
        {
            context = GetComponent<EnemyRuntimeContext>();
        }

        if (detection == null)
        {
            detection = GetComponent<EnemyDetection>();
        }
    }

    private void OnDisable()
    {
        CancelPendingRequest();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showPathGizmos || currentPath.Count == 0)
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
