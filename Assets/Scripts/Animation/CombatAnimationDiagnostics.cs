using UnityEngine;

/// <summary>
/// Cross-floor CB10A presentation counters. They deliberately record only
/// animation requests/results and never influence combat decisions.
/// </summary>
public static class CombatAnimationDiagnostics
{
    public static int PlayerPushRequests { get; private set; }
    public static int PlayerPushPlayed { get; private set; }
    public static int PlayerDirectRequests { get; private set; }
    public static int PlayerDirectPlayed { get; private set; }
    public static int PlayerMissingSequences { get; private set; }

    public static int EnemyWeakHitRequests { get; private set; }
    public static int EnemyWeakHitPlayed { get; private set; }
    public static int EnemyStrongHitRequests { get; private set; }
    public static int EnemyStrongHitPlayed { get; private set; }
    public static int EnemyMissingSequences { get; private set; }

    public static int DeathEchoSpawned { get; private set; }
    public static int DeathEchoCompleted { get; private set; }
    public static int DeathEchoMissingSequence { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayerPushRequests = 0;
        PlayerPushPlayed = 0;
        PlayerDirectRequests = 0;
        PlayerDirectPlayed = 0;
        PlayerMissingSequences = 0;
        EnemyWeakHitRequests = 0;
        EnemyWeakHitPlayed = 0;
        EnemyStrongHitRequests = 0;
        EnemyStrongHitPlayed = 0;
        EnemyMissingSequences = 0;
        DeathEchoSpawned = 0;
        DeathEchoCompleted = 0;
        DeathEchoMissingSequence = 0;
    }

    public static void RecordPlayerAction(
        CombatActionKind actionKind,
        bool played)
    {
        if (actionKind == CombatActionKind.NonlethalPush)
        {
            PlayerPushRequests++;
            if (played) PlayerPushPlayed++;
            else PlayerMissingSequences++;
            return;
        }

        if (actionKind == CombatActionKind.DirectAttack)
        {
            PlayerDirectRequests++;
            if (played) PlayerDirectPlayed++;
            else PlayerMissingSequences++;
        }
    }

    public static void RecordEnemyWeakHit(bool played)
    {
        EnemyWeakHitRequests++;
        if (played) EnemyWeakHitPlayed++;
        else EnemyMissingSequences++;
    }

    public static void RecordEnemyStrongHit(bool played)
    {
        EnemyStrongHitRequests++;
        if (played) EnemyStrongHitPlayed++;
        else EnemyMissingSequences++;
    }

    public static void RecordDeathEchoSpawned(bool played)
    {
        if (played) DeathEchoSpawned++;
        else DeathEchoMissingSequence++;
    }

    public static void RecordDeathEchoCompleted()
    {
        DeathEchoCompleted++;
    }
}
