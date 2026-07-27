using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configurable legacy-input bindings for player combat actions.
/// CB4.5 routes mouse and keyboard inputs into the same action methods.
/// Direct-attack inputs are captured now but intentionally remain reserved
/// until the damage-attack phase is implemented.
/// </summary>
[Serializable]
public sealed class PlayerCombatInputBindings
{
    [Header("Nonlethal push")]
    [Tooltip("Allow the left mouse button to trigger the nonlethal push.")]
    [SerializeField] private bool enableMouseNonlethalPush = true;

    [Tooltip("Primary keyboard binding used with WASD movement.")]
    [SerializeField] private KeyCode nonlethalPushPrimaryKey = KeyCode.J;

    [Tooltip("Secondary keyboard binding used with arrow-key movement.")]
    [SerializeField] private KeyCode nonlethalPushSecondaryKey = KeyCode.Z;

    [Header("Direct attack (reserved until damage phase)")]
    [Tooltip("Capture the right mouse button as the future direct attack input.")]
    [SerializeField] private bool enableMouseDirectAttack = true;

    [Tooltip("Primary future direct-attack key used with WASD movement.")]
    [SerializeField] private KeyCode directAttackPrimaryKey = KeyCode.K;

    [Tooltip("Secondary future direct-attack key used with arrow-key movement.")]
    [SerializeField] private KeyCode directAttackSecondaryKey = KeyCode.X;

    public bool EnableMouseNonlethalPush => enableMouseNonlethalPush;
    public KeyCode NonlethalPushPrimaryKey => nonlethalPushPrimaryKey;
    public KeyCode NonlethalPushSecondaryKey => nonlethalPushSecondaryKey;
    public bool EnableMouseDirectAttack => enableMouseDirectAttack;
    public KeyCode DirectAttackPrimaryKey => directAttackPrimaryKey;
    public KeyCode DirectAttackSecondaryKey => directAttackSecondaryKey;

    public bool HasAnyNonlethalPushBinding =>
        enableMouseNonlethalPush ||
        nonlethalPushPrimaryKey != KeyCode.None ||
        nonlethalPushSecondaryKey != KeyCode.None;

    public bool HasAnyDirectAttackBinding =>
        enableMouseDirectAttack ||
        directAttackPrimaryKey != KeyCode.None ||
        directAttackSecondaryKey != KeyCode.None;

    public bool HasAnyEnabledBinding =>
        HasAnyNonlethalPushBinding || HasAnyDirectAttackBinding;

    public static PlayerCombatInputBindings CreateDefault()
    {
        return new PlayerCombatInputBindings();
    }

    public PlayerCombatInputBindings CreateRuntimeCopy()
    {
        PlayerCombatInputBindings copy = new PlayerCombatInputBindings
        {
            enableMouseNonlethalPush = enableMouseNonlethalPush,
            nonlethalPushPrimaryKey = nonlethalPushPrimaryKey,
            nonlethalPushSecondaryKey = nonlethalPushSecondaryKey,
            enableMouseDirectAttack = enableMouseDirectAttack,
            directAttackPrimaryKey = directAttackPrimaryKey,
            directAttackSecondaryKey = directAttackSecondaryKey
        };

        copy.EnsureValid();
        return copy;
    }

    public void EnsureValid()
    {
        // KeyCode.None is intentionally valid and represents an unbound slot.
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
            ? "Combat Input Bindings: "
            : ownerName + ": Combat Input Bindings: ";

        if (!HasAnyNonlethalPushBinding)
        {
            errors.Add(
                prefix +
                "Nonlethal Push has no enabled mouse or keyboard binding.");
        }

        ValidateCrossActionConflict(
            errors,
            prefix,
            nonlethalPushPrimaryKey,
            "Nonlethal Push Primary Key",
            directAttackPrimaryKey,
            "Direct Attack Primary Key");

        ValidateCrossActionConflict(
            errors,
            prefix,
            nonlethalPushPrimaryKey,
            "Nonlethal Push Primary Key",
            directAttackSecondaryKey,
            "Direct Attack Secondary Key");

        ValidateCrossActionConflict(
            errors,
            prefix,
            nonlethalPushSecondaryKey,
            "Nonlethal Push Secondary Key",
            directAttackPrimaryKey,
            "Direct Attack Primary Key");

        ValidateCrossActionConflict(
            errors,
            prefix,
            nonlethalPushSecondaryKey,
            "Nonlethal Push Secondary Key",
            directAttackSecondaryKey,
            "Direct Attack Secondary Key");
    }

    public void CollectValidationNotes(
        List<string> notes,
        string ownerName)
    {
        if (notes == null)
        {
            return;
        }

        string prefix = string.IsNullOrWhiteSpace(ownerName)
            ? "Combat Input Bindings: "
            : ownerName + ": Combat Input Bindings: ";

        if (nonlethalPushPrimaryKey != KeyCode.None &&
            nonlethalPushPrimaryKey == nonlethalPushSecondaryKey)
        {
            notes.Add(
                prefix +
                "both Nonlethal Push keyboard slots use " +
                nonlethalPushPrimaryKey + ". This is valid but redundant.");
        }

        if (directAttackPrimaryKey != KeyCode.None &&
            directAttackPrimaryKey == directAttackSecondaryKey)
        {
            notes.Add(
                prefix +
                "both Direct Attack keyboard slots use " +
                directAttackPrimaryKey + ". This is valid but redundant.");
        }

        if (!HasAnyDirectAttackBinding)
        {
            notes.Add(
                prefix +
                "Direct Attack has no binding. This does not block CB4.5, " +
                "but no keyboard or mouse input will be ready for the next phase.");
        }
    }

    private static void ValidateCrossActionConflict(
        List<string> errors,
        string prefix,
        KeyCode first,
        string firstName,
        KeyCode second,
        string secondName)
    {
        if (first == KeyCode.None || second == KeyCode.None || first != second)
        {
            return;
        }

        errors.Add(
            prefix + firstName + " and " + secondName +
            " both use " + first +
            ". One physical key must not trigger both combat actions.");
    }
}
