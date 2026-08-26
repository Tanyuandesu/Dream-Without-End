using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CB9.5 validates runtime enemy temporary health bars without changing combat.
/// </summary>
public static class CombatCB95TemporaryHealthBarAudit
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Combat/Run CB9.5 Temporary Health Bar Audit";

    [MenuItem(MenuPath)]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> observations = new List<string>();

        EnemySpawner spawner = FindRuntimeSpawner();
        EnemyTemporaryHealthBar[] bars =
            Object.FindObjectsByType<EnemyTemporaryHealthBar>(
                FindObjectsSortMode.None);
        EnemyCombatReceiver[] receivers =
            Object.FindObjectsByType<EnemyCombatReceiver>(
                FindObjectsSortMode.None);

        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[Combat/CB9.5] Temporary Health Bar Audit\n" +
                "INCOMPLETE: Enter Play Mode, wait for enemies to spawn, " +
                "damage one enemy twice, then wait for the bar to fade.");
            return;
        }

        if (spawner == null)
        {
            errors.Add("No active runtime EnemySpawner was found.");
        }

        EnemyTemporaryHealthBarSettings settings =
            spawner != null
                ? spawner.TemporaryHealthBarSettings
                : null;

        if (settings == null)
        {
            errors.Add("EnemySpawner temporary health bar settings are missing.");
        }
        else
        {
            settings.CollectValidationErrors(
                errors,
                spawner.gameObject.name);

            if (!settings.Enabled)
            {
                errors.Add(
                    "EnemySpawner temporary health bar master switch is disabled.");
            }
        }

        ValidateRuntimeBars(
            bars,
            receivers,
            settings,
            errors);

        CollectObservations(
            bars,
            observations);

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB9.5] Temporary Health Bar Audit");
        report.AppendLine(
            "Runtime Enemies=" + receivers.Length +
            " | Runtime Bars=" + bars.Length);

        if (settings != null)
        {
            report.AppendLine(
                "Settings: Enabled=" + settings.Enabled +
                " | Visible=" + settings.VisibleDuration.ToString("0.##") + "s" +
                " | Fade=" + settings.FadeDuration.ToString("0.##") + "s" +
                " | Size=" + settings.BarSize +
                " | Offset=" + settings.WorldOffset +
                " | OnlyPlayer=" + settings.OnlyPlayerDamage +
                " | HideAtFull=" + settings.HideWhenFull);
        }

        int visibleCount = 0;
        int colliderCount = 0;

        for (int i = 0; i < bars.Length; i++)
        {
            EnemyTemporaryHealthBar bar = bars[i];

            if (bar != null && bar.IsVisible)
            {
                visibleCount++;
            }

            if (bar != null && bar.BarRoot != null)
            {
                colliderCount +=
                    bar.BarRoot.GetComponentsInChildren<Collider2D>(true).Length;
            }
        }

        report.AppendLine(
            "Runtime: Visible=" + visibleCount +
            " | Health-Bar Colliders=" + colliderCount);

        report.AppendLine(
            "Diagnostics: Created=" +
            EnemyTemporaryHealthBarDiagnostics.CreatedCount +
            " | Damage Triggers=" +
            EnemyTemporaryHealthBarDiagnostics.DamageTriggerCount +
            " | Player Triggers=" +
            EnemyTemporaryHealthBarDiagnostics.PlayerDamageTriggerCount +
            " | Distinct Bars=" +
            EnemyTemporaryHealthBarDiagnostics.DistinctTriggeredBarCount);

        report.AppendLine(
            "Visibility: Shows=" +
            EnemyTemporaryHealthBarDiagnostics.ShowStartCount +
            " | Timer Refreshes=" +
            EnemyTemporaryHealthBarDiagnostics.TimerRefreshCount +
            " | Fade Starts=" +
            EnemyTemporaryHealthBarDiagnostics.FadeStartCount +
            " | Fade Hides=" +
            EnemyTemporaryHealthBarDiagnostics.FadeCompletedHideCount +
            " | Death Hides=" +
            EnemyTemporaryHealthBarDiagnostics.DeathHideCount +
            " | Full Hides=" +
            EnemyTemporaryHealthBarDiagnostics.FullHealthHideCount);

        report.AppendLine(
            "Lowest Displayed Health=" +
            EnemyTemporaryHealthBarDiagnostics.LowestDisplayedPercent + "%" +
            " | Rejected Sources=" +
            EnemyTemporaryHealthBarDiagnostics.RejectedSourceCount);

        report.AppendLine(
            "Combat Isolation: Push Hits=" +
            CombatSystemDiagnostics.NonlethalPushHitCount +
            " | Push Damage Violations=" +
            CombatSystemDiagnostics.NonlethalAcceptedDamageViolationCount);

        if (errors.Count > 0)
        {
            report.AppendLine("Errors:");

            for (int i = 0; i < errors.Count; i++)
            {
                report.AppendLine("- " + errors[i]);
            }

            report.AppendLine(
                "FAIL: CB9.5 found a health-bar configuration or runtime invariant violation.");
            Debug.LogError(report.ToString());
            return;
        }

        if (observations.Count > 0)
        {
            report.AppendLine("Observation still required:");

            for (int i = 0; i < observations.Count; i++)
            {
                report.AppendLine("- " + observations[i]);
            }

            report.AppendLine(
                "INCOMPLETE: CB9.5 wiring is valid, but the complete show, " +
                "refresh and fade scenario has not yet been observed.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB9.5 runtime enemies own independent collider-free world-space " +
            "health bars that appear only after accepted damage, update from Health, " +
            "refresh their timer on repeated damage, fade away and leave combat, AI, " +
            "physics and ending records untouched.");
        Debug.Log(report.ToString());
    }

    private static EnemySpawner FindRuntimeSpawner()
    {
        EnemySpawner[] spawners =
            Object.FindObjectsByType<EnemySpawner>(
                FindObjectsSortMode.None);

        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null &&
                spawners[i].isActiveAndEnabled)
            {
                return spawners[i];
            }
        }

        return null;
    }

    private static void ValidateRuntimeBars(
        EnemyTemporaryHealthBar[] bars,
        EnemyCombatReceiver[] receivers,
        EnemyTemporaryHealthBarSettings settings,
        List<string> errors)
    {
        if (receivers.Length == 0)
        {
            errors.Add(
                "No active runtime enemy is available. Enter a floor with enemies.");
            return;
        }

        int expectedBars = 0;

        for (int i = 0; i < receivers.Length; i++)
        {
            EnemyCombatReceiver receiver = receivers[i];

            if (receiver == null)
            {
                continue;
            }

            EnemyRuntimeIdentity identity =
                receiver.GetComponent<EnemyRuntimeIdentity>();
            EnemyDefinition definition = identity != null
                ? identity.Definition
                : null;

            bool expected = settings != null &&
                settings.Enabled &&
                (definition == null ||
                 definition.TemporaryHealthBarEnabled);

            EnemyTemporaryHealthBar bar =
                receiver.GetComponent<EnemyTemporaryHealthBar>();

            if (!expected)
            {
                if (bar != null && bar.IsConfigured)
                {
                    errors.Add(
                        receiver.gameObject.name +
                        ": health bar is configured despite its per-enemy switch being disabled.");
                }

                continue;
            }

            expectedBars++;

            if (bar == null)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": expected EnemyTemporaryHealthBar is missing.");
                continue;
            }

            if (!bar.IsConfigured)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": EnemyTemporaryHealthBar exists but is not configured.");
            }

            Health health = receiver.GetComponent<Health>();

            if (bar.Health != health)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": health bar does not reference the enemy's Health component.");
            }

            if (bar.BarRoot == null ||
                bar.BackgroundRenderer == null ||
                bar.FillRenderer == null)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": health bar visual hierarchy is incomplete.");
            }

            if (bar.BarRoot != null &&
                bar.BarRoot.GetComponentsInChildren<Collider2D>(true).Length > 0)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": health bar hierarchy contains Collider2D components.");
            }

            if (health != null &&
                Mathf.Abs(
                    bar.NormalizedHealth - health.NormalizedHealth) > 0.01f)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": health bar fill is not synchronized with Health.NormalizedHealth.");
            }
        }

        if (bars.Length != expectedBars)
        {
            errors.Add(
                "Runtime health bar count " + bars.Length +
                " does not match enabled live enemy count " + expectedBars + ".");
        }
    }

    private static void CollectObservations(
        EnemyTemporaryHealthBar[] bars,
        List<string> observations)
    {
        if (EnemyTemporaryHealthBarDiagnostics.DamageTriggerCount <= 0 ||
            EnemyTemporaryHealthBarDiagnostics.ShowStartCount <= 0)
        {
            observations.Add(
                "Damage a living enemy once with right mouse, K or X and confirm its bar appears.");
        }

        if (EnemyTemporaryHealthBarDiagnostics.TimerRefreshCount <= 0)
        {
            observations.Add(
                "Damage the same living enemy again before the visible timer expires to observe timer refresh.");
        }

        if (EnemyTemporaryHealthBarDiagnostics.FadeStartCount <= 0 ||
            EnemyTemporaryHealthBarDiagnostics.FadeCompletedHideCount <= 0)
        {
            observations.Add(
                "Leave the damaged enemy alive and wait for visible duration plus fade duration before auditing.");
        }

        if (EnemyTemporaryHealthBarDiagnostics.LowestDisplayedPercent >= 100)
        {
            observations.Add(
                "No reduced health percentage has been recorded yet.");
        }

        if (CombatSystemDiagnostics.NonlethalPushHitCount <= 0)
        {
            observations.Add(
                "Use nonlethal push once during the run to retain representative zero-damage isolation evidence.");
        }

        if (CombatSystemDiagnostics.NonlethalAcceptedDamageViolationCount > 0)
        {
            observations.Add(
                "Nonlethal push accepted damage unexpectedly; rerun the full combat audit.");
        }

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null && bars[i].IsVisible)
            {
                observations.Add(
                    "At least one health bar is still visible. Wait for the fade to complete and audit again.");
                break;
            }
        }
    }
}
