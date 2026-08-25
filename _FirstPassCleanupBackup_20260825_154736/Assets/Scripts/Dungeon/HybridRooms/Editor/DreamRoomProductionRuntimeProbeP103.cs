using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P10.3：Crossroad_01 第一次真实 Runtime 接入探针。
///
/// 目标：
/// 1. 建立唯一的 Production Migration Catalog。
/// 2. 继续复用现有 4 个 R3 Graybox 作为迁移期占位，不复制生成算法。
/// 3. Crossroad_01 在迁移期每层最多出现 1 次，保持固定朝向。
/// 4. 在不保存 GameScene 的情况下，临时把 Production Catalog 接入现有 Hybrid Runtime。
/// 5. 用真正的 TryGenerateHybridRuntimeLayout / DungeonRenderer / DoorSocket / Corridor 流程测试。
/// 6. 测试结束后可一键恢复并保存 Graybox 基线。
///
/// 本阶段不修改：DungeonGenerator / DungeonRenderer / A* / Enemy AI / Corridor 算法。
/// CompositeDraft 仍是临时视觉；Floor/Objects/Effects 正式拆层留到 Runtime 链路通过后。
/// </summary>
public static class DreamRoomProductionRuntimeProbeP103
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.3/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string GrayboxCatalogPath =
        "Assets/DreamDungeon/Generated/R3_Graybox/Catalog/RoomCatalog_Graybox.asset";

    private const string GrayboxCatalogId =
        "Graybox_R3";

    private const string ProductionCatalogFolder =
        "Assets/DreamDungeon/Production/Catalog";

    private const string ProductionCatalogPath =
        ProductionCatalogFolder + "/RoomCatalog_Production.asset";

    private const string ProductionCatalogId =
        "Production_Migration_P10_3";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string CrossroadTemplateId =
        "Production_Crossroad_01";

    private static readonly string[] GrayboxPrefabPaths =
    {
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_08x06.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_09x16.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_13x09.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_18x07.prefab"
    };

    private static readonly Vector2Int ExpectedCrossroadSize =
        new Vector2Int(16, 16);

    private const int ExpectedCrossroadBlocked = 108;
    private const int ExpectedCrossroadWalkable = 148;
    private const int ProductionWeight = 10;
    private const int ProductionMaximumInstances = 1;
    private const int BaselineFixedSeed = 12345;
    private const int ProbeSeedSearchCount = 96;
    private const int ProbeFloor = 1;

    [MenuItem(
        MenuRoot + "1. Create or Refresh Production Migration Catalog",
        false,
        2730)]
    private static void CreateOrRefreshProductionMigrationCatalog()
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

        try
        {
            DreamRoomTemplate crossroad =
                LoadAndValidateCrossroadPrefab();

            ConfigureCrossroadMigrationSelection();

            // 重新载入，确保保存后的 Prefab 数据成为 Catalog 引用来源。
            crossroad = LoadAndValidateCrossroadPrefab();

            List<DreamRoomTemplate> templates =
                new List<DreamRoomTemplate>();

            templates.Add(crossroad);

            for (int i = 0; i < GrayboxPrefabPaths.Length; i++)
            {
                templates.Add(
                    LoadTemplateFromPrefab(
                        GrayboxPrefabPaths[i],
                        "R3 Graybox " + i));
            }

            EnsureFolder("Assets/DreamDungeon/Production");
            EnsureFolder(ProductionCatalogFolder);

            DreamRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                    ProductionCatalogPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DreamRoomCatalog>();
                AssetDatabase.CreateAsset(
                    catalog,
                    ProductionCatalogPath);
            }

            SerializedObject serialized =
                new SerializedObject(catalog);

            SerializedProperty catalogId =
                RequireProperty(serialized, "catalogId");

            SerializedProperty roomTemplates =
                RequireProperty(serialized, "roomTemplates");

            catalogId.stringValue = ProductionCatalogId;
            roomTemplates.arraySize = templates.Count;

            for (int i = 0; i < templates.Count; i++)
            {
                roomTemplates
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue = templates[i];
            }

            RequireProperty(serialized, "previewFloorNumber")
                .intValue = 1;
            RequireProperty(serialized, "previewUsedTemplateId")
                .stringValue = string.Empty;
            RequireProperty(serialized, "previewExistingInstances")
                .intValue = 0;
            RequireProperty(serialized, "previewRollCount")
                .intValue = 1000;
            RequireProperty(serialized, "previewRandomSeed")
                .intValue = BaselineFixedSeed;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> errors = catalog.GetValidationErrors();
            AppendProductionCatalogErrors(catalog, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production Catalog 校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);

            Debug.Log(
                "[P10.3] Production Migration Catalog 已建立/刷新。\n" +
                "Catalog=" + ProductionCatalogId + " | Entries=5\n" +
                "ProductionRooms=1 (Crossroad_01) | GrayboxBridge=4\n" +
                "CrossroadWeight=" + ProductionWeight +
                " | CrossroadMaxInstancesPerFloor=" + ProductionMaximumInstances +
                " | QuarterTurns=False\n" +
                "GameSceneChanged=False | RuntimeCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.3 Catalog Ready",
                "Production Migration Catalog 已建立。\n\n" +
                "其中 Crossroad_01 是正式房间；4 个 R3 Graybox 只作为迁移期占位。\n" +
                "GameScene 尚未修改。\n\n" +
                "下一步执行 P10.3 的第 2 项 Prepare Runtime Probe。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog(
                "P10.3 Catalog 建立失败。\n\n" +
                "请把 Console 第一条红色错误发给我。");
        }
    }

    [MenuItem(
        MenuRoot + "2. Prepare Crossroad Runtime Probe (Do Not Save Scene)",
        false,
        2731)]
    private static void PrepareCrossroadRuntimeProbe()
    {
        List<string> errors = new List<string>();

        Scene scene;
        DungeonGenerator generator;
        DungeonRenderer renderer;

        if (!TryGetCleanGrayboxScene(
                out scene,
                out generator,
                out renderer,
                errors))
        {
            ReportErrors("P10.3 Runtime Probe 无法准备", errors);
            return;
        }

        DreamRoomCatalog productionCatalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                ProductionCatalogPath);

        if (productionCatalog == null)
        {
            errors.Add(
                "找不到 " + ProductionCatalogPath +
                "。请先执行第 1 项 Create or Refresh Catalog。");
        }
        else
        {
            errors.AddRange(
                productionCatalog.GetValidationErrors());
            AppendProductionCatalogErrors(
                productionCatalog,
                errors);
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.3 Runtime Probe 无法准备", errors);
            return;
        }

        int probeSeed;
        DungeonLayout probeLayout;
        string probeReport;

        if (!TryFindProbeSeed(
                generator,
                productionCatalog,
                out probeSeed,
                out probeLayout,
                out probeReport))
        {
            errors.Add(
                "在 " + ProbeSeedSearchCount +
                " 个候选 Seed 中没有找到“Floor 1 恰好含 1 个 Crossroad_01”的有效 Hybrid Layout。\n" +
                probeReport);
            ReportErrors("P10.3 Runtime Probe 预生成失败", errors);
            return;
        }

        SetGeneratorProbeState(
            generator,
            productionCatalog,
            probeSeed);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[P10.3] Crossroad Runtime Probe 已准备。\n" +
            "Catalog=" + ProductionCatalogId +
            " | Floor=1 | ProbeSeed=" + probeSeed + "\n" +
            "PreflightRooms=" + probeLayout.RoomPlacements.Count +
            " | CrossroadCount=1" +
            " | Connections=" + probeLayout.Connections.Count +
            " | FloorCells=" + probeLayout.FloorCells.Count + "\n" +
            "RenderMode=HybridPrefabRooms | SceneSaved=False\n" +
            "IMPORTANT=进入 Play Mode 测试；退出后不要保存当前 Scene，先执行 Restore。",
            generator);

        EditorUtility.DisplayDialog(
            "P10.3 Runtime Probe Ready",
            "已找到可复现的 Probe Seed：" + probeSeed + "\n\n" +
            "当前 GameScene 仅在内存中切换为 Production Catalog，并保证 Floor 1 预生成恰好出现 1 个 Crossroad_01。\n\n" +
            "现在直接进入 Play Mode。\n" +
            "看到正式十字路口后，在 Play Mode 执行第 3 项 Validate Live Runtime。\n\n" +
            "测试结束退出 Play Mode 后，务必执行第 4 项 Restore Graybox Baseline。\n" +
            "不要手动保存当前 Scene。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "3. Validate Live Crossroad Runtime",
        false,
        2732)]
    private static void ValidateLiveCrossroadRuntime()
    {
        List<string> errors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add("必须在 Play Mode 中执行。");
            ReportErrors("P10.3 Live Runtime 校验无法开始", errors);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

        if (generator == null)
        {
            errors.Add("Play Mode 中找不到 DungeonGenerator。");
        }

        if (gameManager == null)
        {
            errors.Add("Play Mode 中找不到 GameManager。");
        }

        if (generator != null)
        {
            DreamRoomCatalog catalog =
                generator.TemplateFirstRoomCatalog;

            if (catalog == null ||
                !string.Equals(
                    catalog.CatalogId,
                    ProductionCatalogId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "当前 Runtime Catalog 不是 " +
                    ProductionCatalogId + "。");
            }
        }

        DungeonLayout layout = null;

        if (gameManager != null &&
            !TryReadCurrentLayout(gameManager, out layout))
        {
            errors.Add(
                "无法读取 GameManager.currentLayout；Floor 1 可能尚未完成提交。");
        }

        DreamRoomPlacement crossroadPlacement = null;
        int crossroadCount = 0;

        if (layout != null)
        {
            List<string> layoutErrors =
                layout.GetValidationErrors();

            for (int i = 0; i < layoutErrors.Count; i++)
            {
                errors.Add(
                    "Layout：" + layoutErrors[i]);
            }

            for (int i = 0; i < layout.RoomPlacements.Count; i++)
            {
                DreamRoomPlacement placement =
                    layout.RoomPlacements[i];

                if (placement != null &&
                    placement.Template != null &&
                    string.Equals(
                        placement.Template.TemplateId,
                        CrossroadTemplateId,
                        StringComparison.Ordinal))
                {
                    crossroadCount++;
                    crossroadPlacement = placement;
                }
            }

            if (crossroadCount != 1)
            {
                errors.Add(
                    "Floor 1 应恰好有 1 个 Crossroad_01，实际=" +
                    crossroadCount + "。");
            }

            if (crossroadPlacement != null &&
                crossroadPlacement.ClockwiseQuarterTurns != 0)
            {
                errors.Add(
                    "Crossroad_01 当前应锁定为 0°，实际 QuarterTurns=" +
                    crossroadPlacement.ClockwiseQuarterTurns + "。");
            }
        }

        GameObject generatedRoot =
            GameObject.Find("GeneratedDungeon_Floor_1");

        if (generatedRoot == null)
        {
            errors.Add("找不到 GeneratedDungeon_Floor_1。");
        }

        DreamRoomTemplate liveCrossroad =
            generatedRoot == null
                ? null
                : FindTemplateById(
                    generatedRoot.transform,
                    CrossroadTemplateId);

        if (liveCrossroad == null)
        {
            errors.Add(
                "GeneratedDungeon_Floor_1 中找不到 Crossroad_01 实例。");
        }
        else
        {
            if (liveCrossroad.SizeInCells != ExpectedCrossroadSize)
            {
                errors.Add("Runtime Crossroad Size 不是 16x16。");
            }

            Transform visual =
                liveCrossroad.transform.Find(
                    "Visual/Floor/CompositeDraft_Runtime");

            if (visual == null ||
                visual.GetComponent<SpriteRenderer>() == null)
            {
                errors.Add(
                    "Runtime Crossroad 缺少 CompositeDraft_Runtime SpriteRenderer。");
            }

            Transform geometry =
                liveCrossroad.transform.Find(
                    "Navigation/Colliders/P10_1_Geometry");

            if (geometry == null)
            {
                errors.Add(
                    "Runtime Crossroad 缺少精修 Geometry 根节点。");
            }

            if (liveCrossroad.DoorSockets == null ||
                liveCrossroad.DoorSockets.Count != 4)
            {
                errors.Add(
                    "Runtime Crossroad 应有 4 个 DoorSocket。");
            }
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.3 Live Crossroad Runtime 失败", errors);
            return;
        }

        Debug.Log(
            "[P10.3] Crossroad_01 真实 Runtime 校验通过。\n" +
            "Requested=HybridPrefabRooms | Effective=HybridPrefabRooms\n" +
            "RoomPlacements=" + layout.RoomPlacements.Count +
            " | CrossroadInstances=1 | QuarterTurns=0\n" +
            "Connections=" + layout.Connections.Count +
            " | CorridorCells=" + layout.CorridorCells.Count +
            " | FloorCells=" + layout.FloorCells.Count + "\n" +
            "CompositeDraft=Live | RefinedGeometry=Live | DoorSockets=4\n" +
            "RuntimeCoreCodeChanged=False | ProductionCatalogCommit=False",
            liveCrossroad);

        EditorUtility.DisplayDialog(
            "P10.3 Live Runtime Passed",
            "Crossroad_01 已经通过真实 Hybrid Runtime：\n\n" +
            "Prefab 实例化、房间布局、Socket、程序化走廊、精修碰撞和 CompositeDraft 都进入了真实运行链。\n\n" +
            "现在退出 Play Mode，然后执行第 4 项 Restore Graybox Baseline。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "4. Restore and Save Graybox Baseline",
        false,
        2733)]
    private static void RestoreAndSaveGrayboxBaseline()
    {
        List<string> errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须先退出 Play Mode。");
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。");
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add("必须打开 " + GameScenePath + "。");
        }

        DungeonGenerator generator =
            scene.IsValid()
                ? FindSceneComponent<DungeonGenerator>(scene)
                : null;

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        DreamRoomCatalog graybox =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                GrayboxCatalogPath);

        if (graybox == null ||
            !string.Equals(
                graybox.CatalogId,
                GrayboxCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add("无法读取权威 Graybox Catalog。");
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.3 Graybox 恢复失败", errors);
            return;
        }

        SerializedObject serialized =
            new SerializedObject(generator);

        RequireProperty(serialized, "templateFirstRoomCatalog")
            .objectReferenceValue = graybox;
        RequireProperty(serialized, "useRandomSeed")
            .boolValue = false;
        RequireProperty(serialized, "fixedSeed")
            .intValue = BaselineFixedSeed;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            errors.Add("GameScene 保存失败。");
            ReportErrors("P10.3 Graybox 恢复失败", errors);
            return;
        }

        Debug.Log(
            "[P10.3] Graybox 基线已恢复并保存。\n" +
            "Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineFixedSeed +
            " | SceneSaved=True\n" +
            "ProductionCatalogRetained=True | CrossroadPrefabRetained=True\n" +
            "GameSceneProductionCommit=False",
            generator);

        EditorUtility.DisplayDialog(
            "P10.3 Baseline Restored",
            "GameScene 已恢复并保存 Graybox 基线。\n\n" +
            "Production Catalog 和 Crossroad_01 正式资产仍然保留，下一阶段可以在已验证 Runtime 的前提下继续推进。",
            "OK");
    }

    private static DreamRoomTemplate LoadAndValidateCrossroadPrefab()
    {
        DreamRoomTemplate template =
            LoadTemplateFromPrefab(
                CrossroadPrefabPath,
                "Crossroad_01");

        List<string> errors = new List<string>();

        if (!string.Equals(
                template.TemplateId,
                CrossroadTemplateId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "TemplateId 应为 " + CrossroadTemplateId + "。");
        }

        if (template.SizeInCells != ExpectedCrossroadSize)
        {
            errors.Add("SizeInCells 应为 16x16。");
        }

        List<Vector2Int> blocked = new List<Vector2Int>();
        List<Vector2Int> walkable = new List<Vector2Int>();
        template.GetBlockedCells(blocked);
        template.GetWalkableCells(walkable);

        if (blocked.Count != ExpectedCrossroadBlocked)
        {
            errors.Add(
                "Blocked Cells 应为 " +
                ExpectedCrossroadBlocked +
                "，实际=" + blocked.Count + "。请先完成 P10.2.5。");
        }

        if (walkable.Count != ExpectedCrossroadWalkable)
        {
            errors.Add(
                "Walkable Cells 应为 " +
                ExpectedCrossroadWalkable +
                "，实际=" + walkable.Count + "。请先完成 P10.2.5。");
        }

        if (template.DoorSockets == null ||
            template.DoorSockets.Count != 4)
        {
            errors.Add("Crossroad_01 必须有 4 个 DoorSocket。");
        }

        Transform composite =
            template.transform.Find(
                "Visual/Floor/CompositeDraft_Runtime");

        if (composite == null ||
            composite.GetComponent<SpriteRenderer>() == null)
        {
            errors.Add(
                "找不到 P10.2 CompositeDraft_Runtime。");
        }

        Transform geometry =
            template.transform.Find(
                "Navigation/Colliders/P10_1_Geometry");

        if (geometry == null)
        {
            errors.Add(
                "找不到 P10.2.5 精修 Geometry 根节点。");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Crossroad_01 不满足 P10.3 前置条件：\n- " +
                string.Join("\n- ", errors));
        }

        return template;
    }

    private static void ConfigureCrossroadMigrationSelection()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(
                CrossroadPrefabPath);

            DreamRoomTemplate template =
                root.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException(
                    "Crossroad Prefab 根节点缺少 DreamRoomTemplate。");
            }

            SerializedObject serialized =
                new SerializedObject(template);

            RequireProperty(serialized, "randomWeight")
                .intValue = ProductionWeight;
            RequireProperty(serialized, "maximumInstancesPerFloor")
                .intValue = ProductionMaximumInstances;
            RequireProperty(serialized, "allowQuarterTurns")
                .boolValue = false;
            RequireProperty(serialized, "roomTags")
                .intValue = (int)DreamRoomTag.Standard;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                CrossroadPrefabPath);
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AppendProductionCatalogErrors(
        DreamRoomCatalog catalog,
        List<string> errors)
    {
        if (catalog == null)
        {
            errors.Add("Production Catalog 为 null。");
            return;
        }

        if (!string.Equals(
                catalog.CatalogId,
                ProductionCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "CatalogId 应为 " + ProductionCatalogId + "。");
        }

        if (catalog.Count != 5)
        {
            errors.Add("Production Catalog 当前应有 5 个条目。");
        }

        DreamRoomTemplate crossroad;

        if (!catalog.TryGetTemplate(
                CrossroadTemplateId,
                out crossroad) ||
            crossroad == null)
        {
            errors.Add("Production Catalog 缺少 Crossroad_01。");
            return;
        }

        if (crossroad.RandomWeight != ProductionWeight)
        {
            errors.Add(
                "Crossroad RandomWeight 应为 " + ProductionWeight + "。");
        }

        if (crossroad.MaximumInstancesPerFloor !=
            ProductionMaximumInstances)
        {
            errors.Add(
                "Crossroad MaximumInstancesPerFloor 应为 " +
                ProductionMaximumInstances + "。");
        }

        if (crossroad.AllowQuarterTurns)
        {
            errors.Add("Crossroad 迁移期必须锁定 QuarterTurns=False。");
        }
    }

    private static bool TryGetCleanGrayboxScene(
        out Scene scene,
        out DungeonGenerator generator,
        out DungeonRenderer renderer,
        List<string> errors)
    {
        scene = SceneManager.GetActiveScene();
        generator = null;
        renderer = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须先退出 Play Mode。");
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。");
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("当前没有有效 Scene。");
            return false;
        }

        if (!string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add(
                "必须打开 " + GameScenePath +
                "，当前为 " + scene.path + "。");
            return false;
        }

        if (scene.isDirty)
        {
            errors.Add(
                "GameScene 当前有未保存修改。请先保存/处理现有修改，再开始 Runtime Probe。");
        }

        generator = FindSceneComponent<DungeonGenerator>(scene);
        renderer = FindSceneComponent<DungeonRenderer>(scene);

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。");
        }

        if (generator != null)
        {
            DreamRoomCatalog current =
                generator.TemplateFirstRoomCatalog;

            if (current == null ||
                !string.Equals(
                    current.CatalogId,
                    GrayboxCatalogId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "开始前 GameScene 必须使用权威 Graybox Catalog。" );
            }
        }

        if (renderer != null &&
            renderer.RenderMode != DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add(
                "DungeonRenderer.RenderMode 必须为 HybridPrefabRooms。");
        }

        return errors.Count == 0;
    }

    private static bool TryFindProbeSeed(
        DungeonGenerator generator,
        DreamRoomCatalog productionCatalog,
        out int foundSeed,
        out DungeonLayout foundLayout,
        out string report)
    {
        foundSeed = 0;
        foundLayout = null;
        report = string.Empty;

        FieldInfo catalogField =
            RequireGeneratorField("templateFirstRoomCatalog");
        FieldInfo randomField =
            RequireGeneratorField("useRandomSeed");
        FieldInfo seedField =
            RequireGeneratorField("fixedSeed");

        object originalCatalog = catalogField.GetValue(generator);
        object originalRandom = randomField.GetValue(generator);
        object originalSeed = seedField.GetValue(generator);

        int successCount = 0;
        string lastFailure = string.Empty;

        try
        {
            catalogField.SetValue(generator, productionCatalog);
            randomField.SetValue(generator, false);

            for (int offset = 0;
                 offset < ProbeSeedSearchCount;
                 offset++)
            {
                int candidateSeed =
                    BaselineFixedSeed + offset;

                seedField.SetValue(generator, candidateSeed);

                DungeonLayout layout;
                string generationReport;

                if (!generator.TryGenerateHybridRuntimeLayout(
                        ProbeFloor,
                        out layout,
                        out generationReport))
                {
                    lastFailure = generationReport;
                    continue;
                }

                if (layout == null)
                {
                    continue;
                }

                List<string> layoutErrors =
                    layout.GetValidationErrors();

                if (layoutErrors.Count > 0)
                {
                    lastFailure =
                        string.Join(" | ", layoutErrors);
                    continue;
                }

                successCount++;

                int crossroadCount =
                    CountCrossroadPlacements(layout);

                if (crossroadCount == 1)
                {
                    foundSeed = candidateSeed;
                    foundLayout = layout;
                    report =
                        "SearchSuccesses=" + successCount +
                        " | FoundSeed=" + foundSeed;
                    return true;
                }
            }
        }
        finally
        {
            catalogField.SetValue(generator, originalCatalog);
            randomField.SetValue(generator, originalRandom);
            seedField.SetValue(generator, originalSeed);
        }

        report =
            "SuccessfulLayouts=" + successCount +
            " | LastFailure=" + lastFailure;
        return false;
    }

    private static int CountCrossroadPlacements(
        DungeonLayout layout)
    {
        int count = 0;

        if (layout == null)
        {
            return count;
        }

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement != null &&
                placement.Template != null &&
                string.Equals(
                    placement.Template.TemplateId,
                    CrossroadTemplateId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static void SetGeneratorProbeState(
        DungeonGenerator generator,
        DreamRoomCatalog catalog,
        int fixedSeed)
    {
        SerializedObject serialized =
            new SerializedObject(generator);

        RequireProperty(serialized, "templateFirstRoomCatalog")
            .objectReferenceValue = catalog;
        RequireProperty(serialized, "useRandomSeed")
            .boolValue = false;
        RequireProperty(serialized, "fixedSeed")
            .intValue = fixedSeed;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
    }

    private static FieldInfo RequireGeneratorField(
        string fieldName)
    {
        FieldInfo field =
            typeof(DungeonGenerator).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            throw new InvalidOperationException(
                "DungeonGenerator 找不到字段：" + fieldName);
        }

        return field;
    }

    private static bool TryReadCurrentLayout(
        GameManager gameManager,
        out DungeonLayout layout)
    {
        layout = null;

        FieldInfo field =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            return false;
        }

        layout = field.GetValue(gameManager) as DungeonLayout;
        return layout != null;
    }

    private static DreamRoomTemplate FindTemplateById(
        Transform root,
        string templateId)
    {
        if (root == null)
        {
            return null;
        }

        DreamRoomTemplate[] templates =
            root.GetComponentsInChildren<DreamRoomTemplate>(true);

        for (int i = 0; i < templates.Length; i++)
        {
            DreamRoomTemplate template = templates[i];

            if (template != null &&
                string.Equals(
                    template.TemplateId,
                    templateId,
                    StringComparison.Ordinal))
            {
                return template;
            }
        }

        return null;
    }

    private static DreamRoomTemplate LoadTemplateFromPrefab(
        string path,
        string label)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            throw new InvalidOperationException(
                label + " Prefab 不存在：" + path);
        }

        DreamRoomTemplate template =
            prefab.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            throw new InvalidOperationException(
                label + " 根节点缺少 DreamRoomTemplate：" + path);
        }

        return template;
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
                "找不到 SerializedProperty：" + propertyName);
        }

        return property;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent =
            System.IO.Path.GetDirectoryName(path)
                .Replace('\\', '/');

        string name =
            System.IO.Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
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
            T component =
                roots[i].GetComponentInChildren<T>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void ReportErrors(
        string title,
        List<string> errors)
    {
        string message =
            errors == null || errors.Count == 0
                ? "未知错误。"
                : string.Join("\n- ", errors);

        Debug.LogError(
            "[P10.3] " + title + "：\n- " + message);

        EditorUtility.DisplayDialog(
            title,
            message,
            "OK");
    }

    private static void FailDialog(string message)
    {
        Debug.LogError("[P10.3] " + message);
        EditorUtility.DisplayDialog(
            "P10.3",
            message,
            "OK");
    }
}
