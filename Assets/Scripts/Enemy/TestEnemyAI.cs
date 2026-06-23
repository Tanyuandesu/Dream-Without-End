using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 測試敵人的追擊與平滑重新尋路。
/// 參數集中由 EnemySpawner 控制。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPathfinder))]
[RequireComponent(typeof(EnemyDetection))]
public sealed class TestEnemyAI : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float waypointTolerance = 0.035f;
    [SerializeField] private float stopDistance = 0.72f;
    [SerializeField] private float lastPositionTolerance = 0.15f;

    private Rigidbody2D body;
    private Transform target;
    private EnemyPathfinder pathfinder;
    private EnemyDetection detection;

    private List<Vector2> currentPath =
        new List<Vector2>();

    private int pathIndex;

    private bool hasLastKnownPosition;
    private Vector2 lastKnownPosition;

    private bool hasKnownTargetCell;
    private Vector2Int knownTargetCell;

    private bool hasQueuedRepath;
    private Vector2 queuedDestination;

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

        ApplySettings(
            newMoveSpeed,
            newWaypointTolerance,
            newStopDistance,
            newLastPositionTolerance);
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

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        pathfinder = GetComponent<EnemyPathfinder>();
        detection = GetComponent<EnemyDetection>();
    }

    private void Update()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        if (!detection.IsTargetDetected)
        {
            return;
        }

        lastKnownPosition =
            detection.LastKnownTargetPosition;

        hasLastKnownPosition = true;

        Vector2Int targetCell =
            pathfinder.WorldToCell(lastKnownPosition);

        if (hasKnownTargetCell &&
            targetCell == knownTargetCell)
        {
            return;
        }

        knownTargetCell = targetCell;
        hasKnownTargetCell = true;

        RequestRepath(lastKnownPosition);
    }

    private void FixedUpdate()
    {
        if (!HasRequiredReferences())
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

        if (!HasActivePath() && hasQueuedRepath)
        {
            ApplyQueuedRepath(body.position);
        }

        if (HasActivePath())
        {
            FollowCurrentWaypoint();
            return;
        }

        HandleMovementWithoutPath();
    }

    private bool HasRequiredReferences()
    {
        return body != null &&
               target != null &&
               pathfinder != null &&
               detection != null;
    }

    private bool HasActivePath()
    {
        return currentPath != null &&
               pathIndex < currentPath.Count;
    }

    private void RequestRepath(Vector2 destination)
    {
        queuedDestination = destination;
        hasQueuedRepath = true;

        if (!HasActivePath())
        {
            ApplyQueuedRepath(body.position);
        }
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

        pathIndex = 0;
        hasQueuedRepath = false;
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
            return;
        }

        pathIndex++;

        if (hasQueuedRepath)
        {
            ApplyQueuedRepath(waypoint);
        }
    }

    private void HandleMovementWithoutPath()
    {
        if (!hasLastKnownPosition)
        {
            return;
        }

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
        hasLastKnownPosition = false;
        hasKnownTargetCell = false;
        hasQueuedRepath = false;

        currentPath.Clear();
        pathIndex = 0;
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
