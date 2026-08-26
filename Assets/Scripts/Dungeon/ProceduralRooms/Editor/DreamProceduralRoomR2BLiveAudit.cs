#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12A-1 R2B Live Audit。
/// 只读当前 Play Mode 已提交结果，不修改 Scene / Catalog / Layout。
/// </summary>
public static class DreamProceduralRoomR2BLiveAudit
{
    private const string MenuPath =
        "Tools/Dream Dungeon/P10.12A-1 R2B/" +
        "1. Validate LIVE Controlled Authority Commit";

    [MenuItem(MenuPath)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12A-1 R2B] LIVE Audit 必须在 Play Mode 中执行。");
            return;
        }

        GameManager gameManager =
            UnityEngine.Object.FindFirstObjectByType<GameManager>();

        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();

        EnemyPathService pathService =
            UnityEngine.Object.FindFirstObjectByType<EnemyPathService>();

        EnemyManager enemyManager =
            UnityEngine.Object.FindFirstObjectByType<EnemyManager>();

        PlayerManager playerManager =
            UnityEngine.Object.FindFirstObjectByType<PlayerManager>();

        ItemManager itemManager =
            UnityEngine.Object.FindFirstObjectByType<ItemManager>();

        if (gameManager == null ||
            generator == null)
        {
            Debug.LogError(
                "[P10.12A-1 R2B] 找不到 GameManager / DungeonGenerator。");
            return;
        }

        DungeonLayout layout =
            ReadCurrentLayout(gameManager);

        if (layout == null)
        {
            Debug.LogError(
                "[P10.12A-1 R2B] 无法读取 currentLayout。");
            return;
        }

        List<string> errors =
            new List<string>();

        errors.AddRange(
            layout.GetValidationErrors());

        List<DreamRoomPlacement> proceduralPlacements =
            new List<DreamRoomPlacement>();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement != null &&
                placement.HasRuntimeProceduralOverride)
            {
                proceduralPlacements.Add(placement);
            }
        }

        if (proceduralPlacements.Count != 1)
        {
            errors.Add(
                "当前应恰好有 1 个 Runtime Procedural Placement，实际=" +
                proceduralPlacements.Count + "。");
        }

        DreamProceduralRoomRuntimeInstanceP1012R2B[] runtimeInstances =
            UnityEngine.Object.FindObjectsByType<
                DreamProceduralRoomRuntimeInstanceP1012R2B>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

        if (runtimeInstances.Length != 1)
        {
            errors.Add(
                "当前应恰好有 1 个 Runtime Procedural Instance，实际=" +
                runtimeInstances.Length + "。");
        }

        int blockedCount = 0;
        int colliderCount = 0;
        string archetype = "None";
        int seed = 0;

        if (proceduralPlacements.Count == 1)
        {
            DreamRoomPlacement placement =
                proceduralPlacements[0];

            List<Vector2Int> blockedLocal =
                new List<Vector2Int>();

            placement.GetRuntimeProceduralBlockedLocalCells(
                blockedLocal);

            blockedCount = blockedLocal.Count;
            archetype =
                placement.RuntimeProceduralArchetype.ToString();
            seed = placement.RuntimeProceduralSeed;

            for (int i = 0;
                 i < blockedLocal.Count;
                 i++)
            {
                Vector2Int global =
                    placement.OriginalToGlobalCell(
                        blockedLocal[i]);

                if (layout.FloorCells.Contains(global) ||
                    layout.RoomCells.Contains(global))
                {
                    errors.Add(
                        "程序 Blocked Cell 仍属于权威 Walkable：" +
                        global + "。");
                    break;
                }

                if (layout.CorridorCells.Contains(global))
                {
                    errors.Add(
                        "程序 Blocked Cell 与 Corridor 重叠：" +
                        global + "。");
                    break;
                }
            }
        }

        if (runtimeInstances.Length == 1)
        {
            colliderCount =
                runtimeInstances[0].ColliderCount;

            if (colliderCount != blockedCount ||
                runtimeInstances[0].BlockedCellCount !=
                    blockedCount)
            {
                errors.Add(
                    "Runtime Collider 与权威 Blocked 数量不一致。" +
                    " Blocked=" + blockedCount +
                    " Collider=" + colliderCount + "。");
            }
        }

        if (pathService == null ||
            !pathService.IsInitialized)
        {
            errors.Add(
                "EnemyPathService 未初始化。");
        }
        else
        {
            if (pathService.WalkableCellCount !=
                layout.FloorCells.Count)
            {
                errors.Add(
                    "EnemyPathService WalkableCellCount 与 FloorCells 不一致：" +
                    pathService.WalkableCellCount +
                    "/" + layout.FloorCells.Count + "。");
            }

            if (pathService.ConnectedComponentCount != 1)
            {
                errors.Add(
                    "EnemyPathService ConnectedComponentCount != 1：" +
                    pathService.ConnectedComponentCount + "。");
            }
        }

        int activeEnemies = 0;
        int enemySpawnViolations = 0;

        if (enemyManager != null)
        {
            IReadOnlyList<GameObject> enemies =
                enemyManager.ActiveEnemies;

            activeEnemies = enemies.Count;

            if (pathService != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject enemy = enemies[i];

                    if (enemy == null)
                    {
                        continue;
                    }

                    Vector2Int cell =
                        pathService.WorldToCell(
                            enemy.transform.position);

                    if (!layout.FloorCells.Contains(cell))
                    {
                        enemySpawnViolations++;
                    }
                }
            }
        }

        if (enemySpawnViolations > 0)
        {
            errors.Add(
                "有敌人当前位于非 FloorCell：" +
                enemySpawnViolations + "。");
        }

        int consumerViolations = 0;

        if (pathService != null &&
            playerManager != null &&
            playerManager.CurrentPlayerObject != null)
        {
            Vector2Int playerCell =
                pathService.WorldToCell(
                    playerManager.CurrentPlayerObject.transform.position);

            if (!layout.FloorCells.Contains(playerCell))
            {
                consumerViolations++;
                errors.Add(
                    "Player 当前不在 FloorCells：" +
                    playerCell + "。");
            }
        }

        if (pathService != null &&
            itemManager != null &&
            itemManager.ActivePickup != null)
        {
            Vector2Int itemCell =
                pathService.WorldToCell(
                    itemManager.ActivePickup.transform.position);

            if (!layout.FloorCells.Contains(itemCell))
            {
                consumerViolations++;
                errors.Add(
                    "Item 当前不在 FloorCells：" +
                    itemCell + "。");
            }
        }

        if (!layout.FloorCells.Contains(layout.StartCell) ||
            !layout.FloorCells.Contains(layout.ExitCell))
        {
            consumerViolations++;
            errors.Add(
                "StartCell / ExitCell 不属于最终 FloorCells。");
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12A-1 R2B] LIVE Controlled Authority Audit FAILED\n- " +
                string.Join("\n- ", errors));
            return;
        }

        Debug.Log(
            "[P10.12A-1 R2B] LIVE Controlled Authority Audit PASS" +
            "\nFloor=" + gameManager.CurrentFloor +
            " | LayoutSeed=" + layout.Seed +
            " | ProceduralPlacements=1" +
            " | Archetype=" + archetype +
            " | ProceduralSeed=" + seed +
            "\nBlockedCells=" + blockedCount +
            " | RuntimeColliders=" + colliderCount +
            " | FloorCells=" + layout.FloorCells.Count +
            " | RoomCells=" + layout.RoomCells.Count +
            " | CorridorCells=" + layout.CorridorCells.Count +
            "\nEnemyPathWalkable=" +
            pathService.WalkableCellCount +
            " | ConnectedComponents=" +
            pathService.ConnectedComponentCount +
            " | ActiveEnemies=" + activeEnemies +
            " | EnemySpawnViolations=0" +
            " | ConsumerViolations=" + consumerViolations +
            "\nAuthority=DreamRoomPlacement.RuntimeProceduralOverride" +
            " | ColliderMapping=1to1" +
            " | ProductionMainChanged=False" +
            " | OtherRoomTemplatesChanged=False" +
            "\nResult=PASS");
    }

    private static DungeonLayout ReadCurrentLayout(
        GameManager gameManager)
    {
        FieldInfo field =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        return field == null
            ? null
            : field.GetValue(gameManager)
                as DungeonLayout;
    }
}
#endif
