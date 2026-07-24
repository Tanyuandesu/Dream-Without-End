using System;
using UnityEngine;

public enum CombatReactionKind
{
    None = 0,
    Hit = 10,
    Stunned = 20
}

/// <summary>
/// Temporary interruption request consumed by an enemy state owner.
/// It deliberately does not expose EnemyRuntimeState so the combat core does
/// not need to know the enemy AI implementation.
/// </summary>
[Serializable]
public struct CombatReactionRequest
{
    [SerializeField] private CombatAttackId attackId;
    [SerializeField] private CombatReactionKind kind;
    [Min(0f)]
    [SerializeField] private float duration;
    [SerializeField] private bool extendExistingReaction;
    [SerializeField] private string reason;

    public CombatAttackId AttackId => attackId;
    public CombatReactionKind Kind => kind;
    public float Duration => duration;
    public bool ExtendExistingReaction => extendExistingReaction;
    public string Reason => reason;

    public bool IsValid =>
        attackId.IsValid &&
        kind != CombatReactionKind.None &&
        duration >= 0f;

    public CombatReactionRequest(
        CombatAttackId newAttackId,
        CombatReactionKind newKind,
        float newDuration,
        bool shouldExtendExistingReaction,
        string newReason)
    {
        attackId = newAttackId;
        kind = newKind;
        duration = Mathf.Max(0f, newDuration);
        extendExistingReaction = shouldExtendExistingReaction;
        reason = newReason ?? string.Empty;
    }
}
