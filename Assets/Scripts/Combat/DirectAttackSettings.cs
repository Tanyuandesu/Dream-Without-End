using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring data for the player's facing-based damaging attack.
/// Damage geometry, player timing and CB7's weak enemy response stay
/// independent from the nonlethal push and its resistance ladder.
/// </summary>
[Serializable]
public sealed class DirectAttackSettings
{
    [SerializeField] private bool enabled = true;

    [Tooltip("Damage applied to every accepted target in one direct attack.")]
    [Min(0.01f)]
    [SerializeField] private float damage = 5f;

    [Tooltip("Maximum distance from the player origin used to collect damage targets.")]
    [Min(0.1f)]
    [SerializeField] private float range = 1.15f;

    [Tooltip("Full fan angle centred on the player's current eight-direction facing.")]
    [Range(1f, 360f)]
    [SerializeField] private float arcAngle = 90f;

    [Tooltip("Maximum number of enemies damaged by one direct-attack action.")]
    [Range(1, 32)]
    [SerializeField] private int maximumTargets = 4;

    [Header("Player action timing")]
    [Tooltip(
        "Minimum time from one accepted direct-attack action start until the " +
        "next direct attack may start. Whiffed actions also consume it.")]
    [Min(0.01f)]
    [SerializeField] private float cooldownDuration = 0.42f;

    [Tooltip(
        "Brief recovery window after every accepted direct-attack action. " +
        "Cross-action arbitration is intentionally deferred to CB8.")]
    [Min(0.01f)]
    [SerializeField] private float afterlagDuration = 0.16f;

    [Tooltip(
        "Player movement multiplier during direct-attack recovery. " +
        "Zero is a full stop; one preserves full movement speed.")]
    [Range(0f, 1f)]
    [SerializeField] private float afterlagMovementMultiplier = 0.6f;

    [Header("Weak enemy hit response")]
    [Tooltip(
        "Base collision-safe nudge distance carried by a damaging hit. " +
        "Each EnemyDefinition scales it independently; zero disables movement.")]
    [Min(0f)]
    [SerializeField] private float weakDisplacementDistance = 0.12f;

    [Tooltip(
        "Time used to travel the base weak displacement. Short values feel " +
        "like impact rather than a second nonlethal push.")]
    [Min(0.01f)]
    [SerializeField] private float weakDisplacementDuration = 0.06f;

    [Tooltip(
        "Base post-displacement Hit pause. Each EnemyDefinition scales it " +
        "independently; zero disables the reaction pause.")]
    [Min(0f)]
    [SerializeField] private float weakHitPauseDuration = 0.08f;

    [SerializeField, HideInInspector]
    private bool cb7WeakResponseInitialized;

    public bool Enabled => enabled;
    public float Damage => damage;
    public float Range => range;
    public float ArcAngle => arcAngle;
    public float HalfArcAngle => arcAngle * 0.5f;
    public int MaximumTargets => maximumTargets;
    public float CooldownDuration => cooldownDuration;
    public float AfterlagDuration => afterlagDuration;
    public float AfterlagMovementMultiplier =>
        afterlagMovementMultiplier;
    public float WeakDisplacementDistance =>
        weakDisplacementDistance;
    public float WeakDisplacementDuration =>
        weakDisplacementDuration;
    public float WeakHitPauseDuration =>
        weakHitPauseDuration;

    public static DirectAttackSettings CreateDefault()
    {
        return new DirectAttackSettings();
    }

    public DirectAttackSettings CreateRuntimeCopy()
    {
        DirectAttackSettings copy = new DirectAttackSettings
        {
            enabled = enabled,
            damage = damage,
            range = range,
            arcAngle = arcAngle,
            maximumTargets = maximumTargets,
            cooldownDuration = cooldownDuration,
            afterlagDuration = afterlagDuration,
            afterlagMovementMultiplier = afterlagMovementMultiplier,
            weakDisplacementDistance = weakDisplacementDistance,
            weakDisplacementDuration = weakDisplacementDuration,
            weakHitPauseDuration = weakHitPauseDuration,
            cb7WeakResponseInitialized = cb7WeakResponseInitialized
        };

        copy.EnsureValid();
        return copy;
    }

