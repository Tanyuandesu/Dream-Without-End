#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CombatCB8ActionArbitrationAudit
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Combat/Run CB8 Action Arbitration Audit";

    [MenuItem(MenuPath)]
    public static void RunAudit()
    {
        PlayerCombatController controller = FindRuntimeController();
        List<string> errors = new List<string>();
        List<string> observations = new List<string>();

        if (controller == null)
        {
            Debug.LogError(
                "[Combat/CB8] Action Arbitration Audit\n" +
                "FAIL: No active runtime PlayerCombatController was found. " +
                "Enter Play Mode and let the player spawn before running this audit.");
            return;
        }

        controller.CollectValidationErrors(errors);

        CombatActionArbitrationSettings settings =
            controller.ActionArbitrationSettings;

        if (settings == null)
        {
            errors.Add("Runtime action-arbitration settings are missing.");
        }
        else if (!settings.Enabled)
        {
            errors.Add("CB8 action arbitration is disabled.");
        }

        int simultaneousResolutionTotal =
            controller.SimultaneousPushPriorityResolutionCount +
            controller.SimultaneousDirectPriorityResolutionCount +
            controller.SimultaneousRejectBothCount;

        if (simultaneousResolutionTotal !=
            controller.SimultaneousActionInputFrameCount)
        {
            errors.Add(
                "Simultaneous input frames and arbitration resolutions do not match.");
        }

        if (controller.DualActionStartFrameViolationCount != 0)
        {
            errors.Add(
                "Both combat actions started in the same rendered frame at least once.");
        }

        if (controller.HasOverlappingActionRecovery)
        {
            errors.Add(
                "Push and direct-attack afterlag windows are active simultaneously.");
        }

        if (controller.IssuedNonlethalPushAttackCount !=
            controller.StartedLeftPushActionCount)
        {
            errors.Add(
                "Nonlethal-push starts and issued attack ids are not one-to-one.");
        }

        if (controller.IssuedDirectAttackCount !=
            controller.StartedDirectAttackActionCount)
        {
            errors.Add(
                "Direct-attack starts and issued attack ids are not one-to-one.");
        }

        bool observedSimultaneousResolution =
            controller.SimultaneousActionInputFrameCount > 0;

        if (!observedSimultaneousResolution)
        {
            observations.Add(
                "Press one push key and one direct-attack key during the same " +
                "moment, for example J+K or Z+X.");
        }
        else if (settings != null)
        {
            switch (settings.SimultaneousInputPolicy)
            {
                case SimultaneousCombatActionPolicy.PreferNonlethalPush:
                    if (controller.SimultaneousPushPriorityResolutionCount <= 0 ||
                        controller.SuppressedDirectAttackInputFrameCount <= 0)
                    {
                        observations.Add(
                            "A dual-input frame was seen, but nonlethal priority " +
                            "and direct-attack suppression were not both recorded.");
                    }
                    break;

                case SimultaneousCombatActionPolicy.PreferDirectAttack:
                    if (controller.SimultaneousDirectPriorityResolutionCount <= 0 ||
                        controller.SuppressedPushInputFrameCount <= 0)
                    {
                        observations.Add(
                            "A dual-input frame was seen, but direct-attack priority " +
                            "and push suppression were not both recorded.");
                    }
                    break;

                case SimultaneousCombatActionPolicy.RejectBoth:
                    if (controller.SimultaneousRejectBothCount <= 0)
                    {
                        observations.Add(
                            "A dual-input frame was seen, but reject-both arbitration " +
                            "was not recorded.");
                    }
                    break;
            }
        }

        bool pushCrossPolicyObserved = false;
        bool directCrossPolicyObserved = false;

        if (settings != null)
        {
            if (settings.PushDuringDirectAttackRecovery ==
                CrossActionRecoveryPolicy.BlockNewAction)
            {
                pushCrossPolicyObserved =
                    controller.PushBlockedByDirectRecoveryCount > 0;

                if (!pushCrossPolicyObserved)
                {
                    observations.Add(
                        "Start a direct attack, then press push during its 0.16s " +
                        "afterlag to observe the configured cross-action block.");
                }
            }
            else
            {
                pushCrossPolicyObserved =
                    controller.PushCancelledDirectRecoveryCount > 0;

                if (!pushCrossPolicyObserved)
                {
                    observations.Add(
                        "Start a direct attack, then press push during its afterlag " +
                        "to observe the configured cancellation policy.");
                }
            }

            if (settings.DirectAttackDuringPushRecovery ==
                CrossActionRecoveryPolicy.BlockNewAction)
            {
                directCrossPolicyObserved =
                    controller.DirectBlockedByPushRecoveryCount > 0;

                if (!directCrossPolicyObserved)
                {
                    observations.Add(
                        "Start a push, then press direct attack during its 0.18s " +
                        "afterlag to observe the configured cross-action block.");
                }
            }
            else
            {
                directCrossPolicyObserved =
                    controller.DirectCancelledPushRecoveryCount > 0;

                if (!directCrossPolicyObserved)
                {
                    observations.Add(
                        "Start a push, then press direct attack during its afterlag " +
                        "to observe the configured cancellation policy.");
                }
            }
        }

        bool independentCooldownObserved =
            controller.PushStartedWhileDirectCooldownActiveCount > 0 ||
            controller.DirectStartedWhilePushCooldownActiveCount > 0;

        if (!independentCooldownObserved)
        {
            observations.Add(
                "After one action's afterlag ends but before its cooldown ends, " +
                "start the other action to confirm cooldown independence.");
        }

        bool bothActionsStarted =
            controller.StartedLeftPushActionCount > 0 &&
            controller.StartedDirectAttackActionCount > 0;

        if (!bothActionsStarted)
        {
            observations.Add(
                "Start at least one nonlethal push and one direct attack.");
        }

        bool transientStateClear =
            !controller.IsActionRecoveryActive &&
            !controller.IsDirectAttackRecoveryActive &&
            !controller.HasOverlappingActionRecovery &&
            (controller.Movement == null ||
             !controller.Movement.IsTimedMovementScaleActive);

        if (!transientStateClear)
        {
            observations.Add(
                "Wait about 0.3 seconds after the final action, then run the audit again.");
        }

        bool complete =
            errors.Count == 0 &&
            observedSimultaneousResolution &&
            pushCrossPolicyObserved &&
            directCrossPolicyObserved &&
            independentCooldownObserved &&
            bothActionsStarted &&
            transientStateClear;

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB8] Action Arbitration Audit");
        report.AppendLine(
            "Runtime Players=1 | Arbitration=" +
            (settings != null && settings.Enabled));

        if (settings != null)
        {
            report.AppendLine(
                "Policies: Simultaneous=" + settings.SimultaneousInputPolicy +
                " | PushDuringDirect=" +
                settings.PushDuringDirectAttackRecovery +
                " | DirectDuringPush=" +
                settings.DirectAttackDuringPushRecovery);
        }

        report.AppendLine(
            "Starts: Push=" + controller.StartedLeftPushActionCount +
            " | Direct=" + controller.StartedDirectAttackActionCount +
            " | PushIds=" + controller.IssuedNonlethalPushAttackCount +
            " | DirectIds=" + controller.IssuedDirectAttackCount);
        report.AppendLine(
            "Simultaneous: Frames=" +
            controller.SimultaneousActionInputFrameCount +
            " | PushPriority=" +
            controller.SimultaneousPushPriorityResolutionCount +
            " | DirectPriority=" +
            controller.SimultaneousDirectPriorityResolutionCount +
            " | RejectBoth=" + controller.SimultaneousRejectBothCount);
        report.AppendLine(
            "Suppressed: Push=" + controller.SuppressedPushInputFrameCount +
            " | Direct=" + controller.SuppressedDirectAttackInputFrameCount);
        report.AppendLine(
            "Cross Recovery: PushBlocked=" +
            controller.PushBlockedByDirectRecoveryCount +
            " | DirectBlocked=" +
            controller.DirectBlockedByPushRecoveryCount +
            " | PushCancelledDirect=" +
            controller.PushCancelledDirectRecoveryCount +
            " | DirectCancelledPush=" +
            controller.DirectCancelledPushRecoveryCount);
        report.AppendLine(
            "Independent Cooldowns: PushDuringDirectCooldown=" +
            controller.PushStartedWhileDirectCooldownActiveCount +
            " | DirectDuringPushCooldown=" +
            controller.DirectStartedWhilePushCooldownActiveCount);
        report.AppendLine(
            "Violations: DualStartFrame=" +
            controller.DualActionStartFrameViolationCount +
            " | RecoveryOverlap=" + controller.HasOverlappingActionRecovery);
        report.AppendLine(
            "Active: PushRecovery=" + controller.IsActionRecoveryActive +
            " | DirectRecovery=" + controller.IsDirectAttackRecoveryActive +
            " | MovementScale=" +
            (controller.Movement != null &&
             controller.Movement.IsTimedMovementScaleActive));

        if (errors.Count > 0)
        {
            report.AppendLine("Errors:");
            for (int i = 0; i < errors.Count; i++)
            {
                report.AppendLine("- " + errors[i]);
            }
        }

        if (observations.Count > 0)
        {
            report.AppendLine("Observation still required:");
            for (int i = 0; i < observations.Count; i++)
            {
                report.AppendLine("- " + observations[i]);
            }
        }

        if (errors.Count > 0)
        {
            report.AppendLine(
                "FAIL: CB8 action-arbitration wiring contains a structural or " +
                "runtime ownership violation.");
            Debug.LogError(report.ToString(), controller);
            return;
        }

        if (!complete)
        {
            report.AppendLine(
                "INCOMPLETE: CB8 wiring is valid, but not every configured " +
                "arbitration path has been observed in this run.");
            Debug.LogWarning(report.ToString(), controller);
            return;
        }

        report.AppendLine(
            "PASS: CB8 resolves same-frame dual input deterministically, blocks " +
            "cross-action afterlag overlap, preserves independent cooldowns and " +
            "never starts both actions in one rendered frame.");
        Debug.Log(report.ToString(), controller);
    }

    private static PlayerCombatController FindRuntimeController()
    {
        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (controller == null ||
                !controller.gameObject.scene.IsValid() ||
                !controller.gameObject.activeInHierarchy)
            {
                continue;
            }

            return controller;
        }

        return null;
    }
}
#endif
