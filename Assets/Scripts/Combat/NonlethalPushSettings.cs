using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Authoring data for the player's multi-input nonlethal push.
/// The player owns action geometry and timing. CB4 moves enemy-specific
/// post-knockback pause and pursuit recovery into each EnemyDefinition.
/// </summary>
[Serializable]
public sealed class NonlethalPushSettings
{
    [SerializeField] private bool enabled = true;

    [Tooltip("Maximum distance from the player origin used to collect push targets.")]
    [Min(0.1f)]
    [SerializeField] private float range = 1.35f;

    [Tooltip("Full fan angle centred on the player's current eight-direction facing.")]
    [Range(1f, 360f)]
    [SerializeField] private float arcAngle = 100f;

    [Tooltip("Requested enemy travel distance before collision clipping.")]
    [Min(0.01f)]
    [SerializeField] private float displacementDistance = 0.9f;

    [Tooltip("Time used by EnemyMotor2D to perform the displacement.")]
    [Min(0.01f)]
    [SerializeField] private float displacementDuration = 0.18f;

    // CB0-CB3 serialized an immediate reaction duration on the player action.
    // CB4 replaces that overlapping timer with a per-enemy pause that begins
    // only after displacement ends. The old value is retained invisibly so
    // existing scenes deserialize without data churn.
    [FormerlySerializedAs("reactionDuration")]
    [SerializeField, HideInInspector]
    private float legacyReactionDuration = 0.22f;

    [Tooltip("Maximum number of enemies affected by one nonlethal-push action.")]
    [Range(1, 32)]
    [SerializeField] private int maximumTargets = 8;

    [Header("CB3 player action timing")]
    [Tooltip(
        "Minimum time from one accepted nonlethal-push action start until the " +
        "next nonlethal-push action may start. Whiffed actions also consume it.")]
    [Min(0.01f)]
    [SerializeField] private float cooldownDuration = 0.55f;

    [Tooltip(
        "Brief recovery window after every accepted nonlethal-push action. " +
        "The player can still steer while movement is scaled.")]
    [Min(0.01f)]
    [SerializeField] private float afterlagDuration = 0.18f;

    [Tooltip(
        "Player movement multiplier during the brief recovery window. " +
        "Zero is a full stop; one preserves full movement speed.")]
    [Range(0f, 1f)]
    [SerializeField] private float afterlagMovementMultiplier = 0.45f;

    // Existing scenes created before CB3 have no serialized values for the
    // three timing fields. This marker lets EnsureValid distinguish that case
    // from an intentional zero movement multiplier.
    [SerializeField, HideInInspector]
    private bool cb3TimingInitialized = true;

    // Existing scenes serialized before CB3.5 still contain the old 120-degree
    // mouse-centred fan. This marker performs a one-time migration to the new
    // 100-degree facing-centred default without touching later authored values.
    [SerializeField, HideInInspector]
    private bool cb35FacingInitialized;

    public bool Enabled => enabled;
    public float Range => range;
    public float ArcAngle => arcAngle;
    public float HalfArcAngle => arcAngle * 0.5f;
    public float DisplacementDistance => displacementDistance;
    public float DisplacementDuration => displacementDuration;
    public int MaximumTargets => maximumTargets;
    public float CooldownDuration => cooldownDuration;
    public float AfterlagDuration => afterlagDuration;
    public float AfterlagMovementMultiplier =>
        afterlagMovementMultiplier;

    public static NonlethalPushSettings CreateDefault()
    {
        return new NonlethalPushSettings();
    }

    public NonlethalPushSettings CreateRuntimeCopy()
    {
        NonlethalPushSettings copy = new NonlethalPushSettings
        {
            enabled = enabled,
            range = range,
            arcAngle = arcAngle,
            displacementDistance = displacementDistance,
            displacementDuration = displacementDuration,
            legacyReactionDuration = legacyReactionDuration,
            maximumTargets = maximumTargets,
            cooldownDuration = cooldownDuration,
            afterlagDuration = afterlagDuration,
            afterlagMovementMultiplier = afterlagMovementMultiplier,
            cb3TimingInitialized = cb3TimingInitialized,
            cb35FacingInitialized = cb35FacingInitialized
        };

        copy.EnsureValid();
        return copy;
    }

    public void EnsureValid()
    {
        bool appearsUninitialized =
            range <= 0f &&
            arcAngle <= 0f &&
            displacementDistance <= 0f &&
            displacementDuration <= 0f &&
            maximumTargets <= 0;

        if (appearsUninitialized)
        {
            enabled = true;
            range = 1.35f;
            arcAngle = 100f;
            displacementDistance = 0.9f;
            displacementDuration = 0.18f;
            legacyReactionDuration = 0.22f;
            maximumTargets = 8;
        }

        if (!cb3TimingInitialized ||
            cooldownDuration <= 0f ||
            afterlagDuration <= 0f)
        {
            cooldownDuration = 0.55f;
            afterlagDuration = 0.18f;
            afterlagMovementMultiplier = 0.45f;
            cb3TimingInitialized = true;
        }

        if (!cb35FacingInitialized)
        {
            arcAngle = 100f;
            cb35FacingInitialized = true;
        }

        range = Mathf.Max(0.1f, range);
        arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
        displacementDistance = Mathf.Max(0.01f, displacementDistance);
        displacementDuration = Mathf.Max(0.01f, displacementDuration);
        legacyReactionDuration = Mathf.Max(0f, legacyReactionDuration);
        maximumTargets = Mathf.Clamp(maximumTargets, 1, 32);
        cooldownDuration = Mathf.Max(0.01f, cooldownDuration);
        afterlagDuration = Mathf.Max(0.01f, afterlagDuration);
        afterlagMovementMultiplier = Mathf.Clamp01(
            afterlagMovementMultiplier);
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
            ? "Nonlethal Push: "
            : ownerName + ": Nonlethal Push: ";

        if (range <= 0f)
        {
            errors.Add(prefix + "Range must be above zero.");
        }

        if (arcAngle <= 0f || arcAngle > 360f)
        {
            errors.Add(prefix + "Arc Angle must be within 1..360 degrees.");
        }

        if (displacementDistance <= 0f)
        {
            errors.Add(prefix + "Displacement Distance must be above zero.");
        }

        if (displacementDuration <= 0f)
        {
            errors.Add(prefix + "Displacement Duration must be above zero.");
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
    }
}
