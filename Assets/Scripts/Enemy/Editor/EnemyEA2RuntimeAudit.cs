using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyEA2RuntimeAudit
{
    private static readonly string[] T4ExpectedShowcaseEnemyIds =
    {
        "dream_wanderer",
        "dream_scout",
        "dream_hunter",
        "dream_brute",
        "dream_gazer"
    };

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
        List<string> definitionBindings = new List<string>();
        HashSet<string> instanceIds = new HashSet<string>();

        Dictionary<EnemyRuntimeState, int> stateCounts =
            new Dictionary<EnemyRuntimeState, int>();

        Dictionary<string, int> definitionCounts =
            new Dictionary<string, int>();

        EnemySpawner activeSpawner =
            GetActiveSceneSpawner(errors);

        EnemyRuntimeContext[] allContexts =
            Resources.FindObjectsOfTypeAll<EnemyRuntimeContext>();

        int runtimeEnemyCount = 0;
        int initializedContextCount = 0;
        int initializedStateMachineCount = 0;
        int legacyAdapterCount = 0;
        int optionalTransitionLoggingCount = 0;
        int verifiedDefinitionBindingCount = 0;
        int extendedBehaviorMachineCount = 0;
        int extendedNavigationAgentCount = 0;
        int t5bRuntimeBindingCount = 0;
        int t5cRuntimeBindingCount = 0;
        int t6aFormalMeleeExpectedCount = 0;
        int t6aFormalMeleeBindingCount = 0;
        int totalFormalMeleeStarts = 0;
        int totalFormalMeleeCommits = 0;
        int totalFormalMeleeHits = 0;
        int totalFormalMeleeMisses = 0;
        int totalFormalMeleeRejected = 0;
        int totalFormalMeleeCompletes = 0;
        int totalFormalMeleeCancels = 0;
        int totalAlertBroadcasts = 0;
        int totalAlertRecipients = 0;
        int totalAlertsReceived = 0;

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

                if (stateMachine.UsesExtendedBehaviorLoop)
                {
                    extendedBehaviorMachineCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": T5A extended patrol/search/return loop is inactive.");
                }

                if (stateMachine.NavigationAgent != null &&
                    stateMachine.NavigationAgent.SupportsExtendedBehaviorStates)
                {
                    extendedNavigationAgentCount++;
                }
                else
                {
                    errors.Add(
                        context.gameObject.name +
                        ": T5A navigation agent does not support extended states.");
                }

                totalAlertBroadcasts +=
                    stateMachine.AlertBroadcastCount;
                totalAlertRecipients +=
                    stateMachine.AlertRecipientCount;
                totalAlertsReceived +=
                    stateMachine.AlertReceivedCount;

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

            if (AuditT4DefinitionBinding(
                    context,
                    errors,
                    definitionBindings,
                    definitionCounts))
            {
                verifiedDefinitionBindingCount++;
            }

            if (AuditT5BRuntimeBinding(
                    context,
                    errors))
            {
                t5bRuntimeBindingCount++;
            }

            if (AuditT5CRuntimeBinding(
                    context,
                    errors))
            {
                t5cRuntimeBindingCount++;
            }

            bool expectsFormalMelee =
                ExpectsFormalMelee(context.Definition);

            if (expectsFormalMelee)
            {
                t6aFormalMeleeExpectedCount++;
            }

            if (AuditT6AFormalMeleeBinding(
                    context,
                    errors,
                    out EnemyMeleeAttackController meleeController))
            {
                if (expectsFormalMelee)
                {
                    t6aFormalMeleeBindingCount++;
                }
            }

            if (meleeController != null)
            {
                totalFormalMeleeStarts +=
                    meleeController.AttackStartCount;
                totalFormalMeleeCommits +=
                    meleeController.DamageCommitCount;
                totalFormalMeleeHits +=
                    meleeController.DamageAcceptedCount;
                totalFormalMeleeMisses +=
                    meleeController.AttackMissCount;
                totalFormalMeleeRejected +=
                    meleeController.DamageRejectedCount;
                totalFormalMeleeCompletes +=
                    meleeController.AttackCompleteCount;
                totalFormalMeleeCancels +=
                    meleeController.AttackCancelCount;
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

        AuditT4SpawnModeContract(
            activeSpawner,
            runtimeEnemyCount,
            definitionCounts,
            errors,
            notes);

        notes.Add(
            "T5B verifies per-Definition view cones, motor-facing authority " +
            "and Maximum Chase Path Cost binding. A path-cost rejection " +
            "returns the enemy home and suppresses immediate reacquisition " +
            "until the target leaves perception.");

        notes.Add(
            "T5C verifies the shared EnemyManager alert roster and the Alert " +
            "state relay. A broadcast supplies only a last-known position; " +
            "each recipient still uses its own perception and movement profile.");

        notes.Add(
            "T6A verifies the formal melee Attack state, one timed damage " +
            "commit per sequence, independent Windup/Recovery/Cooldown values " +
            "and removal of runtime legacy contact damage for melee profiles.");

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
        report.AppendLine(
            "T4DefinitionBindings=" +
            verifiedDefinitionBindingCount + "/" + runtimeEnemyCount);
        report.AppendLine(
            "T5AExtendedBehaviorMachines=" +
            extendedBehaviorMachineCount + "/" + runtimeEnemyCount);
        report.AppendLine(
            "T5AExtendedNavigationAgents=" +
            extendedNavigationAgentCount + "/" + runtimeEnemyCount);
        report.AppendLine(
            "T5BRuntimeBindings=" +
            t5bRuntimeBindingCount + "/" + runtimeEnemyCount);
        report.AppendLine(
            "T5CAlertRuntimeBindings=" +
            t5cRuntimeBindingCount + "/" + runtimeEnemyCount);
        report.AppendLine(
            "T5CAlertActivity=Broadcasts:" + totalAlertBroadcasts +
            ", Recipients:" + totalAlertRecipients +
            ", Received:" + totalAlertsReceived);
        report.AppendLine(
            "T6AFormalMeleeBindings=" +
            t6aFormalMeleeBindingCount + "/" +
            t6aFormalMeleeExpectedCount);
        report.AppendLine(
            "T6AFormalMeleeActivity=Starts:" + totalFormalMeleeStarts +
            ", Commits:" + totalFormalMeleeCommits +
            ", Hits:" + totalFormalMeleeHits +
            ", Misses:" + totalFormalMeleeMisses +
            ", Rejected:" + totalFormalMeleeRejected +
            ", Completes:" + totalFormalMeleeCompletes +
            ", Cancels:" + totalFormalMeleeCancels);

        if (activeSpawner != null)
        {
            report.AppendLine(
                "SpawnMode=" + activeSpawner.SpawnMode);
        }

        for (int i = 0; i < definitionBindings.Count; i++)
        {
            report.AppendLine(
                "BINDING: " + definitionBindings[i]);
        }

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

    private static bool AuditT4DefinitionBinding(
        EnemyRuntimeContext context,
        List<string> errors,
        List<string> definitionBindings,
        Dictionary<string, int> definitionCounts)
    {
        if (context == null)
        {
            return false;
        }

        string prefix = context.gameObject.name + ": ";
        EnemyRuntimeIdentity identity = context.Identity;
        EnemyDefinition definition = context.Definition;
        bool valid = true;

        if (identity == null)
        {
            errors.Add(prefix + "T4 runtime identity is missing.");
            return false;
        }

        if (definition == null)
        {
            errors.Add(prefix + "T4 EnemyDefinition is missing.");
            return false;
        }

        if (identity.Definition != definition)
        {
            errors.Add(
                prefix +
                "T4 identity and runtime context reference different " +
                "EnemyDefinition assets.");
            valid = false;
        }

        if (context.EnemyId != definition.Id.Value ||
            identity.EnemyId != definition.Id)
        {
            errors.Add(
                prefix +
                "T4 EnemyId binding mismatch. Context=" +
                context.EnemyId + ", Identity=" + identity.EnemyId +
                ", Definition=" + definition.Id + ".");
            valid = false;
        }

        if (!context.gameObject.name.StartsWith(
                definition.DisplayName + "_"))
        {
            errors.Add(
                prefix +
                "T4 runtime name does not begin with Definition DisplayName '" +
                definition.DisplayName + "'.");
            valid = false;
        }

        IncrementCount(
            definitionCounts,
            definition.Id.Value);

        Health health = context.Health;

        if (health == null ||
            !Approximately(
                health.MaxHealth,
                definition.MaxHealth))
        {
            errors.Add(
                prefix +
                "T4 MaxHealth does not match Definition. Runtime=" +
                DescribeFloat(health != null ? health.MaxHealth : -1f) +
                ", Definition=" +
                DescribeFloat(definition.MaxHealth) + ".");
            valid = false;
        }

        EnemyMotor2D motor = context.Motor;

        if (motor == null ||
            !motor.IsInitialized ||
            !Approximately(
                motor.MoveSpeed,
                definition.MoveSpeed))
        {
            errors.Add(
                prefix +
                "T4 MoveSpeed does not match Definition. Runtime=" +
                DescribeFloat(motor != null ? motor.MoveSpeed : -1f) +
                ", Definition=" +
                DescribeFloat(definition.MoveSpeed) + ".");
            valid = false;
        }

        EnemyDetection detection = context.Detection;

        if (detection == null ||
            !Approximately(
                detection.DetectionRadius,
                definition.DetectionRadius) ||
            !Approximately(
                detection.LoseTargetRadius,
                definition.LoseTargetRadius) ||
            detection.RequiresLineOfSight !=
                definition.RequireLineOfSight ||
            detection.ObstacleMask.value !=
                definition.ObstacleMask.value)
        {
            errors.Add(
                prefix +
                "T4 perception settings do not match Definition.");
            valid = false;
        }

        BoxCollider2D[] colliders =
            context.GetComponents<BoxCollider2D>();

        if (colliders.Length != 1 ||
            !Approximately(
                colliders[0].size,
                definition.ColliderSize) ||
            !Approximately(
                colliders[0].offset,
                definition.ColliderOffset))
        {
            errors.Add(
                prefix +
                "T4 collider size/offset does not match Definition.");
            valid = false;
        }

        EnemyVisual visual = context.Visual;

        if (visual == null ||
            visual.Renderer == null ||
            !Approximately(
                visual.VisualWorldHeight,
                definition.VisualWorldHeight) ||
            !Approximately(
                visual.VisualOffset,
                definition.VisualOffset) ||
            !Approximately(
                visual.VisualColor,
                definition.VisualColor) ||
            !Approximately(
                visual.Renderer.color,
                definition.VisualColor) ||
            visual.SortingOrder != definition.SortingOrder)
        {
            errors.Add(
                prefix +
                "T4 presentation settings do not match Definition.");
            valid = false;
        }

        ContactDamage2D[] contactDamageComponents =
            context.GetComponents<ContactDamage2D>();

        if (definition.EnableLegacyContactDamage)
        {
            if (contactDamageComponents.Length != 1 ||
                !Approximately(
                    contactDamageComponents[0].Damage,
                    definition.LegacyContactDamage) ||
                !Approximately(
                    contactDamageComponents[0].HitCooldown,
                    definition.LegacyContactDamageCooldown) ||
                contactDamageComponents[0].TargetFactions !=
                    definition.LegacyContactDamageTargets)
            {
                errors.Add(
                    prefix +
                    "T4 legacy contact-damage settings do not match " +
                    "Definition.");
                valid = false;
            }
        }
        else if (contactDamageComponents.Length != 0)
        {
            errors.Add(
                prefix +
                "T4 contact damage exists although the Definition " +
                "disables it.");
            valid = false;
        }

        definitionBindings.Add(
            definition.Id.Value +
            " | Room=" + identity.RoomIndex +
            " | HP=" + DescribeFloat(definition.MaxHealth) +
            " | Speed=" + DescribeFloat(definition.MoveSpeed) +
            " | Patrol=" + definition.PatrolRadiusInCells +
            "@" + DescribeFloat(definition.PatrolPauseDuration) +
            " | Search=" + definition.SearchRadiusInCells +
            "@" + DescribeFloat(definition.SearchDuration) +
            " | Detect=" +
            DescribeFloat(definition.DetectionRadius) + "/" +
            DescribeFloat(definition.LoseTargetRadius) +
            " | View=" + DescribeFloat(definition.ViewAngle) +
            " | ChaseCost=" + definition.MaximumChasePathCost +
            " | Alert=" +
            (definition.BroadcastsAlert
                ? DescribeFloat(definition.AlertRadius) +
                  "@" +
                  DescribeFloat(definition.AlertBroadcastCooldown)
                : "Off") +
            " | FormalAttack=" +
            (definition.AttackMode == EnemyAttackMode.Melee
                ? DescribeFloat(definition.AttackDamage) +
                  "@" + DescribeFloat(definition.AttackRange) +
                  "/" + DescribeFloat(definition.AttackWindup) +
                  "/" + DescribeFloat(definition.AttackRecovery) +
                  "/" + DescribeFloat(definition.AttackCooldown)
                : "Projectile") +
            " | Contact=" +
            (definition.EnableLegacyContactDamage
                ? DescribeFloat(definition.LegacyContactDamage)
                : "Off") +
            " | Height=" +
            DescribeFloat(definition.VisualWorldHeight) +
            " | Color=#" +
            ColorUtility.ToHtmlStringRGBA(definition.VisualColor));

        return valid;
    }

    private static bool AuditT5BRuntimeBinding(
        EnemyRuntimeContext context,
        List<string> errors)
    {
        if (context == null || context.Definition == null)
        {
            return false;
        }

        string prefix = context.gameObject.name + ": ";
        EnemyDefinition definition = context.Definition;
        EnemyDetection detection = context.Detection;
        EnemyMotor2D motor = context.Motor;
        EnemyNavigationAgent navigationAgent =
            context.NavigationAgent;

        bool valid = true;

        if (detection == null ||
            !Approximately(
                detection.ViewAngle,
                definition.ViewAngle))
        {
            errors.Add(
                prefix +
                "T5B View Angle runtime binding does not match Definition.");
            valid = false;
        }

        if (detection == null ||
            detection.FacingSource != motor)
        {
            errors.Add(
                prefix +
                "T5B EnemyDetection does not use the authoritative " +
                "EnemyMotor2D facing source.");
            valid = false;
        }

        if (motor == null ||
            motor.FacingDirection.sqrMagnitude <= 0.99f)
        {
            errors.Add(
                prefix +
                "T5B motor facing direction is unavailable or not normalized.");
            valid = false;
        }

        if (navigationAgent == null ||
            navigationAgent.ConfiguredMaximumPathCostInCells !=
                definition.MaximumChasePathCost)
        {
            errors.Add(
                prefix +
                "T5B Maximum Chase Path Cost runtime binding mismatch. " +
                "Runtime=" +
                (navigationAgent != null
                    ? navigationAgent.ConfiguredMaximumPathCostInCells
                    : -1) +
                ", Definition=" +
                definition.MaximumChasePathCost + ".");
            valid = false;
        }

        EnemyStateMachine stateMachine =
            context.GetComponent<EnemyStateMachine>();

        if (stateMachine == null)
        {
            errors.Add(
                prefix +
                "T5B chase-leash state owner is missing.");
            valid = false;
        }

        return valid;
    }

    private static bool AuditT5CRuntimeBinding(
        EnemyRuntimeContext context,
        List<string> errors)
    {
        if (context == null || context.Definition == null)
        {
            return false;
        }

        string prefix = context.gameObject.name + ": ";
        EnemyStateMachine stateMachine =
            context.GetComponent<EnemyStateMachine>();

        if (stateMachine == null ||
            !stateMachine.SupportsLocalAlertRelay)
        {
            errors.Add(
                prefix +
                "T5C local alert state owner is missing or inactive.");
            return false;
        }

        if (stateMachine.AlertManager == null)
        {
            errors.Add(
                prefix +
                "T5C state machine is not bound to the active EnemyManager roster.");
            return false;
        }

        if (context.Definition.BroadcastsAlert &&
            context.Definition.AlertRadius <= 0f)
        {
            errors.Add(
                prefix +
                "T5C broadcaster has a non-positive Alert Radius.");
            return false;
        }

        return true;
    }

    private static bool ExpectsFormalMelee(
        EnemyDefinition definition)
    {
        return definition != null &&
               definition.AttackMode == EnemyAttackMode.Melee &&
               definition.AttackDamage > 0f &&
               definition.AttackRange > 0f;
    }

    private static bool AuditT6AFormalMeleeBinding(
        EnemyRuntimeContext context,
        List<string> errors,
        out EnemyMeleeAttackController controller)
    {
        controller = null;

        if (context == null || context.Definition == null)
        {
            return false;
        }

        string prefix = context.gameObject.name + ": ";
        EnemyDefinition definition = context.Definition;
        bool expectsFormalMelee = ExpectsFormalMelee(definition);
        EnemyMeleeAttackController[] controllers =
            context.GetComponents<EnemyMeleeAttackController>();
        ContactDamage2D[] contactDamageComponents =
            context.GetComponents<ContactDamage2D>();

        if (!expectsFormalMelee)
        {
            if (controllers.Length != 0)
            {
                errors.Add(
                    prefix +
                    "T6A non-melee profile has a formal melee controller.");
                return false;
            }

            return true;
        }

        if (controllers.Length != 1)
        {
            errors.Add(
                prefix +
                "T6A expected exactly one EnemyMeleeAttackController, found " +
                controllers.Length + ".");
            return false;
        }

        controller = controllers[0];
        bool valid = true;

        if (!controller.IsInitialized ||
            controller.Context != context)
        {
            errors.Add(
                prefix +
                "T6A formal melee controller is not initialized against " +
                "its runtime context.");
            valid = false;
        }

        EnemyStateMachine stateMachine =
            context.GetComponent<EnemyStateMachine>();

        if (stateMachine == null ||
            stateMachine.MeleeAttackController != controller ||
            !stateMachine.SupportsFormalMeleeAttack)
        {
            errors.Add(
                prefix +
                "T6A state machine is not bound to the formal melee controller.");
            valid = false;
        }

        if (!Approximately(
                controller.ConfiguredDamage,
                definition.AttackDamage) ||
            !Approximately(
                controller.ConfiguredRange,
                definition.AttackRange) ||
            !Approximately(
                controller.ConfiguredWindup,
                definition.AttackWindup) ||
            !Approximately(
                controller.ConfiguredRecovery,
                definition.AttackRecovery) ||
            !Approximately(
                controller.ConfiguredCooldown,
                definition.AttackCooldown))
        {
            errors.Add(
                prefix +
                "T6A runtime attack values do not match EnemyDefinition.");
            valid = false;
        }

        if (contactDamageComponents.Length != 0)
        {
            errors.Add(
                prefix +
                "T6A formal melee profile still has legacy ContactDamage2D.");
            valid = false;
        }

        if (definition.EnableLegacyContactDamage)
        {
            errors.Add(
                prefix +
                "T6A formal melee Definition still enables legacy contact damage.");
            valid = false;
        }

        if (controller.TargetHealth == null)
        {
            errors.Add(
                prefix +
                "T6A formal melee target Health reference is missing.");
            valid = false;
        }

        return valid;
    }

    private static void AuditT4SpawnModeContract(
        EnemySpawner activeSpawner,
        int runtimeEnemyCount,
        Dictionary<string, int> definitionCounts,
        List<string> errors,
        List<string> notes)
    {
        if (activeSpawner == null)
        {
            return;
        }

        if (activeSpawner.SpawnMode ==
            EnemySpawnMode.TemporaryFiveTypeShowcase)
        {
            if (runtimeEnemyCount !=
                T4ExpectedShowcaseEnemyIds.Length)
            {
                errors.Add(
                    "T4 showcase requires exactly five runtime enemies. " +
                    "Actual=" + runtimeEnemyCount + ".");
            }

            for (int i = 0;
                 i < T4ExpectedShowcaseEnemyIds.Length;
                 i++)
            {
                string expectedId =
                    T4ExpectedShowcaseEnemyIds[i];

                int count = definitionCounts.TryGetValue(
                    expectedId,
                    out int foundCount)
                        ? foundCount
                        : 0;

                if (count != 1)
                {
                    errors.Add(
                        "T4 showcase requires one '" + expectedId +
                        "' instance. Actual=" + count + ".");
                }
            }

            notes.Add(
                "T4 showcase verifies five independent Definition " +
                "bindings. GameplayRole is descriptive only; runtime " +
                "values come directly from each EnemyDefinition asset.");

            return;
        }

        int expectedBaselineCount =
            activeSpawner.ConfiguredEnemyCount;

        if (runtimeEnemyCount != expectedBaselineCount)
        {
            errors.Add(
                "T4 baseline runtime count mismatch. Expected=" +
                expectedBaselineCount + ", Actual=" +
                runtimeEnemyCount + ".");
        }

        EnemyDefinition baselineDefinition =
            activeSpawner.DefaultEnemyDefinition;

        if (baselineDefinition != null)
        {
            int baselineCount = definitionCounts.TryGetValue(
                baselineDefinition.Id.Value,
                out int foundCount)
                    ? foundCount
                    : 0;

            if (baselineCount != runtimeEnemyCount ||
                definitionCounts.Count !=
                    (runtimeEnemyCount > 0 ? 1 : 0))
            {
                errors.Add(
                    "T4 baseline mode contains a runtime Definition " +
                    "other than Default Enemy Definition '" +
                    baselineDefinition.Id + "'.");
            }
        }

        notes.Add(
            "T4 baseline verifies that every runtime enemy reads the " +
            "selected Default Enemy Definition without role-based overrides.");
    }

    private static EnemySpawner GetActiveSceneSpawner(
        List<string> errors)
    {
        EnemySpawner[] spawners =
            Resources.FindObjectsOfTypeAll<EnemySpawner>();

        EnemySpawner activeSpawner = null;
        int activeCount = 0;

        for (int i = 0; i < spawners.Length; i++)
        {
            if (!IsActiveSceneObject(spawners[i]))
            {
                continue;
            }

            activeCount++;

            if (activeSpawner == null)
            {
                activeSpawner = spawners[i];
            }
        }

        if (activeCount == 0)
        {
            errors.Add(
                "No active EnemySpawner was found in the loaded scene.");
        }
        else if (activeCount > 1)
        {
            errors.Add(
                "Expected one active EnemySpawner, found " +
                activeCount + ".");
        }

        return activeSpawner;
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

    private static void IncrementCount(
        Dictionary<string, int> counts,
        string key)
    {
        if (!counts.TryGetValue(key, out int count))
        {
            count = 0;
        }

        counts[key] = count + 1;
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

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Approximately(a.x, b.x) &&
               Approximately(a.y, b.y);
    }

    private static bool Approximately(Color a, Color b)
    {
        return Approximately(a.r, b.r) &&
               Approximately(a.g, b.g) &&
               Approximately(a.b, b.b) &&
               Approximately(a.a, b.a);
    }

    private static string DescribeFloat(float value)
    {
        return value.ToString("0.###");
    }
}
