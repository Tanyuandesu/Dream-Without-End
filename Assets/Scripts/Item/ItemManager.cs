using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理一整局的核心道具進度、刷新判定與收集事件。
///
/// ItemSystem 必須存在於樓層根物件之外，
/// 才能跨樓層保存進度。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemSpawner))]
public sealed class ItemManager : MonoBehaviour
{
    [Header("資料")]
    [SerializeField] private ItemCatalog itemCatalog;
    [SerializeField] private ItemSpawnPolicy spawnPolicy;
    [SerializeField] private ItemSpawner itemSpawner;

    private readonly List<ItemDefinition> collectedItems =
        new List<ItemDefinition>();

    private readonly HashSet<string> collectedItemIds =
        new HashSet<string>();

    private readonly Dictionary<int, FloorItemPlan>
        floorPlans =
            new Dictionary<int, FloorItemPlan>();

    private GameObject activePickup;
    private int progressionScore;
    private int lastCollectedFloor = -1;
    private bool candidatePoolExhaustionReported;

    public int CollectedItemCount =>
        collectedItems.Count;

    public int ProgressionScore =>
        progressionScore;

    public int LastCollectedFloor =>
        lastCollectedFloor;

    public GameObject ActivePickup =>
        activePickup;

    // SYS14 runtime-only bridge. This exposes where the current floor item
    // actually spawned without teaching NPC code how ItemSpawner chooses rooms.
    public DungeonSpawnCellResult ActiveSpawnResult =>
        activePickup != null && itemSpawner != null
            ? itemSpawner.LastSpawnResult
            : null;

    public int FirstGuaranteedFloor =>
        spawnPolicy != null
            ? spawnPolicy.FirstGuaranteedFloor
            : 2;

    public event Action<ItemCollectedEvent>
        ItemCollected;

    public event Action<ItemProgressSnapshot>
        ProgressChanged;

    public event Action<ItemSpawnDecision>
        SpawnDecisionMade;

    /// <summary>
    /// 把 ItemCatalog 的實際容量與勝利條件放在一起檢查。
    /// 不滿足時只報告配置錯誤，不偷偷降低通關門檻。
    /// </summary>
    public bool ValidateProgressionConfiguration(
        int requiredValue,
        bool useProgressionScore)
    {
        CacheComponents();

        List<string> errors =
            new List<string>();

        if (itemCatalog == null)
        {
            errors.Add("Item Catalog is not assigned.");
        }
        else
        {
            errors.AddRange(
                itemCatalog.GetProgressionValidationErrors(
                    requiredValue,
                    useProgressionScore));
        }

        if (spawnPolicy == null)
        {
            errors.Add("Item Spawn Policy is not assigned.");
        }

        if (itemSpawner == null)
        {
            errors.Add("Item Spawner is not available.");
        }

        if (errors.Count == 0)
        {
            return true;
        }

        Debug.LogError(
            "Item progression configuration invalid:\n- " +
            string.Join("\n- ", errors),
            this);

        return false;
    }

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    public void SetupFloor(
        int floorNumber,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer)
    {
        SetupFloor(
            floorNumber,
            layout,
            floorRoot,
            dungeonRenderer,
            null);
    }

    /// <summary>
    /// R8.3 运行时入口。旧四参数入口完整保留；
    /// GameManager 会传入本楼层共用的出生格保留集合。
    /// </summary>
    public void SetupFloor(
        int floorNumber,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        ClearFloor();
        CacheComponents();

        if (itemCatalog == null ||
            spawnPolicy == null)
        {
            Debug.LogWarning(
                "ItemManager：尚未設定 ItemCatalog 或 ItemSpawnPolicy。");

            return;
        }

        FloorItemPlan plan =
            GetOrCreateFloorPlan(
                floorNumber,
                layout);

        SpawnDecisionMade?.Invoke(
            new ItemSpawnDecision(
                floorNumber,
                plan.Chance,
                plan.Roll,
                plan.ShouldSpawn,
                plan.Definition));

        if (!plan.ShouldSpawn ||
            plan.Definition == null ||
            plan.Collected)
        {
            return;
        }

        if (plan.Definition.UniqueInRun &&
            HasCollected(plan.Definition.ItemId))
        {
            return;
        }

        activePickup =
            itemSpawner.Spawn(
                plan.Definition,
                layout,
                floorRoot,
                dungeonRenderer,
                this,
                floorNumber,
                runtimeSpawnReservations);
    }

