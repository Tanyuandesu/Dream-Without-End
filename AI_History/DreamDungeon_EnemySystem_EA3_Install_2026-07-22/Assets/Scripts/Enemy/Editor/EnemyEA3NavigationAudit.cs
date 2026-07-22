using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyEA3NavigationAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Enemy System/Run EA3 Navigation Audit")]
    public static void RunAudit()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[Enemy System/EA3] Navigation Audit requires Play Mode. " +
                "Enter Play Mode, wait for the floor and enemies to spawn, " +
                "then run it.");

            return;
        }

        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        Dictionary<EnemyNavigationStatus, int> statusCounts =
            new Dictionary<EnemyNavigationStatus, int>();

        HashSet<EnemyPathService> usedServices =
            new HashSet<EnemyPathService>();

        EnemyRuntimeContext[] allContexts =
            Resources.FindObjectsOfTypeAll<EnemyRuntimeContext>();

        int runtimeEnemyCount = 0;
        int initializedAgentCount = 0;
        int initializedMotorCount = 0;
        int initializedPathfinderCount = 0;
        int initializedBridgeCount = 0;
        int stateMachineLinkCount = 0;
        int activeFailureCount = 0;
        int immediateProbeCount = 0;

        for (int i = 0; i < allContexts.Length; i++)
        {
            EnemyRuntimeContext context = allContexts[i];

            if (!IsActiveSceneObject(context))
            {
                continue;
            }

            runtimeEnemyCount++;
            context.CollectValidationErrors(errors);

            EnemyNavigationAgent[] agents =
                context.GetComponents<EnemyNavigationAgent>();

            EnemyMotor2D[] motors =
                context.GetComponents<EnemyMotor2D>();

            EnemyPathfinder[] pathfinders =
                context.GetComponents<EnemyPathfinder>();

            TestEnemyAI[] bridges =
                context.GetComponents<TestEnemyAI>();

            EnemyStateMachine[] stateMachines =
                context.GetComponents<EnemyStateMachine>();

            ValidateSingleComponent(
                context,
                "EnemyNavigationAgent",
                agents.Length,
                errors);

            ValidateSingleComponent(
                context,
                "EnemyMotor2D",
                motors.Length,
                errors);

            ValidateSingleComponent(
                context,
                "EnemyPathfinder facade",
                pathfinders.Length,
                errors);

            ValidateSingleComponent(
                context,
                "TestEnemyAI compatibility bridge",
                bridges.Length,
                errors);

            ValidateSingleComponent(
                context,
                "EnemyStateMachine",
                stateMachines.Length,
                errors);

            if (agents.Length == 1)
            {
                EnemyNavigationAgent agent = agents[0];

                if (agent.IsInitialized)
                {
                    initializedAgentCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": EnemyNavigationAgent is not initialized.");
                }

                if (agent.Context != context)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": navigation agent references a different context.");
                }

                if (agent.PathService == null)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": navigation agent has no shared path service.");
                }
                else
                {
                    usedServices.Add(agent.PathService);
                }

                if (agent.NavigationStatus ==
                        EnemyNavigationStatus.Failed)
                {
                    activeFailureCount++;
                    errors.Add(
                        context.gameObject.name +
                        ": navigation is Failed. Reason=" +
                        agent.LastFailureReason +
                        " | Details=" +
                        agent.LastFailureDetails);
                }

                AddStatusCount(
                    statusCounts,
                    agent.NavigationStatus);
            }

            if (motors.Length == 1)
            {
                EnemyMotor2D motor = motors[0];

                if (motor.IsInitialized)
                {
                    initializedMotorCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": EnemyMotor2D is not initialized.");
                }

                if (motor.Body != context.Body)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": motor and context use different Rigidbody2D instances.");
                }
            }

            if (pathfinders.Length == 1)
            {
                EnemyPathfinder pathfinder = pathfinders[0];

                if (agents.Length == 1 &&
                    agents[0].Pathfinder == pathfinder)
                {
                    initializedPathfinderCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": navigation agent does not reference its local " +
                        "EnemyPathfinder facade.");
                }
            }

            if (bridges.Length == 1)
            {
                TestEnemyAI bridge = bridges[0];

                if (bridge.IsInitialized)
                {
                    initializedBridgeCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": TestEnemyAI compatibility bridge is not initialized.");
                }

                SerializedObject serializedBridge =
                    new SerializedObject(bridge);

                SerializedProperty bridgeAgentProperty =
                    serializedBridge.FindProperty("navigationAgent");

                if (agents.Length == 1 &&
                    (bridgeAgentProperty == null ||
                     bridgeAgentProperty.objectReferenceValue != agents[0]))
                {
                    errors.Add(
                        context.gameObject.name +
                        ": compatibility bridge does not forward to the EA3 agent.");
                }
            }

            if (stateMachines.Length == 1 && agents.Length == 1)
            {
                SerializedObject serializedStateMachine =
                    new SerializedObject(stateMachines[0]);

                SerializedProperty stateMachineAgentProperty =
                    serializedStateMachine.FindProperty("navigationAgent");

                if (stateMachineAgentProperty != null &&
                    stateMachineAgentProperty.objectReferenceValue == agents[0])
                {
                    stateMachineLinkCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": state machine does not reference the EA3 agent.");
                }
            }

            if (agents.Length == 1 &&
                agents[0].PathService != null &&
                context.Body != null &&
                context.CurrentTarget != null)
            {
                EnemyPathResult probe =
                    agents[0].PathService.FindPathImmediate(
                        context.Body.position,
                        context.CurrentTarget.position);

                immediateProbeCount++;

                if (!probe.Success)
                {
                    errors.Add(
                        context.gameObject.name +
                        ": immediate floor-connectivity probe failed. Reason=" +
                        probe.FailureReason +
                        " | Details=" + probe.Details);
                }
            }
        }

        if (runtimeEnemyCount == 0)
        {
            errors.Add(
                "No active enemy runtime context exists. Wait for floor " +
                "generation before running the audit.");
        }

        EnemyPathService[] allServices =
            Resources.FindObjectsOfTypeAll<EnemyPathService>();

        int activeServiceCount = 0;
        EnemyPathService activeService = null;

        for (int i = 0; i < allServices.Length; i++)
        {
            if (!IsActiveSceneObject(allServices[i]))
            {
                continue;
            }

            activeServiceCount++;
            activeService = allServices[i];
        }

        if (activeServiceCount != 1)
        {
            errors.Add(
                "Expected exactly one active EnemyPathService for the " +
                "generated floor, found " + activeServiceCount + ".");
        }

        if (usedServices.Count != 1)
        {
            errors.Add(
                "All enemies must share exactly one path service; unique " +
                "agent services=" + usedServices.Count + ".");
        }

        if (activeService != null)
        {
            if (!activeService.IsInitialized)
            {
                errors.Add("The active EnemyPathService is not initialized.");
            }

            if (activeService.Topology !=
                EnemyNavigationTopology.FourDirections)
            {
                errors.Add(
                    "EA3 baseline topology must be FourDirections; current=" +
                    activeService.Topology + ".");
            }

            if (activeService.SimplifiesCollinearWaypoints)
            {
                errors.Add(
                    "EA3 baseline must keep explicit cell-center waypoints; " +
                    "collinear simplification is currently enabled.");
            }

            if (activeService.WalkableCellCount == 0)
            {
                errors.Add("EnemyPathService contains zero FloorCells.");
            }

            if (activeService.ConnectedComponentCount != 1)
            {
                errors.Add(
                    "Current floor must be one reachable navigation component; " +
                    "components=" +
                    activeService.ConnectedComponentCount + ".");
            }

            if (activeService.TotalProcessedRequests == 0)
            {
                notes.Add(
                    "No queued runtime query has completed yet. Move close " +
                    "enough to trigger Chase if you want request counters.");
            }
        }

        int managerEnemyCount = GetActiveManagerEnemyCount(
            out int activeManagerCount);

        if (activeManagerCount == 0)
        {
            errors.Add("No active EnemyManager was found.");
        }
        else if (managerEnemyCount != runtimeEnemyCount)
        {
            errors.Add(
                "EnemyManager active count (" + managerEnemyCount +
                ") does not match runtime enemies (" +
                runtimeEnemyCount + ").");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Enemy System/EA3] Navigation Audit");
        report.AppendLine("RuntimeEnemies=" + runtimeEnemyCount);
        report.AppendLine(
            "InitializedAgents=" + initializedAgentCount);
        report.AppendLine(
            "InitializedMotors=" + initializedMotorCount);
        report.AppendLine(
            "InitializedPathfinderFacades=" +
            initializedPathfinderCount);
        report.AppendLine(
            "InitializedCompatibilityBridges=" +
            initializedBridgeCount);
        report.AppendLine(
            "StateMachineNavigationLinks=" +
            stateMachineLinkCount);
        report.AppendLine(
            "SharedPathServices=" + activeServiceCount);
        report.AppendLine(
            "UniqueAgentServices=" + usedServices.Count);
        report.AppendLine(
            "Topology=" +
            (activeService != null
                ? activeService.Topology.ToString()
                : "None"));
        report.AppendLine(
            "FloorCells=" +
            (activeService != null
                ? activeService.WalkableCellCount
                : 0) +
            " | Components=" +
            (activeService != null
                ? activeService.ConnectedComponentCount
                : 0));
        report.AppendLine(
            "QueuedQueries=" +
            (activeService != null
                ? activeService.QueuedRequestCount
                : 0) +
            " | PeakQueued=" +
            (activeService != null
                ? activeService.PeakQueuedRequestCount
                : 0) +
            " | Processed=" +
            (activeService != null
                ? activeService.TotalProcessedRequests
                : 0) +
            " | Succeeded=" +
            (activeService != null
                ? activeService.TotalSuccessfulRequests
                : 0) +
            " | Failed=" +
            (activeService != null
                ? activeService.TotalFailedRequests
                : 0) +
            " | Cancelled=" +
            (activeService != null
                ? activeService.TotalCancelledRequests
                : 0));
        report.AppendLine(
            "ImmediateConnectivityProbes=" + immediateProbeCount);
        report.AppendLine(
            "Statuses=" + FormatStatusCounts(statusCounts));
        report.AppendLine("ActiveFailures=" + activeFailureCount);
        report.AppendLine("EnemyManagerActive=" + managerEnemyCount);

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

    private static void ValidateSingleComponent(
        EnemyRuntimeContext context,
        string componentName,
        int count,
        List<string> errors)
    {
        if (count == 1)
        {
            return;
        }

        errors.Add(
            context.gameObject.name +
            ": expected exactly one " + componentName +
            ", found " + count + ".");
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

    private static void AddStatusCount(
        Dictionary<EnemyNavigationStatus, int> counts,
        EnemyNavigationStatus status)
    {
        if (!counts.TryGetValue(status, out int count))
        {
            count = 0;
        }

        counts[status] = count + 1;
    }

    private static string FormatStatusCounts(
        Dictionary<EnemyNavigationStatus, int> counts)
    {
        if (counts.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();

        foreach (KeyValuePair<EnemyNavigationStatus, int> pair in counts)
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
