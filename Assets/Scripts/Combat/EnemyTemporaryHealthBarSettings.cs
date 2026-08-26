using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared presentation settings for runtime enemy health bars.
/// The settings are authored on EnemySpawner and copied into each spawned bar.
/// </summary>
[Serializable]
public sealed class EnemyTemporaryHealthBarSettings
{
    [Tooltip("Master switch for runtime enemy temporary health bars.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("How long the bar remains fully visible after accepted damage.")]
    [Min(0f)]
    [SerializeField] private float visibleDuration = 1.5f;

    [Tooltip("Fade duration after the fully-visible window. Zero hides instantly.")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 0.25f;

    [Tooltip("Outer background size in world units.")]
    [SerializeField] private Vector2 barSize =
        new Vector2(0.8f, 0.08f);

    [Tooltip("Local-space position above the enemy root.")]
    [SerializeField] private Vector2 worldOffset =
        new Vector2(0f, 0.62f);

    [Tooltip("Inset used to leave a visible background border around the fill.")]
    [Min(0f)]
    [SerializeField] private float borderThickness = 0.012f;

    [SerializeField] private Color backgroundColor =
        new Color(0.03f, 0.03f, 0.04f, 0.82f);

    [SerializeField] private Color fillColor =
        new Color(0.86f, 0.14f, 0.16f, 1f);

    [Tooltip("Added to the enemy visual sorting order. Fill uses one order above background.")]
    [Range(-100, 200)]
    [SerializeField] private int sortingOrderOffset = 20;

    [Tooltip("When enabled, environment or other non-player damage does not reveal the bar.")]
    [SerializeField] private bool onlyPlayerDamage;

    [Tooltip("Hide immediately when healing returns the enemy to full health.")]
    [SerializeField] private bool hideWhenFull = true;

    [Tooltip("Hide immediately when the enemy dies instead of waiting for object destruction.")]
    [SerializeField] private bool hideImmediatelyOnDeath = true;

    public bool Enabled => enabled;
    public float VisibleDuration => Mathf.Max(0f, visibleDuration);
    public float FadeDuration => Mathf.Max(0f, fadeDuration);
    public Vector2 BarSize => new Vector2(
        Mathf.Max(0.02f, barSize.x),
        Mathf.Max(0.01f, barSize.y));

    public Vector2 WorldOffset => worldOffset;
    public float BorderThickness => Mathf.Max(0f, borderThickness);
    public Color BackgroundColor => backgroundColor;
    public Color FillColor => fillColor;
    public int SortingOrderOffset => sortingOrderOffset;
    public bool OnlyPlayerDamage => onlyPlayerDamage;
    public bool HideWhenFull => hideWhenFull;
    public bool HideImmediatelyOnDeath => hideImmediatelyOnDeath;

    public static EnemyTemporaryHealthBarSettings CreateDefault()
    {
        return new EnemyTemporaryHealthBarSettings();
    }

    public void EnsureValid()
    {
        visibleDuration = Mathf.Max(0f, visibleDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        barSize.x = Mathf.Max(0.02f, barSize.x);
        barSize.y = Mathf.Max(0.01f, barSize.y);
        borderThickness = Mathf.Clamp(
            borderThickness,
            0f,
            Mathf.Min(barSize.x, barSize.y) * 0.49f);
        sortingOrderOffset = Mathf.Clamp(
            sortingOrderOffset,
            -100,
            200);
    }

    public void CollectValidationErrors(
        List<string> errors,
        string ownerName)
    {
        if (errors == null)
        {
            return;
        }

        string safeOwner = string.IsNullOrEmpty(ownerName)
            ? "Enemy health bar settings"
            : ownerName;

        if (barSize.x <= 0f || barSize.y <= 0f)
        {
            errors.Add(safeOwner + ": health bar size must be positive.");
        }

        if (visibleDuration < 0f || fadeDuration < 0f)
        {
            errors.Add(safeOwner + ": health bar timing cannot be negative.");
        }

        if (borderThickness < 0f)
        {
            errors.Add(safeOwner + ": health bar border thickness cannot be negative.");
        }
    }
}
