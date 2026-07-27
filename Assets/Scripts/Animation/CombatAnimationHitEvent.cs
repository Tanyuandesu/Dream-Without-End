using UnityEngine;

/// <summary>
/// Read-only presentation summary emitted after an enemy accepted a combat hit.
/// Animation reads this message but never owns damage, displacement or AI state.
/// </summary>
public readonly struct CombatAnimationHitEvent
{
    public CombatAnimationHitEvent(
        CombatHit hit,
        bool damageAccepted,
        bool displacementAccepted,
        bool reactionAccepted,
        bool directReactionSuppressed)
    {
        Hit = hit;
        DamageAccepted = damageAccepted;
        DisplacementAccepted = displacementAccepted;
        ReactionAccepted = reactionAccepted;
        DirectReactionSuppressed = directReactionSuppressed;
    }

    public CombatHit Hit { get; }
    public bool DamageAccepted { get; }
    public bool DisplacementAccepted { get; }
    public bool ReactionAccepted { get; }
    public bool DirectReactionSuppressed { get; }

    public bool HasVisibleKnockback =>
        DisplacementAccepted || ReactionAccepted;
}
