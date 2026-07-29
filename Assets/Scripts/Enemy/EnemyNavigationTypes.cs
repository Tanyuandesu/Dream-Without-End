using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grid topology used by the shared enemy path service.
/// Character animation direction remains a separate presentation concern.
/// </summary>
public enum EnemyNavigationTopology
{
    FourDirections = 0,
    EightDirectionsNoCornerCutting = 1
}

public enum EnemyNavigationIntent
{
    None = 0,
    TrackingTarget = 1,
    FixedDestination = 2
}

public enum EnemyNavigationStatus
{
    Uninitialized = 0,
    Idle = 1,
    WaitingForRepathWindow = 2,
    WaitingForPath = 3,
    FollowingPath = 4,
    DirectMovement = 5,
    Recovering = 6,
    Failed = 7,
    Stopped = 8
}

public enum EnemyPathFailureReason
{
    None = 0,
    ServiceNotInitialized = 1,
    StartCellNotWalkable = 2,
    GoalCellNotWalkable = 3,
    Unreachable = 4,
    NodeLimitExceeded = 5,
    PathCostLimitExceeded = 6,
    RequestRejected = 7,
    RequestCancelled = 8,
    OwnerUnavailable = 9,
    StuckDetected = 10,
    RecoveryCellUnavailable = 11,
    RecoveryDistanceExceeded = 12,
    RecoveryAttemptsExhausted = 13
}

/// <summary>
/// Immutable result returned by EnemyPathService.
/// A successful same-cell query intentionally contains zero waypoints.
/// </summary>
public sealed class EnemyPathResult
{
    private readonly List<Vector2Int> cellPath;
    private readonly List<Vector2> worldPath;

    public int RequestId { get; }
    public bool Success { get; }
    public EnemyPathFailureReason FailureReason { get; }
    public string Details { get; }
    public Vector2Int RequestedStartCell { get; }
    public Vector2Int ResolvedStartCell { get; }
    public Vector2Int GoalCell { get; }
    public bool StartCellAdjusted { get; }
    public int PathCost { get; }
    public int ExpandedNodeCount { get; }
    public IReadOnlyList<Vector2Int> CellPath => cellPath;
    public IReadOnlyList<Vector2> WorldPath => worldPath;

    private EnemyPathResult(
        int requestId,
        bool success,
        EnemyPathFailureReason failureReason,
        string details,
        Vector2Int requestedStartCell,
        Vector2Int resolvedStartCell,
        Vector2Int goalCell,
        bool startCellAdjusted,
        int pathCost,
        int expandedNodeCount,
        List<Vector2Int> newCellPath,
        List<Vector2> newWorldPath)
    {
        RequestId = requestId;
        Success = success;
        FailureReason = failureReason;
        Details = details ?? string.Empty;
        RequestedStartCell = requestedStartCell;
        ResolvedStartCell = resolvedStartCell;
        GoalCell = goalCell;
        StartCellAdjusted = startCellAdjusted;
        PathCost = pathCost;
        ExpandedNodeCount = expandedNodeCount;
        cellPath = newCellPath ?? new List<Vector2Int>();
        worldPath = newWorldPath ?? new List<Vector2>();
    }

    public static EnemyPathResult CreateSuccess(
        int requestId,
        Vector2Int requestedStartCell,
        Vector2Int resolvedStartCell,
        Vector2Int goalCell,
        bool startCellAdjusted,
        int pathCost,
        int expandedNodeCount,
        List<Vector2Int> newCellPath,
        List<Vector2> newWorldPath)
    {
        return new EnemyPathResult(
            requestId,
            true,
            EnemyPathFailureReason.None,
            string.Empty,
            requestedStartCell,
            resolvedStartCell,
            goalCell,
            startCellAdjusted,
            pathCost,
            expandedNodeCount,
            newCellPath,
            newWorldPath);
    }

    public static EnemyPathResult CreateFailure(
        int requestId,
        EnemyPathFailureReason reason,
        string details,
        Vector2Int requestedStartCell,
        Vector2Int resolvedStartCell,
        Vector2Int goalCell,
        bool startCellAdjusted,
        int expandedNodeCount = 0)
    {
        return new EnemyPathResult(
            requestId,
            false,
            reason,
            details,
            requestedStartCell,
            resolvedStartCell,
            goalCell,
            startCellAdjusted,
            0,
            expandedNodeCount,
            new List<Vector2Int>(),
            new List<Vector2>());
    }
}
