using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.4.1 Clean Replacement 的只读资产、数据生成与 Live 验收工具。
///
/// 测试 Prefab 和 Catalog 随补丁静态提供；本工具不会创建、复制或保存
/// Prefab。只有 Prepare／Restore 会有意改变当前 Scene 的 Catalog，
/// Cleanup 菜单只删除旧失败补丁专用的隔离目录。
/// </summary>
public static class DreamRoomRoleTagAuditR941
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

    private const string CleanTestRoot =
        "Assets/DreamDungeon/Generated/R9_4_1_RoleTags_Clean";

    private const string CleanRoomRoot =
        CleanTestRoot + "/Rooms";

    private const string CleanCatalogRoot =
        CleanTestRoot + "/Catalog";

    private const string TaggedCatalogPath =
        CleanCatalogRoot +
        "/RoomCatalog_R941C_RoleTagsTest.asset";

    private const string TaggedCatalogId =
        "RoleTags_R941_CleanTest";

    private const string UnsafeCatalogPath =
        CleanCatalogRoot +
        "/RoomCatalog_R941C_UnsafeNoStandard.asset";

    private const string UnsafeCatalogId =
        "RoleTags_R941_UnsafeNoStandard";

    private const string LegacyFailedRoot =
        "Assets/DreamDungeon/Generated/R9_4_RoleTags";

    private const string FallbackWarningMarker =
        "[DungeonGenerator/R9.4.1] RoomTags 安全回退";

    private const string AuditFixId =
        "PrefabContentsContextFix_2026-07-22";

    private const int TestFloor = 1;
    private const int ExpectedRoomCount = 7;

    private static readonly TemplateSpec[] TaggedTemplateSpecs =
    {
        new TemplateSpec(
            CleanRoomRoot + "/Room_R941C_Start.prefab",
            "R941C_Start",
            DreamRoomTag.Standard |
            DreamRoomTag.StartCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1),

        new TemplateSpec(
            CleanRoomRoot + "/Room_R941C_Exit.prefab",
            "R941C_Exit",
            DreamRoomTag.Standard |
            DreamRoomTag.ExitCandidate,
            randomWeight: 1,
            maximumInstancesPerFloor: 1),

        new TemplateSpec(
            CleanRoomRoot + "/Room_R941C_Standard_A.prefab",
            "R941C_Standard_A",
            DreamRoomTag.Standard,
            randomWeight: 10,
            maximumInstancesPerFloor: 0),

        new TemplateSpec(
            CleanRoomRoot + "/Room_R941C_Standard_B.prefab",
            "R941C_Standard_B",
            DreamRoomTag.Standard,
            randomWeight: 10,
            maximumInstancesPerFloor: 0)
    };

    private static readonly TemplateSpec[] UnsafeTemplateSpecs =
    {
        new TemplateSpec(
            CleanRoomRoot + "/Room_R941C_RareOnly.prefab",
            "R941C_RareOnly",
            DreamRoomTag.Rare,
            randomWeight: 10,
            maximumInstancesPerFloor: 0)
    };

    [MenuItem(
        MenuRoot +
        "Diagnose Prefab Validation Context (R9.4.1 Fix)",
        false,
        2400)]
    private static void DiagnosePrefabValidationContext()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.1 Prefab 读取上下文诊断无法开始",
                errors,
                null);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore = context.Scene.isDirty;
        TemplateSpec spec = TaggedTemplateSpecs[0];

        int assetViewErrorCount = -1;
        int assetViewSocketCount = -1;
        int assetViewSocketOwners = -1;
        int prefabContentsErrorCount = -1;
        int prefabContentsSocketCount = -1;
        int prefabContentsSocketOwners = -1;

        DreamRoomTemplate assetTemplate =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomTemplate>(
                spec.AssetPath);

        if (assetTemplate == null)
        {
            errors.Add(
                "Asset 视图找不到 DreamRoomTemplate：" +
                spec.AssetPath);
        }
        else
        {
            List<string> assetViewErrors =
                assetTemplate.GetValidationErrors();

            assetViewErrorCount = assetViewErrors.Count;
            assetViewSocketCount =
                assetTemplate.DoorSockets.Count;
            assetViewSocketOwners =
                CountOwnedSockets(assetTemplate);
        }

        GameObject loadedRoot = null;

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(
                    spec.AssetPath);

            DreamRoomTemplate loadedTemplate =
                loadedRoot != null
                    ? loadedRoot.GetComponent<
                        DreamRoomTemplate>()
                    : null;

            if (loadedTemplate == null)
            {
                errors.Add(
                    "Prefab Contents 找不到 DreamRoomTemplate：" +
                    spec.AssetPath);
            }
            else
            {
                List<string> loadedErrors =
                    loadedTemplate.GetValidationErrors();

                prefabContentsErrorCount =
                    loadedErrors.Count;

                prefabContentsSocketCount =
                    loadedTemplate.DoorSockets.Count;

                prefabContentsSocketOwners =
                    CountOwnedSockets(loadedTemplate);

                for (int i = 0;
                     i < loadedErrors.Count;
                     i++)
                {
                    errors.Add(
                        "Prefab Contents：" +
                        loadedErrors[i]);
                }

                if (prefabContentsSocketCount != 4)
                {
                    errors.Add(
                        "Prefab Contents 应有 4 个 Socket，实际 " +
                        prefabContentsSocketCount + "。" );
                }

                if (prefabContentsSocketOwners !=
                    prefabContentsSocketCount)
                {
                    errors.Add(
                        "Prefab Contents Socket 归属应为 " +
                        prefabContentsSocketCount + "/" +
                        prefabContentsSocketCount +
                        "，实际 " +
                        prefabContentsSocketOwners + "/" +
                        prefabContentsSocketCount + "。" );
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "LoadPrefabContents 诊断抛出异常：\n" +
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
                "只读上下文诊断改变了受保护基线依赖哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读上下文诊断改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Prefab 读取上下文诊断失败",
                errors,
                context.Generator);
            return;
        }

        bool contextMismatchObserved =
            assetViewErrorCount !=
                prefabContentsErrorCount ||
            assetViewSocketOwners !=
                prefabContentsSocketOwners;

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Prefab 读取上下文诊断通过。\n" +
            "AuditFix=" + AuditFixId + "\n" +
            "Prefab=" + spec.TemplateId +
            " | AssetViewErrors=" +
            assetViewErrorCount +
            " | AssetViewSocketOwners=" +
            assetViewSocketOwners + "/" +
            assetViewSocketCount + "\n" +
            "PrefabContentsErrors=" +
            prefabContentsErrorCount +
            " | PrefabContentsSocketOwners=" +
            prefabContentsSocketOwners + "/" +
            prefabContentsSocketCount + "\n" +
            "ContextMismatchObserved=" +
            contextMismatchObserved +
            " | HierarchyAuthority=LoadPrefabContents\n" +
            "SceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | AssetsModified=False",
            assetTemplate);

        EditorUtility.DisplayDialog(
            "R9.4.1 Prefab Context Passed",
            "完整 Prefab Contents 的层级与 Socket 归属已通过。\n\n" +
            "Asset 视图只保留为诊断对照，不再作为层级校验依据。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Installed Role Test Assets (R9.4.1 Clean)",
        false,
        2410)]
    private static void ValidateInstalledRoleTestAssets()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.1 Clean 静态资产校验无法开始",
                errors,
                null);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore = context.Scene.isDirty;

        DreamRoomCatalog taggedCatalog =
            LoadCatalog(
                TaggedCatalogPath,
                TaggedCatalogId,
                "Tagged Test Catalog",
                errors);

        DreamRoomCatalog unsafeCatalog =
            LoadCatalog(
                UnsafeCatalogPath,
                UnsafeCatalogId,
                "Unsafe Test Catalog",
                errors);

        int taggedSocketReferences = 0;
        int unsafeSocketReferences = 0;

        if (taggedCatalog != null)
        {
            AppendStaticCatalogErrors(
                taggedCatalog,
                TaggedTemplateSpecs,
                "Tagged Test Catalog",
                errors,
                out taggedSocketReferences);
        }

        if (unsafeCatalog != null)
        {
            AppendStaticCatalogErrors(
                unsafeCatalog,
                UnsafeTemplateSpecs,
                "Unsafe Test Catalog",
                errors,
                out unsafeSocketReferences);
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
                "只读资产校验改变了 GameScene／Graybox 依赖哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读资产校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Clean 静态测试资产失败",
                errors,
                context.Generator);
            return;
        }

        bool legacyFolderPresent =
            AssetDatabase.IsValidFolder(
                LegacyFailedRoot);

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Clean 静态测试资产校验通过。\n" +
            "AuditFix=" + AuditFixId + "\n" +
            "Source=PackageStaticAssets" +
            " | PrefabCreateCalls=0" +
            " | PrefabSaveCalls=0\n" +
            "HierarchyAuthority=LoadPrefabContents" +
            " | AssetViewHierarchyTraversal=False\n" +
            "TaggedCatalog=" + TaggedCatalogId +
            " | Templates=4" +
            " | SocketOwners=" +
            taggedSocketReferences + "/16\n" +
            "UnsafeCatalog=" + UnsafeCatalogId +
            " | Templates=1" +
            " | SocketOwners=" +
            unsafeSocketReferences + "/4\n" +
            "GameSceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | LegacyFailedFolderPresent=" +
            legacyFolderPresent,
            taggedCatalog);

        EditorUtility.DisplayDialog(
            "R9.4.1 Clean Assets Passed",
            "五个随包静态提供的 Prefab 与两个 Catalog 已通过。\n\n" +
            "本工具没有创建、复制或保存任何 Prefab。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Start-Exit Tag Contract (R9.4.1 Clean)",
        false,
        2420)]
    private static void ValidateSelectionContract()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.1 Clean 选择契约无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog taggedCatalog =
            LoadAndValidateTaggedCatalog(errors);

        DreamRoomCatalog grayboxCatalog =
            LoadCatalog(
                GrayboxCatalogPath,
                GrayboxCatalogId,
                "Graybox Catalog",
                errors);

        DreamRoomCatalog unsafeCatalog =
            LoadAndValidateUnsafeCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Clean 选择契约无法开始",
                errors,
                context.Generator);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore = context.Scene.isDirty;

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        RoleMetrics taggedMetrics = default(RoleMetrics);
        RoleMetrics fallbackMetrics = default(RoleMetrics);
        bool deterministic = false;
        bool unsafeRejected = false;
        int taggedWarningCount = -1;
        int fallbackWarningCount = -1;
        int unsafeWarningCount = -1;

        FallbackWarningCapture warningCapture =
            new FallbackWarningCapture();

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                taggedCatalog);

            warningCapture.Reset();

            DungeonLayout firstTaggedLayout;
            DungeonLayout repeatedTaggedLayout;

            if (TryGenerateForAudit(
                    context.Generator,
                    out firstTaggedLayout,
                    errors,
                    "Tagged Catalog 第一次生成") &&
                TryGenerateForAudit(
                    context.Generator,
                    out repeatedTaggedLayout,
                    errors,
                    "Tagged Catalog 重复生成"))
            {
                ValidateRoleLayout(
                    firstTaggedLayout,
                    requireTaggedRoles: true,
                    requireUniqueTaggedRoles: true,
                    label: "Tagged Catalog",
                    errors: errors,
                    metrics: out taggedMetrics);

                deterministic =
                    string.Equals(
                        BuildLayoutSignature(
                            firstTaggedLayout),
                        BuildLayoutSignature(
                            repeatedTaggedLayout),
                        StringComparison.Ordinal);

                if (!deterministic)
                {
                    errors.Add(
                        "Tagged Catalog 使用固定 Seed 重复生成不一致。" );
                }
            }

            taggedWarningCount =
                warningCapture.Count;

            if (taggedWarningCount != 0)
            {
                errors.Add(
                    "Tagged Catalog 不应触发回退警告，实际 " +
                    taggedWarningCount + " 条。" );
            }

            SetGeneratorCatalogTransient(
                context.Generator,
                grayboxCatalog);

            warningCapture.Reset();

            DungeonLayout fallbackLayout;

            if (TryGenerateForAudit(
                    context.Generator,
                    out fallbackLayout,
                    errors,
                    "Graybox Standard 回退生成"))
            {
                ValidateRoleLayout(
                    fallbackLayout,
                    requireTaggedRoles: false,
                    requireUniqueTaggedRoles: false,
                    label: "Graybox Standard 回退",
                    errors: errors,
                    metrics: out fallbackMetrics);
            }

            fallbackWarningCount =
                warningCapture.Count;

            if (fallbackWarningCount != 1)
            {
                errors.Add(
                    "每份 Graybox 最终布局应有 1 条合并回退警告，实际 " +
                    fallbackWarningCount + " 条。" );
            }
            else if (!warningCapture.LastMessage.Contains(
                         "Start=StandardFallback") ||
                     !warningCapture.LastMessage.Contains(
                         "Exit=StandardFallback"))
            {
                errors.Add(
                    "Graybox 合并警告没有同时记录 Start／Exit 回退。" );
            }

            SetGeneratorCatalogTransient(
                context.Generator,
                unsafeCatalog);

            warningCapture.Reset();

            string unsafeReport;

            unsafeRejected =
                TryGenerateExpectedFailure(
                    context.Generator,
                    out unsafeReport,
                    errors);

            unsafeWarningCount =
                warningCapture.Count;

            if (unsafeWarningCount != 0)
            {
                errors.Add(
                    "无安全回退的失败布局不应输出成功回退警告。" );
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "R9.4.1 Clean 只读契约校验抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);

            warningCapture.Dispose();
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
                "只读契约校验改变了受保护基线依赖哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读契约校验改变了 GameScene Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Clean Start／Exit 标签契约失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Clean Start／Exit 标签选择契约通过。\n" +
            FormatMetrics(
                "Tagged",
                TaggedCatalogId,
                taggedMetrics) + "\n" +
            FormatMetrics(
                "Fallback",
                GrayboxCatalogId,
                fallbackMetrics) + "\n" +
            "Deterministic=" + deterministic +
            " | TaggedWarnings=" + taggedWarningCount +
            " | FallbackWarnings=" + fallbackWarningCount + "\n" +
            "UnsafeNoStandardRejected=" + unsafeRejected +
            " | UnsafeWarnings=" + unsafeWarningCount +
            " | FallbackPolicy=StandardOnly\n" +
            "SceneChanged=" + sceneChanged +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged +
            " | RuntimeObjectsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Clean Contract Passed",
            "标签强制选择、Graybox Standard 回退、单条警告、" +
            "固定 Seed 与无 Standard 受控失败均通过。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Prepare Tagged Role Runtime Test (R9.4.1 Clean)",
        false,
        2430)]
    private static void PrepareTaggedRuntimeTest()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.1 Clean Runtime Test 准备失败",
                errors,
                null);
            return;
        }

        DreamRoomCatalog taggedCatalog =
            LoadAndValidateTaggedCatalog(errors);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Clean Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        FallbackWarningCapture warningCapture =
            new FallbackWarningCapture();

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                taggedCatalog);

            DungeonLayout auditLayout;

            if (TryGenerateForAudit(
                    context.Generator,
                    out auditLayout,
                    errors,
                    "Runtime Test 只读预生成"))
            {
                RoleMetrics metrics;

                ValidateRoleLayout(
                    auditLayout,
                    requireTaggedRoles: true,
                    requireUniqueTaggedRoles: true,
                    label: "Runtime Test 预生成",
                    errors: errors,
                    metrics: out metrics);
            }

            if (warningCapture.Count != 0)
            {
                errors.Add(
                    "Tagged Runtime 预生成不应触发回退警告。" );
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "Runtime Test 预生成抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                originalCatalog);

            warningCapture.Dispose();
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Clean Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        SetGeneratorCatalog(
            context.Generator,
            taggedCatalog);

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Clean Tagged Runtime Test 已准备。\n" +
            "Catalog=" + TaggedCatalogId +
            " | SceneSaved=False" +
            " | DiskBaseline=" + GrayboxCatalogId + "\n" +
            "StaticAssets=True" +
            " | ReadOnlyPreflightPassed=True" +
            " | DoNotSaveUntilRestore=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Clean Runtime Test Ready",
            "GameScene 当前仅在内存中使用 Clean Tagged Catalog。\n\n" +
            "现在可以进入 Play Mode；测试后必须执行 Restore。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Live Tagged Roles (R9.4.1 Clean)",
        false,
        2440)]
    private static void ValidateLiveTaggedRoles()
    {
        List<string> errors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            errors.Add(
                "必须在 Play Mode 且 Floor 1 已完整生成后执行。" );
            ReportFailure(
                "R9.4.1 Clean Live 校验无法开始",
                errors,
                null);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

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
                 TaggedCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "当前 Catalog 不是 " + TaggedCatalogId +
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

        RoleMetrics metrics = default(RoleMetrics);

        if (layout != null)
        {
            ValidateRoleLayout(
                layout,
                requireTaggedRoles: true,
                requireUniqueTaggedRoles: true,
                label: "Live Tagged Runtime",
                errors: errors,
                metrics: out metrics);
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
                "R9.4.1 Clean Live Tagged Roles 失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Clean 真实运行时 Start／Exit 标签审计通过。\n" +
            FormatMetrics(
                "Live",
                TaggedCatalogId,
                metrics) + "\n" +
            "StartCellInTaggedRoom=True" +
            " | ExitCellInTaggedRoom=True" +
            " | DistinctRooms=True\n" +
            "GeneratedRoot=GeneratedDungeon_Floor_1" +
            " | RuntimeObjectsModified=False",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Clean Live Roles Passed",
            "Floor 1 的 Start／Exit 已分别落入唯一标签房，" +
            "且两个房间不同。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Restore and Save Graybox after R9.4.1 Clean",
        false,
        2450)]
    private static void RestoreGrayboxBaseline()
    {
        List<string> errors = new List<string>();

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

        Scene scene = SceneManager.GetActiveScene();

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
                ? FindSceneComponent<DungeonGenerator>(scene)
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
                "R9.4.1 Clean Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        SetGeneratorCatalog(
            generator,
            grayboxCatalog);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            errors.Add(
                "Graybox 已写回内存，但 GameScene 保存失败。" );
            ReportFailure(
                "R9.4.1 Clean Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        if (scene.isDirty ||
            generator.TemplateFirstRoomCatalog !=
            grayboxCatalog)
        {
            errors.Add(
                "保存后 Scene 仍为 Dirty，或 Catalog 未保持 Graybox。" );
            ReportFailure(
                "R9.4.1 Clean Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        bool legacyFolderPresent =
            AssetDatabase.IsValidFolder(
                LegacyFailedRoot);

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Clean Graybox 基线已恢复并保存。\n" +
            "Catalog=" + GrayboxCatalogId +
            " | SceneSaved=True" +
            " | RenderMode=HybridPrefabRooms" +
            " | FixedSeed=12345\n" +
            "CleanTestAssetsRetained=True" +
            " | RuntimePatchRetained=True" +
            " | LegacyFailedFolderPresent=" +
            legacyFolderPresent,
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Clean Graybox Restored",
            "GameScene 已恢复 RoomCatalog_Graybox 并保存。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Remove Superseded R9.4.1 Generated Assets",
        false,
        2460)]
    private static void RemoveSupersededGeneratedAssets()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "旧 R9.4.1 隔离资产清理无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog taggedCatalog =
            LoadAndValidateTaggedCatalog(errors);

        if (taggedCatalog == null ||
            errors.Count > 0)
        {
            ReportFailure(
                "旧 R9.4.1 隔离资产清理无法开始",
                errors,
                context.Generator);
            return;
        }

        string protectedHashBefore =
            BuildProtectedBaselineHashSignature();

        bool wasPresent =
            AssetDatabase.IsValidFolder(
                LegacyFailedRoot);

        bool removed = false;

        if (wasPresent)
        {
            removed =
                AssetDatabase.DeleteAsset(
                    LegacyFailedRoot);

            if (!removed)
            {
                errors.Add(
                    "Unity 未能删除旧工具专用目录：" +
                    LegacyFailedRoot);
            }
        }

        string protectedHashAfter =
            BuildProtectedBaselineHashSignature();

        bool protectedHashUnchanged =
            string.Equals(
                protectedHashBefore,
                protectedHashAfter,
                StringComparison.Ordinal);

        if (!protectedHashUnchanged)
        {
            errors.Add(
                "清理旧隔离目录时受保护基线依赖哈希发生变化。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "旧 R9.4.1 隔离资产清理失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "旧 R9.4.1 失败生成资产清理完成。\n" +
            "Target=" + LegacyFailedRoot +
            " | WasPresent=" + wasPresent +
            " | Removed=" + removed +
            " | AlreadyAbsent=" + (!wasPresent) + "\n" +
            "CleanAssetsRetained=True" +
            " | GameSceneChanged=False" +
            " | ProtectedHashUnchanged=" +
            protectedHashUnchanged,
            taggedCatalog);

        EditorUtility.DisplayDialog(
            "Superseded R9.4.1 Assets Removed",
            wasPresent
                ? "旧失败补丁专用目录已删除；Clean 静态资产保留。"
                : "旧失败补丁专用目录原本就不存在。",
            "OK");
    }

    private static DreamRoomCatalog
        LoadAndValidateTaggedCatalog(List<string> errors)
    {
        DreamRoomCatalog catalog =
            LoadCatalog(
                TaggedCatalogPath,
                TaggedCatalogId,
                "Tagged Test Catalog",
                errors);

        if (catalog != null)
        {
            int ignoredSocketCount;

            AppendStaticCatalogErrors(
                catalog,
                TaggedTemplateSpecs,
                "Tagged Test Catalog",
                errors,
                out ignoredSocketCount);
        }

        return catalog;
    }

    private static DreamRoomCatalog
        LoadAndValidateUnsafeCatalog(List<string> errors)
    {
        DreamRoomCatalog catalog =
            LoadCatalog(
                UnsafeCatalogPath,
                UnsafeCatalogId,
                "Unsafe Test Catalog",
                errors);

        if (catalog != null)
        {
            int ignoredSocketCount;

            AppendStaticCatalogErrors(
                catalog,
                UnsafeTemplateSpecs,
                "Unsafe Test Catalog",
                errors,
                out ignoredSocketCount);
        }

        return catalog;
    }

    private static void AppendStaticCatalogErrors(
        DreamRoomCatalog catalog,
        TemplateSpec[] expectedSpecs,
        string label,
        List<string> errors,
        out int validSocketOwners)
    {
        validSocketOwners = 0;

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                label + "：" + catalogErrors[i]);
        }

        if (catalog.Count != expectedSpecs.Length)
        {
            errors.Add(
                label + " 应包含 " +
                expectedSpecs.Length + " 个模板，实际 " +
                catalog.Count + "。" );
        }

        int count = Mathf.Min(
            catalog.Count,
            expectedSpecs.Length);

        for (int i = 0; i < count; i++)
        {
            TemplateSpec spec = expectedSpecs[i];

            // Asset 视图只读取 Catalog 可安全访问的顶层选择数据。
            // 任何 Transform／Socket／Spawn 层级检查都必须走下方的
            // LoadPrefabContents 隔离 Scene。
            DreamRoomTemplate assetTemplate =
                AssetDatabase.LoadAssetAtPath<
                    DreamRoomTemplate>(
                    spec.AssetPath);

            if (assetTemplate == null)
            {
                errors.Add(
                    label + " 找不到静态 Prefab：" +
                    spec.AssetPath);
                continue;
            }

            if (catalog.RoomTemplates[i] != assetTemplate)
            {
                errors.Add(
                    label + " 第 " + i +
                    " 项没有引用预期静态 Prefab：" +
                    spec.AssetPath);
            }

            if (!string.Equals(
                    AssetDatabase.GetAssetPath(assetTemplate),
                    spec.AssetPath,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    spec.TemplateId +
                    " 的持久资产路径与清单不一致。" );
            }

            if (!string.Equals(
                    assetTemplate.TemplateId,
                    spec.TemplateId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    spec.AssetPath + " TemplateId 应为 " +
                    spec.TemplateId + "，实际为 " +
                    assetTemplate.TemplateId + "。" );
            }

            if (assetTemplate.RoomTags != spec.Tags)
            {
                errors.Add(
                    spec.TemplateId + " RoomTags 应为 " +
                    spec.Tags + "，实际为 " +
                    assetTemplate.RoomTags + "。" );
            }

            if (assetTemplate.RandomWeight !=
                spec.RandomWeight)
            {
                errors.Add(
                    spec.TemplateId + " RandomWeight 应为 " +
                    spec.RandomWeight + "，实际为 " +
                    assetTemplate.RandomWeight + "。" );
            }

            if (assetTemplate.MaximumInstancesPerFloor !=
                spec.MaximumInstancesPerFloor)
            {
                errors.Add(
                    spec.TemplateId +
                    " MaximumInstancesPerFloor 应为 " +
                    spec.MaximumInstancesPerFloor +
                    "，实际为 " +
                    assetTemplate.MaximumInstancesPerFloor +
                    "。" );
            }

            if (!assetTemplate.CanAppearOnFloor(TestFloor))
            {
                errors.Add(
                    spec.TemplateId +
                    " 不能出现在测试 Floor 1。" );
            }

            int loadedSocketOwners;

            AppendLoadedPrefabHierarchyErrors(
                spec,
                label,
                errors,
                out loadedSocketOwners);

            validSocketOwners += loadedSocketOwners;
        }
    }

    private static void AppendLoadedPrefabHierarchyErrors(
        TemplateSpec spec,
        string label,
        List<string> errors,
        out int validSocketOwners)
    {
        validSocketOwners = 0;
        GameObject loadedRoot = null;

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(
                    spec.AssetPath);

            if (loadedRoot == null)
            {
                errors.Add(
                    label + " 无法完整加载 Prefab：" +
                    spec.AssetPath);
                return;
            }

            DreamRoomTemplate loadedTemplate =
                loadedRoot.GetComponent<DreamRoomTemplate>();

            if (loadedTemplate == null)
            {
                errors.Add(
                    label + " / " + spec.TemplateId +
                    " 的 Prefab 根节点缺少 DreamRoomTemplate。" );
                return;
            }

            if (!string.Equals(
                    loadedTemplate.TemplateId,
                    spec.TemplateId,
                    StringComparison.Ordinal) ||
                loadedTemplate.RoomTags != spec.Tags ||
                loadedTemplate.RandomWeight !=
                    spec.RandomWeight ||
                loadedTemplate.MaximumInstancesPerFloor !=
                    spec.MaximumInstancesPerFloor)
            {
                errors.Add(
                    label + " / " + spec.TemplateId +
                    " 的 Asset 视图与完整 Prefab Contents " +
                    "元数据不一致。" );
            }

            List<string> templateErrors =
                loadedTemplate.GetValidationErrors();

            for (int errorIndex = 0;
                 errorIndex < templateErrors.Count;
                 errorIndex++)
            {
                errors.Add(
                    spec.TemplateId +
                    " / Prefab Contents：" +
                    templateErrors[errorIndex]);
            }

            if (loadedTemplate.DoorSockets.Count != 4)
            {
                errors.Add(
                    spec.TemplateId +
                    " 应保持 4 个 Graybox Socket，实际为 " +
                    loadedTemplate.DoorSockets.Count + "。" );
            }

            validSocketOwners =
                CountOwnedSockets(loadedTemplate);

            if (validSocketOwners !=
                loadedTemplate.DoorSockets.Count)
            {
                errors.Add(
                    spec.TemplateId +
                    " 的完整 Prefab Contents Socket 归属应为 " +
                    loadedTemplate.DoorSockets.Count + "/" +
                    loadedTemplate.DoorSockets.Count +
                    "，实际 " + validSocketOwners + "/" +
                    loadedTemplate.DoorSockets.Count + "。" );
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                label + " / " + spec.TemplateId +
                " 完整加载时抛出异常：\n" +
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

    private static int CountOwnedSockets(
        DreamRoomTemplate template)
    {
        if (template == null ||
            template.DoorSockets == null)
        {
            return 0;
        }

        int ownedSocketCount = 0;

        for (int socketIndex = 0;
             socketIndex < template.DoorSockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[socketIndex];

            if (socket != null &&
                socket.GetComponentInParent<
                    DreamRoomTemplate>() == template)
            {
                ownedSocketCount++;
            }
        }

        return ownedSocketCount;
    }

    private static bool TryGenerateForAudit(
        DungeonGenerator generator,
        out DungeonLayout layout,
        List<string> errors,
        string label)
    {
        layout = null;
        string report;

        try
        {
            if (!generator.TryGenerateHybridRuntimeLayout(
                    TestFloor,
                    out layout,
                    out report) ||
                layout == null)
            {
                errors.Add(
                    label + "失败：\n" + report);
                return false;
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                label + "抛出异常：\n" +
                exception);
            return false;
        }

        List<string> layoutErrors =
            layout.GetValidationErrors();

        for (int i = 0; i < layoutErrors.Count; i++)
        {
            errors.Add(
                label + " / DungeonLayout：" +
                layoutErrors[i]);
        }

        List<string> corridorErrors =
            generator.GetSocketCorridorValidationErrors(
                layout);

        for (int i = 0; i < corridorErrors.Count; i++)
        {
            errors.Add(
                label + " / SocketCorridor：" +
                corridorErrors[i]);
        }

        return layoutErrors.Count == 0 &&
               corridorErrors.Count == 0;
    }

    private static bool TryGenerateExpectedFailure(
        DungeonGenerator generator,
        out string report,
        List<string> errors)
    {
        report = string.Empty;
        DungeonLayout layout;

        try
        {
            bool succeeded =
                generator.TryGenerateHybridRuntimeLayout(
                    TestFloor,
                    out layout,
                    out report);

            if (succeeded || layout != null)
            {
                errors.Add(
                    "UnsafeNoStandard Catalog 本应被拒绝，" +
                    "但生成器返回了成功布局。" );
                return false;
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "UnsafeNoStandard 应以可报告失败结束，" +
                "不应抛出异常：\n" + exception);
            return false;
        }

        bool hasRoleMarker =
            report.IndexOf(
                "R9.4.1",
                StringComparison.Ordinal) >= 0;

        bool explainsStandardFallback =
            report.IndexOf(
                "Standard",
                StringComparison.OrdinalIgnoreCase) >= 0;

        if (!hasRoleMarker ||
            !explainsStandardFallback)
        {
            errors.Add(
                "UnsafeNoStandard 的失败报告没有明确说明 " +
                "R9.4.1／Standard 回退原因：\n" + report);
            return false;
        }

        return true;
    }

    private static void ValidateRoleLayout(
        DungeonLayout layout,
        bool requireTaggedRoles,
        bool requireUniqueTaggedRoles,
        string label,
        List<string> errors,
        out RoleMetrics metrics)
    {
        metrics = default(RoleMetrics);

        if (layout == null)
        {
            errors.Add(
                label + " 的 Layout 为空。" );
            return;
        }

        metrics.RoomCount =
            layout.RoomPlacements.Count;

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
            FormatTemplateId(startTemplate);

        metrics.ExitTemplateId =
            FormatTemplateId(exitTemplate);

        metrics.StartTagged =
            startTemplate != null &&
            startTemplate.HasTag(
                DreamRoomTag.StartCandidate);

        metrics.ExitTagged =
            exitTemplate != null &&
            exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate);

        metrics.StartIsStandard =
            startTemplate != null &&
            startTemplate.HasTag(
                DreamRoomTag.Standard);

        metrics.ExitIsStandard =
            exitTemplate != null &&
            exitTemplate.HasTag(
                DreamRoomTag.Standard);

        metrics.StartIsSpecial =
            startTemplate != null &&
            startTemplate.HasTag(
                DreamRoomTag.Special);

        metrics.ExitIsSpecial =
            exitTemplate != null &&
            exitTemplate.HasTag(
                DreamRoomTag.Special);

        metrics.StartCandidateCount =
            CountPlacedCandidates(
                layout,
                DreamRoomTag.StartCandidate,
                excludeRoomIndex: -1);

        metrics.ExitCandidateCount =
            CountPlacedCandidates(
                layout,
                DreamRoomTag.ExitCandidate,
                excludeRoomIndex:
                    metrics.StartRoomIndex);

        if (metrics.RoomCount != ExpectedRoomCount)
        {
            errors.Add(
                label + " 房间数应为 " +
                ExpectedRoomCount + "，实际为 " +
                metrics.RoomCount + "。" );
        }

        if (metrics.StartRoomIndex < 0 ||
            metrics.ExitRoomIndex < 0)
        {
            errors.Add(
                label + " 无法把 StartCell／ExitCell 映射回房间。" );
            return;
        }

        if (metrics.StartRoomIndex ==
            metrics.ExitRoomIndex)
        {
            errors.Add(
                label + " 的 Start 与 Exit 落在同一房间。" );
        }

        if (requireTaggedRoles)
        {
            if (!metrics.StartTagged ||
                !metrics.ExitTagged)
            {
                errors.Add(
                    label +
                    " 的实际 Start／Exit 没有使用对应标签房。" );
            }

            if (!string.Equals(
                    metrics.StartTemplateId,
                    "R941C_Start",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    metrics.ExitTemplateId,
                    "R941C_Exit",
                    StringComparison.Ordinal))
            {
                errors.Add(
                    label + " 的唯一角色房不正确：Start=" +
                    metrics.StartTemplateId + "，Exit=" +
                    metrics.ExitTemplateId + "。" );
            }

            if (requireUniqueTaggedRoles &&
                (metrics.StartCandidateCount != 1 ||
                 metrics.ExitCandidateCount != 1))
            {
                errors.Add(
                    label +
                    " 应各放入 1 个角色候选，实际 Start=" +
                    metrics.StartCandidateCount + "，Exit=" +
                    metrics.ExitCandidateCount + "。" );
            }
        }
        else
        {
            if (metrics.StartTagged ||
                metrics.ExitTagged)
            {
                errors.Add(
                    label +
                    " 本应验证无标签 Standard 回退。" );
            }

            if (!metrics.StartIsStandard ||
                !metrics.ExitIsStandard ||
                metrics.StartIsSpecial ||
                metrics.ExitIsSpecial)
            {
                errors.Add(
                    label +
                    " 必须回退到普通 Standard，不能使用 Special。" );
            }
        }
    }

    private static string BuildLayoutSignature(
        DungeonLayout layout)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            builder.Append(i);
            builder.Append(':');
            builder.Append(
                placement.Template.TemplateId);
            builder.Append('@');
            builder.Append(placement.MinimumCell.x);
            builder.Append(',');
            builder.Append(placement.MinimumCell.y);
            builder.Append(',');
            builder.Append(
                placement.ClockwiseQuarterTurns);
            builder.Append(';');
        }

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            builder.Append('C');
            builder.Append(connection.RoomAIndex);
            builder.Append('-');
            builder.Append(connection.RoomBIndex);
            builder.Append(':');
            builder.Append(connection.SocketAId);
            builder.Append('/');
            builder.Append(connection.SocketBId);
            builder.Append('[');

            for (int cellIndex = 0;
                 cellIndex < connection.CorridorCells.Count;
                 cellIndex++)
            {
                Vector2Int cell =
                    connection.CorridorCells[cellIndex];

                builder.Append(cell.x);
                builder.Append(',');
                builder.Append(cell.y);
                builder.Append('|');
            }

            builder.Append(']');
        }

        builder.Append("S=");
        builder.Append(layout.StartCell.x);
        builder.Append(',');
        builder.Append(layout.StartCell.y);
        builder.Append(";E=");
        builder.Append(layout.ExitCell.x);
        builder.Append(',');
        builder.Append(layout.ExitCell.y);

        return builder.ToString();
    }

    private static int FindWalkableRoomIndex(
        DungeonLayout layout,
        Vector2Int cell)
    {
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
            roomIndex >= layout.RoomPlacements.Count ||
            layout.RoomPlacements[roomIndex] == null)
        {
            return null;
        }

        return layout.RoomPlacements[roomIndex].Template;
    }

    private static int CountPlacedCandidates(
        DungeonLayout layout,
        DreamRoomTag tag,
        int excludeRoomIndex)
    {
        int count = 0;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            if (i == excludeRoomIndex)
            {
                continue;
            }

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

    private static string FormatTemplateId(
        DreamRoomTemplate template)
    {
        if (template == null)
        {
            return "<missing>";
        }

        return string.IsNullOrWhiteSpace(template.TemplateId)
            ? "<empty>"
            : template.TemplateId;
    }

    private static string FormatMetrics(
        string label,
        string catalogId,
        RoleMetrics metrics)
    {
        return label + "Catalog=" + catalogId +
               " | Rooms=" + metrics.RoomCount +
               "/" + ExpectedRoomCount +
               " | Start=" + metrics.StartTemplateId +
               "(Tagged=" + metrics.StartTagged + ")" +
               " | Exit=" + metrics.ExitTemplateId +
               "(Tagged=" + metrics.ExitTagged + ")" +
               " | StartCandidates=" +
               metrics.StartCandidateCount +
               " | ExitCandidates=" +
               metrics.ExitCandidateCount;
    }

    private static DreamRoomCatalog LoadCatalog(
        string assetPath,
        string expectedId,
        string label,
        List<string> errors)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomCatalog>(assetPath);

        if (catalog == null)
        {
            errors.Add(
                label + " 不存在：" + assetPath);
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

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
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

        if (requireCleanScene && scene.isDirty)
        {
            errors.Add(
                "GameScene 当前有未保存修改。请确认 Catalog 为 Graybox 后保存。" );
        }

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

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
                12345,
                "Fixed Seed",
                errors);
        }

        context = new SceneContext(
            scene,
            generator,
            renderer);

        return errors.Count == 0;
    }

    private static void SetGeneratorCatalog(
        DungeonGenerator generator,
        DreamRoomCatalog catalog)
    {
        SerializedObject serializedGenerator =
            new SerializedObject(generator);

        SerializedProperty property =
            serializedGenerator.FindProperty(
                "templateFirstRoomCatalog");

        if (property == null)
        {
            throw new InvalidOperationException(
                "DungeonGenerator 找不到 templateFirstRoomCatalog 字段。" );
        }

        property.objectReferenceValue = catalog;
        serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 只供只读审计使用；在 finally 中恢复，不调用 SerializedObject／SetDirty。
    /// </summary>
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

        layout = field.GetValue(gameManager) as DungeonLayout;
        return layout != null;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
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
        UnityEngine.Object target,
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
        UnityEngine.Object target,
        string fieldName,
        int expected,
        string label,
        List<string> errors)
    {
        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null ||
            field.FieldType != typeof(int))
        {
            errors.Add(
                "无法读取 " + label + "。" );
            return;
        }

        int actual =
            (int)field.GetValue(target);

        if (actual != expected)
        {
            errors.Add(
                label + " 应为 " + expected +
                "，实际为 " + actual + "。" );
        }
    }

    private static string BuildProtectedBaselineHashSignature()
    {
        string[] protectedPaths =
        {
            GameScenePath,
            GrayboxCatalogPath,
            GrayboxRoot + "/Rooms/Room_08x06.prefab",
            GrayboxRoot + "/Rooms/Room_09x16.prefab",
            GrayboxRoot + "/Rooms/Room_13x09.prefab",
            GrayboxRoot + "/Rooms/Room_18x07.prefab"
        };

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < protectedPaths.Length; i++)
        {
            builder.Append(protectedPaths[i]);
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
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DreamRoomRoleTagAuditR941] " + title);

        if (errors == null || errors.Count == 0)
        {
            builder.AppendLine(
                "- 未提供具体错误。" );
        }
        else
        {
            for (int i = 0; i < errors.Count; i++)
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

    private readonly struct TemplateSpec
    {
        public string AssetPath { get; }
        public string TemplateId { get; }
        public DreamRoomTag Tags { get; }
        public int RandomWeight { get; }
        public int MaximumInstancesPerFloor { get; }

        public TemplateSpec(
            string assetPath,
            string templateId,
            DreamRoomTag tags,
            int randomWeight,
            int maximumInstancesPerFloor)
        {
            AssetPath = assetPath;
            TemplateId = templateId;
            Tags = tags;
            RandomWeight = randomWeight;
            MaximumInstancesPerFloor =
                maximumInstancesPerFloor;
        }
    }

    private struct RoleMetrics
    {
        public int RoomCount;
        public int StartRoomIndex;
        public int ExitRoomIndex;
        public string StartTemplateId;
        public string ExitTemplateId;
        public bool StartTagged;
        public bool ExitTagged;
        public bool StartIsStandard;
        public bool ExitIsStandard;
        public bool StartIsSpecial;
        public bool ExitIsSpecial;
        public int StartCandidateCount;
        public int ExitCandidateCount;
    }

    private readonly struct SceneContext
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

    private sealed class FallbackWarningCapture : IDisposable
    {
        public int Count { get; private set; }
        public string LastMessage { get; private set; }

        public FallbackWarningCapture()
        {
            LastMessage = string.Empty;
            Application.logMessageReceived += OnLogMessage;
        }

        public void Reset()
        {
            Count = 0;
            LastMessage = string.Empty;
        }

        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (type != LogType.Warning ||
                string.IsNullOrEmpty(condition) ||
                condition.IndexOf(
                    FallbackWarningMarker,
                    StringComparison.Ordinal) < 0)
            {
                return;
            }

            Count++;
            LastMessage = condition;
        }
    }
}
