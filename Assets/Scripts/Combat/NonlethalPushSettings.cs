using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring data for the player's left-mouse nonlethal push.
/// CB1 intentionally contains no cooldown, stamina cost, decay execution or
/// post-knockback pursuit bonus. Those balance layers are added later without
/// changing the hit/displacement contracts established here.
/// </summary>
[Serializable]
public sealed class NonlethalPushSettings
{
    [SerializeField] private bool enabled = true;

    [Tooltip("Maximum distance from the player origin used to collect push targets.")]
    [Min(0.1f)]
    [SerializeField] private float range = 1.35f;

    [Tooltip("Full fan angle centred on the mouse direction.")]
    [Range(1f, 360f)]
    [SerializeField] private float arcAngle = 120f;

    [Tooltip("Requested enemy travel distance before collision clipping.")]
    [Min(0.01f)]
    [SerializeField] private float displacementDistance = 0.9f;

    [Tooltip("Time used by EnemyMotor2D to perform the displacement.")]
    [Min(0.01f)]
    [SerializeField] private float displacementDuration = 0.18f;

    [Tooltip(
        "Enemy navigation interruption duration. It should not be shorter " +
        "than the displacement duration.")]
    [Min(0f)]
    [SerializeField] private float reactionDuration = 0.22f;

    [Tooltip("Maximum number of enemies affected by one left-click action.")]
    [Range(1, 32)]
    [SerializeField] private int maximumTargets = 8;

    public bool Enabled => enabled;
    public float Range => range;
    public float ArcAngle => arcAngle;
    public float HalfArcAngle => arcAngle * 0.5f;
    public float DisplacementDistance => displacementDistance;
    public float DisplacementDuration => displacementDuration;
    public float ReactionDuration => Mathf.Max(
        reactionDuration,
        displacementDuration);

    public int MaximumTargets => maximumTargets;

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
            reactionDuration = reactionDuration,
            maximumTargets = maximumTargets
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
            reactionDuration <= 0f &&
            maximumTargets <= 0;

        if (appearsUninitialized)
        {
            enabled = true;
            range = 1.35f;
            arcAngle = 120f;
            displacementDistance = 0.9f;
            displacementDuration = 0.18f;
            reactionDuration = 0.22f;
            maximumTargets = 8;
        }

        range = Mathf.Max(0.1f, range);
        arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
        displacementDistance = Mathf.Max(0.01f, displacementDistance);
        displacementDuration = Mathf.Max(0.01f, displacementDuration);
        reactionDuration = Mathf.Max(
            displacementDuration,
            reactionDuration);

        maximumTargets = Mathf.Clamp(maximumTargets, 1, 32);
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

        if (reactionDuration < displacementDuration)
        {
            errors.Add(
                prefix +
                "Reaction Duration must not be shorter than Displacement Duration.");
        }

        if (maximumTargets < 1)
        {
            errors.Add(prefix + "Maximum Targets must be at least one.");
        }
    }
}
