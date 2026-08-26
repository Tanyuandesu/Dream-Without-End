using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves competition between the player's nonlethal push and direct attack.
/// The two actions retain independent cooldowns. CB8 only governs same-frame
/// dual input and whether one action may begin during the other action's
/// afterlag window.
/// </summary>
public enum SimultaneousCombatActionPolicy
{
    PreferNonlethalPush = 0,
    PreferDirectAttack = 1,
    RejectBoth = 2
}

public enum CrossActionRecoveryPolicy
{
    BlockNewAction = 0,
    CancelCurrentRecovery = 1
}

[Serializable]
public sealed class CombatActionArbitrationSettings
{
    [SerializeField] private bool enabled = true;

    [Tooltip(
        "Resolution when push and direct-attack inputs begin in the same " +
        "rendered frame. Nonlethal priority is the safe default for accidental " +
        "dual input and no-kill play.")]
    [SerializeField]
    private SimultaneousCombatActionPolicy simultaneousInputPolicy =
        SimultaneousCombatActionPolicy.PreferNonlethalPush;

    [Tooltip(
        "What happens when a nonlethal push is requested while the direct " +
        "attack is still in its afterlag window. Cooldowns remain independent.")]
    [SerializeField]
    private CrossActionRecoveryPolicy pushDuringDirectAttackRecovery =
        CrossActionRecoveryPolicy.BlockNewAction;

    [Tooltip(
        "What happens when a direct attack is requested while the nonlethal " +
        "push is still in its afterlag window. Cooldowns remain independent.")]
    [SerializeField]
    private CrossActionRecoveryPolicy directAttackDuringPushRecovery =
        CrossActionRecoveryPolicy.BlockNewAction;

    public bool Enabled => enabled;
    public SimultaneousCombatActionPolicy SimultaneousInputPolicy =>
        simultaneousInputPolicy;
    public CrossActionRecoveryPolicy PushDuringDirectAttackRecovery =>
        pushDuringDirectAttackRecovery;
    public CrossActionRecoveryPolicy DirectAttackDuringPushRecovery =>
        directAttackDuringPushRecovery;

    public static CombatActionArbitrationSettings CreateDefault()
    {
        return new CombatActionArbitrationSettings();
    }

    public CombatActionArbitrationSettings CreateRuntimeCopy()
    {
        CombatActionArbitrationSettings copy =
            new CombatActionArbitrationSettings
            {
                enabled = enabled,
                simultaneousInputPolicy = simultaneousInputPolicy,
                pushDuringDirectAttackRecovery =
                    pushDuringDirectAttackRecovery,
                directAttackDuringPushRecovery =
                    directAttackDuringPushRecovery
            };

        copy.EnsureValid();
        return copy;
    }

    public void EnsureValid()
    {
        if (!Enum.IsDefined(
                typeof(SimultaneousCombatActionPolicy),
                simultaneousInputPolicy))
        {
            simultaneousInputPolicy =
                SimultaneousCombatActionPolicy.PreferNonlethalPush;
        }

        if (!Enum.IsDefined(
                typeof(CrossActionRecoveryPolicy),
                pushDuringDirectAttackRecovery))
        {
            pushDuringDirectAttackRecovery =
                CrossActionRecoveryPolicy.BlockNewAction;
        }

        if (!Enum.IsDefined(
                typeof(CrossActionRecoveryPolicy),
                directAttackDuringPushRecovery))
        {
            directAttackDuringPushRecovery =
                CrossActionRecoveryPolicy.BlockNewAction;
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

        string prefix = string.IsNullOrWhiteSpace(ownerName)
            ? "Action Arbitration: "
            : ownerName + ": Action Arbitration: ";

        if (!Enum.IsDefined(
                typeof(SimultaneousCombatActionPolicy),
                simultaneousInputPolicy))
        {
            errors.Add(prefix + "Simultaneous Input Policy is invalid.");
        }

        if (!Enum.IsDefined(
                typeof(CrossActionRecoveryPolicy),
                pushDuringDirectAttackRecovery))
        {
            errors.Add(
                prefix +
                "Push During Direct-Attack Recovery policy is invalid.");
        }

        if (!Enum.IsDefined(
                typeof(CrossActionRecoveryPolicy),
                directAttackDuringPushRecovery))
        {
            errors.Add(
                prefix +
                "Direct Attack During Push Recovery policy is invalid.");
        }
    }
}
