using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies CB3.5 authoritative eight-direction facing and facing-centred fan
/// attacks. For a complete runtime observation: push one enemy in front, put
/// an in-range enemy behind the player and click, use at least one diagonal
/// action, and change direction while clicking at least once.
/// </summary>
public static class CombatCB35FacingAttackAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB3.5 Facing Attack Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        ValidateEightDirectionMapping(errors);

        if (!EditorApplication.isPlaying)
        {
            StringBuilder editReport = new StringBuilder();
            editReport.AppendLine("[Combat/CB3.5] Facing Attack Audit");
            editReport.AppendLine(
                "Eight-direction mapping self-test=" +
                (errors.Count == 0 ? "PASS" : "FAIL"));

            if (errors.Count > 0)
            {
                editReport.AppendLine("Errors:");

                for (int i = 0; i < errors.Count; i++)
                {
                    editReport.AppendLine("- " + errors[i]);
                }

                Debug.LogError(editReport.ToString());
                return;
            }

            editReport.AppendLine(
                "Enter Play Mode after player and enemy generation to " +
                "observe front hits, rear rejection and same-frame turns.");

            Debug.Log(editReport.ToString());
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

            RuntimeDungeonPlayer movement = controller.Movement;
            NonlethalPushSettings settings = controller.PushSettings;

            if (movement == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": RuntimeDungeonPlayer is missing.");

                continue;
            }

            if (settings == null)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": Nonlethal Push settings are missing.");

                continue;
            }

            settings.EnsureValid();

            observation.ArcAngle = settings.ArcAngle;
            observation.StartedActions +=
                controller.StartedLeftPushActionCount;
            observation.FacingBasedActions +=
                controller.FacingBasedActionCount;
            observation.SuccessfulActions +=
                controller.SuccessfulLeftPushActionCount;
            observation.AcceptedTargets +=
                controller.AcceptedLeftPushTargetCount;
            observation.ArcRejectedTargets +=
                controller.ArcRejectedTargetCount;
            observation.SameFrameTurnActions +=
                controller.SameFrameTurnActionCount;
            observation.ObservedFacingMask |=
                controller.ObservedActionFacingMask;
            observation.VisualFacingSyncs +=
                controller.VisualFacingSyncCount;
            observation.CombatFacingRefreshes +=
                movement.CombatFacingRefreshCount;
            observation.CombatFacingChanges +=
                movement.CombatFacingChangeCount;
            observation.FacingUpdates +=
                movement.FacingUpdateCount;
            observation.DiagonalFacingUpdates +=
                movement.DiagonalFacingUpdateCount;
            observation.LastFacing =
                controller.LastActionFacing;
            observation.LastFacingVector =
                controller.LastAimDirection;

            Vector2 authoritativeVector = movement.FacingVector;
            Vector2 expectedVector =
                RuntimeDungeonPlayer.FacingToVector(
                    movement.CurrentFacing);

            if (authoritativeVector.sqrMagnitude < 0.99f ||
                Vector2.Dot(
                    authoritativeVector.normalized,
                    expectedVector.normalized) < 0.999f)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": authoritative facing enum and vector disagree.");
            }

            if (controller.FacingBasedActionCount !=
                controller.StartedLeftPushActionCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": every started action must snapshot one facing.");
            }

            if (movement.CombatFacingRefreshCount <
                controller.StartedLeftPushActionCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": movement recorded fewer combat-facing refreshes than " +
                    "started nonlethal-push actions. Later actions may add " +
                    "more refreshes through the same facing source.");
            }

            if (controller.VisualAnimator != null &&
                !controller.VisualAnimator.HasAuthoritativeFacingSource)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": DirectionalSpriteAnimator did not bind the shared " +
                    "ICharacterFacingSource.");
            }

            if (controller.VisualAnimator != null &&
                controller.VisualFacingSyncCount !=
                controller.StartedLeftPushActionCount)
            {
                errors.Add(
                    controller.gameObject.name +
                    ": visual facing sync count does not match started " +
                    "actions.");
            }

            if (controller.StartedLeftPushActionCount > 0)
            {
                Vector2 lastExpected =
                    RuntimeDungeonPlayer.FacingToVector(
                        controller.LastActionFacing);

                if (controller.LastAimDirection.sqrMagnitude < 0.99f ||
                    Vector2.Dot(
                        controller.LastAimDirection.normalized,
                        lastExpected.normalized) < 0.999f)
                {
                    errors.Add(
                        controller.gameObject.name +
                        ": last attack fan does not match its snapped " +
                        "eight-direction facing.");
                }
            }
        }

        if (activePlayerCount != 1)
        {
            errors.Add(
                "Expected exactly one active runtime player, found " +
                activePlayerCount + ".");
        }

        observation.ObservedDirectionCount =
            CountBits(observation.ObservedFacingMask);
        observation.ObservedDiagonalAction =
            ContainsDiagonalFacing(observation.ObservedFacingMask);

        bool facingActionsObserved =
            observation.StartedActions >= 3 &&
            observation.FacingBasedActions ==
                observation.StartedActions &&
            observation.CombatFacingRefreshes >=
                observation.StartedActions;

        bool frontHitObserved =
            observation.SuccessfulActions > 0 &&
            observation.AcceptedTargets > 0;

        bool rearOrSideRejectionObserved =
            observation.ArcRejectedTargets > 0;

        bool directionVarietyObserved =
            observation.ObservedDirectionCount >= 2 &&
            observation.ObservedDiagonalAction;

        bool sameFrameTurnObserved =
            observation.SameFrameTurnActions > 0 &&
            observation.CombatFacingChanges > 0;

        if (observation.ArcAngle > 180f)
        {
            notes.Add(
                "The fan is wider than 180 degrees. Facing still owns the " +
                "centre, but rear exclusion becomes weak.");
        }

        if (observation.ArcRejectedTargets > 0)
        {
            notes.Add(
                "At least one in-range enemy was rejected outside the " +
                "facing-centred fan.");
        }

        if (observation.SameFrameTurnActions > 0)
        {
            notes.Add(
                "At least one action used a facing changed during the same " +
                "rendered frame.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB3.5] Facing Attack Audit");
        report.AppendLine(
            "Runtime Players=" + activePlayerCount +
            " | Direction Mapping=8/8" +
            " | Arc=" + observation.ArcAngle.ToString("0.###") +
            " degrees");

        report.AppendLine(
            "Started Actions=" + observation.StartedActions +
            " | Facing Snapshots=" + observation.FacingBasedActions +
            " | Combat Facing Refreshes=" +
            observation.CombatFacingRefreshes);

        report.AppendLine(
            "Successful Push Actions=" +
            observation.SuccessfulActions +
            " | Accepted Front Targets=" +
            observation.AcceptedTargets +
            " | Arc-Rejected Targets=" +
            observation.ArcRejectedTargets);

        report.AppendLine(
            "Observed Directions=" +
            observation.ObservedDirectionCount +
            " | Facing Mask=" +
            BuildFacingMaskLabel(observation.ObservedFacingMask) +
            " | Diagonal Observed=" +
            observation.ObservedDiagonalAction);

        report.AppendLine(
            "Same-Frame Turn Actions=" +
            observation.SameFrameTurnActions +
            " | Combat Facing Changes=" +
            observation.CombatFacingChanges +
            " | Total Facing Updates=" +
            observation.FacingUpdates);

        report.AppendLine(
            "Last Action Facing=" + observation.LastFacing +
            " | Last Fan Vector=" +
            observation.LastFacingVector.ToString("F3") +
            " | Visual Syncs=" +
            observation.VisualFacingSyncs);

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
                "FAIL: Fix CB3.5 facing authority before accepting this " +
                "phase.");

            Debug.LogError(report.ToString());
            return;
        }

        if (!facingActionsObserved ||
            !frontHitObserved ||
            !rearOrSideRejectionObserved ||
            !directionVarietyObserved ||
            !sameFrameTurnObserved)
        {
            report.AppendLine("Observation still required:");

            if (!facingActionsObserved)
            {
                report.AppendLine(
                    "- Start at least three valid left-push actions.");
            }

            if (!frontHitObserved)
            {
                report.AppendLine(
                    "- Face an in-range enemy and successfully push it.");
            }

            if (!rearOrSideRejectionObserved)
            {
                report.AppendLine(
                    "- Put an enemy within range behind or well beside the " +
                    "player, face away and click once.");
            }

            if (!directionVarietyObserved)
            {
                report.AppendLine(
                    "- Use at least two attack facings, including one " +
                    "diagonal direction.");
            }

            if (!sameFrameTurnObserved)
            {
                report.AppendLine(
                    "- Press a new direction and click during the same " +
                    "moment so the action snapshots that fresh facing.");
            }

            report.AppendLine(
                "INCOMPLETE: CB3.5 wiring is valid, but front/rear, diagonal " +
                "and same-frame-turn observations are not all recorded.");

            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB3.5 left-push fans use the authoritative eight-direction " +
            "player facing, reject targets outside that fan and capture fresh " +
            "same-frame turns. Mouse position no longer selects attack " +
            "direction.");

        Debug.Log(report.ToString());
    }

    private static void ValidateEightDirectionMapping(
        List<string> errors)
    {
        FacingCase[] cases =
        {
            new FacingCase(
                CharacterFacingDirection.South,
                Vector2.down),
            new FacingCase(
                CharacterFacingDirection.SouthEast,
                new Vector2(1f, -1f)),
            new FacingCase(
                CharacterFacingDirection.East,
                Vector2.right),
            new FacingCase(
                CharacterFacingDirection.NorthEast,
                new Vector2(1f, 1f)),
            new FacingCase(
                CharacterFacingDirection.North,
                Vector2.up),
            new FacingCase(
                CharacterFacingDirection.NorthWest,
                new Vector2(-1f, 1f)),
            new FacingCase(
                CharacterFacingDirection.West,
                Vector2.left),
            new FacingCase(
                CharacterFacingDirection.SouthWest,
                new Vector2(-1f, -1f))
        };

        for (int i = 0; i < cases.Length; i++)
        {
            FacingCase facingCase = cases[i];
            CharacterFacingDirection quantized =
                RuntimeDungeonPlayer.QuantizeFacingDirection(
                    facingCase.Direction);

            if (quantized != facingCase.Facing)
            {
                errors.Add(
                    "Eight-direction quantization expected " +
                    facingCase.Facing + " but returned " + quantized + ".");
            }

            Vector2 vector = RuntimeDungeonPlayer.FacingToVector(
                facingCase.Facing);

            if (vector.sqrMagnitude < 0.99f ||
                Vector2.Dot(
                    vector.normalized,
                    facingCase.Direction.normalized) < 0.999f)
            {
                errors.Add(
                    "Facing vector conversion failed for " +
                    facingCase.Facing + ".");
            }
        }
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

    private static bool ContainsDiagonalFacing(int mask)
    {
        return HasFacing(mask, CharacterFacingDirection.SouthEast) ||
               HasFacing(mask, CharacterFacingDirection.NorthEast) ||
               HasFacing(mask, CharacterFacingDirection.NorthWest) ||
               HasFacing(mask, CharacterFacingDirection.SouthWest);
    }

    private static bool HasFacing(
        int mask,
        CharacterFacingDirection facing)
    {
        return (mask & (1 << (int)facing)) != 0;
    }

    private static string BuildFacingMaskLabel(int mask)
    {
        StringBuilder label = new StringBuilder();

        for (int i = 0; i < 8; i++)
        {
            CharacterFacingDirection facing =
                (CharacterFacingDirection)i;

            if (!HasFacing(mask, facing))
            {
                continue;
            }

            if (label.Length > 0)
            {
                label.Append(",");
            }

            label.Append(facing);
        }

        return label.Length > 0 ? label.ToString() : "None";
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.scene.isLoaded &&
               component.gameObject.activeInHierarchy;
    }

    private readonly struct FacingCase
    {
        public readonly CharacterFacingDirection Facing;
        public readonly Vector2 Direction;

        public FacingCase(
            CharacterFacingDirection facing,
            Vector2 direction)
        {
            Facing = facing;
            Direction = direction;
        }
    }

    private struct RuntimeObservation
    {
        public int StartedActions;
        public int FacingBasedActions;
        public int SuccessfulActions;
        public int AcceptedTargets;
        public int ArcRejectedTargets;
        public int SameFrameTurnActions;
        public int ObservedFacingMask;
        public int ObservedDirectionCount;
        public int VisualFacingSyncs;
        public int CombatFacingRefreshes;
        public int CombatFacingChanges;
        public int FacingUpdates;
        public int DiagonalFacingUpdates;
        public float ArcAngle;
        public CharacterFacingDirection LastFacing;
        public Vector2 LastFacingVector;
        public bool ObservedDiagonalAction;

        public static RuntimeObservation CreateEmpty()
        {
            return new RuntimeObservation
            {
                LastFacing = CharacterFacingDirection.South,
                LastFacingVector = Vector2.down
            };
        }
    }
}
