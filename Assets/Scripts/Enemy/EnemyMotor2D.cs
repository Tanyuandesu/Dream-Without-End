using System;
using UnityEngine;

/// <summary>
/// Rigidbody2D movement executor. It never chooses a target or requests a
/// path; EnemyNavigationAgent supplies one movement destination at a time.
///
/// Combat callers submit displacement requests instead of moving the body.
/// CB1 and later phases clip each requested displacement against solid static/kinematic 2D
/// geometry before issuing MovePosition commands, preserving EA3's single
/// Rigidbody2D owner and preventing wall penetration.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyMotor2D : MonoBehaviour
{
    private const float MinimumDisplacementDistance = 0.0005f;
    private const int CastResultCapacity = 32;

    [Header("EA3 motor settings")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 3.2f;

    [Header("Combat collision-safe displacement")]
    [SerializeField] private Collider2D bodyCollider;

    [Tooltip(
        "Small clearance retained between the enemy collider and blocking geometry.")]
    [Range(0f, 0.08f)]
    [SerializeField] private float collisionSkin = 0.015f;

    [SerializeField] private bool combatDisplacementActive;
    [SerializeField] private CombatDisplacementRequest activeDisplacement;
    [SerializeField] private Vector2 combatDisplacementStartPosition;
    [SerializeField] private Vector2 combatDisplacementTargetPosition;
    [SerializeField] private float combatDisplacementSpeed;
    [SerializeField] private float combatDisplacementStartedAt;
    [SerializeField] private bool activeDisplacementWasClipped;
    [SerializeField] private CombatDisplacementEndReason targetEndReason;

    [Header("Runtime diagnostics (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool hasMovementIntent;
    [SerializeField] private Vector2 movementDestination;
    [SerializeField] private Vector2 lastIssuedPosition;
    [SerializeField] private int moveCommandCount;
    [SerializeField] private int recoverySnapCount;
    [SerializeField] private int combatDisplacementStartCount;
    [SerializeField] private int combatDisplacementCompleteCount;
    [SerializeField] private int combatDisplacementCancelCount;
    [SerializeField] private int combatDisplacementBlockedCount;
    [SerializeField] private float lastRequestedDisplacementDistance;
    [SerializeField] private float lastSafeDisplacementDistance;
    [SerializeField] private Collider2D lastBlockingCollider;
    [SerializeField] private CombatDisplacementEndReason lastDisplacementEndReason;

    private Rigidbody2D body;
    private ContactFilter2D solidCastFilter;

    private readonly RaycastHit2D[] solidCastHits =
        new RaycastHit2D[CastResultCapacity];

    public bool IsInitialized => initialized;
    public Rigidbody2D Body => body;
    public Collider2D BodyCollider => bodyCollider;
    public float MoveSpeed => moveSpeed;
    public bool HasMovementIntent => hasMovementIntent;
    public Vector2 MovementDestination => movementDestination;
    public Vector2 LastIssuedPosition => lastIssuedPosition;
    public int MoveCommandCount => moveCommandCount;
    public int RecoverySnapCount => recoverySnapCount;

    public bool IsCombatDisplacementActive =>
        combatDisplacementActive;

    public CombatDisplacementRequest ActiveCombatDisplacement =>
        activeDisplacement;

    public Vector2 CombatDisplacementStartPosition =>
        combatDisplacementStartPosition;

    public Vector2 CombatDisplacementTargetPosition =>
        combatDisplacementTargetPosition;

    public int CombatDisplacementStartCount =>
        combatDisplacementStartCount;

    public int CombatDisplacementCompleteCount =>
        combatDisplacementCompleteCount;

    public int CombatDisplacementCancelCount =>
        combatDisplacementCancelCount;

    public int CombatDisplacementBlockedCount =>
        combatDisplacementBlockedCount;

    public float LastRequestedDisplacementDistance =>
        lastRequestedDisplacementDistance;

    public float LastSafeDisplacementDistance =>
        lastSafeDisplacementDistance;

    public Collider2D LastBlockingCollider => lastBlockingCollider;

    public CombatDisplacementEndReason LastDisplacementEndReason =>
        lastDisplacementEndReason;

    public event Action<
        EnemyMotor2D,
        CombatDisplacementRequest> CombatDisplacementStarted;

    public event Action<
        EnemyMotor2D,
        CombatDisplacementRequest,
        CombatDisplacementEndReason> CombatDisplacementEnded;

    private void Awake()
    {
        CacheBody();
        CacheCollider();
        ConfigureSolidCastFilter();
    }

    private void FixedUpdate()
    {
        TickCombatDisplacement();
    }

    public void Initialize(
        Rigidbody2D newBody,
        float newMoveSpeed)
    {
        body = newBody;
        CacheBody();
        CacheCollider();
        ConfigureSolidCastFilter();
        ApplySpeed(newMoveSpeed);

        hasMovementIntent = false;
        movementDestination = body != null
            ? body.position
            : (Vector2)transform.position;

        lastIssuedPosition = movementDestination;
        moveCommandCount = 0;
        recoverySnapCount = 0;
        combatDisplacementStartCount = 0;
        combatDisplacementCompleteCount = 0;
        combatDisplacementCancelCount = 0;
        combatDisplacementBlockedCount = 0;
        lastRequestedDisplacementDistance = 0f;
        lastSafeDisplacementDistance = 0f;
        lastBlockingCollider = null;
        lastDisplacementEndReason =
            CombatDisplacementEndReason.Completed;

        ResetCombatDisplacementFields();
        initialized = body != null && bodyCollider != null;
    }

    public void ApplySpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    /// <summary>
    /// Issues one fixed-step navigation MovePosition command.
    /// Combat displacement has higher authority and temporarily rejects this
    /// navigation command without cancelling the active displacement.
    /// Returns true when this command reaches the destination tolerance.
    /// </summary>
    public bool MoveTowards(
        Vector2 destination,
        float tolerance)
    {
        if (!initialized ||
            body == null ||
            combatDisplacementActive)
        {
            return false;
        }

        float safeTolerance = Mathf.Max(0.001f, tolerance);
        float step = moveSpeed * Time.fixedDeltaTime;

        Vector2 nextPosition = Vector2.MoveTowards(
            body.position,
            destination,
            step);

        bool reachedDestination =
            Vector2.Distance(
                nextPosition,
                destination) <= safeTolerance;

        if (reachedDestination)
        {
            nextPosition = destination;
        }

        hasMovementIntent = true;
        movementDestination = destination;
        lastIssuedPosition = nextPosition;
        moveCommandCount++;
        body.MovePosition(nextPosition);
        return reachedDestination;
    }

    public bool TryBeginCombatDisplacement(
        CombatDisplacementRequest request,
        bool replaceExisting = false)
    {
        return TryBeginCombatDisplacement(
            request,
            null,
            replaceExisting);
    }

    /// <summary>
    /// Reserves the motor for one collision-safe combat displacement.
    /// Dynamic actor bodies are not treated as geometry blockers by the
    /// pre-cast, allowing a fan push to move several enemies together. Static
    /// and kinematic colliders clip the travel distance before movement starts.
    /// </summary>
    public bool TryBeginCombatDisplacement(
        CombatDisplacementRequest request,
        Collider2D ignoredSourceCollider,
        bool replaceExisting = false)
    {
        if (!initialized ||
            body == null ||
            bodyCollider == null ||
            !request.IsValid)
        {
            return false;
        }

        if (combatDisplacementActive)
        {
            if (!replaceExisting)
            {
                return false;
            }

            FinishCombatDisplacement(
                CombatDisplacementEndReason.CancelledByReplacement);
        }

        Vector2 normalizedDirection = request.NormalizedDirection;

        float safeDistance = CalculateSafeDisplacementDistance(
            normalizedDirection,
            request.Distance,
            ignoredSourceCollider,
            out Collider2D blockingCollider);

        combatDisplacementActive = true;
        activeDisplacement = request;
        combatDisplacementStartPosition = body.position;
        combatDisplacementTargetPosition =
            body.position + normalizedDirection * safeDistance;

        combatDisplacementSpeed =
            request.Distance / request.Duration;

        combatDisplacementStartedAt = Time.time;
        activeDisplacementWasClipped =
            safeDistance < request.Distance - MinimumDisplacementDistance;

        targetEndReason = activeDisplacementWasClipped
            ? CombatDisplacementEndReason.BlockedByCollision
            : CombatDisplacementEndReason.Completed;

        hasMovementIntent = false;
        movementDestination = body.position;
        lastIssuedPosition = body.position;
        combatDisplacementStartCount++;

        lastRequestedDisplacementDistance = request.Distance;
        lastSafeDisplacementDistance = safeDistance;
        lastBlockingCollider = blockingCollider;

        CombatDisplacementStarted?.Invoke(this, request);

        if (safeDistance <= MinimumDisplacementDistance)
        {
            FinishCombatDisplacement(
                CombatDisplacementEndReason.BlockedByCollision);
        }

        return true;
    }

    public bool CancelCombatDisplacement(
        CombatDisplacementEndReason reason =
            CombatDisplacementEndReason.CancelledByOwner)
    {
        if (!combatDisplacementActive)
        {
            return false;
        }

        if (reason == CombatDisplacementEndReason.Completed)
        {
            reason = CombatDisplacementEndReason.CancelledByOwner;
        }

        FinishCombatDisplacement(reason);
        return true;
    }

    public void SnapToRecoveryCell(Vector2 position)
    {
        if (!initialized || body == null)
        {
            return;
        }

        if (combatDisplacementActive)
        {
            FinishCombatDisplacement(
                CombatDisplacementEndReason.CancelledByOwner);
        }

        body.position = position;
        hasMovementIntent = false;
        movementDestination = position;
        lastIssuedPosition = position;
        recoverySnapCount++;
    }

    /// <summary>
    /// Stops navigation intent only. It deliberately does not cancel an
    /// active combat displacement, because reaction states call Stop while
    /// the combat movement channel still owns the Rigidbody2D.
    /// </summary>
    public void Stop()
    {
        hasMovementIntent = false;

        if (body != null)
        {
            movementDestination = body.position;
            lastIssuedPosition = body.position;
        }
    }

    private void TickCombatDisplacement()
    {
        if (!combatDisplacementActive || body == null)
        {
            return;
        }

        float safeSpeed = Mathf.Max(
            0.001f,
            combatDisplacementSpeed);

        Vector2 nextPosition = Vector2.MoveTowards(
            body.position,
            combatDisplacementTargetPosition,
            safeSpeed * Time.fixedDeltaTime);

        lastIssuedPosition = nextPosition;
        body.MovePosition(nextPosition);

        bool reachedTarget = Vector2.Distance(
            nextPosition,
            combatDisplacementTargetPosition) <= 0.001f;

        float maximumExpectedDuration =
            Mathf.Max(0.01f, activeDisplacement.Duration) +
            Time.fixedDeltaTime * 2f;

        bool exceededDuration =
            Time.time - combatDisplacementStartedAt >=
            maximumExpectedDuration;

        if (reachedTarget)
        {
            FinishCombatDisplacement(targetEndReason);
            return;
        }

        if (exceededDuration)
        {
            FinishCombatDisplacement(
                CombatDisplacementEndReason.BlockedByCollision);
        }
    }

    private float CalculateSafeDisplacementDistance(
        Vector2 direction,
        float requestedDistance,
        Collider2D ignoredSourceCollider,
        out Collider2D blockingCollider)
    {
        blockingCollider = null;

        if (bodyCollider == null || requestedDistance <= 0f)
        {
            return Mathf.Max(0f, requestedDistance);
        }

        float castDistance = requestedDistance + collisionSkin;

        int hitCount = bodyCollider.Cast(
            direction,
            solidCastFilter,
            solidCastHits,
            castDistance,
            ignoreSiblingColliders: true);

        float nearestBlockingDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = solidCastHits[i];
            Collider2D candidate = hit.collider;

            if (!IsSolidGeometryBlocker(
                    candidate,
                    ignoredSourceCollider))
            {
                continue;
            }

            if (hit.distance >= nearestBlockingDistance)
            {
                continue;
            }

            nearestBlockingDistance = hit.distance;
            blockingCollider = candidate;
        }

        if (float.IsPositiveInfinity(nearestBlockingDistance))
        {
            return requestedDistance;
        }

        return Mathf.Clamp(
            nearestBlockingDistance - collisionSkin,
            0f,
            requestedDistance);
    }

    private bool IsSolidGeometryBlocker(
        Collider2D candidate,
        Collider2D ignoredSourceCollider)
    {
        if (candidate == null ||
            !candidate.enabled ||
            candidate.isTrigger ||
            candidate == bodyCollider ||
            candidate == ignoredSourceCollider)
        {
            return false;
        }

        Rigidbody2D attachedBody = candidate.attachedRigidbody;

        if (attachedBody == body)
        {
            return false;
        }

        // Dynamic bodies are actors. The physics solver may still resolve
        // their contacts during movement, but they do not clip the initial
        // fan displacement and therefore do not prevent group pushes.
        return attachedBody == null ||
               attachedBody.bodyType != RigidbodyType2D.Dynamic;
    }

    private void FinishCombatDisplacement(
        CombatDisplacementEndReason reason)
    {
        if (!combatDisplacementActive)
        {
            return;
        }

        CombatDisplacementRequest completedRequest =
            activeDisplacement;

        combatDisplacementActive = false;
        lastDisplacementEndReason = reason;

        if (reason == CombatDisplacementEndReason.Completed)
        {
            combatDisplacementCompleteCount++;
        }
        else if (reason ==
                 CombatDisplacementEndReason.BlockedByCollision)
        {
            combatDisplacementBlockedCount++;
        }
        else
        {
            combatDisplacementCancelCount++;
        }

        if (body != null)
        {
            hasMovementIntent = false;
            movementDestination = body.position;
            lastIssuedPosition = body.position;
        }

        ResetCombatDisplacementFields(
            preserveActiveFlag: true);

        CombatDisplacementEnded?.Invoke(
            this,
            completedRequest,
            reason);
    }

    private void ResetCombatDisplacementFields(
        bool preserveActiveFlag = false)
    {
        if (!preserveActiveFlag)
        {
            combatDisplacementActive = false;
        }

        activeDisplacement =
            default(CombatDisplacementRequest);

        Vector2 currentPosition = body != null
            ? body.position
            : (Vector2)transform.position;

        combatDisplacementStartPosition = currentPosition;
        combatDisplacementTargetPosition = currentPosition;
        combatDisplacementSpeed = 0f;
        combatDisplacementStartedAt = 0f;
        activeDisplacementWasClipped = false;
        targetEndReason = CombatDisplacementEndReason.Completed;
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void CacheCollider()
    {
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureSolidCastFilter()
    {
        solidCastFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = Physics2D.AllLayers,
            useTriggers = false
        };
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        collisionSkin = Mathf.Clamp(collisionSkin, 0f, 0.08f);
        CacheCollider();
    }

    private void OnDisable()
    {
        if (combatDisplacementActive)
        {
            FinishCombatDisplacement(
                CombatDisplacementEndReason.ComponentDisabled);
        }
    }
}
