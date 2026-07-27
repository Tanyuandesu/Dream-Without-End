using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB4's ordered enemy recovery sequence:
/// collision-safe displacement, post-displacement pause, temporary pursuit
/// acceleration, then normal navigation speed. Run in Play Mode after at least
/// one successful push and wait roughly one second before auditing.
/// </summary>
public static class CombatCB4PostKnockbackRecoveryAudit
{
    private const float TimeOrderTolerance = 0.03f;

    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB4 Post-Knockback Recovery Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        EnemyDefinition[] definitions =
            Resources.FindObjectsOfTypeAll<EnemyDefinition>();

        int configuredDefinitions = 0;
        int minimumDecayTiers = int.MaxValue;
        int maximumDecayTiers = 0;
        float minimumPause = float.PositiveInfinity;
        float maximumPause = 0f;
        float minimumBoostMultiplier = float.PositiveInfinity;
        float maximumBoostMultiplier = 0f;
        float minimumBoostDuration = float.PositiveInfinity;
        float maximumBoostDuration = 0f;

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null || !definition.Id.IsValid)
            {
                continue;
            }

            configuredDefinitions++;
            definition.CollectValidationErrors(errors);

            KnockbackResistanceSettings resistance =
                definition.KnockbackResistance;

            if (resistance == null)
            {
                errors.Add(
                    definition.name +
                    ": Knockback Resistance settings are missing.");

                continue;
            }

            resistance.EnsureValid();
            minimumDecayTiers = Mathf.Min(
                minimumDecayTiers,
                resistance.DecayTierCount);
            maximumDecayTiers = Mathf.Max(
                maximumDecayTiers,
                resistance.DecayTierCount);

            minimumPause = Mathf.Min(
                minimumPause,
                definition.PostKnockbackPauseDuration);
            maximumPause = Mathf.Max(
                maximumPause,
                definition.PostKnockbackPauseDuration);
            minimumBoostMultiplier = Mathf.Min(
                minimumBoostMultiplier,
                definition.PostKnockbackPursuitSpeedMultiplier);
            maximumBoostMultiplier = Mathf.Max(
                maximumBoostMultiplier,
                definition.PostKnockbackPursuitSpeedMultiplier);
            minimumBoostDuration = Mathf.Min(
                minimumBoostDuration,
                definition.PostKnockbackPursuitDuration);
            maximumBoostDuration = Mathf.Max(
                maximumBoostDuration,
                definition.PostKnockbackPursuitDuration);
        }

        if (configuredDefinitions == 0)
        {
            errors.Add("No valid EnemyDefinition assets were found.");
        }

        if (minimumDecayTiers == int.MaxValue)
        {
            minimumDecayTiers = 0;
        }

        if (float.IsPositiveInfinity(minimumPause))
        {
            minimumPause = 0f;
        }

        if (float.IsPositiveInfinity(minimumBoostMultiplier))
        {
            minimumBoostMultiplier = 0f;
        }

        if (float.IsPositiveInfinity(minimumBoostDuration))
        {
            minimumBoostDuration = 0f;
        }

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine(
                "[Combat/CB4] Post-Knockback Recovery Audit");
            editReport.AppendLine(
                "Enemy Definitions=" + configuredDefinitions +
                " | Decay Tiers=" + minimumDecayTiers +
                ".." + maximumDecayTiers);
            editReport.AppendLine(
                "Configured Pause=" + minimumPause.ToString("0.###") +
                ".." + maximumPause.ToString("0.###") +
                "s | Boost=" +
                minimumBoostMultiplier.ToString("0.###") +
                ".." + maximumBoostMultiplier.ToString("0.###") +
                "x for " + minimumBoostDuration.ToString("0.###") +
                ".." + maximumBoostDuration.ToString("0.###") + "s");

            if (errors.Count > 0)
            {
                AppendErrors(editReport, errors);
                Debug.LogError(editReport.ToString());
                return;
            }

            editReport.AppendLine(
                "PASS: CB4 configuration is present. Enter Play Mode, " +
                "push an enemy, wait about one second, then run this audit " +
                "again to verify runtime ordering.");
            Debug.Log(editReport.ToString());
            return;
        }

        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();
        EnemyCombatReceiver[] receivers =
            Resources.FindObjectsOfTypeAll<EnemyCombatReceiver>();

        int runtimePlayers = 0;
        int runtimeEnemies = 0;
        int successfulPushActions = 0;
        int acceptedTargets = 0;
        int receiverRecoveryTriggers = 0;
        int displacementStarts = 0;
        int displacementCompleted = 0;
        int displacementClipped = 0;
        int pauseStarts = 0;
        int pauseCompleted = 0;
        int pursuitRequests = 0;
        int pursuitAccepted = 0;
        int pursuitSkipped = 0;
        int boostStarts = 0;
        int boostCompleted = 0;
        int boostCancelled = 0;
        int boostRejected = 0;
        int activeDisplacements = 0;
        int activeReactions = 0;
        int activePauses = 0;
        int activeBoosts = 0;
        int displacementPauseOrderViolations = 0;
        int pauseBoostOrderViolations = 0;
        float lowestObservedPause = float.PositiveInfinity;
        float highestObservedPause = 0f;
        float lowestObservedBoostMultiplier = float.PositiveInfinity;
        float highestObservedBoostMultiplier = 0f;
        float lowestObservedBoostDuration = float.PositiveInfinity;
        float highestObservedBoostDuration = 0f;

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (!IsActiveSceneObject(controller))
            {
                continue;
            }

            runtimePlayers++;
            controller.CollectValidationErrors(errors);
            successfulPushActions +=
                controller.SuccessfulLeftPushActionCount;
            acceptedTargets +=
                controller.AcceptedLeftPushTargetCount;
        }

        for (int i = 0; i < receivers.Length; i++)
        {
            EnemyCombatReceiver receiver = receivers[i];

            if (!IsActiveSceneObject(receiver))
            {
                continue;
            }

            runtimeEnemies++;
            receiver.CollectValidationErrors(errors);
            receiverRecoveryTriggers +=
                receiver.PursuitRecoveryTriggerCount;

            EnemyMotor2D motor = receiver.Motor;
            EnemyStateMachine state = receiver.StateMachine;

            if (motor == null || state == null)
            {
                continue;
            }

            displacementStarts += motor.CombatDisplacementStartCount;
            displacementCompleted += motor.CombatDisplacementCompleteCount;
            displacementClipped += motor.CombatDisplacementBlockedCount;
            pauseStarts += state.PostDisplacementPauseStartCount;
            pauseCompleted += state.PostDisplacementPauseCompleteCount;
            pursuitRequests += state.PursuitRecoveryRequestCount;
            pursuitAccepted += state.PursuitRecoveryAcceptedCount;
            pursuitSkipped += state.PursuitRecoverySkippedCount;
            boostStarts += motor.TimedNavigationSpeedStartCount;
            boostCompleted += motor.TimedNavigationSpeedCompleteCount;
            boostCancelled += motor.TimedNavigationSpeedCancelCount;
            boostRejected += motor.TimedNavigationSpeedRejectCount;

            if (motor.IsCombatDisplacementActive)
            {
                activeDisplacements++;
            }

            if (state.IsCombatReactionActive)
            {
                activeReactions++;
            }

            if (state.IsPostDisplacementPauseActive)
            {
                activePauses++;
            }

            if (motor.IsTimedNavigationSpeedActive)
            {
                activeBoosts++;
            }

            if (state.PostDisplacementPauseStartCount > 0)
            {
                lowestObservedPause = Mathf.Min(
                    lowestObservedPause,
                    state.LastPostDisplacementPauseDuration);
                highestObservedPause = Mathf.Max(
                    highestObservedPause,
                    state.LastPostDisplacementPauseDuration);
            }

            if (motor.TimedNavigationSpeedStartCount > 0)
            {
                lowestObservedBoostMultiplier = Mathf.Min(
                    lowestObservedBoostMultiplier,
                    motor.LastRequestedNavigationSpeedMultiplier);
                highestObservedBoostMultiplier = Mathf.Max(
                    highestObservedBoostMultiplier,
                    motor.LastRequestedNavigationSpeedMultiplier);
                lowestObservedBoostDuration = Mathf.Min(
                    lowestObservedBoostDuration,
                    motor.LastRequestedNavigationSpeedDuration);
                highestObservedBoostDuration = Mathf.Max(
                    highestObservedBoostDuration,
                    motor.LastRequestedNavigationSpeedDuration);
            }

            if (motor.LastCombatDisplacementEndedAt >= 0f &&
                state.LastPostDisplacementPauseStartedAt >= 0f &&
                state.LastPostDisplacementPauseStartedAt +
                    TimeOrderTolerance <
                    motor.LastCombatDisplacementEndedAt)
            {
                displacementPauseOrderViolations++;
                errors.Add(
                    receiver.gameObject.name +
                    ": post-knockback pause started before combat " +
                    "displacement ended.");
            }

            if (state.LastPursuitRecoveryPauseCompletedAt >= 0f &&
                motor.LastTimedNavigationSpeedStartedAt >= 0f &&
                motor.LastTimedNavigationSpeedStartedAt +
                    TimeOrderTolerance <
                    state.LastPursuitRecoveryPauseCompletedAt)
            {
                pauseBoostOrderViolations++;
                errors.Add(
                    receiver.gameObject.name +
                    ": pursuit acceleration started before the " +
                    "post-knockback pause completed.");
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
            errors.Add("No active runtime enemies were found.");
        }

        if (float.IsPositiveInfinity(lowestObservedPause))
        {
            lowestObservedPause = 0f;
        }

        if (float.IsPositiveInfinity(lowestObservedBoostMultiplier))
        {
            lowestObservedBoostMultiplier = 0f;
        }

        if (float.IsPositiveInfinity(lowestObservedBoostDuration))
        {
            lowestObservedBoostDuration = 0f;
        }

        bool pushObserved =
            successfulPushActions > 0 &&
            acceptedTargets > 0 &&
            receiverRecoveryTriggers > 0 &&
            displacementStarts > 0;

        bool pauseObserved =
            pauseStarts > 0 &&
            pauseCompleted > 0;

        bool boostObserved =
            pursuitRequests > 0 &&
            pursuitAccepted > 0 &&
            boostStarts > 0 &&
            boostCompleted > 0;

        bool settled =
            activeDisplacements == 0 &&
            activeReactions == 0 &&
            activePauses == 0 &&
            activeBoosts == 0;

        if (displacementClipped > 0)
        {
            notes.Add(
                "At least one displacement was collision-clipped. CB4 " +
                "still applies the configured post-knockback pause.");
        }

        if (boostCancelled > 0)
        {
            notes.Add(
                "At least one pursuit boost was cancelled by a later hit, " +
                "death or owner reset. This is valid sequence arbitration.");
        }

        if (!settled)
        {
            notes.Add(
                "A displacement, pause or boost is active while auditing. " +
                "Wait about one second and run the audit again.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[Combat/CB4] Post-Knockback Recovery Audit");
        report.AppendLine(
            "Enemy Definitions=" + configuredDefinitions +
            " | Runtime Players=" + runtimePlayers +
            " | Runtime Enemies=" + runtimeEnemies);
        report.AppendLine(
            "Configured Pause=" + minimumPause.ToString("0.###") +
            ".." + maximumPause.ToString("0.###") +
            "s | Boost=" +
            minimumBoostMultiplier.ToString("0.###") +
            ".." + maximumBoostMultiplier.ToString("0.###") +
            "x for " + minimumBoostDuration.ToString("0.###") +
            ".." + maximumBoostDuration.ToString("0.###") + "s");
        report.AppendLine(
            "Successful Push Actions=" + successfulPushActions +
            " | Accepted Targets=" + acceptedTargets +
            " | Receiver Recovery Triggers=" +
            receiverRecoveryTriggers);
        report.AppendLine(
            "Motor Displacements: Starts=" + displacementStarts +
            " | Completed=" + displacementCompleted +
            " | Collision-Clipped=" + displacementClipped);
        report.AppendLine(
            "Post-Pause: Starts=" + pauseStarts +
            " | Completed=" + pauseCompleted +
            " | Last Duration Range=" +
            lowestObservedPause.ToString("0.###") +
            ".." + highestObservedPause.ToString("0.###") + "s");
        report.AppendLine(
            "Pursuit Recovery: Requests=" + pursuitRequests +
            " | Accepted=" + pursuitAccepted +
            " | Skipped=" + pursuitSkipped);
        report.AppendLine(
            "Motor Boosts: Starts=" + boostStarts +
            " | Completed=" + boostCompleted +
            " | Cancelled=" + boostCancelled +
            " | Rejected=" + boostRejected);
        report.AppendLine(
            "Observed Boost=" +
            lowestObservedBoostMultiplier.ToString("0.###") +
            ".." + highestObservedBoostMultiplier.ToString("0.###") +
            "x for " + lowestObservedBoostDuration.ToString("0.###") +
            ".." + highestObservedBoostDuration.ToString("0.###") + "s");
        report.AppendLine(
            "Order Violations: Displacement->Pause=" +
            displacementPauseOrderViolations +
            " | Pause->Boost=" + pauseBoostOrderViolations);
        report.AppendLine(
            "Active: Displacements=" + activeDisplacements +
            " | Reactions=" + activeReactions +
            " | Pauses=" + activePauses +
            " | Boosts=" + activeBoosts);

        if (notes.Count > 0)
        {
            report.AppendLine("Notes:");

            for (int i = 0; i < notes.Count; i++)
            {
                report.AppendLine("- " + notes[i]);
            }
        }

        if (errors.Count > 0)
        {
            AppendErrors(report, errors);
            report.AppendLine(
                "FAIL: Fix the CB4 configuration or runtime order before " +
                "accepting this phase.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!pushObserved ||
            !pauseObserved ||
            !boostObserved ||
            !settled)
        {
            report.AppendLine("Observation still required:");

            if (!pushObserved)
            {
                report.AppendLine(
                    "- Successfully push at least one enemy.");
            }

            if (!pauseObserved)
            {
                report.AppendLine(
                    "- Observe one displacement ending and its enemy " +
                    "remaining briefly paused afterward.");
            }

            if (!boostObserved)
            {
                report.AppendLine(
                    "- After the pause, let one enemy pursue long enough for " +
                    "its temporary speed boost to start and complete.");
            }

            if (!settled)
            {
                report.AppendLine(
                    "- Stop clicking and wait about one second before " +
                    "auditing again.");
            }

            report.AppendLine(
                "INCOMPLETE: CB4 wiring is valid, but the full displacement, " +
                "pause, pursuit-boost and expiry sequence has not all been " +
                "recorded.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB4 collision-safe displacement, post-knockback pause and " +
            "temporary pursuit acceleration were observed in the required " +
            "order. EnemyMotor2D remains the sole Rigidbody2D movement owner.");
        Debug.Log(report.ToString());
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

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.scene.isLoaded &&
               component.gameObject.activeInHierarchy;
    }
}
