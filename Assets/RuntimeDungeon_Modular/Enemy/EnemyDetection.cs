using UnityEngine;

/// <summary>
/// 敵人的索敵判定。
/// 所有主要參數由 EnemySpawner 在生成時傳入。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDetection : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 20f;
    [SerializeField] private float loseTargetRadius = 24f;
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private Transform target;

    public bool IsTargetDetected { get; private set; }
    public Vector2 LastKnownTargetPosition { get; private set; }

    public void Initialize(
        Transform newTarget,
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask)
    {
        target = newTarget;
        ApplySettings(
            newDetectionRadius,
            newLoseTargetRadius,
            newRequireLineOfSight,
            newObstacleMask);
    }

    public void ApplySettings(
        float newDetectionRadius,
        float newLoseTargetRadius,
        bool newRequireLineOfSight,
        LayerMask newObstacleMask)
    {
        detectionRadius = Mathf.Max(0.1f, newDetectionRadius);
        loseTargetRadius = Mathf.Max(
            detectionRadius,
            newLoseTargetRadius);

        requireLineOfSight = newRequireLineOfSight;
        obstacleMask = newObstacleMask;
    }

    private void Update()
    {
        if (target == null)
        {
            IsTargetDetected = false;
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            target.position);

        float activeRadius = IsTargetDetected
            ? loseTargetRadius
            : detectionRadius;

        if (distance > activeRadius)
        {
            IsTargetDetected = false;
            return;
        }

        if (requireLineOfSight && !HasClearLineOfSight())
        {
            IsTargetDetected = false;
            return;
        }

        IsTargetDetected = true;
        LastKnownTargetPosition = target.position;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius);

        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawWireSphere(
            transform.position,
            loseTargetRadius);

        if (target != null && IsTargetDetected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                transform.position,
                target.position);
        }
    }
}
