using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One shared path-query authority for a generated floor.
///
/// Requests are queued and processed with a per-frame budget. A* uses a
/// binary min-heap, deterministic neighbour order and a precomputed
/// connectivity map so disconnected goals fail before a full search.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPathService : MonoBehaviour
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private static readonly Vector2Int[] DiagonalDirections =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1)
    };

    [Header("EA3 floor navigation authority")]
    [SerializeField] private EnemyNavigationTopology topology =
        EnemyNavigationTopology.FourDirections;

    [Range(1, 32)]
    [SerializeField] private int maxRequestsPerFrame = 2;

    [Min(64)]
    [SerializeField] private int maxExpandedNodesPerQuery = 4096;

    [Range(0, 4)]
    [SerializeField] private int startRecoveryRadiusInCells = 1;

    [Tooltip(
        "Disabled for the EA3 baseline. Full cell-center paths are easier " +
        "to verify in corners and non-rectangular rooms.")]
    [SerializeField] private bool simplifyCollinearWaypoints;

    [Header("Runtime diagnostics (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private int walkableCellCount;
    [SerializeField] private int connectedComponentCount;
    [SerializeField] private int queuedRequestCount;
    [SerializeField] private int peakQueuedRequestCount;
    [SerializeField] private int processedThisFrame;
    [SerializeField] private int totalProcessedRequests;
    [SerializeField] private int totalSuccessfulRequests;
    [SerializeField] private int totalFailedRequests;
    [SerializeField] private int totalCancelledRequests;
    [SerializeField] private EnemyPathFailureReason lastFailureReason;
    [SerializeField] private string lastFailureDetails = string.Empty;

    private readonly HashSet<Vector2Int> floorCells =
        new HashSet<Vector2Int>();

    private readonly Dictionary<Vector2Int, int> componentByCell =
        new Dictionary<Vector2Int, int>();

    private readonly Queue<PendingPathRequest> pendingRequests =
        new Queue<PendingPathRequest>();

    private readonly HashSet<int> cancelledRequestIds =
        new HashSet<int>();

    private float cellSize = 1f;
    private int nextRequestId = 1;

    public bool IsInitialized => initialized;
    public EnemyNavigationTopology Topology => topology;
    public int MaxRequestsPerFrame => maxRequestsPerFrame;
    public int WalkableCellCount => walkableCellCount;
    public int ConnectedComponentCount => connectedComponentCount;
    public int QueuedRequestCount => queuedRequestCount;
    public int PeakQueuedRequestCount => peakQueuedRequestCount;
    public int ProcessedThisFrame => processedThisFrame;
    public int TotalProcessedRequests => totalProcessedRequests;
    public int TotalSuccessfulRequests => totalSuccessfulRequests;
    public int TotalFailedRequests => totalFailedRequests;
    public int TotalCancelledRequests => totalCancelledRequests;
    public EnemyPathFailureReason LastFailureReason => lastFailureReason;
    public string LastFailureDetails => lastFailureDetails;
    public float CellSize => cellSize;
    public bool SimplifiesCollinearWaypoints => simplifyCollinearWaypoints;

    public void Initialize(
        DungeonLayout layout,
        float dungeonCellSize,
        EnemyNavigationTopology newTopology,
        int newMaxRequestsPerFrame,
        int newMaxExpandedNodesPerQuery,
        int newStartRecoveryRadiusInCells,
        bool newSimplifyCollinearWaypoints)
    {
        floorCells.Clear();
        componentByCell.Clear();
        pendingRequests.Clear();
        cancelledRequestIds.Clear();

        topology = newTopology;
        cellSize = Mathf.Max(0.01f, dungeonCellSize);
        maxRequestsPerFrame = Mathf.Clamp(
            newMaxRequestsPerFrame,
            1,
            32);

        maxExpandedNodesPerQuery = Mathf.Max(
            64,
            newMaxExpandedNodesPerQuery);

        startRecoveryRadiusInCells = Mathf.Clamp(
            newStartRecoveryRadiusInCells,
            0,
            4);

        simplifyCollinearWaypoints =
            newSimplifyCollinearWaypoints;

        if (layout != null && layout.FloorCells != null)
        {
            floorCells.UnionWith(layout.FloorCells);
        }

        walkableCellCount = floorCells.Count;
        connectedComponentCount = BuildConnectedComponents();
        queuedRequestCount = 0;
        peakQueuedRequestCount = 0;
        processedThisFrame = 0;
        totalProcessedRequests = 0;
        totalSuccessfulRequests = 0;
        totalFailedRequests = 0;
        totalCancelledRequests = 0;
        lastFailureReason = EnemyPathFailureReason.None;
        lastFailureDetails = string.Empty;
        nextRequestId = 1;
        initialized = walkableCellCount > 0;
    }

    public bool TryRequestPath(
        MonoBehaviour owner,
        Vector2 startWorld,
        Vector2 targetWorld,
        int maximumPathCostInCells,
        Action<EnemyPathResult> callback,
        out int requestId,
        out EnemyPathFailureReason rejectionReason,
        out string rejectionDetails)
    {
        requestId = 0;
        rejectionReason = EnemyPathFailureReason.None;
        rejectionDetails = string.Empty;

        if (!initialized)
        {
            rejectionReason =
                EnemyPathFailureReason.ServiceNotInitialized;

            rejectionDetails =
                "The floor path service has no initialized FloorCells.";

            return false;
        }

        if (owner == null || callback == null)
        {
            rejectionReason =
                EnemyPathFailureReason.RequestRejected;

            rejectionDetails =
                "A path request requires a live owner and callback.";

            return false;
        }

        requestId = nextRequestId++;

        pendingRequests.Enqueue(
            new PendingPathRequest(
                requestId,
                owner,
                startWorld,
                targetWorld,
                Mathf.Max(0, maximumPathCostInCells),
                callback));

        RefreshQueueSnapshot();
        return true;
    }

    public void CancelRequest(int requestId)
    {
        if (requestId <= 0)
        {
            return;
        }

        cancelledRequestIds.Add(requestId);
    }

    /// <summary>
    /// Compatibility and audit entry. Runtime agents use the queued API.
    /// </summary>
    public EnemyPathResult FindPathImmediate(
        Vector2 startWorld,
        Vector2 targetWorld,
        int maximumPathCostInCells = 0)
    {
        return FindPathInternal(
            0,
            startWorld,
            targetWorld,
            Mathf.Max(0, maximumPathCostInCells));
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / cellSize),
            Mathf.RoundToInt(worldPosition.y / cellSize));
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(
            cell.x * cellSize,
            cell.y * cellSize);
    }

    public bool AreInSameCell(
        Vector2 firstWorld,
        Vector2 secondWorld)
    {
        return WorldToCell(firstWorld) ==
               WorldToCell(secondWorld);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return floorCells.Contains(cell);
    }

    public bool AreCellsReachable(
        Vector2Int start,
        Vector2Int goal)
    {
        return componentByCell.TryGetValue(
                   start,
                   out int startComponent) &&
               componentByCell.TryGetValue(
                   goal,
                   out int goalComponent) &&
               startComponent == goalComponent;
    }

    public bool TryFindNearestWalkableCell(
        Vector2 worldPosition,
        int maximumRadiusInCells,
        out Vector2Int resolvedCell)
    {
        return TryResolveNearestWalkableCell(
            WorldToCell(worldPosition),
            Mathf.Max(0, maximumRadiusInCells),
            out resolvedCell);
    }

    private void Update()
    {
        processedThisFrame = 0;

        while (pendingRequests.Count > 0 &&
               processedThisFrame < maxRequestsPerFrame)
        {
            PendingPathRequest request =
                pendingRequests.Dequeue();

            if (cancelledRequestIds.Remove(request.RequestId))
            {
                totalCancelledRequests++;
                continue;
            }

            if (request.Owner == null)
            {
                totalCancelledRequests++;
                continue;
            }

            EnemyPathResult result = FindPathInternal(
                request.RequestId,
                request.StartWorld,
                request.TargetWorld,
                request.MaximumPathCostInCells);

            processedThisFrame++;
            totalProcessedRequests++;

            if (result.Success)
            {
                totalSuccessfulRequests++;
            }
            else
            {
                totalFailedRequests++;
                lastFailureReason = result.FailureReason;
                lastFailureDetails = result.Details;
            }

            try
            {
                request.Callback(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, request.Owner);
            }
        }

        RefreshQueueSnapshot();
    }

    private EnemyPathResult FindPathInternal(
        int requestId,
        Vector2 startWorld,
        Vector2 targetWorld,
        int maximumPathCostInCells)
    {
        Vector2Int requestedStartCell =
            WorldToCell(startWorld);

        Vector2Int goalCell = WorldToCell(targetWorld);
        Vector2Int resolvedStartCell = requestedStartCell;

        if (!initialized)
        {
            return EnemyPathResult.CreateFailure(
                requestId,
                EnemyPathFailureReason.ServiceNotInitialized,
                "The path service is not initialized.",
                requestedStartCell,
                resolvedStartCell,
                goalCell,
                false);
        }

        bool startCellAdjusted = false;

        if (!floorCells.Contains(resolvedStartCell))
        {
            if (!TryResolveNearestWalkableCell(
                    requestedStartCell,
                    startRecoveryRadiusInCells,
                    out resolvedStartCell))
            {
                return EnemyPathResult.CreateFailure(
                    requestId,
                    EnemyPathFailureReason.StartCellNotWalkable,
                    "Start cell " + requestedStartCell +
                    " is outside FloorCells and no nearby recovery cell exists.",
                    requestedStartCell,
                    requestedStartCell,
                    goalCell,
                    false);
            }

            startCellAdjusted = true;
        }

        if (!floorCells.Contains(goalCell))
        {
            return EnemyPathResult.CreateFailure(
                requestId,
                EnemyPathFailureReason.GoalCellNotWalkable,
                "Goal cell " + goalCell +
                " is not a FloorCell.",
                requestedStartCell,
                resolvedStartCell,
                goalCell,
                startCellAdjusted);
        }

        if (!AreCellsReachable(resolvedStartCell, goalCell))
        {
            return EnemyPathResult.CreateFailure(
                requestId,
                EnemyPathFailureReason.Unreachable,
                "Connectivity precheck rejected " +
                resolvedStartCell + " -> " + goalCell + ".",
                requestedStartCell,
                resolvedStartCell,
                goalCell,
                startCellAdjusted);
        }

        if (resolvedStartCell == goalCell)
        {
            return EnemyPathResult.CreateSuccess(
                requestId,
                requestedStartCell,
                resolvedStartCell,
                goalCell,
                startCellAdjusted,
                0,
                0,
                new List<Vector2Int>(),
                new List<Vector2>());
        }

        BinaryMinHeap openSet = new BinaryMinHeap();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();

        Dictionary<Vector2Int, int> gScore =
            new Dictionary<Vector2Int, int>
            {
                [resolvedStartCell] = 0
            };

        HashSet<Vector2Int> closedSet =
            new HashSet<Vector2Int>();

        int sequence = 0;
        int startHeuristic =
            CalculateHeuristic(resolvedStartCell, goalCell);

        openSet.Enqueue(
            new OpenNode(
                resolvedStartCell,
                0,
                startHeuristic,
                startHeuristic,
                sequence++));

        int expandedNodes = 0;
        bool prunedByPathCost = false;
        int maximumScaledPathCost =
            maximumPathCostInCells > 0
                ? maximumPathCostInCells * 10
                : 0;

        while (openSet.Count > 0)
        {
            OpenNode currentNode = openSet.Dequeue();
            Vector2Int current = currentNode.Cell;

            if (closedSet.Contains(current))
            {
                continue;
            }

            if (!gScore.TryGetValue(
                    current,
                    out int bestKnownG) ||
                currentNode.GScore != bestKnownG)
            {
                continue;
            }

            if (current == goalCell)
            {
                List<Vector2Int> cellPath =
                    ReconstructCellPath(
                        cameFrom,
                        current,
                        resolvedStartCell);

                if (simplifyCollinearWaypoints)
                {
                    cellPath = SimplifyCollinearPath(
                        cellPath,
                        resolvedStartCell);
                }

                return EnemyPathResult.CreateSuccess(
                    requestId,
                    requestedStartCell,
                    resolvedStartCell,
                    goalCell,
                    startCellAdjusted,
                    bestKnownG,
                    expandedNodes,
                    cellPath,
                    ConvertToWorldPath(cellPath));
            }

            closedSet.Add(current);
            expandedNodes++;

            if (expandedNodes > maxExpandedNodesPerQuery)
            {
                return EnemyPathResult.CreateFailure(
                    requestId,
                    EnemyPathFailureReason.NodeLimitExceeded,
                    "A* expanded more than " +
                    maxExpandedNodesPerQuery + " cells.",
                    requestedStartCell,
                    resolvedStartCell,
                    goalCell,
                    startCellAdjusted,
                    expandedNodes);
            }

            ExpandCardinalNeighbours(
                current,
                goalCell,
                bestKnownG,
                maximumScaledPathCost,
                cameFrom,
                gScore,
                closedSet,
                openSet,
                ref sequence,
                ref prunedByPathCost);

            if (topology ==
                EnemyNavigationTopology.EightDirectionsNoCornerCutting)
            {
                ExpandDiagonalNeighbours(
                    current,
                    goalCell,
                    bestKnownG,
                    maximumScaledPathCost,
                    cameFrom,
                    gScore,
                    closedSet,
                    openSet,
                    ref sequence,
                    ref prunedByPathCost);
            }
        }

        EnemyPathFailureReason failureReason =
            prunedByPathCost
                ? EnemyPathFailureReason.PathCostLimitExceeded
                : EnemyPathFailureReason.Unreachable;

        string details = prunedByPathCost
            ? "No path within the configured cost limit of " +
              maximumPathCostInCells + " cells."
            : "A* exhausted the reachable search space.";

        return EnemyPathResult.CreateFailure(
            requestId,
            failureReason,
            details,
            requestedStartCell,
            resolvedStartCell,
            goalCell,
            startCellAdjusted,
            expandedNodes);
    }

    private void ExpandCardinalNeighbours(
        Vector2Int current,
        Vector2Int goal,
        int currentG,
        int maximumScaledPathCost,
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Dictionary<Vector2Int, int> gScore,
        HashSet<Vector2Int> closedSet,
        BinaryMinHeap openSet,
        ref int sequence,
        ref bool prunedByPathCost)
    {
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            TryAddNeighbour(
                current,
                current + CardinalDirections[i],
                goal,
                10,
                currentG,
                maximumScaledPathCost,
                cameFrom,
                gScore,
                closedSet,
                openSet,
                ref sequence,
                ref prunedByPathCost);
        }
    }

    private void ExpandDiagonalNeighbours(
        Vector2Int current,
        Vector2Int goal,
        int currentG,
        int maximumScaledPathCost,
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Dictionary<Vector2Int, int> gScore,
        HashSet<Vector2Int> closedSet,
        BinaryMinHeap openSet,
        ref int sequence,
        ref bool prunedByPathCost)
    {
        for (int i = 0; i < DiagonalDirections.Length; i++)
        {
            Vector2Int direction = DiagonalDirections[i];
            Vector2Int neighbour = current + direction;

            if (!CanTraverseDiagonal(current, direction))
            {
                continue;
            }

            TryAddNeighbour(
                current,
                neighbour,
                goal,
                14,
                currentG,
                maximumScaledPathCost,
                cameFrom,
                gScore,
                closedSet,
                openSet,
                ref sequence,
                ref prunedByPathCost);
        }
    }

    private void TryAddNeighbour(
        Vector2Int current,
        Vector2Int neighbour,
        Vector2Int goal,
        int stepCost,
        int currentG,
        int maximumScaledPathCost,
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Dictionary<Vector2Int, int> gScore,
        HashSet<Vector2Int> closedSet,
        BinaryMinHeap openSet,
        ref int sequence,
        ref bool prunedByPathCost)
    {
        if (!floorCells.Contains(neighbour) ||
            closedSet.Contains(neighbour))
        {
            return;
        }

        int tentativeG = currentG + stepCost;

        if (maximumScaledPathCost > 0 &&
            tentativeG > maximumScaledPathCost)
        {
            prunedByPathCost = true;
            return;
        }

        if (gScore.TryGetValue(
                neighbour,
                out int previousG) &&
            tentativeG >= previousG)
        {
            return;
        }

        cameFrom[neighbour] = current;
        gScore[neighbour] = tentativeG;

        int heuristic = CalculateHeuristic(neighbour, goal);

        openSet.Enqueue(
            new OpenNode(
                neighbour,
                tentativeG,
                tentativeG + heuristic,
                heuristic,
                sequence++));
    }

    private int BuildConnectedComponents()
    {
        componentByCell.Clear();

        if (floorCells.Count == 0)
        {
            return 0;
        }

        int componentId = 0;
        Queue<Vector2Int> frontier =
            new Queue<Vector2Int>();

        foreach (Vector2Int seed in floorCells)
        {
            if (componentByCell.ContainsKey(seed))
            {
                continue;
            }

            componentByCell[seed] = componentId;
            frontier.Enqueue(seed);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                EnqueueConnectedCardinalNeighbours(
                    current,
                    componentId,
                    frontier);

                if (topology ==
                    EnemyNavigationTopology.EightDirectionsNoCornerCutting)
                {
                    EnqueueConnectedDiagonalNeighbours(
                        current,
                        componentId,
                        frontier);
                }
            }

            componentId++;
        }

        return componentId;
    }

    private void EnqueueConnectedCardinalNeighbours(
        Vector2Int current,
        int componentId,
        Queue<Vector2Int> frontier)
    {
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            TryEnqueueConnectedCell(
                current + CardinalDirections[i],
                componentId,
                frontier);
        }
    }

    private void EnqueueConnectedDiagonalNeighbours(
        Vector2Int current,
        int componentId,
        Queue<Vector2Int> frontier)
    {
        for (int i = 0; i < DiagonalDirections.Length; i++)
        {
            Vector2Int direction = DiagonalDirections[i];

            if (!CanTraverseDiagonal(current, direction))
            {
                continue;
            }

            TryEnqueueConnectedCell(
                current + direction,
                componentId,
                frontier);
        }
    }

    private void TryEnqueueConnectedCell(
        Vector2Int cell,
        int componentId,
        Queue<Vector2Int> frontier)
    {
        if (!floorCells.Contains(cell) ||
            componentByCell.ContainsKey(cell))
        {
            return;
        }

        componentByCell[cell] = componentId;
        frontier.Enqueue(cell);
    }

    private bool CanTraverseDiagonal(
        Vector2Int current,
        Vector2Int diagonalDirection)
    {
        Vector2Int horizontal =
            current + new Vector2Int(
                diagonalDirection.x,
                0);

        Vector2Int vertical =
            current + new Vector2Int(
                0,
                diagonalDirection.y);

        return floorCells.Contains(current + diagonalDirection) &&
               floorCells.Contains(horizontal) &&
               floorCells.Contains(vertical);
    }

    private bool TryResolveNearestWalkableCell(
        Vector2Int requestedCell,
        int maximumRadiusInCells,
        out Vector2Int resolvedCell)
    {
        if (floorCells.Contains(requestedCell))
        {
            resolvedCell = requestedCell;
            return true;
        }

        for (int distance = 1;
             distance <= maximumRadiusInCells;
             distance++)
        {
            for (int xOffset = -distance;
                 xOffset <= distance;
                 xOffset++)
            {
                int yMagnitude = distance - Mathf.Abs(xOffset);

                Vector2Int firstCandidate =
                    requestedCell +
                    new Vector2Int(xOffset, yMagnitude);

                if (floorCells.Contains(firstCandidate))
                {
                    resolvedCell = firstCandidate;
                    return true;
                }

                if (yMagnitude == 0)
                {
                    continue;
                }

                Vector2Int secondCandidate =
                    requestedCell +
                    new Vector2Int(xOffset, -yMagnitude);

                if (floorCells.Contains(secondCandidate))
                {
                    resolvedCell = secondCandidate;
                    return true;
                }
            }
        }

        resolvedCell = requestedCell;
        return false;
    }

    private int CalculateHeuristic(
        Vector2Int first,
        Vector2Int second)
    {
        int deltaX = Mathf.Abs(first.x - second.x);
        int deltaY = Mathf.Abs(first.y - second.y);

        if (topology ==
            EnemyNavigationTopology.EightDirectionsNoCornerCutting)
        {
            int diagonalSteps = Mathf.Min(deltaX, deltaY);
            int straightSteps = Mathf.Max(deltaX, deltaY) -
                                diagonalSteps;

            return diagonalSteps * 14 + straightSteps * 10;
        }

        return (deltaX + deltaY) * 10;
    }

    private static List<Vector2Int> ReconstructCellPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        Vector2Int start)
    {
        List<Vector2Int> path =
            new List<Vector2Int> { current };

        while (cameFrom.TryGetValue(
            current,
            out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();

        if (path.Count > 0 && path[0] == start)
        {
            path.RemoveAt(0);
        }

        return path;
    }

    private static List<Vector2Int> SimplifyCollinearPath(
        List<Vector2Int> path,
        Vector2Int start)
    {
        if (path == null || path.Count < 2)
        {
            return path ?? new List<Vector2Int>();
        }

        List<Vector2Int> simplified =
            new List<Vector2Int>();

        Vector2Int previous = start;
        Vector2Int previousDirection = path[0] - previous;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int direction = path[i] - path[i - 1];

            if (direction != previousDirection)
            {
                simplified.Add(path[i - 1]);
                previousDirection = direction;
            }

            previous = path[i - 1];
        }

        simplified.Add(path[path.Count - 1]);
        return simplified;
    }

    private List<Vector2> ConvertToWorldPath(
        List<Vector2Int> cellPath)
    {
        List<Vector2> worldPath =
            new List<Vector2>(cellPath.Count);

        for (int i = 0; i < cellPath.Count; i++)
        {
            worldPath.Add(CellToWorld(cellPath[i]));
        }

        return worldPath;
    }

    private void RefreshQueueSnapshot()
    {
        queuedRequestCount = pendingRequests.Count;
        peakQueuedRequestCount = Mathf.Max(
            peakQueuedRequestCount,
            queuedRequestCount);
    }

    private sealed class PendingPathRequest
    {
        public int RequestId { get; }
        public MonoBehaviour Owner { get; }
        public Vector2 StartWorld { get; }
        public Vector2 TargetWorld { get; }
        public int MaximumPathCostInCells { get; }
        public Action<EnemyPathResult> Callback { get; }

        public PendingPathRequest(
            int requestId,
            MonoBehaviour owner,
            Vector2 startWorld,
            Vector2 targetWorld,
            int maximumPathCostInCells,
            Action<EnemyPathResult> callback)
        {
            RequestId = requestId;
            Owner = owner;
            StartWorld = startWorld;
            TargetWorld = targetWorld;
            MaximumPathCostInCells = maximumPathCostInCells;
            Callback = callback;
        }
    }

    private readonly struct OpenNode
    {
        public Vector2Int Cell { get; }
        public int GScore { get; }
        public int FScore { get; }
        public int Heuristic { get; }
        public int Sequence { get; }

        public OpenNode(
            Vector2Int cell,
            int gScore,
            int fScore,
            int heuristic,
            int sequence)
        {
            Cell = cell;
            GScore = gScore;
            FScore = fScore;
            Heuristic = heuristic;
            Sequence = sequence;
        }
    }

    private sealed class BinaryMinHeap
    {
        private readonly List<OpenNode> nodes =
            new List<OpenNode>();

        public int Count => nodes.Count;

        public void Enqueue(OpenNode node)
        {
            nodes.Add(node);
            int index = nodes.Count - 1;

            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;

                if (!ComesBefore(nodes[index], nodes[parentIndex]))
                {
                    break;
                }

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        public OpenNode Dequeue()
        {
            OpenNode root = nodes[0];
            int lastIndex = nodes.Count - 1;
            nodes[0] = nodes[lastIndex];
            nodes.RemoveAt(lastIndex);

            int index = 0;

            while (index < nodes.Count)
            {
                int leftIndex = index * 2 + 1;
                int rightIndex = leftIndex + 1;

                if (leftIndex >= nodes.Count)
                {
                    break;
                }

                int bestChildIndex = leftIndex;

                if (rightIndex < nodes.Count &&
                    ComesBefore(
                        nodes[rightIndex],
                        nodes[leftIndex]))
                {
                    bestChildIndex = rightIndex;
                }

                if (!ComesBefore(
                        nodes[bestChildIndex],
                        nodes[index]))
                {
                    break;
                }

                Swap(index, bestChildIndex);
                index = bestChildIndex;
            }

            return root;
        }

        private void Swap(int firstIndex, int secondIndex)
        {
            OpenNode temporary = nodes[firstIndex];
            nodes[firstIndex] = nodes[secondIndex];
            nodes[secondIndex] = temporary;
        }

        private static bool ComesBefore(
            OpenNode first,
            OpenNode second)
        {
            if (first.FScore != second.FScore)
            {
                return first.FScore < second.FScore;
            }

            if (first.Heuristic != second.Heuristic)
            {
                return first.Heuristic < second.Heuristic;
            }

            return first.Sequence < second.Sequence;
        }
    }
}
