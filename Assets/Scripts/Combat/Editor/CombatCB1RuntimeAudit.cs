using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB1 runtime wiring and reports observed nonlethal push activity.
/// Run in Play Mode after floor and enemy generation. The audit never moves
/// an enemy itself; the user supplies the real mouse input and wall tests.
/// </summary>
public static class CombatCB1RuntimeAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB1 Runtime Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[Combat/CB1] Runtime Audit\n" +
                "FAIL: Enter Play Mode, wait for player/enemy generation, " +
                "then run this audit again.");
            return;
        }

        int playerCount = ValidatePlayers(
            errors,
            notes,
            out int issuedActions,
            out int successfulActions,
            out int acceptedTargets);

        int enemyCount = ValidateEnemies(
            errors,
            notes,
            out int receiverAcceptedPushes,
            out int motorStarts,
            out int motorCompletions,
            out int motorBlocked,
            out int activeDisplacements,
            out int activeReactions);

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB1] Runtime Audit");
        report.AppendLine(
            "Runtime Players=" + playerCount +
            " | Runtime Enemies=" + enemyCount);

        report.AppendLine(
            "Issued Left-Push Actions=" + issuedActions +
            " | Successful Actions=" + successfulActions +
            " | Accepted Targets=" + acceptedTargets);

        report.AppendLine(
            "Receiver Pushes=" + receiverAcceptedPushes +
            " | Motor Starts=" + motorStarts +
            " | Completed=" + motorCompletions +
            " | Collision-Clipped=" + motorBlocked);

        report.AppendLine(
            "Active Displacements=" + activeDisplacements +
            " | Active Reactions=" + activeReactions);

        if (notes.Count > 0)
        {
            report.AppendLine("Notes:");

            for (int i = 0; i < notes.Count; i++)
            {
                report.AppendLine("- " + notes[i]);
            }
        }

        if (errors.Count == 0)
        {
            report.AppendLine(
                "PASS: CB1 zero-damage push is wired through " +
                "EnemyCombatReceiver, EnemyStateMachine and the sole " +
                "EnemyMotor2D movement owner.");

            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine("Errors:");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("- " + errors[i]);
        }

        report.AppendLine(
            "FAIL: Fix the runtime wiring before accepting CB1.");

        Debug.LogError(report.ToString());
    }

    private static int ValidatePlayers(
        List<string> errors,
        List<string> notes,
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
                    ": CB1 combat input is not enabled.");
            }

            if (controller.PushSettings == null ||
                !controller.PushSettings.Enabled)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": CB1 nonlethal push settings are disabled or missing.");
            }

            issuedActions += controller.IssuedAttackCount;
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

        if (activeCount == 1 && issuedActions == 0)
        {
            notes.Add(
                "No nonlethal-push action has been observed yet. Face " +
                "a nearby enemy, use any enabled push binding, then rerun the audit.");
        }
        else if (issuedActions > 0 && successfulActions == 0)
        {
            notes.Add(
                "Push input was observed, but no enemy accepted a push. " +
                "Check range, fan direction and wall line-of-sight.");
        }

        return activeCount;
    }

    private static int ValidateEnemies(
        List<string> errors,
        List<string> notes,
        out int receiverAcceptedPushes,
        out int motorStarts,
        out int motorCompletions,
        out int motorBlocked,
        out int activeDisplacements,
        out int activeReactions)
    {
        receiverAcceptedPushes = 0;
        motorStarts = 0;
        motorCompletions = 0;
        motorBlocked = 0;
        activeDisplacements = 0;
        activeReactions = 0;

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

            EnemyCombatReceiver[] receivers =
                context.GetComponents<EnemyCombatReceiver>();

            EnemyMotor2D[] motors =
                context.GetComponents<EnemyMotor2D>();

            EnemyStateMachine[] stateMachines =
                context.GetComponents<EnemyStateMachine>();

            if (receivers.Length != 1)
            {
                errors.Add(
                    context.gameObject.name +
                    ": expected one EnemyCombatReceiver, found " +
                    receivers.Length + ".");
            }

            if (motors.Length != 1)
            {
                errors.Add(
                    context.gameObject.name +
                    ": expected one EnemyMotor2D, found " +
                    motors.Length + ".");
            }

            if (stateMachines.Length != 1)
            {
                errors.Add(
                    context.gameObject.name +
                    ": expected one EnemyStateMachine, found " +
                    stateMachines.Length + ".");
            }

            if (receivers.Length == 1)
            {
                receivers[0].CollectValidationErrors(errors);
                receiverAcceptedPushes +=
                    receivers[0].AcceptedNonlethalPushCount;
            }

            if (motors.Length == 1)
            {
                EnemyMotor2D motor = motors[0];

                if (!motor.IsInitialized ||
                    motor.BodyCollider == null)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": collision-safe EnemyMotor2D is not fully initialized.");
                }

                motorStarts += motor.CombatDisplacementStartCount;
                motorCompletions +=
                    motor.CombatDisplacementCompleteCount;
                motorBlocked += motor.CombatDisplacementBlockedCount;

                if (motor.IsCombatDisplacementActive)
                {
                    activeDisplacements++;
                }
            }

            if (stateMachines.Length == 1 &&
                stateMachines[0].IsCombatReactionActive)
            {
                activeReactions++;
            }
        }

        if (activeCount == 0)
        {
            errors.Add(
                "No active runtime enemy exists. Wait for enemy generation.");
        }

        if (receiverAcceptedPushes > 0 && motorStarts == 0)
        {
            errors.Add(
                "A receiver accepted a push but no EnemyMotor2D displacement " +
                "start was recorded.");
        }

        if (motorBlocked > 0)
        {
            notes.Add(
                "At least one displacement was clipped by solid geometry. " +
                "This is expected when testing beside walls or blockers.");
        }

        if (motorStarts > 0 &&
            motorCompletions + motorBlocked == 0 &&
            activeDisplacements == 0)
        {
            notes.Add(
                "A displacement started but has not yet reported completion " +
                "or collision clipping. Rerun after a few fixed frames.");
        }

        return activeCount;
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.activeInHierarchy;
    }
}
