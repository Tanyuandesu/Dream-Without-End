using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB4.5 mouse and full-keyboard combat bindings.
/// In Play Mode, successfully push one enemy with left mouse, the primary
/// push key and the secondary push key. Also press right mouse and both
/// reserved direct-attack keys once each before running the audit.
/// </summary>
public static class CombatCB45MultiInputAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB4.5 Multi-Input Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        ValidateLoadedSpawners(errors, notes);

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine("[Combat/CB4.5] Multi-Input Audit");
            AppendNotes(editReport, notes);

            if (errors.Count == 0)
            {
                editReport.AppendLine(
                    "PASS: Loaded PlayerSpawner input bindings are valid. " +
                    "Enter Play Mode to observe all mouse and keyboard paths.");

                Debug.Log(editReport.ToString());
            }
            else
            {
                AppendErrors(editReport, errors);
                editReport.AppendLine(
                    "FAIL: Fix CB4.5 binding errors before Play Mode testing.");

                Debug.LogError(editReport.ToString());
            }

            return;
        }

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

            PlayerCombatInputBindings bindings =
                controller.InputBindings;

            if (bindings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": runtime Player Combat Input Bindings are missing.");
                continue;
            }

            bindings.EnsureValid();
            bindings.CollectValidationNotes(
                notes,
                controller.gameObject.name);

            observation.PushMouseEnabled =
                bindings.EnableMouseNonlethalPush;
            observation.PushPrimaryKey =
                bindings.NonlethalPushPrimaryKey;
            observation.PushSecondaryKey =
                bindings.NonlethalPushSecondaryKey;
            observation.DirectMouseEnabled =
                bindings.EnableMouseDirectAttack;
            observation.DirectPrimaryKey =
                bindings.DirectAttackPrimaryKey;
            observation.DirectSecondaryKey =
                bindings.DirectAttackSecondaryKey;

            observation.PushMouseInputs +=
                controller.MousePushInputCount;
            observation.PushPrimaryInputs +=
                controller.PrimaryKeyPushInputCount;
            observation.PushSecondaryInputs +=
                controller.SecondaryKeyPushInputCount;

            observation.PushMouseStarts +=
                controller.MousePushStartedActionCount;
            observation.PushPrimaryStarts +=
                controller.PrimaryKeyPushStartedActionCount;
            observation.PushSecondaryStarts +=
                controller.SecondaryKeyPushStartedActionCount;

            observation.PushMouseSuccesses +=
                controller.MousePushSuccessfulActionCount;
            observation.PushPrimarySuccesses +=
                controller.PrimaryKeyPushSuccessfulActionCount;
            observation.PushSecondarySuccesses +=
                controller.SecondaryKeyPushSuccessfulActionCount;

            observation.CoalescedPushFrames +=
                controller.CoalescedPushInputFrameCount;
            observation.TotalPushActionRequests +=
                controller.LeftPushInputCount;
            observation.TotalStartedPushActions +=
                controller.StartedLeftPushActionCount;
            observation.TotalSuccessfulPushActions +=
                controller.SuccessfulLeftPushActionCount;
            observation.IssuedAttackIds +=
                controller.IssuedAttackCount;

            observation.DirectInputFrames +=
                controller.DirectAttackInputFrameCount;
            observation.DirectMouseInputs +=
                controller.MouseDirectAttackInputCount;
            observation.DirectPrimaryInputs +=
                controller.PrimaryKeyDirectAttackInputCount;
            observation.DirectSecondaryInputs +=
                controller.SecondaryKeyDirectAttackInputCount;
            observation.CoalescedDirectFrames +=
                controller.CoalescedDirectAttackInputFrameCount;
            observation.ReservedDirectRequests +=
                controller.ReservedDirectAttackRequestCount;
            observation.ExecutedDirectActions +=
                controller.ExecutedDirectAttackActionCount;
        }

        if (activePlayerCount != 1)
        {
            errors.Add(
                "Expected exactly one active runtime player, found " +
                activePlayerCount + ".");
        }

        if (observation.IssuedAttackIds !=
            observation.TotalStartedPushActions)
        {
            errors.Add(
                "Issued attack ids must equal started nonlethal-push " +
                "actions. Reserved direct-attack inputs must not issue ids.");
        }

        if (observation.ReservedDirectRequests !=
            observation.DirectInputFrames)
        {
            errors.Add(
                "Each direct-attack input frame must enter exactly one " +
                "reserved common action request.");
        }

        if (observation.ExecutedDirectActions != 0)
        {
            errors.Add(
                "CB4.5 must not execute a damaging direct attack before " +
                "the damage phase is installed.");
        }

        bool pushMouseObserved =
            !observation.PushMouseEnabled ||
            observation.PushMouseSuccesses > 0;

        bool pushPrimaryObserved =
            observation.PushPrimaryKey == KeyCode.None ||
            observation.PushPrimarySuccesses > 0;

        bool pushSecondaryObserved =
            observation.PushSecondaryKey == KeyCode.None ||
            observation.PushSecondarySuccesses > 0;

        bool directMouseObserved =
            !observation.DirectMouseEnabled ||
            observation.DirectMouseInputs > 0;

        bool directPrimaryObserved =
            observation.DirectPrimaryKey == KeyCode.None ||
            observation.DirectPrimaryInputs > 0;

        bool directSecondaryObserved =
            observation.DirectSecondaryKey == KeyCode.None ||
            observation.DirectSecondaryInputs > 0;

        bool completeObservation =
            pushMouseObserved &&
            pushPrimaryObserved &&
            pushSecondaryObserved &&
            directMouseObserved &&
            directPrimaryObserved &&
            directSecondaryObserved;

        if (!pushMouseObserved)
        {
            notes.Add(
                "Successfully push an enemy with the left mouse button.");
        }

        if (!pushPrimaryObserved)
        {
            notes.Add(
                "Successfully push an enemy with primary key " +
                observation.PushPrimaryKey + ".");
        }

        if (!pushSecondaryObserved)
        {
            notes.Add(
                "Successfully push an enemy with secondary key " +
                observation.PushSecondaryKey + ".");
        }

        if (!directMouseObserved)
        {
            notes.Add(
                "Press the right mouse button once to observe its reserved " +
                "direct-attack entry path.");
        }

        if (!directPrimaryObserved)
        {
            notes.Add(
                "Press reserved direct-attack primary key " +
                observation.DirectPrimaryKey + " once.");
        }

        if (!directSecondaryObserved)
        {
            notes.Add(
                "Press reserved direct-attack secondary key " +
                observation.DirectSecondaryKey + " once.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB4.5] Multi-Input Audit");
        report.AppendLine(
            "Runtime Players=" + activePlayerCount);
        report.AppendLine(
            "Push Bindings: Mouse=" + observation.PushMouseEnabled +
            " | Primary=" + observation.PushPrimaryKey +
            " | Secondary=" + observation.PushSecondaryKey);
        report.AppendLine(
            "Direct Bindings: Mouse=" + observation.DirectMouseEnabled +
            " | Primary=" + observation.DirectPrimaryKey +
            " | Secondary=" + observation.DirectSecondaryKey);
        report.AppendLine(
            "Push Inputs: Mouse=" + observation.PushMouseInputs +
            " | Primary=" + observation.PushPrimaryInputs +
            " | Secondary=" + observation.PushSecondaryInputs +
            " | Coalesced Frames=" + observation.CoalescedPushFrames);
        report.AppendLine(
            "Push Starts: Mouse=" + observation.PushMouseStarts +
            " | Primary=" + observation.PushPrimaryStarts +
            " | Secondary=" + observation.PushSecondaryStarts);
        report.AppendLine(
            "Push Successes: Mouse=" + observation.PushMouseSuccesses +
            " | Primary=" + observation.PushPrimarySuccesses +
            " | Secondary=" + observation.PushSecondarySuccesses);
        report.AppendLine(
            "Push Pipeline: Requests=" + observation.TotalPushActionRequests +
            " | Started=" + observation.TotalStartedPushActions +
            " | Successful=" + observation.TotalSuccessfulPushActions +
            " | Issued Attack Ids=" + observation.IssuedAttackIds);
        report.AppendLine(
            "Reserved Direct Inputs: Frames=" + observation.DirectInputFrames +
            " | Mouse=" + observation.DirectMouseInputs +
            " | Primary=" + observation.DirectPrimaryInputs +
            " | Secondary=" + observation.DirectSecondaryInputs +
            " | Coalesced Frames=" + observation.CoalescedDirectFrames);
        report.AppendLine(
            "Reserved Direct Pipeline: Requests=" +
            observation.ReservedDirectRequests +
            " | Executed Actions=" + observation.ExecutedDirectActions);

        AppendNotes(report, notes);

        if (errors.Count > 0)
        {
            AppendErrors(report, errors);
            report.AppendLine("FAIL: CB4.5 multi-input wiring is invalid.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!completeObservation)
        {
            report.AppendLine(
                "INCOMPLETE: CB4.5 wiring is valid, but not every enabled " +
                "mouse/keyboard input path has been observed.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: Mouse, WASD-side keyboard and arrow-key-side keyboard " +
            "inputs share the same nonlethal-push pipeline. Right mouse, " +
            "primary and secondary direct-attack inputs reach one reserved " +
            "entry point without issuing damage or attack ids.");
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

            if (spawner == null ||
                EditorUtility.IsPersistent(spawner))
            {
                continue;
            }

            PlayerCombatInputBindings bindings =
                spawner.CombatInputBindings;

            if (bindings == null)
            {
                errors.Add(
                    spawner.gameObject.name +
                    ": PlayerSpawner has no Combat Input Bindings.");
                continue;
            }

            bindings.EnsureValid();
            bindings.CollectValidationErrors(
                errors,
                spawner.gameObject.name);
            bindings.CollectValidationNotes(
                notes,
                spawner.gameObject.name);
        }
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.activeInHierarchy;
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
        public bool PushMouseEnabled;
        public KeyCode PushPrimaryKey;
        public KeyCode PushSecondaryKey;
        public bool DirectMouseEnabled;
        public KeyCode DirectPrimaryKey;
        public KeyCode DirectSecondaryKey;

        public int PushMouseInputs;
        public int PushPrimaryInputs;
        public int PushSecondaryInputs;
        public int PushMouseStarts;
        public int PushPrimaryStarts;
        public int PushSecondaryStarts;
        public int PushMouseSuccesses;
        public int PushPrimarySuccesses;
        public int PushSecondarySuccesses;
        public int CoalescedPushFrames;
        public int TotalPushActionRequests;
        public int TotalStartedPushActions;
        public int TotalSuccessfulPushActions;
        public int IssuedAttackIds;

        public int DirectInputFrames;
        public int DirectMouseInputs;
        public int DirectPrimaryInputs;
        public int DirectSecondaryInputs;
        public int CoalescedDirectFrames;
        public int ReservedDirectRequests;
        public int ExecutedDirectActions;

        public static RuntimeObservation CreateEmpty()
        {
            return new RuntimeObservation
            {
                PushPrimaryKey = KeyCode.None,
                PushSecondaryKey = KeyCode.None,
                DirectPrimaryKey = KeyCode.None,
                DirectSecondaryKey = KeyCode.None
            };
        }
    }
}
