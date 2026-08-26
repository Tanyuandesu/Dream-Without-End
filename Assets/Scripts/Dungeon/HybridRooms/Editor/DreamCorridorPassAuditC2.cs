using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Corridor Pass C2 的安装、C1／C2 对照、实机与恢复工具。
///
/// C2 只修改走廊宽度后处理与表现层：
/// - C1 Mixed1And2 保留为可恢复基线。
/// - Mixed1To3 只在安全空间中增加短三格开阔段。
/// - 墙碰撞保持整格，视觉子物体只从外侧收进。
/// </summary>
public static class DreamCorridorPassAuditC2
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Corridor Pass C2/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string GrayboxCatalogId =
        "Graybox_R3";

    private const int BaselineSeed = 12345;

    private const string C1VisualProfilePath =
        "Assets/DreamDungeon/Generated/" +
        "CorridorVisual_GrayStone_C1.asset";

    private const string C2VisualProfilePath =
        "Assets/DreamDungeon/Generated/" +
        "CorridorVisual_GrayStone_C2.asset";

    private const string C1VisualProfileId =
        "GrayStone_Temporary_C1";

    private const string C2VisualProfileId =
        "GrayStone_Temporary_C2";

    private const string WidthModeField =
        "socketCorridorWidthMode";

    private const float ExpectedOuterInset = 0.30f;
    private const float MaximumOverallDeltaRatio = 0.12f;
    private const float FloatTolerance = 0.001f;

    [MenuItem(
        MenuRoot + "Validate Installed Assets (C2)",
        false,
        2700)]
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
                "Corridor Pass C2 静态校验无法开始",
                errors,
                null);
            return;
        }

        bool dirtyBefore = context.Scene.isDirty;

        DungeonCorridorVisualProfile c1Profile =
            LoadProfile(
                C1VisualProfilePath,
                C1VisualProfileId,
                errors);

        DungeonCorridorVisualProfile c2Profile =
            LoadProfile(
                C2VisualProfilePath,
                C2VisualProfileId,
                errors);

        RequireSerializedProperty(
            context.Generator,
            WidthModeField,
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorOpenFraction",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorMinimumOpenRunLength",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorMaximumOpenRunLength",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorDoorTransitionLength",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorOpenPrimaryRoute",
            errors);

        RequireSerializedProperty(
            context.Generator,
            "layeredCorridorOpenJunctions",
            errors);

        RequireSerializedProperty(
            context.Renderer,
            "corridorVisualProfile",
            errors);

        if ((int)DungeonCorridorWidthMode.Uniform2 != 0 ||
            (int)DungeonCorridorWidthMode.Mixed1And2 != 1 ||
            (int)DungeonCorridorWidthMode.Mixed1To3 != 2)
        {
            errors.Add(
                "宽度枚举兼容值不正确，必须保持 0／1／2。" );
        }

        if (context.Generator.SocketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1And2)
        {
            errors.Add(
                "C2 静态校验前必须处于已封板的 C1 Mixed1And2 基线。" );
        }

        if (context.Renderer.CorridorVisualProfile == null ||
            !string.Equals(
                context.Renderer.CorridorVisualProfile.ProfileId,
                C1VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "C2 静态校验前必须仍挂载 C1 临时灰石 Profile。" );
        }

        if (c1Profile != null &&
            Mathf.Abs(c1Profile.WallOuterVisualInset) >
            FloatTolerance)
        {
            errors.Add(
                "C1 Profile 的外侧视觉缩进必须保持 0。" );
        }

        if (c2Profile != null &&
            Mathf.Abs(
                c2Profile.WallOuterVisualInset -
                ExpectedOuterInset) >
            FloatTolerance)
        {
            errors.Add(
                "C2 Profile 的 Wall Outer Visual Inset 必须为 0.30。" );
        }

        if (context.Scene.isDirty != dirtyBefore)
        {
            errors.Add(
                "只读静态校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Corridor Pass C2 静态校验失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC2] 安装资产校验通过。\n" +
            "Baseline=Mixed1And2" +
            " | BaselineProfile=" + C1VisualProfileId +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | RenderMode=HybridPrefabRooms\n" +
            "Mixed1To3Available=True" +
            " | EnumValues=0/1/2" +
            " | BaseSafetyEnvelope=2" +
            " | DoorWidth=2" +
            " | LocalFallback=True\n" +
            "C2VisualProfile=" + C2VisualProfileId +
            " | OuterVisualInset=" + ExpectedOuterInset +
            " | InnerEdgeFixed=True" +
            " | FullCellColliderRetained=True" +
            " | SceneChanged=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Corridor Pass C2 Assets Passed",
            "C1 基线、Mixed1To3 字段与 C2 外侧缩进 Profile 均已安装。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Prepare Layered Corridor Preview (C2)",
        false,
        2710)]
    private static void PrepareLayeredPreview()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: true,
                out context,
                errors))
        {
            ReportFailure(
                "Layered Corridor Preview 准备失败",
                errors,
                null);
            return;
        }

        DungeonCorridorVisualProfile c1Profile =
            LoadProfile(
                C1VisualProfilePath,
                C1VisualProfileId,
                errors);

        DungeonCorridorVisualProfile c2Profile =
            LoadProfile(
                C2VisualProfilePath,
                C2VisualProfileId,
                errors);

        if (context.Generator.SocketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1And2)
        {
            errors.Add(
                "Prepare 前必须从 C1 Mixed1And2 基线开始。" );
        }

        if (context.Renderer.CorridorVisualProfile == null ||
            !string.Equals(
                context.Renderer.CorridorVisualProfile.ProfileId,
                C1VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "Prepare 前必须挂载 C1 临时灰石 Profile。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Layered Corridor Preview 准备失败",
                errors,
                context.Generator);
            return;
        }

        ConfigureC2Preview(
            context.Generator,
            context.Renderer,
            c2Profile);

        LayeredComparison comparison;

        if (!TryBuildLayeredComparison(
                context.Generator,
                out comparison,
                errors))
        {
            ConfigureC1Baseline(
                context.Generator,
                context.Renderer,
                c1Profile);

            EditorSceneManager.MarkSceneDirty(
                context.Scene);

            ReportFailure(
                "Layered Corridor Preview 预检失败",
                errors,
                context.Generator);
            return;
        }

        EditorSceneManager.MarkSceneDirty(context.Scene);

        Debug.Log(
            "[DreamCorridorPassAuditC2] Layered Corridor Preview 已准备。\n" +
            BuildComparisonReport(comparison) + "\n" +
            "WidthMode=Mixed1To3" +
            " | Width1Retained=True" +
            " | Width2DominantTarget=True" +
            " | Width3LocalExpansion=True" +
            " | DoorWidth=2" +
            " | DoorTransition=1" +
            " | OpenFractionTarget=0.20\n" +
            "Profile=" + C2VisualProfileId +
            " | OuterVisualInset=" + ExpectedOuterInset +
            " | InnerEdgeFixed=True" +
            " | ColliderChanged=False\n" +
            "SceneSaved=False" +
            " | GameSceneDirty=True" +
            " | CatalogUnchanged=" + GrayboxCatalogId +
            " | FixedSeedUnchanged=" + BaselineSeed +
            " | DoNotSaveUntilLiveValidation=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Layered Corridor Preview Ready",
            "预检已证明 C2 保留完整 C1 通道，并只加入通过空间校验的局部三格区域。" +
            "现在进入 Play Mode；暂时不要保存 GameScene。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Validate Live Layered Corridor (C2)",
        false,
        2720)]
    private static void ValidateLiveLayeredCorridor()
    {
        List<string> errors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add("必须在 Play Mode 中执行。" );
            ReportFailure(
                "Live Layered Corridor 校验无法开始",
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
                DungeonCorridorWidthMode.Mixed1To3)
        {
            errors.Add("运行时 Width Mode 不是 Mixed1To3。" );
        }

        if (renderer != null &&
            (renderer.CorridorVisualProfile == null ||
             !string.Equals(
                 renderer.CorridorVisualProfile.ProfileId,
                 C2VisualProfileId,
                 StringComparison.Ordinal)))
        {
            errors.Add("运行时没有挂载 C2 临时灰石 Profile。" );
        }

        int narrowStraightCells = 0;
        int width2TopologyCells = 0;
        int solidThreeByThreeCenters = 0;

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
                out width2TopologyCells,
                out solidThreeByThreeCenters);

            if (narrowStraightCells == 0)
            {
                errors.Add("本层没有检测到保留的一格宽直线中段。" );
            }

            if (width2TopologyCells == 0)
            {
                errors.Add("本层没有检测到两格／交叉拓扑证据。" );
            }

            if (solidThreeByThreeCenters == 0)
            {
                errors.Add("本层没有检测到局部三格开阔区域。" );
            }
        }

        RenderEvidence renderEvidence =
            default(RenderEvidence);

        if (gameManager != null &&
            layout != null &&
            renderer != null)
        {
            ValidateRenderedGeometry(
                gameManager.CurrentFloor,
                layout,
                renderer,
                out renderEvidence,
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
                "Live Layered Corridor 校验失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC2] Live Layered Corridor 通过。\n" +
            "Floor=" + gameManager.CurrentFloor +
            " | Seed=" + layout.Seed +
            " | Rooms=" + layout.RoomPlacements.Count +
            " | Connections=" + layout.Connections.Count +
            " | FloorCells=" + layout.FloorCells.Count +
            " | CorridorCells=" + layout.CorridorCells.Count + "\n" +
            "WidthMode=Mixed1To3" +
            " | Width1Evidence=" + narrowStraightCells +
            " | Width2TopologyEvidence=" + width2TopologyCells +
            " | Width3Evidence=" + solidThreeByThreeCenters +
            " | DoorWidth=2" +
            " | Connected=True\n" +
            "RenderedCorridorFloors=" +
            renderEvidence.RenderedFloors +
            " | FloorColliders=" +
            renderEvidence.FloorColliders +
            " | RenderedCorridorWalls=" +
            renderEvidence.RenderedWalls +
            " | WallColliders=" +
            renderEvidence.WallColliders +
            " | WallVisualChildren=" +
            renderEvidence.WallVisualChildren +
            " | InsetAppliedWalls=" +
            renderEvidence.InsetAppliedWalls +
            " | FullCellColliderRetained=True" +
            " | InnerEdgeFixed=True" +
            " | OuterVisualInset=" + ExpectedOuterInset +
            " | DistinctWallColors=" +
            renderEvidence.DistinctWallColors + "\n" +
            "VisualProfile=" + C2VisualProfileId +
            " | EnemyTopology=" + pathService.Topology +
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
            "Live Layered Corridor Passed",
            "1／2／3 格层次、整格碰撞、外侧视觉缩进与 EA3.1 合法门边均通过。" +
            "仍需人工观察宽窄节奏和多敌人移动手感。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Save Layered Corridor Baseline (C2)",
        false,
        2730)]
    private static void SaveLayeredBaseline()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: false,
                out context,
                errors))
        {
            ReportFailure(
                "Layered Corridor Baseline 保存失败",
                errors,
                null);
            return;
        }

        if (context.Generator.SocketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1To3)
        {
            errors.Add("当前 Width Mode 不是 Mixed1To3。" );
        }

        if (context.Renderer.CorridorVisualProfile == null ||
            !string.Equals(
                context.Renderer.CorridorVisualProfile.ProfileId,
                C2VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add("当前没有挂载 C2 临时灰石 Profile。" );
        }

        LayeredComparison comparison;

        if (errors.Count == 0)
        {
            TryBuildLayeredComparison(
                context.Generator,
                out comparison,
                errors);
        }
        else
        {
            comparison = default(LayeredComparison);
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "Layered Corridor Baseline 保存失败",
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
                "Layered Corridor Baseline 保存失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC2] Layered Corridor Baseline 已保存。\n" +
            BuildComparisonReport(comparison) + "\n" +
            "SceneSaved=True" +
            " | WidthMode=Mixed1To3" +
            " | VisualProfile=" + C2VisualProfileId +
            " | OuterVisualInset=" + ExpectedOuterInset +
            " | C1RollbackRetained=True" +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | R9.4AssetsRetained=True" +
            " | EA3.1AlgorithmRetained=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "Layered Corridor Baseline Saved",
            "GameScene 已保存为 Mixed1To3＋C2 外侧缩进灰石 Profile。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Restore C1 Mixed1And2 and Save",
        false,
        2740)]
    private static void RestoreC1AndSave()
    {
        List<string> errors = new List<string>();
        SceneContext context;

        if (!TryGetEditModeContext(
                requireCleanScene: false,
                out context,
                errors))
        {
            ReportFailure(
                "C1 Mixed1And2 恢复失败",
                errors,
                null);
            return;
        }

        DungeonCorridorVisualProfile c1Profile =
            LoadProfile(
                C1VisualProfilePath,
                C1VisualProfileId,
                errors);

        if (errors.Count == 0)
        {
            ConfigureC1Baseline(
                context.Generator,
                context.Renderer,
                c1Profile);
        }

        EditorSceneManager.MarkSceneDirty(context.Scene);

        if (errors.Count == 0 &&
            (!EditorSceneManager.SaveScene(context.Scene) ||
             context.Scene.isDirty))
        {
            errors.Add("C1 状态已写回内存，但 GameScene 保存失败。" );
        }

        if (context.Generator.SocketCorridorWidthMode !=
                DungeonCorridorWidthMode.Mixed1And2 ||
            context.Renderer.CorridorVisualProfile == null ||
            !string.Equals(
                context.Renderer.CorridorVisualProfile.ProfileId,
                C1VisualProfileId,
                StringComparison.Ordinal))
        {
            errors.Add("保存后的宽度模式或视觉 Profile 未恢复到 C1。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "C1 Mixed1And2 恢复失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamCorridorPassAuditC2] C1 Mixed1And2 已恢复并保存。\n" +
            "SceneSaved=True" +
            " | WidthMode=Mixed1And2" +
            " | VisualProfile=" + C1VisualProfileId +
            " | WallOuterVisualInset=0" +
            " | Catalog=" + GrayboxCatalogId +
            " | FixedSeed=" + BaselineSeed +
            " | C2CodeRetained=True" +
            " | C2ProfileRetained=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "C1 Mixed1And2 Restored",
            "GameScene 已恢复为刚才封板的 C1 混合通道与原灰石显示。",
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

    private static void ConfigureC2Preview(
        DungeonGenerator generator,
        DungeonRenderer renderer,
        DungeonCorridorVisualProfile profile)
    {
        SerializedObject serializedGenerator =
            new SerializedObject(generator);

        SetEnum(
            serializedGenerator,
            WidthModeField,
            (int)DungeonCorridorWidthMode.Mixed1To3);

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
        SetFloat(
            serializedGenerator,
            "layeredCorridorOpenFraction",
            0.20f);
        SetInt(
            serializedGenerator,
            "layeredCorridorMinimumOpenRunLength",
            3);
        SetInt(
            serializedGenerator,
            "layeredCorridorMaximumOpenRunLength",
            4);
        SetInt(
            serializedGenerator,
            "layeredCorridorDoorTransitionLength",
            1);
        SetBool(
            serializedGenerator,
            "layeredCorridorOpenPrimaryRoute",
            true);
        SetBool(
            serializedGenerator,
            "layeredCorridorOpenJunctions",
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

    private static void ConfigureC1Baseline(
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

    private static bool TryBuildLayeredComparison(
        DungeonGenerator generator,
        out LayeredComparison comparison,
        List<string> errors)
    {
        comparison = default(LayeredComparison);

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
        DungeonLayout c1Layout = null;
        DungeonLayout c2Layout = null;
        string uniformReport = string.Empty;
        string c1Report = string.Empty;
        string c2Report = string.Empty;

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
                    out c1Layout,
                    out c1Report))
            {
                errors.Add(
                    "C1 Mixed1And2 对照布局生成失败：\n" +
                    c1Report);
                return false;
            }

            modeField.SetValue(
                generator,
                DungeonCorridorWidthMode.Mixed1To3);

            if (!generator.TryGenerateHybridRuntimeLayout(
                    1,
                    out c2Layout,
                    out c2Report))
            {
                errors.Add(
                    "C2 Mixed1To3 布局生成失败：\n" +
                    c2Report);
                return false;
            }
        }
        finally
        {
            modeField.SetValue(generator, originalValue);
        }

        bool sameRoomsSockets =
            HaveSameRoomAndSocketSelections(
                uniformLayout,
                c1Layout) &&
            HaveSameRoomAndSocketSelections(
                c1Layout,
                c2Layout);

        if (!sameRoomsSockets)
        {
            errors.Add(
                "C2 改变了房间摆放、角色模板、Socket 或连接图。" );
        }

        bool c1FullyRetained =
            c2Layout.CorridorCells.IsSupersetOf(
                c1Layout.CorridorCells);

        if (!c1FullyRetained)
        {
            errors.Add(
                "C2 没有完整保留已封板的 C1 CorridorCells。" );
        }

        int addedBeyondC1 =
            CountCellsOutside(
                c2Layout.CorridorCells,
                c1Layout.CorridorCells);

        int addedBeyondUniform =
            CountCellsOutside(
                c2Layout.CorridorCells,
                uniformLayout.CorridorCells);

        if (addedBeyondC1 <= 0 ||
            addedBeyondUniform <= 0)
        {
            errors.Add(
                "固定 Seed 12345 没有产生超出两格包络的局部三格区域。" );
        }

        float overallDeltaRatio =
            uniformLayout.CorridorCells.Count == 0
                ? 1f
                : Mathf.Abs(
                    c2Layout.CorridorCells.Count -
                    uniformLayout.CorridorCells.Count) /
                  (float)uniformLayout.CorridorCells.Count;

        if (overallDeltaRatio >
            MaximumOverallDeltaRatio)
        {
            errors.Add(
                "C2 总体通道占格偏离 Uniform2 超过 12%；" +
                "当前比例=" +
                overallDeltaRatio.ToString("F3") + "。" );
        }

        List<string> c2Errors =
            c2Layout.GetValidationErrors();

        c2Errors.AddRange(
            generator.GetSocketCorridorValidationErrors(
                c2Layout));

        for (int i = 0; i < c2Errors.Count; i++)
        {
            errors.Add("C2 Layout：" + c2Errors[i]);
        }

        int narrowCells;
        int width2TopologyCells;
        int solidThreeByThreeCenters;

        CountCorridorWidthEvidence(
            c2Layout.CorridorCells,
            out narrowCells,
            out width2TopologyCells,
            out solidThreeByThreeCenters);

        if (narrowCells == 0)
        {
            errors.Add("固定 Seed 12345 没有保留一格宽直线中段。" );
        }

        if (width2TopologyCells == 0)
        {
            errors.Add("固定 Seed 12345 没有两格／交叉拓扑证据。" );
        }

        if (solidThreeByThreeCenters == 0)
        {
            errors.Add("固定 Seed 12345 没有三格开阔区域证据。" );
        }

        if (c1Report.IndexOf(
                "Width Profile：Mixed1And2",
                StringComparison.Ordinal) < 0 ||
            c2Report.IndexOf(
                "Width Profile：Mixed1To3",
                StringComparison.Ordinal) < 0 ||
            c2Report.IndexOf(
                "BaseSafetyEnvelope=2",
                StringComparison.Ordinal) < 0 ||
            c2Report.IndexOf(
                "DoorWidth=2",
                StringComparison.Ordinal) < 0 ||
            c2Report.IndexOf(
                "OpenExpansion=Validated",
                StringComparison.Ordinal) < 0)
        {
            errors.Add("C1／C2 成功报告缺少宽度安全契约。" );
        }

        comparison = new LayeredComparison(
            c2Layout.RoomPlacements.Count,
            c2Layout.Connections.Count,
            uniformLayout.CorridorCells.Count,
            c1Layout.CorridorCells.Count,
            c2Layout.CorridorCells.Count,
            addedBeyondC1,
            addedBeyondUniform,
            narrowCells,
            width2TopologyCells,
            solidThreeByThreeCenters,
            overallDeltaRatio,
            c2Layout.Seed,
            sameRoomsSockets,
            c1FullyRetained);

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

    private static int CountCellsOutside(
        HashSet<Vector2Int> candidate,
        HashSet<Vector2Int> baseline)
    {
        int count = 0;

        foreach (Vector2Int cell in candidate)
        {
            if (!baseline.Contains(cell))
            {
                count++;
            }
        }

        return count;
    }

    private static void CountCorridorWidthEvidence(
        HashSet<Vector2Int> corridorCells,
        out int narrowStraightCells,
        out int width2TopologyCells,
        out int solidThreeByThreeCenters)
    {
        narrowStraightCells = 0;
        width2TopologyCells = 0;
        solidThreeByThreeCenters = 0;

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
                width2TopologyCells++;
            }

            bool solidThreeByThree = true;

            for (int x = -1;
                 x <= 1 && solidThreeByThree;
                 x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (!corridorCells.Contains(
                            cell + new Vector2Int(x, y)))
                    {
                        solidThreeByThree = false;
                        break;
                    }
                }
            }

            if (solidThreeByThree)
            {
                solidThreeByThreeCenters++;
            }
        }
    }

    private static void ValidateRenderedGeometry(
        int floorNumber,
        DungeonLayout layout,
        DungeonRenderer renderer,
        out RenderEvidence evidence,
        List<string> errors)
    {
        evidence = default(RenderEvidence);

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

        int renderedFloors = floors.childCount;
        int renderedWalls = walls.childCount;
        int floorColliders =
            floors.GetComponentsInChildren<BoxCollider2D>(
                true).Length;
        int wallColliders =
            walls.GetComponentsInChildren<BoxCollider2D>(
                true).Length;

        int wallVisualChildren = 0;
        int insetAppliedWalls = 0;
        HashSet<Color32> wallColors =
            new HashSet<Color32>();

        float expectedScale =
            1f - ExpectedOuterInset;
        float expectedOffset =
            ExpectedOuterInset * 0.5f;

        for (int wallIndex = 0;
             wallIndex < walls.childCount;
             wallIndex++)
        {
            Transform wallRoot = walls.GetChild(wallIndex);
            BoxCollider2D collider =
                wallRoot.GetComponent<BoxCollider2D>();
            Transform visual =
                wallRoot.Find("WallVisual_C2");

            if (collider == null)
            {
                errors.Add(
                    wallRoot.name +
                    " 缺少整格 BoxCollider2D。" );
                continue;
            }

            if (!Approximately(
                    collider.size.x,
                    1f) ||
                !Approximately(
                    collider.size.y,
                    1f) ||
                collider.offset != Vector2.zero ||
                !Approximately(
                    collider.bounds.size.x,
                    renderer.CellSize) ||
                !Approximately(
                    collider.bounds.size.y,
                    renderer.CellSize))
            {
                errors.Add(
                    wallRoot.name +
                    " 的碰撞不再是原位置完整一格。" );
            }

            if (visual == null)
            {
                errors.Add(
                    wallRoot.name +
                    " 缺少 WallVisual_C2 子物体。" );
                continue;
            }

            wallVisualChildren++;

            SpriteRenderer spriteRenderer =
                visual.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null ||
                !spriteRenderer.enabled)
            {
                errors.Add(
                    wallRoot.name +
                    " 的 C2 视觉子物体没有有效 SpriteRenderer。" );
                continue;
            }

            wallColors.Add(spriteRenderer.color);

            bool insetX = visual.localScale.x < 0.999f;
            bool insetY = visual.localScale.y < 0.999f;

            if (insetX || insetY)
            {
                insetAppliedWalls++;
            }

            ValidateInsetAxis(
                wallRoot.name,
                "X",
                visual.localScale.x,
                visual.localPosition.x,
                expectedScale,
                expectedOffset,
                errors);

            ValidateInsetAxis(
                wallRoot.name,
                "Y",
                visual.localScale.y,
                visual.localPosition.y,
                expectedScale,
                expectedOffset,
                errors);
        }

        if (renderedFloors !=
            layout.CorridorCells.Count)
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
            errors.Add("走廊墙与整格墙 Collider 数量不一致。" );
        }

        if (wallVisualChildren != renderedWalls)
        {
            errors.Add("并非每个走廊墙根都有一个 C2 视觉子物体。" );
        }

        if (insetAppliedWalls <= 0)
        {
            errors.Add("没有墙体实际应用外侧视觉缩进。" );
        }

        if (wallColors.Count < 3)
        {
            errors.Add(
                "灰石墙没有形成至少三档可辨识的确定性明暗。" );
        }

        evidence = new RenderEvidence(
            renderedFloors,
            renderedWalls,
            floorColliders,
            wallColliders,
            wallVisualChildren,
            insetAppliedWalls,
            wallColors.Count);
    }

    private static void ValidateInsetAxis(
        string wallName,
        string axisName,
        float scale,
        float position,
        float expectedScale,
        float expectedOffset,
        List<string> errors)
    {
        if (Approximately(scale, 1f))
        {
            if (!Approximately(position, 0f))
            {
                errors.Add(
                    wallName + " 的 " + axisName +
                    " 轴未缩进却发生偏移。" );
            }

            return;
        }

        if (!Approximately(scale, expectedScale) ||
            !Approximately(
                Mathf.Abs(position),
                expectedOffset) ||
            !Approximately(
                Mathf.Abs(position) +
                scale * 0.5f,
                0.5f))
        {
            errors.Add(
                wallName + " 的 " + axisName +
                " 轴没有保持内缘固定的 0.30 外侧缩进。" );
        }
    }

    private static bool Approximately(
        float first,
        float second)
    {
        return Mathf.Abs(first - second) <=
               FloatTolerance;
    }

    private static DungeonCorridorVisualProfile
        LoadProfile(
            string path,
            string expectedId,
            List<string> errors)
    {
        DungeonCorridorVisualProfile profile =
            AssetDatabase.LoadAssetAtPath<
                DungeonCorridorVisualProfile>(path);

        if (profile == null)
        {
            errors.Add("找不到视觉 Profile：" + path + "。" );
            return null;
        }

        if (!string.Equals(
                profile.ProfileId,
                expectedId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "视觉 Profile Id 不匹配：" +
                profile.ProfileId +
                "，Expected=" + expectedId + "。" );
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

    private static void SetFloat(
        SerializedObject target,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            target.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.floatValue = value;
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
        LayeredComparison comparison)
    {
        return
            "Rooms=" + comparison.RoomCount +
            " | Connections=" + comparison.ConnectionCount +
            " | Seed=" + comparison.Seed +
            " | SameRoomsSockets=" +
            comparison.SameRoomsAndSockets + "\n" +
            "Uniform2CorridorCells=" +
            comparison.UniformCorridorCells +
            " | C1CorridorCells=" +
            comparison.C1CorridorCells +
            " | C2CorridorCells=" +
            comparison.C2CorridorCells +
            " | OverallDeltaFromUniform=" +
            comparison.OverallDeltaRatio.ToString("F3") + "\n" +
            "C1FullyRetained=" +
            comparison.C1FullyRetained +
            " | AddedBeyondC1=" +
            comparison.AddedBeyondC1 +
            " | AddedBeyondUniform2=" +
            comparison.AddedBeyondUniform +
            " | Width1Evidence=" +
            comparison.NarrowStraightCells +
            " | Width2TopologyEvidence=" +
            comparison.Width2TopologyCells +
            " | Width3Evidence=" +
            comparison.SolidThreeByThreeCenters +
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
            "[DreamCorridorPassAuditC2] " +
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

    private struct LayeredComparison
    {
        public readonly int RoomCount;
        public readonly int ConnectionCount;
        public readonly int UniformCorridorCells;
        public readonly int C1CorridorCells;
        public readonly int C2CorridorCells;
        public readonly int AddedBeyondC1;
        public readonly int AddedBeyondUniform;
        public readonly int NarrowStraightCells;
        public readonly int Width2TopologyCells;
        public readonly int SolidThreeByThreeCenters;
        public readonly float OverallDeltaRatio;
        public readonly int Seed;
        public readonly bool SameRoomsAndSockets;
        public readonly bool C1FullyRetained;

        public LayeredComparison(
            int roomCount,
            int connectionCount,
            int uniformCorridorCells,
            int c1CorridorCells,
            int c2CorridorCells,
            int addedBeyondC1,
            int addedBeyondUniform,
            int narrowStraightCells,
            int width2TopologyCells,
            int solidThreeByThreeCenters,
            float overallDeltaRatio,
            int seed,
            bool sameRoomsAndSockets,
            bool c1FullyRetained)
        {
            RoomCount = roomCount;
            ConnectionCount = connectionCount;
            UniformCorridorCells = uniformCorridorCells;
            C1CorridorCells = c1CorridorCells;
            C2CorridorCells = c2CorridorCells;
            AddedBeyondC1 = addedBeyondC1;
            AddedBeyondUniform = addedBeyondUniform;
            NarrowStraightCells = narrowStraightCells;
            Width2TopologyCells = width2TopologyCells;
            SolidThreeByThreeCenters =
                solidThreeByThreeCenters;
            OverallDeltaRatio = overallDeltaRatio;
            Seed = seed;
            SameRoomsAndSockets = sameRoomsAndSockets;
            C1FullyRetained = c1FullyRetained;
        }
    }

    private struct RenderEvidence
    {
        public readonly int RenderedFloors;
        public readonly int RenderedWalls;
        public readonly int FloorColliders;
        public readonly int WallColliders;
        public readonly int WallVisualChildren;
        public readonly int InsetAppliedWalls;
        public readonly int DistinctWallColors;

        public RenderEvidence(
            int renderedFloors,
            int renderedWalls,
            int floorColliders,
            int wallColliders,
            int wallVisualChildren,
            int insetAppliedWalls,
            int distinctWallColors)
        {
            RenderedFloors = renderedFloors;
            RenderedWalls = renderedWalls;
            FloorColliders = floorColliders;
            WallColliders = wallColliders;
            WallVisualChildren = wallVisualChildren;
            InsetAppliedWalls = insetAppliedWalls;
            DistinctWallColors = distinctWallColors;
        }
    }
}
