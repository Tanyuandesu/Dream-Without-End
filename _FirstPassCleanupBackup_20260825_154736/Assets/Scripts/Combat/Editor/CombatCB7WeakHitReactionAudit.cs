using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB7's weak direct-attack response. Damage attacks may request a
/// small collision-safe displacement and Hit pause, but must never advance the
/// nonlethal knockback resistance ladder or queue CB4 pursuit recovery.
/// </summary>
public static class CombatCB7WeakHitReactionAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB7 Weak Hit Reaction Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        EnemyDefinition[] definitions =
            Resources.FindObjectsOfTypeAll<EnemyDefinition>();

        int configuredDefinitions = 0;
        float minimumDistanceMultiplier = float.PositiveInfinity;
        float maximumDistanceMultiplier = 0f;
        float minimumPauseMultiplier = float.PositiveInfinity;
        float maximumPauseMultiplier = 0f;

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null || !definition.Id.IsValid)
            {
                continue;
            }

            configuredDefinitions++;
            definition.CollectValidationErrors(errors);
            minimumDistanceMultiplier = Mathf.Min(
                minimumDistanceMultiplier,
                definition.DirectAttackWeakDisplacementMultiplier);
            maximumDistanceMultiplier = Mathf.Max(
                maximumDistanceMultiplier,
                definition.DirectAttackWeakDisplacementMultiplier);
            minimumPauseMultiplier = Mathf.Min(
                minimumPauseMultiplier,
                definition.DirectAttackWeakHitPauseMultiplier);
            maximumPauseMultiplier = Mathf.Max(
                maximumPauseMultiplier,
                definition.DirectAttackWeakHitPauseMultiplier);
        }

        if (configuredDefinitions <= 0)
        {
            errors.Add("No valid EnemyDefinition assets were found.");
        }

        if (float.IsPositiveInfinity(minimumDistanceMultiplier))
        {
            minimumDistanceMultiplier = 0f;
        }

        if (float.IsPositiveInfinity(minimumPauseMultiplier))
        {
            minimumPauseMultiplier = 0f;
        }

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine("[Combat/CB7] Weak Hit Reaction Audit");
            editReport.AppendLine(
                "Enemy Definitions=" + configuredDefinitions +
                " | Distance Multipliers=" +
                minimumDistanceMultiplier.ToString("0.###") +
                ".." + maximumDistanceMultiplier.ToString("0.###") +
                " | Pause Multipliers=" +
                minimumPauseMultiplier.ToString("0.###") +
                ".." + maximumPauseMultiplier.ToString("0.###"));

            ValidateLoadedSpawners(errors, notes);
            AppendNotes(editReport, notes);

            if (errors.Count > 0)
            {
                AppendErrors(editReport, errors);
                editReport.AppendLine(
                    "FAIL: Fix CB7 authoring errors before Play Mode testing.");
                Debug.LogError(editReport.ToString());
                return;
            }

            editReport.AppendLine(
                "PASS: CB7 authoring data is present. Enter Play Mode, damage " +
                "a surviving enemy at least once, wait briefly, then run this " +
                "audit again.");
            Debug.Log(editReport.ToString());
            return;
        }

        int runtimePlayers = 0;
        int runtimeEnemies = 0;
        int successfulDirectActions = 0;
        int acceptedDirectTargets = 0;
        int receiverDirectAttacks = 0;
        int receiverDamageHits = 0;
        float acceptedDamage = 0f;
        int responseResolutions = 0;
        int weakDisplacements = 0;
        int weakReactions = 0;
        int suppressedWeakReactions = 0;
        int payloadViolations = 0;
        int decayIsolationViolations = 0;
        int pursuitIsolationViolations = 0;
        int activeDisplacements = 0;
        int activeReactions = 0;
        float baseDistance = 0f;
        float baseDuration = 0f;
        float basePause = 0f;
        float lowestResolvedDistance = float.PositiveInfinity;
        float highestResolvedDistance = 0f;
        float lowestResolvedPause = float.PositiveInfinity;
        float highestResolvedPause = 0f;
        float lowestAppliedDistanceMultiplier = float.PositiveInfinity;
        float highestAppliedDistanceMultiplier = 0f;
        float lowestAppliedPauseMultiplier = float.PositiveInfinity;
        float highestAppliedPauseMultiplier = 0f;

        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (!IsActiveSceneObject(controller))
            {
                continue;
            }

            runtimePlayers++;
            controller.CollectValidationErrors(errors);
            successfulDirectActions +=
                controller.SuccessfulDirectAttackActionCount;
            acceptedDirectTargets +=
                controller.AcceptedDirectAttackTargetCount;

            DirectAttackSettings settings =
                controller.DirectAttackSettings;

            if (settings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": runtime Direct Attack settings are missing.");
                continue;
            }

            baseDistance = settings.WeakDisplacementDistance;
            baseDuration = settings.WeakDisplacementDuration;
            basePause = settings.WeakHitPauseDuration;

            NonlethalPushSettings push =
                controller.PushSettings;

            if (push != null &&
                settings.WeakDisplacementDistance >=
                    push.DisplacementDistance)
            {
                notes.Add(
                    "Direct-attack weak displacement is not smaller than the " +
                    "nonlethal push distance. This is configurable, but may " +
                    "blur the two actions' roles.");
            }
        }

        EnemyCombatReceiver[] receivers =
            Resources.FindObjectsOfTypeAll<EnemyCombatReceiver>();

        for (int i = 0; i < receivers.Length; i++)
        {
            EnemyCombatReceiver receiver = receivers[i];

            if (!IsActiveSceneObject(receiver))
            {
                continue;
            }

            runtimeEnemies++;
            receiver.CollectValidationErrors(errors);
            receiverDirectAttacks += receiver.AcceptedDirectAttackCount;
            receiverDamageHits += receiver.AcceptedDamageHitCount;
            acceptedDamage += receiver.TotalAcceptedDamage;
            responseResolutions +=
                receiver.DirectAttackResponseResolutionCount;
            weakDisplacements +=
                receiver.AcceptedDirectAttackWeakDisplacementCount;
            weakReactions +=
                receiver.AcceptedDirectAttackWeakReactionCount;
            suppressedWeakReactions +=
                receiver.DirectAttackReactionSuppressedByExistingCount;
            payloadViolations +=
                receiver.DirectAttackPayloadViolationCount;
            decayIsolationViolations +=
                receiver.DirectAttackDecayIsolationViolationCount;
            pursuitIsolationViolations +=
                receiver.DirectAttackPursuitIsolationViolationCount;

            if (receiver.Motor != null &&
                receiver.Motor.IsCombatDisplacementActive)
            {
                activeDisplacements++;
            }

            if (receiver.StateMachine != null &&
                receiver.StateMachine.IsCombatReactionActive)
            {
                activeReactions++;
            }

            if (receiver.DirectAttackResponseResolutionCount > 0)
            {
                lowestResolvedDistance = Mathf.Min(
                    lowestResolvedDistance,
                    receiver.LastDirectAttackResolvedDisplacementDistance);
                highestResolvedDistance = Mathf.Max(
                    highestResolvedDistance,
                    receiver.LastDirectAttackResolvedDisplacementDistance);
                lowestResolvedPause = Mathf.Min(
                    lowestResolvedPause,
                    receiver.LastDirectAttackResolvedPauseDuration);
                highestResolvedPause = Mathf.Max(
                    highestResolvedPause,
                    receiver.LastDirectAttackResolvedPauseDuration);
                lowestAppliedDistanceMultiplier = Mathf.Min(
                    lowestAppliedDistanceMultiplier,
                    receiver.LastDirectAttackDistanceMultiplier);
                highestAppliedDistanceMultiplier = Mathf.Max(
                    highestAppliedDistanceMultiplier,
                    receiver.LastDirectAttackDistanceMultiplier);
                lowestAppliedPauseMultiplier = Mathf.Min(
                    lowestAppliedPauseMultiplier,
                    receiver.LastDirectAttackPauseMultiplier);
                highestAppliedPauseMultiplier = Mathf.Max(
                    highestAppliedPauseMultiplier,
                    receiver.LastDirectAttackPauseMultiplier);
            }
        }

        if (runtimePlayers != 1)
        {
            errors.Add(
                "Expected exactly one active runtime player, found " +
                runtimePlayers + ".");
        }

        if (runtimeEnemies <= 0)
        {
            errors.Add(
                "No active runtime enemy is available. Restart the floor and " +
                "leave at least one enemy alive for CB7 testing.");
        }

        if (payloadViolations > 0)
        {
            errors.Add(
                "A direct attack used a forbidden reaction policy, entered " +
                "nonlethal decay or queued pursuit recovery.");
        }

        if (decayIsolationViolations > 0)
        {
            errors.Add(
                "A direct attack mutated the nonlethal knockback resistance " +
                "level or qualifying-push count.");
        }

        if (pursuitIsolationViolations > 0)
        {
            errors.Add(
                "A direct attack incremented the CB4 pursuit-recovery trigger.");
        }

        if (float.IsPositiveInfinity(lowestResolvedDistance))
        {
            lowestResolvedDistance = 0f;
        }

        if (float.IsPositiveInfinity(lowestResolvedPause))
        {
            lowestResolvedPause = 0f;
        }

        if (float.IsPositiveInfinity(lowestAppliedDistanceMultiplier))
        {
            lowestAppliedDistanceMultiplier = 0f;
        }

        if (float.IsPositiveInfinity(lowestAppliedPauseMultiplier))
        {
            lowestAppliedPauseMultiplier = 0f;
        }

        bool directDamageObserved =
            successfulDirectActions > 0 &&
            acceptedDirectTargets > 0 &&
            receiverDirectAttacks > 0 &&
            receiverDamageHits > 0 &&
            acceptedDamage > 0f;

        bool weakResponseObserved =
            responseResolutions > 0 &&
            (baseDistance <= 0f || weakDisplacements > 0) &&
            (basePause <= 0f || weakReactions > 0);

        bool settled =
            activeDisplacements == 0 &&
            activeReactions == 0;

        if (!directDamageObserved)
        {
            notes.Add(
                "Damage a surviving enemy once with right mouse, K or X.");
        }

        if (!weakResponseObserved)
        {
            notes.Add(
                "Observe one nonlethal direct hit long enough for its weak " +
                "nudge and Hit pause to be accepted.");
        }

        if (!settled)
        {
            notes.Add(
                "A weak displacement or Hit pause is still active. Wait about " +
                "0.2 seconds and run the audit again.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB7] Weak Hit Reaction Audit");
        report.AppendLine(
            "Runtime Players=" + runtimePlayers +
            " | Runtime Enemies=" + runtimeEnemies +
            " | Enemy Definitions=" + configuredDefinitions);
        report.AppendLine(
            "Player Base Response: Distance=" + baseDistance.ToString("0.###") +
            " in " + baseDuration.ToString("0.###") +
            "s | Hit Pause=" + basePause.ToString("0.###") + "s");
        report.AppendLine(
            "Enemy Multipliers: Distance=" +
            minimumDistanceMultiplier.ToString("0.###") +
            ".." + maximumDistanceMultiplier.ToString("0.###") +
            " | Pause=" + minimumPauseMultiplier.ToString("0.###") +
            ".." + maximumPauseMultiplier.ToString("0.###"));
        report.AppendLine(
            "Direct Damage: Successful Actions=" + successfulDirectActions +
            " | Accepted Targets=" + acceptedDirectTargets +
            " | Receiver Attacks=" + receiverDirectAttacks +
            " | Damage Hits=" + receiverDamageHits +
            " | Accepted Damage=" + acceptedDamage.ToString("0.##"));
        report.AppendLine(
            "Weak Response: Resolutions=" + responseResolutions +
            " | Displacements Accepted=" + weakDisplacements +
            " | Hit Reactions Accepted=" + weakReactions +
            " | Reactions Preserved/Suppressed=" +
            suppressedWeakReactions);
        report.AppendLine(
            "Resolved Distance=" + lowestResolvedDistance.ToString("0.###") +
            ".." + highestResolvedDistance.ToString("0.###") +
            " | Resolved Pause=" + lowestResolvedPause.ToString("0.###") +
            ".." + highestResolvedPause.ToString("0.###") + "s");
        report.AppendLine(
            "Applied Multipliers: Distance=" +
            lowestAppliedDistanceMultiplier.ToString("0.###") +
            ".." + highestAppliedDistanceMultiplier.ToString("0.###") +
            " | Pause=" + lowestAppliedPauseMultiplier.ToString("0.###") +
            ".." + highestAppliedPauseMultiplier.ToString("0.###"));
        report.AppendLine(
            "Isolation Violations: Payload=" + payloadViolations +
            " | Decay=" + decayIsolationViolations +
            " | Pursuit=" + pursuitIsolationViolations);
        report.AppendLine(
            "Active Weak Displacements=" + activeDisplacements +
            " | Active Hit Reactions=" + activeReactions);

        AppendNotes(report, notes);

        if (errors.Count > 0)
        {
            AppendErrors(report, errors);
            report.AppendLine(
                "FAIL: CB7 weak-hit response or isolation is invalid.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!directDamageObserved ||
            !weakResponseObserved ||
            !settled)
        {
            report.AppendLine(
                "INCOMPLETE: CB7 wiring is valid, but damage, weak response " +
                "or settled-state observations are not all recorded.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB7 direct attacks apply damage plus a configurable weak " +
            "collision-safe nudge and Hit pause. They do not advance nonlethal " +
            "knockback decay, queue pursuit recovery or steal Rigidbody2D " +
            "ownership from EnemyMotor2D.");
        Debug.Log(report.ToString());
    }

    private static void ValidateLoadedSpawners(
        List<string> errors,
        List<string> notes)
    {
        PlayerSpawner[] spawners =
            Resources.FindObjectsOfTypeAll<PlayerSpawner>();

        for (int i = 0; i < spawners.Length; i++)
        {
            PlayerSpawner spawner = spawners[i];

            if (spawner == null || EditorUtility.IsPersistent(spawner))
            {
                continue;
            }

            DirectAttackSettings settings =
                spawner.DirectAttackSettings;

            if (settings == null)
            {
                errors.Add(
                    spawner.gameObject.name +
                    ": Direct Attack settings are missing.");
                continue;
            }

            settings.EnsureValid();
            settings.CollectValidationErrors(
                errors,
                spawner.gameObject.name);

            if (settings.WeakDisplacementDistance <= 0f &&
                settings.WeakHitPauseDuration <= 0f)
            {
                notes.Add(
                    spawner.gameObject.name +
                    ": both CB7 weak-response channels are disabled.");
            }
        }
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.scene.isLoaded &&
               component.gameObject.activeInHierarchy;
    }

    private static void AppendNotes(
        StringBuilder report,
        List<string> notes)
    {
        if (notes.Count <= 0)
        {
            return;
        }

        report.AppendLine("Notes:");

        for (int i = 0; i < notes.Count; i++)
        {
            report.AppendLine("- " + notes[i]);
        }
    }

    private static void AppendErrors(
        StringBuilder report,
        List<string> errors)
    {
        report.AppendLine("Errors:");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("- " + errors[i]);
        }
    }
}
