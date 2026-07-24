using System;
using UnityEngine;

/// <summary>
/// Rigidbody2D movement executor. It never chooses a target or requests a
/// path; EnemyNavigationAgent supplies one movement destination at a time.
///
/// CB0 adds a controlled combat-displacement channel. Callers may request a
/// displacement but never move the Rigidbody2D directly, preserving the
/// single-owner rule established by EA3.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyMotor2D : MonoBehaviour
{
    [Header("EA3 motor settings")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 3.2f;

    [Header("CB0 combat displacement contract")]
    [SerializeField] private bool combatDisplacementActive;
    [SerializeField] private CombatDisplacementRequest activeDisplacement;
    [SerializeField] private Vector2 combatDisplacementStartPosition;
    [SerializeField] private Vector2 combatDisplacementTargetPosition;
    [SerializeField] private float combatDisplacementSpeed;
    [SerializeField] private float combatDisplacementStartedAt;

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

    private Rigidbody2D body;

    public bool IsInitialized => initialized;
    public Rigidbody2D Body => body;
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
        ResetCombatDisplacementFields();
        initialized = body != null;
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

    /// <summary>
    /// Reserves the motor for a combat displacement. The caller supplies the
    /// desired displacement only; EnemyMotor2D remains the Rigidbody2D owner.
    /// CB1 will add wall-aware casting before this contract is used by player
    /// input. No current baseline system calls this method.
    /// </summary>
    public bool TryBeginCombatDisplacement(
        CombatDisplacementRequest request,
        bool replaceExisting = false)
    {
        if (!initialized || body == null || !request.IsValid)
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

        Vector2 normalizedDirection =
            request.NormalizedDirection;

        combatDisplacementActive = true;
        activeDisplacement = request;
        combatDisplacementStartPosition = body.position;
        combatDisplacementTargetPosition =
            body.position +
            normalizedDirection * request.Distance;

        combatDisplacementSpeed =
            request.Distance / request.Duration;

        combatDisplacementStartedAt = Time.time;
        hasMovementIntent = false;
        movementDestination = body.position;
        lastIssuedPosition = body.position;
        combatDisplacementStartCount++;

        CombatDisplacementStarted?.Invoke(this, request);
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

        if (reachedTarget || exceededDuration)
        {
            FinishCombatDisplacement(
                CombatDisplacementEndReason.Completed);
        }
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

        if (reason == CombatDisplacementEndReason.Completed)
        {
            combatDisplacementCompleteCount++;
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
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
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
