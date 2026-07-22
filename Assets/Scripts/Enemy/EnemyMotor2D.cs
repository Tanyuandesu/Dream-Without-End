using UnityEngine;

/// <summary>
/// Rigidbody2D movement executor. It never chooses a target or requests a
/// path; EnemyNavigationAgent supplies one movement destination at a time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyMotor2D : MonoBehaviour
{
    [Header("EA3 motor settings")]
    [Min(0.1f)]
    [SerializeField] private float moveSpeed = 3.2f;

    [Header("Runtime diagnostics (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool hasMovementIntent;
    [SerializeField] private Vector2 movementDestination;
    [SerializeField] private Vector2 lastIssuedPosition;
    [SerializeField] private int moveCommandCount;
    [SerializeField] private int recoverySnapCount;

    private Rigidbody2D body;

    public bool IsInitialized => initialized;
    public Rigidbody2D Body => body;
    public float MoveSpeed => moveSpeed;
    public bool HasMovementIntent => hasMovementIntent;
    public Vector2 MovementDestination => movementDestination;
    public Vector2 LastIssuedPosition => lastIssuedPosition;
    public int MoveCommandCount => moveCommandCount;
    public int RecoverySnapCount => recoverySnapCount;

    private void Awake()
    {
        CacheBody();
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
        initialized = body != null;
    }

    public void ApplySpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    /// <summary>
    /// Issues one fixed-step MovePosition command.
    /// Returns true when this command reaches the destination tolerance.
    /// </summary>
    public bool MoveTowards(
        Vector2 destination,
        float tolerance)
    {
        if (!initialized || body == null)
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

    public void SnapToRecoveryCell(Vector2 position)
    {
        if (!initialized || body == null)
        {
            return;
        }

        body.position = position;
        hasMovementIntent = false;
        movementDestination = position;
        lastIssuedPosition = position;
        recoverySnapCount++;
    }

    public void Stop()
    {
        hasMovementIntent = false;

        if (body != null)
        {
            movementDestination = body.position;
            lastIssuedPosition = body.position;
        }
    }

    private void CacheBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }
}
