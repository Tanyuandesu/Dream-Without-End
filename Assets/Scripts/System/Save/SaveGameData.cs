using System;
using System.Collections.Generic;

/// <summary>
/// Compact, detached persistence payload for EnemyRunRecord.
/// It stores cumulative run facts only. Enemy instances, positions and map
/// state remain intentionally excluded from the save contract.
/// </summary>
[Serializable]
public sealed class EnemyRunSaveData
{
    public int allSpawnedCount;
    public int eligibleSpawnedCount;
    public int playerKillCount;
    public int otherDeathCount;
    public int survivedFloorCount;
    public int removedWithoutDeathCount;
    public int activeCount;

    public EnemyRunSaveData()
    {
    }

    public EnemyRunSaveData(EnemyRunRecordSnapshot snapshot)
    {
        allSpawnedCount = snapshot.AllSpawnedCount;
        eligibleSpawnedCount = snapshot.EligibleSpawnedCount;
        playerKillCount = snapshot.PlayerKillCount;
        otherDeathCount = snapshot.OtherDeathCount;
        survivedFloorCount = snapshot.SurvivedFloorCount;
        removedWithoutDeathCount = snapshot.RemovedWithoutDeathCount;
        activeCount = snapshot.ActiveCount;
    }

    public EnemyRunRecordSnapshot CreateSnapshot()
    {
        return new EnemyRunRecordSnapshot(
            allSpawnedCount,
            eligibleSpawnedCount,
            playerKillCount,
            otherDeathCount,
            survivedFloorCount,
            removedWithoutDeathCount,
            activeCount);
    }

    public EnemyRunSaveData CreateCopy()
    {
        return new EnemyRunSaveData(CreateSnapshot());
    }
}

/// <summary>
/// Lightweight run-progress save contract.
///
/// Intentionally excludes generated map layout, player position, enemy
/// instances, room visit state and other world snapshots. Continue restores
/// durable run facts, then lets the requested floor generate normally.
/// </summary>
[Serializable]
public sealed class SaveGameData
{
    // Keep required scalar defaults invalid so truncated/minimal JSON such as
    // "{}" cannot silently become a legitimate floor-1 save.
    public int saveVersion;
    public int floorIndex;
    public float currentHP;
    public List<string> collectedItemIds = new List<string>();
    public EnemyRunSaveData enemyRun = new EnemyRunSaveData();

    // Compatibility read surface for existing debug/UI code. There is no
    // second persisted kill-count field; EnemyRunSaveData is authoritative.
    public int killCount =>
        enemyRun != null
            ? enemyRun.playerKillCount
            : 0;

    public SaveGameData()
    {
    }

    public SaveGameData(
        int floorIndex,
        float currentHP,
        IEnumerable<string> collectedItemIds,
        EnemyRunRecordSnapshot enemyRunSnapshot)
    {
        saveVersion = SaveSystemManager.CurrentSaveVersion;
        this.floorIndex = floorIndex;
        this.currentHP = currentHP;
        enemyRun = new EnemyRunSaveData(enemyRunSnapshot);

        if (collectedItemIds != null)
        {
            this.collectedItemIds.AddRange(collectedItemIds);
        }
    }

    /// <summary>
    /// Compatibility constructor used by existing SYS8 probes. It represents
    /// a minimal historical run in which every eligible spawn was killed by
    /// the player. Runtime saves use the full snapshot overload above.
    /// </summary>
    public SaveGameData(
        int floorIndex,
        float currentHP,
        IEnumerable<string> collectedItemIds,
        int killCount)
        : this(
            floorIndex,
            currentHP,
            collectedItemIds,
            new EnemyRunRecordSnapshot(
                killCount,
                killCount,
                killCount,
                0,
                0,
                0,
                0))
    {
    }

    public SaveGameData CreateCopy()
    {
        SaveGameData copy = new SaveGameData
        {
            saveVersion = saveVersion,
            floorIndex = floorIndex,
            currentHP = currentHP,
            enemyRun = enemyRun != null
                ? enemyRun.CreateCopy()
                : null
        };

        if (collectedItemIds != null)
        {
            copy.collectedItemIds.AddRange(collectedItemIds);
        }

        return copy;
    }
}
