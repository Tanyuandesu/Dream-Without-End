using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P10.6：把已经通过 Runtime Probe 的 Production Catalog 正式提交为 GameScene 的房间目录权威。
///
/// 目标：
/// 1. GameScene 从 Graybox_R3 永久切换到 RoomCatalog_Production.asset。
/// 2. Production Catalog 成为唯一运行时目录入口；4 个 Graybox 仅作为目录内部的迁移期占位。
/// 3. 不修改 DungeonGenerator / DungeonRenderer / A* / Enemy AI / Corridor 核心代码。
/// 4. 不修改既有 Seed 策略，只替换 Catalog 引用。
/// 5. 保存前对 Floor 1~3 做真实 Hybrid 预生成，失败则不提交 Scene。
///
/// 注意：P10.6 提交成功后，不再使用 P10.3 的 Restore Graybox Baseline；
/// P10.3 仅保留为迁移历史工具，后续统一 Legacy Cleanup。
/// </summary>
public static class DreamRoomProductionCatalogCommitP106
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.6/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string ProductionCatalogPath =
        "Assets/DreamDungeon/Production/Catalog/RoomCatalog_Production.asset";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string CrossroadTemplateId =
        "Production_Crossroad_01";

    private const string CanonicalCatalogId =
        "Production_Main";

    private const string MigrationCatalogId =
        "Production_Migration_P10_3";

    private static readonly string[] GrayboxPrefabPaths =
    {
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_08x06.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_09x16.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_13x09.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_18x07.prefab"
    };

    [MenuItem(
        MenuRoot + "1. Commit Production Catalog as GameScene Authority",
        false,
        2760)]
    private static void CommitProductionCatalogAsAuthority()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            FailDialog("请先退出 Prefab Mode。");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(scene.path, GameScenePath, StringComparison.Ordinal))
        {
            FailDialog("请先打开 " + GameScenePath + "。");
            return;
        }

        if (scene.isDirty)
        {
            FailDialog(
                "GameScene 当前有未保存修改（Hierarchy 名称后有 *）。\n\n" +
                "请先决定保存或撤销这些修改，再执行 P10.6。\n" +
                "这是为了避免 Production Commit 顺手保存无关改动。");
            return;
        }

        DungeonGenerator generator = FindSceneComponent<DungeonGenerator>(scene);
        DungeonRenderer renderer = FindSceneComponent<DungeonRenderer>(scene);

        if (generator == null || renderer == null)
        {
            FailDialog("GameScene 中找不到 DungeonGenerator 或 DungeonRenderer。");
            return;
        }

        if (renderer.RenderMode != DungeonRenderMode.HybridPrefabRooms)
        {
            FailDialog(
                "DungeonRenderer.RenderMode 必须已经是 HybridPrefabRooms。\n" +
                "P10.6 不会替你修改 Renderer 核心配置。");
            return;
        }

        DreamRoomCatalog productionCatalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(ProductionCatalogPath);

        if (productionCatalog == null)
        {
            FailDialog(
                "找不到 Production Catalog：\n" + ProductionCatalogPath +
                "\n\n请先完成 P10.3 第 1 项。" );
            return;
        }

        List<string> errors = ValidateProductionAssets(productionCatalog);
        if (errors.Count > 0)
        {
            ReportErrors("P10.6 Production 资产前置校验失败", errors);
            return;
        }

        SerializedObject generatorSerialized = new SerializedObject(generator);
        SerializedProperty catalogProperty =
            RequireProperty(generatorSerialized, "templateFirstRoomCatalog");
        SerializedProperty randomProperty =
            RequireProperty(generatorSerialized, "useRandomSeed");
        SerializedProperty seedProperty =
            RequireProperty(generatorSerialized, "fixedSeed");

        UnityEngine.Object previousCatalogObject = catalogProperty.objectReferenceValue;
        bool previousUseRandomSeed = randomProperty.boolValue;
        int previousFixedSeed = seedProperty.intValue;
        string previousCatalogId = productionCatalog.CatalogId;

        bool catalogIdentityChanged = false;
        bool sceneReferenceChanged = false;

        try
        {
            // 正式目录文件路径不再改变，只把迁移期 ID 升级成稳定 ID。
            if (!string.Equals(
                    productionCatalog.CatalogId,
                    CanonicalCatalogId,
                    StringComparison.Ordinal))
            {
                SerializedObject catalogSerialized =
                    new SerializedObject(productionCatalog);
                RequireProperty(catalogSerialized, "catalogId")
                    .stringValue = CanonicalCatalogId;
                catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(productionCatalog);
                catalogIdentityChanged = true;
            }

            // 只替换目录。Seed 策略逐字保留。
            catalogProperty.objectReferenceValue = productionCatalog;
            randomProperty.boolValue = previousUseRandomSeed;
            seedProperty.intValue = previousFixedSeed;
            generatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);
            sceneReferenceChanged = true;

            List<string> postAssignErrors =
                ValidateCommittedSceneState(
                    scene,
                    generator,
                    renderer,
                    productionCatalog);

            if (postAssignErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production Commit 保存前状态校验失败：\n- " +
                    string.Join("\n- ", postAssignErrors));
            }

            string preflightReport;
            if (!TryPreflightFloors(generator, 1, 3, out preflightReport))
            {
                throw new InvalidOperationException(
                    "Production Catalog Floor 1~3 Hybrid 预生成失败：\n" +
                    preflightReport);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("GameScene 保存失败。");
            }

            Selection.activeObject = productionCatalog;
            EditorGUIUtility.PingObject(productionCatalog);

            Debug.Log(
                "[P10.6] Production Catalog 已正式成为 GameScene 房间目录权威。\n" +
                "CatalogPath=" + ProductionCatalogPath + "\n" +
                "CatalogId=" + CanonicalCatalogId +
                " | Entries=5 | ProductionRooms=1 | GrayboxBridge=4\n" +
                "GameSceneCatalog=Production_Main | SceneSaved=True\n" +
                "RenderMode=HybridPrefabRooms\n" +
                "SeedPolicyPreserved=True | UseRandomSeed=" + previousUseRandomSeed +
                " | FixedSeed=" + previousFixedSeed + "\n" +
                preflightReport + "\n" +
                "RuntimeCoreCodeChanged=False | GrayboxAssetsDeleted=False\n" +
                "IMPORTANT=P10.3 Restore Graybox Baseline is retired after this commit.",
                generator);

            EditorUtility.DisplayDialog(
                "P10.6 Production Authority Committed",
                "GameScene 已正式保存为 Production Catalog。\n\n" +
                "当前仍有 4 个 Graybox 作为 Production Catalog 内部迁移占位，" +
                "但运行时目录入口已经只有一个：RoomCatalog_Production.asset。\n\n" +
                "以后不要再执行 P10.3 的 Restore Graybox Baseline。\n" +
                "现在执行 P10.6 第 2 项 Validate Production Baseline。",
                "OK");
        }
        catch (Exception exception)
        {
            // 尽量保持提交的原子性：Scene 未保存前恢复原引用与 Seed。
            if (sceneReferenceChanged && generator != null)
            {
                SerializedObject rollback = new SerializedObject(generator);
                RequireProperty(rollback, "templateFirstRoomCatalog")
                    .objectReferenceValue = previousCatalogObject;
                RequireProperty(rollback, "useRandomSeed")
                    .boolValue = previousUseRandomSeed;
                RequireProperty(rollback, "fixedSeed")
                    .intValue = previousFixedSeed;
                rollback.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(generator);
            }

            if (catalogIdentityChanged && productionCatalog != null)
            {
                SerializedObject rollbackCatalog =
                    new SerializedObject(productionCatalog);
                RequireProperty(rollbackCatalog, "catalogId")
                    .stringValue = previousCatalogId;
                rollbackCatalog.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(productionCatalog);
                AssetDatabase.SaveAssets();
            }

            Debug.LogException(exception);
            FailDialog(
                "P10.6 提交已中止，未主动保存失败状态。\n\n" +
                "请把 Console 第一条红色错误发给我。" );
        }
    }

    [MenuItem(
        MenuRoot + "2. Validate Production Baseline",
        false,
        2761)]
    private static void ValidateProductionBaseline()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。P10.6 基线校验在 Edit Mode 执行。");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        List<string> errors = new List<string>();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(scene.path, GameScenePath, StringComparison.Ordinal))
        {
            errors.Add("当前必须打开 GameScene.unity。");
        }

        DreamRoomCatalog productionCatalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(ProductionCatalogPath);

        if (productionCatalog == null)
        {
            errors.Add("找不到 RoomCatalog_Production.asset。");
        }
        else
        {
            errors.AddRange(ValidateProductionAssets(productionCatalog));

            if (!string.Equals(
                    productionCatalog.CatalogId,
                    CanonicalCatalogId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Production Catalog Id 应为 " + CanonicalCatalogId +
                    "，实际=" + productionCatalog.CatalogId + "。" );
            }
        }

        DungeonGenerator generator =
            scene.IsValid() ? FindSceneComponent<DungeonGenerator>(scene) : null;
        DungeonRenderer renderer =
            scene.IsValid() ? FindSceneComponent<DungeonRenderer>(scene) : null;

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。");
        }

        if (generator != null && renderer != null && productionCatalog != null)
        {
            errors.AddRange(
                ValidateCommittedSceneState(
                    scene,
                    generator,
                    renderer,
                    productionCatalog));
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.6 Production Baseline 校验失败", errors);
            return;
        }

        SerializedObject serialized = new SerializedObject(generator);
        bool useRandomSeed = RequireProperty(serialized, "useRandomSeed").boolValue;
        int fixedSeed = RequireProperty(serialized, "fixedSeed").intValue;

        string preflightReport;
        if (!TryPreflightFloors(generator, 1, 3, out preflightReport))
        {
            FailDialog(
                "P10.6 Floor 1~3 预生成失败。\n\n" + preflightReport);
            return;
        }

        Debug.Log(
            "[P10.6] Production Baseline 校验通过。\n" +
            "GameSceneCatalog=Production_Main | SceneSaved=" + (!scene.isDirty) + "\n" +
            "CatalogEntries=5 | ProductionRooms=1 | GrayboxBridge=4\n" +
            "Crossroad=16x16 | Blocked108 | Walkable148 | Sockets4\n" +
            "ArtLayers=Floor/Objects/Effects | ClosedBlockerDebugSprites=0\n" +
            "RenderMode=HybridPrefabRooms\n" +
            "UseRandomSeed=" + useRandomSeed + " | FixedSeed=" + fixedSeed + "\n" +
            preflightReport + "\n" +
            "SingleRuntimeCatalogAuthority=True\n" +
            "P10.3Restore=Retired | RuntimeCoreCodeChanged=False",
            generator);

        EditorUtility.DisplayDialog(
            "P10.6 Passed",
            "Production Catalog 已成为 GameScene 的单一房间目录权威。\n\n" +
            "4 个 Graybox 仍保留在同一个 Catalog 里作为迁移占位；" +
            "后续每完成一个正式房间，就在这里替换对应占位，而不是再建立第二套 Catalog。",
            "OK");
    }

    private static List<string> ValidateProductionAssets(
        DreamRoomCatalog catalog)
    {
        List<string> errors = new List<string>();

        if (catalog == null)
        {
            errors.Add("Production Catalog 为 null。");
            return errors;
        }

        if (!string.Equals(catalog.CatalogId, CanonicalCatalogId, StringComparison.Ordinal) &&
            !string.Equals(catalog.CatalogId, MigrationCatalogId, StringComparison.Ordinal))
        {
            errors.Add(
                "Production Catalog Id 非预期值：" + catalog.CatalogId + "。" );
        }

        errors.AddRange(catalog.GetValidationErrors());

        if (catalog.Count != 5)
        {
            errors.Add("迁移期 Production Catalog 应有 5 个条目，实际=" + catalog.Count + "。" );
        }

        GameObject crossroadPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadPrefabPath);

        if (crossroadPrefab == null)
        {
            errors.Add("找不到 Crossroad_01 Prefab。");
            return errors;
        }

        DreamRoomTemplate crossroad =
            crossroadPrefab.GetComponent<DreamRoomTemplate>();

        if (crossroad == null)
        {
            errors.Add("Crossroad_01 根节点缺少 DreamRoomTemplate。");
            return errors;
        }

        DreamRoomTemplate catalogCrossroad;
        if (!catalog.TryGetTemplate(CrossroadTemplateId, out catalogCrossroad) ||
            catalogCrossroad == null)
        {
            errors.Add("Production Catalog 缺少 " + CrossroadTemplateId + "。" );
        }
        else if (catalogCrossroad != crossroad)
        {
            errors.Add("Catalog 中的 Crossroad_01 引用不是当前正式 Prefab。");
        }

        if (crossroad.SizeInCells != new Vector2Int(16, 16))
        {
            errors.Add("Crossroad_01 SizeInCells 应为 16x16。");
        }

        List<Vector2Int> blocked = new List<Vector2Int>();
        List<Vector2Int> walkable = new List<Vector2Int>();
        crossroad.GetBlockedCells(blocked);
        crossroad.GetWalkableCells(walkable);

        if (blocked.Count != 108 || walkable.Count != 148)
        {
            errors.Add(
                "Crossroad_01 Geometry 应为 Blocked108 / Walkable148，实际=" +
                blocked.Count + " / " + walkable.Count + "。" );
        }

        if (crossroad.DoorSockets == null || crossroad.DoorSockets.Count != 4)
        {
            errors.Add("Crossroad_01 必须有 4 个 DoorSocket。");
        }

        ValidateCrossroadHierarchy(crossroadPrefab, errors);

        for (int i = 0; i < GrayboxPrefabPaths.Length; i++)
        {
            GameObject grayboxPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(GrayboxPrefabPaths[i]);

            if (grayboxPrefab == null)
            {
                errors.Add("找不到迁移期 Graybox：" + GrayboxPrefabPaths[i]);
                continue;
            }

            DreamRoomTemplate graybox =
                grayboxPrefab.GetComponent<DreamRoomTemplate>();

            if (graybox == null)
            {
                errors.Add("Graybox 缺少 DreamRoomTemplate：" + GrayboxPrefabPaths[i]);
                continue;
            }

            bool foundByReference = false;
            IReadOnlyList<DreamRoomTemplate> roomTemplates = catalog.RoomTemplates;
            for (int j = 0; j < roomTemplates.Count; j++)
            {
                if (roomTemplates[j] == graybox)
                {
                    foundByReference = true;
                    break;
                }
            }

            if (!foundByReference)
            {
                errors.Add("Production Catalog 缺少迁移 Graybox：" + graybox.TemplateId);
            }
        }

        return errors;
    }

    private static void ValidateCrossroadHierarchy(
        GameObject prefab,
        List<string> errors)
    {
        string[] requiredPaths =
        {
            "Visual/Floor/Floor_Runtime",
            "Visual/Objects/Objects_Runtime",
            "Visual/Effects/Effects_Runtime",
            "Visual/Objects/ClosedBlockers",
            "Navigation/Colliders/P10_1_Geometry"
        };

        for (int i = 0; i < requiredPaths.Length; i++)
        {
            if (prefab.transform.Find(requiredPaths[i]) == null)
            {
                errors.Add("Crossroad_01 缺少层级：" + requiredPaths[i]);
            }
        }

        Transform blockers =
            prefab.transform.Find("Visual/Objects/ClosedBlockers");

        if (blockers != null)
        {
            int blockerCount = 0;
            for (int i = 0; i < blockers.childCount; i++)
            {
                Transform blocker = blockers.GetChild(i);
                blockerCount++;

                if (blocker.GetComponent<SpriteRenderer>() != null)
                {
                    errors.Add(
                        blocker.name + " 仍有 P10.0 Debug SpriteRenderer。");
                }

                BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();
                if (collider == null || collider.isTrigger)
                {
                    errors.Add(blocker.name + " 缺少有效实体 BoxCollider2D。");
                }
            }

            if (blockerCount != 4)
            {
                errors.Add("ClosedBlocker 应有 4 个，实际=" + blockerCount + "。" );
            }
        }
    }

    private static List<string> ValidateCommittedSceneState(
        Scene scene,
        DungeonGenerator generator,
        DungeonRenderer renderer,
        DreamRoomCatalog productionCatalog)
    {
        List<string> errors = new List<string>();

        if (renderer.RenderMode != DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add("DungeonRenderer.RenderMode 不是 HybridPrefabRooms。");
        }

        SerializedObject serialized = new SerializedObject(generator);
        UnityEngine.Object assigned =
            RequireProperty(serialized, "templateFirstRoomCatalog")
                .objectReferenceValue;

        if (assigned != productionCatalog)
        {
            errors.Add("DungeonGenerator 尚未引用 RoomCatalog_Production.asset。");
        }

        if (!string.Equals(
                productionCatalog.CatalogId,
                CanonicalCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "Production Catalog Id 尚未正式化为 " + CanonicalCatalogId + "。" );
        }

        return errors;
    }

    private static bool TryPreflightFloors(
        DungeonGenerator generator,
        int firstFloor,
        int lastFloor,
        out string report)
    {
        List<string> lines = new List<string>();

        for (int floor = firstFloor; floor <= lastFloor; floor++)
        {
            DungeonLayout layout;
            string generationReport;

            if (!generator.TryGenerateHybridRuntimeLayout(
                    floor,
                    out layout,
                    out generationReport) ||
                layout == null)
            {
                report =
                    "PreflightFloor=" + floor + " FAILED\n" + generationReport;
                return false;
            }

            int crossroadCount = 0;
            for (int i = 0; i < layout.RoomPlacements.Count; i++)
            {
                DreamRoomPlacement placement = layout.RoomPlacements[i];
                if (placement != null &&
                    placement.Template != null &&
                    string.Equals(
                        placement.Template.TemplateId,
                        CrossroadTemplateId,
                        StringComparison.Ordinal))
                {
                    crossroadCount++;
                }
            }

            if (crossroadCount > 1)
            {
                report =
                    "PreflightFloor=" + floor +
                    " FAILED | CrossroadInstances=" + crossroadCount +
                    " (>1)";
                return false;
            }

            lines.Add(
                "F" + floor +
                ":Rooms=" + layout.RoomPlacements.Count +
                ",Connections=" + layout.Connections.Count +
                ",Crossroad=" + crossroadCount +
                ",FloorCells=" + layout.FloorCells.Count);
        }

        report =
            "PreflightFloors=" + firstFloor + "-" + lastFloor +
            " Passed | " + string.Join(" | ", lines);
        return true;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " 找不到 SerializedProperty：" + propertyName);
        }

        return property;
    }

    private static void ReportErrors(
        string title,
        List<string> errors)
    {
        string message =
            title + "：\n- " + string.Join("\n- ", errors);
        Debug.LogError(message);
        EditorUtility.DisplayDialog(title, message, "OK");
    }

    private static void FailDialog(string message)
    {
        Debug.LogError("[P10.6] " + message);
        EditorUtility.DisplayDialog("P10.6", message, "OK");
    }
}
