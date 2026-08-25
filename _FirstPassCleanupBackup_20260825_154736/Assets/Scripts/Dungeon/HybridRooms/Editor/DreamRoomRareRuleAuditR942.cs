using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.4.2 Rare 权重、每 Template 单层上限与普通槽位标签边界的
/// 静态资产、纯数据和 Live 验收工具。
///
/// 所有 Prefab／Catalog 均随包静态提供。本工具不会创建、复制或保存
/// Prefab；只有 Prepare／Restore 会有意改变 GameScene 的 Catalog
/// 与 Fixed Seed。
/// </summary>
public static class DreamRoomRareRuleAuditR942
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string GrayboxRoot =
        "Assets/DreamDungeon/Generated/R3_Graybox";

    private const string GrayboxCatalogPath =
        GrayboxRoot + "/Catalog/RoomCatalog_Graybox.asset";

    private const string GrayboxCatalogId =
        "Graybox_R3";

    private const string R941Root =
        "Assets/DreamDungeon/Generated/R9_4_1_RoleTags_Clean";

    private const string TestRoot =
        "Assets/DreamDungeon/Generated/R9_4_2_Rare";

    private const string RoomRoot =
        TestRoot + "/Rooms";

    private const string CatalogRoot =
        TestRoot + "/Catalog";

    private const string RuntimeCatalogPath =
        CatalogRoot + "/RoomCatalog_R942_Runtime.asset";

    private const string RuntimeCatalogId =
        "RareRules_R942_Runtime";

    private const string WeightCatalogPath =
        CatalogRoot + "/RoomCatalog_R942_WeightProbe.asset";

    private const string WeightCatalogId =
        "RareRules_R942_WeightProbe";

    private const string StartTemplateId =
        "R942_Start";

    private const string ExitTemplateId =
        "R942_Exit";

    private const string CommonTemplateId =
        "R942_Common";

    private const string RareTemplateId =
        "R942_Rare";

    private const string CoreDeferredTemplateId =
        "R942_CoreDeferred";

    private const string SpecialDeferredTemplateId =
        "R942_SpecialDeferred";

    private const int TestFloor = 1;
    private const int ExpectedRoomCount = 7;
    private const int BaselineSeed = 12345;
    private const int WeightSampleSeed = 94202;
    private const int WeightSampleCount = 512;
    private const int BatchSeedStart = 94200;
    private const int BatchSeedCount = 64;
    private const int LiveSeedSearchCount = 256;

    private static readonly TemplateSpec[] RuntimeTemplateSpecs =
    {
        new TemplateSpec(
            RoomRoot + "/Room_R942_Start.prefab",
            StartTemplateId,
            DreamRoomTag.Standard |
            DreamRoomTag.StartCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1),

        new TemplateSpec(
            RoomRoot + "/Room_R942_Exit.prefab",
            ExitTemplateId,
            DreamRoomTag.Standard |
            DreamRoomTag.ExitCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1),

        new TemplateSpec(
            RoomRoot + "/Room_R942_Common.prefab",
            CommonTemplateId,
            DreamRoomTag.Standard,
            randomWeight: 4,
            maximumInstancesPerFloor: 0),

        new TemplateSpec(
            RoomRoot + "/Room_R942_Rare.prefab",
            RareTemplateId,
            DreamRoomTag.Rare,
            randomWeight: 1,
            maximumInstancesPerFloor: 1),

        new TemplateSpec(
            RoomRoot + "/Room_R942_CoreDeferred.prefab",
            CoreDeferredTemplateId,
            DreamRoomTag.CoreItemCandidate,
            randomWeight: 1000,
            maximumInstancesPerFloor: 0),

        new TemplateSpec(
            RoomRoot + "/Room_R942_SpecialDeferred.prefab",
            SpecialDeferredTemplateId,
            DreamRoomTag.Special,
            randomWeight: 1000,
            maximumInstancesPerFloor: 0)
    };

    private static readonly string[] WeightTemplateIds =
    {
        CommonTemplateId,
        RareTemplateId
    };

    [MenuItem(
        MenuRoot +
        "Validate Installed Rare Test Assets (R9.4.2)",
        false,
        2470)]
    private static void ValidateInstalledAssets()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.2 静态资产校验无法开始",
                errors,
                null);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore =
            context.Scene.isDirty;

        DreamRoomCatalog runtimeCatalog =
            LoadCatalog(
                RuntimeCatalogPath,
                RuntimeCatalogId,
                "Runtime Catalog",
                errors);

        DreamRoomCatalog weightCatalog =
            LoadCatalog(
                WeightCatalogPath,
                WeightCatalogId,
                "Weight Probe Catalog",
                errors);

        int ownedSockets = 0;

        if (runtimeCatalog != null)
        {
            AppendStaticCatalogErrors(
                runtimeCatalog,
                RuntimeTemplateSpecs,
                errors,
                out ownedSockets);
        }

        if (weightCatalog != null)
        {
            AppendCatalogReferenceErrors(
                weightCatalog,
                WeightTemplateIds,
                "Weight Probe Catalog",
                errors);
        }

        string protectedHashAfter =
            BuildProtectedBaselineHashSignature();

        bool protectedHashUnchanged =
            string.Equals(
                protectedHashBefore,
                protectedHashAfter,
                StringComparison.Ordinal);

        bool sceneChanged =
            context.Scene.isDirty != sceneDirtyBefore;

        if (!protectedHashUnchanged)
        {
            errors.Add(
                "只读校验改变了 GameScene／Graybox／R9.4.1 资产哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 静态资产校验失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRareRuleAuditR942] " +
            "R9.4.2 静态测试资产校验通过。\n" +
            "Source=PackageStaticAssets" +
            " | PrefabCreateCalls=0" +
            " | PrefabSaveCalls=0\n" +
            "HierarchyAuthority=LoadPrefabContents" +
            " | AssetViewHierarchyTraversal=False\n" +
            "RuntimeCatalog=" + RuntimeCatalogId +
            " | Templates=6" +
            " | SocketOwners=" + ownedSockets + "/24\n" +
            "WeightCatalog=" + WeightCatalogId +
            " | Templates=2" +
            " | Weights=Common:4,Rare:1\n" +
            "FutureTagDecoys=CoreItemCandidate:1000," +
            "Special:1000\n" +
            "GameSceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | AssetsModified=False",
            runtimeCatalog);

        EditorUtility.DisplayDialog(
            "R9.4.2 Assets Passed",
            "六个静态 Prefab 与两个 Catalog 已通过。\n\n" +
            "没有创建、复制或保存任何 Prefab。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Rare Weight-Cap Contract (R9.4.2)",
        false,
        2480)]
    private static void ValidateRareContract()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.2 权重／上限契约无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog runtimeCatalog =
            LoadAndValidateRuntimeCatalog(errors);

        DreamRoomCatalog weightCatalog =
            LoadAndValidateWeightCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 权重／上限契约无法开始",
                errors,
                context.Generator);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore =
            context.Scene.isDirty;

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        WeightMetrics weightMetrics =
            default(WeightMetrics);

        BatchMetrics batchMetrics =
            default(BatchMetrics);

        bool capBeforeAvailable = false;
        bool capAtOneAvailable = true;
        int postCapRareSelections = -1;
        bool fixedSeedDeterministic = false;
        bool grayboxDeterministic = false;

        try
        {
            ValidateWeightDistribution(
                weightCatalog,
                errors,
                out weightMetrics);

            ValidatePerTemplateCap(
                weightCatalog,
                errors,
                out capBeforeAvailable,
                out capAtOneAvailable,
                out postCapRareSelections);

            SetGeneratorCatalogTransient(
                context.Generator,
                runtimeCatalog);

            ValidateRuntimeBatch(
                context.Generator,
                errors,
                out batchMetrics,
                out fixedSeedDeterministic);

            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);

            grayboxDeterministic =
                ValidateGrayboxDeterminism(
                    context.Generator,
                    errors);
        }
        catch (Exception exception)
        {
            errors.Add(
                "R9.4.2 契约执行抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);
        }

        string protectedHashAfter =
            BuildProtectedBaselineHashSignature();

        bool protectedHashUnchanged =
            string.Equals(
                protectedHashBefore,
                protectedHashAfter,
                StringComparison.Ordinal);

        bool sceneChanged =
            context.Scene.isDirty != sceneDirtyBefore;

        if (!protectedHashUnchanged)
        {
            errors.Add(
                "契约测试改变了 GameScene／Graybox／R9.4.1 资产哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "契约测试改变了 GameScene Dirty 状态。" );
        }

        if (context.Generator.TemplateFirstRoomCatalog !=
            originalCatalog)
        {
            errors.Add(
                "契约结束后没有恢复原 Catalog 引用。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 权重／上限契约失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRareRuleAuditR942] " +
            "R9.4.2 Rare 权重／单层上限契约通过。\n" +
            "WeightProbe=Common:4,Rare:1" +
            " | Samples=" + WeightSampleCount +
            " | CommonSelections=" +
            weightMetrics.CommonSelections +
            " | RareSelections=" +
            weightMetrics.RareSelections +
            " | CommonRareRatio=" +
            weightMetrics.CommonRareRatio.ToString("F2") +
            " | WeightedDeterministic=" +
            weightMetrics.Deterministic + "\n" +
            "CapBeforeAvailable=" +
            capBeforeAvailable +
            " | CapAtOneAvailable=" +
            capAtOneAvailable +
            " | PostCapRareSelections=" +
            postCapRareSelections +
            " | CapScope=PerTemplatePerFloor\n" +
            "BatchSeeds=" + BatchSeedCount +
            " | Layouts=" + batchMetrics.SuccessfulLayouts +
            "/" + BatchSeedCount +
            " | FloorsWithRare=" +
            batchMetrics.FloorsWithRare +
            " | FloorsWithoutRare=" +
            batchMetrics.FloorsWithoutRare +
            " | RarePlacements=" +
            batchMetrics.RarePlacements +
            " | MaxRarePerFloor=" +
            batchMetrics.MaximumRarePerFloor + "\n" +
            "CoreDeferredPlacements=" +
            batchMetrics.CoreDeferredPlacements +
            " | SpecialDeferredPlacements=" +
            batchMetrics.SpecialDeferredPlacements +
            " | OrdinaryPool=Standard+Rare" +
            " | DeferredOrdinaryTagsExcluded=True\n" +
            "CommittedCountOnly=True" +
            " | FixedSeedDeterministic=" +
            fixedSeedDeterministic +
            " | GrayboxDeterministic=" +
            grayboxDeterministic +
            " | GrayboxRandomSequencePreserved=True\n" +
            "SceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | RuntimeObjectsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.2 Contract Passed",
            "低权重 Rare、单层上限 1、成功落位计数、" +
            "未来标签排除与固定 Seed 均已通过。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Prepare Rare Runtime Test (R9.4.2)",
        false,
        2490)]
    private static void PrepareRareRuntimeTest()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.2 Runtime Test 准备失败",
                errors,
                null);
            return;
        }

        DreamRoomCatalog runtimeCatalog =
            LoadAndValidateRuntimeCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        int liveSeed = 0;
        RareLayoutMetrics liveMetrics =
            default(RareLayoutMetrics);

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                runtimeCatalog);

            if (!TryFindLiveSeed(
                    context.Generator,
                    out liveSeed,
                    out liveMetrics,
                    errors))
            {
                errors.Add(
                    "没有找到可用于 Live 验收的完整 R6 Seed。" );
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "Runtime 预生成抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        SetGeneratorRuntimeSettings(
            context.Generator,
            runtimeCatalog,
            useRandomSeed: false,
            fixedSeed: liveSeed);

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        Debug.Log(
            "[DreamRoomRareRuleAuditR942] " +
            "R9.4.2 Rare Runtime Test 已准备。\n" +
            "Catalog=" + RuntimeCatalogId +
            " | LiveSeed=" + liveSeed +
            " | SceneSaved=False" +
            " | DiskBaseline=" + GrayboxCatalogId + "\n" +
            "Rooms=" + liveMetrics.RoomCount +
            "/" + ExpectedRoomCount +
            " | RarePlacements=" +
            liveMetrics.RarePlacements +
            " | RareCap=1" +
            " | DeferredPlacements=0\n" +
            "StaticAssets=True" +
            " | ReadOnlyPreflightPassed=True" +
            " | RestoreCatalog=" + GrayboxCatalogId +
            " | RestoreFixedSeed=" + BaselineSeed +
            " | DoNotSaveUntilRestore=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.2 Runtime Test Ready",
            "GameScene 当前只在内存中使用 R9.4.2 Catalog 与测试 Seed。\n\n" +
            "现在可以进入 Play Mode；结束后必须执行 Restore。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Live Rare Contract (R9.4.2)",
        false,
        2500)]
    private static void ValidateLiveRareContract()
    {
        List<string> errors =
            new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add(
                "必须在 Play Mode 且 Floor 1 已完整生成后执行。" );
            ReportFailure(
                "R9.4.2 Live 校验无法开始",
                errors,
                null);
            return;
        }

        Scene scene =
            SceneManager.GetActiveScene();

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(
                scene);

        GameManager gameManager =
            FindSceneComponent<GameManager>(
                scene);

        if (generator == null)
        {
            errors.Add(
                "Play Mode 中找不到 DungeonGenerator。" );
        }

        if (gameManager == null)
        {
            errors.Add(
                "Play Mode 中找不到 GameManager。" );
        }

        if (generator != null &&
            (generator.TemplateFirstRoomCatalog == null ||
             !string.Equals(
                 generator.TemplateFirstRoomCatalog.CatalogId,
                 RuntimeCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "当前 Catalog 不是 " + RuntimeCatalogId +
                "。请退出 Play Mode 后重新执行 Prepare。" );
        }

        if (gameManager != null &&
            gameManager.CurrentFloor != TestFloor)
        {
            errors.Add(
                "当前应为 Floor 1，实际为 Floor " +
                gameManager.CurrentFloor + "。" );
        }

        DungeonLayout layout = null;

        if (gameManager != null &&
            !TryReadCurrentLayout(
                gameManager,
                out layout))
        {
            errors.Add(
                "无法读取 GameManager.currentLayout，或布局尚未提交。" );
        }

        RareLayoutMetrics metrics =
            default(RareLayoutMetrics);

        if (layout != null)
        {
            ValidateLiveLayout(
                layout,
                errors,
                out metrics);
        }

        GameObject generatedRoot =
            GameObject.Find(
                "GeneratedDungeon_Floor_1");

        if (generatedRoot == null)
        {
            errors.Add(
                "找不到 GeneratedDungeon_Floor_1。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 Live Rare Contract 失败",
                errors,
                generator);
            return;
        }

        int liveSeed =
            ReadPrivateInt(
                generator,
                "fixedSeed",
                -1);

        Debug.Log(
            "[DreamRoomRareRuleAuditR942] " +
            "R9.4.2 真实运行时 Rare 契约通过。\n" +
            "Catalog=" + RuntimeCatalogId +
            " | LiveSeed=" + liveSeed +
            " | Rooms=" + metrics.RoomCount +
            "/" + ExpectedRoomCount + "\n" +
            "Start=" + metrics.StartTemplateId +
            "(Tagged=True)" +
            " | Exit=" + metrics.ExitTemplateId +
            "(Tagged=True)" +
            " | DistinctRooms=True\n" +
            "Rare=" + RareTemplateId +
            " | Weight=1" +
            " | Placements=" + metrics.RarePlacements +
            " | Cap=1" +
            " | CapBreaches=0\n" +
            "CoreDeferredPlacements=" +
            metrics.CoreDeferredPlacements +
            " | SpecialDeferredPlacements=" +
            metrics.SpecialDeferredPlacements +
            " | DeferredOrdinaryTagsExcluded=True\n" +
            "GeneratedRoot=GeneratedDungeon_Floor_1" +
            " | RuntimeObjectsModified=False",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.2 Live Rare Passed",
            "Floor 1 含且仅含一个低权重 Rare 房；" +
            "未来标签没有进入布局。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Restore and Save Graybox after R9.4.2",
        false,
        2510)]
    private static void RestoreGrayboxBaseline()
    {
        List<string> errors =
            new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add(
                "必须先退出 Play Mode。" );
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add(
                "必须先退出 Prefab Mode。" );
        }

        Scene scene =
            SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add(
                "必须打开 " + GameScenePath + "。" );
        }

        DungeonGenerator generator =
            scene.IsValid()
                ? FindSceneComponent<DungeonGenerator>(
                    scene)
                : null;

        if (generator == null)
        {
            errors.Add(
                "GameScene 中找不到 DungeonGenerator。" );
        }

        DreamRoomCatalog grayboxCatalog =
            LoadCatalog(
                GrayboxCatalogPath,
                GrayboxCatalogId,
                "Graybox Catalog",
                errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.2 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        SetGeneratorRuntimeSettings(
            generator,
            grayboxCatalog,
            useRandomSeed: false,
            fixedSeed: BaselineSeed);

        EditorSceneManager.MarkSceneDirty(
            scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            errors.Add(
                "Graybox 已写回内存，但 GameScene 保存失败。" );
            ReportFailure(
                "R9.4.2 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        if (scene.isDirty ||
            generator.TemplateFirstRoomCatalog !=
            grayboxCatalog ||
            ReadPrivateInt(
                generator,
                "fixedSeed",
                -1) != BaselineSeed)
        {
            errors.Add(
                "保存后 Scene 仍为 Dirty，或 Catalog／Fixed Seed 未恢复。" );
            ReportFailure(
                "R9.4.2 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRareRuleAuditR942] " +
            "R9.4.2 Graybox 基线已恢复并保存。\n" +
            "Catalog=" + GrayboxCatalogId +
            " | SceneSaved=True" +
            " | RenderMode=HybridPrefabRooms" +
            " | FixedSeed=" + BaselineSeed + "\n" +
            "R9.4.1AssetsRetained=True" +
            " | R9.4.2TestAssetsRetained=True" +
            " | RuntimePatchRetained=True",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.2 Graybox Restored",
            "GameScene 已恢复 Graybox Catalog 与 Fixed Seed 12345 并保存。",
            "OK");
    }

    private static DreamRoomCatalog
        LoadAndValidateRuntimeCatalog(
            List<string> errors)
    {
        DreamRoomCatalog catalog =
            LoadCatalog(
                RuntimeCatalogPath,
                RuntimeCatalogId,
                "Runtime Catalog",
                errors);

        if (catalog == null)
        {
            return null;
        }

        AppendCatalogValidationErrors(
            catalog,
            "Runtime Catalog",
            errors);

        string[] expectedIds =
        {
            StartTemplateId,
            ExitTemplateId,
            CommonTemplateId,
            RareTemplateId,
            CoreDeferredTemplateId,
            SpecialDeferredTemplateId
        };

        AppendCatalogReferenceErrors(
            catalog,
            expectedIds,
            "Runtime Catalog",
            errors);

        return catalog;
    }

    private static DreamRoomCatalog
        LoadAndValidateWeightCatalog(
            List<string> errors)
    {
        DreamRoomCatalog catalog =
            LoadCatalog(
                WeightCatalogPath,
                WeightCatalogId,
                "Weight Probe Catalog",
                errors);

        if (catalog == null)
        {
            return null;
        }

        AppendCatalogValidationErrors(
            catalog,
            "Weight Probe Catalog",
            errors);

        AppendCatalogReferenceErrors(
            catalog,
            WeightTemplateIds,
            "Weight Probe Catalog",
            errors);

        return catalog;
    }

    private static void ValidateWeightDistribution(
        DreamRoomCatalog catalog,
        List<string> errors,
        out WeightMetrics metrics)
    {
        metrics = default(WeightMetrics);

        WeightMetrics first =
            RunWeightSample(
                catalog,
                WeightSampleSeed,
                errors);

        WeightMetrics repeated =
            RunWeightSample(
                catalog,
                WeightSampleSeed,
                errors);

        first.Deterministic =
            first.CommonSelections ==
                repeated.CommonSelections &&
            first.RareSelections ==
                repeated.RareSelections;

        first.CommonRareRatio =
            first.RareSelections <= 0
                ? 0d
                : (double)first.CommonSelections /
                  first.RareSelections;

        if (!first.Deterministic)
        {
            errors.Add(
                "相同 Seed 的权重采样结果不一致。" );
        }

        if (first.CommonSelections +
            first.RareSelections !=
            WeightSampleCount)
        {
            errors.Add(
                "权重采样成功次数不是 " +
                WeightSampleCount + "。" );
        }

        if (first.RareSelections <= 0)
        {
            errors.Add(
                "低权重 Rare 在固定样本中一次也没有被抽到。" );
        }

        if (first.CommonRareRatio < 2.5d ||
            first.CommonRareRatio > 5.5d)
        {
            errors.Add(
                "Common:Rare 的实际选择比例没有体现 4:1 权重。" +
                "当前为 " +
                first.CommonRareRatio.ToString("F2") +
                ":1。" );
        }

        metrics = first;
    }

    private static WeightMetrics RunWeightSample(
        DreamRoomCatalog catalog,
        int seed,
        List<string> errors)
    {
        WeightMetrics metrics =
            default(WeightMetrics);

        System.Random random =
            new System.Random(seed);

        for (int i = 0;
             i < WeightSampleCount;
             i++)
        {
            DreamRoomTemplate selected;

            if (!catalog.TryChooseWeighted(
                    TestFloor,
                    null,
                    random,
                    out selected) ||
                selected == null)
            {
                errors.Add(
                    "权重采样在第 " + i +
                    " 次没有返回模板。" );
                break;
            }

            if (string.Equals(
                    selected.TemplateId,
                    CommonTemplateId,
                    StringComparison.Ordinal))
            {
                metrics.CommonSelections++;
            }
            else if (string.Equals(
                         selected.TemplateId,
                         RareTemplateId,
                         StringComparison.Ordinal))
            {
                metrics.RareSelections++;
            }
            else
            {
                errors.Add(
                    "Weight Probe 返回未知模板：" +
                    selected.TemplateId + "。" );
                break;
            }
        }

        return metrics;
    }

    private static void ValidatePerTemplateCap(
        DreamRoomCatalog catalog,
        List<string> errors,
        out bool capBeforeAvailable,
        out bool capAtOneAvailable,
        out int postCapRareSelections)
    {
        capBeforeAvailable = false;
        capAtOneAvailable = true;
        postCapRareSelections = 0;

        DreamRoomTemplate rareTemplate;

        if (!catalog.TryGetTemplate(
                RareTemplateId,
                out rareTemplate) ||
            rareTemplate == null)
        {
            errors.Add(
                "Weight Probe 找不到 " +
                RareTemplateId + "。" );
            return;
        }

        Dictionary<string, int> counts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        counts[RareTemplateId] = 0;

        capBeforeAvailable =
            catalog.IsTemplateEligible(
                rareTemplate,
                TestFloor,
                counts);

        counts[RareTemplateId] = 1;

        capAtOneAvailable =
            catalog.IsTemplateEligible(
                rareTemplate,
                TestFloor,
                counts);

        if (!capBeforeAvailable)
        {
            errors.Add(
                "Rare 在使用次数 0 时本应可用。" );
        }

        if (capAtOneAvailable)
        {
            errors.Add(
                "Rare 在使用次数达到上限 1 后仍被判定为可用。" );
        }

        System.Random random =
            new System.Random(WeightSampleSeed);

        for (int i = 0; i < 128; i++)
        {
            DreamRoomTemplate selected;

            if (!catalog.TryChooseWeighted(
                    TestFloor,
                    counts,
                    random,
                    out selected) ||
                selected == null)
            {
                errors.Add(
                    "达到 Rare 上限后的候选池不应为空。" );
                return;
            }

            if (string.Equals(
                    selected.TemplateId,
                    RareTemplateId,
                    StringComparison.Ordinal))
            {
                postCapRareSelections++;
            }
        }

        if (postCapRareSelections != 0)
        {
            errors.Add(
                "Rare 达到单层上限后仍被选中 " +
                postCapRareSelections + " 次。" );
        }
    }

    private static void ValidateRuntimeBatch(
        DungeonGenerator generator,
        List<string> errors,
        out BatchMetrics metrics,
        out bool fixedSeedDeterministic)
    {
        metrics = default(BatchMetrics);
        fixedSeedDeterministic = false;

        string firstSignature = string.Empty;

        for (int offset = 0;
             offset < BatchSeedCount;
             offset++)
        {
            int seed =
                unchecked(BatchSeedStart + offset);

            DungeonLayout layout;
            string report;

            if (!generator.TryGenerateTemplateFirstLayout(
                    TestFloor,
                    seed,
                    out layout,
                    out report))
            {
                errors.Add(
                    "批量 Seed " + seed +
                    " 的 R4 生成失败：\n" + report);
                continue;
            }

            RareLayoutMetrics layoutMetrics;

            ValidateR4Layout(
                layout,
                seed,
                errors,
                out layoutMetrics);

            metrics.SuccessfulLayouts++;
            metrics.RarePlacements +=
                layoutMetrics.RarePlacements;

            metrics.CoreDeferredPlacements +=
                layoutMetrics.CoreDeferredPlacements;

            metrics.SpecialDeferredPlacements +=
                layoutMetrics.SpecialDeferredPlacements;

            metrics.MaximumRarePerFloor =
                Mathf.Max(
                    metrics.MaximumRarePerFloor,
                    layoutMetrics.RarePlacements);

            if (layoutMetrics.RarePlacements > 0)
            {
                metrics.FloorsWithRare++;
            }
            else
            {
                metrics.FloorsWithoutRare++;
            }

            if (offset == 0)
            {
                firstSignature =
                    BuildPlacementSignature(layout);
            }
        }

        if (metrics.SuccessfulLayouts !=
            BatchSeedCount)
        {
            errors.Add(
                "批量布局成功数应为 " +
                BatchSeedCount + "，实际为 " +
                metrics.SuccessfulLayouts + "。" );
        }

        if (metrics.FloorsWithRare == 0)
        {
            errors.Add(
                "批量 Seed 中没有任何楼层选中 Rare。" );
        }

        if (metrics.FloorsWithoutRare == 0)
        {
            errors.Add(
                "低权重 Rare 在每个批量楼层都出现，" +
                "没有体现“候选而非强制房”的语义。" );
        }

        if (metrics.MaximumRarePerFloor > 1)
        {
            errors.Add(
                "批量生成中 Rare 的单层最大值为 " +
                metrics.MaximumRarePerFloor +
                "，超过上限 1。" );
        }

        if (metrics.CoreDeferredPlacements != 0 ||
            metrics.SpecialDeferredPlacements != 0)
        {
            errors.Add(
                "未来标签诱饵进入了普通槽位：" +
                "Core=" +
                metrics.CoreDeferredPlacements +
                "，Special=" +
                metrics.SpecialDeferredPlacements + "。" );
        }

        DungeonLayout repeated;
        string repeatedReport;

        if (!generator.TryGenerateTemplateFirstLayout(
                TestFloor,
                BatchSeedStart,
                out repeated,
                out repeatedReport))
        {
            errors.Add(
                "固定 Seed 重现失败：\n" +
                repeatedReport);
            return;
        }

        fixedSeedDeterministic =
            string.Equals(
                firstSignature,
                BuildPlacementSignature(repeated),
                StringComparison.Ordinal);

        if (!fixedSeedDeterministic)
        {
            errors.Add(
                "相同 R9.4.2 Seed 没有重现相同 R4 布局。" );
        }
    }

    private static bool ValidateGrayboxDeterminism(
        DungeonGenerator generator,
        List<string> errors)
    {
        DungeonLayout first;
        DungeonLayout repeated;
        string firstReport;
        string repeatedReport;

        if (!generator.TryGenerateTemplateFirstLayout(
                TestFloor,
                BaselineSeed,
                out first,
                out firstReport))
        {
            errors.Add(
                "Graybox 固定 Seed 第一次生成失败：\n" +
                firstReport);
            return false;
        }

        if (!generator.TryGenerateTemplateFirstLayout(
                TestFloor,
                BaselineSeed,
                out repeated,
                out repeatedReport))
        {
            errors.Add(
                "Graybox 固定 Seed 第二次生成失败：\n" +
                repeatedReport);
            return false;
        }

        bool deterministic =
            string.Equals(
                BuildPlacementSignature(first),
                BuildPlacementSignature(repeated),
                StringComparison.Ordinal);

        if (!deterministic)
        {
            errors.Add(
                "Graybox 固定 Seed 12345 无法重现。" );
        }

        return deterministic;
    }

    private static bool TryFindLiveSeed(
        DungeonGenerator generator,
        out int liveSeed,
        out RareLayoutMetrics metrics,
        List<string> errors)
    {
        liveSeed = 0;
        metrics = default(RareLayoutMetrics);

        for (int offset = 0;
             offset < LiveSeedSearchCount;
             offset++)
        {
            int candidateSeed =
                unchecked(BatchSeedStart + offset);

            DungeonLayout layout;
            string report;

            if (!generator.TryGenerateSocketCorridorLayout(
                    TestFloor,
                    candidateSeed,
                    out layout,
                    out report))
            {
                continue;
            }

            List<string> candidateErrors =
                new List<string>();

            RareLayoutMetrics candidateMetrics;

            ValidateLiveLayout(
                layout,
                candidateErrors,
                out candidateMetrics);

            if (candidateErrors.Count == 0 &&
                candidateMetrics.RarePlacements == 1)
            {
                liveSeed = candidateSeed;
                metrics = candidateMetrics;
                return true;
            }
        }

        errors.Add(
            "在 " + LiveSeedSearchCount +
            " 个固定 Seed 内，没有找到含且仅含一个 Rare 的完整 R6 布局。" );

        return false;
    }

    private static void ValidateR4Layout(
        DungeonLayout layout,
        int seed,
        List<string> errors,
        out RareLayoutMetrics metrics)
    {
        metrics = AnalyzeLayout(layout);

        if (layout == null)
        {
            errors.Add(
                "Seed " + seed + " 的 R4 Layout 为空。" );
            return;
        }

        if (metrics.RoomCount !=
            ExpectedRoomCount)
        {
            errors.Add(
                "Seed " + seed +
                " 的房间数应为 " +
                ExpectedRoomCount +
                "，实际为 " +
                metrics.RoomCount + "。" );
        }

        DreamRoomTemplate firstTemplate =
            GetTemplateAt(layout, 0);

        DreamRoomTemplate secondTemplate =
            GetTemplateAt(layout, 1);

        if (firstTemplate == null ||
            !firstTemplate.HasTag(
                DreamRoomTag.StartCandidate))
        {
            errors.Add(
                "Seed " + seed +
                " 的 R4 第 0 槽不是 StartCandidate。" );
        }

        if (secondTemplate == null ||
            !secondTemplate.HasTag(
                DreamRoomTag.ExitCandidate))
        {
            errors.Add(
                "Seed " + seed +
                " 的 R4 第 1 槽不是 ExitCandidate。" );
        }

        if (metrics.RarePlacements > 1)
        {
            errors.Add(
                "Seed " + seed +
                " 的 Rare 放置数为 " +
                metrics.RarePlacements +
                "，超过上限 1。" );
        }

        if (metrics.CoreDeferredPlacements != 0 ||
            metrics.SpecialDeferredPlacements != 0)
        {
            errors.Add(
                "Seed " + seed +
                " 选中了尚未启用的未来标签。" );
        }
    }

    private static void ValidateLiveLayout(
        DungeonLayout layout,
        List<string> errors,
        out RareLayoutMetrics metrics)
    {
        metrics = AnalyzeLayout(layout);

        if (layout == null)
        {
            errors.Add(
                "Live Layout 为空。" );
            return;
        }

        if (metrics.RoomCount !=
            ExpectedRoomCount)
        {
            errors.Add(
                "Live 房间数应为 " +
                ExpectedRoomCount +
                "，实际为 " +
                metrics.RoomCount + "。" );
        }

        int startRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.StartCell);

        int exitRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.ExitCell);

        DreamRoomTemplate startTemplate =
            GetTemplateAt(
                layout,
                startRoomIndex);

        DreamRoomTemplate exitTemplate =
            GetTemplateAt(
                layout,
                exitRoomIndex);

        metrics.StartTemplateId =
            FormatTemplateId(startTemplate);

        metrics.ExitTemplateId =
            FormatTemplateId(exitTemplate);

        if (startTemplate == null ||
            !startTemplate.HasTag(
                DreamRoomTag.StartCandidate))
        {
            errors.Add(
                "StartCell 不在 StartCandidate 房内。" );
        }

        if (exitTemplate == null ||
            !exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate))
        {
            errors.Add(
                "ExitCell 不在 ExitCandidate 房内。" );
        }

        if (startRoomIndex < 0 ||
            exitRoomIndex < 0 ||
            startRoomIndex == exitRoomIndex)
        {
            errors.Add(
                "Start／Exit 房必须存在且彼此不同。" );
        }

        if (metrics.RarePlacements != 1)
        {
            errors.Add(
                "Live 布局应含且仅含一个 Rare，实际为 " +
                metrics.RarePlacements + "。" );
        }

        if (metrics.CoreDeferredPlacements != 0 ||
            metrics.SpecialDeferredPlacements != 0)
        {
            errors.Add(
                "Live 布局包含尚未启用的未来标签。" );
        }

        List<string> quotaErrors =
            GetRareQuotaErrors(layout);

        for (int i = 0;
             i < quotaErrors.Count;
             i++)
        {
            errors.Add(quotaErrors[i]);
        }
    }

    private static RareLayoutMetrics AnalyzeLayout(
        DungeonLayout layout)
    {
        RareLayoutMetrics metrics =
            default(RareLayoutMetrics);

        if (layout == null)
        {
            return metrics;
        }

        metrics.RoomCount =
            layout.RoomPlacements.Count;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                GetTemplateAt(layout, i);

            if (template == null)
            {
                continue;
            }

            if (template.HasTag(
                    DreamRoomTag.Rare))
            {
                metrics.RarePlacements++;
            }

            if (template.HasTag(
                    DreamRoomTag.CoreItemCandidate))
            {
                metrics.CoreDeferredPlacements++;
            }

            if (template.HasTag(
                    DreamRoomTag.Special))
            {
                metrics.SpecialDeferredPlacements++;
            }
        }

        return metrics;
    }

    private static List<string> GetRareQuotaErrors(
        DungeonLayout layout)
    {
        List<string> errors =
            new List<string>();

        Dictionary<string, int> counts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, DreamRoomTemplate> templates =
            new Dictionary<string, DreamRoomTemplate>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                GetTemplateAt(layout, i);

            if (template == null ||
                !template.HasTag(DreamRoomTag.Rare))
            {
                continue;
            }

            int current;
            counts.TryGetValue(
                template.TemplateId,
                out current);

            counts[template.TemplateId] =
                current + 1;

            templates[template.TemplateId] =
                template;
        }

        foreach (KeyValuePair<string, int> pair
                 in counts)
        {
            DreamRoomTemplate template =
                templates[pair.Key];

            int maximum =
                template.MaximumInstancesPerFloor;

            if (maximum > 0 &&
                pair.Value > maximum)
            {
                errors.Add(
                    "Rare 模板 '" + pair.Key +
                    "' 超过单层上限：" +
                    pair.Value + "/" + maximum + "。" );
            }
        }

        return errors;
    }

    private static string BuildPlacementSignature(
        DungeonLayout layout)
    {
        StringBuilder builder =
            new StringBuilder();

        if (layout == null)
        {
            return "null";
        }

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                builder.Append("null;");
                continue;
            }

            builder.Append(
                placement.Template.TemplateId);
            builder.Append('|');
            builder.Append(
                placement.MinimumCell.x);
            builder.Append(',');
            builder.Append(
                placement.MinimumCell.y);
            builder.Append('|');
            builder.Append(
                placement.ClockwiseQuarterTurns);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static int FindWalkableRoomIndex(
        DungeonLayout layout,
        Vector2Int cell)
    {
        if (layout == null)
        {
            return -1;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null)
            {
                continue;
            }

            placement.GetWalkableGlobalCells(
                walkableCells);

            for (int cellIndex = 0;
                 cellIndex < walkableCells.Count;
                 cellIndex++)
            {
                if (walkableCells[cellIndex] == cell)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static DreamRoomTemplate GetTemplateAt(
        DungeonLayout layout,
        int roomIndex)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >=
                layout.RoomPlacements.Count ||
            layout.RoomPlacements[roomIndex] == null)
        {
            return null;
        }

        return layout.RoomPlacements[
            roomIndex].Template;
    }

    private static string FormatTemplateId(
        DreamRoomTemplate template)
    {
        if (template == null ||
            string.IsNullOrWhiteSpace(
                template.TemplateId))
        {
            return "<missing>";
        }

        return template.TemplateId;
    }

    private static void AppendStaticCatalogErrors(
        DreamRoomCatalog catalog,
        TemplateSpec[] specs,
        List<string> errors,
        out int ownedSockets)
    {
        ownedSockets = 0;

        AppendCatalogValidationErrors(
            catalog,
            "Runtime Catalog",
            errors);

        string[] expectedIds =
            new string[specs.Length];

        for (int i = 0; i < specs.Length; i++)
        {
            expectedIds[i] = specs[i].TemplateId;
        }

        AppendCatalogReferenceErrors(
            catalog,
            expectedIds,
            "Runtime Catalog",
            errors);

        for (int i = 0; i < specs.Length; i++)
        {
            TemplateSpec spec = specs[i];

            DreamRoomTemplate assetTemplate =
                AssetDatabase.LoadAssetAtPath<
                    DreamRoomTemplate>(
                    spec.AssetPath);

            if (assetTemplate == null)
            {
                errors.Add(
                    "找不到 Prefab Asset：" +
                    spec.AssetPath);
                continue;
            }

            if (!string.Equals(
                    assetTemplate.TemplateId,
                    spec.TemplateId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    spec.TemplateId +
                    " 的 TemplateId 不正确。" );
            }

            if (assetTemplate.RoomTags !=
                spec.RoomTags)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的 RoomTags 应为 " +
                    spec.RoomTags + "，实际为 " +
                    assetTemplate.RoomTags + "。" );
            }

            if (assetTemplate.RandomWeight !=
                spec.RandomWeight)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的 RandomWeight 应为 " +
                    spec.RandomWeight + "，实际为 " +
                    assetTemplate.RandomWeight + "。" );
            }

            if (assetTemplate.MaximumInstancesPerFloor !=
                spec.MaximumInstancesPerFloor)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的 MaximumInstancesPerFloor 应为 " +
                    spec.MaximumInstancesPerFloor +
                    "，实际为 " +
                    assetTemplate.MaximumInstancesPerFloor +
                    "。" );
            }

            GameObject loadedRoot = null;

            try
            {
                loadedRoot =
                    PrefabUtility.LoadPrefabContents(
                        spec.AssetPath);

                DreamRoomTemplate loadedTemplate =
                    loadedRoot == null
                        ? null
                        : loadedRoot.GetComponent<
                            DreamRoomTemplate>();

                if (loadedTemplate == null)
                {
                    errors.Add(
                        spec.TemplateId +
                        " 的完整 Prefab Contents 找不到 DreamRoomTemplate。" );
                    continue;
                }

                List<string> prefabErrors =
                    loadedTemplate.GetValidationErrors();

                for (int errorIndex = 0;
                     errorIndex < prefabErrors.Count;
                     errorIndex++)
                {
                    errors.Add(
                        spec.TemplateId + "：" +
                        prefabErrors[errorIndex]);
                }

                int socketCount =
                    loadedTemplate.DoorSockets.Count;

                int owned =
                    CountOwnedSockets(
                        loadedTemplate);

                ownedSockets += owned;

                if (socketCount != 4 ||
                    owned != socketCount)
                {
                    errors.Add(
                        spec.TemplateId +
                        " 的 Socket 所有权应为 4/4，实际为 " +
                        owned + "/" + socketCount + "。" );
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的 Prefab Contents 校验抛出异常：\n" +
                    exception);
            }
            finally
            {
                if (loadedRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(
                        loadedRoot);
                }
            }
        }
    }

    private static int CountOwnedSockets(
        DreamRoomTemplate template)
    {
        int owned = 0;

        for (int i = 0;
             i < template.DoorSockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[i];

            if (socket != null &&
                socket.transform.IsChildOf(
                    template.transform) &&
                socket.GetComponentInParent<
                    DreamRoomTemplate>() == template)
            {
                owned++;
            }
        }

        return owned;
    }

    private static void AppendCatalogValidationErrors(
        DreamRoomCatalog catalog,
        string label,
        List<string> errors)
    {
        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0;
             i < catalogErrors.Count;
             i++)
        {
            errors.Add(
                label + "：" +
                catalogErrors[i]);
        }
    }

    private static void AppendCatalogReferenceErrors(
        DreamRoomCatalog catalog,
        string[] expectedTemplateIds,
        string label,
        List<string> errors)
    {
        if (catalog.Count !=
            expectedTemplateIds.Length)
        {
            errors.Add(
                label + " 模板数应为 " +
                expectedTemplateIds.Length +
                "，实际为 " + catalog.Count + "。" );
            return;
        }

        for (int i = 0;
             i < expectedTemplateIds.Length;
             i++)
        {
            DreamRoomTemplate template =
                catalog.RoomTemplates[i];

            if (template == null ||
                !string.Equals(
                    template.TemplateId,
                    expectedTemplateIds[i],
                    StringComparison.Ordinal))
            {
                errors.Add(
                    label + " Element " + i +
                    " 应为 " +
                    expectedTemplateIds[i] +
                    "，实际为 " +
                    FormatTemplateId(template) + "。" );
            }
        }
    }

    private static DreamRoomCatalog LoadCatalog(
        string assetPath,
        string expectedId,
        string label,
        List<string> errors)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomCatalog>(
                assetPath);

        if (catalog == null)
        {
            errors.Add(
                label + " 不存在：" +
                assetPath);
            return null;
        }

        if (!string.Equals(
                catalog.CatalogId,
                expectedId,
                StringComparison.Ordinal))
        {
            errors.Add(
                label + " CatalogId 应为 " +
                expectedId + "，实际为 " +
                catalog.CatalogId + "。" );
        }

        return catalog;
    }

    private static bool TryGetCleanGrayboxSceneContext(
        bool requireCleanScene,
        out SceneContext context,
        out List<string> errors)
    {
        context = default(SceneContext);
        errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add(
                "必须先退出 Play Mode。" );
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add(
                "必须先退出 Prefab Mode。" );
            return false;
        }

        Scene scene =
            SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            errors.Add(
                "当前没有有效 Scene。" );
            return false;
        }

        if (!string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add(
                "必须打开 " + GameScenePath +
                "，当前为 " + scene.path + "。" );
            return false;
        }

        if (requireCleanScene &&
            scene.isDirty)
        {
            errors.Add(
                "GameScene 当前有未保存修改。" +
                "开始前必须是无星号的 Graybox 基线。" );
        }

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(
                scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(
                scene);

        if (generator == null)
        {
            errors.Add(
                "GameScene 中找不到 DungeonGenerator。" );
        }

        if (renderer == null)
        {
            errors.Add(
                "GameScene 中找不到 DungeonRenderer。" );
        }

        if (generator != null &&
            (generator.TemplateFirstRoomCatalog == null ||
             !string.Equals(
                 generator.TemplateFirstRoomCatalog.CatalogId,
                 GrayboxCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "开始前 R4 Catalog 必须为 " +
                GrayboxCatalogId + "。" );
        }

        if (renderer != null &&
            renderer.RenderMode !=
            DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add(
                "DungeonRenderer.Render Mode 必须为 Hybrid Prefab Rooms。" );
        }

        if (generator != null)
        {
            RequireBool(
                generator,
                "useRandomSeed",
                false,
                "Use Random Seed",
                errors);

            RequireInt(
                generator,
                "fixedSeed",
                BaselineSeed,
                "Fixed Seed",
                errors);
        }

        context =
            new SceneContext(
                scene,
                generator,
                renderer);

        return errors.Count == 0;
    }

    private static void SetGeneratorRuntimeSettings(
        DungeonGenerator generator,
        DreamRoomCatalog catalog,
        bool useRandomSeed,
        int fixedSeed)
    {
        SerializedObject serializedGenerator =
            new SerializedObject(generator);

        SerializedProperty catalogProperty =
            serializedGenerator.FindProperty(
                "templateFirstRoomCatalog");

        SerializedProperty randomProperty =
            serializedGenerator.FindProperty(
                "useRandomSeed");

        SerializedProperty seedProperty =
            serializedGenerator.FindProperty(
                "fixedSeed");

        if (catalogProperty == null ||
            randomProperty == null ||
            seedProperty == null)
        {
            throw new InvalidOperationException(
                "DungeonGenerator 缺少 Catalog／随机种子序列化字段。" );
        }

        catalogProperty.objectReferenceValue =
            catalog;

        randomProperty.boolValue =
            useRandomSeed;

        seedProperty.intValue =
            fixedSeed;

        serializedGenerator.
            ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetGeneratorCatalogTransient(
        DungeonGenerator generator,
        DreamRoomCatalog catalog)
    {
        FieldInfo field =
            typeof(DungeonGenerator).GetField(
                "templateFirstRoomCatalog",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            throw new InvalidOperationException(
                "DungeonGenerator 找不到 templateFirstRoomCatalog 字段。" );
        }

        field.SetValue(generator, catalog);
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

        layout =
            field.GetValue(gameManager)
            as DungeonLayout;

        return layout != null;
    }

    private static T FindSceneComponent<T>(
        Scene scene)
        where T : Component
    {
        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int i = 0;
             i < roots.Length;
             i++)
        {
            T component =
                roots[i].GetComponentInChildren<T>(
                    includeInactive: true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void RequireBool(
        object target,
        string fieldName,
        bool expected,
        string label,
        List<string> errors)
    {
        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null ||
            field.FieldType != typeof(bool))
        {
            errors.Add(
                "无法读取 " + label + "。" );
            return;
        }

        bool actual =
            (bool)field.GetValue(target);

        if (actual != expected)
        {
            errors.Add(
                label + " 应为 " + expected +
                "，实际为 " + actual + "。" );
        }
    }

    private static void RequireInt(
        object target,
        string fieldName,
        int expected,
        string label,
        List<string> errors)
    {
        int actual =
            ReadPrivateInt(
                target,
                fieldName,
                int.MinValue);

        if (actual == int.MinValue)
        {
            errors.Add(
                "无法读取 " + label + "。" );
            return;
        }

        if (actual != expected)
        {
            errors.Add(
                label + " 应为 " + expected +
                "，实际为 " + actual + "。" );
        }
    }

    private static int ReadPrivateInt(
        object target,
        string fieldName,
        int fallback)
    {
        if (target == null)
        {
            return fallback;
        }

        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null ||
            field.FieldType != typeof(int))
        {
            return fallback;
        }

        return (int)field.GetValue(target);
    }

    private static string
        BuildProtectedBaselineHashSignature()
    {
        string[] protectedPaths =
        {
            GameScenePath,
            GrayboxCatalogPath,
            GrayboxRoot + "/Rooms/Room_08x06.prefab",
            GrayboxRoot + "/Rooms/Room_09x16.prefab",
            GrayboxRoot + "/Rooms/Room_13x09.prefab",
            GrayboxRoot + "/Rooms/Room_18x07.prefab",
            R941Root + "/Catalog/RoomCatalog_R941C_RoleTagsTest.asset",
            R941Root + "/Catalog/RoomCatalog_R941C_UnsafeNoStandard.asset",
            R941Root + "/Rooms/Room_R941C_Start.prefab",
            R941Root + "/Rooms/Room_R941C_Exit.prefab",
            R941Root + "/Rooms/Room_R941C_Standard_A.prefab",
            R941Root + "/Rooms/Room_R941C_Standard_B.prefab",
            R941Root + "/Rooms/Room_R941C_RareOnly.prefab"
        };

        StringBuilder builder =
            new StringBuilder();

        for (int i = 0;
             i < protectedPaths.Length;
             i++)
        {
            builder.Append(
                protectedPaths[i]);
            builder.Append('=');
            builder.Append(
                AssetDatabase.GetAssetDependencyHash(
                    protectedPaths[i]));
            builder.Append('|');
        }

        return builder.ToString();
    }

    private static void ReportFailure(
        string title,
        List<string> errors,
        UnityEngine.Object context)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "[DreamRoomRareRuleAuditR942] " +
            title);

        if (errors == null ||
            errors.Count == 0)
        {
            builder.AppendLine(
                "- 未提供具体错误。" );
        }
        else
        {
            for (int i = 0;
                 i < errors.Count;
                 i++)
            {
                builder.AppendLine(
                    "- " + errors[i]);
            }
        }

        Debug.LogError(
            builder.ToString(),
            context);

        EditorUtility.DisplayDialog(
            title,
            "操作未完成。请保留 Console 第一条完整红错。",
            "OK");
    }

    private sealed class TemplateSpec
    {
        public string AssetPath { get; }
        public string TemplateId { get; }
        public DreamRoomTag RoomTags { get; }
        public int RandomWeight { get; }
        public int MaximumInstancesPerFloor { get; }

        public TemplateSpec(
            string assetPath,
            string templateId,
            DreamRoomTag roomTags,
            int randomWeight,
            int maximumInstancesPerFloor)
        {
            AssetPath = assetPath;
            TemplateId = templateId;
            RoomTags = roomTags;
            RandomWeight = randomWeight;
            MaximumInstancesPerFloor =
                maximumInstancesPerFloor;
        }
    }

    private struct SceneContext
    {
        public Scene Scene { get; }
        public DungeonGenerator Generator { get; }
        public DungeonRenderer Renderer { get; }

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

    private struct WeightMetrics
    {
        public int CommonSelections;
        public int RareSelections;
        public double CommonRareRatio;
        public bool Deterministic;
    }

    private struct BatchMetrics
    {
        public int SuccessfulLayouts;
        public int FloorsWithRare;
        public int FloorsWithoutRare;
        public int RarePlacements;
        public int MaximumRarePerFloor;
        public int CoreDeferredPlacements;
        public int SpecialDeferredPlacements;
    }

    private struct RareLayoutMetrics
    {
        public int RoomCount;
        public int RarePlacements;
        public int CoreDeferredPlacements;
        public int SpecialDeferredPlacements;
        public string StartTemplateId;
        public string ExitTemplateId;
    }
}
