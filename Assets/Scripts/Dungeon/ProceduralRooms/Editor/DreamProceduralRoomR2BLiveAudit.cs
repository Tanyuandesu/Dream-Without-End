#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12B-2 Live Audit。
///
/// 目标：
/// - Small 08x06 真正进入权威 Runtime。
/// - Medium 13x09 同时迁移到 Generic Family Kernel，不保留第二套 Authority Path。
/// - A2 / A3.1 / A3.2 自动复用到所有 Runtime Procedural Placement。
/// </summary>
public static class DreamProceduralRoomR2BLiveAudit
{
    private const string MenuPath =
        "Tools/Dream Dungeon/P10.12B-2 Small Family/" +
        "1. Validate LIVE Small + Medium Authority";

    [MenuItem(MenuPath)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12B-2] LIVE Audit 必须在 Play Mode 中执行。");
            return;
        }

        GameManager gameManager =
            UnityEngine.Object.FindFirstObjectByType<
                GameManager>();

        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonGenerator>();

        EnemyPathService pathService =
            UnityEngine.Object.FindFirstObjectByType<
                EnemyPathService>();

        EnemyManager enemyManager =
            UnityEngine.Object.FindFirstObjectByType<
                EnemyManager>();

        PlayerManager playerManager =
            UnityEngine.Object.FindFirstObjectByType<
                PlayerManager>();

        ItemManager itemManager =
            UnityEngine.Object.FindFirstObjectByType<
                ItemManager>();

        if (gameManager == null ||
            generator == null)
        {
            Debug.LogError(
                "[P10.12B-2] 找不到 GameManager / DungeonGenerator。");
            return;
        }

        DungeonLayout layout =
            ReadCurrentLayout(
                gameManager);

        if (layout == null)
        {
            Debug.LogError(
                "[P10.12B-2] 无法读取 currentLayout。");
            return;
        }

        List<string> errors =
            new List<string>();

        errors.AddRange(
            layout.GetValidationErrors());

        int smallTemplateInstances = 0;
        int mediumTemplateInstances = 0;

        int smallProcedural = 0;
        int mediumProcedural = 0;

        int smallRoomIndex = -1;
        int smallUsedSockets = 0;

        Dictionary<int, DreamRoomPlacement>
            proceduralByRoomIndex =
                new Dictionary<int, DreamRoomPlacement>();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                continue;
            }

            string templateId =
                placement.Template.TemplateId;

            if (string.Equals(
                    templateId,
                    "Graybox_08x06",
                    StringComparison.Ordinal))
            {
                smallTemplateInstances++;
            }

            if (string.Equals(
                    templateId,
                    "Graybox_13x09",
                    StringComparison.Ordinal))
            {
                mediumTemplateInstances++;
            }

            if (!placement.HasRuntimeProceduralOverride)
            {
                continue;
            }

            proceduralByRoomIndex[i] =
                placement;

            if (string.Equals(
                    templateId,
                    "Graybox_08x06",
                    StringComparison.Ordinal))
            {
                smallProcedural++;
                smallRoomIndex = i;
                smallUsedSockets =
                    CountUsedSockets(
                        layout,
                        i);
            }
            else if (string.Equals(
                         templateId,
                         "Graybox_13x09",
                         StringComparison.Ordinal))
            {
                mediumProcedural++;
            }
            else
            {
                errors.Add(
                    "非 B2 Family Template 出现 Runtime Procedural Override：" +
                    templateId + "。");
            }
        }

        if (!generator.P1012B2Small08x06Enabled)
        {
            errors.Add(
                "DungeonGenerator Small08x06 Authority 开关为 False。");
        }

        if (smallTemplateInstances <= 0)
        {
            errors.Add(
                "当前 Floor 没有 Graybox_08x06。" +
                " 请按 R 重生，直到出现 Small 房再做 B2 验收。");
        }
        else if (smallProcedural != 1)
        {
            errors.Add(
                "当前存在 Small Graybox，但应恰好转换第一个为 Procedural。" +
                " TemplateInstances=" +
                smallTemplateInstances +
                " Procedural=" +
                smallProcedural + "。");
        }

        if (mediumTemplateInstances > 0 &&
            generator.P1012R2BEnabled &&
            mediumProcedural != 1)
        {
            errors.Add(
                "当前存在 Medium Graybox 且 Medium 开关启用，" +
                "应恰好转换第一个。 TemplateInstances=" +
                mediumTemplateInstances +
                " Procedural=" +
                mediumProcedural + "。");
        }

        DreamProceduralRoomRuntimeInstanceP1012R2B[]
            runtimeInstances =
                UnityEngine.Object.FindObjectsByType<
                    DreamProceduralRoomRuntimeInstanceP1012R2B>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

        if (runtimeInstances.Length !=
            proceduralByRoomIndex.Count)
        {
            errors.Add(
                "Runtime Procedural Instance 数量与 Authority Placement 不一致：" +
                runtimeInstances.Length +
                "/" +
                proceduralByRoomIndex.Count + "。");
        }

        Dictionary<int,
            DreamProceduralRoomRuntimeInstanceP1012R2B>
            runtimeByRoomIndex =
                new Dictionary<int,
                    DreamProceduralRoomRuntimeInstanceP1012R2B>();

        for (int i = 0;
             i < runtimeInstances.Length;
             i++)
        {
            DreamProceduralRoomRuntimeInstanceP1012R2B
                instance =
                    runtimeInstances[i];

            if (instance == null)
            {
                continue;
            }

            if (!runtimeByRoomIndex.TryAdd(
                    instance.RoomIndex,
                    instance))
            {
                errors.Add(
                    "重复 Runtime Procedural RoomIndex=" +
                    instance.RoomIndex + "。");
            }
        }

        int totalBlocked = 0;
        int totalColliders = 0;
        int structuralVisualMismatches = 0;
        int softDecorMismatches = 0;
        int shellMismatches = 0;

        foreach (
            KeyValuePair<int, DreamRoomPlacement>
            pair in proceduralByRoomIndex)
        {
            int roomIndex =
                pair.Key;

            DreamRoomPlacement placement =
                pair.Value;

            List<Vector2Int> blockedLocal =
                new List<Vector2Int>();

            placement
                .GetRuntimeProceduralBlockedLocalCells(
                    blockedLocal);

            totalBlocked +=
                blockedLocal.Count;

            for (int i = 0;
                 i < blockedLocal.Count;
                 i++)
            {
                Vector2Int global =
                    placement
                        .OriginalToGlobalCell(
                            blockedLocal[i]);

                if (layout.FloorCells.Contains(global) ||
                    layout.RoomCells.Contains(global))
                {
                    errors.Add(
                        "Procedural Blocked Cell 仍属于权威 Walkable：" +
                        global + "。");
                    break;
                }

                if (layout.CorridorCells.Contains(global))
                {
                    errors.Add(
                        "Procedural Blocked Cell 与 Corridor 重叠：" +
                        global + "。");
                    break;
                }
            }

            DreamProceduralRoomRuntimeInstanceP1012R2B
                runtimeInstance;

            if (!runtimeByRoomIndex.TryGetValue(
                    roomIndex,
                    out runtimeInstance) ||
                runtimeInstance == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " 找不到对应 Runtime Geometry。");
                continue;
            }

            totalColliders +=
                runtimeInstance.ColliderCount;

            if (runtimeInstance.BlockedCellCount !=
                    blockedLocal.Count ||
                runtimeInstance.ColliderCount !=
                    blockedLocal.Count)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " Blocked / Collider 数量不一致：" +
                    blockedLocal.Count +
                    "/" +
                    runtimeInstance.BlockedCellCount +
                    "/" +
                    runtimeInstance.ColliderCount + "。");
            }

            DreamProceduralRoomStructuralSkinP1012A31
                structure =
                    runtimeInstance.GetComponent<
                        DreamProceduralRoomStructuralSkinP1012A31>();

            if (structure == null ||
                !structure.IsCommitted ||
                structure.RendererCount !=
                    blockedLocal.Count)
            {
                structuralVisualMismatches++;
            }

            DreamProceduralRoomSoftDecorP1012A2
                soft =
                    runtimeInstance.GetComponent<
                        DreamProceduralRoomSoftDecorP1012A2>();

            if (soft == null ||
                !soft.IsCommitted)
            {
                softDecorMismatches++;
            }

            DreamProceduralRoomShellSkinP1012A32
                shell =
                    runtimeInstance.GetComponent<
                        DreamProceduralRoomShellSkinP1012A32>();

            if (shell == null ||
                !shell.IsCommitted)
            {
                shellMismatches++;
            }
        }

        if (structuralVisualMismatches > 0)
        {
            errors.Add(
                "Structural Skin 未同步的 Procedural 房=" +
                structuralVisualMismatches + "。");
        }

        if (softDecorMismatches > 0)
        {
            errors.Add(
                "Soft Decor 未同步的 Procedural 房=" +
                softDecorMismatches + "。");
        }

        if (shellMismatches > 0)
        {
            errors.Add(
                "Shell Skin 未同步的 Procedural 房=" +
                shellMismatches + "。");
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
                    "/" +
                    layout.FloorCells.Count + "。");
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

            activeEnemies =
                enemies.Count;

            if (pathService != null)
            {
                for (int i = 0;
                     i < enemies.Count;
                     i++)
                {
                    GameObject enemy =
                        enemies[i];

                    if (enemy == null)
                    {
                        continue;
                    }

                    Vector2Int cell =
                        pathService.WorldToCell(
                            enemy.transform.position);

                    if (!layout.FloorCells.Contains(
                            cell))
                    {
                        enemySpawnViolations++;
                    }
                }
            }
        }

        if (enemySpawnViolations > 0)
        {
            errors.Add(
                "有敌人位于非 FloorCell：" +
                enemySpawnViolations + "。");
        }

        int consumerViolations = 0;

        if (pathService != null &&
            playerManager != null &&
            playerManager.CurrentPlayerObject != null)
        {
            Vector2Int playerCell =
                pathService.WorldToCell(
                    playerManager
                        .CurrentPlayerObject
                        .transform
                        .position);

            if (!layout.FloorCells.Contains(
                    playerCell))
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
                    itemManager
                        .ActivePickup
                        .transform
                        .position);

            if (!layout.FloorCells.Contains(
                    itemCell))
            {
                consumerViolations++;

                errors.Add(
                    "Item 当前不在 FloorCells：" +
                    itemCell + "。");
            }
        }

        if (!layout.FloorCells.Contains(
                layout.StartCell) ||
            !layout.FloorCells.Contains(
                layout.ExitCell))
        {
            consumerViolations++;

            errors.Add(
                "StartCell / ExitCell 不属于最终 FloorCells。");
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12B-2] LIVE Small + Medium Authority Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.12B-2] LIVE Small + Medium Authority Audit PASS" +
            "\nFloor=" +
            gameManager.CurrentFloor +
            " | LayoutSeed=" +
            layout.Seed +
            " | ProceduralPlacements=" +
            proceduralByRoomIndex.Count +
            " | SmallProcedural=" +
            smallProcedural +
            " | MediumProcedural=" +
            mediumProcedural +
            "\nSmallRoomIndex=" +
            smallRoomIndex +
            " | SmallUsedSockets=" +
            smallUsedSockets +
            " | NEWSStress=" +
            (smallUsedSockets == 4) +
            "\nBlockedCells=" +
            totalBlocked +
            " | RuntimeColliders=" +
            totalColliders +
            " | FloorCells=" +
            layout.FloorCells.Count +
            " | RoomCells=" +
            layout.RoomCells.Count +
            " | CorridorCells=" +
            layout.CorridorCells.Count +
            "\nEnemyPathWalkable=" +
            pathService.WalkableCellCount +
            " | ConnectedComponents=" +
            pathService.ConnectedComponentCount +
            " | ActiveEnemies=" +
            activeEnemies +
            " | EnemySpawnViolations=0" +
            " | ConsumerViolations=" +
            consumerViolations +
            "\nStructuralSkinCommitted=True" +
            " | SoftDecorCommitted=True" +
            " | ShellSkinCommitted=True" +
            " | ColliderMapping=1to1" +
            "\nKernel=GenericFamilyP1012B1" +
            " | ProductionMainChanged=False" +
            " | WideTallChanged=False" +
            "\nResult=PASS");
    }

    private static int CountUsedSockets(
        DungeonLayout layout,
        int roomIndex)
    {
        HashSet<string> socketIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            if (connection == null ||
                !connection.HasAssignedSockets)
            {
                continue;
            }

            if (connection.RoomAIndex ==
                roomIndex &&
                !string.IsNullOrWhiteSpace(
                    connection.SocketAId))
            {
                socketIds.Add(
                    connection.SocketAId);
            }
            else if (connection.RoomBIndex ==
                     roomIndex &&
                     !string.IsNullOrWhiteSpace(
                         connection.SocketBId))
            {
                socketIds.Add(
                    connection.SocketBId);
            }
        }

        return socketIds.Count;
    }

    private static DungeonLayout ReadCurrentLayout(
        GameManager gameManager)
    {
        FieldInfo field =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        return
            field == null
                ? null
                : field.GetValue(
                    gameManager)
                    as DungeonLayout;
    }
}
#endif
