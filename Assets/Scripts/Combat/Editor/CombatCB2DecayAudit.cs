using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB2 configuration and reports observed runtime resistance changes.
/// For a complete observation, push the same enemy at least four accepted
/// times inside its build window, then leave it untouched long enough for at
/// least one recovery step before running this audit.
/// </summary>
public static class CombatCB2DecayAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB2 Decay Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        int definitionCount = ValidateDefinitions(
            errors,
            out int minimumConfiguredTierCount,
            out int maximumConfiguredTierCount);

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine("[Combat/CB2] Decay Audit");
            editReport.AppendLine(
                "Enemy Definitions=" + definitionCount +
                " | Configured Decay Tiers=" +
                minimumConfiguredTierCount + ".." +
                maximumConfiguredTierCount);

            AppendErrors(editReport, errors);

            if (errors.Count == 0)
            {
                editReport.AppendLine(
                    "PASS: Every EnemyDefinition has an independent, " +
                    "editable decay table with at least three levels. " +
                    "Enter Play Mode for runtime accumulation and recovery checks.");

                Debug.Log(editReport.ToString());
            }
            else
            {
                editReport.AppendLine(
                    "FAIL: Fix the CB2 definition errors before Play Mode testing.");

                Debug.LogError(editReport.ToString());
            }

            return;
        }

        int playerCount = ValidatePlayers(
            errors,
            out int issuedActions,
            out int successfulActions,
            out int acceptedTargets);

        int enemyCount = ValidateEnemies(
            errors,
            notes,
            out RuntimeObservation observation);

        bool accumulationObserved =
            observation.MaximumReachedLevel >=
                KnockbackResistanceSettings.MinimumDecayTierCount &&
            observation.TotalAdvanceCount >=
                KnockbackResistanceSettings.MinimumDecayTierCount &&
            observation.TotalDecayedPushes >=
                KnockbackResistanceSettings.MinimumDecayTierCount;

        bool recoveryObserved =
            observation.TotalRecoverySteps > 0;

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB2] Decay Audit");
        report.AppendLine(
            "Enemy Definitions=" + definitionCount +
            " | Configured Decay Tiers=" +
            minimumConfiguredTierCount + ".." +
            maximumConfiguredTierCount);

        report.AppendLine(
            "Runtime Players=" + playerCount +
            " | Runtime Enemies=" + enemyCount);

        report.AppendLine(
            "Issued Left-Push Actions=" + issuedActions +
            " | Successful Actions=" + successfulActions +
            " | Accepted Targets=" + acceptedTargets);

        report.AppendLine(
            "Qualifying Pushes=" + observation.TotalQualifyingPushes +
            " | Full Strength=" + observation.TotalFullStrengthPushes +
            " | Decayed=" + observation.TotalDecayedPushes);

        report.AppendLine(
            "Tier Advances=" + observation.TotalAdvanceCount +
            " | Highest Level Reached=" +
            observation.MaximumReachedLevel +
            " | Current Highest Level=" +
            observation.MaximumCurrentLevel +
            " | Recovery Steps=" +
            observation.TotalRecoverySteps);

        report.AppendLine(
            "Lowest Applied Distance Multiplier=" +
            observation.LowestDistanceMultiplier.ToString("0.###") +
            " | Lowest Applied Stagger Multiplier=" +
            observation.LowestStaggerMultiplier.ToString("0.###"));

        report.AppendLine(
            "Active Displacements=" +
            observation.ActiveDisplacements +
            " | Active Reactions=" +
            observation.ActiveReactions);

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
                "FAIL: Fix the CB2 runtime wiring before accepting this phase.");

            Debug.LogError(report.ToString());
            return;
        }

        if (!accumulationObserved || !recoveryObserved)
        {
            report.AppendLine("Observation still required:");

            if (!accumulationObserved)
            {
                report.AppendLine(
                    "- Push the same enemy at least four accepted times, " +
                    "keeping every interval inside its Decay Build Window.");
            }

            if (!recoveryObserved)
            {
                report.AppendLine(
                    "- After reaching a decay level, stop pushing and wait " +
                    "past Recovery Delay plus at least one Recovery Step Interval.");
            }

            report.AppendLine(
                "INCOMPLETE: CB2 wiring is valid, but both accumulation and " +
                "time-based recovery have not yet been observed in this run.");

            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB2 per-enemy multi-level knockback decay and stepped " +
            "time recovery were both observed. EnemyMotor2D remains the sole " +
            "Rigidbody2D movement owner.");

        Debug.Log(report.ToString());
    }

    private static int ValidateDefinitions(
        List<string> errors,
        out int minimumConfiguredTierCount,
        out int maximumConfiguredTierCount)
    {
        minimumConfiguredTierCount = int.MaxValue;
        maximumConfiguredTierCount = 0;

        string[] guids = AssetDatabase.FindAssets(
            "t:EnemyDefinition");

        if (guids.Length == 0)
        {
            errors.Add("No EnemyDefinition assets were found.");
            minimumConfiguredTierCount = 0;
            return 0;
        }

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

            KnockbackResistanceSettings settings =
                definition.KnockbackResistance;

            if (settings == null)
            {
                errors.Add(
                    definition.name +
                    ": Knockback Resistance is missing.");
                continue;
            }

            settings.EnsureValid();
            int tierCount = settings.DecayTierCount;

            minimumConfiguredTierCount = Mathf.Min(
                minimumConfiguredTierCount,
                tierCount);

            maximumConfiguredTierCount = Mathf.Max(
                maximumConfiguredTierCount,
                tierCount);

            if (tierCount <
                KnockbackResistanceSettings.MinimumDecayTierCount)
            {
                errors.Add(
                    definition.name +
                    ": expected at least three independently editable " +
                    "decay tiers, found " + tierCount + ".");
            }
        }

        if (minimumConfiguredTierCount == int.MaxValue)
        {
            minimumConfiguredTierCount = 0;
        }

        return guids.Length;
    }

    private static int ValidatePlayers(
        List<string> errors,
        out int issuedActions,
        out int successfulActions,
        out int acceptedTargets)
    {
        issuedActions = 0;
        successfulActions = 0;
        acceptedTargets = 0;

        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        int activeCount = 0;

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (!IsActiveSceneObject(controller))
            {
                continue;
            }

            activeCount++;
            controller.CollectValidationErrors(errors);

            if (!controller.CombatInputEnabled)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": combat input is not enabled.");
            }

            issuedActions += controller.IssuedNonlethalPushAttackCount;
            successfulActions +=
                controller.SuccessfulLeftPushActionCount;
            acceptedTargets +=
                controller.AcceptedLeftPushTargetCount;
        }

        if (activeCount != 1)
        {
            errors.Add(
                "Expected exactly one active PlayerCombatController, found " +
                activeCount + ".");
        }

        return activeCount;
    }

    private static int ValidateEnemies(
        List<string> errors,
        List<string> notes,
        out RuntimeObservation observation)
    {
        observation = RuntimeObservation.CreateEmpty();

        EnemyRuntimeContext[] contexts =
            Resources.FindObjectsOfTypeAll<EnemyRuntimeContext>();

        int activeCount = 0;

        for (int i = 0; i < contexts.Length; i++)
        {
            EnemyRuntimeContext context = contexts[i];

            if (!IsActiveSceneObject(context))
            {
                continue;
            }

            activeCount++;

            EnemyCombatReceiver receiver =
                context.GetComponent<EnemyCombatReceiver>();

            EnemyMotor2D motor =
                context.GetComponent<EnemyMotor2D>();

            EnemyStateMachine stateMachine =
                context.GetComponent<EnemyStateMachine>();

            if (receiver == null)
            {
                errors.Add(
                    context.gameObject.name +
                    ": EnemyCombatReceiver is missing.");
                continue;
            }

            receiver.CollectValidationErrors(errors);

            observation.TotalQualifyingPushes +=
                receiver.QualifyingDecayPushCount;

            observation.TotalFullStrengthPushes +=
                receiver.FullStrengthPushCount;

            observation.TotalDecayedPushes +=
                receiver.DecayedPushCount;

            observation.TotalAdvanceCount +=
                receiver.ResistanceAdvanceCount;

            observation.TotalRecoverySteps +=
                receiver.ResistanceRecoveryStepCount;

            observation.MaximumReachedLevel = Mathf.Max(
                observation.MaximumReachedLevel,
                receiver.HighestKnockbackResistanceLevel);

            observation.MaximumCurrentLevel = Mathf.Max(
                observation.MaximumCurrentLevel,
                receiver.CurrentKnockbackResistanceLevel);

            if (receiver.QualifyingDecayPushCount > 0)
            {
                observation.LowestDistanceMultiplier = Mathf.Min(
                    observation.LowestDistanceMultiplier,
                    receiver.LowestAppliedDistanceMultiplier);

                observation.LowestStaggerMultiplier = Mathf.Min(
                    observation.LowestStaggerMultiplier,
                    receiver.LowestAppliedStaggerMultiplier);
            }

            if (motor == null)
            {
                errors.Add(
                    context.gameObject.name +
                    ": EnemyMotor2D is missing.");
            }
            else if (motor.IsCombatDisplacementActive)
            {
                observation.ActiveDisplacements++;
            }

            if (stateMachine == null)
            {
                errors.Add(
                    context.gameObject.name +
                    ": EnemyStateMachine is missing.");
            }
            else if (stateMachine.IsCombatReactionActive)
            {
                observation.ActiveReactions++;
            }
        }

        if (activeCount == 0)
        {
            errors.Add(
                "No active runtime enemy exists. Wait for enemy generation.");
        }

        if (observation.TotalQualifyingPushes > 0 &&
            observation.TotalFullStrengthPushes == 0)
        {
            errors.Add(
                "Qualifying pushes were recorded without a full-strength " +
                "first push.");
        }

        if (observation.TotalDecayedPushes > 0)
        {
            notes.Add(
                "At least one accepted push used an EnemyDefinition decay " +
                "level rather than full strength.");
        }

        if (observation.TotalRecoverySteps > 0)
        {
            notes.Add(
                "At least one enemy reduced resistance by a timed recovery step.");
        }

        return activeCount;
    }

    private static void AppendErrors(
        StringBuilder report,
        List<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

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

    private struct RuntimeObservation
    {
        public int TotalQualifyingPushes;
        public int TotalFullStrengthPushes;
        public int TotalDecayedPushes;
        public int TotalAdvanceCount;
        public int TotalRecoverySteps;
        public int MaximumReachedLevel;
        public int MaximumCurrentLevel;
        public int ActiveDisplacements;
        public int ActiveReactions;
        public float LowestDistanceMultiplier;
        public float LowestStaggerMultiplier;

        public static RuntimeObservation CreateEmpty()
        {
            return new RuntimeObservation
            {
                LowestDistanceMultiplier = 1f,
                LowestStaggerMultiplier = 1f
            };
        }
    }
}
