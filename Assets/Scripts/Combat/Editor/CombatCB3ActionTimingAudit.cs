using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB3 left-push cooldown and brief player movement recovery.
/// For a complete observation, perform one push, click again rapidly, wait
/// beyond the cooldown, perform another push, then wait for recovery to end.
/// </summary>
public static class CombatCB3ActionTimingAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB3 Action Timing Audit")]
    public static void RunAudit()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log(
                "[Combat/CB3] Action Timing Audit\n" +
                "Enter Play Mode after player and enemy generation. " +
                "CB3 runtime timing cannot be observed in Edit Mode.");

            return;
        }

        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        int activePlayerCount = 0;
        RuntimeObservation observation =
            RuntimeObservation.CreateEmpty();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (!IsActiveSceneObject(controller))
            {
                continue;
            }

            activePlayerCount++;
            controller.CollectValidationErrors(errors);

            NonlethalPushSettings settings =
                controller.PushSettings;

            if (settings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": Nonlethal Push settings are missing.");

                continue;
            }

            settings.EnsureValid();
            observation.CooldownDuration =
                settings.CooldownDuration;
            observation.AfterlagDuration =
                settings.AfterlagDuration;
            observation.AfterlagMovementMultiplier =
                settings.AfterlagMovementMultiplier;

            if (settings.CooldownDuration <
                settings.AfterlagDuration)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": Cooldown Duration should not be shorter than " +
                    "Afterlag Duration in CB3.");
            }

            RuntimeDungeonPlayer movement =
                controller.Movement;

            if (movement == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": RuntimeDungeonPlayer is missing.");

                continue;
            }

            observation.RawInputs +=
                controller.LeftPushInputCount;
            observation.StartedActions +=
                controller.StartedLeftPushActionCount;
            observation.IssuedActions +=
                controller.IssuedNonlethalPushAttackCount;
            observation.SuccessfulActions +=
                controller.SuccessfulLeftPushActionCount;
            observation.AcceptedTargets +=
                controller.AcceptedLeftPushTargetCount;
            observation.RecoveryRejects +=
                controller.RecoveryRejectedLeftPushCount;
            observation.CooldownRejects +=
                controller.CooldownRejectedLeftPushCount;
            observation.ControllerAfterlagStarts +=
                controller.AfterlagMovementStartCount;
            observation.ControllerAfterlagRejects +=
                controller.AfterlagMovementRejectCount;
            observation.MovementScaleStarts +=
                movement.TimedMovementScaleStartCount;
            observation.MovementScaleCompletions +=
                movement.TimedMovementScaleCompleteCount;
            observation.MovementScaleRejects +=
                movement.TimedMovementScaleRejectCount;
            observation.LowestMovementScale = Mathf.Min(
                observation.LowestMovementScale,
                movement.LowestRequestedTimedMovementScale);

            if (controller.IsActionRecoveryActive)
            {
                observation.ActiveActionRecoveries++;
            }

            if (controller.IsLeftPushCooldownActive)
            {
                observation.ActiveCooldowns++;
            }

            if (movement.IsTimedMovementScaleActive)
            {
                observation.ActiveMovementScales++;
            }

            if (controller.StartedLeftPushActionCount !=
                controller.IssuedNonlethalPushAttackCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": every started action should issue exactly one " +
                    "CombatAttackId.");
            }

            if (movement.TimedMovementScaleStartCount <
                controller.AfterlagMovementStartCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": movement owner recorded fewer timed-scale starts " +
                    "than the nonlethal-push controller. Later combat " +
                    "actions may legitimately add more movement scales.");
            }
        }

        if (activePlayerCount != 1)
        {
            errors.Add(
                "Expected exactly one active runtime player, found " +
                activePlayerCount + ".");
        }

        bool cooldownObserved =
            observation.StartedActions >= 2 &&
            observation.RawInputs > observation.StartedActions &&
            observation.TotalTimingRejects > 0;

        bool afterlagObserved =
            observation.ControllerAfterlagStarts >= 2 &&
            observation.MovementScaleStarts >= 2 &&
            observation.MovementScaleCompletions >= 1 &&
            observation.LowestMovementScale < 0.999f;

        bool pushChainObserved =
            observation.SuccessfulActions > 0 &&
            observation.AcceptedTargets > 0;

        if (observation.RecoveryRejects > 0)
        {
            notes.Add(
                "At least one click was rejected during the brief " +
                "action-recovery window.");
        }

        if (observation.CooldownRejects > 0)
        {
            notes.Add(
                "At least one click was rejected after recovery but before " +
                "the left-push cooldown ended.");
        }

        if (observation.ActiveMovementScales > 0)
        {
            notes.Add(
                "A movement recovery is active while the audit runs. " +
                "Wait about 0.25 seconds and audit again to observe expiry.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB3] Action Timing Audit");
        report.AppendLine(
            "Runtime Players=" + activePlayerCount +
            " | Cooldown=" +
            observation.CooldownDuration.ToString("0.###") +
            "s | Afterlag=" +
            observation.AfterlagDuration.ToString("0.###") +
            "s | Movement Scale=" +
            observation.AfterlagMovementMultiplier.ToString("0.###"));

        report.AppendLine(
            "Raw Push Inputs=" + observation.RawInputs +
            " | Started Actions=" + observation.StartedActions +
            " | Issued Attack Ids=" + observation.IssuedActions);

        report.AppendLine(
            "Recovery Rejects=" + observation.RecoveryRejects +
            " | Cooldown Rejects=" + observation.CooldownRejects +
            " | Total Timing Rejects=" +
            observation.TotalTimingRejects);

        report.AppendLine(
            "Successful Push Actions=" +
            observation.SuccessfulActions +
            " | Accepted Targets=" + observation.AcceptedTargets);

        report.AppendLine(
            "Controller Afterlag Starts=" +
            observation.ControllerAfterlagStarts +
            " | Controller Rejects=" +
            observation.ControllerAfterlagRejects);

        report.AppendLine(
            "Movement Scale Starts=" +
            observation.MovementScaleStarts +
            " | Completed=" +
            observation.MovementScaleCompletions +
            " | Rejected=" +
            observation.MovementScaleRejects +
            " | Lowest Scale=" +
            observation.LowestMovementScale.ToString("0.###"));

        report.AppendLine(
            "Active Action Recoveries=" +
            observation.ActiveActionRecoveries +
            " | Active Cooldowns=" +
            observation.ActiveCooldowns +
            " | Active Movement Scales=" +
            observation.ActiveMovementScales);

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
                "FAIL: Fix the CB3 runtime wiring before accepting this phase.");

            Debug.LogError(report.ToString());
            return;
        }

        if (!cooldownObserved ||
            !afterlagObserved ||
            !pushChainObserved)
        {
            report.AppendLine("Observation still required:");

            if (!cooldownObserved)
            {
                report.AppendLine(
                    "- Start one push, click rapidly during its timing gate, " +
                    "then wait beyond cooldown and start a second push.");
            }

            if (!afterlagObserved)
            {
                report.AppendLine(
                    "- Move while pushing at least twice, then wait about " +
                    "0.25 seconds so one movement recovery can complete.");
            }

            if (!pushChainObserved)
            {
                report.AppendLine(
                    "- Successfully push at least one enemy to preserve the " +
                    "CB1 and CB2 hit chain.");
            }

            report.AppendLine(
                "INCOMPLETE: CB3 wiring is valid, but cooldown, movement " +
                "recovery and a successful push have not all been observed.");

            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB3 short left-push cooldown and steerable movement " +
            "recovery were both observed. RuntimeDungeonPlayer remains the " +
            "sole owner of player Rigidbody2D movement.");

        Debug.Log(report.ToString());
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
        public int RawInputs;
        public int StartedActions;
        public int IssuedActions;
        public int SuccessfulActions;
        public int AcceptedTargets;
        public int RecoveryRejects;
        public int CooldownRejects;
        public int ControllerAfterlagStarts;
        public int ControllerAfterlagRejects;
        public int MovementScaleStarts;
        public int MovementScaleCompletions;
        public int MovementScaleRejects;
        public int ActiveActionRecoveries;
        public int ActiveCooldowns;
        public int ActiveMovementScales;
        public float CooldownDuration;
        public float AfterlagDuration;
        public float AfterlagMovementMultiplier;
        public float LowestMovementScale;

        public int TotalTimingRejects =>
            RecoveryRejects + CooldownRejects;

        public static RuntimeObservation CreateEmpty()
        {
            return new RuntimeObservation
            {
                LowestMovementScale = 1f
            };
        }
    }
}
