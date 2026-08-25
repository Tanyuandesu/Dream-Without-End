using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CB6 runtime audit.
/// Recommended observation: kill at least one enemy with right mouse/K/X,
/// leave the floor once, then run this audit on the next floor.
/// </summary>
public static class CombatCB6DeathLifecycleAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Combat/Run CB6 Death Lifecycle Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        EnemyManager manager = FindActiveManager();

        if (manager == null)
        {
            Debug.LogError(
                "[Combat/CB6] Death Lifecycle Audit\n" +
                "FAIL: No active EnemyManager was found.");
            return;
        }

        EnemyRunRecordSnapshot snapshot =
            manager.CurrentRunSnapshot;

        int liveEnemyObjects = 0;
        int liveEligibleEnemies = 0;
        int deadObjectsStillActive = 0;
        int missingLifecycleCount = 0;
        int uninitializedLifecycleCount = 0;

        EnemyRuntimeIdentity[] identities =
            Resources.FindObjectsOfTypeAll<EnemyRuntimeIdentity>();

        for (int i = 0; i < identities.Length; i++)
        {
            EnemyRuntimeIdentity identity = identities[i];

            if (!IsActiveSceneObject(identity))
            {
                continue;
            }

            Health health = identity.GetComponent<Health>();

            if (health == null)
            {
                errors.Add(
                    identity.gameObject.name +
                    ": runtime enemy has no Health component.");
                continue;
            }

            if (health.IsDead)
            {
                deadObjectsStillActive++;

                continue;
            }

            liveEnemyObjects++;

            if (identity.CountsForEnding)
            {
                liveEligibleEnemies++;
            }

            EnemyDeathLifecycle lifecycle =
                identity.GetComponent<EnemyDeathLifecycle>();

            if (lifecycle == null)
            {
                missingLifecycleCount++;
            }
            else if (!lifecycle.IsInitialized)
            {
                uninitializedLifecycleCount++;
            }
        }

        if (manager.ActiveEnemyCount != liveEnemyObjects)
        {
            errors.Add(
                "EnemyManager active count (" +
                manager.ActiveEnemyCount +
                ") does not match live runtime enemy objects (" +
                liveEnemyObjects + ").");
        }

        if (snapshot.ActiveCount != liveEligibleEnemies)
        {
            errors.Add(
                "Run-record active eligible count (" +
                snapshot.ActiveCount +
                ") does not match live eligible enemies (" +
                liveEligibleEnemies + ").");
        }

        if (missingLifecycleCount > 0)
        {
            errors.Add(
                missingLifecycleCount +
                " live enemies are missing EnemyDeathLifecycle.");
        }

        if (uninitializedLifecycleCount > 0)
        {
            errors.Add(
                uninitializedLifecycleCount +
                " live EnemyDeathLifecycle components are uninitialized.");
        }

        if (deadObjectsStillActive > 0)
        {
            errors.Add(
                deadObjectsStillActive +
                " dead enemy objects remain active in the loaded scene.");
        }

        if (manager.RecordedPlayerDeathCount !=
            snapshot.PlayerKillCount)
        {
            errors.Add(
                "Manager player-death diagnostics (" +
                manager.RecordedPlayerDeathCount +
                ") do not match run record (" +
                snapshot.PlayerKillCount + ").");
        }

        if (manager.RecordedOtherDeathCount !=
            snapshot.OtherDeathCount)
        {
            errors.Add(
                "Manager other-death diagnostics (" +
                manager.RecordedOtherDeathCount +
                ") do not match run record (" +
                snapshot.OtherDeathCount + ").");
        }

        if (manager.DuplicateDeathRecordCount > 0)
        {
            errors.Add(
                "Duplicate death records=" +
                manager.DuplicateDeathRecordCount + ".");
        }

        if (manager.UnexpectedRemovalCount > 0)
        {
            errors.Add(
                "Unexpected enemy removals=" +
                manager.UnexpectedRemovalCount + ".");
        }

        if (EnemyDeathLifecycleDiagnostics.LifecycleViolationCount > 0)
        {
            errors.Add(
                "Death lifecycle violations=" +
                EnemyDeathLifecycleDiagnostics.LifecycleViolationCount +
                ".");
        }

        if (EnemyDeathLifecycleDiagnostics.ProcessedDeathCount !=
            manager.DeathNotificationCount)
        {
            errors.Add(
                "Lifecycle processed deaths (" +
                EnemyDeathLifecycleDiagnostics.ProcessedDeathCount +
                ") do not match manager notifications (" +
                manager.DeathNotificationCount + ").");
        }

        ValidateEndingQueryContract(errors);

        bool killObserved = snapshot.PlayerKillCount > 0;
        bool floorTransitionObserved =
            manager.FloorSetupCount >= 2 &&
            manager.FloorClearCount >= 1;

        if (!killObserved)
        {
            notes.Add(
                "Kill at least one enemy with right mouse/K/X to observe " +
                "player attribution and synchronous death cleanup.");
        }

        if (!floorTransitionObserved)
        {
            notes.Add(
                "After a player-attributed kill, enter the next floor once " +
                "to observe survivor finalization and stale-reference cleanup.");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Combat/CB6] Death Lifecycle Audit");
        report.AppendLine(
            "Runtime Active=" + manager.ActiveEnemyCount +
            " | Live Objects=" + liveEnemyObjects +
            " | Live Eligible=" + liveEligibleEnemies);
        report.AppendLine(
            "Manager: Registered=" + manager.RegisteredEnemyCount +
            " | Death Notifications=" + manager.DeathNotificationCount +
            " | Player Recorded=" + manager.RecordedPlayerDeathCount +
            " | Other Recorded=" + manager.RecordedOtherDeathCount +
            " | Unregistered=" + manager.UnregisterCount);
        report.AppendLine(
            "Floors: Setups=" + manager.FloorSetupCount +
            " | Clears=" + manager.FloorClearCount +
            " | Last Clear Survivors=" +
            manager.LastClearSurvivorCount);
        report.AppendLine(
            "Run Record: Eligible=" + snapshot.EligibleSpawnedCount +
            " | Player Kills=" + snapshot.PlayerKillCount +
            " | Other Deaths=" + snapshot.OtherDeathCount +
            " | Survived Floors=" + snapshot.SurvivedFloorCount +
            " | Missing=" + snapshot.RemovedWithoutDeathCount +
            " | Active=" + snapshot.ActiveCount);
        report.AppendLine(
            "Ending Queries: No Player Kills=" +
            snapshot.HasNoPlayerKills +
            " | No Enemy Deaths=" + snapshot.HasNoEnemyDeaths +
            " | All Dead=" + snapshot.AreAllEligibleEnemiesDead +
            " | All Player-Killed=" +
            snapshot.WereAllEligibleEnemiesKilledByPlayer);
        report.AppendLine(
            "Death Seal: Processed=" +
            EnemyDeathLifecycleDiagnostics.ProcessedDeathCount +
            " | Player=" +
            EnemyDeathLifecycleDiagnostics.PlayerAttributedDeathCount +
            " | Collider Shutdown=" +
            EnemyDeathLifecycleDiagnostics.ColliderShutdownCount +
            " | Physics Shutdown=" +
            EnemyDeathLifecycleDiagnostics.PhysicsShutdownCount +
            " | AI Shutdown=" +
            EnemyDeathLifecycleDiagnostics.AiShutdownCount +
            " | Violations=" +
            EnemyDeathLifecycleDiagnostics.LifecycleViolationCount);
        report.AppendLine(
            "Last Death: Instance=" +
            EnemyDeathLifecycleDiagnostics.LastInstanceId +
            " | Attribution=" +
            EnemyDeathLifecycleDiagnostics.LastAttribution +
            " | StateDead=" +
            EnemyDeathLifecycleDiagnostics.LastStateWasDead +
            " | MotorInactive=" +
            EnemyDeathLifecycleDiagnostics.LastMotorWasInactive +
            " | ReactionInactive=" +
            EnemyDeathLifecycleDiagnostics.LastReactionWasInactive);

        AppendNotes(report, notes);

        if (errors.Count > 0)
        {
            AppendErrors(report, errors);
            report.AppendLine(
                "FAIL: CB6 death cleanup, manager removal or run statistics " +
                "contain an invalid state.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!killObserved || !floorTransitionObserved)
        {
            report.AppendLine(
                "INCOMPLETE: CB6 wiring is valid, but the required kill and " +
                "next-floor observations are not both recorded yet.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB6 player-attributed death, synchronous hazard shutdown, " +
            "EnemyManager removal, survivor finalization and persistent " +
            "no-kill/all-kill query data were observed without stale refs.");
        Debug.Log(report.ToString());
    }

    private static void ValidateEndingQueryContract(
        List<string> errors)
    {
        EnemyRunRecordSnapshot noKill =
            new EnemyRunRecordSnapshot(3, 3, 0, 0, 0, 0, 3);

        EnemyRunRecordSnapshot allKill =
            new EnemyRunRecordSnapshot(3, 3, 3, 0, 0, 0, 0);

        EnemyRunRecordSnapshot mixed =
            new EnemyRunRecordSnapshot(3, 3, 1, 0, 2, 0, 0);

        bool valid =
            noKill.HasNoPlayerKills &&
            noKill.HasNoEnemyDeaths &&
            !noKill.AreAllEligibleEnemiesDead &&
            !noKill.WereAllEligibleEnemiesKilledByPlayer &&
            !allKill.HasNoPlayerKills &&
            allKill.AreAllEligibleEnemiesDead &&
            allKill.WereAllEligibleEnemiesKilledByPlayer &&
            !mixed.HasNoPlayerKills &&
            !mixed.AreAllEligibleEnemiesDead &&
            !mixed.WereAllEligibleEnemiesKilledByPlayer;

        if (!valid)
        {
            errors.Add(
                "EnemyRunRecordSnapshot no-kill/all-kill query contract failed.");
        }
    }

    private static EnemyManager FindActiveManager()
    {
        EnemyManager[] managers =
            Resources.FindObjectsOfTypeAll<EnemyManager>();

        for (int i = 0; i < managers.Length; i++)
        {
            if (IsActiveSceneObject(managers[i]))
            {
                return managers[i];
            }
        }

        return null;
    }

    private static bool IsActiveSceneObject(Component component)
    {
        return component != null &&
               !EditorUtility.IsPersistent(component) &&
               component.gameObject.scene.IsValid() &&
               component.gameObject.activeInHierarchy;
    }

    private static void AppendNotes(
        StringBuilder report,
        List<string> notes)
    {
        if (notes.Count == 0)
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
        report.AppendLine("Errors:");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("- " + errors[i]);
        }
    }
}
