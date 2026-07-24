using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the CB0 combat boundaries without executing a player attack.
/// Asset checks are available in Edit Mode; runtime wiring checks require
/// Play Mode after the floor and enemies have spawned.
/// </summary>
public static class CombatCB0ContractAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB0 Contract Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        int definitionCount = ValidateEnemyDefinitions(
            errors,
            notes);

        ValidateLoadedPlayerSpawners(errors, notes);

        int runtimeEnemyCount = 0;
        int runtimePlayerCount = 0;

        if (EditorApplication.isPlaying)
        {
            runtimePlayerCount = ValidateRuntimePlayer(
                errors,
                notes);

            runtimeEnemyCount = ValidateRuntimeEnemies(
                errors,
                notes);
        }
        else
        {
            notes.Add(
                "Edit Mode checked assets and loaded scene references. " +
                "Enter Play Mode after floor generation and run this audit " +
                "again to verify runtime player/enemy wiring.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB0] Contract Audit");
        report.AppendLine(
            "Enemy Definitions=" + definitionCount +
            " | Runtime Players=" + runtimePlayerCount +
            " | Runtime Enemies=" + runtimeEnemyCount);

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
                "PASS: CB0 contracts are present and baseline combat " +
                "input remains inactive.");

            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine("Errors:");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("- " + errors[i]);
        }

        report.AppendLine(
            "FAIL: Fix the errors above before starting CB1.");

        Debug.LogError(report.ToString());
    }

    private static int ValidateEnemyDefinitions(
        List<string> errors,
        List<string> notes)
    {
        string[] definitionGuids =
            AssetDatabase.FindAssets("t:EnemyDefinition");

        if (definitionGuids.Length == 0)
        {
            errors.Add(
                "No EnemyDefinition assets were found.");

            return 0;
        }

        for (int i = 0; i < definitionGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(
                definitionGuids[i]);

            EnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);

            if (definition == null)
            {
                errors.Add(
                    path + ": failed to load EnemyDefinition.");
                continue;
            }

            List<string> definitionErrors =
                new List<string>();

            definition.CollectValidationErrors(
                definitionErrors);

            for (int j = 0; j < definitionErrors.Count; j++)
            {
                errors.Add(definitionErrors[j]);
            }

            KnockbackResistanceSettings resistance =
                definition.KnockbackResistance;

            if (resistance == null)
            {
                errors.Add(
                    definition.name +
                    ": Knockback Resistance is missing.");

                continue;
            }

            if (resistance.DecayTierCount <
                KnockbackResistanceSettings.MinimumDecayTierCount)
            {
                errors.Add(
                    definition.name +
                    ": expected at least " +
                    KnockbackResistanceSettings.MinimumDecayTierCount +
                    " repeated-knockback decay tiers, found " +
                    resistance.DecayTierCount + ".");
            }
        }

        notes.Add(
            "Every EnemyDefinition owns independent knockback decay and " +
            "post-knockback pursuit settings. CB0 stores but does not yet " +
            "execute those values.");

        return definitionGuids.Length;
    }

    private static void ValidateLoadedPlayerSpawners(
        List<string> errors,
        List<string> notes)
    {
        PlayerSpawner[] spawners =
            Resources.FindObjectsOfTypeAll<PlayerSpawner>();

        int loadedSceneSpawnerCount = 0;

        for (int i = 0; i < spawners.Length; i++)
        {
            if (!IsActiveSceneObject(spawners[i]))
            {
                continue;
            }

            loadedSceneSpawnerCount++;
        }

        if (loadedSceneSpawnerCount == 0)
        {
            notes.Add(
                "No active PlayerSpawner exists in the currently loaded " +
                "scene. Open GameScene for the full scene audit.");
        }
        else if (loadedSceneSpawnerCount > 1)
        {
            errors.Add(
                "Expected one active PlayerSpawner in the loaded scene, " +
                "found " + loadedSceneSpawnerCount + ".");
        }
    }

    private static int ValidateRuntimePlayer(
        List<string> errors,
        List<string> notes)
    {
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

            if (controller.CombatInputEnabled)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": CB0 combat input must remain disabled.");
            }
        }

        if (activeCount != 1)
        {
            errors.Add(
                "Expected exactly one active PlayerCombatController after " +
                "player spawn, found " + activeCount + ".");
        }
        else
        {
            notes.Add(
                "Player combat boundary is initialized; left/right mouse " +
                "actions remain intentionally inactive in CB0.");
        }

        return activeCount;
    }

    private static int ValidateRuntimeEnemies(
        List<string> errors,
        List<string> notes)
    {
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

            EnemyMotor2D[] motors =
                context.GetComponents<EnemyMotor2D>();

            EnemyStateMachine[] stateMachines =
                context.GetComponents<EnemyStateMachine>();

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

            if (motors.Length == 1)
            {
                EnemyMotor2D motor = motors[0];

                if (!motor.IsInitialized)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": combat-capable EnemyMotor2D is not initialized.");
                }

                if (motor.IsCombatDisplacementActive)
                {
                    notes.Add(
                        context.gameObject.name +
                        ": a combat displacement is active during audit.");
                }
            }

            if (stateMachines.Length == 1)
            {
                EnemyStateMachine stateMachine =
                    stateMachines[0];

                if (!stateMachine.IsInitialized)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": EnemyStateMachine is not initialized.");
                }

                if (motors.Length == 1 &&
                    stateMachine.Motor != motors[0])
                {
                    errors.Add(
                        context.gameObject.name +
                        ": state machine and navigation do not share the " +
                        "same EnemyMotor2D.");
                }

                if (stateMachine.IsCombatReactionActive)
                {
                    notes.Add(
                        context.gameObject.name +
                        ": Hit/Stunned reaction is active during audit.");
                }
            }

            EnemyDefinition definition = context.Definition;

            if (definition != null &&
                definition.KnockbackResistance.DecayTierCount <
                KnockbackResistanceSettings.MinimumDecayTierCount)
            {
                errors.Add(
                    context.gameObject.name +
                    ": runtime definition has fewer than three " +
                    "knockback decay tiers.");
            }
        }

        if (activeCount == 0)
        {
            errors.Add(
                "No active EnemyRuntimeContext exists. Wait for floor and " +
                "enemy generation before running the Play Mode audit.");
        }
        else
        {
            notes.Add(
                "Enemy Hit/Stunned interruption and motor-owned combat " +
                "displacement boundaries are available on every runtime enemy.");
        }

        return activeCount;
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.scene.isLoaded &&
               component.gameObject.activeInHierarchy;
    }
}
