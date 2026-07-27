#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CombatCB10AAnimationAudit
{
    private const string PlayerProfilePath =
        "Assets/GeneratedCharacterAnimation/CA1_TemporaryStickHuman_8Direction.asset";
    private const string EnemyProfilePath =
        "Assets/GeneratedCharacterAnimation/CA1_TemporaryStickCat_8Direction.asset";

    private static readonly CharacterFacingDirection[] Directions =
    {
        CharacterFacingDirection.South,
        CharacterFacingDirection.SouthEast,
        CharacterFacingDirection.East,
        CharacterFacingDirection.NorthEast,
        CharacterFacingDirection.North,
        CharacterFacingDirection.NorthWest,
        CharacterFacingDirection.West,
        CharacterFacingDirection.SouthWest
    };

    [MenuItem("Tools/Dream Dungeon/Animation/Run CB10A Combat Animation Audit")]
    public static void RunAudit()
    {
        CharacterAnimationProfile playerProfile =
            AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(
                PlayerProfilePath);
        CharacterAnimationProfile enemyProfile =
            AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(
                EnemyProfilePath);

        List<string> errors = new List<string>();
        ValidateProfileState(
            playerProfile,
            CharacterAnimationState.Attack,
            "Player DirectAttack",
            errors);
        ValidateProfileState(
            playerProfile,
            CharacterAnimationState.Special,
            "Player Push",
            errors);
        ValidateProfileState(
            enemyProfile,
            CharacterAnimationState.Hurt,
            "Enemy WeakHit",
            errors);
        ValidateProfileState(
            enemyProfile,
            CharacterAnimationState.Special,
            "Enemy StrongKnockback",
            errors);
        ValidateProfileState(
            enemyProfile,
            CharacterAnimationState.Death,
            "Enemy Death",
            errors);

        PlayerCombatAnimationBridge[] playerBridges =
            Object.FindObjectsByType<PlayerCombatAnimationBridge>(
                FindObjectsSortMode.None);
        EnemyCombatAnimationBridge[] enemyBridges =
            Object.FindObjectsByType<EnemyCombatAnimationBridge>(
                FindObjectsSortMode.None);
        TemporaryDeathAnimationEcho[] deathEchoes =
            Object.FindObjectsByType<TemporaryDeathAnimationEcho>(
                FindObjectsSortMode.None);

        int uninitializedPlayers = 0;
        for (int i = 0; i < playerBridges.Length; i++)
        {
            if (!playerBridges[i].IsInitialized)
            {
                uninitializedPlayers++;
            }
        }

        int uninitializedEnemies = 0;
        int bridgeMissing = 0;
        for (int i = 0; i < enemyBridges.Length; i++)
        {
            if (!enemyBridges[i].IsInitialized)
            {
                uninitializedEnemies++;
            }
            bridgeMissing += enemyBridges[i].MissingSequenceCount;
        }

        bool structuralPass =
            errors.Count == 0 &&
            playerBridges.Length == 1 &&
            uninitializedPlayers == 0 &&
            uninitializedEnemies == 0 &&
            CombatAnimationDiagnostics.PlayerMissingSequences == 0 &&
            CombatAnimationDiagnostics.EnemyMissingSequences == 0 &&
            CombatAnimationDiagnostics.DeathEchoMissingSequence == 0 &&
            bridgeMissing == 0;

        bool observationsComplete =
            CombatAnimationDiagnostics.PlayerPushPlayed > 0 &&
            CombatAnimationDiagnostics.PlayerDirectPlayed > 0 &&
            CombatAnimationDiagnostics.EnemyWeakHitPlayed > 0 &&
            CombatAnimationDiagnostics.EnemyStrongHitPlayed > 0 &&
            CombatAnimationDiagnostics.DeathEchoSpawned > 0 &&
            CombatAnimationDiagnostics.DeathEchoCompleted > 0;

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Animation/CB10A] Combat Animation Audit");
        report.AppendLine(
            "Runtime Player Bridges=" + playerBridges.Length +
            " | Enemy Bridges=" + enemyBridges.Length +
            " | Active Death Echoes=" + deathEchoes.Length);
        report.AppendLine(
            "Player: Push=" + CombatAnimationDiagnostics.PlayerPushPlayed +
            "/" + CombatAnimationDiagnostics.PlayerPushRequests +
            " | Direct=" + CombatAnimationDiagnostics.PlayerDirectPlayed +
            "/" + CombatAnimationDiagnostics.PlayerDirectRequests +
            " | Missing=" + CombatAnimationDiagnostics.PlayerMissingSequences);
        report.AppendLine(
            "Enemy: Weak=" + CombatAnimationDiagnostics.EnemyWeakHitPlayed +
            "/" + CombatAnimationDiagnostics.EnemyWeakHitRequests +
            " | Strong=" + CombatAnimationDiagnostics.EnemyStrongHitPlayed +
            "/" + CombatAnimationDiagnostics.EnemyStrongHitRequests +
            " | Missing=" + CombatAnimationDiagnostics.EnemyMissingSequences);
        report.AppendLine(
            "Death Echo: Spawned=" + CombatAnimationDiagnostics.DeathEchoSpawned +
            " | Completed=" + CombatAnimationDiagnostics.DeathEchoCompleted +
            " | Missing=" + CombatAnimationDiagnostics.DeathEchoMissingSequence);

        if (errors.Count > 0)
        {
            report.AppendLine("Profile errors:");
            for (int i = 0; i < errors.Count; i++)
            {
                report.AppendLine("- " + errors[i]);
            }
        }

        if (!structuralPass)
        {
            report.AppendLine(
                "FAIL: CB10A animation profiles or runtime bridges are incomplete.");
            Debug.LogError(report.ToString());
            return;
        }

        if (!observationsComplete)
        {
            report.AppendLine("Observation still required:");
            if (CombatAnimationDiagnostics.PlayerPushPlayed == 0)
                report.AppendLine("- Start one nonlethal push.");
            if (CombatAnimationDiagnostics.PlayerDirectPlayed == 0)
                report.AppendLine("- Start one direct attack.");
            if (CombatAnimationDiagnostics.EnemyWeakHitPlayed == 0)
                report.AppendLine("- Damage a living enemy once.");
            if (CombatAnimationDiagnostics.EnemyStrongHitPlayed == 0)
                report.AppendLine("- Successfully push a living enemy once.");
            if (CombatAnimationDiagnostics.DeathEchoSpawned == 0)
                report.AppendLine("- Kill one enemy and observe its death echo.");
            if (CombatAnimationDiagnostics.DeathEchoCompleted == 0)
                report.AppendLine("- Wait about 0.6 seconds after the death animation.");
            report.AppendLine(
                "INCOMPLETE: wiring is valid, but not every temporary combat animation has been observed.");
            Debug.LogWarning(report.ToString());
            return;
        }

        report.AppendLine(
            "PASS: CB10A player Push/DirectAttack and enemy WeakHit/StrongKnockback/Death temporary animations were played through decoupled combat presentation bridges.");
        Debug.Log(report.ToString());
    }

    private static void ValidateProfileState(
        CharacterAnimationProfile profile,
        CharacterAnimationState state,
        string label,
        List<string> errors)
    {
        if (profile == null)
        {
            errors.Add(label + " profile is missing.");
            return;
        }

        for (int i = 0; i < Directions.Length; i++)
        {
            if (!profile.HasSequence(state, Directions[i]))
            {
                errors.Add(label + " missing " + Directions[i] + ".");
            }
        }
    }
}
#endif
