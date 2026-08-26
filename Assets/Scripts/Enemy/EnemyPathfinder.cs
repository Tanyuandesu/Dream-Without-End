using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EA3 compatibility facade for one enemy.
///
/// It no longer contains A*. All queries are delegated to the floor's shared
/// EnemyPathService. Existing WorldToCell/CellToWorld/FindPath callers remain
/// source-compatible while new runtime agents use the queued request API.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPathfinder : MonoBehaviour
{
    [Header("EA3 shared service reference")]
    [SerializeField] private EnemyPathService pathService;
    [SerializeField] private bool initialized;

    public bool IsInitialized =>
        initialized &&
        pathService != null &&
        pathService.IsInitialized;

    public EnemyPathService Service => pathService;

    public void Initialize(EnemyPathService newPathService)
    {
        pathService = newPathService;
        initialized =
            pathService != null &&
            pathService.IsInitialized;
    }

    /// <summary>
    /// Legacy setup entry retained for isolated tests. Production spawning
    /// creates one service on the generated floor and calls Initialize(service).
    /// </summary>
    public void Initialize(
        DungeonLayout dungeonLayout,
        float dungeonCellSize)
    {
        EnemyPathService service =
            GetComponentInParent<EnemyPathService>();

        if (service == null)
        {
            Transform serviceOwner = transform.parent != null
                ? transform.parent
                : transform;

            service = serviceOwner.gameObject.AddComponent<
                EnemyPathService>();
        }

        if (!service.IsInitialized)
        {
            service.Initialize(
                dungeonLayout,
                dungeonCellSize,
                EnemyNavigationTopology.FourDirections,
                2,
                4096,
                1,
                false);
        }

        Initialize(service);
    }

    /// <summary>
    /// Compatibility-only synchronous query. EnemyNavigationAgent uses the
    /// centrally queued TryRequestPath API instead.
    /// </summary>
    public List<Vector2> FindPath(
        Vector2 startWorld,
        Vector2 targetWorld)
    {
        if (!IsInitialized)
        {
            return new List<Vector2>();
        }

        EnemyPathResult result = pathService.FindPathImmediate(
            startWorld,
            targetWorld);

        if (!result.Success)
        {
            return new List<Vector2>();
        }

        List<Vector2> path =
            new List<Vector2>(result.WorldPath.Count);

        for (int i = 0; i < result.WorldPath.Count; i++)
        {
            path.Add(result.WorldPath[i]);
        }

        return path;
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
        if (!IsInitialized)
        {
            requestId = 0;
            rejectionReason =
                EnemyPathFailureReason.ServiceNotInitialized;

            rejectionDetails =
                "EnemyPathfinder has no initialized shared service.";

            return false;
        }

        return pathService.TryRequestPath(
            owner,
            startWorld,
            targetWorld,
            maximumPathCostInCells,
            callback,
            out requestId,
            out rejectionReason,
            out rejectionDetails);
    }

    public void CancelRequest(int requestId)
    {
        if (pathService != null)
        {
            pathService.CancelRequest(requestId);
        }
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        return pathService != null
            ? pathService.WorldToCell(worldPosition)
            : Vector2Int.RoundToInt(worldPosition);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return pathService != null
            ? pathService.CellToWorld(cell)
            : (Vector2)cell;
    }

    public bool AreInSameCell(
        Vector2 firstWorld,
        Vector2 secondWorld)
    {
        return pathService != null
            ? pathService.AreInSameCell(firstWorld, secondWorld)
            : Vector2Int.RoundToInt(firstWorld) ==
              Vector2Int.RoundToInt(secondWorld);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return pathService != null &&
               pathService.IsWalkable(cell);
    }
}
