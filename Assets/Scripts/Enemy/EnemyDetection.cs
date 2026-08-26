using UnityEngine;

/// <summary>
/// Enemy perception gate. Radius, line of sight and initial-acquisition view
/// angle are authored by EnemyDefinition and supplied by EnemySpawner.
///
/// The view cone gates only initial acquisition. Once a target is detected,
/// retention uses LoseTargetRadius and line of sight so an enemy does not
/// forget the player merely because a corridor turn briefly places the target
/// behind its current movement direction.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 20f;
    [SerializeField] private float loseTargetRadius = 24f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Range(1f, 360f)]
    [SerializeField] private float viewAngle = 360f;

    [Header("T5B facing source")]
    [SerializeField] private EnemyMotor2D facingSource;
    [SerializeField] private Vector2 fallbackFacingDirection = Vector2.down;

    [Header("Runtime diagnostics (read only during Play Mode)")]
    [SerializeField] private float distanceToTarget;
    [SerializeField] private bool targetWithinActiveRadius;
    [SerializeField] private bool targetWithinViewAngle;
    [SerializeField] private bool targetHasClearLineOfSight;

    private Transform target;

    public Transform Target => target;
    public float DetectionRadius => detectionRadius;
    public float LoseTargetRadius => loseTargetRadius;
    public bool RequiresLineOfSight => requireLineOfSight;
    public LayerMask ObstacleMask => obstacleMask;
    public float ViewAngle => viewAngle;
    public EnemyMotor2D FacingSource => facingSource;
    public Vector2 FacingDirection => ResolveFacingDirection();
    public float DistanceToTarget => distanceToTarget;
    public bool IsTargetWithinActiveRadius => targetWithinActiveRadius;
    public bool IsTargetWithinViewAngle => targetWithinViewAngle;
    public bool HasClearLineOfSightToTarget => targetHasClearLineOfSight;
    public bool IsTargetDetected { get; private set; }
    public Vector2 LastKnownTargetPosition { get; private set; }

    public void Initialize(
        Transform newTarget,
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask)
    {
        Initialize(
            newTarget,
            newDetectionRadius,
            newLoseTargetRadius,
            newRequireLineOfSight,
            newObstacleMask,
            360f,
            null);
    }

    public void Initialize(
        Transform newTarget,
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask,
        float newViewAngle,
        EnemyMotor2D newFacingSource)
    {
        target = newTarget;
        facingSource = newFacingSource;

        ApplySettings(
            newDetectionRadius,
            newLoseTargetRadius,
            newRequireLineOfSight,
            newObstacleMask,
            newViewAngle);
    }

    public void ApplySettings(
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask)
    {
        ApplySettings(
            newDetectionRadius,
            newLoseTargetRadius,
            newRequireLineOfSight,
            newObstacleMask,
            viewAngle);
    }

    public void ApplySettings(
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask,
        float newViewAngle)
    {
        detectionRadius = Mathf.Max(0.1f, newDetectionRadius);
        loseTargetRadius = Mathf.Max(
            detectionRadius,
            newLoseTargetRadius);

        requireLineOfSight = newRequireLineOfSight;
        obstacleMask = newObstacleMask;
        viewAngle = Mathf.Clamp(newViewAngle, 1f, 360f);
    }

    public void AttachFacingSource(EnemyMotor2D newFacingSource)
    {
        facingSource = newFacingSource;
    }

    private void Update()
    {
        if (target == null)
        {
            ResetObservationSnapshot();
            IsTargetDetected = false;
            return;
        }

        Vector2 origin = transform.position;
        Vector2 destination = target.position;
        Vector2 toTarget = destination - origin;

        distanceToTarget = toTarget.magnitude;

        float activeRadius = IsTargetDetected
            ? loseTargetRadius
            : detectionRadius;

        targetWithinActiveRadius =
            distanceToTarget <= activeRadius;

        if (!targetWithinActiveRadius)
        {
            targetWithinViewAngle = false;
            targetHasClearLineOfSight = false;
            IsTargetDetected = false;
            return;
        }

        targetHasClearLineOfSight =
            !requireLineOfSight || HasClearLineOfSight();

        if (!targetHasClearLineOfSight)
        {
            targetWithinViewAngle = false;
            IsTargetDetected = false;
            return;
        }

        targetWithinViewAngle =
            IsTargetDetected || IsWithinViewAngle(toTarget);

        if (!targetWithinViewAngle)
        {
            IsTargetDetected = false;
            return;
        }

        IsTargetDetected = true;
        LastKnownTargetPosition = destination;
    }

    private bool IsWithinViewAngle(Vector2 toTarget)
    {
        if (viewAngle >= 359.9f ||
            toTarget.sqrMagnitude <= 0.000001f)
        {
            return true;
        }

        Vector2 facing = ResolveFacingDirection();
        Vector2 directionToTarget = toTarget.normalized;
        float halfAngle = viewAngle * 0.5f;
        float minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

        return Vector2.Dot(facing, directionToTarget) >= minimumDot;
    }

    private Vector2 ResolveFacingDirection()
    {
        if (facingSource != null &&
            facingSource.FacingDirection.sqrMagnitude > 0.000001f)
        {
            return facingSource.FacingDirection.normalized;
        }

        if (fallbackFacingDirection.sqrMagnitude <= 0.000001f)
        {
            return Vector2.down;
        }

        return fallbackFacingDirection.normalized;
    }

    private bool HasClearLineOfSight()
    {
        Vector2 origin = transform.position;
        Vector2 destination = target.position;
        Vector2 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction.normalized,
            distance,
            obstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider == null || hitCollider.isTrigger)
            {
                continue;
            }

            if (hitCollider.transform == transform ||
                hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitCollider.transform == target ||
                hitCollider.transform.IsChildOf(target))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void ResetObservationSnapshot()
    {
        distanceToTarget = 0f;
        targetWithinActiveRadius = false;
        targetWithinViewAngle = false;
        targetHasClearLineOfSight = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectionRadius);

        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawWireSphere(origin, loseTargetRadius);

        Vector2 facing = ResolveFacingDirection();

        Gizmos.color = new Color(0.3f, 0.95f, 1f);
        Gizmos.DrawLine(
            origin,
            origin + facing * Mathf.Max(0.5f, detectionRadius));

        if (viewAngle < 359.9f)
        {
            float halfAngle = viewAngle * 0.5f;
            Vector2 left = Rotate(facing, halfAngle);
            Vector2 right = Rotate(facing, -halfAngle);

            Gizmos.color = new Color(0.95f, 0.85f, 0.2f);
            Gizmos.DrawLine(origin, origin + left * detectionRadius);
            Gizmos.DrawLine(origin, origin + right * detectionRadius);
        }

        if (target != null && IsTargetDetected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, target.position);
        }
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }
}
