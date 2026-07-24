using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One repeated-push decay layer. Distance and interruption deliberately use
/// separate multipliers because spatial control and action denial are
/// different balance axes.
/// </summary>
[Serializable]
public struct KnockbackDecayTier
{
    [Range(0f, 2f)]
    [SerializeField] private float distanceMultiplier;

    [Range(0f, 2f)]
    [SerializeField] private float staggerMultiplier;

    public float DistanceMultiplier => distanceMultiplier;
    public float StaggerMultiplier => staggerMultiplier;

    public static KnockbackDecayTier FullStrength =>
        new KnockbackDecayTier(1f, 1f);

    public KnockbackDecayTier(
        float newDistanceMultiplier,
        float newStaggerMultiplier)
    {
        distanceMultiplier = Mathf.Clamp(
            newDistanceMultiplier,
            0f,
            2f);

        staggerMultiplier = Mathf.Clamp(
            newStaggerMultiplier,
            0f,
            2f);
    }

    public void Validate()
    {
        distanceMultiplier = Mathf.Clamp(
            distanceMultiplier,
            0f,
            2f);

        staggerMultiplier = Mathf.Clamp(
            staggerMultiplier,
            0f,
            2f);
    }
}

/// <summary>
/// Per-enemy repeated-knockback contract.
/// The first accepted push always uses FullStrength. The array contains at
/// least three later decay tiers and may be extended for special enemies.
/// </summary>
[Serializable]
public sealed class KnockbackResistanceSettings
{
    public const int MinimumDecayTierCount = 3;

    [Tooltip(
        "A new push inside this window advances the repeated-hit tier.")]
    [Min(0.05f)]
    [SerializeField] private float decayBuildWindow = 0.9f;

    [Tooltip(
        "Time without another push before tier recovery begins.")]
    [Min(0f)]
    [SerializeField] private float recoveryDelay = 1.5f;

    [Tooltip(
        "After recovery begins, one tier is removed at this interval.")]
    [Min(0.05f)]
    [SerializeField] private float recoveryStepInterval = 0.6f;

    [Tooltip(
        "Repeated-hit tiers after the first full-strength push. " +
        "At least three are always retained.")]
    [SerializeField] private KnockbackDecayTier[] decayTiers =
    {
        new KnockbackDecayTier(0.75f, 0.60f),
        new KnockbackDecayTier(0.50f, 0.25f),
        new KnockbackDecayTier(0.30f, 0.00f)
    };

    public float DecayBuildWindow => decayBuildWindow;
    public float RecoveryDelay => recoveryDelay;
    public float RecoveryStepInterval => recoveryStepInterval;
    public int DecayTierCount =>
        decayTiers != null ? decayTiers.Length : 0;

    public static KnockbackResistanceSettings CreateDefault()
    {
        return new KnockbackResistanceSettings();
    }

    /// <summary>
    /// consecutiveAcceptedPushCount is one-based. One returns full strength;
    /// two returns the first configured decay tier. Counts beyond the array
    /// continue using the final tier.
    /// </summary>
    public KnockbackDecayTier GetTierForPushCount(
        int consecutiveAcceptedPushCount)
    {
        EnsureValid();

        if (consecutiveAcceptedPushCount <= 1)
        {
            return KnockbackDecayTier.FullStrength;
        }

        int index = Mathf.Clamp(
            consecutiveAcceptedPushCount - 2,
            0,
            decayTiers.Length - 1);

        return decayTiers[index];
    }

    public KnockbackDecayTier GetDecayTier(int index)
    {
        EnsureValid();

        int safeIndex = Mathf.Clamp(
            index,
            0,
            decayTiers.Length - 1);

        return decayTiers[safeIndex];
    }

    public void EnsureValid()
    {
        decayBuildWindow = Mathf.Max(
            0.05f,
            decayBuildWindow);

        recoveryDelay = Mathf.Max(0f, recoveryDelay);
        recoveryStepInterval = Mathf.Max(
            0.05f,
            recoveryStepInterval);

        if (decayTiers == null)
        {
            decayTiers = new KnockbackDecayTier[0];
        }

        if (decayTiers.Length < MinimumDecayTierCount)
        {
            KnockbackDecayTier[] expanded =
                new KnockbackDecayTier[MinimumDecayTierCount];

            for (int i = 0; i < expanded.Length; i++)
            {
                if (i < decayTiers.Length)
                {
                    expanded[i] = decayTiers[i];
                }
                else
                {
                    expanded[i] = GetDefaultTier(i);
                }
            }

            decayTiers = expanded;
        }

        for (int i = 0; i < decayTiers.Length; i++)
        {
            KnockbackDecayTier tier = decayTiers[i];
            tier.Validate();
            decayTiers[i] = tier;
        }
    }

    public void CollectValidationErrors(
        List<string> errors,
        string ownerName)
    {
        if (errors == null)
        {
            return;
        }

        EnsureValid();

        string safeOwner = string.IsNullOrWhiteSpace(ownerName)
            ? "EnemyDefinition"
            : ownerName;

        if (decayTiers.Length < MinimumDecayTierCount)
        {
            errors.Add(
                safeOwner +
                ": Knockback Resistance requires at least " +
                MinimumDecayTierCount + " decay tiers.");
        }
    }

    private static KnockbackDecayTier GetDefaultTier(int index)
    {
        switch (index)
        {
            case 0:
                return new KnockbackDecayTier(0.75f, 0.60f);

            case 1:
                return new KnockbackDecayTier(0.50f, 0.25f);

            default:
                return new KnockbackDecayTier(0.30f, 0.00f);
        }
    }
}
