using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyEA2RuntimeAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Enemy System/Run EA2 Runtime Audit")]
    public static void RunAudit()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[Enemy System/EA2] Runtime Audit requires Play Mode. " +
                "Enter Play Mode, wait for the floor to spawn, then run it.");

            return;
        }

        List<string> errors = new List<string>();
        List<string> notes = new List<string>();
        HashSet<string> instanceIds = new HashSet<string>();

        Dictionary<EnemyRuntimeState, int> stateCounts =
            new Dictionary<EnemyRuntimeState, int>();

        EnemyRuntimeContext[] allContexts =
            Resources.FindObjectsOfTypeAll<EnemyRuntimeContext>();

        int runtimeEnemyCount = 0;
        int initializedContextCount = 0;
        int initializedStateMachineCount = 0;
        int legacyAdapterCount = 0;
        int optionalTransitionLoggingCount = 0;

        for (int i = 0; i < allContexts.Length; i++)
        {
            EnemyRuntimeContext context = allContexts[i];

            if (!IsActiveSceneObject(context))
            {
                continue;
            }

            runtimeEnemyCount++;
            context.CollectValidationErrors(errors);

            if (context.IsInitialized)
            {
                initializedContextCount++;
            }

            EnemyRuntimeIdentity identity = context.Identity;

            if (identity != null &&
                !instanceIds.Add(identity.InstanceId))
            {
                errors.Add(
                    context.gameObject.name +
                    ": duplicate runtime InstanceId " +
                    identity.InstanceId + ".");
            }

            EnemyStateMachine[] stateMachines =
                context.GetComponents<EnemyStateMachine>();

            TestEnemyAI[] legacyAdapters =
                context.GetComponents<TestEnemyAI>();

            if (stateMachines.Length != 1)
            {
                errors.Add(
                    context.gameObject.name +
                    ": expected exactly one EnemyStateMachine, found " +
                    stateMachines.Length + ".");
            }

            if (legacyAdapters.Length != 1)
            {
                errors.Add(
                    context.gameObject.name +
                    ": expected exactly one EA2 legacy chase adapter, found " +
                    legacyAdapters.Length + ".");
            }

            if (legacyAdapters.Length == 1)
            {
                legacyAdapterCount++;

                if (!legacyAdapters[0].IsInitialized ||
                    legacyAdapters[0].Context != context)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": legacy chase adapter is not initialized against " +
                        "its runtime context.");
                }
            }

            if (stateMachines.Length == 1)
            {
                EnemyStateMachine stateMachine =
                    stateMachines[0];

                if (stateMachine.IsInitialized)
                {
                    initializedStateMachineCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": EnemyStateMachine is not initialized.");
                }

                if (stateMachine.Context != context)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": EnemyStateMachine references a different context.");
                }

                if (stateMachine.CurrentState ==
                    EnemyRuntimeState.Spawn)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": state is still Spawn after floor setup.");
                }

                if (stateMachine.TransitionCount < 1)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": no initialization state transition was recorded.");
                }

                if (stateMachine.CurrentState ==
                        EnemyRuntimeState.Dead &&
                    context.Health != null &&
                    !context.Health.IsDead)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": state is Dead while Health is alive.");
                }

                if (stateMachine.LogsStateTransitions)
                {
                    optionalTransitionLoggingCount++;
                }

                AddStateCount(
                    stateCounts,
                    stateMachine.CurrentState);
            }

            if (context.Definition != null &&
                context.Definition.AnimationProfile != null &&
                (context.Visual == null ||
                 context.Visual.Animator == null))
            {
                errors.Add(
                    context.gameObject.name +
                    ": CA1 DirectionalSpriteAnimator is missing.");
            }
        }

        if (runtimeEnemyCount == 0)
        {
            errors.Add(
                "No active EnemyRuntimeContext was found. " +
                "Wait for GameScene floor generation before running the audit.");
        }

        int managerEnemyCount = GetActiveManagerEnemyCount(
            out int activeManagerCount);

        if (activeManagerCount == 0)
        {
            errors.Add("No active EnemyManager was found in the loaded scene.");
        }
        else if (managerEnemyCount != runtimeEnemyCount)
        {
            errors.Add(
                "EnemyManager active count (" + managerEnemyCount +
                ") does not match runtime context count (" +
                runtimeEnemyCount + ").");
        }

        if (optionalTransitionLoggingCount > 0)
        {
            notes.Add(
                optionalTransitionLoggingCount +
                " enemy instance(s) have optional transition logging enabled. " +
                "This logs changes only, never per-frame updates.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Enemy System/EA2] Runtime Audit");
        report.AppendLine("RuntimeEnemies=" + runtimeEnemyCount);
        report.AppendLine(
            "InitializedContexts=" + initializedContextCount);
        report.AppendLine(
            "InitializedStateMachines=" +
            initializedStateMachineCount);
        report.AppendLine(
            "LegacyChaseAdapters=" + legacyAdapterCount);
        report.AppendLine(
            "EnemyManagerActive=" + managerEnemyCount);
        report.AppendLine(
            "States=" + FormatStateCounts(stateCounts));

        for (int i = 0; i < notes.Count; i++)
        {
            report.AppendLine("NOTE: " + notes[i]);
        }

        if (errors.Count == 0)
        {
            report.AppendLine("Result=PASS");
            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine("Result=FAIL");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("ERROR: " + errors[i]);
        }

        Debug.LogError(report.ToString());
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.activeInHierarchy;
    }

    private static int GetActiveManagerEnemyCount(
        out int activeManagerCount)
    {
        EnemyManager[] managers =
            Resources.FindObjectsOfTypeAll<EnemyManager>();

        int enemyCount = 0;
        activeManagerCount = 0;

        for (int i = 0; i < managers.Length; i++)
        {
            if (!IsActiveSceneObject(managers[i]))
            {
                continue;
            }

            activeManagerCount++;
            enemyCount += managers[i].ActiveEnemyCount;
        }

        return enemyCount;
    }

    private static void AddStateCount(
        Dictionary<EnemyRuntimeState, int> counts,
        EnemyRuntimeState state)
    {
        if (!counts.TryGetValue(state, out int count))
        {
            count = 0;
        }

        counts[state] = count + 1;
    }

    private static string FormatStateCounts(
        Dictionary<EnemyRuntimeState, int> counts)
    {
        if (counts.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();

        foreach (KeyValuePair<EnemyRuntimeState, int> pair in counts)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }
}
