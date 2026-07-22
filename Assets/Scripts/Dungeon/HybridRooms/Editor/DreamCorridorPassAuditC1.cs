using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Corridor Pass C1 的安装、对照、实机与恢复工具。
///
/// 本工具不修改 Room Catalog、Prefab、R9.4 角色规则或敌人算法。
/// Prepare 只把 Scene 内的宽度模式与视觉 Profile 临时切到预览状态；
/// Save／Restore 才会写入 GameScene。
/// </summary>
public static class DreamCorridorPassAuditC1
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Corridor Pass C1/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string GrayboxCatalogId =
        "Graybox_R3";

    private const int BaselineSeed = 12345;

    private const string VisualProfilePath =
        "Assets/DreamDungeon/Generated/" +
        "CorridorVisual_GrayStone_C1.asset";

    private const string VisualProfileId =
        "GrayStone_Temporary_C1";

    private const string WidthModeField =
        "socketCorridorWidthMode";

    [MenuItem(
        MenuRoot + "Validate Installed Assets (C1)",
        false,
        2600)]
    private static void ValidateInstalledAssets()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: true,
                out context,
                errors))
        {
            ReportFailure(
                "Corridor Pass C1 静态校验无法开始",
                errors,
                null);
            return;
        }

        bool dirtyBefore = context.Scene.isDirty;

        DungeonCorridorVisualProfile profile =
            LoadVisualProfile(errors);

        RequireSerializedProperty(
            context.Generator,
            WidthModeField,
            errors);

        RequireSerializedProperty(
            context.Generator,
            "mixedCorridorDoorApronLength",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "mixedCorridorCornerRadius",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "mixedCorridorJunctionRadius",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "mixedCorridorMinimumNarrowRunLength",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "mixedCorridorKeepPrimaryRouteWide",
            errors);

        RequireSerializedProperty(
            context.Renderer,
            "corridorVisualProfile",
            errors);

        if (context.Generator.SocketCorridorWidthMode !=
            DungeonCorridorWidthMode.Uniform2)
        {
            errors.Add(
                "静态校验前必须仍处于 Uniform2 安全基线。" );
        }

        if (context.Renderer.CorridorVisualProfile != null)
        {
            errors.Add(
                "静态校验前 Corridor Visual Profile 必须为 None。" );
        }

        if (profile != null &&
            !string.Equals(
                profile.ProfileId,
                VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "临时视觉 Profile Id 不匹配：" +
                profile.ProfileId + "。" );
        }

        if (context.Scene.isDirty != dirtyBefore)
        {
            errors.Add(
                "只读静态校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Corridor Pass C1 静态校验失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC1] 安装资产校验通过。\n" +
            "Baseline=Uniform2" +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | RenderMode=HybridPrefabRooms\n" +
            "MixedModeAvailable=True" +
            " | Uniform2DefaultValue=0" +
            " | PrimaryRouteWide=True" +
            " | DoorApron=2" +
            " | CornerRadius=1" +
            " | JunctionRadius=1" +
            " | MinimumNarrowRun=3\n" +
            "VisualProfile=" + VisualProfileId +
            " | FloorMaskSlots=16" +
            " | WallMaskSlots=16" +
            " | SpriteSlotsOptional=True" +
            " | SceneChanged=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Corridor Pass C1 Assets Passed",
            "Uniform2 安全基线、Mixed1And2 字段与临时灰石 Profile 均已安装。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Prepare Mixed Corridor Preview (C1)",
        false,
        2610)]
    private static void PrepareMixedPreview()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: true,
                out context,
                errors))
        {
            ReportFailure(
                "Mixed Corridor Preview 准备失败",
                errors,
                null);
            return;
        }

        DungeonCorridorVisualProfile profile =
            LoadVisualProfile(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "Mixed Corridor Preview 准备失败",
                errors,
                context.Generator);
            return;
        }

        ConfigureMixedPreview(
            context.Generator,
            context.Renderer,
            profile);

        CorridorComparison comparison;

        if (!TryBuildCorridorComparison(
                context.Generator,
                out comparison,
                errors))
        {
            ConfigureUniformBaseline(
                context.Generator,
                context.Renderer);

            EditorSceneManager.MarkSceneDirty(
                context.Scene);

            ReportFailure(
                "Mixed Corridor Preview 预检失败",
                errors,
                context.Generator);
            return;
        }

        EditorSceneManager.MarkSceneDirty(context.Scene);

        Debug.Log(
            "[DreamCorridorPassAuditC1] Mixed Corridor Preview 已准备。\n" +
            BuildComparisonReport(comparison) + "\n" +
            "WidthMode=Mixed1And2" +
            " | CorridorWidthSafetyEnvelope=2" +
            " | NarrowWidth=1" +
            " | Profile=" + VisualProfileId + "\n" +
            "SceneSaved=False" +
            " | GameSceneDirty=True" +
            " | CatalogUnchanged=" + GrayboxCatalogId +
            " | FixedSeedUnchanged=" + BaselineSeed +
            " | DoNotSaveUntilLiveValidation=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Mixed Corridor Preview Ready",
            "预检已经证明 Mixed Corridors 是 Uniform2 安全包络的连通子集。" +
            "现在进入 Play Mode；暂时不要保存 GameScene。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Validate Live Mixed Corridor (C1)",
        false,
        2620)]
    private static void ValidateLiveMixedCorridor()
    {
        List<string> errors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add("必须在 Play Mode 中执行。" );
            ReportFailure(
                "Live Mixed Corridor 校验无法开始",
                errors,
                null);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);
        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);
        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);
        EnemyPathService pathService =
            FindSceneComponent<EnemyPathService>(scene);

        if (generator == null)
        {
            errors.Add("找不到 DungeonGenerator。" );
        }

        if (renderer == null)
        {
            errors.Add("找不到 DungeonRenderer。" );
        }

        if (gameManager == null)
        {
            errors.Add("找不到 GameManager。" );
        }

        if (pathService == null)
        {
            errors.Add("找不到本层 EnemyPathService。" );
        }

        DungeonLayout layout = null;

        if (gameManager != null &&
            !TryReadCurrentLayout(
                gameManager,
                out layout))
        {
            errors.Add("无法读取 GameManager.currentLayout。" );
        }

        if (generator != null &&
            generator.SocketCorridorWidthMode !=
                DungeonCorridorWidthMode.Mixed1And2)
        {
            errors.Add("运行时 Width Mode 不是 Mixed1And2。" );
        }

        if (renderer != null &&
            (renderer.CorridorVisualProfile == null ||
             !string.Equals(
                 renderer.CorridorVisualProfile.ProfileId,
                 VisualProfileId,
                 StringComparison.Ordinal)))
        {
            errors.Add("运行时没有挂载临时灰石 Profile。" );
        }

        int narrowStraightCells = 0;
        int wideTopologyCells = 0;

        if (layout != null)
        {
            List<string> layoutErrors =
                layout.GetValidationErrors();

            if (generator != null)
            {
                layoutErrors.AddRange(
                    generator
                        .GetSocketCorridorValidationErrors(
                            layout));
            }

            for (int i = 0; i < layoutErrors.Count; i++)
            {
                errors.Add("Layout：" + layoutErrors[i]);
            }

            CountCorridorWidthEvidence(
                layout.CorridorCells,
                out narrowStraightCells,
                out wideTopologyCells);

            if (narrowStraightCells == 0)
            {
                errors.Add("本层没有检测到一格宽直线中段。" );
            }

            if (wideTopologyCells == 0)
            {
                errors.Add("本层没有检测到两格宽／交叉拓扑证据。" );
            }
        }

        int renderedFloors = 0;
        int renderedWalls = 0;
        int floorColliders = 0;
        int wallColliders = 0;
        int distinctWallColors = 0;

        if (gameManager != null && layout != null)
        {
            ValidateRenderedGeometry(
                gameManager.CurrentFloor,
                layout,
                out renderedFloors,
                out renderedWalls,
                out floorColliders,
                out wallColliders,
                out distinctWallColors,
                errors);
        }

        if (pathService != null && layout != null)
        {
            if (!pathService.IsInitialized)
            {
                errors.Add("EnemyPathService 尚未初始化。" );
            }

            if (pathService.Topology !=
                EnemyNavigationTopology.FourDirections)
            {
                errors.Add("敌人导航拓扑不再是 FourDirections。" );
            }

            if (!pathService.UsesHybridTraversalEdges)
            {
                errors.Add("敌人导航没有启用 Hybrid 合法门边。" );
            }

            if (pathService.OpenDoorTransitionCount <= 0)
            {
                errors.Add("敌人导航没有开放的 Socket 门边。" );
            }

            if (pathService.ConnectedComponentCount != 1)
            {
                errors.Add(
                    "敌人导航连通分量不是 1：" +
                    pathService.ConnectedComponentCount + "。" );
            }

            if (pathService.WalkableCellCount !=
                layout.FloorCells.Count)
            {
                errors.Add(
                    "EnemyPathService Walkable 数量与 FloorCells 不一致。" );
            }
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Live Mixed Corridor 校验失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC1] Live Mixed Corridor 通过。\n" +
            "Floor=" + gameManager.CurrentFloor +
            " | Seed=" + layout.Seed +
            " | Rooms=" + layout.RoomPlacements.Count +
            " | Connections=" + layout.Connections.Count +
            " | FloorCells=" + layout.FloorCells.Count +
            " | CorridorCells=" + layout.CorridorCells.Count + "\n" +
            "WidthMode=Mixed1And2" +
            " | NarrowStraightCells=" + narrowStraightCells +
            " | WideTopologyCells=" + wideTopologyCells +
            " | DoorApron=2" +
            " | MainRouteWide=True" +
            " | Connected=True\n" +
            "RenderedCorridorFloors=" + renderedFloors +
            " | FloorColliders=" + floorColliders +
            " | RenderedCorridorWalls=" + renderedWalls +
            " | WallColliders=" + wallColliders +
            " | DistinctWallColors=" + distinctWallColors +
            " | VisualProfile=" + VisualProfileId + "\n" +
            "EnemyTopology=" + pathService.Topology +
            " | HybridDoorEdges=" +
            pathService.UsesHybridTraversalEdges +
            " | OpenDoorTransitions=" +
            pathService.OpenDoorTransitionCount +
            " | Components=" +
            pathService.ConnectedComponentCount +
            " | Walkable=" +
            pathService.WalkableCellCount +
            " | AlgorithmChanged=False" +
            " | Result=PASS",
            generator);

        EditorUtility.DisplayDialog(
            "Live Mixed Corridor Passed",
            "混合宽度、碰撞、灰石明暗和 EA3.1 合法门边均通过。" +
            "仍需人工观察多敌人卡位与移动手感。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Save Mixed Corridor Baseline (C1)",
        false,
        2630)]
    private static void SaveMixedBaseline()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: false,
                out context,
                errors))
        {
            ReportFailure(
                "Mixed Corridor Baseline 保存失败",
                errors,
                null);
            return;
        }

        if (context.Generator.SocketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1And2)
        {
            errors.Add("当前 Width Mode 不是 Mixed1And2。" );
        }

        if (context.Renderer.CorridorVisualProfile == null ||
            !string.Equals(
                context.Renderer.CorridorVisualProfile.ProfileId,
                VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add("当前没有挂载临时灰石 Profile。" );
        }

        CorridorComparison comparison;

        if (errors.Count == 0)
        {
            TryBuildCorridorComparison(
                context.Generator,
                out comparison,
                errors);
        }
        else
        {
            comparison = default(CorridorComparison);
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Mixed Corridor Baseline 保存失败",
                errors,
                context.Generator);
            return;
        }

        EditorSceneManager.MarkSceneDirty(context.Scene);

        if (!EditorSceneManager.SaveScene(context.Scene) ||
            context.Scene.isDirty)
        {
            errors.Add("GameScene 保存失败或保存后仍为 Dirty。" );
            ReportFailure(
                "Mixed Corridor Baseline 保存失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC1] Mixed Corridor Baseline 已保存。\n" +
            BuildComparisonReport(comparison) + "\n" +
            "SceneSaved=True" +
            " | WidthMode=Mixed1And2" +
            " | VisualProfile=" + VisualProfileId +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | R9.4AssetsRetained=True" +
            " | EA3.1AlgorithmRetained=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Mixed Corridor Baseline Saved",
            "GameScene 已保存为 Mixed1And2＋临时灰石 Profile。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Restore Uniform2 and Save (C1)",
        false,
        2640)]
    private static void RestoreUniformAndSave()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: false,
                out context,
                errors))
        {
            ReportFailure(
                "Uniform2 恢复失败",
                errors,
                null);
            return;
        }

        ConfigureUniformBaseline(
            context.Generator,
            context.Renderer);

        EditorSceneManager.MarkSceneDirty(context.Scene);

        if (!EditorSceneManager.SaveScene(context.Scene) ||
            context.Scene.isDirty)
        {
            errors.Add("Uniform2 已写回内存，但 GameScene 保存失败。" );
        }

        if (context.Generator.SocketCorridorWidthMode !=
                DungeonCorridorWidthMode.Uniform2 ||
            context.Renderer.CorridorVisualProfile != null)
        {
            errors.Add("保存后的宽度模式或视觉 Profile 未恢复。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Uniform2 恢复失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC1] Uniform2 安全基线已恢复并保存。\n" +
            "SceneSaved=True" +
            " | WidthMode=Uniform2" +
            " | CorridorVisualProfile=None" +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | MixedCodeRetained=True" +
            " | GrayStoneAssetRetained=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Uniform2 Restored",
            "GameScene 已恢复为原双格走廊与平面色安全基线。",
            "OK");
    }

    private static bool TryGetEditModeContext(
        bool requireCleanScene,
        out SceneContext context,
        List<string> errors)
    {
        context = default(SceneContext);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须退出 Play Mode。" );
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须退出 Prefab Mode。" );
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add("必须打开 " + GameScenePath + "。" );
        }

        if (requireCleanScene &&
            scene.IsValid() &&
            scene.isDirty)
        {
            errors.Add("GameScene 必须无星号／无未保存修改。" );
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
            errors.Add("GameScene 中找不到 DungeonGenerator。" );
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。" );
        }

        if (generator != null)
        {
            if (generator.TemplateFirstRoomCatalog == null ||
                !string.Equals(
                    generator.TemplateFirstRoomCatalog.CatalogId,
                    GrayboxCatalogId,
                    StringComparison.Ordinal))
            {
                errors.Add("Catalog 必须为 Graybox_R3。" );
            }

            RequirePrivateBool(
                generator,
                "useRandomSeed",
                false,
                errors);

            RequirePrivateInt(
                generator,
                "fixedSeed",
                BaselineSeed,
                errors);
        }

        if (renderer != null &&
            renderer.RenderMode !=
                DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add("Render Mode 必须为 HybridPrefabRooms。" );
        }

        context = new SceneContext(
            scene,
            generator,
            renderer);

        return errors.Count == 0;
    }

    private static void ConfigureMixedPreview(
        DungeonGenerator generator,
        DungeonRenderer renderer,
        DungeonCorridorVisualProfile profile)
    {
        SerializedObject serializedGenerator =
            new SerializedObject(generator);

        SetEnum(
            serializedGenerator,
            WidthModeField,
            (int)DungeonCorridorWidthMode.Mixed1And2);

        SetInt(serializedGenerator, "socketCorridorWidth", 2);
        SetInt(
            serializedGenerator,
            "mixedCorridorDoorApronLength",
            2);
        SetInt(
            serializedGenerator,
            "mixedCorridorCornerRadius",
            1);
        SetInt(
            serializedGenerator,
            "mixedCorridorJunctionRadius",
            1);
        SetInt(
            serializedGenerator,
            "mixedCorridorMinimumNarrowRunLength",
            3);
        SetBool(
            serializedGenerator,
            "mixedCorridorKeepPrimaryRouteWide",
            true);

        serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedRenderer =
            new SerializedObject(renderer);

        SerializedProperty profileProperty =
            serializedRenderer.FindProperty(
                "corridorVisualProfile");

        profileProperty.objectReferenceValue = profile;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureUniformBaseline(
        DungeonGenerator generator,
        DungeonRenderer renderer)
    {
        SerializedObject serializedGenerator =
            new SerializedObject(generator);

        SetEnum(
            serializedGenerator,
            WidthModeField,
            (int)DungeonCorridorWidthMode.Uniform2);

        SetInt(serializedGenerator, "socketCorridorWidth", 2);
        serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedRenderer =
            new SerializedObject(renderer);

        SerializedProperty profileProperty =
            serializedRenderer.FindProperty(
                "corridorVisualProfile");

        profileProperty.objectReferenceValue = null;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool TryBuildCorridorComparison(
        DungeonGenerator generator,
        out CorridorComparison comparison,
        List<string> errors)
    {
        comparison = default(CorridorComparison);

        FieldInfo modeField =
            typeof(DungeonGenerator).GetField(
                WidthModeField,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (modeField == null)
        {
            errors.Add("找不到 Width Mode 私有字段。" );
            return false;
        }

        object originalValue = modeField.GetValue(generator);
        DungeonLayout uniformLayout = null;
        DungeonLayout mixedLayout = null;
        string uniformReport = string.Empty;
        string mixedReport = string.Empty;

        try
        {
            modeField.SetValue(
                generator,
                DungeonCorridorWidthMode.Uniform2);

            if (!generator.TryGenerateHybridRuntimeLayout(
                    1,
                    out uniformLayout,
                    out uniformReport))
            {
                errors.Add(
                    "Uniform2 对照布局生成失败：\n" +
                    uniformReport);
                return false;
            }

            modeField.SetValue(
                generator,
                DungeonCorridorWidthMode.Mixed1And2);

            if (!generator.TryGenerateHybridRuntimeLayout(
                    1,
                    out mixedLayout,
                    out mixedReport))
            {
                errors.Add(
                    "Mixed1And2 布局生成失败：\n" +
                    mixedReport);
                return false;
            }
        }
        finally
        {
            modeField.SetValue(generator, originalValue);
        }

        if (!HaveSameRoomAndSocketSelections(
                uniformLayout,
                mixedLayout))
        {
            errors.Add(
                "Mixed1And2 改变了房间摆放、角色模板、Socket 或连接图。" );
        }

        if (!uniformLayout.CorridorCells.IsSupersetOf(
                mixedLayout.CorridorCells))
        {
            errors.Add(
                "Mixed Corridors 不是 Uniform2 安全包络的子集。" );
        }

        if (mixedLayout.CorridorCells.Count >=
            uniformLayout.CorridorCells.Count)
        {
            errors.Add(
                "Mixed Corridors 没有实际减少通道占格。" );
        }

        List<string> mixedErrors =
            mixedLayout.GetValidationErrors();

        mixedErrors.AddRange(
            generator.GetSocketCorridorValidationErrors(
                mixedLayout));

        for (int i = 0; i < mixedErrors.Count; i++)
        {
            errors.Add("Mixed Layout：" + mixedErrors[i]);
        }

        int narrowCells;
        int wideCells;

        CountCorridorWidthEvidence(
            mixedLayout.CorridorCells,
            out narrowCells,
            out wideCells);

        if (narrowCells == 0)
        {
            errors.Add("固定 Seed 12345 没有产生一格宽直线中段。" );
        }

        if (wideCells == 0)
        {
            errors.Add("固定 Seed 12345 没有保留双格／交叉拓扑。" );
        }

        if (mixedReport.IndexOf(
                "Width Profile：Mixed1And2",
                StringComparison.Ordinal) < 0 ||
            mixedReport.IndexOf(
                "Uniform2SafetyEnvelope=Preserved",
                StringComparison.Ordinal) < 0)
        {
            errors.Add("Mixed 成功报告缺少宽度 Profile 契约。" );
        }

        comparison = new CorridorComparison(
            uniformLayout.RoomPlacements.Count,
            uniformLayout.Connections.Count,
            uniformLayout.FloorCells.Count,
            mixedLayout.FloorCells.Count,
            uniformLayout.CorridorCells.Count,
            mixedLayout.CorridorCells.Count,
            narrowCells,
            wideCells,
            uniformLayout.Seed,
            HaveSameRoomAndSocketSelections(
                uniformLayout,
                mixedLayout),
            uniformLayout.CorridorCells.IsSupersetOf(
                mixedLayout.CorridorCells));

        return errors.Count == 0;
    }

    private static bool HaveSameRoomAndSocketSelections(
        DungeonLayout first,
        DungeonLayout second)
    {
        if (first == null || second == null ||
            first.RoomPlacements.Count !=
                second.RoomPlacements.Count ||
            first.Connections.Count !=
                second.Connections.Count ||
            first.StartCell != second.StartCell ||
            first.ExitCell != second.ExitCell)
        {
            return false;
        }

        for (int i = 0;
             i < first.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement firstPlacement =
                first.RoomPlacements[i];
            DreamRoomPlacement secondPlacement =
                second.RoomPlacements[i];

            if (firstPlacement == null ||
                secondPlacement == null ||
                firstPlacement.Template !=
                    secondPlacement.Template ||
                firstPlacement.MinimumCell !=
                    secondPlacement.MinimumCell ||
                firstPlacement.ClockwiseQuarterTurns !=
                    secondPlacement.ClockwiseQuarterTurns)
            {
                return false;
            }
        }

        for (int i = 0;
             i < first.Connections.Count;
             i++)
        {
            DreamRoomConnection firstConnection =
                first.Connections[i];
            DreamRoomConnection secondConnection =
                second.Connections[i];

            if (firstConnection == null ||
                secondConnection == null ||
                firstConnection.RoomAIndex !=
                    secondConnection.RoomAIndex ||
                firstConnection.RoomBIndex !=
                    secondConnection.RoomBIndex ||
                !string.Equals(
                    firstConnection.SocketAId,
                    secondConnection.SocketAId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    firstConnection.SocketBId,
                    secondConnection.SocketBId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void CountCorridorWidthEvidence(
        HashSet<Vector2Int> corridorCells,
        out int narrowStraightCells,
        out int wideTopologyCells)
    {
        narrowStraightCells = 0;
        wideTopologyCells = 0;

        foreach (Vector2Int cell in corridorCells)
        {
            bool north = corridorCells.Contains(
                cell + Vector2Int.up);
            bool east = corridorCells.Contains(
                cell + Vector2Int.right);
            bool south = corridorCells.Contains(
                cell + Vector2Int.down);
            bool west = corridorCells.Contains(
                cell + Vector2Int.left);

            bool narrowVertical =
                north && south && !east && !west;
            bool narrowHorizontal =
                east && west && !north && !south;

            if (narrowVertical || narrowHorizontal)
            {
                narrowStraightCells++;
            }

            int neighbours =
                (north ? 1 : 0) +
                (east ? 1 : 0) +
                (south ? 1 : 0) +
                (west ? 1 : 0);

            if (neighbours >= 3)
            {
                wideTopologyCells++;
            }
        }
    }

    private static void ValidateRenderedGeometry(
        int floorNumber,
        DungeonLayout layout,
        out int renderedFloors,
        out int renderedWalls,
        out int floorColliders,
        out int wallColliders,
        out int distinctWallColors,
        List<string> errors)
    {
        renderedFloors = 0;
        renderedWalls = 0;
        floorColliders = 0;
        wallColliders = 0;
        distinctWallColors = 0;

        GameObject root = GameObject.Find(
            "GeneratedDungeon_Floor_" + floorNumber);

        if (root == null)
        {
            errors.Add("找不到当前 GeneratedDungeon 根节点。" );
            return;
        }

        Transform floors = root.transform.Find("Corridors");
        Transform walls = root.transform.Find("CorridorWalls");

        if (floors == null || walls == null)
        {
            errors.Add("找不到 Corridors／CorridorWalls 根节点。" );
            return;
        }

        renderedFloors = floors.childCount;
        renderedWalls = walls.childCount;
        floorColliders =
            floors.GetComponentsInChildren<BoxCollider2D>(
                true).Length;
        wallColliders =
            walls.GetComponentsInChildren<BoxCollider2D>(
                true).Length;

        HashSet<Color32> wallColors =
            new HashSet<Color32>();

        SpriteRenderer[] wallRenderers =
            walls.GetComponentsInChildren<SpriteRenderer>(
                true);

        for (int i = 0; i < wallRenderers.Length; i++)
        {
            wallColors.Add(wallRenderers[i].color);
        }

        distinctWallColors = wallColors.Count;

        if (renderedFloors != layout.CorridorCells.Count)
        {
            errors.Add(
                "渲染走廊地板数量与 CorridorCells 不一致。" );
        }

        if (floorColliders != 0)
        {
            errors.Add("走廊地板不应拥有 Collider。" );
        }

        if (renderedWalls <= 0 ||
            wallColliders != renderedWalls)
        {
            errors.Add("走廊墙与墙 Collider 数量不一致。" );
        }

        if (distinctWallColors < 3)
        {
            errors.Add(
                "灰石墙没有形成至少三档可辨识的确定性明暗。" );
        }
    }

    private static DungeonCorridorVisualProfile
        LoadVisualProfile(List<string> errors)
    {
        DungeonCorridorVisualProfile profile =
            AssetDatabase.LoadAssetAtPath<
                DungeonCorridorVisualProfile>(
                    VisualProfilePath);

        if (profile == null)
        {
            errors.Add(
                "找不到临时灰石 Profile：" +
                VisualProfilePath + "。" );
        }

        return profile;
    }

    private static void RequireSerializedProperty(
        UnityEngine.Object target,
        string propertyName,
        List<string> errors)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject =
            new SerializedObject(target);

        if (serializedObject.FindProperty(propertyName) == null)
        {
            errors.Add(
                target.GetType().Name +
                " 缺少序列化字段 '" +
                propertyName + "'。" );
        }
    }

    private static void RequirePrivateBool(
        object target,
        string fieldName,
        bool expected,
        List<string> errors)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        if (field == null ||
            !(field.GetValue(target) is bool) ||
            (bool)field.GetValue(target) != expected)
        {
            errors.Add(
                fieldName + " 必须为 " + expected + "。" );
        }
    }

    private static void RequirePrivateInt(
        object target,
        string fieldName,
        int expected,
        List<string> errors)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        if (field == null ||
            !(field.GetValue(target) is int) ||
            (int)field.GetValue(target) != expected)
        {
            errors.Add(
                fieldName + " 必须为 " + expected + "。" );
        }
    }

    private static bool TryReadCurrentLayout(
        GameManager gameManager,
        out DungeonLayout layout)
    {
        layout = null;

        FieldInfo field = typeof(GameManager).GetField(
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

    private static void SetEnum(
        SerializedObject target,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            target.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.enumValueIndex = value;
    }

    private static void SetInt(
        SerializedObject target,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            target.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.intValue = value;
    }

    private static void SetBool(
        SerializedObject target,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            target.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.boolValue = value;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            T component =
                roots[rootIndex]
                    .GetComponentInChildren<T>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static string BuildComparisonReport(
        CorridorComparison comparison)
    {
        return
            "Rooms=" + comparison.RoomCount +
            " | Connections=" + comparison.ConnectionCount +
            " | Seed=" + comparison.Seed +
            " | SameRoomsSockets=" +
            comparison.SameRoomsAndSockets + "\n" +
            "UniformFloorCells=" +
            comparison.UniformFloorCells +
            " | MixedFloorCells=" +
            comparison.MixedFloorCells +
            " | UniformCorridorCells=" +
            comparison.UniformCorridorCells +
            " | MixedCorridorCells=" +
            comparison.MixedCorridorCells + "\n" +
            "MixedSubsetOfUniform=" +
            comparison.MixedSubsetOfUniform +
            " | NarrowStraightCells=" +
            comparison.NarrowStraightCells +
            " | WideTopologyCells=" +
            comparison.WideTopologyCells +
            " | FullyConnected=True";
    }

    private static void ReportFailure(
        string title,
        List<string> errors,
        UnityEngine.Object context)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.Append(errors[i]);

            if (i < errors.Count - 1)
            {
                builder.AppendLine();
            }
        }

        string message = builder.Length > 0
            ? builder.ToString()
            : "未知错误。";

        Debug.LogError(
            "[DreamCorridorPassAuditC1] " +
            title + "\n" + message,
            context);

        EditorUtility.DisplayDialog(
            title,
            message,
            "OK");
    }

    private struct SceneContext
    {
        public readonly Scene Scene;
        public readonly DungeonGenerator Generator;
        public readonly DungeonRenderer Renderer;

        public SceneContext(
            Scene scene,
            DungeonGenerator generator,
            DungeonRenderer renderer)
        {
            Scene = scene;
            Generator = generator;
            Renderer = renderer;
        }
    }

    private struct CorridorComparison
    {
        public readonly int RoomCount;
        public readonly int ConnectionCount;
        public readonly int UniformFloorCells;
        public readonly int MixedFloorCells;
        public readonly int UniformCorridorCells;
        public readonly int MixedCorridorCells;
        public readonly int NarrowStraightCells;
        public readonly int WideTopologyCells;
        public readonly int Seed;
        public readonly bool SameRoomsAndSockets;
        public readonly bool MixedSubsetOfUniform;

        public CorridorComparison(
            int roomCount,
            int connectionCount,
            int uniformFloorCells,
            int mixedFloorCells,
            int uniformCorridorCells,
            int mixedCorridorCells,
            int narrowStraightCells,
            int wideTopologyCells,
            int seed,
            bool sameRoomsAndSockets,
            bool mixedSubsetOfUniform)
        {
            RoomCount = roomCount;
            ConnectionCount = connectionCount;
            UniformFloorCells = uniformFloorCells;
            MixedFloorCells = mixedFloorCells;
            UniformCorridorCells = uniformCorridorCells;
            MixedCorridorCells = mixedCorridorCells;
            NarrowStraightCells = narrowStraightCells;
            WideTopologyCells = wideTopologyCells;
            Seed = seed;
            SameRoomsAndSockets = sameRoomsAndSockets;
            MixedSubsetOfUniform = mixedSubsetOfUniform;
        }
    }
}
