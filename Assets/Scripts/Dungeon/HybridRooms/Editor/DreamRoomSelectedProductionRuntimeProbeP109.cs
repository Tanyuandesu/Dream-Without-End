using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P10.9：通用 Production Room Runtime Probe。
///
/// 用途：
/// 1. 对 Project 中当前选中的 Production Room Prefab 搜索一个可复现 Seed；
/// 2. 不修改 Catalog、Room Weight、Prefab 或 Runtime 核心代码；
/// 3. 仅在当前未保存的 GameScene 内临时改 FixedSeed；
/// 4. Play Mode 中确认目标房间真的进入 Hybrid Runtime；
/// 5. 测试后恢复原 Seed 并保存 GameScene。
///
/// 这是通用工具，后续所有 Production Room 都可复用。
/// </summary>
public static class DreamRoomSelectedProductionRuntimeProbeP109
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.9 Selected Room Runtime Probe/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string ProductionCatalogId =
        "Production_Main";

    private const int ProbeFloor = 1;
    private const int SeedSearchCount = 1024;

    private const string SessionActiveKey =
        "DreamDungeon.P109.Active";
    private const string SessionTemplateIdKey =
        "DreamDungeon.P109.TemplateId";
    private const string SessionOriginalRandomKey =
        "DreamDungeon.P109.OriginalUseRandomSeed";
    private const string SessionOriginalSeedKey =
        "DreamDungeon.P109.OriginalFixedSeed";
    private const string SessionProbeSeedKey =
        "DreamDungeon.P109.ProbeSeed";

    [MenuItem(
        MenuRoot + "1. Prepare Selected Room Probe (Do Not Save Scene)",
        false,
        2790)]
    private static void PrepareSelectedRoomProbe()
    {
        List<string> errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("请先退出 Play Mode。");
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("请先退出 Prefab Mode。");
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(scene.path, GameScenePath, StringComparison.Ordinal))
        {
            errors.Add("请先打开 " + GameScenePath + "。");
        }
        else if (scene.isDirty)
        {
            errors.Add(
                "GameScene 当前有未保存修改。请先保存，让 Probe 只临时修改 FixedSeed。");
        }

        if (SessionState.GetBool(SessionActiveKey, false))
        {
            errors.Add(
                "已有尚未 Restore 的 P10.9 Probe。请先执行第 3 项 Restore。");
        }

        DungeonGenerator generator =
            scene.IsValid()
                ? FindSceneComponent<DungeonGenerator>(scene)
                : null;

        DungeonRenderer renderer =
            scene.IsValid()
                ? FindSceneComponent<DungeonRenderer>(scene)
                : null;

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。");
        }
        else if (renderer.RenderMode != DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add("DungeonRenderer.RenderMode 必须为 HybridPrefabRooms。");
        }

        DreamRoomTemplate selectedTemplate =
            TryGetSelectedPrefabTemplate(errors);

        DreamRoomCatalog catalog =
            generator == null
                ? null
                : generator.TemplateFirstRoomCatalog;

        if (catalog == null)
        {
            errors.Add("DungeonGenerator.TemplateFirstRoomCatalog 为空。");
        }
        else if (!string.Equals(
                     catalog.CatalogId,
                     ProductionCatalogId,
                     StringComparison.Ordinal))
        {
            errors.Add(
                "当前 GameScene Catalog 不是 " + ProductionCatalogId + "。");
        }

        if (selectedTemplate != null && catalog != null)
        {
            DreamRoomTemplate catalogTemplate;

            if (!catalog.TryGetTemplate(
                    selectedTemplate.TemplateId,
                    out catalogTemplate))
            {
                errors.Add(
                    "选中房间尚未发布到 Production_Main：" +
                    selectedTemplate.TemplateId + "。");
            }
            else if (catalogTemplate == null)
            {
                errors.Add("Production_Main 中目标 Template 引用为空。");
            }
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.9 Probe 无法准备", errors);
            return;
        }

        FieldInfo randomField =
            RequireGeneratorField("useRandomSeed");
        FieldInfo seedField =
            RequireGeneratorField("fixedSeed");

        bool originalUseRandom =
            (bool)randomField.GetValue(generator);
        int originalFixedSeed =
            (int)seedField.GetValue(generator);

        int searchBaseSeed = originalFixedSeed;
        int probeSeed;
        DungeonLayout probeLayout;
        string searchReport;

        if (!TryFindSeedContainingTemplate(
                generator,
                selectedTemplate.TemplateId,
                searchBaseSeed,
                out probeSeed,
                out probeLayout,
                out searchReport))
        {
            errors.Add(
                "从 Seed " + searchBaseSeed +
                " 起检查 " + SeedSearchCount +
                " 个候选 Seed，仍未找到包含 '" +
                selectedTemplate.TemplateId + "' 的 Floor 1。\n" +
                searchReport);

            ReportErrors("P10.9 Seed 搜索失败", errors);
            return;
        }

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetString(
            SessionTemplateIdKey,
            selectedTemplate.TemplateId);
        SessionState.SetBool(
            SessionOriginalRandomKey,
            originalUseRandom);
        SessionState.SetInt(
            SessionOriginalSeedKey,
            originalFixedSeed);
        SessionState.SetInt(
            SessionProbeSeedKey,
            probeSeed);

        SerializedObject serialized =
            new SerializedObject(generator);

        RequireProperty(serialized, "useRandomSeed")
            .boolValue = false;
        RequireProperty(serialized, "fixedSeed")
            .intValue = probeSeed;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);

        int targetCount =
            CountTemplatePlacements(
                probeLayout,
                selectedTemplate.TemplateId);

        Debug.Log(
            "[P10.9] Selected Production Room Runtime Probe 已准备。\n" +
            "TemplateId=" + selectedTemplate.TemplateId +
            " | Floor=" + ProbeFloor +
            " | ProbeSeed=" + probeSeed + "\n" +
            "TargetInstances=" + targetCount +
            " | Rooms=" + probeLayout.RoomPlacements.Count +
            " | Connections=" + probeLayout.Connections.Count +
            " | FloorCells=" + probeLayout.FloorCells.Count + "\n" +
            "Weight=" + selectedTemplate.RandomWeight +
            " | MaxInstancesPerFloor=" +
            selectedTemplate.MaximumInstancesPerFloor + "\n" +
            "Catalog=" + ProductionCatalogId +
            " | SceneSaved=False\n" +
            "IMPORTANT=现在进入 Play Mode；测试后退出 Play Mode，再执行第 3 项 Restore。",
            generator);

        EditorUtility.DisplayDialog(
            "P10.9 Probe Ready",
            "已找到保证 Floor 1 出现目标房间的 Seed：" +
            probeSeed + "\n\n" +
            "目标：" + selectedTemplate.TemplateId + "\n" +
            "本次预生成实例数：" + targetCount + "\n\n" +
            "当前只临时修改了 GameScene 的 FixedSeed，没有保存。\n" +
            "现在直接进入 Play Mode。\n\n" +
            "进入游戏后执行第 2 项 Validate Live Selected Room。\n" +
            "退出 Play Mode 后执行第 3 项 Restore。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "2. Validate Live Selected Room",
        false,
        2791)]
    private static void ValidateLiveSelectedRoom()
    {
        List<string> errors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add("必须在 Play Mode 中执行。");
        }

        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            errors.Add("没有活动中的 P10.9 Probe。请先执行第 1 项。");
        }

        string templateId =
            SessionState.GetString(SessionTemplateIdKey, string.Empty);

        if (string.IsNullOrWhiteSpace(templateId))
        {
            errors.Add("Probe 没有记录目标 TemplateId。");
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
        else
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
                    "Runtime Catalog 不是 " + ProductionCatalogId + "。");
            }
        }

        if (gameManager == null)
        {
            errors.Add("Play Mode 中找不到 GameManager。");
        }

        DungeonLayout layout = null;

        if (gameManager != null &&
            !TryReadCurrentLayout(gameManager, out layout))
        {
            errors.Add("无法读取 GameManager.currentLayout。");
        }

        int targetCount = 0;

        if (layout != null)
        {
            List<string> layoutErrors = layout.GetValidationErrors();

            for (int i = 0; i < layoutErrors.Count; i++)
            {
                errors.Add("Layout：" + layoutErrors[i]);
            }

            targetCount =
                CountTemplatePlacements(layout, templateId);

            if (targetCount < 1)
            {
                errors.Add(
                    "当前 Floor 1 没有目标房间：" + templateId + "。");
            }
        }

        GameObject generatedRoot =
            GameObject.Find("GeneratedDungeon_Floor_1");

        if (generatedRoot == null)
        {
            errors.Add("找不到 GeneratedDungeon_Floor_1。");
        }

        DreamRoomTemplate liveTemplate =
            generatedRoot == null
                ? null
                : FindTemplateById(
                    generatedRoot.transform,
                    templateId);

        if (liveTemplate == null)
        {
            errors.Add(
                "GeneratedDungeon_Floor_1 中找不到目标 Prefab 实例：" +
                templateId + "。");
        }
        else
        {
            List<string> prefabErrors =
                liveTemplate.GetValidationErrors();

            for (int i = 0; i < prefabErrors.Count; i++)
            {
                errors.Add("Runtime Prefab：" + prefabErrors[i]);
            }
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.9 Live Runtime 校验失败", errors);
            return;
        }

        Debug.Log(
            "[P10.9] Selected Production Room 真实 Runtime 校验通过。\n" +
            "TemplateId=" + templateId +
            " | Instances=" + targetCount + "\n" +
            "Requested=HybridPrefabRooms | Effective=HybridPrefabRooms\n" +
            "Rooms=" + layout.RoomPlacements.Count +
            " | Connections=" + layout.Connections.Count +
            " | CorridorCells=" + layout.CorridorCells.Count +
            " | FloorCells=" + layout.FloorCells.Count + "\n" +
            "PrefabInstance=Live | DoorSockets=" +
            liveTemplate.DoorSockets.Count +
            " | RuntimeCoreCodeChanged=False",
            liveTemplate);

        EditorUtility.DisplayDialog(
            "P10.9 Runtime Passed",
            templateId +
            " 已进入真实 Hybrid Runtime。\n\n" +
            "现在请实际走动检查碰撞、门口、敌人寻路。\n" +
            "测试完成后退出 Play Mode，并执行第 3 项 Restore。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "3. Restore Original Production Seed and Save",
        false,
        2792)]
    private static void RestoreOriginalProductionSeed()
    {
        List<string> errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("请先退出 Play Mode。");
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("请先退出 Prefab Mode。");
        }

        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            errors.Add("没有需要恢复的 P10.9 Probe。");
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(scene.path, GameScenePath, StringComparison.Ordinal))
        {
            errors.Add("请打开 " + GameScenePath + "。");
        }

        DungeonGenerator generator =
            scene.IsValid()
                ? FindSceneComponent<DungeonGenerator>(scene)
                : null;

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (errors.Count > 0)
        {
            ReportErrors("P10.9 Restore 无法开始", errors);
            return;
        }

        bool originalUseRandom =
            SessionState.GetBool(SessionOriginalRandomKey, false);
        int originalSeed =
            SessionState.GetInt(SessionOriginalSeedKey, 12345);
        int probeSeed =
            SessionState.GetInt(SessionProbeSeedKey, originalSeed);
        string templateId =
            SessionState.GetString(SessionTemplateIdKey, string.Empty);

        SerializedObject serialized =
            new SerializedObject(generator);

        RequireProperty(serialized, "useRandomSeed")
            .boolValue = originalUseRandom;
        RequireProperty(serialized, "fixedSeed")
            .intValue = originalSeed;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            errors.Add("GameScene 保存失败。");
            ReportErrors("P10.9 Restore 失败", errors);
            return;
        }

        ClearSession();

        Debug.Log(
            "[P10.9] Production Seed 已恢复并保存。\n" +
            "TemplateId=" + templateId +
            " | ProbeSeed=" + probeSeed + "\n" +
            "RestoredUseRandomSeed=" + originalUseRandom +
            " | RestoredFixedSeed=" + originalSeed + "\n" +
            "Catalog=" + ProductionCatalogId +
            " | SceneSaved=True | CatalogChanged=False",
            generator);

        EditorUtility.DisplayDialog(
            "P10.9 Restored",
            "GameScene 已恢复测试前的 Production Seed，并保存完成。",
            "OK");
    }

    private static DreamRoomTemplate TryGetSelectedPrefabTemplate(
        List<string> errors)
    {
        GameObject selected = Selection.activeObject as GameObject;

        if (selected == null)
        {
            errors.Add(
                "请先在 Project 窗口选中要测试的 Production Room Prefab。" );
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);

        if (string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("当前选择不是 Prefab 资产。");
            return null;
        }

        DreamRoomTemplate template =
            selected.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            errors.Add("选中的 Prefab 根节点没有 DreamRoomTemplate。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(template.TemplateId) ||
            !template.TemplateId.StartsWith(
                "Production_",
                StringComparison.Ordinal))
        {
            errors.Add(
                "当前 Prefab 不是 Production Room：" +
                template.TemplateId + "。");
            return null;
        }

        return template;
    }

    private static bool TryFindSeedContainingTemplate(
        DungeonGenerator generator,
        string templateId,
        int baseSeed,
        out int foundSeed,
        out DungeonLayout foundLayout,
        out string report)
    {
        foundSeed = 0;
        foundLayout = null;
        report = string.Empty;

        FieldInfo randomField =
            RequireGeneratorField("useRandomSeed");
        FieldInfo seedField =
            RequireGeneratorField("fixedSeed");

        object originalRandom = randomField.GetValue(generator);
        object originalSeed = seedField.GetValue(generator);

        int successfulLayouts = 0;
        string lastFailure = string.Empty;

        try
        {
            randomField.SetValue(generator, false);

            for (int offset = 0;
                 offset < SeedSearchCount;
                 offset++)
            {
                int candidateSeed = unchecked(baseSeed + offset);
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
                    lastFailure = string.Join(" | ", layoutErrors);
                    continue;
                }

                successfulLayouts++;

                if (CountTemplatePlacements(layout, templateId) > 0)
                {
                    foundSeed = candidateSeed;
                    foundLayout = layout;
                    report =
                        "SuccessfulLayouts=" + successfulLayouts +
                        " | FoundSeed=" + foundSeed;
                    return true;
                }
            }
        }
        finally
        {
            randomField.SetValue(generator, originalRandom);
            seedField.SetValue(generator, originalSeed);
        }

        report =
            "SuccessfulLayouts=" + successfulLayouts +
            (string.IsNullOrWhiteSpace(lastFailure)
                ? string.Empty
                : " | LastFailure=" + lastFailure);

        return false;
    }

    private static int CountTemplatePlacements(
        DungeonLayout layout,
        string templateId)
    {
        if (layout == null ||
            string.IsNullOrWhiteSpace(templateId))
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < layout.RoomPlacements.Count; i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement != null &&
                placement.Template != null &&
                string.Equals(
                    placement.Template.TemplateId,
                    templateId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
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

    private static FieldInfo RequireGeneratorField(string fieldName)
    {
        FieldInfo field =
            typeof(DungeonGenerator).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            throw new InvalidOperationException(
                "找不到 DungeonGenerator 字段：" + fieldName);
        }

        return field;
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

    private static void ClearSession()
    {
        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseString(SessionTemplateIdKey);
        SessionState.EraseBool(SessionOriginalRandomKey);
        SessionState.EraseInt(SessionOriginalSeedKey);
        SessionState.EraseInt(SessionProbeSeedKey);
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
            "[P10.9] " + title + "：\n- " + message);

        EditorUtility.DisplayDialog(
            title,
            message,
            "OK");
    }
}
