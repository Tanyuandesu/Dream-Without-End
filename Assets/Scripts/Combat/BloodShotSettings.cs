using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring data for the player's HP-consuming ranged blood shot.
/// The shot deliberately combines weaker versions of direct damage and
/// nonlethal control while adding range as its unique advantage.
/// </summary>
[Serializable]
public sealed class BloodShotSettings
{
    [SerializeField] private bool enabled = true;

    [Header("Input")]
    [Tooltip("Hold the direct-attack button for this long to convert B into Blood Shot.")]
    [Min(0.05f)]
    [SerializeField] private float holdThreshold = 0.32f;

    [Header("HP cost")]
    [Min(0f)]
    [SerializeField] private float healthCost = 10f;

    [Tooltip("The shot is rejected if paying its cost would leave the player below this HP.")]
    [Min(0f)]
    [SerializeField] private float minimumRemainingHealth = 1f;

    [Header("Projectile")]
    [Min(0.1f)]
    [SerializeField] private float projectileSpeed = 9f;

    [Min(0.05f)]
    [SerializeField] private float projectileLifetime = 1.4f;

    [Min(0.01f)]
    [SerializeField] private float projectileRadius = 0.12f;

    [Min(0f)]
    [SerializeField] private float spawnOffset = 0.45f;

    [Min(0.02f)]
    [SerializeField] private float projectileVisualSize = 0.24f;

    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Color projectileColor =
        new Color(0.55f, 0.05f, 0.08f, 1f);

    [Header("Hit payload")]
    [Min(0f)]
    [SerializeField] private float damage = 6f;

    [Min(0f)]
    [SerializeField] private float displacementDistance = 0.60f;

    [Min(0.01f)]
    [SerializeField] private float displacementDuration = 0.12f;

    [Min(0f)]
    [SerializeField] private float stunDuration = 0.30f;

    [Header("Player action timing")]
    [Min(0.01f)]
    [SerializeField] private float cooldownDuration = 0.75f;

    [Min(0.01f)]
    [SerializeField] private float afterlagDuration = 0.25f;

    [Range(0f, 1f)]
    [SerializeField] private float afterlagMovementMultiplier = 0.35f;

    public bool Enabled => enabled;
    public float HoldThreshold => holdThreshold;
    public float HealthCost => healthCost;
    public float MinimumRemainingHealth => minimumRemainingHealth;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileRadius => projectileRadius;
    public float SpawnOffset => spawnOffset;
    public float ProjectileVisualSize => projectileVisualSize;
    public Sprite ProjectileSprite => projectileSprite;
    public Color ProjectileColor => projectileColor;
    public float Damage => damage;
    public float DisplacementDistance => displacementDistance;
    public float DisplacementDuration => displacementDuration;
    public float StunDuration => stunDuration;
    public float CooldownDuration => cooldownDuration;
    public float AfterlagDuration => afterlagDuration;
    public float AfterlagMovementMultiplier => afterlagMovementMultiplier;

    public static BloodShotSettings CreateDefault()
    {
        return new BloodShotSettings();
    }

    public BloodShotSettings CreateRuntimeCopy()
    {
        BloodShotSettings copy = new BloodShotSettings
        {
            enabled = enabled,
            holdThreshold = holdThreshold,
            healthCost = healthCost,
            minimumRemainingHealth = minimumRemainingHealth,
            projectileSpeed = projectileSpeed,
            projectileLifetime = projectileLifetime,
            projectileRadius = projectileRadius,
            spawnOffset = spawnOffset,
            projectileVisualSize = projectileVisualSize,
            projectileSprite = projectileSprite,
            projectileColor = projectileColor,
            damage = damage,
            displacementDistance = displacementDistance,
            displacementDuration = displacementDuration,
            stunDuration = stunDuration,
            cooldownDuration = cooldownDuration,
            afterlagDuration = afterlagDuration,
            afterlagMovementMultiplier = afterlagMovementMultiplier
        };

        copy.EnsureValid();
        return copy;
    }

    public void EnsureValid()
    {
        holdThreshold = Mathf.Max(0.05f, holdThreshold);
        healthCost = Mathf.Max(0f, healthCost);
        minimumRemainingHealth = Mathf.Max(0f, minimumRemainingHealth);
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
        projectileRadius = Mathf.Max(0.01f, projectileRadius);
        spawnOffset = Mathf.Max(0f, spawnOffset);
        projectileVisualSize = Mathf.Max(0.02f, projectileVisualSize);
        damage = Mathf.Max(0f, damage);
        displacementDistance = Mathf.Max(0f, displacementDistance);
        displacementDuration = Mathf.Max(0.01f, displacementDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
        cooldownDuration = Mathf.Max(0.01f, cooldownDuration);
        afterlagDuration = Mathf.Max(0.01f, afterlagDuration);
        afterlagMovementMultiplier = Mathf.Clamp01(afterlagMovementMultiplier);
    }

    public void CollectValidationErrors(List<string> errors, string ownerName)
    {
        if (errors == null)
        {
            return;
        }

        string prefix = string.IsNullOrWhiteSpace(ownerName)
            ? "Blood Shot: "
            : ownerName + ": Blood Shot: ";

        if (holdThreshold <= 0f)
        {
            errors.Add(prefix + "Hold Threshold must be above zero.");
        }

        if (projectileSpeed <= 0f || projectileLifetime <= 0f || projectileRadius <= 0f)
        {
            errors.Add(prefix + "Projectile Speed, Lifetime and Radius must be above zero.");
        }

        if (cooldownDuration <= 0f || afterlagDuration <= 0f)
        {
            errors.Add(prefix + "Cooldown and Afterlag must be above zero.");
        }
    }
}
