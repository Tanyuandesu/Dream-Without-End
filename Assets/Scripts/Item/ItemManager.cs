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
public sealed class ItemManager :
    MonoBehaviour,
    IItemProgressionReader
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

    public int CollectedItemCount =>
        collectedItems.Count;

    public int ProgressionScore =>
        progressionScore;

    public int LastCollectedFloor =>
        lastCollectedFloor;

    public GameObject ActivePickup =>
        activePickup;

    public event Action<ItemCollectedEvent>
        ItemCollected;

    public event Action<ItemProgressSnapshot>
        ProgressChanged;

    public event Action<ItemSpawnDecision>
        SpawnDecisionMade;

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
                floorNumber);
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
            return null;
        }

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
