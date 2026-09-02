using System;
using System.Collections.Generic;

/// <summary>
/// Detached snapshot passed from the final legacy maze into EndingScene.
/// It deliberately contains only run facts needed to resolve/present an ending.
/// Future NPC/choice systems can extend this contract without coupling the
/// EndingScene back to GameScene runtime objects.
/// </summary>
[Serializable]
public sealed class EndingRunData
{
    public string endingId;
    public int sourceFloor;
    public float finalHP;
    public List<string> collectedItemIds;
    public List<string> eventFlags = new List<string>();

    public int allEnemySpawnCount;
    public int eligibleEnemySpawnCount;
    public int playerKillCount;
    public int otherEnemyDeathCount;
    public int survivedEnemyCount;
    public int removedWithoutDeathCount;
    public int activeEnemyCount;

    // Compatibility read surface. Enemy run history is authoritative.
    public int killCount => playerKillCount;

    public EndingRunData(
        int sourceFloor,
        float finalHP,
        IEnumerable<string> collectedItemIds,
        EnemyRunRecordSnapshot enemySnapshot)
    {
        this.sourceFloor = Math.Max(1, sourceFloor);
        this.finalHP = finalHP;
        this.collectedItemIds =
            collectedItemIds != null
                ? new List<string>(collectedItemIds)
                : new List<string>();

        allEnemySpawnCount = Math.Max(0, enemySnapshot.AllSpawnedCount);
        eligibleEnemySpawnCount =
            Math.Max(0, enemySnapshot.EligibleSpawnedCount);
        playerKillCount = Math.Max(0, enemySnapshot.PlayerKillCount);
        otherEnemyDeathCount = Math.Max(0, enemySnapshot.OtherDeathCount);
        survivedEnemyCount = Math.Max(0, enemySnapshot.SurvivedFloorCount);
        removedWithoutDeathCount =
            Math.Max(0, enemySnapshot.RemovedWithoutDeathCount);
        activeEnemyCount = Math.Max(0, enemySnapshot.ActiveCount);
        endingId = string.Empty;
    }

    /// <summary>
    /// Compatibility overload for direct/test callers that only possess a
    /// kill count. Runtime completion uses the full snapshot overload.
    /// </summary>
    public EndingRunData(
        int sourceFloor,
        float finalHP,
        IEnumerable<string> collectedItemIds,
        int killCount)
        : this(
            sourceFloor,
            finalHP,
            collectedItemIds,
            new EnemyRunRecordSnapshot(
                Math.Max(0, killCount),
                Math.Max(0, killCount),
                Math.Max(0, killCount),
                0,
                0,
                0,
                0))
    {
    }

    public EndingRunData Clone()
    {
        EnemyRunRecordSnapshot enemySnapshot =
            new EnemyRunRecordSnapshot(
                allEnemySpawnCount,
                eligibleEnemySpawnCount,
                playerKillCount,
                otherEnemyDeathCount,
                survivedEnemyCount,
                removedWithoutDeathCount,
                activeEnemyCount);

        EndingRunData clone =
            new EndingRunData(
                sourceFloor,
                finalHP,
                collectedItemIds,
                enemySnapshot);

        clone.endingId = endingId;

        if (eventFlags != null)
        {
            clone.eventFlags.AddRange(eventFlags);
        }

        return clone;
    }

    public void AddEventFlag(string eventFlag)
    {
        if (string.IsNullOrWhiteSpace(eventFlag))
        {
            return;
        }

        string normalized = eventFlag.Trim();
        if (!eventFlags.Contains(normalized))
        {
            eventFlags.Add(normalized);
        }
    }

    public bool HasEventFlag(string eventFlag)
    {
        if (string.IsNullOrWhiteSpace(eventFlag) ||
            eventFlags == null)
        {
            return false;
        }

        return eventFlags.Contains(eventFlag.Trim());
    }

    public static EndingRunData CreateDirectSceneFallback()
    {
        EndingRunData data =
            new EndingRunData(
                1,
                1f,
                Array.Empty<string>(),
                new EnemyRunRecordSnapshot());

        data.endingId = EndingResolver.DefaultEndingId;
        return data;
    }
}

/// <summary>
/// One-shot handoff. GameScene queues a detached result immediately before
/// loading EndingScene; EndingScene consumes it once.
/// </summary>
public static class EndingRunContext
{
    private static EndingRunData pending;

    public static bool HasPending => pending != null;

    public static void Queue(EndingRunData data)
    {
        pending = data != null
            ? data.Clone()
            : null;
    }

    public static bool TryPeek(out EndingRunData data)
    {
        data = pending != null
            ? pending.Clone()
            : null;

        return data != null;
    }

    public static bool TryConsume(out EndingRunData data)
    {
        if (pending == null)
        {
            data = null;
            return false;
        }

        data = pending.Clone();
        pending = null;
        return true;
    }

    public static void Clear()
    {
        pending = null;
    }
}
