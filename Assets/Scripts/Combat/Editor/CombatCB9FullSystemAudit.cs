using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CB9 consolidates authoring validation and representative runtime checks for
/// both player combat actions, enemy reactions, arbitration and death cleanup.
/// It does not alter gameplay or tuning values.
/// </summary>
public static class CombatCB9FullSystemAudit
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Combat/Run CB9 Full Combat Audit";

    [MenuItem(MenuPath)]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> observations = new List<string>();
        List<string> notes = new List<string>();

        List<EnemyDefinition> definitions = LoadDefinitions(errors);
        ValidateDefinitions(definitions, errors);

        PlayerCombatController controller = FindRuntimeController();
        EnemyManager manager = FindRuntimeEnemyManager();

        if (controller == null)
        {
            Debug.LogError(
                "[Combat/CB9] Full Combat Audit\n" +
                "FAIL: No active runtime PlayerCombatController was found. " +
                "Enter Play Mode and wait for the player to spawn.");
            return;
        }

        controller.CollectValidationErrors(errors);

        if (controller.InputBindings != null)
        {
            controller.InputBindings.CollectValidationNotes(
                notes,
                controller.gameObject.name);
        }

        if (manager == null)
        {
            errors.Add("No active runtime EnemyManager was found.");
        }

        List<EnemyCombatReceiver> liveReceivers =
            FindLiveRuntimeReceivers();

        ValidateRuntimeReferences(
            controller,
            manager,
            liveReceivers,
            errors);

        bool expectsWeakDisplacement =
            ExpectsWeakDisplacement(controller, definitions);
        bool expectsWeakReaction =
            ExpectsWeakReaction(controller, definitions);

        ValidateInvariants(
            controller,
            manager,
            liveReceivers,
            errors);

        CollectRequiredObservations(
            controller,
            manager,
            liveReceivers,
            expectsWeakDisplacement,
            expectsWeakReaction,
            observations);

        bool transientStateClear =
            AreTransientStatesClear(controller, liveReceivers);

        if (!transientStateClear)
        {
            observations.Add(
                "Wait about one second after the final combat action, then run " +
                "the audit again so all displacement, reaction, pause, boost " +
                "and player recovery windows can finish.");
        }

        bool complete =
            errors.Count == 0 &&
            observations.Count == 0 &&
            transientStateClear;

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB9] Full Combat Audit");
        report.AppendLine(
            "Runtime Players=1 | Runtime Enemies=" +
            liveReceivers.Count +
            " | Enemy Definitions=" + definitions.Count);

        AppendPlayerTuning(report, controller);
        AppendEnemyTuning(report, definitions);
        AppendCombatDiagnostics(report);
        AppendArbitrationDiagnostics(report, controller);
        AppendDeathDiagnostics(report, manager);
        AppendTransientDiagnostics(report, controller, liveReceivers);

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
            report.AppendLine("Errors:");

            for (int i = 0; i < errors.Count; i++)
            {
                report.AppendLine("- " + errors[i]);
            }

            report.AppendLine(
                "FAIL: CB9 found a configuration or runtime invariant violation.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!complete)
        {
            report.AppendLine("Observation still required:");

            for (int i = 0; i < observations.Count; i++)
            {
                report.AppendLine("- " + observations[i]);
            }

            report.AppendLine(
                "INCOMPLETE: CB9 configuration is valid, but the representative " +
                "full-chain scenario has not yet been observed in this run.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB9 observed configurable nonlethal push and direct attack " +
            "actions, strict zero-damage push behavior, isolated weak direct-hit " +
            "response, deterministic arbitration, player-attributed death cleanup " +
            "and clear transient state without stale runtime references.");
        Debug.Log(report.ToString());
    }

    private static List<EnemyDefinition> LoadDefinitions(
        List<string> errors)
    {
        List<EnemyDefinition> definitions =
            new List<EnemyDefinition>();

        string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);

            if (definition == null)
            {
                errors.Add(path + ": failed to load EnemyDefinition.");
                continue;
            }

            definitions.Add(definition);
        }

        if (definitions.Count == 0)
        {
            errors.Add("No EnemyDefinition assets were found.");
        }

        return definitions;
    }

    private static void ValidateDefinitions(
        List<EnemyDefinition> definitions,
        List<string> errors)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];
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

            if (resistance.DecayTierCount <
                KnockbackResistanceSettings.MinimumDecayTierCount)
            {
                errors.Add(
                    definition.name +
                    ": fewer than three independently editable decay tiers.");
            }
        }
    }

    private static void ValidateRuntimeReferences(
        PlayerCombatController controller,
        EnemyManager manager,
        List<EnemyCombatReceiver> liveReceivers,
        List<string> errors)
    {
        if (controller == null)
        {
            return;
        }

        if (!controller.CombatInputEnabled)
        {
            errors.Add("Player combat input is disabled at runtime.");
        }

        if (liveReceivers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < liveReceivers.Count; i++)
        {
            liveReceivers[i].CollectValidationErrors(errors);
        }

        if (manager != null &&
            manager.ActiveEnemyCount != liveReceivers.Count)
        {
            errors.Add(
                "EnemyManager ActiveEnemyCount=" +
                manager.ActiveEnemyCount +
                " does not match live EnemyCombatReceiver count=" +
                liveReceivers.Count + ".");
        }
    }

    private static void ValidateInvariants(
        PlayerCombatController controller,
        EnemyManager manager,
        List<EnemyCombatReceiver> liveReceivers,
        List<string> errors)
    {
        if (CombatSystemDiagnostics.NonlethalDamagePayloadViolationCount != 0 ||
            CombatSystemDiagnostics.NonlethalAcceptedDamageViolationCount != 0)
        {
            errors.Add(
                "At least one nonlethal push carried or applied damage.");
        }

        if (CombatSystemDiagnostics.DirectAttackPayloadViolationCount != 0 ||
            CombatSystemDiagnostics.DirectAttackDecayIsolationViolationCount != 0 ||
            CombatSystemDiagnostics.DirectAttackPursuitIsolationViolationCount != 0)
        {
            errors.Add(
                "Direct attack polluted nonlethal decay, pursuit recovery or " +
                "strong-reaction payload rules.");
        }

        if (CombatSystemDiagnostics.UnclassifiedHitCount != 0)
        {
            errors.Add("An accepted combat hit had no recognized action kind.");
        }

        if (controller.IssuedNonlethalPushAttackCount !=
            controller.StartedLeftPushActionCount)
        {
            errors.Add(
                "Nonlethal-push action starts and attack ids are not one-to-one.");
        }

        if (controller.IssuedDirectAttackCount !=
            controller.StartedDirectAttackActionCount)
        {
            errors.Add(
                "Direct-attack action starts and attack ids are not one-to-one.");
        }

        if (controller.DualActionStartFrameViolationCount != 0 ||
            controller.HasOverlappingActionRecovery)
        {
            errors.Add(
                "Two combat actions started together or their recovery windows overlapped.");
        }

        if (manager != null)
        {
            if (manager.DuplicateDeathRecordCount != 0)
            {
                errors.Add("EnemyManager recorded a duplicate death.");
            }

            if (manager.UnexpectedRemovalCount != 0)
            {
                errors.Add("EnemyManager observed an unexpected enemy removal.");
            }

            if (manager.RecordedPlayerDeathCount !=
                EnemyDeathLifecycleDiagnostics.PlayerAttributedDeathCount)
            {
                errors.Add(
                    "EnemyManager player-kill count and death-seal diagnostics disagree.");
            }
        }

        if (EnemyDeathLifecycleDiagnostics.LifecycleViolationCount != 0)
        {
            errors.Add(
                "A dead enemy retained contact damage, collider, physics, AI, " +
                "movement or combat reaction state.");
        }

        for (int i = 0; i < liveReceivers.Count; i++)
        {
            EnemyCombatReceiver receiver = liveReceivers[i];

            if (receiver.DirectAttackPayloadViolationCount != 0 ||
                receiver.DirectAttackDecayIsolationViolationCount != 0 ||
                receiver.DirectAttackPursuitIsolationViolationCount != 0)
            {
                errors.Add(
                    receiver.gameObject.name +
                    ": runtime direct-attack isolation violation detected.");
            }
        }
    }

    private static void CollectRequiredObservations(
        PlayerCombatController controller,
        EnemyManager manager,
        List<EnemyCombatReceiver> liveReceivers,
        bool expectsWeakDisplacement,
        bool expectsWeakReaction,
        List<string> observations)
    {
        if (liveReceivers.Count == 0)
        {
            observations.Add(
                "Leave at least one enemy alive so CB9 can inspect current " +
                "runtime references and transient state.");
        }

        if (CombatSystemDiagnostics.NonlethalPushHitCount <= 0)
        {
            observations.Add(
                "Successfully hit at least one enemy with left mouse, J or Z.");
        }

        if (CombatSystemDiagnostics.DirectAttackHitCount <= 0 ||
            CombatSystemDiagnostics.DirectAttackDamageHitCount <= 0 ||
            CombatSystemDiagnostics.DirectAttackAcceptedDamage <= 0f)
        {
            observations.Add(
                "Successfully damage at least one enemy with right mouse, K or X.");
        }

        if (expectsWeakDisplacement &&
            CombatSystemDiagnostics.DirectAttackWeakDisplacementCount <= 0)
        {
            observations.Add(
                "Hit a live enemy with direct attack so the configured weak " +
                "collision-safe displacement is observed.");
        }

        if (expectsWeakReaction &&
            CombatSystemDiagnostics.DirectAttackWeakReactionCount <= 0)
        {
            observations.Add(
                "Hit a live enemy with direct attack so the configured weak Hit " +
                "pause is observed.");
        }

        if (manager == null || manager.RecordedPlayerDeathCount <= 0)
        {
            observations.Add(
                "Kill at least one enemy with direct attack while leaving another alive.");
        }
        else if (EnemyDeathLifecycleDiagnostics.ProcessedDeathCount <= 0)
        {
            observations.Add(
                "A player kill was recorded, but no synchronous death seal has " +
                "been observed in this run.");
        }

        if (controller.StartedLeftPushActionCount <= 0 ||
            controller.StartedDirectAttackActionCount <= 0)
        {
            observations.Add(
                "Start at least one action of each type in the current run.");
        }
    }

    private static bool ExpectsWeakDisplacement(
        PlayerCombatController controller,
        List<EnemyDefinition> definitions)
    {
        if (controller.DirectAttackSettings == null ||
            controller.DirectAttackSettings.WeakDisplacementDistance <= 0f)
        {
            return false;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].DirectAttackWeakDisplacementMultiplier > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExpectsWeakReaction(
        PlayerCombatController controller,
        List<EnemyDefinition> definitions)
    {
        if (controller.DirectAttackSettings == null ||
            controller.DirectAttackSettings.WeakHitPauseDuration <= 0f)
        {
            return false;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].DirectAttackWeakHitPauseMultiplier > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreTransientStatesClear(
        PlayerCombatController controller,
        List<EnemyCombatReceiver> liveReceivers)
    {
        if (controller.IsActionRecoveryActive ||
            controller.IsDirectAttackRecoveryActive ||
            controller.HasOverlappingActionRecovery ||
            (controller.Movement != null &&
             controller.Movement.IsTimedMovementScaleActive))
        {
            return false;
        }

        for (int i = 0; i < liveReceivers.Count; i++)
        {
            EnemyCombatReceiver receiver = liveReceivers[i];

            if ((receiver.Motor != null &&
                 (receiver.Motor.IsCombatDisplacementActive ||
                  receiver.Motor.IsTimedNavigationSpeedActive)) ||
                (receiver.StateMachine != null &&
                 receiver.StateMachine.IsCombatReactionActive))
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendPlayerTuning(
        StringBuilder report,
        PlayerCombatController controller)
    {
        NonlethalPushSettings push = controller.PushSettings;
        DirectAttackSettings direct = controller.DirectAttackSettings;

        if (push != null)
        {
            report.AppendLine(
                "Push Tuning: Range=" + push.Range.ToString("0.##") +
                " | Arc=" + push.ArcAngle.ToString("0.##") +
                " | Distance=" + push.DisplacementDistance.ToString("0.##") +
                " in " + push.DisplacementDuration.ToString("0.###") + "s" +
                " | Cooldown=" + push.CooldownDuration.ToString("0.###") + "s" +
                " | Afterlag=" + push.AfterlagDuration.ToString("0.###") + "s" +
                " @" + push.AfterlagMovementMultiplier.ToString("0.##") + "x");
        }

        if (direct != null)
        {
            report.AppendLine(
                "Direct Tuning: Damage=" + direct.Damage.ToString("0.##") +
                " | Range=" + direct.Range.ToString("0.##") +
                " | Arc=" + direct.ArcAngle.ToString("0.##") +
                " | Cooldown=" + direct.CooldownDuration.ToString("0.###") + "s" +
                " | Afterlag=" + direct.AfterlagDuration.ToString("0.###") + "s" +
                " @" + direct.AfterlagMovementMultiplier.ToString("0.##") + "x" +
                " | Weak=" + direct.WeakDisplacementDistance.ToString("0.###") +
                "/" + direct.WeakHitPauseDuration.ToString("0.###") + "s");
        }
    }

    private static void AppendEnemyTuning(
        StringBuilder report,
        List<EnemyDefinition> definitions)
    {
        int minimumTiers = int.MaxValue;
        int maximumTiers = 0;
        float minimumPause = float.MaxValue;
        float maximumPause = 0f;
        float minimumBoost = float.MaxValue;
        float maximumBoost = 0f;

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];
            int tierCount = definition.KnockbackResistance.DecayTierCount;
            minimumTiers = Mathf.Min(minimumTiers, tierCount);
            maximumTiers = Mathf.Max(maximumTiers, tierCount);
            minimumPause = Mathf.Min(
                minimumPause,
                definition.PostKnockbackPauseDuration);
            maximumPause = Mathf.Max(
                maximumPause,
                definition.PostKnockbackPauseDuration);
            minimumBoost = Mathf.Min(
                minimumBoost,
                definition.PostKnockbackPursuitSpeedMultiplier);
            maximumBoost = Mathf.Max(
                maximumBoost,
                definition.PostKnockbackPursuitSpeedMultiplier);
        }

        if (definitions.Count == 0)
        {
            minimumTiers = 0;
            minimumPause = 0f;
            minimumBoost = 0f;
        }

        report.AppendLine(
            "Enemy Tuning: Decay Tiers=" + minimumTiers + ".." + maximumTiers +
            " | Post-Pause=" + minimumPause.ToString("0.###") + ".." +
            maximumPause.ToString("0.###") + "s" +
            " | Pursuit Boost=" + minimumBoost.ToString("0.##") + ".." +
            maximumBoost.ToString("0.##") + "x");
    }

    private static void AppendCombatDiagnostics(StringBuilder report)
    {
        report.AppendLine(
            "Accepted Hits: Total=" + CombatSystemDiagnostics.AcceptedHitCount +
            " | Push=" + CombatSystemDiagnostics.NonlethalPushHitCount +
            " | Direct=" + CombatSystemDiagnostics.DirectAttackHitCount +
            " | Direct Damage Hits=" +
            CombatSystemDiagnostics.DirectAttackDamageHitCount +
            " | Damage=" +
            CombatSystemDiagnostics.DirectAttackAcceptedDamage.ToString("0.##"));
        report.AppendLine(
            "Weak Response: Displacements=" +
            CombatSystemDiagnostics.DirectAttackWeakDisplacementCount +
            " | Hit Pauses=" +
            CombatSystemDiagnostics.DirectAttackWeakReactionCount);
        report.AppendLine(
            "Isolation Violations: PushPayload=" +
            CombatSystemDiagnostics.NonlethalDamagePayloadViolationCount +
            " | PushDamage=" +
            CombatSystemDiagnostics.NonlethalAcceptedDamageViolationCount +
            " | DirectPayload=" +
            CombatSystemDiagnostics.DirectAttackPayloadViolationCount +
            " | DirectDecay=" +
            CombatSystemDiagnostics.DirectAttackDecayIsolationViolationCount +
            " | DirectPursuit=" +
            CombatSystemDiagnostics.DirectAttackPursuitIsolationViolationCount +
            " | Unclassified=" + CombatSystemDiagnostics.UnclassifiedHitCount);
    }

    private static void AppendArbitrationDiagnostics(
        StringBuilder report,
        PlayerCombatController controller)
    {
        report.AppendLine(
            "Actions: Push Starts=" + controller.StartedLeftPushActionCount +
            "/Ids=" + controller.IssuedNonlethalPushAttackCount +
            " | Direct Starts=" + controller.StartedDirectAttackActionCount +
            "/Ids=" + controller.IssuedDirectAttackCount +
            " | Simultaneous=" +
            controller.SimultaneousActionInputFrameCount +
            " | DualStart Violations=" +
            controller.DualActionStartFrameViolationCount);
    }

    private static void AppendDeathDiagnostics(
        StringBuilder report,
        EnemyManager manager)
    {
        if (manager == null)
        {
            report.AppendLine("Death Chain: EnemyManager missing");
            return;
        }

        EnemyRunRecordSnapshot snapshot = manager.CurrentRunSnapshot;
        report.AppendLine(
            "Death Chain: Player Kills=" + manager.RecordedPlayerDeathCount +
            " | Other=" + manager.RecordedOtherDeathCount +
            " | Sealed=" + EnemyDeathLifecycleDiagnostics.ProcessedDeathCount +
            " | Violations=" +
            EnemyDeathLifecycleDiagnostics.LifecycleViolationCount +
            " | Duplicate=" + manager.DuplicateDeathRecordCount +
            " | Unexpected=" + manager.UnexpectedRemovalCount);
        report.AppendLine(
            "Run Record: Eligible=" + snapshot.EligibleSpawnedCount +
            " | Player Kills=" + snapshot.PlayerKillCount +
            " | Other Deaths=" + snapshot.OtherDeathCount +
            " | Survivors=" + snapshot.SurvivedFloorCount +
            " | Active=" + snapshot.ActiveCount);
    }

    private static void AppendTransientDiagnostics(
        StringBuilder report,
        PlayerCombatController controller,
        List<EnemyCombatReceiver> liveReceivers)
    {
        int activeDisplacements = 0;
        int activeReactions = 0;
        int activeBoosts = 0;

        for (int i = 0; i < liveReceivers.Count; i++)
        {
            EnemyCombatReceiver receiver = liveReceivers[i];

            if (receiver.Motor != null &&
                receiver.Motor.IsCombatDisplacementActive)
            {
                activeDisplacements++;
            }

            if (receiver.Motor != null &&
                receiver.Motor.IsTimedNavigationSpeedActive)
            {
                activeBoosts++;
            }

            if (receiver.StateMachine != null &&
                receiver.StateMachine.IsCombatReactionActive)
            {
                activeReactions++;
            }
        }

        report.AppendLine(
            "Active Transients: PushRecovery=" +
            controller.IsActionRecoveryActive +
            " | DirectRecovery=" + controller.IsDirectAttackRecoveryActive +
            " | MovementScale=" +
            (controller.Movement != null &&
             controller.Movement.IsTimedMovementScaleActive) +
            " | EnemyDisplacements=" + activeDisplacements +
            " | EnemyReactions=" + activeReactions +
            " | EnemyBoosts=" + activeBoosts);
    }

    private static PlayerCombatController FindRuntimeController()
    {
        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            if (IsActiveSceneComponent(controllers[i]))
            {
                return controllers[i];
            }
        }

        return null;
    }

    private static EnemyManager FindRuntimeEnemyManager()
    {
        EnemyManager[] managers =
            Resources.FindObjectsOfTypeAll<EnemyManager>();

        for (int i = 0; i < managers.Length; i++)
        {
            if (IsActiveSceneComponent(managers[i]))
            {
                return managers[i];
            }
        }

        return null;
    }

    private static List<EnemyCombatReceiver> FindLiveRuntimeReceivers()
    {
        EnemyCombatReceiver[] receivers =
            Resources.FindObjectsOfTypeAll<EnemyCombatReceiver>();
        List<EnemyCombatReceiver> live =
            new List<EnemyCombatReceiver>();

        for (int i = 0; i < receivers.Length; i++)
        {
            EnemyCombatReceiver receiver = receivers[i];

            if (!IsActiveSceneComponent(receiver) ||
                !receiver.IsInitialized ||
                receiver.Health == null ||
                receiver.Health.IsDead)
            {
                continue;
            }

            live.Add(receiver);
        }

        return live;
    }

    private static bool IsActiveSceneComponent(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.scene.isLoaded &&
               component.gameObject.activeInHierarchy;
    }
}
