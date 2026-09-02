using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyLifeResolution
{
    Active = 0,
    KilledByPlayer = 1,
    KilledByOther = 2,
    SurvivedFloor = 3,
    RemovedWithoutDeath = 4
}

[Serializable]
public sealed class EnemyCombatRecordEntry
{
    [SerializeField] private string instanceId;
    [SerializeField] private string enemyId;
    [SerializeField] private int floorNumber;
    [SerializeField] private int floorSessionId;
    [SerializeField] private int roomIndex;
    [SerializeField] private Vector2Int spawnCell;
    [SerializeField] private bool countsForEnding;
    [SerializeField] private EnemyLifeResolution resolution;
    [SerializeField] private DamageAttribution deathAttribution;

    public string InstanceId => instanceId;
    public string EnemyId => enemyId;
    public int FloorNumber => floorNumber;
    public int FloorSessionId => floorSessionId;
    public int RoomIndex => roomIndex;
    public Vector2Int SpawnCell => spawnCell;
    public bool CountsForEnding => countsForEnding;
    public EnemyLifeResolution Resolution => resolution;
    public DamageAttribution DeathAttribution => deathAttribution;
    public bool IsResolved => resolution != EnemyLifeResolution.Active;

    public EnemyCombatRecordEntry(EnemyRuntimeIdentity identity)
    {
        instanceId = identity != null
            ? identity.InstanceId
            : Guid.NewGuid().ToString("N");

        enemyId = identity != null
            ? identity.EnemyId.Value
            : "unknown_enemy";

        floorNumber = identity != null ? identity.FloorNumber : 0;
        floorSessionId = identity != null ? identity.FloorSessionId : 0;
        roomIndex = identity != null ? identity.RoomIndex : -1;
        spawnCell = identity != null ? identity.SpawnCell : Vector2Int.zero;
        countsForEnding = identity == null || identity.CountsForEnding;
        resolution = EnemyLifeResolution.Active;
        deathAttribution = DamageAttribution.Unspecified;
    }

    internal bool TryResolveDeath(DamageAttribution attribution)
    {
        if (resolution != EnemyLifeResolution.Active)
        {
            return false;
        }

        deathAttribution = attribution;
        resolution = attribution == DamageAttribution.Player
            ? EnemyLifeResolution.KilledByPlayer
            : EnemyLifeResolution.KilledByOther;

        return true;
    }

    internal bool TryResolveSurvivedFloor()
    {
        return TryResolve(EnemyLifeResolution.SurvivedFloor);
    }

    internal bool TryResolveMissing()
    {
        return TryResolve(EnemyLifeResolution.RemovedWithoutDeath);
    }

    private bool TryResolve(EnemyLifeResolution newResolution)
    {
        if (resolution != EnemyLifeResolution.Active)
        {
            return false;
        }

        resolution = newResolution;
        deathAttribution = DamageAttribution.Unspecified;
        return true;
    }
}

[Serializable]
public sealed class EnemyFloorCombatRecord
{
    [SerializeField] private int floorNumber;
    [SerializeField] private int floorSessionId;
    [SerializeField] private int layoutSeed;
    [SerializeField] private bool finalized;
    [SerializeField] private List<EnemyCombatRecordEntry> entries =
        new List<EnemyCombatRecordEntry>();

    public int FloorNumber => floorNumber;
    public int FloorSessionId => floorSessionId;
    public int LayoutSeed => layoutSeed;
    public bool Finalized => finalized;
    public IReadOnlyList<EnemyCombatRecordEntry> Entries => entries;

    public EnemyFloorCombatRecord(
        int newFloorNumber,
        int newFloorSessionId,
        int newLayoutSeed)
    {
        floorNumber = Mathf.Max(0, newFloorNumber);
        floorSessionId = Mathf.Max(0, newFloorSessionId);
        layoutSeed = newLayoutSeed;
        finalized = false;
    }

    internal void Add(EnemyCombatRecordEntry entry)
    {
        if (entry != null)
        {
            entries.Add(entry);
        }
    }