    public bool TryCollect(
        ItemDefinition definition,
        int floorNumber,
        GameObject collector)
    {
        if (definition == null)
        {
            return false;
        }

        if (definition.UniqueInRun &&
            HasCollected(definition.ItemId))
        {
            return false;
        }

        collectedItems.Add(definition);
        collectedItemIds.Add(definition.ItemId);

        progressionScore +=
            definition.ProgressionValue;

        lastCollectedFloor = floorNumber;
        activePickup = null;

        if (floorPlans.TryGetValue(
                floorNumber,
                out FloorItemPlan plan))
        {
            plan.Collected = true;
        }

        ItemCollectedEvent collectedEvent =
            new ItemCollectedEvent(
                definition,
                floorNumber,
                collector,
                CollectedItemCount,
                progressionScore);

        ItemCollected?.Invoke(collectedEvent);

        ItemProgressSnapshot snapshot =
            CreateProgressSnapshot();

        ProgressChanged?.Invoke(snapshot);

        Debug.Log(
            "Collected item: " +
            definition.DisplayName +
            " | Total: " +
            CollectedItemCount +
            " | Progression Score: " +
            progressionScore);

        return true;
    }

    public bool HasCollected(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) &&
               collectedItemIds.Contains(itemId);
    }

    public ItemProgressSnapshot CreateProgressSnapshot()
    {
        return new ItemProgressSnapshot(
            collectedItems,
            progressionScore,
            lastCollectedFloor);
    }

    public float GetSpawnChanceForFloor(int floorNumber)
    {
        if (spawnPolicy == null)
        {
            return 0f;
        }

        return spawnPolicy.GetSpawnChance(
            floorNumber,
            CollectedItemCount > 0,
            lastCollectedFloor);
    }

    public void ClearFloor()
    {
        activePickup = null;

        if (itemSpawner != null)
        {
            itemSpawner.ClearRuntimeSpawnResult();
        }
    }

    /// <summary>
    /// SYS9 restore entry. Rebuilds only the run-level collected-item state
    /// from stable Item IDs. Floor plans, active pickup and collection timing
    /// history are intentionally discarded. The loaded floor therefore starts
    /// its post-load spawn pacing from the configured base chance.
    /// </summary>
    public bool TryRestoreRunProgress(
        IEnumerable<string> savedItemIds,
        int loadedFloorNumber,
        out string error)
    {
        error = string.Empty;
        CacheComponents();

        if (itemCatalog == null)
        {
            error = "Item Catalog is not assigned.";
            return false;
        }

        if (spawnPolicy == null)
        {
            error = "Item Spawn Policy is not assigned.";
            return false;
        }

        if (loadedFloorNumber < 1)
        {
            error = "Loaded floor must be at least 1.";
            return false;
        }

        List<ItemDefinition> restoredItems =
            new List<ItemDefinition>();
        HashSet<string> restoredIds =
            new HashSet<string>(StringComparer.Ordinal);
        int restoredScore = 0;

        if (savedItemIds != null)
        {
            foreach (string rawId in savedItemIds)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    continue;
                }

                string itemId = rawId.Trim();

                if (!restoredIds.Add(itemId))
                {
                    continue;
                }

                ItemDefinition definition =
                    itemCatalog.FindById(itemId);

                if (definition == null)
                {
                    error =
                        "Save references unknown Item ID: " +
                        itemId;
                    return false;
                }

                restoredItems.Add(definition);
                restoredScore +=
                    Mathf.Max(0, definition.ProgressionValue);
            }
        }

        ClearFloor();
        floorPlans.Clear();
        collectedItems.Clear();
        collectedItemIds.Clear();
        candidatePoolExhaustionReported = false;

        for (int i = 0; i < restoredItems.Count; i++)
        {
            ItemDefinition definition = restoredItems[i];
            collectedItems.Add(definition);
            collectedItemIds.Add(definition.ItemId);
        }

        progressionScore = restoredScore;

        if (collectedItems.Count > 0)
        {
            int minimumGap = Mathf.Max(
                1,
                spawnPolicy.MinimumFloorGapAfterCollection);

            // At the loaded floor, floorGap == minimumGap, which is exactly
            // the policy's base chance with zero accumulated pity increase.
            lastCollectedFloor =
                loadedFloorNumber - minimumGap;
        }
        else
        {
            lastCollectedFloor = -1;
        }

        ItemProgressSnapshot snapshot =
            CreateProgressSnapshot();

        ProgressChanged?.Invoke(snapshot);

        Debug.Log(
            "[SYS9] Item progress restored" +
            " | Items=" + CollectedItemCount +
            " | Score=" + progressionScore +
            " | LoadedFloor=" + loadedFloorNumber +
            " | LastCollectedFloorBaseline=" +
            lastCollectedFloor,
            this);

        return true;
    }

    private FloorItemPlan GetOrCreateFloorPlan(
        int floorNumber,
        DungeonLayout layout)
    {
        if (floorPlans.TryGetValue(
                floorNumber,
                out FloorItemPlan existingPlan))
        {
            return existingPlan;
        }

        float chance =
            GetSpawnChanceForFloor(floorNumber);

        int randomSeed =
            CombineSeed(
                layout != null ? layout.Seed : 0,
                floorNumber,
                progressionScore);

        System.Random random =
            new System.Random(randomSeed);

        float roll =
            (float)random.NextDouble();

        bool shouldSpawn =
            chance > 0f &&
            roll <= chance;

        ItemDefinition definition =
            shouldSpawn
                ? SelectDefinition(random)
                : null;

        FloorItemPlan plan =
            new FloorItemPlan(
                chance,
                roll,
                shouldSpawn &&
                definition != null,
                definition);

        floorPlans.Add(floorNumber, plan);

        return plan;
    }

    private ItemDefinition SelectDefinition(
        System.Random random)
    {
        if (CollectedItemCount == 0)
        {
            return itemCatalog.FirstGuaranteedItem;
        }

        List<ItemDefinition> candidates =
            new List<ItemDefinition>();

        IReadOnlyList<ItemDefinition> pool =
            itemCatalog.SubsequentItems;

        for (int i = 0; i < pool.Count; i++)
        {
            ItemDefinition definition =
                pool[i];

            if (definition == null)
            {
                continue;
            }

            if (definition.UniqueInRun &&
                HasCollected(definition.ItemId))
            {
                continue;
            }

            candidates.Add(definition);
        }

        if (candidates.Count == 0)
        {
            ReportCandidatePoolExhaustion();
            return null;
        }

        candidatePoolExhaustionReported = false;

        int totalWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight +=
                candidates[i].SpawnWeight;
        }

        int selectedWeight =
            random.Next(0, totalWeight);

        int accumulated = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            accumulated +=
                candidates[i].SpawnWeight;

            if (selectedWeight < accumulated)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private void ReportCandidatePoolExhaustion()
    {
        if (candidatePoolExhaustionReported)
        {
            return;
        }

        candidatePoolExhaustionReported = true;

        int configuredLaterItems =
            itemCatalog != null &&
            itemCatalog.SubsequentItems != null
                ? itemCatalog.SubsequentItems.Count
                : 0;

        Debug.LogError(
            "Item progression candidate pool is empty. " +
            "No eligible subsequent item can be selected. " +
            "Collected=" + CollectedItemCount +
            ", ConfiguredSubsequentItems=" +
            configuredLaterItems +
            ". Check ItemCatalog references, unique Item IDs, " +
            "and Unique In Run settings.",
            this);
    }

    private void CacheComponents()
    {
        if (itemSpawner == null)
        {
            itemSpawner =
                GetComponent<ItemSpawner>();
        }
    }

    private static int CombineSeed(
        int first,
        int second,
        int third)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + first;
            hash = hash * 31 + second;
            hash = hash * 31 + third;
            return hash;
        }
    }

    private sealed class FloorItemPlan
    {
        public float Chance { get; }
        public float Roll { get; }
        public bool ShouldSpawn { get; }
        public ItemDefinition Definition { get; }
        public bool Collected { get; set; }

        public FloorItemPlan(
            float chance,
            float roll,
            bool shouldSpawn,
            ItemDefinition definition)
        {
            Chance = chance;
            Roll = roll;
            ShouldSpawn = shouldSpawn;
            Definition = definition;
        }
    }
}
