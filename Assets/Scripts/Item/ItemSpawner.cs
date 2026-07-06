using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 ItemDefinition 生成到當前樓層。
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemSpawner : MonoBehaviour
{
    [Header("生成位置")]
    [SerializeField] private bool preferRoomCenters = true;

    [Min(0)]
    [SerializeField] private int minimumDistanceFromStart = 4;

    [Min(0)]
    [SerializeField] private int minimumDistanceFromExit = 2;

    [Header("測試外觀")]
    [SerializeField] private int fallbackSortingOrder = 12;

    public GameObject Spawn(
        ItemDefinition definition,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer dungeonRenderer,
        ItemManager itemManager,
        int floorNumber)
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

        List<Vector2Int> candidates =
            BuildCandidateCells(layout);

        if (candidates.Count == 0)
        {
            Debug.LogWarning(
                "ItemSpawner：找不到適合的道具生成格。");

            return null;
        }

        System.Random random =
            new System.Random(
                CombineSeed(
                    layout.Seed,
                    floorNumber,
                    StableHash(definition.ItemId)));

        Vector2Int spawnCell =
            candidates[random.Next(candidates.Count)];

        GameObject pickup =
            CreatePickupObject(
                definition,
                spawnCell,
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

        return pickup;
    }

    private List<Vector2Int> BuildCandidateCells(
        DungeonLayout layout)
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>();

        if (preferRoomCenters &&
            layout.Rooms != null)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RectInt room = layout.Rooms[i];

                Vector2Int center =
                    new Vector2Int(
                        room.xMin + room.width / 2,
                        room.yMin + room.height / 2);

                TryAddCandidate(
                    center,
                    layout,
                    candidates);
            }
        }

        if (candidates.Count > 0)
        {
            return candidates;
        }

        foreach (Vector2Int cell in layout.FloorCells)
        {
            TryAddCandidate(
                cell,
                layout,
                candidates);
        }

        return candidates;
    }

    private void TryAddCandidate(
        Vector2Int cell,
        DungeonLayout layout,
        List<Vector2Int> candidates)
    {
        if (!layout.FloorCells.Contains(cell) ||
            cell == layout.StartCell ||
            cell == layout.ExitCell)
        {
            return;
        }

        if (Manhattan(cell, layout.StartCell) <
            minimumDistanceFromStart)
        {
            return;
        }

        if (Manhattan(cell, layout.ExitCell) <
            minimumDistanceFromExit)
        {
            return;
        }

        if (!candidates.Contains(cell))
        {
            candidates.Add(cell);
        }
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

        return dungeonRenderer.CreateSquare(
            "Item_" + definition.ItemId,
            spawnCell,
            definition.FallbackColor,
            floorRoot,
            fallbackSortingOrder,
            false,
            definition.FallbackVisualScale);
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

    private static int Manhattan(
        Vector2Int first,
        Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) +
               Mathf.Abs(first.y - second.y);
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
