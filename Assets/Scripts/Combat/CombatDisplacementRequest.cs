using System;
using UnityEngine;

public enum CombatDisplacementEndReason
{
    Completed = 0,
    CancelledByOwner = 10,
    CancelledByReplacement = 20,
    OwnerDied = 30,
    ComponentDisabled = 40
}

/// <summary>
/// A request to move one combat target without granting the caller direct
/// access to its Rigidbody2D. EnemyMotor2D remains the sole movement owner.
/// Collision-safe travel is implemented in the next combat phase; CB0 fixes
/// the ownership and lifecycle contract first.
/// </summary>
[Serializable]
public struct CombatDisplacementRequest
{
    [SerializeField] private CombatAttackId attackId;
    [SerializeField] private Vector2 direction;
    [Min(0f)]
    [SerializeField] private float distance;
    [Min(0f)]
    [SerializeField] private float duration;

    public CombatAttackId AttackId => attackId;
    public Vector2 Direction => direction;
    public Vector2 NormalizedDirection =>
        direction.sqrMagnitude > 0.000001f
            ? direction.normalized
            : Vector2.zero;

    public float Distance => distance;
    public float Duration => duration;

    public bool IsValid =>
        attackId.IsValid &&
        direction.sqrMagnitude > 0.000001f &&
        distance > 0f &&
        duration > 0f;

    public CombatDisplacementRequest(
        CombatAttackId newAttackId,
        Vector2 newDirection,
        float newDistance,
        float newDuration)
    {
        attackId = newAttackId;
        direction = newDirection;
        distance = Mathf.Max(0f, newDistance);
        duration = Mathf.Max(0f, newDuration);
    }
}