    public void EnsureValid()
    {
        bool appearsUninitialized =
            damage <= 0f &&
            range <= 0f &&
            arcAngle <= 0f &&
            maximumTargets <= 0 &&
            cooldownDuration <= 0f &&
            afterlagDuration <= 0f;

        if (appearsUninitialized)
        {
            enabled = true;
            damage = 5f;
            range = 1.15f;
            arcAngle = 90f;
            maximumTargets = 4;
            cooldownDuration = 0.42f;
            afterlagDuration = 0.16f;
            afterlagMovementMultiplier = 0.6f;
            weakDisplacementDistance = 0.12f;
            weakDisplacementDuration = 0.06f;
            weakHitPauseDuration = 0.08f;
        }

        EnsureWeakResponseInitialized();

        damage = Mathf.Max(0.01f, damage);
        range = Mathf.Max(0.1f, range);
        arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
        maximumTargets = Mathf.Clamp(maximumTargets, 1, 32);
        cooldownDuration = Mathf.Max(0.01f, cooldownDuration);
        afterlagDuration = Mathf.Max(0.01f, afterlagDuration);
        afterlagMovementMultiplier = Mathf.Clamp01(
            afterlagMovementMultiplier);
        weakDisplacementDistance = Mathf.Max(
            0f,
            weakDisplacementDistance);
        weakDisplacementDuration = Mathf.Max(
            0.01f,
            weakDisplacementDuration);
        weakHitPauseDuration = Mathf.Max(
            0f,
            weakHitPauseDuration);
    }

    private void EnsureWeakResponseInitialized()
    {
        if (cb7WeakResponseInitialized)
        {
            return;
        }

        if (weakDisplacementDistance <= 0f &&
            weakHitPauseDuration <= 0f)
        {
            weakDisplacementDistance = 0.12f;
            weakDisplacementDuration = 0.06f;
            weakHitPauseDuration = 0.08f;
        }

        cb7WeakResponseInitialized = true;
    }

    public void CollectValidationErrors(
        List<string> errors,
        string ownerName)
    {
        if (errors == null)
        {
            return;
        }

        string prefix = string.IsNullOrWhiteSpace(ownerName)
            ? "Direct Attack: "
            : ownerName + ": Direct Attack: ";

        if (damage <= 0f)
        {
            errors.Add(prefix + "Damage must be above zero.");
        }

        if (range <= 0f)
        {
            errors.Add(prefix + "Range must be above zero.");
        }

        if (arcAngle <= 0f || arcAngle > 360f)
        {
            errors.Add(prefix + "Arc Angle must be within 1..360 degrees.");
        }

        if (maximumTargets < 1)
        {
            errors.Add(prefix + "Maximum Targets must be at least one.");
        }

        if (cooldownDuration <= 0f)
        {
            errors.Add(prefix + "Cooldown Duration must be above zero.");
        }

        if (afterlagDuration <= 0f)
        {
            errors.Add(prefix + "Afterlag Duration must be above zero.");
        }

        if (afterlagMovementMultiplier < 0f ||
            afterlagMovementMultiplier > 1f)
        {
            errors.Add(
                prefix +
                "Afterlag Movement Multiplier must be within 0..1.");
        }

        if (weakDisplacementDistance < 0f)
        {
            errors.Add(
                prefix +
                "Weak Displacement Distance cannot be negative.");
        }

        if (weakDisplacementDuration <= 0f)
        {
            errors.Add(
                prefix +
                "Weak Displacement Duration must be above zero.");
        }

        if (weakHitPauseDuration < 0f)
        {
            errors.Add(
                prefix +
                "Weak Hit Pause Duration cannot be negative.");
        }
    }
}
