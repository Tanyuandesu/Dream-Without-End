using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.4.4 Special 每层全局唯一、Core + Special 组合角色与
/// ItemSpawner 共享作用域的静态资产、纯数据和真实运行验收工具。
///
/// Prefab 层级校验只使用 LoadPrefabContents；本工具不会创建、复制或
/// 保存 Prefab。Prepare／Restore 只临时切换或恢复 GameScene 的
/// Catalog 与固定 Seed。
/// </summary>
public static class DreamRoomSpecialRoomAuditR944
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

    private const string R942Root =
        "Assets/DreamDungeon/Generated/R9_4_2_Rare";

    private const string R943Root =
        "Assets/DreamDungeon/Generated/R9_4_3_CoreItem";

    private const string TestRoot =
        "Assets/DreamDungeon/Generated/R9_4_4_Special";

    private const string RoomRoot =
        TestRoot + "/Rooms";

    private const string RuntimeCatalogPath =
        TestRoot + "/Catalog/RoomCatalog_R944_Runtime.asset";

    private const string RuntimeCatalogId =
        "SpecialUnique_R944_Runtime";

    private const string DedicatedCatalogPath =
        TestRoot + "/Catalog/RoomCatalog_R944_Dedicated.asset";

    private const string DedicatedCatalogId =
        "SpecialUnique_R944_Dedicated";

    private const string WeightCatalogPath =
        TestRoot + "/Catalog/RoomCatalog_R944_WeightProbe.asset";

    private const string WeightCatalogId =
        "SpecialUnique_R944_WeightProbe";

    private const string StartTemplateId =
        "R944_Start";

    private const string ExitTemplateId =
        "R944_Exit";

    private const string CommonTemplateId =
        "R944_Common";

    private const string RareTemplateId =
        "R944_Rare";

    private const string CoreStandaloneTemplateId =
        "R944_CoreStandalone";

    private const string CoreSpecialTemplateId =
        "R944_CoreSpecial";

    private const string SpecialLowTemplateId =
        "R944_SpecialLow";

    private const string SpecialHighTemplateId =
        "R944_SpecialHigh";

    // 仅供本文件中保留的无菜单 R9.4.3 诊断实现编译；正式 R9.4.4
    // 验收入口使用上方语义明确的常量。
    private const string CoreTemplateId =
        CoreStandaloneTemplateId;

    private const string CoreSpecialDecoyTemplateId =
        CoreSpecialTemplateId;

    private const string SpecialDeferredTemplateId =
        SpecialHighTemplateId;

    private const int ContractFloor = 2;
    private const int ExpectedRoomCount = 7;
    private const int ExpectedSocketCountPerTemplate = 4;
    private const int BaselineSeed = 12345;
    private const int BatchSeedStart = 94400;
    private const int BatchSeedCount = 24;
    private const int LiveSeedSearchCount = 384;
    private const int WeightSampleCount = 512;
    private const int WeightSampleSeed = 9440404;
    private const int ItemSelectionSalt = 9440404;

    private static readonly TemplateSpec StartSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_Start.prefab",
            StartTemplateId,
            DreamRoomTag.Standard |
            DreamRoomTag.StartCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec ExitSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_Exit.prefab",
            ExitTemplateId,
            DreamRoomTag.Standard |
            DreamRoomTag.ExitCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec CommonSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_Common.prefab",
            CommonTemplateId,
            DreamRoomTag.Standard,
            randomWeight: 4,
            maximumInstancesPerFloor: 0);

    private static readonly TemplateSpec RareSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_Rare.prefab",
            RareTemplateId,
            DreamRoomTag.Rare,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec CoreStandaloneSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_CoreStandalone.prefab",
            CoreStandaloneTemplateId,
            DreamRoomTag.CoreItemCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec CoreSpecialSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_CoreSpecial.prefab",
            CoreSpecialTemplateId,
            DreamRoomTag.CoreItemCandidate |
            DreamRoomTag.Special,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec SpecialLowSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_SpecialLow.prefab",
            SpecialLowTemplateId,
            DreamRoomTag.Special,
            randomWeight: 1,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec SpecialHighSpec =
        new TemplateSpec(
            RoomRoot + "/Room_R944_SpecialHigh.prefab",
            SpecialHighTemplateId,
            DreamRoomTag.Special,
            randomWeight: 4,
            maximumInstancesPerFloor: 1);

    private static readonly TemplateSpec[] AllTemplateSpecs =
    {
        StartSpec,
        ExitSpec,
        CommonSpec,
        RareSpec,
        CoreStandaloneSpec,
        CoreSpecialSpec,
        SpecialLowSpec,
        SpecialHighSpec
    };

    private static readonly TemplateSpec[] RuntimeTemplateSpecs =
    {
        StartSpec,
        ExitSpec,
        CommonSpec,
        RareSpec,
        CoreSpecialSpec,
        SpecialHighSpec
    };

    private static readonly TemplateSpec[] DedicatedTemplateSpecs =
    {
        StartSpec,
        ExitSpec,
        CommonSpec,
        RareSpec,
        CoreStandaloneSpec,
        SpecialLowSpec,
        SpecialHighSpec
    };

    private static readonly TemplateSpec[] WeightTemplateSpecs =
    {
        SpecialLowSpec,
        SpecialHighSpec
    };

    [MenuItem(
        MenuRoot +
        "Validate Installed Special Test Assets (R9.4.4)",
        false,
        2520)]
    private static void ValidateInstalledAssets()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.4 静态资产校验无法开始",
                errors,
                null);
            return;
        }

        string protectedHashBefore =
            BuildProtectedHashSignature(
                includeTestAssets: true);

        bool sceneDirtyBefore =
            context.Scene.isDirty;

        DreamRoomCatalog runtimeCatalog =
            LoadCatalog(
                RuntimeCatalogPath,
                RuntimeCatalogId,
                "Runtime Catalog",
                errors);

        DreamRoomCatalog dedicatedCatalog =
            LoadCatalog(
                DedicatedCatalogPath,
                DedicatedCatalogId,
                "Dedicated Catalog",
                errors);

        DreamRoomCatalog weightCatalog =
            LoadCatalog(
                WeightCatalogPath,
                WeightCatalogId,
                "Weight Catalog",
                errors);

        int socketOwners = 0;

        if (runtimeCatalog != null)
        {
            AppendCatalogReferenceErrors(
                runtimeCatalog,
                RuntimeTemplateSpecs,
                errors);
        }

        if (dedicatedCatalog != null)
        {
            AppendCatalogReferenceErrors(
                dedicatedCatalog,
                DedicatedTemplateSpecs,
                errors);
        }

        if (weightCatalog != null)
        {
            AppendCatalogReferenceErrors(
                weightCatalog,
                WeightTemplateSpecs,
                errors);
        }

        for (int i = 0;
             i < AllTemplateSpecs.Length;
             i++)
        {
            socketOwners +=
                ValidateStaticPrefab(
                    AllTemplateSpecs[i],
                    errors);
        }

        string protectedHashAfter =
            BuildProtectedHashSignature(
                includeTestAssets: true);

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
                "只读校验改变了 GameScene／既有基线／R9.4.4 测试资产哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 静态资产校验失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 静态测试资产校验通过。\n" +
            "Source=PackageStaticAssets" +
            " | PrefabCreateCalls=0" +
            " | PrefabSaveCalls=0" +
            " | HierarchyAuthority=LoadPrefabContents" +
            " | AssetViewHierarchyTraversal=False\n" +
            "RuntimeCatalog=" + RuntimeCatalogId +
            " | Templates=" + RuntimeTemplateSpecs.Length +
            " | DedicatedCatalog=" + DedicatedCatalogId +
            " | Templates=" + DedicatedTemplateSpecs.Length +
            " | WeightCatalog=" + WeightCatalogId +
            " | Templates=" + WeightTemplateSpecs.Length + "\n" +
            "StaticPrefabs=" + AllTemplateSpecs.Length +
            " | SocketOwners=" + socketOwners +
            "/" +
            (AllTemplateSpecs.Length *
             ExpectedSocketCountPerTemplate) + "\n" +
            "CoreSpecial=" + CoreSpecialTemplateId +
            ":CoreItemCandidate+Special:Weight1:Cap1" +
            " | DedicatedCore=" +
            CoreStandaloneTemplateId +
            ":Weight1:Cap1\n" +
            "SpecialWeightProbe=" +
            SpecialLowTemplateId + ":1," +
            SpecialHighTemplateId + ":4" +
            " | GlobalSpecialCap=1\n" +
            "GameSceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | AssetsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Assets Passed",
            "3 个 Catalog、8 个静态模板、32/32 个 Socket Owner、" +
            "Core + Special 与 4:1 权重配置均已通过。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Special Unique-Room Contract (R9.4.4)",
        false,
        2530)]
    private static void ValidateSpecialContract()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.4 契约校验无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog runtimeCatalog =
            LoadAndValidateRuntimeCatalog(errors);

        DreamRoomCatalog dedicatedCatalog =
            LoadAndValidateDedicatedCatalog(errors);

        DreamRoomCatalog weightCatalog =
            LoadAndValidateWeightCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 契约校验无法开始",
                errors,
                context.Generator);
            return;
        }

        string protectedHashBefore =
            BuildProtectedHashSignature(
                includeTestAssets: true);

        bool sceneDirtyBefore =
            context.Scene.isDirty;

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        WeightMetrics weightMetrics =
            default(WeightMetrics);

        SpecialBatchMetrics dedicatedMetrics =
            default(SpecialBatchMetrics);

        SpecialBatchMetrics sharedMetrics =
            default(SpecialBatchMetrics);

        int grayboxSpecialRooms = -1;
        int grayboxCoreRooms = -1;
        bool grayboxLegacyFallback = false;

        try
        {
            ValidateSpecialWeightDistribution(
                weightCatalog,
                errors,
                out weightMetrics);

            SetGeneratorCatalogTransient(
                context.Generator,
                dedicatedCatalog);

            ValidateSpecialBatch(
                context.Generator,
                expectSharedCoreSpecial: false,
                errors: errors,
                metrics: out dedicatedMetrics);

            SetGeneratorCatalogTransient(
                context.Generator,
                runtimeCatalog);

            ValidateSpecialBatch(
                context.Generator,
                expectSharedCoreSpecial: true,
                errors: errors,
                metrics: out sharedMetrics);

            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);

            DungeonLayout grayboxLayout;
            string grayboxReport;

            if (!context.Generator
                    .TryGenerateSocketCorridorLayout(
                        floorNumber: 1,
                        seed: BaselineSeed,
                        layout: out grayboxLayout,
                        report: out grayboxReport))
            {
                errors.Add(
                    "Graybox 基线无法完成 R9.4.4 回退契约：\n" +
                    grayboxReport);
            }
            else
            {
                List<int> specialIndices =
                    new List<int>();

                List<int> coreIndices =
                    new List<int>();

                DungeonSpecialRoomScopeR944
                    .CollectSpecialRoomIndices(
                        grayboxLayout,
                        specialIndices);

                DungeonCoreItemRoomScopeR943
                    .CollectCandidateRoomIndices(
                        grayboxLayout,
                        coreIndices);

                grayboxSpecialRooms =
                    specialIndices.Count;

                grayboxCoreRooms =
                    coreIndices.Count;

                DungeonSpawnCellResult grayboxResult;
                string grayboxItemFailure;

                grayboxLegacyFallback =
                    TryResolveLegacyItemCell(
                        grayboxLayout,
                        out grayboxResult,
                        out grayboxItemFailure);

                if (grayboxSpecialRooms != 0 ||
                    grayboxCoreRooms != 0)
                {
                    errors.Add(
                        "Graybox 不应包含 Special／CoreItemCandidate。" );
                }

                if (!grayboxLegacyFallback)
                {
                    errors.Add(
                        "Graybox 的旧全图 Item 回退不可用：" +
                        grayboxItemFailure);
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "R9.4.4 契约执行抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);
        }

        string protectedHashAfter =
            BuildProtectedHashSignature(
                includeTestAssets: true);

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
                "契约测试改变了 GameScene／受保护资产哈希。" );
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
                "契约结束后没有恢复 Graybox Catalog 引用。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Special 唯一房契约失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 Special 唯一房／组合角色契约通过。\n" +
            "WeightProbe=SpecialLow:1,SpecialHigh:4" +
            " | Samples=" + WeightSampleCount +
            " | LowSelections=" +
            weightMetrics.LowSelections +
            " | HighSelections=" +
            weightMetrics.HighSelections +
            " | HighLowRatio=" +
            weightMetrics.HighLowRatio.ToString("F2") +
            " | WeightedDeterministic=" +
            weightMetrics.Deterministic + "\n" +
            "DedicatedLayouts=" +
            dedicatedMetrics.SuccessfulLayouts +
            "/" + BatchSeedCount +
            " | CoreStandalonePlacements=" +
            dedicatedMetrics.CoreStandalonePlacements +
            " | SpecialPlacements=" +
            dedicatedMetrics.SpecialPlacements +
            " | SpecialPerFloor=" +
            dedicatedMetrics.MinimumSpecialPerFloor +
            ".." +
            dedicatedMetrics.MaximumSpecialPerFloor +
            " | SpecialAfterCore=" +
            dedicatedMetrics.SpecialAfterCore + "\n" +
            "SharedLayouts=" +
            sharedMetrics.SuccessfulLayouts +
            "/" + BatchSeedCount +
            " | CoreSpecialPlacements=" +
            sharedMetrics.CoreSpecialPlacements +
            " | SpecialHighAfterShared=" +
            sharedMetrics.SpecialHighPlacements +
            " | CoreAndSpecialSameRoom=" +
            sharedMetrics.SharedCoreSpecialLayouts +
            "/" + BatchSeedCount + "\n" +
            "ItemScopeResolved=" +
            sharedMetrics.ItemScopeResolved +
            "/" + BatchSeedCount +
            " | OutsideCoreSpecialSelections=" +
            sharedMetrics.OutsideCoreSpecialSelections +
            " | ScopedFailureRejected=" +
            sharedMetrics.ScopedFailureRejected +
            " | LayoutWideEscape=False\n" +
            "GlobalSpecialCap=1" +
            " | CapBreaches=" +
            (dedicatedMetrics.CapBreaches +
             sharedMetrics.CapBreaches) +
            " | CountPolicy=CommittedPlacementsOnly" +
            " | RandomWeightReused=True" +
            " | TemplateCapReused=True" +
            " | Deterministic=" +
            (dedicatedMetrics.Deterministic &&
             sharedMetrics.Deterministic) + "\n" +
            "GrayboxSpecialRooms=" +
            grayboxSpecialRooms +
            " | GrayboxCoreRooms=" +
            grayboxCoreRooms +
            " | GrayboxLegacyItemFallback=" +
            grayboxLegacyFallback + "\n" +
            "SceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | RuntimeObjectsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Contract Passed",
            "Special 全局唯一、4:1 权重、独立／组合保留槽、" +
            "Core Item 作用域与 Graybox 兼容均已通过。",
            "OK");
    }

    // 保留为无菜单的内部对照实现，不属于 R9.4.4 用户验收入口。
    private static void ValidateCoreItemContractLegacy()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.4 契约校验无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog runtimeCatalog =
            LoadAndValidateRuntimeCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 契约校验无法开始",
                errors,
                context.Generator);
            return;
        }

        string protectedHashBefore =
            BuildProtectedHashSignature(
                includeTestAssets: true);

        bool sceneDirtyBefore =
            context.Scene.isDirty;

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        int successfulLayouts = 0;
        int totalCorePlacements = 0;
        int minimumCorePlacements = int.MaxValue;
        int maximumCorePlacements = int.MinValue;
        int itemScopeResolved = 0;
        int outsideScopeSelections = 0;
        int coreSpecialPlacements = 0;
        int specialPlacements = 0;
        bool deterministic = true;
        bool scopedFailureRejected = false;
        bool grayboxLegacyFallback = false;
        int grayboxCoreCandidates = -1;

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                runtimeCatalog);

            for (int offset = 0;
                 offset < BatchSeedCount;
                 offset++)
            {
                int seed = BatchSeedStart + offset;

                DungeonLayout layout;
                string report;

                if (!context.Generator
                        .TryGenerateSocketCorridorLayout(
                            ContractFloor,
                            seed,
                            out layout,
                            out report))
                {
                    errors.Add(
                        "Seed " + seed +
                        " 无法生成 R9.4.4 完整布局：\n" +
                        report);
                    continue;
                }

                LayoutMetrics metrics;

                ValidateCoreLayout(
                    layout,
                    errors,
                    out metrics,
                    "Seed " + seed);

                successfulLayouts++;
                totalCorePlacements +=
                    metrics.CorePlacements;

                minimumCorePlacements =
                    Mathf.Min(
                        minimumCorePlacements,
                        metrics.CorePlacements);

                maximumCorePlacements =
                    Mathf.Max(
                        maximumCorePlacements,
                        metrics.CorePlacements);

                coreSpecialPlacements +=
                    metrics.CoreSpecialPlacements;

                specialPlacements +=
                    metrics.SpecialPlacements;

                DungeonSpawnCellResult itemResult;
                string itemFailure;

                if (TryResolveScopedItemCell(
                        layout,
                        out itemResult,
                        out itemFailure))
                {
                    itemScopeResolved++;

                    if (!DungeonCoreItemRoomScopeR943
                            .ContainsRoomIndex(
                                metrics.CoreRoomIndices,
                                itemResult.RoomIndex))
                    {
                        outsideScopeSelections++;
                        errors.Add(
                            "Seed " + seed +
                            " 的 Item Resolver 离开了 Core 候选房作用域。" );
                    }
                }
                else
                {
                    errors.Add(
                        "Seed " + seed +
                        " 的 Core 房间内无法解析 Item Cell：" +
                        itemFailure);
                }

                DungeonLayout repeatedLayout;
                string repeatedReport;

                if (!context.Generator
                        .TryGenerateSocketCorridorLayout(
                            ContractFloor,
                            seed,
                            out repeatedLayout,
                            out repeatedReport) ||
                    !string.Equals(
                        BuildLayoutSignature(layout),
                        BuildLayoutSignature(repeatedLayout),
                        StringComparison.Ordinal))
                {
                    deterministic = false;
                    errors.Add(
                        "Seed " + seed +
                        " 的 R9.4.4 布局不具确定性。" );
                }

                if (offset == 0)
                {
                    scopedFailureRejected =
                        ValidateScopedFailureDoesNotEscape(
                            layout,
                            metrics.CoreRoomIndices,
                            errors);
                }
            }

            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);

            DungeonLayout grayboxLayout;
            string grayboxReport;

            if (!context.Generator
                    .TryGenerateSocketCorridorLayout(
                        floorNumber: 1,
                        seed: BaselineSeed,
                        layout: out grayboxLayout,
                        report: out grayboxReport))
            {
                errors.Add(
                    "Graybox 基线无法完成 R9.4.4 回退契约：\n" +
                    grayboxReport);
            }
            else
            {
                List<int> grayboxIndices =
                    new List<int>();

                DungeonCoreItemRoomScopeR943
                    .CollectCandidateRoomIndices(
                        grayboxLayout,
                        grayboxIndices);

                grayboxCoreCandidates =
                    grayboxIndices.Count;

                DungeonSpawnCellResult grayboxResult;
                string grayboxItemFailure;

                grayboxLegacyFallback =
                    TryResolveLegacyItemCell(
                        grayboxLayout,
                        out grayboxResult,
                        out grayboxItemFailure);

                if (grayboxCoreCandidates != 0)
                {
                    errors.Add(
                        "Graybox 不应包含 CoreItemCandidate。" );
                }

                if (!grayboxLegacyFallback)
                {
                    errors.Add(
                        "Graybox 的旧全图 Item 回退不可用：" +
                        grayboxItemFailure);
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "R9.4.4 契约执行抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);
        }

        string protectedHashAfter =
            BuildProtectedHashSignature(
                includeTestAssets: true);

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
                "契约测试改变了 GameScene／资产哈希。" );
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
                "契约结束后没有恢复 Graybox Catalog 引用。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Core Item 契约失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 Core Item 房间／作用域契约通过。\n" +
            "BatchSeeds=" + BatchSeedCount +
            " | Layouts=" + successfulLayouts +
            "/" + BatchSeedCount +
            " | CorePlacements=" + totalCorePlacements +
            " | CorePerFloor=" +
            minimumCorePlacements + ".." +
            maximumCorePlacements + "\n" +
            "CoreTemplate=" + CoreTemplateId +
            " | ReservedAfterStartExit=True" +
            " | WeightedCandidatesReuseRandomWeight=True" +
            " | TemplateCapReused=True\n" +
            "ItemScopeResolved=" + itemScopeResolved +
            "/" + BatchSeedCount +
            " | OutsideScopeSelections=" +
            outsideScopeSelections +
            " | ScopedFailureRejected=" +
            scopedFailureRejected +
            " | LayoutWideEscape=False\n" +
            "CoreSpecialDecoyPlacements=" +
            coreSpecialPlacements +
            " | SpecialPlacements=" + specialPlacements +
            " | SpecialDeferredTo=R9.4.4\n" +
            "Deterministic=" + deterministic +
            " | GrayboxCoreCandidates=" +
            grayboxCoreCandidates +
            " | GrayboxLegacyItemFallback=" +
            grayboxLegacyFallback + "\n" +
            "SceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | RuntimeObjectsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Contract Passed",
            "Core 候选房保留、Item 房间作用域、" +
            "禁止越界回退与 Graybox 兼容均已通过。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Prepare Special Runtime Test (R9.4.4)",
        false,
        2540)]
    private static void PrepareRuntimeTest()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.4 Runtime Test 准备失败",
                errors,
                null);
            return;
        }

        DreamRoomCatalog runtimeCatalog =
            LoadAndValidateRuntimeCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        int liveBaseSeed = 0;

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                runtimeCatalog);

            if (!TryFindLiveBaseSeed(
                    context.Generator,
                    out liveBaseSeed,
                    errors))
            {
                errors.Add(
                    "搜索范围内没有同时通过 Floor 1／Floor 2 的 Live Seed。" );
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
                "R9.4.4 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        SetGeneratorRuntimeSettings(
            context.Generator,
            runtimeCatalog,
            useRandomSeed: false,
            fixedSeed: liveBaseSeed);

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 Special Runtime Test 已准备。\n" +
            "Catalog=" + RuntimeCatalogId +
            " | FixedSeed=" + liveBaseSeed +
            " | Floor1Seed=" + liveBaseSeed +
            " | Floor2Seed=" + (liveBaseSeed + 1) + "\n" +
            "Floor1Preflight=True" +
            " | Floor2Preflight=True" +
            " | Floor2ItemScopePreflight=True" +
            " | CoreSpecialSharedPreflight=True" +
            " | SpecialHighSuppressed=True\n" +
            "SceneSaved=False" +
            " | DiskBaseline=" + GrayboxCatalogId +
            " | RestoreFixedSeed=" + BaselineSeed +
            " | DoNotSaveUntilRestore=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Runtime Test Ready",
            "GameScene 当前只在内存中使用 R9.4.4 Catalog 与测试 Seed。\n\n" +
            "进入 Play Mode，等待 Floor 1 完成后再执行 Advance 菜单。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Advance to Special Core Floor 2 (R9.4.4)",
        false,
        2550)]
    private static void AdvanceToCoreItemFloor()
    {
        List<string> errors =
            new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add(
                "必须在 Play Mode 且 Floor 1 已完整生成后执行。" );
        }

        Scene scene =
            SceneManager.GetActiveScene();

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        if (gameManager == null)
        {
            errors.Add(
                "Play Mode 中找不到 GameManager。" );
        }

        if (generator == null ||
            generator.TemplateFirstRoomCatalog == null ||
            !string.Equals(
                generator.TemplateFirstRoomCatalog.CatalogId,
                RuntimeCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "当前不是 R9.4.4 Runtime Catalog。" );
        }

        if (gameManager != null &&
            gameManager.CurrentFloor != 1)
        {
            errors.Add(
                "Advance 前必须位于 Floor 1，实际为 Floor " +
                gameManager.CurrentFloor + "。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Floor 2 推进失败",
                errors,
                generator);
            return;
        }

        bool committed =
            gameManager.TryPlayerReachedExit();

        ItemManager itemManager =
            FindSceneComponent<ItemManager>(scene);

        if (!committed ||
            gameManager.CurrentFloor != ContractFloor)
        {
            errors.Add(
                "GameManager 没有提交 Floor 2。" );
        }

        if (itemManager == null ||
            itemManager.ActivePickup == null)
        {
            errors.Add(
                "Floor 2 已提交，但第一件保证出现的核心道具没有生成。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Floor 2 推进失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 已推进到 Special Core 道具保证层。\n" +
            "Floor=2" +
            " | Commit=True" +
            " | ActivePickup=" +
            itemManager.ActivePickup.name +
            " | ProgressionUnchanged=True\n" +
            "WaitOneFrameBeforeLiveValidate=True" +
            " | DoNotCollectItemYet=True",
            gameManager);

        EditorUtility.DisplayDialog(
            "R9.4.4 Floor 2 Ready",
            "Floor 2、唯一 Special Core 与核心道具已经生成。\n" +
            "等待一帧后执行 Validate Live；暂时不要拾取道具。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Live Special Contract (R9.4.4)",
        false,
        2560)]
    private static void ValidateLiveCoreItemScope()
    {
        List<string> errors =
            new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add(
                "必须在 Play Mode、Floor 2 已生成且道具尚未拾取时执行。" );
        }

        Scene scene =
            SceneManager.GetActiveScene();

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

        ItemManager itemManager =
            FindSceneComponent<ItemManager>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

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

        if (itemManager == null)
        {
            errors.Add(
                "Play Mode 中找不到 ItemManager。" );
        }

        if (renderer == null)
        {
            errors.Add(
                "Play Mode 中找不到 DungeonRenderer。" );
        }

        if (generator != null &&
            (generator.TemplateFirstRoomCatalog == null ||
             !string.Equals(
                 generator.TemplateFirstRoomCatalog.CatalogId,
                 RuntimeCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "当前 Catalog 不是 " + RuntimeCatalogId + "。" );
        }

        if (gameManager != null &&
            gameManager.CurrentFloor != ContractFloor)
        {
            errors.Add(
                "当前应为 Floor 2，实际为 Floor " +
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

        LayoutMetrics metrics =
            default(LayoutMetrics);

        if (layout != null)
        {
            ValidateSpecialLayout(
                layout,
                true,
                errors,
                out metrics,
                "Live Floor 2");
        }

        GameObject pickup =
            itemManager == null
                ? null
                : itemManager.ActivePickup;

        if (pickup == null)
        {
            errors.Add(
                "核心道具不存在；若已拾取，请退出 Play Mode 后重做 Prepare。" );
        }

        Vector2Int itemCell = default(Vector2Int);
        float itemCellDistance = float.MaxValue;
        bool itemCellResolved = false;
        int itemRoomIndex = -1;
        bool itemInsideCoreRoom = false;
        bool itemInsideSpecialRoom = false;

        if (pickup != null &&
            layout != null &&
            renderer != null)
        {
            itemCellResolved =
                TryFindNearestFloorCell(
                    layout,
                    renderer,
                    pickup.transform.position,
                    out itemCell,
                    out itemCellDistance);

            if (!itemCellResolved ||
                itemCellDistance > 0.05f)
            {
                errors.Add(
                    "无法把核心道具世界位置还原为准确 FloorCell。" );
            }
            else
            {
                itemRoomIndex =
                    FindWalkableRoomIndex(
                        layout,
                        itemCell);

                itemInsideCoreRoom =
                    DungeonCoreItemRoomScopeR943
                        .ContainsRoomIndex(
                            metrics.CoreRoomIndices,
                            itemRoomIndex);

                itemInsideSpecialRoom =
                    DungeonSpecialRoomScopeR944
                        .ContainsRoomIndex(
                            metrics.SpecialRoomIndices,
                            itemRoomIndex);

                if (!itemInsideCoreRoom ||
                    !itemInsideSpecialRoom)
                {
                    errors.Add(
                        "核心道具生成在 Room " +
                        itemRoomIndex +
                        "，不属于唯一 CoreItemCandidate + Special 房。" );
                }
            }
        }

        GameObject generatedRoot =
            GameObject.Find(
                "GeneratedDungeon_Floor_2");

        GameObject staleRoot =
            GameObject.Find(
                "GeneratedDungeon_Floor_1");

        if (generatedRoot == null)
        {
            errors.Add(
                "找不到 GeneratedDungeon_Floor_2。" );
        }

        if (staleRoot != null)
        {
            errors.Add(
                "Floor 1 的 GeneratedDungeon 尚未在帧末销毁；" +
                "请等待一帧后再 Validate。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Live Special Contract 失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 真实运行时 Special 唯一房／Core Item 作用域通过。\n" +
            "Catalog=" + RuntimeCatalogId +
            " | Floor=2" +
            " | Rooms=" + metrics.RoomCount +
            "/" + ExpectedRoomCount +
            " | GeneratedRoot=GeneratedDungeon_Floor_2\n" +
            "CoreSpecialTemplate=" + CoreSpecialTemplateId +
            " | CorePlacements=" +
            metrics.CorePlacements +
            " | CoreRoomIndex=" +
            metrics.CoreRoomIndices[0] +
            " | SpecialPlacements=" +
            metrics.SpecialPlacements +
            " | SpecialRoomIndex=" +
            metrics.SpecialRoomIndices[0] +
            " | CoreAndSpecialSameRoom=True" +
            " | SpecialHighPlacements=0\n" +
            "ActivePickup=" + pickup.name +
            " | ItemCell=" + itemCell +
            " | ItemRoomIndex=" + itemRoomIndex +
            " | ItemInsideCoreRoom=" +
            itemInsideCoreRoom +
            " | ItemInsideSpecialRoom=" +
            itemInsideSpecialRoom + "\n" +
            "ItemScope=CoreItemCandidate+SpecialRoom" +
            " | GlobalSpecialCap=1" +
            " | CapBreaches=0" +
            " | LayoutWideFallbackAllowed=False" +
            " | RuntimeObjectsModified=False",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Live Special Passed",
            "Floor 2 已确认只有一个 Special；它同时承担 Core 角色，" +
            "核心道具也位于该房内。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Restore and Save Graybox after R9.4.4",
        false,
        2570)]
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
                "R9.4.4 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        SetGeneratorRuntimeSettings(
            generator,
            grayboxCatalog,
            useRandomSeed: false,
            fixedSeed: BaselineSeed);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            errors.Add(
                "Graybox 已写回内存，但 GameScene 保存失败。" );
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
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.4 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomSpecialRoomAuditR944] " +
            "R9.4.4 Graybox 基线已恢复并保存。\n" +
            "Catalog=" + GrayboxCatalogId +
            " | SceneSaved=True" +
            " | RenderMode=HybridPrefabRooms" +
            " | FixedSeed=" + BaselineSeed + "\n" +
            "R9.4.1AssetsRetained=True" +
            " | R9.4.2AssetsRetained=True" +
            " | R9.4.3AssetsRetained=True" +
            " | R9.4.4TestAssetsRetained=True" +
            " | RuntimePatchRetained=True",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.4 Graybox Restored",
            "GameScene 已恢复并保存为 Graybox_R3 / Fixed Seed 12345。",
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

        if (catalog != null)
        {
            AppendCatalogReferenceErrors(
                catalog,
                RuntimeTemplateSpecs,
                errors);
        }

        return catalog;
    }

    private static DreamRoomCatalog
        LoadAndValidateDedicatedCatalog(
            List<string> errors)
    {
        DreamRoomCatalog catalog =
            LoadCatalog(
                DedicatedCatalogPath,
                DedicatedCatalogId,
                "Dedicated Catalog",
                errors);

        if (catalog != null)
        {
            AppendCatalogReferenceErrors(
                catalog,
                DedicatedTemplateSpecs,
                errors);
        }

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
                "Weight Catalog",
                errors);

        if (catalog != null)
        {
            AppendCatalogReferenceErrors(
                catalog,
                WeightTemplateSpecs,
                errors);
        }

        return catalog;
    }

    private static DreamRoomCatalog LoadCatalog(
        string path,
        string expectedCatalogId,
        string label,
        List<string> errors)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomCatalog>(path);

        if (catalog == null)
        {
            errors.Add(
                label + " 不存在：" + path);
            return null;
        }

        if (!string.Equals(
                catalog.CatalogId,
                expectedCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                label + " CatalogId 应为 " +
                expectedCatalogId +
                "，实际为 " + catalog.CatalogId + "。" );
        }

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0;
             i < catalogErrors.Count;
             i++)
        {
            errors.Add(
                label + "：" + catalogErrors[i]);
        }

        return catalog;
    }

    private static void AppendCatalogReferenceErrors(
        DreamRoomCatalog catalog,
        TemplateSpec[] specs,
        List<string> errors)
    {
        if (catalog == null)
        {
            return;
        }

        if (catalog.Count != specs.Length)
        {
            errors.Add(
                "Catalog 模板数应为 " +
                specs.Length +
                "，实际为 " + catalog.Count + "。" );
        }

        HashSet<string> expectedIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < specs.Length; i++)
        {
            expectedIds.Add(specs[i].TemplateId);
        }

        for (int i = 0;
             i < catalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                catalog.RoomTemplates[i];

            if (template == null)
            {
                errors.Add(
                    "Catalog 第 " + i +
                    " 项为空。" );
                continue;
            }

            if (!expectedIds.Remove(template.TemplateId))
            {
                errors.Add(
                    "Catalog 含有未预期或重复模板：" +
                    template.TemplateId + "。" );
            }
        }

        foreach (string missingId in expectedIds)
        {
            errors.Add(
                "Runtime Catalog 缺少模板：" +
                missingId + "。" );
        }

        for (int i = 0; i < specs.Length; i++)
        {
            DreamRoomTemplate template;

            if (!catalog.TryGetTemplate(
                    specs[i].TemplateId,
                    out template))
            {
                continue;
            }

            string actualPath =
                AssetDatabase.GetAssetPath(template);

            if (!string.Equals(
                    actualPath,
                    specs[i].PrefabPath,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    specs[i].TemplateId +
                    " 引用路径错误：" + actualPath);
            }
        }
    }

    private static int ValidateStaticPrefab(
        TemplateSpec spec,
        List<string> errors)
    {
        DreamRoomTemplate assetTemplate =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomTemplate>(
                    spec.PrefabPath);

        if (assetTemplate == null)
        {
            errors.Add(
                "Prefab 缺失或主组件不可读：" +
                spec.PrefabPath);
            return 0;
        }

        if (!string.Equals(
                assetTemplate.TemplateId,
                spec.TemplateId,
                StringComparison.Ordinal))
        {
            errors.Add(
                spec.TemplateId +
                " 的 Asset 视图 TemplateId 不匹配。" );
        }

        if (assetTemplate.RoomTags != spec.RoomTags ||
            assetTemplate.RandomWeight != spec.RandomWeight ||
            assetTemplate.MaximumInstancesPerFloor !=
            spec.MaximumInstancesPerFloor)
        {
            errors.Add(
                spec.TemplateId +
                " 的 Tags／Weight／Cap 不符合测试契约。" );
        }

        GameObject contentsRoot = null;
        int ownedSocketCount = 0;

        try
        {
            contentsRoot =
                PrefabUtility.LoadPrefabContents(
                    spec.PrefabPath);

            DreamRoomTemplate loadedTemplate =
                contentsRoot == null
                    ? null
                    : contentsRoot.GetComponent<
                        DreamRoomTemplate>();

            if (loadedTemplate == null)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的完整 Prefab Contents 根节点缺少 DreamRoomTemplate。" );
                return 0;
            }

            List<string> templateErrors =
                loadedTemplate.GetValidationErrors();

            for (int i = 0;
                 i < templateErrors.Count;
                 i++)
            {
                errors.Add(
                    spec.TemplateId +
                    "：" + templateErrors[i]);
            }

            if (loadedTemplate.DoorSockets == null ||
                loadedTemplate.DoorSockets.Count !=
                ExpectedSocketCountPerTemplate)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的完整 Prefab Contents 应有 4 个 Socket。" );
            }

            if (loadedTemplate.DoorSockets != null)
            {
                for (int i = 0;
                     i < loadedTemplate.DoorSockets.Count;
                     i++)
                {
                    DreamRoomDoorSocket socket =
                        loadedTemplate.DoorSockets[i];

                    DreamRoomTemplate owner =
                        socket == null
                            ? null
                            : socket.GetComponentInParent<
                                DreamRoomTemplate>();

                    if (socket != null &&
                        owner == loadedTemplate)
                    {
                        ownedSocketCount++;
                    }
                    else
                    {
                        errors.Add(
                            spec.TemplateId +
                            " 的 Socket " + i +
                            " 不归完整 Prefab 根模板所有。" );
                    }
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                spec.TemplateId +
                " 的 LoadPrefabContents 校验抛出异常：\n" +
                exception);
        }
        finally
        {
            if (contentsRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    contentsRoot);
            }
        }

        return ownedSocketCount;
    }

    private static void ValidateSpecialWeightDistribution(
        DreamRoomCatalog catalog,
        List<string> errors,
        out WeightMetrics metrics)
    {
        WeightMetrics first =
            RunSpecialWeightSample(
                catalog,
                WeightSampleSeed,
                errors);

        WeightMetrics repeated =
            RunSpecialWeightSample(
                catalog,
                WeightSampleSeed,
                errors);

        first.Deterministic =
            first.LowSelections ==
                repeated.LowSelections &&
            first.HighSelections ==
                repeated.HighSelections;

        first.HighLowRatio =
            first.LowSelections == 0
                ? double.PositiveInfinity
                : (double)first.HighSelections /
                  first.LowSelections;

        if (!first.Deterministic)
        {
            errors.Add(
                "相同 Seed 的 Special 权重采样结果不一致。" );
        }

        if (first.LowSelections +
            first.HighSelections !=
            WeightSampleCount)
        {
            errors.Add(
                "Special 权重采样成功次数不是 " +
                WeightSampleCount + "。" );
        }

        if (first.LowSelections <= 0 ||
            first.HighSelections <= 0)
        {
            errors.Add(
                "Special 1:4 权重样本必须同时选中过两个模板。" );
        }

        if (first.HighLowRatio < 2.5d ||
            first.HighLowRatio > 5.5d)
        {
            errors.Add(
                "SpecialHigh:SpecialLow 的实际选择比例没有体现 4:1；" +
                "当前为 " +
                first.HighLowRatio.ToString("F2") +
                ":1。" );
        }

        metrics = first;
    }

    private static WeightMetrics RunSpecialWeightSample(
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
                    ContractFloor,
                    null,
                    random,
                    out selected) ||
                selected == null)
            {
                errors.Add(
                    "Special 权重采样在第 " + i +
                    " 次没有返回模板。" );
                break;
            }

            if (string.Equals(
                    selected.TemplateId,
                    SpecialLowTemplateId,
                    StringComparison.Ordinal))
            {
                metrics.LowSelections++;
            }
            else if (string.Equals(
                         selected.TemplateId,
                         SpecialHighTemplateId,
                         StringComparison.Ordinal))
            {
                metrics.HighSelections++;
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

    private static void ValidateSpecialBatch(
        DungeonGenerator generator,
        bool expectSharedCoreSpecial,
        List<string> errors,
        out SpecialBatchMetrics metrics)
    {
        metrics = default(SpecialBatchMetrics);
        metrics.MinimumSpecialPerFloor = int.MaxValue;
        metrics.MaximumSpecialPerFloor = int.MinValue;
        metrics.Deterministic = true;
        metrics.SpecialAfterCore = true;

        for (int offset = 0;
             offset < BatchSeedCount;
             offset++)
        {
            int seed = BatchSeedStart + offset;

            DungeonLayout layout;
            string report;

            if (!generator.TryGenerateSocketCorridorLayout(
                    ContractFloor,
                    seed,
                    out layout,
                    out report))
            {
                errors.Add(
                    (expectSharedCoreSpecial
                        ? "Shared"
                        : "Dedicated") +
                    " Seed " + seed +
                    " 无法生成完整布局：\n" +
                    report);
                continue;
            }

            LayoutMetrics layoutMetrics;

            ValidateSpecialLayout(
                layout,
                expectSharedCoreSpecial,
                errors,
                out layoutMetrics,
                (expectSharedCoreSpecial
                    ? "Shared Seed "
                    : "Dedicated Seed ") +
                seed);

            metrics.SuccessfulLayouts++;
            metrics.SpecialPlacements +=
                layoutMetrics.SpecialPlacements;
            metrics.CoreStandalonePlacements +=
                layoutMetrics.CoreStandalonePlacements;
            metrics.CoreSpecialPlacements +=
                layoutMetrics.CoreSpecialPlacements;
            metrics.SpecialLowPlacements +=
                layoutMetrics.SpecialLowPlacements;
            metrics.SpecialHighPlacements +=
                layoutMetrics.SpecialHighPlacements;
            metrics.CapBreaches +=
                layoutMetrics.SpecialPlacements > 1
                    ? 1
                    : 0;

            metrics.MinimumSpecialPerFloor =
                Mathf.Min(
                    metrics.MinimumSpecialPerFloor,
                    layoutMetrics.SpecialPlacements);

            metrics.MaximumSpecialPerFloor =
                Mathf.Max(
                    metrics.MaximumSpecialPerFloor,
                    layoutMetrics.SpecialPlacements);

            if (expectSharedCoreSpecial)
            {
                bool sameRoom =
                    layoutMetrics.CoreRoomIndices.Count == 1 &&
                    layoutMetrics.SpecialRoomIndices.Count == 1 &&
                    layoutMetrics.CoreRoomIndices[0] ==
                    layoutMetrics.SpecialRoomIndices[0];

                if (sameRoom)
                {
                    metrics.SharedCoreSpecialLayouts++;
                }

                DungeonSpawnCellResult itemResult;
                string itemFailure;

                if (TryResolveScopedItemCell(
                        layout,
                        out itemResult,
                        out itemFailure))
                {
                    metrics.ItemScopeResolved++;

                    bool insideCore =
                        DungeonCoreItemRoomScopeR943
                            .ContainsRoomIndex(
                                layoutMetrics.CoreRoomIndices,
                                itemResult.RoomIndex);

                    bool insideSpecial =
                        DungeonSpecialRoomScopeR944
                            .ContainsRoomIndex(
                                layoutMetrics.SpecialRoomIndices,
                                itemResult.RoomIndex);

                    if (!insideCore || !insideSpecial)
                    {
                        metrics.OutsideCoreSpecialSelections++;
                        errors.Add(
                            "Shared Seed " + seed +
                            " 的 Item Resolver 离开了 Core + Special 房。" );
                    }
                }
                else
                {
                    errors.Add(
                        "Shared Seed " + seed +
                        " 的组合房内无法解析 Item Cell：" +
                        itemFailure);
                }

                if (offset == 0)
                {
                    metrics.ScopedFailureRejected =
                        ValidateScopedFailureDoesNotEscape(
                            layout,
                            layoutMetrics.CoreRoomIndices,
                            errors);
                }
            }
            else
            {
                bool ordered =
                    layoutMetrics.CoreRoomIndices.Count == 1 &&
                    layoutMetrics.SpecialRoomIndices.Count == 1 &&
                    layoutMetrics.SpecialRoomIndices[0] >
                    layoutMetrics.CoreRoomIndices[0];

                metrics.SpecialAfterCore =
                    metrics.SpecialAfterCore && ordered;
            }

            DungeonLayout repeatedLayout;
            string repeatedReport;

            if (!generator.TryGenerateSocketCorridorLayout(
                    ContractFloor,
                    seed,
                    out repeatedLayout,
                    out repeatedReport) ||
                !string.Equals(
                    BuildLayoutSignature(layout),
                    BuildLayoutSignature(repeatedLayout),
                    StringComparison.Ordinal))
            {
                metrics.Deterministic = false;
                errors.Add(
                    "Seed " + seed +
                    " 的 R9.4.4 布局不具确定性。" );
            }
        }

        if (metrics.SuccessfulLayouts == 0)
        {
            metrics.MinimumSpecialPerFloor = 0;
            metrics.MaximumSpecialPerFloor = 0;
        }
    }

    private static void ValidateSpecialLayout(
        DungeonLayout layout,
        bool expectSharedCoreSpecial,
        List<string> errors,
        out LayoutMetrics metrics,
        string label)
    {
        metrics = default(LayoutMetrics);

        if (layout == null)
        {
            errors.Add(label + " Layout 为空。" );
            return;
        }

        metrics.RoomCount =
            layout.RoomPlacements.Count;

        metrics.CoreRoomIndices =
            new List<int>();

        metrics.SpecialRoomIndices =
            new List<int>();

        DungeonCoreItemRoomScopeR943
            .CollectCandidateRoomIndices(
                layout,
                metrics.CoreRoomIndices);

        DungeonSpecialRoomScopeR944
            .CollectSpecialRoomIndices(
                layout,
                metrics.SpecialRoomIndices);

        metrics.CorePlacements =
            metrics.CoreRoomIndices.Count;

        metrics.SpecialPlacements =
            metrics.SpecialRoomIndices.Count;

        metrics.CoreStandalonePlacements =
            CountTemplatePlacements(
                layout,
                CoreStandaloneTemplateId);

        metrics.CoreSpecialPlacements =
            CountTemplatePlacements(
                layout,
                CoreSpecialTemplateId);

        metrics.SpecialLowPlacements =
            CountTemplatePlacements(
                layout,
                SpecialLowTemplateId);

        metrics.SpecialHighPlacements =
            CountTemplatePlacements(
                layout,
                SpecialHighTemplateId);

        metrics.RarePlacements =
            CountTaggedPlacements(
                layout,
                DreamRoomTag.Rare);

        metrics.StartRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.StartCell);

        metrics.ExitRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.ExitCell);

        DreamRoomTemplate startTemplate =
            GetTemplateAt(
                layout,
                metrics.StartRoomIndex);

        DreamRoomTemplate exitTemplate =
            GetTemplateAt(
                layout,
                metrics.ExitRoomIndex);

        metrics.StartTemplateId =
            startTemplate == null
                ? "<missing>"
                : startTemplate.TemplateId;

        metrics.ExitTemplateId =
            exitTemplate == null
                ? "<missing>"
                : exitTemplate.TemplateId;

        if (metrics.RoomCount != ExpectedRoomCount)
        {
            errors.Add(
                label + " 房间数应为 7，实际为 " +
                metrics.RoomCount + "。" );
        }

        if (metrics.CorePlacements != 1)
        {
            errors.Add(
                label +
                " 应含且仅含一个 CoreItemCandidate，实际为 " +
                metrics.CorePlacements + "。" );
        }

        if (metrics.SpecialPlacements != 1)
        {
            errors.Add(
                label +
                " 应含且仅含一个 Special，实际为 " +
                metrics.SpecialPlacements + "。" );
        }

        if (expectSharedCoreSpecial)
        {
            bool shared =
                metrics.CoreSpecialPlacements == 1 &&
                metrics.CoreStandalonePlacements == 0 &&
                metrics.SpecialHighPlacements == 0 &&
                metrics.CoreRoomIndices.Count == 1 &&
                metrics.SpecialRoomIndices.Count == 1 &&
                metrics.CoreRoomIndices[0] ==
                metrics.SpecialRoomIndices[0];

            if (!shared)
            {
                errors.Add(
                    label +
                    " 没有由唯一 R944_CoreSpecial 同时满足 Core／Special，" +
                    "或全局上限后仍放置了 SpecialHigh。" );
            }
        }
        else
        {
            bool dedicated =
                metrics.CoreStandalonePlacements == 1 &&
                metrics.CoreSpecialPlacements == 0 &&
                metrics.SpecialLowPlacements +
                metrics.SpecialHighPlacements == 1 &&
                metrics.CoreRoomIndices.Count == 1 &&
                metrics.SpecialRoomIndices.Count == 1 &&
                metrics.SpecialRoomIndices[0] >
                metrics.CoreRoomIndices[0];

            if (!dedicated)
            {
                errors.Add(
                    label +
                    " 没有先放置独立 Core，再放置唯一 Special。" );
            }
        }

        if (metrics.RarePlacements > 1)
        {
            errors.Add(
                label +
                " 破坏了 R9.4.2 Rare 单层上限。" );
        }

        if (startTemplate == null ||
            !startTemplate.HasTag(
                DreamRoomTag.StartCandidate) ||
            startTemplate.HasTag(DreamRoomTag.Special))
        {
            errors.Add(
                label +
                " Start 房未命中安全的非 Special StartCandidate。" );
        }

        if (exitTemplate == null ||
            !exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate) ||
            exitTemplate.HasTag(DreamRoomTag.Special))
        {
            errors.Add(
                label +
                " Exit 房未命中安全的非 Special ExitCandidate。" );
        }

        if (metrics.StartRoomIndex < 0 ||
            metrics.ExitRoomIndex < 0 ||
            metrics.StartRoomIndex ==
            metrics.ExitRoomIndex)
        {
            errors.Add(
                label + " Start／Exit 房必须存在且不同。" );
        }
    }

    private static void ValidateCoreLayout(
        DungeonLayout layout,
        List<string> errors,
        out LayoutMetrics metrics,
        string label)
    {
        metrics = default(LayoutMetrics);

        if (layout == null)
        {
            errors.Add(label + " Layout 为空。" );
            return;
        }

        metrics.RoomCount =
            layout.RoomPlacements.Count;

        metrics.CoreRoomIndices =
            new List<int>();

        DungeonCoreItemRoomScopeR943
            .CollectCandidateRoomIndices(
                layout,
                metrics.CoreRoomIndices);

        metrics.CorePlacements =
            metrics.CoreRoomIndices.Count;

        metrics.CoreSpecialPlacements =
            CountTemplatePlacements(
                layout,
                CoreSpecialDecoyTemplateId);

        metrics.SpecialPlacements =
            CountTaggedPlacements(
                layout,
                DreamRoomTag.Special);

        metrics.RarePlacements =
            CountTaggedPlacements(
                layout,
                DreamRoomTag.Rare);

        metrics.StartRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.StartCell);

        metrics.ExitRoomIndex =
            FindWalkableRoomIndex(
                layout,
                layout.ExitCell);

        DreamRoomTemplate startTemplate =
            GetTemplateAt(
                layout,
                metrics.StartRoomIndex);

        DreamRoomTemplate exitTemplate =
            GetTemplateAt(
                layout,
                metrics.ExitRoomIndex);

        metrics.StartTemplateId =
            startTemplate == null
                ? "<missing>"
                : startTemplate.TemplateId;

        metrics.ExitTemplateId =
            exitTemplate == null
                ? "<missing>"
                : exitTemplate.TemplateId;

        if (metrics.RoomCount != ExpectedRoomCount)
        {
            errors.Add(
                label + " 房间数应为 7，实际为 " +
                metrics.RoomCount + "。" );
        }

        if (metrics.CorePlacements != 1)
        {
            errors.Add(
                label +
                " 应含且仅含一个已启用 CoreItemCandidate，实际为 " +
                metrics.CorePlacements + "。" );
        }
        else
        {
            DreamRoomTemplate coreTemplate =
                GetTemplateAt(
                    layout,
                    metrics.CoreRoomIndices[0]);

            if (coreTemplate == null ||
                !string.Equals(
                    coreTemplate.TemplateId,
                    CoreTemplateId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    label +
                    " 的 Core 保留槽没有选择 " +
                    CoreTemplateId + "。" );
            }
        }

        if (metrics.CoreSpecialPlacements != 0 ||
            metrics.SpecialPlacements != 0)
        {
            errors.Add(
                label +
                " 提前放置了 R9.4.4 Special／CoreSpecial 诱饵。" );
        }

        if (metrics.RarePlacements > 1)
        {
            errors.Add(
                label +
                " 破坏了 R9.4.2 Rare 单层上限。" );
        }

        if (startTemplate == null ||
            !startTemplate.HasTag(
                DreamRoomTag.StartCandidate))
        {
            errors.Add(
                label + " Start 房未命中 StartCandidate。" );
        }

        if (exitTemplate == null ||
            !exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate))
        {
            errors.Add(
                label + " Exit 房未命中 ExitCandidate。" );
        }

        if (metrics.StartRoomIndex < 0 ||
            metrics.ExitRoomIndex < 0 ||
            metrics.StartRoomIndex ==
            metrics.ExitRoomIndex)
        {
            errors.Add(
                label + " Start／Exit 房必须存在且不同。" );
        }
    }

    private static bool TryResolveScopedItemCell(
        DungeonLayout layout,
        out DungeonSpawnCellResult result,
        out string failureReason)
    {
        List<int> coreRoomIndices =
            new List<int>();

        DungeonCoreItemRoomScopeR943
            .CollectCandidateRoomIndices(
                layout,
                coreRoomIndices);

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Item,
                coreRoomIndices,
                ItemSelectionSalt,
                reservedCells: new[]
                {
                    layout.StartCell,
                    layout.ExitCell
                },
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 4,
                minimumDistanceFromExit: 2,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        return DungeonSpawnCellResolver.TryResolve(
            request,
            out result,
            out failureReason);
    }

    private static bool TryResolveLegacyItemCell(
        DungeonLayout layout,
        out DungeonSpawnCellResult result,
        out string failureReason)
    {
        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Item,
                allowedRoomIndices: null,
                selectionSalt: ItemSelectionSalt,
                reservedCells: new[]
                {
                    layout.StartCell,
                    layout.ExitCell
                },
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 4,
                minimumDistanceFromExit: 2,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        return DungeonSpawnCellResolver.TryResolve(
            request,
            out result,
            out failureReason);
    }

    private static bool ValidateScopedFailureDoesNotEscape(
        DungeonLayout layout,
        IReadOnlyList<int> coreRoomIndices,
        List<string> errors)
    {
        HashSet<Vector2Int> reservations =
            new HashSet<Vector2Int>();

        List<Vector2Int> cells =
            new List<Vector2Int>();

        for (int i = 0;
             i < coreRoomIndices.Count;
             i++)
        {
            int roomIndex = coreRoomIndices[i];

            if (roomIndex < 0 ||
                roomIndex >= layout.RoomPlacements.Count ||
                layout.RoomPlacements[roomIndex] == null)
            {
                continue;
            }

            layout.RoomPlacements[roomIndex]
                .GetWalkableGlobalCells(cells);

            reservations.UnionWith(cells);
        }

        reservations.Add(layout.StartCell);
        reservations.Add(layout.ExitCell);

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Item,
                coreRoomIndices,
                ItemSelectionSalt,
                reservedCells: reservations,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 0,
                minimumDistanceFromExit: 0,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        DungeonSpawnCellResult unexpectedResult;
        string failureReason;

        bool resolved =
            DungeonSpawnCellResolver.TryResolve(
                request,
                out unexpectedResult,
                out failureReason);

        if (resolved)
        {
            errors.Add(
                "受控失败中 Item Resolver 离开了 Core 房作用域，" +
                "错误返回 " + unexpectedResult + "。" );
            return false;
        }

        return true;
    }

    private static bool TryFindLiveBaseSeed(
        DungeonGenerator generator,
        out int liveBaseSeed,
        List<string> errors)
    {
        liveBaseSeed = 0;

        for (int offset = 0;
             offset < LiveSeedSearchCount;
             offset++)
        {
            int candidateBaseSeed =
                BatchSeedStart + offset;

            DungeonLayout floorOne;
            string floorOneReport;

            if (!generator.TryGenerateSocketCorridorLayout(
                    floorNumber: 1,
                    seed: candidateBaseSeed,
                    layout: out floorOne,
                    report: out floorOneReport))
            {
                continue;
            }

            DungeonLayout floorTwo;
            string floorTwoReport;

            if (!generator.TryGenerateSocketCorridorLayout(
                    floorNumber: ContractFloor,
                    seed: candidateBaseSeed + 1,
                    layout: out floorTwo,
                    report: out floorTwoReport))
            {
                continue;
            }

            List<string> localErrors =
                new List<string>();

            LayoutMetrics floorOneMetrics;
            LayoutMetrics floorTwoMetrics;

            ValidateSpecialLayout(
                floorOne,
                true,
                localErrors,
                out floorOneMetrics,
                "Live Floor 1 Preflight");

            ValidateSpecialLayout(
                floorTwo,
                true,
                localErrors,
                out floorTwoMetrics,
                "Live Floor 2 Preflight");

            DungeonSpawnCellResult itemResult;
            string itemFailure;

            if (!TryResolveScopedItemCell(
                    floorTwo,
                    out itemResult,
                    out itemFailure))
            {
                localErrors.Add(
                    "Floor 2 Item Scope Preflight：" +
                    itemFailure);
            }
            else if (!DungeonCoreItemRoomScopeR943
                         .ContainsRoomIndex(
                             floorTwoMetrics.CoreRoomIndices,
                             itemResult.RoomIndex) ||
                     !DungeonSpecialRoomScopeR944
                         .ContainsRoomIndex(
                             floorTwoMetrics.SpecialRoomIndices,
                             itemResult.RoomIndex))
            {
                localErrors.Add(
                    "Floor 2 Item Scope Preflight 离开 Core + Special 房。" );
            }

            if (localErrors.Count == 0)
            {
                liveBaseSeed = candidateBaseSeed;
                return true;
            }
        }

        return false;
    }

    private static int CountTemplatePlacements(
        DungeonLayout layout,
        string templateId)
    {
        if (layout == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                GetTemplateAt(layout, i);

            if (template != null &&
                string.Equals(
                    template.TemplateId,
                    templateId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountTaggedPlacements(
        DungeonLayout layout,
        DreamRoomTag tag)
    {
        if (layout == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                GetTemplateAt(layout, i);

            if (template != null &&
                template.HasTag(tag))
            {
                count++;
            }
        }

        return count;
    }

    private static DreamRoomTemplate GetTemplateAt(
        DungeonLayout layout,
        int roomIndex)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count ||
            layout.RoomPlacements[roomIndex] == null)
        {
            return null;
        }

        return layout.RoomPlacements[roomIndex].Template;
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

    private static bool TryFindNearestFloorCell(
        DungeonLayout layout,
        DungeonRenderer renderer,
        Vector3 worldPosition,
        out Vector2Int nearestCell,
        out float nearestDistance)
    {
        nearestCell = default(Vector2Int);
        nearestDistance = float.MaxValue;

        if (layout == null ||
            renderer == null ||
            layout.FloorCells == null ||
            layout.FloorCells.Count == 0)
        {
            return false;
        }

        foreach (Vector2Int cell in layout.FloorCells)
        {
            Vector3 cellWorld =
                renderer.CellToWorld(cell);

            float distance =
                Vector2.Distance(
                    new Vector2(
                        worldPosition.x,
                        worldPosition.y),
                    new Vector2(
                        cellWorld.x,
                        cellWorld.y));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCell = cell;
            }
        }

        return nearestDistance < float.MaxValue;
    }

    private static string BuildLayoutSignature(
        DungeonLayout layout)
    {
        if (layout == null)
        {
            return "<null>";
        }

        StringBuilder builder =
            new StringBuilder();

        builder.Append("Seed=");
        builder.Append(layout.Seed);
        builder.Append("|Start=");
        builder.Append(layout.StartCell);
        builder.Append("|Exit=");
        builder.Append(layout.ExitCell);

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            builder.Append("|R");
            builder.Append(i);
            builder.Append('=');

            if (placement == null ||
                placement.Template == null)
            {
                builder.Append("<null>");
                continue;
            }

            builder.Append(
                placement.Template.TemplateId);
            builder.Append('@');
            builder.Append(placement.MinimumCell);
            builder.Append('#');
            builder.Append(
                placement.ClockwiseQuarterTurns);
        }

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            builder.Append("|C");
            builder.Append(i);
            builder.Append('=');

            if (connection == null)
            {
                builder.Append("<null>");
                continue;
            }

            builder.Append(connection.RoomAIndex);
            builder.Append(':');
            builder.Append(connection.SocketAId);
            builder.Append('>');
            builder.Append(connection.RoomBIndex);
            builder.Append(':');
            builder.Append(connection.SocketBId);
        }

        return builder.ToString();
    }

    private static bool TryGetCleanGrayboxSceneContext(
        out SceneContext context,
        out List<string> errors)
    {
        context = default(SceneContext);
        errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add(
                "必须退出 Play Mode。" );
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add(
                "必须退出 Prefab Mode。" );
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

        if (scene.IsValid() && scene.isDirty)
        {
            errors.Add(
                "GameScene 开始前必须没有星号。" );
        }

        DungeonGenerator generator =
            scene.IsValid()
                ? FindSceneComponent<DungeonGenerator>(
                    scene)
                : null;

        DungeonRenderer renderer =
            scene.IsValid()
                ? FindSceneComponent<DungeonRenderer>(
                    scene)
                : null;

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
                "DungeonRenderer Render Mode 必须为 Hybrid Prefab Rooms。" );
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

        catalogProperty.objectReferenceValue = catalog;
        randomProperty.boolValue = useRandomSeed;
        seedProperty.intValue = fixedSeed;

        serializedGenerator
            .ApplyModifiedPropertiesWithoutUndo();
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

        for (int i = 0; i < roots.Length; i++)
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

    private static string BuildProtectedHashSignature(
        bool includeTestAssets)
    {
        List<string> paths =
            new List<string>();

        paths.Add(GameScenePath);
        AppendAssetPathsUnderRoot(
            GrayboxRoot,
            paths);
        AppendAssetPathsUnderRoot(
            R941Root,
            paths);
        AppendAssetPathsUnderRoot(
            R942Root,
            paths);
        AppendAssetPathsUnderRoot(
            R943Root,
            paths);

        if (includeTestAssets)
        {
            AppendAssetPathsUnderRoot(
                TestRoot,
                paths);
        }

        paths.Sort(StringComparer.Ordinal);

        StringBuilder builder =
            new StringBuilder();

        for (int i = 0; i < paths.Count; i++)
        {
            builder.Append(paths[i]);
            builder.Append('=');
            builder.Append(
                AssetDatabase.GetAssetDependencyHash(
                    paths[i]));
            builder.Append('|');
        }

        return builder.ToString();
    }

    private static void AppendAssetPathsUnderRoot(
        string root,
        List<string> results)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[] { root });

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            if (!string.IsNullOrEmpty(path) &&
                !results.Contains(path))
            {
                results.Add(path);
            }
        }
    }

    private static void ReportFailure(
        string title,
        List<string> errors,
        UnityEngine.Object context)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(title);

        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.AppendLine(errors[i]);
        }

        Debug.LogError(
            builder.ToString(),
            context);

        EditorUtility.DisplayDialog(
            title,
            errors.Count == 0
                ? "未知错误。"
                : errors[0],
            "OK");
    }

    private sealed class TemplateSpec
    {
        public string PrefabPath { get; }
        public string TemplateId { get; }
        public DreamRoomTag RoomTags { get; }
        public int RandomWeight { get; }
        public int MaximumInstancesPerFloor { get; }

        public TemplateSpec(
            string prefabPath,
            string templateId,
            DreamRoomTag roomTags,
            int randomWeight,
            int maximumInstancesPerFloor)
        {
            PrefabPath = prefabPath;
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

    private struct LayoutMetrics
    {
        public int RoomCount;
        public int StartRoomIndex;
        public int ExitRoomIndex;
        public string StartTemplateId;
        public string ExitTemplateId;
        public int CorePlacements;
        public int CoreStandalonePlacements;
        public int CoreSpecialPlacements;
        public int SpecialPlacements;
        public int SpecialLowPlacements;
        public int SpecialHighPlacements;
        public int RarePlacements;
        public List<int> CoreRoomIndices;
        public List<int> SpecialRoomIndices;
    }

    private struct WeightMetrics
    {
        public int LowSelections;
        public int HighSelections;
        public double HighLowRatio;
        public bool Deterministic;
    }

    private struct SpecialBatchMetrics
    {
        public int SuccessfulLayouts;
        public int SpecialPlacements;
        public int MinimumSpecialPerFloor;
        public int MaximumSpecialPerFloor;
        public int CoreStandalonePlacements;
        public int CoreSpecialPlacements;
        public int SpecialLowPlacements;
        public int SpecialHighPlacements;
        public int SharedCoreSpecialLayouts;
        public int ItemScopeResolved;
        public int OutsideCoreSpecialSelections;
        public int CapBreaches;
        public bool ScopedFailureRejected;
        public bool SpecialAfterCore;
        public bool Deterministic;
    }
}
