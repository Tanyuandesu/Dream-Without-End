using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared runtime references and mutable observation data for one enemy.
///
/// EnemyRuntimeIdentity remains immutable spawn provenance. This component is
/// the single context consumed by the state machine and later navigation,
/// perception and combat controllers.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyRuntimeContext : MonoBehaviour
{
    [Header("EA2/EA3 authority")]
    [SerializeField] private string enemyId = "uninitialized";
    [SerializeField] private EnemyRuntimeIdentity identity;
    [SerializeField] private EnemyDefinition definition;

    [Header("Runtime references")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private EnemyPathfinder pathfinder;
    [SerializeField] private EnemyDetection detection;
    [SerializeField] private Health health;
    [SerializeField] private EnemyVisual visual;

    [Header("EA3 navigation references")]
    [SerializeField] private EnemyPathService pathService;
    [SerializeField] private EnemyNavigationAgent navigationAgent;
    [SerializeField] private EnemyMotor2D motor;

    [Header("Home anchor")]
    [SerializeField] private int homeRoomIndex = -1;
    [SerializeField] private Vector2Int homeCell;
    [SerializeField] private Vector2 homeWorldPosition;

    [Header("Runtime debug snapshot")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool hasLastKnownTargetPosition;
    [SerializeField] private Vector2 lastKnownTargetPosition;
    [SerializeField] private bool hasNavigationDestination;
    [SerializeField] private Vector2 navigationDestination;

    [Header("Optional scene diagnostics")]
    [SerializeField] private bool showRuntimeGizmos = true;

    public bool IsInitialized => initialized;
    public string EnemyId => enemyId;
    public EnemyRuntimeIdentity Identity => identity;
    public EnemyDefinition Definition => definition;
    public Transform CurrentTarget => currentTarget;
    public Rigidbody2D Body => body;
    public EnemyPathfinder Pathfinder => pathfinder;
    public EnemyDetection Detection => detection;
    public Health Health => health;
    public EnemyVisual Visual => visual;
    public EnemyPathService PathService => pathService;
    public EnemyNavigationAgent NavigationAgent => navigationAgent;
    public EnemyMotor2D Motor => motor;
    public int HomeRoomIndex => homeRoomIndex;
    public Vector2Int HomeCell => homeCell;
    public Vector2 HomeWorldPosition => homeWorldPosition;
    public bool HasLastKnownTargetPosition =>
        hasLastKnownTargetPosition;

    public Vector2 LastKnownTargetPosition =>
        lastKnownTargetPosition;

    public bool HasNavigationDestination =>
        hasNavigationDestination;

    public Vector2 NavigationDestination =>
        navigationDestination;

    private void Awake()
    {
        CacheLocalComponents();
        RefreshEnemyId();
    }

    public void Initialize(
        EnemyRuntimeIdentity newIdentity,
        EnemyDefinition newDefinition,
        Transform newTarget,
        Rigidbody2D newBody,
        EnemyPathfinder newPathfinder,
        EnemyDetection newDetection,
        Health newHealth,
        EnemyVisual newVisual,
        int newHomeRoomIndex,
        Vector2Int newHomeCell,
        Vector2 newHomeWorldPosition)
    {
        identity = newIdentity;
        definition = newDefinition != null
            ? newDefinition
            : newIdentity != null
                ? newIdentity.Definition
                : null;

        currentTarget = newTarget;
        body = newBody;
        pathfinder = newPathfinder;
        detection = newDetection;
        health = newHealth;
        visual = newVisual;

        homeRoomIndex = newHomeRoomIndex;
        homeCell = newHomeCell;
        homeWorldPosition = newHomeWorldPosition;

        hasLastKnownTargetPosition = false;
        lastKnownTargetPosition = Vector2.zero;
        hasNavigationDestination = false;
        navigationDestination = Vector2.zero;

        CacheLocalComponents();
        RefreshEnemyId();

        initialized =
            currentTarget != null &&
            body != null &&
            pathfinder != null &&
            detection != null &&
            health != null;
    }

    public void AttachNavigation(
        EnemyPathService newPathService,
        EnemyNavigationAgent newNavigationAgent,
        EnemyMotor2D newMotor)
    {
        pathService = newPathService;
        navigationAgent = newNavigationAgent;
        motor = newMotor;
    }

    public void SetLastKnownTargetPosition(Vector2 position)
    {
        lastKnownTargetPosition = position;
        hasLastKnownTargetPosition = true;
    }

    public void ClearLastKnownTargetPosition()
    {
        hasLastKnownTargetPosition = false;
    }

    public void SetNavigationDestination(Vector2 destination)
    {
        navigationDestination = destination;
        hasNavigationDestination = true;
    }

    public void ClearNavigationDestination()
    {
        hasNavigationDestination = false;
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        string prefix = gameObject.name + ": ";

        if (!initialized)
        {
            errors.Add(prefix + "EnemyRuntimeContext is not initialized.");
        }

        if (identity == null)
        {
            errors.Add(prefix + "EnemyRuntimeIdentity is missing.");
        }

        if (definition == null)
        {
            errors.Add(prefix + "EnemyDefinition is missing.");
        }

        if (currentTarget == null)
        {
            errors.Add(prefix + "Current Target is missing.");
        }

        if (body == null ||
            pathfinder == null ||
            detection == null ||
            health == null ||
            visual == null)
        {
            errors.Add(
                prefix +
                "one or more required runtime component references are missing.");
        }

        if (pathService == null ||
            navigationAgent == null ||
            motor == null)
        {
            errors.Add(
                prefix +
                "one or more EA3 navigation references are missing.");
        }

        if (pathService != null &&
            pathfinder != null &&
            pathfinder.Service != pathService)
        {
            errors.Add(
                prefix +
                "EnemyPathfinder and Context reference different path services.");
        }

        if (navigationAgent != null &&
            navigationAgent.Context != this)
        {
            errors.Add(
                prefix +
                "EnemyNavigationAgent references a different runtime context.");
        }

        if (motor != null &&
            motor.Body != body)
        {
            errors.Add(
                prefix +
                "EnemyMotor2D references a different Rigidbody2D.");
        }

        if (identity != null &&
            enemyId != identity.EnemyId.Value)
        {
            errors.Add(
                prefix +
                "debug EnemyId does not match EnemyRuntimeIdentity.");
        }

        if (identity != null &&
            (homeRoomIndex != identity.RoomIndex ||
             homeCell != identity.SpawnCell))
        {
            errors.Add(
                prefix +
                "Home Anchor does not match immutable spawn provenance.");
        }
    }

    private void CacheLocalComponents()
    {
        if (identity == null)
        {
            identity = GetComponent<EnemyRuntimeIdentity>();
        }

        if (definition == null && identity != null)
        {
            definition = identity.Definition;
        }

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

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (visual == null)
        {
            visual = GetComponent<EnemyVisual>();
        }

        if (pathService == null && pathfinder != null)
        {
            pathService = pathfinder.Service;
        }

        if (navigationAgent == null)
        {
            navigationAgent =
                GetComponent<EnemyNavigationAgent>();
        }

        if (motor == null)
        {
            motor = GetComponent<EnemyMotor2D>();
        }
    }

    private void RefreshEnemyId()
    {
        if (identity != null)
        {
            enemyId = identity.EnemyId.Value;
            return;
        }

        if (definition != null)
        {
            enemyId = definition.Id.Value;
            return;
        }

        enemyId = "uninitialized";
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRuntimeGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.75f, 1f);
        Gizmos.DrawWireCube(
            homeWorldPosition,
            new Vector3(0.22f, 0.22f, 0f));

        if (hasLastKnownTargetPosition)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.85f);
            Gizmos.DrawLine(
                transform.position,
                lastKnownTargetPosition);

            Gizmos.DrawWireSphere(
                lastKnownTargetPosition,
                0.12f);
        }
    }
}
