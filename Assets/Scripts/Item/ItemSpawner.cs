using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 ItemDefinition 生成到當前樓層。
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemSpawner : MonoBehaviour
{
    private const int R83ItemSelectionSalt = 830303;

    [Header("生成位置")]
    [Min(0)]
    [SerializeField] private int minimumDistanceFromStart = 4;

    [Min(0)]
    [SerializeField] private int minimumDistanceFromExit = 2;

    [Header("測試外觀")]
    [SerializeField] private int fallbackSortingOrder = 12;

    [Header("R8.3 受控失败测试")]
    [Tooltip(
        "只用于 R8.3 受控失败：把全部 FloorCells 视为已保留，" +
        "强制 Item Spawn Cell 解析被拒绝。正常运行必须关闭；" +
        "不会修改 Layout、Prefab 或道具进度。")]
    [SerializeField]
    private bool r83InjectNoLegalItemCellForControlledFailure;

    public GameObject Spawn(
        ItemDefinition definition,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer,
        ItemManager itemManager,
        int floorNumber)
    {
        return Spawn(
            definition,
            layout,
            floorRoot,
            dungeonRenderer,
            itemManager,
            floorNumber,
            null);
    }

    /// <summary>
    /// R8.3 运行时入口。旧六参数入口完整保留；
    /// 共用保留集合由 GameManager 建立，解析器只读取它，
    /// 成功实例化后才提交 Item 的最终出生格。
    /// </summary>
    public GameObject Spawn(
        ItemDefinition definition,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer,
        ItemManager itemManager,
        int floorNumber,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        if (definition == null ||
            layout == null ||
            floorRoot == null ||
            dungeonRenderer == null ||
            itemManager == null)
        {
            Debug.LogError(
                "ItemSpawner：生成資料不完整。");

            return null;
        }

        HashSet<Vector2Int> requestReservations =
            runtimeSpawnReservations == null
                ? new HashSet<Vector2Int>()
                : new HashSet<Vector2Int>(
                    runtimeSpawnReservations);

        requestReservations.Add(layout.StartCell);
        requestReservations.Add(layout.ExitCell);

        if (r83InjectNoLegalItemCellForControlledFailure)
        {
            requestReservations.UnionWith(
                layout.FloorCells);
        }

        int selectionSalt = CombineSeed(
            R83ItemSelectionSalt,
            floorNumber,
            StableHash(definition.ItemId));

        selectionSalt = CombineSeed(
            selectionSalt,
            itemManager.ProgressionScore,
            83);

        List<int> coreItemRoomIndices =
            new List<int>();

        DungeonCoreItemRoomScopeR943
            .CollectCandidateRoomIndices(
                layout,
                coreItemRoomIndices);

        bool hasTaggedCoreItemScope =
            coreItemRoomIndices.Count > 0;

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Item,
                allowedRoomIndices:
                    hasTaggedCoreItemScope
                        ? coreItemRoomIndices
                        : null,
                selectionSalt: selectionSalt,
                reservedCells: requestReservations,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart:
                    minimumDistanceFromStart,
                minimumDistanceFromExit:
                    minimumDistanceFromExit,
                allowWalkableFallback: true,
                allowLayoutWideFallback:
                    !hasTaggedCoreItemScope);

        DungeonSpawnCellResult spawnResult;
        string failureReason;

        if (!DungeonSpawnCellResolver.TryResolve(
                request,
                out spawnResult,
                out failureReason))
        {
            Debug.LogWarning(
                "[ItemSpawner/R8.3] Item SpawnCell 提交被拒绝。" +
                "\nRequested=SpawnPoint(Item)" +
                " | Effective=Rejected" +
                " | ControlledFailure=" +
                r83InjectNoLegalItemCellForControlledFailure +
                " | Floor=" + floorNumber +
                " | Seed=" + layout.Seed +
                " | ItemId=" + definition.ItemId +
                "\nCoreScope=" +
                (hasTaggedCoreItemScope
                    ? "CoreItemCandidateRooms"
                    : "LegacyLayoutWideFallback") +
                " | CoreCandidateRooms=" +
                coreItemRoomIndices.Count +
                " | LayoutWideFallbackAllowed=" +
                !hasTaggedCoreItemScope +
                "\nReason=" + failureReason +
                "\nItemSpawned=None" +
                " | LayoutMutation=None" +
                " | PrefabMutation=None" +
                " | ProgressMutation=None",
                this);

            return null;
        }

        GameObject pickup =
            CreatePickupObject(
                definition,
                spawnResult.Cell,
                floorRoot,
                dungeonRenderer);

        if (pickup == null)
        {
            return null;
        }

        pickup.name =
            "Item_" + definition.ItemId;

        EnsurePickupCollider(pickup);

        ItemPickup pickupController =
            pickup.GetComponent<ItemPickup>();

        if (pickupController == null)
        {
            pickupController =
                pickup.AddComponent<ItemPickup>();
        }

        pickupController.Initialize(
            definition,
            itemManager,
            floorNumber);

        if (runtimeSpawnReservations != null)
        {
            runtimeSpawnReservations.Add(
                spawnResult.Cell);
        }

        Debug.Log(
            "[ItemSpawner/R8.3] Item SpawnCell 已提交。" +
            "\nRequested=SpawnPoint(Item)" +
            " | Effective=" + spawnResult.Source +
            " | Floor=" + floorNumber +
            " | Seed=" + layout.Seed +
            " | ProgressionScore=" +
            itemManager.ProgressionScore +
            " | ItemId=" + definition.ItemId +
            "\nCoreScope=" +
            (hasTaggedCoreItemScope
                ? "CoreItemCandidateRooms"
                : "LegacyLayoutWideFallback") +
            " | CoreCandidateRooms=" +
            coreItemRoomIndices.Count +
            " | ResolvedInsideCoreCandidate=" +
            (hasTaggedCoreItemScope &&
             DungeonCoreItemRoomScopeR943.ContainsRoomIndex(
                 coreItemRoomIndices,
                 spawnResult.RoomIndex)) +
            "\nRoomIndex=" + spawnResult.RoomIndex +
            " | Cell=" + spawnResult.Cell +
            " | SpawnPointId=" +
            (string.IsNullOrEmpty(spawnResult.SpawnPointId)
                ? "None"
                : spawnResult.SpawnPointId) +
            " | Candidates=" +
            spawnResult.CandidateCount +
            " | Rejected=" +
            spawnResult.RejectedCandidateCount +
            " | SelectionSeed=" +
            spawnResult.SelectionSeed +
            "\nFloorCellsMembership=True" +
            " | MinimumDistanceFromStart=" +
            minimumDistanceFromStart +
            " | MinimumDistanceFromExit=" +
            minimumDistanceFromExit +
            " | SharedReservation=Committed" +
            " | LayoutMutation=None",
            this);

        return pickup;
    }

    private GameObject CreatePickupObject(
        ItemDefinition definition,
        Vector2Int spawnCell,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer)
    {
        if (definition.PickupPrefab != null)
        {
            GameObject instance =
                Instantiate(
                    definition.PickupPrefab,
                    dungeonRenderer.CellToWorld(spawnCell),
                    Quaternion.identity,
                    floorRoot);

            return instance;
        }

        GameObject fallbackPickup =
            dungeonRenderer.CreateSquare(
                "Item_" + definition.ItemId,
                spawnCell,
                new Color(0.86f, 0.89f, 0.94f, 1f),
                floorRoot,
                fallbackSortingOrder,
                false,
                definition.FallbackVisualScale);

        BreathingPickupVisual pulse =
            fallbackPickup.GetComponent<BreathingPickupVisual>();

        if (pulse == null)
        {
            pulse = fallbackPickup.AddComponent<BreathingPickupVisual>();
        }

        return fallbackPickup;
    }

    private static void EnsurePickupCollider(
        GameObject pickup)
    {
        Collider2D pickupCollider =
            pickup.GetComponent<Collider2D>();

        if (pickupCollider == null)
        {
            CircleCollider2D circle =
                pickup.AddComponent<CircleCollider2D>();

            circle.radius = 0.5f;
            pickupCollider = circle;
        }

        pickupCollider.isTrigger = true;
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

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;

            if (value == null)
            {
                return hash;
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }
}
