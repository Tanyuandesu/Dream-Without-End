using System;
using UnityEngine;

/// <summary>
/// Complete message emitted by a combat action.
/// Damage, displacement and interruption are independent payloads so a
/// nonlethal push can carry zero damage without becoming a special-case API.
/// </summary>
[Serializable]
public struct CombatHit
{
    [SerializeField] private CombatAttackId attackId;
    [SerializeField] private CombatActionKind actionKind;
    [SerializeField] private GameObject source;
    [SerializeField] private DamageFaction sourceFaction;
    [SerializeField] private DamageAttribution attribution;
    [SerializeField] private Vector2 hitPoint;
    [SerializeField] private Vector2 direction;
    [Min(0f)]
    [SerializeField] private float damage;
    [SerializeField] private CombatDisplacementRequest displacement;
    [SerializeField] private CombatReactionRequest reaction;
    [SerializeField] private bool countsTowardKnockbackDecay;
    [SerializeField] private bool triggersPursuitRecovery;

    public CombatAttackId AttackId => attackId;
    public CombatActionKind ActionKind => actionKind;
    public GameObject Source => source;
    public DamageFaction SourceFaction => sourceFaction;
    public DamageAttribution Attribution => attribution;
    public Vector2 HitPoint => hitPoint;
    public Vector2 Direction => direction;
    public float Damage => damage;
    public CombatDisplacementRequest Displacement => displacement;
    public CombatReactionRequest Reaction => reaction;
    public bool CountsTowardKnockbackDecay =>
        countsTowardKnockbackDecay;

    public bool TriggersPursuitRecovery =>
        triggersPursuitRecovery;

    public bool HasDamage => damage > 0f;
    public bool HasDisplacement => displacement.IsValid;
    public bool HasReaction => reaction.IsValid;

    public CombatHit(
        CombatAttackId newAttackId,
        CombatActionKind newActionKind,
        GameObject newSource,
        DamageFaction newSourceFaction,
        DamageAttribution newAttribution,
        Vector2 newHitPoint,
        Vector2 newDirection,
        float newDamage,
        CombatDisplacementRequest newDisplacement,
        CombatReactionRequest newReaction,
        bool shouldCountTowardKnockbackDecay,
        bool shouldTriggerPursuitRecovery)
    {
        attackId = newAttackId;
        actionKind = newActionKind;
        source = newSource;
        sourceFaction = newSourceFaction;
        attribution = newAttribution;
        hitPoint = newHitPoint;
        direction = newDirection;
        damage = Mathf.Max(0f, newDamage);
        displacement = newDisplacement;
        reaction = newReaction;
        countsTowardKnockbackDecay =
            shouldCountTowardKnockbackDecay;
        triggersPursuitRecovery =
            shouldTriggerPursuitRecovery;
    }

    public DamageInfo ToDamageInfo()
    {
        return new DamageInfo(
            damage,
            source,
            sourceFaction,
            attribution,
            hitPoint,
            direction);
    }
}
