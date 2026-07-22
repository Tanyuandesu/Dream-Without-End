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
/// In-memory record for one complete run. It deliberately does not save to
/// disk yet, but exposes stable queries for future ending logic.
/// </summary>
[Serializable]
public sealed class EnemyRunRecord
{
    [SerializeField] private List<EnemyFloorCombatRecord> floors =
        new List<EnemyFloorCombatRecord>();

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
        int allSpawned = 0;
        int eligibleSpawned = 0;
        int playerKills = 0;
        int otherDeaths = 0;
        int survived = 0;
        int missing = 0;
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

        entryLookup = new Dictionary<string, EnemyCombatRecordEntry>();
        currentFloor = null;
        Changed?.Invoke();
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