    internal void FinalizeActiveAsSurvived()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].TryResolveSurvivedFloor();
        }

        finalized = true;
    }
}

[Serializable]
public struct EnemyRunRecordSnapshot
{
    public int AllSpawnedCount { get; }
    public int EligibleSpawnedCount { get; }
    public int PlayerKillCount { get; }
    public int OtherDeathCount { get; }
    public int SurvivedFloorCount { get; }
    public int RemovedWithoutDeathCount { get; }
    public int ActiveCount { get; }

    public int TotalDeathCount =>
        PlayerKillCount + OtherDeathCount;

    public bool HasNoEnemyDeaths => TotalDeathCount == 0;
    public bool HasNoPlayerKills => PlayerKillCount == 0;

    public bool AreAllEligibleEnemiesDead =>
        EligibleSpawnedCount > 0 &&
        TotalDeathCount == EligibleSpawnedCount;

    public bool WereAllEligibleEnemiesKilledByPlayer =>
        EligibleSpawnedCount > 0 &&
        PlayerKillCount == EligibleSpawnedCount;

    public EnemyRunRecordSnapshot(
        int allSpawnedCount,
        int eligibleSpawnedCount,
        int playerKillCount,
        int otherDeathCount,
        int survivedFloorCount,
        int removedWithoutDeathCount,
        int activeCount)
    {
        AllSpawnedCount = allSpawnedCount;
        EligibleSpawnedCount = eligibleSpawnedCount;
        PlayerKillCount = playerKillCount;
        OtherDeathCount = otherDeathCount;
        SurvivedFloorCount = survivedFloorCount;
        RemovedWithoutDeathCount = removedWithoutDeathCount;
        ActiveCount = activeCount;
    }
}

/// <summary>
/// Authoritative combat-history record for one complete run. Detailed live
/// entries stay in memory; Continue may restore earlier history as compact
/// cumulative totals while preserving the same snapshot semantics.
/// </summary>
[Serializable]
public sealed class EnemyRunRecord
{
    [SerializeField] private List<EnemyFloorCombatRecord> floors =
        new List<EnemyFloorCombatRecord>();

    // Continue compacts pre-load detailed entries into one authoritative
    // historical baseline. Newly generated floors continue accumulating in
    // floors, so CreateSnapshot always returns the complete run history.
    [SerializeField] private int carriedAllSpawnedCount;
    [SerializeField] private int carriedEligibleSpawnedCount;
    [SerializeField] private int carriedPlayerKillCount;
    [SerializeField] private int carriedOtherDeathCount;
    [SerializeField] private int carriedSurvivedFloorCount;
    [SerializeField] private int carriedRemovedWithoutDeathCount;

    [NonSerialized]
    private Dictionary<string, EnemyCombatRecordEntry> entryLookup;

    [NonSerialized]
    private EnemyFloorCombatRecord currentFloor;

    public IReadOnlyList<EnemyFloorCombatRecord> Floors => floors;
    public EnemyFloorCombatRecord CurrentFloor => currentFloor;
    public event Action Changed;

    public void BeginFloor(
        int floorNumber,
        int floorSessionId,
        int layoutSeed)
    {
        EnsureRuntimeIndex();

        if (currentFloor != null && !currentFloor.Finalized)
        {
            currentFloor.FinalizeActiveAsSurvived();
        }

        currentFloor = new EnemyFloorCombatRecord(
            floorNumber,
            floorSessionId,
            layoutSeed);

        floors.Add(currentFloor);
        Changed?.Invoke();
    }

    public bool RegisterSpawn(EnemyRuntimeIdentity identity)
    {
        if (identity == null ||
            string.IsNullOrWhiteSpace(identity.InstanceId))
        {
            return false;
        }

        EnsureRuntimeIndex();

        if (entryLookup.ContainsKey(identity.InstanceId))
        {
            return false;
        }

        if (currentFloor == null || currentFloor.Finalized)
        {
            BeginFloor(
                identity.FloorNumber,
                identity.FloorSessionId,
                0);
        }

        EnemyCombatRecordEntry entry =
            new EnemyCombatRecordEntry(identity);

        currentFloor.Add(entry);
        entryLookup.Add(entry.InstanceId, entry);
        Changed?.Invoke();
        return true;
    }

