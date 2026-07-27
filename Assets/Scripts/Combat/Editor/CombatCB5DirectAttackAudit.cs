using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB5 facing-based direct damage. In Play Mode, damage at least one
/// enemy with right mouse, K and X, quickly repeat one input to observe the
/// independent cooldown, and place one in-range enemy behind the player so the
/// facing fan rejects it.
/// </summary>
public static class CombatCB5DirectAttackAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB5 Direct Attack Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        ValidateLoadedSpawners(errors, notes);

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine("[Combat/CB5] Direct Attack Audit");
            AppendNotes(editReport, notes);

            if (errors.Count == 0)
            {
                editReport.AppendLine(
                    "PASS: Loaded CB5 player damage settings and bindings are valid. " +
                    "Enter Play Mode to observe damage, facing and timing.");
                Debug.Log(editReport.ToString());
            }
            else
            {
                AppendErrors(editReport, errors);
                editReport.AppendLine(
                    "FAIL: Fix CB5 authoring errors before Play Mode testing.");
                Debug.LogError(editReport.ToString());
            }

            return;
        }

        RuntimeObservation observation = RuntimeObservation.CreateEmpty();

        PlayerCombatController[] controllers =
            Resources.FindObjectsOfTypeAll<PlayerCombatController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerCombatController controller = controllers[i];

            if (!IsActiveSceneObject(controller))
            {
                continue;
            }

            observation.RuntimePlayers++;
            controller.CollectValidationErrors(errors);

            DirectAttackSettings settings =
                controller.DirectAttackSettings;

            if (settings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": runtime Direct Attack settings are missing.");
                continue;
            }

            observation.Damage = settings.Damage;
            observation.Range = settings.Range;
            observation.ArcAngle = settings.ArcAngle;
            observation.MaximumTargets = settings.MaximumTargets;
            observation.Cooldown = settings.CooldownDuration;
            observation.Afterlag = settings.AfterlagDuration;
            observation.AfterlagMovementScale =
                settings.AfterlagMovementMultiplier;

            PlayerCombatInputBindings bindings =
                controller.InputBindings;

            if (bindings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": runtime combat input bindings are missing.");
                continue;
            }

            observation.MouseEnabled =
                bindings.EnableMouseDirectAttack;
            observation.PrimaryKey =
                bindings.DirectAttackPrimaryKey;
            observation.SecondaryKey =
                bindings.DirectAttackSecondaryKey;

            observation.MouseInputs +=
                controller.MouseDirectAttackInputCount;
            observation.PrimaryInputs +=
                controller.PrimaryKeyDirectAttackInputCount;
            observation.SecondaryInputs +=
                controller.SecondaryKeyDirectAttackInputCount;
            observation.InputFrames +=
                controller.DirectAttackInputFrameCount;
            observation.Requests +=
                controller.ReservedDirectAttackRequestCount;
            observation.StartedActions +=
                controller.StartedDirectAttackActionCount;
            observation.ExecutedActions +=
                controller.ExecutedDirectAttackActionCount;
            observation.SuccessfulActions +=
                controller.SuccessfulDirectAttackActionCount;
            observation.AcceptedTargets +=
                controller.AcceptedDirectAttackTargetCount;
            observation.IssuedDirectAttackIds +=
                controller.IssuedDirectAttackCount;
            observation.IssuedPushAttackIds +=
                controller.IssuedNonlethalPushAttackCount;
            observation.IssuedTotalAttackIds +=
                controller.IssuedAttackCount;
            observation.RecoveryRejects +=
                controller.DirectAttackRecoveryRejectCount;
            observation.CooldownRejects +=
                controller.DirectAttackCooldownRejectCount;
            observation.AfterlagStarts +=
                controller.DirectAttackAfterlagMovementStartCount;
            observation.AfterlagRejects +=
                controller.DirectAttackAfterlagMovementRejectCount;
            observation.FacingSnapshots +=
                controller.DirectAttackFacingSnapshotCount;
            observation.VisualFacingSyncs +=
                controller.DirectAttackVisualFacingSyncCount;
            observation.SameFrameTurns +=
                controller.DirectAttackSameFrameTurnCount;
            observation.ArcRejectedTargets +=
                controller.DirectAttackArcRejectedTargetCount;
            observation.ObservedFacingMask |=
                controller.DirectAttackObservedFacingMask;

            observation.MouseStarts +=
                controller.MouseDirectAttackStartedActionCount;
            observation.PrimaryStarts +=
                controller.PrimaryKeyDirectAttackStartedActionCount;
            observation.SecondaryStarts +=
                controller.SecondaryKeyDirectAttackStartedActionCount;
            observation.MouseSuccesses +=
                controller.MouseDirectAttackSuccessfulActionCount;
            observation.PrimarySuccesses +=
                controller.PrimaryKeyDirectAttackSuccessfulActionCount;
            observation.SecondarySuccesses +=
                controller.SecondaryKeyDirectAttackSuccessfulActionCount;


            if (controller.DirectAttackFacingSnapshotCount !=
                controller.StartedDirectAttackActionCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": every started direct attack must snapshot exactly " +
                    "one authoritative facing.");
            }

            if (controller.VisualAnimator != null &&
                controller.DirectAttackVisualFacingSyncCount !=
                controller.StartedDirectAttackActionCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": direct-attack visual facing sync count does not match " +
                    "started direct attacks.");
            }

            if (controller.IsDirectAttackRecoveryActive)
            {
                observation.ActiveRecoveries++;
            }

            if (controller.IsDirectAttackCooldownActive)
            {
                observation.ActiveCooldowns++;
            }
        }

        EnemyCombatReceiver[] receivers =
            Resources.FindObjectsOfTypeAll<EnemyCombatReceiver>();

        for (int i = 0; i < receivers.Length; i++)
        {
            EnemyCombatReceiver receiver = receivers[i];

            if (!IsActiveSceneObject(receiver))
            {
                continue;
            }

            observation.RuntimeEnemies++;
            receiver.CollectValidationErrors(errors);
            observation.ReceiverDirectAttacks +=
                receiver.AcceptedDirectAttackCount;
            observation.ReceiverDamageHits +=
                receiver.AcceptedDamageHitCount;
            observation.ReceiverAcceptedDamage +=
                receiver.TotalAcceptedDamage;
            observation.PayloadViolations +=
                receiver.DirectAttackPayloadViolationCount;
        }

        if (observation.RuntimePlayers != 1)
        {
            errors.Add(
                "Expected exactly one active runtime player, found " +
                observation.RuntimePlayers + ".");
        }

        if (observation.RuntimeEnemies <= 0)
        {
            errors.Add("No active runtime enemy is available for CB5 testing.");
        }

        if (observation.Requests != observation.InputFrames)
        {
            errors.Add(
                "Every direct-attack input frame must enter exactly one " +
                "shared action request.");
        }

        if (observation.StartedActions != observation.ExecutedActions)
        {
            errors.Add(
                "Started and executed direct-attack action counts differ.");
        }

        if (observation.IssuedDirectAttackIds !=
            observation.StartedActions)
        {
            errors.Add(
                "Every started direct attack must issue exactly one " +
                "CombatAttackId.");
        }

        if (observation.IssuedTotalAttackIds !=
            observation.IssuedPushAttackIds +
            observation.IssuedDirectAttackIds)
        {
            errors.Add(
                "Total CombatAttackId count does not equal push plus direct " +
                "action ids.");
        }

        if (observation.AfterlagStarts + observation.AfterlagRejects !=
            observation.StartedActions)
        {
            errors.Add(
                "Every started direct attack must submit exactly one timed " +
                "movement-scale request.");
        }

        if (observation.PayloadViolations > 0)
        {
            errors.Add(
                "One or more direct attacks entered nonlethal knockback " +
                "decay, queued pursuit recovery, used a non-Hit reaction or " +
                "cancelled an existing navigation-speed recovery.");
        }

        bool mouseObserved =
            !observation.MouseEnabled || observation.MouseSuccesses > 0;
        bool primaryObserved =
            observation.PrimaryKey == KeyCode.None ||
            observation.PrimarySuccesses > 0;
        bool secondaryObserved =
            observation.SecondaryKey == KeyCode.None ||
            observation.SecondarySuccesses > 0;
        bool damageObserved =
            observation.SuccessfulActions > 0 &&
            observation.AcceptedTargets > 0 &&
            observation.ReceiverDirectAttacks > 0 &&
            observation.ReceiverDamageHits > 0 &&
            observation.ReceiverAcceptedDamage > 0f;
        bool cooldownObserved =
            observation.RecoveryRejects +
            observation.CooldownRejects > 0;
        bool facingObserved =
            CountBits(observation.ObservedFacingMask) >= 2;
        bool rearRejectedObserved =
            observation.ArcRejectedTargets > 0;

        if (!mouseObserved)
        {
            notes.Add(
                "Damage an enemy once with the right mouse button.");
        }

        if (!primaryObserved)
        {
            notes.Add(
                "Damage an enemy once with primary direct-attack key " +
                observation.PrimaryKey + ".");
        }

        if (!secondaryObserved)
        {
            notes.Add(
                "Damage an enemy once with secondary direct-attack key " +
                observation.SecondaryKey + ".");
        }

        if (!cooldownObserved)
        {
            notes.Add(
                "Quickly repeat a direct-attack input so its independent " +
                "recovery or cooldown rejects at least one request.");
        }

        if (!facingObserved)
        {
            notes.Add(
                "Use the direct attack while facing at least two directions.");
        }

        if (!rearRejectedObserved)
        {
            notes.Add(
                "Place an in-range enemy outside the facing fan and attack " +
                "to observe one arc rejection.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB5] Direct Attack Audit");
        report.AppendLine(
            "Runtime Players=" + observation.RuntimePlayers +
            " | Runtime Enemies=" + observation.RuntimeEnemies);
        report.AppendLine(
            "Settings: Damage=" + observation.Damage.ToString("0.##") +
            " | Range=" + observation.Range.ToString("0.##") +
            " | Arc=" + observation.ArcAngle.ToString("0.##") +
            " | Max Targets=" + observation.MaximumTargets);
        report.AppendLine(
            "Timing: Cooldown=" + observation.Cooldown.ToString("0.##") +
            "s | Afterlag=" + observation.Afterlag.ToString("0.##") +
            "s | Movement Scale=" +
            observation.AfterlagMovementScale.ToString("0.##"));
        report.AppendLine(
            "Inputs: Mouse=" + observation.MouseInputs +
            " | Primary=" + observation.PrimaryInputs +
            " | Secondary=" + observation.SecondaryInputs +
            " | Frames=" + observation.InputFrames);
        report.AppendLine(
            "Starts: Mouse=" + observation.MouseStarts +
            " | Primary=" + observation.PrimaryStarts +
            " | Secondary=" + observation.SecondaryStarts +
            " | Total=" + observation.StartedActions);
        report.AppendLine(
            "Successes: Mouse=" + observation.MouseSuccesses +
            " | Primary=" + observation.PrimarySuccesses +
            " | Secondary=" + observation.SecondarySuccesses +
            " | Total=" + observation.SuccessfulActions);
        report.AppendLine(
            "Pipeline: Requests=" + observation.Requests +
            " | Accepted Targets=" + observation.AcceptedTargets +
            " | Direct Attack Ids=" + observation.IssuedDirectAttackIds +
            " | Total Attack Ids=" + observation.IssuedTotalAttackIds);
        report.AppendLine(
            "Timing Rejects: Recovery=" + observation.RecoveryRejects +
            " | Cooldown=" + observation.CooldownRejects +
            " | Afterlag Starts=" + observation.AfterlagStarts +
            " | Afterlag Rejected=" + observation.AfterlagRejects);
        report.AppendLine(
            "Facing: Snapshots=" + observation.FacingSnapshots +
            " | Visual Syncs=" + observation.VisualFacingSyncs +
            " | Observed Directions=" +
            CountBits(observation.ObservedFacingMask) +
            " | Same-Frame Turns=" + observation.SameFrameTurns +
            " | Arc-Rejected Targets=" +
            observation.ArcRejectedTargets);
        report.AppendLine(
            "Receivers: Direct Attacks=" +
            observation.ReceiverDirectAttacks +
            " | Damage Hits=" + observation.ReceiverDamageHits +
            " | Accepted Damage=" +
            observation.ReceiverAcceptedDamage.ToString("0.##") +
            " | Payload Violations=" + observation.PayloadViolations);
        report.AppendLine(
            "Active Direct Recovery=" + observation.ActiveRecoveries +
            " | Active Direct Cooldown=" + observation.ActiveCooldowns);

        AppendNotes(report, notes);

        if (errors.Count > 0)
        {
            AppendErrors(report, errors);
            report.AppendLine("FAIL: CB5 direct-attack wiring is invalid.");
            Debug.LogError(report.ToString());
            return;
        }

        bool completeObservation =
            mouseObserved &&
            primaryObserved &&
            secondaryObserved &&
            damageObserved &&
            cooldownObserved &&
            facingObserved &&
            rearRejectedObserved;

        if (!completeObservation)
        {
            report.AppendLine(
                "INCOMPLETE: CB5 wiring is valid, but damage, input, timing " +
                "or facing observations are not all recorded.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB5 right mouse, K and X share one facing-based damage " +
            "pipeline with independent timing and attack ids. Direct attacks " +
            "apply damage without entering nonlethal knockback decay or " +
            "post-knockback pursuit recovery. CB7 may add a separate weak Hit " +
            "response through the same motor/state owners.");
        Debug.Log(report.ToString());
    }

    private static void ValidateLoadedSpawners(
        List<string> errors,
        List<string> notes)
    {
        PlayerSpawner[] spawners =
            Resources.FindObjectsOfTypeAll<PlayerSpawner>();

        for (int i = 0; i < spawners.Length; i++)
        {
            PlayerSpawner spawner = spawners[i];

            if (spawner == null || EditorUtility.IsPersistent(spawner))
            {
                continue;
            }

            DirectAttackSettings settings =
                spawner.DirectAttackSettings;

            if (settings == null)
            {
                errors.Add(
                    spawner.gameObject.name +
                    ": PlayerSpawner has no Direct Attack settings.");
            }
            else
            {
                settings.EnsureValid();
                settings.CollectValidationErrors(
                    errors,
                    spawner.gameObject.name);
            }

            PlayerCombatInputBindings bindings =
                spawner.CombatInputBindings;

            if (bindings == null)
            {
                errors.Add(
                    spawner.gameObject.name +
                    ": PlayerSpawner has no combat input bindings.");
                continue;
            }

            bindings.EnsureValid();
            bindings.CollectValidationErrors(
                errors,
                spawner.gameObject.name);
            bindings.CollectValidationNotes(
                notes,
                spawner.gameObject.name);

            if (settings != null &&
                settings.Enabled &&
                !bindings.HasAnyDirectAttackBinding)
            {
                errors.Add(
                    spawner.gameObject.name +
                    ": Direct Attack is enabled but has no mouse or keyboard binding.");
            }
        }
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.activeInHierarchy;
    }

    private static int CountBits(int mask)
    {
        int count = 0;

        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendNotes(
        StringBuilder report,
        List<string> notes)
    {
        if (notes.Count <= 0)
        {
            return;
        }

        report.AppendLine("Notes:");

        for (int i = 0; i < notes.Count; i++)
        {
            report.AppendLine("- " + notes[i]);
        }
    }

    private static void AppendErrors(
        StringBuilder report,
        List<string> errors)
    {
        if (errors.Count <= 0)
        {
            return;
        }

        report.AppendLine("Errors:");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("- " + errors[i]);
        }
    }

    private struct RuntimeObservation
    {
        public int RuntimePlayers;
        public int RuntimeEnemies;
        public float Damage;
        public float Range;
        public float ArcAngle;
        public int MaximumTargets;
        public float Cooldown;
        public float Afterlag;
        public float AfterlagMovementScale;
        public bool MouseEnabled;
        public KeyCode PrimaryKey;
        public KeyCode SecondaryKey;
        public int MouseInputs;
        public int PrimaryInputs;
        public int SecondaryInputs;
        public int InputFrames;
        public int Requests;
        public int MouseStarts;
        public int PrimaryStarts;
        public int SecondaryStarts;
        public int StartedActions;
        public int ExecutedActions;
        public int MouseSuccesses;
        public int PrimarySuccesses;
        public int SecondarySuccesses;
        public int SuccessfulActions;
        public int AcceptedTargets;
        public int IssuedDirectAttackIds;
        public int IssuedPushAttackIds;
        public int IssuedTotalAttackIds;
        public int RecoveryRejects;
        public int CooldownRejects;
        public int AfterlagStarts;
        public int AfterlagRejects;
        public int FacingSnapshots;
        public int VisualFacingSyncs;
        public int SameFrameTurns;
        public int ArcRejectedTargets;
        public int ObservedFacingMask;
        public int ReceiverDirectAttacks;
        public int ReceiverDamageHits;
        public float ReceiverAcceptedDamage;
        public int PayloadViolations;
        public int ActiveRecoveries;
        public int ActiveCooldowns;

        public static RuntimeObservation CreateEmpty()
        {
            return new RuntimeObservation
            {
                PrimaryKey = KeyCode.None,
                SecondaryKey = KeyCode.None
            };
        }
    }
}