    public bool RegisterDeath(
        EnemyRuntimeIdentity identity,
        DamageAttribution attribution)
    {
        EnemyCombatRecordEntry entry;

        if (!TryGetOrRegister(identity, out entry))
        {
            return false;
        }

        bool changed = entry.TryResolveDeath(attribution);

        if (changed)
        {
            Changed?.Invoke();
        }

        return changed;
    }

    public bool MarkSurvivedFloor(EnemyRuntimeIdentity identity)
    {
        EnemyCombatRecordEntry entry;

        if (!TryGetOrRegister(identity, out entry))
        {
            return false;
        }

        bool changed = entry.TryResolveSurvivedFloor();

        if (changed)
        {
            Changed?.Invoke();
        }

        return changed;
    }

    public bool MarkRemovedWithoutDeath(EnemyRuntimeIdentity identity)
    {
        EnemyCombatRecordEntry entry;

        if (!TryGetOrRegister(identity, out entry))
        {
            return false;
        }

        bool changed = entry.TryResolveMissing();

        if (changed)
        {
            Changed?.Invoke();
        }

        return changed;
    }

    public void FinalizeCurrentFloor()
    {
        EnsureRuntimeIndex();

        if (currentFloor == null || currentFloor.Finalized)
        {
            return;
        }

        currentFloor.FinalizeActiveAsSurvived();
        Changed?.Invoke();
    }

    public EnemyRunRecordSnapshot CreateSnapshot()
    {
        int allSpawned = carriedAllSpawnedCount;
        int eligibleSpawned = carriedEligibleSpawnedCount;
        int playerKills = carriedPlayerKillCount;
        int otherDeaths = carriedOtherDeathCount;
        int survived = carriedSurvivedFloorCount;
        int missing = carriedRemovedWithoutDeathCount;
        int active = 0;

        if (floors != null)
        {
            for (int floorIndex = 0;
                 floorIndex < floors.Count;
                 floorIndex++)
            {
                EnemyFloorCombatRecord floor = floors[floorIndex];

                if (floor == null || floor.Entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0;
                     entryIndex < floor.Entries.Count;
                     entryIndex++)
                {
                    EnemyCombatRecordEntry entry =
                        floor.Entries[entryIndex];

                    if (entry == null)
                    {
                        continue;
                    }

                    allSpawned++;

                    if (!entry.CountsForEnding)
                    {
                        continue;
                    }

                    eligibleSpawned++;

                    switch (entry.Resolution)
                    {
                        case EnemyLifeResolution.KilledByPlayer:
                            playerKills++;
                            break;

                        case EnemyLifeResolution.KilledByOther:
                            otherDeaths++;
                            break;

                        case EnemyLifeResolution.SurvivedFloor:
                            survived++;
                            break;

                        case EnemyLifeResolution.RemovedWithoutDeath:
                            missing++;
                            break;

                        default:
                            active++;
                            break;
                    }
                }
            }
        }

        return new EnemyRunRecordSnapshot(
            allSpawned,
            eligibleSpawned,
            playerKills,
            otherDeaths,
            survived,
            missing,
            active);
    }

    /// <summary>
    /// Restores the durable factual history of an earlier run without
    /// restoring enemy instances or a generated floor. Enemies that were
    /// still Active at the save point are counted as SurvivedFloor on
    /// Continue because those exact instances are abandoned when the world
    /// snapshot is regenerated. Their original spawns remain in the ending
    /// denominator and therefore cannot be erased by loading.
    /// </summary>
    public bool TryRestorePersistentHistory(
        EnemyRunRecordSnapshot snapshot,
        out string error)
    {
        error = string.Empty;

        if (!TryValidateSnapshot(snapshot, out error))
        {
            return false;
        }

        if (floors == null)
        {
            floors = new List<EnemyFloorCombatRecord>();
        }
        else
        {
            floors.Clear();
        }

        carriedAllSpawnedCount = snapshot.AllSpawnedCount;
        carriedEligibleSpawnedCount = snapshot.EligibleSpawnedCount;
        carriedPlayerKillCount = snapshot.PlayerKillCount;
        carriedOtherDeathCount = snapshot.OtherDeathCount;
        carriedSurvivedFloorCount =
            snapshot.SurvivedFloorCount + snapshot.ActiveCount;
        carriedRemovedWithoutDeathCount =
            snapshot.RemovedWithoutDeathCount;

        entryLookup = new Dictionary<string, EnemyCombatRecordEntry>(
            StringComparer.Ordinal);
        currentFloor = null;
        Changed?.Invoke();
        return true;
    }

    public void ResetRun()
    {
        if (floors == null)
        {
            floors = new List<EnemyFloorCombatRecord>();
        }
        else
        {
            floors.Clear();
        }

        carriedAllSpawnedCount = 0;
        carriedEligibleSpawnedCount = 0;
        carriedPlayerKillCount = 0;
        carriedOtherDeathCount = 0;
        carriedSurvivedFloorCount = 0;
        carriedRemovedWithoutDeathCount = 0;

        entryLookup = new Dictionary<string, EnemyCombatRecordEntry>();
        currentFloor = null;
        Changed?.Invoke();
    }

    private static bool TryValidateSnapshot(
        EnemyRunRecordSnapshot snapshot,
        out string error)
    {
        error = string.Empty;

        if (snapshot.AllSpawnedCount < 0 ||
            snapshot.EligibleSpawnedCount < 0 ||
            snapshot.PlayerKillCount < 0 ||
            snapshot.OtherDeathCount < 0 ||
            snapshot.SurvivedFloorCount < 0 ||
            snapshot.RemovedWithoutDeathCount < 0 ||
            snapshot.ActiveCount < 0)
        {
            error = "Enemy run history contains a negative count.";
            return false;
        }

        if (snapshot.AllSpawnedCount < snapshot.EligibleSpawnedCount)
        {
            error =
                "Eligible enemy count cannot exceed all spawned enemies.";
            return false;
        }

        long classifiedEligible =
            (long)snapshot.PlayerKillCount +
            snapshot.OtherDeathCount +
            snapshot.SurvivedFloorCount +
            snapshot.RemovedWithoutDeathCount +
            snapshot.ActiveCount;

        if (classifiedEligible != snapshot.EligibleSpawnedCount)
        {
            error =
                "Eligible enemy history is internally inconsistent.";
            return false;
        }

        return true;
    }

    private bool TryGetOrRegister(
        EnemyRuntimeIdentity identity,
        out EnemyCombatRecordEntry entry)
    {
        entry = null;

        if (identity == null ||
            string.IsNullOrWhiteSpace(identity.InstanceId))
        {
            return false;
        }

        EnsureRuntimeIndex();

        if (entryLookup.TryGetValue(identity.InstanceId, out entry))
        {
            return true;
        }

        if (!RegisterSpawn(identity))
        {
            return false;
        }

        return entryLookup.TryGetValue(identity.InstanceId, out entry);
    }

    private void EnsureRuntimeIndex()
    {
        if (entryLookup != null)
        {
            return;
        }

        entryLookup =
            new Dictionary<string, EnemyCombatRecordEntry>(
                StringComparer.Ordinal);

        currentFloor = null;

        if (floors == null)
        {
            floors = new List<EnemyFloorCombatRecord>();
            return;
        }

        for (int floorIndex = 0;
             floorIndex < floors.Count;
             floorIndex++)
        {
            EnemyFloorCombatRecord floor = floors[floorIndex];

            if (floor == null)
            {
                continue;
            }

            if (!floor.Finalized)
            {
                currentFloor = floor;
            }

            for (int entryIndex = 0;
                 entryIndex < floor.Entries.Count;
                 entryIndex++)
            {
                EnemyCombatRecordEntry entry =
                    floor.Entries[entryIndex];

                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.InstanceId) ||
                    entryLookup.ContainsKey(entry.InstanceId))
                {
                    continue;
                }

                entryLookup.Add(entry.InstanceId, entry);
            }
        }
    }
}
