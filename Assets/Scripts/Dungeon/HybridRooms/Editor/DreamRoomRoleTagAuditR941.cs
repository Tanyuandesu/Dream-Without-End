using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.4.1 Start／Exit RoomTags 的独立资产生成与验收工具。
///
/// 测试资产全部位于 R9_4_RoleTags；不会修改 R3 Graybox Prefab、
/// R9.1 非矩形样本或正式 GameScene。只有 Prepare 菜单会把当前
/// GameScene 的 Catalog 临时切到测试目录，Restore 菜单负责恢复并保存。
///
/// R9.4.1b：每次从受保护的 Graybox 源重新建立工具自有测试 Prefab，
/// 随后直接在持久 Prefab Asset 上写入角色参数与内部引用。刻意避开
/// LoadPrefabContents -> SaveAsPrefabAsset 的同路径二次保存，防止 Unity 6000
/// 把已经重绑的 Socket 再次序列化为跨 Prefab 引用。
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

    private const string GrayboxSourcePrefabPath =
        GrayboxRoot + "/Rooms/Room_08x06.prefab";

    private const string TestRoot =
        "Assets/DreamDungeon/Generated/R9_4_RoleTags";

    private const string TestRoomFolder =
        TestRoot + "/Rooms";

    private const string TestCatalogFolder =
        TestRoot + "/Catalog";

    private const string TestCatalogPath =
        TestCatalogFolder +
        "/RoomCatalog_R941_RoleTagsTest.asset";

    private const string TestCatalogId =
        "RoleTags_R941_Test";

    private const string GrayboxCatalogId =
        "Graybox_R3";

    private const int TestFloor = 1;
    private const int ExpectedRoomCount = 7;

    private static readonly RolePrefabSpec[] RolePrefabSpecs =
    {
        new RolePrefabSpec(
            "R941_Start",
            "Room_R941_Start.prefab",
            DreamRoomTag.Standard |
            DreamRoomTag.StartCandidate,
            randomWeight: 1,
            maximumInstances: 1),

        new RolePrefabSpec(
            "R941_Exit",
            "Room_R941_Exit.prefab",
            DreamRoomTag.Standard |
            DreamRoomTag.ExitCandidate,
            randomWeight: 1,
            maximumInstances: 1),

        new RolePrefabSpec(
            "R941_Standard_A",
            "Room_R941_Standard_A.prefab",
            DreamRoomTag.Standard,
            randomWeight: 10,
            maximumInstances: 0),

        new RolePrefabSpec(
            "R941_Standard_B",
            "Room_R941_Standard_B.prefab",
            DreamRoomTag.Standard,
            randomWeight: 10,
            maximumInstances: 0)
    };

    [MenuItem(
        MenuRoot +
        "Generate Start-Exit Role Test Assets (R9.4.1)",
        false,
        2410)]
    private static void GenerateTestAssets()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetCleanGrayboxSceneContext(
                requireCleanScene: true,
                out context,
                out errors))
        {
            ReportFailure(
                "R9.4.1 资产生成无法开始",
                errors,
                null);
            return;
        }

        string baselineHashBefore =
            BuildProtectedBaselineHashSignature();

        EnsureAssetFolder(TestRoot);
        EnsureAssetFolder(TestRoomFolder);
        EnsureAssetFolder(TestCatalogFolder);

        List<DreamRoomTemplate> templates =
            new List<DreamRoomTemplate>();

        for (int i = 0;
             i < RolePrefabSpecs.Length;
             i++)
        {
            DreamRoomTemplate template;

            if (!TryCreateOrRefreshRolePrefab(
                    RolePrefabSpecs[i],
                    out template,
                    errors))
            {
                break;
            }

            templates.Add(template);
        }

        DreamRoomCatalog catalog = null;

        if (errors.Count == 0)
        {
            catalog = CreateOrRefreshTestCatalog(
                templates,
                errors);
        }

        if (catalog != null)
        {
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        AssetDatabase.Refresh();

        if (catalog != null)
        {
            AppendTestAssetValidationErrors(
                catalog,
                errors);
        }

        string baselineHashAfter =
            BuildProtectedBaselineHashSignature();

        bool baselineUnchanged =
            string.Equals(
                baselineHashBefore,
                baselineHashAfter,
                StringComparison.Ordinal);

        if (!baselineUnchanged)
        {
            errors.Add(
                "受保护的 GameScene／Graybox 依赖哈希发生变化。" +
                "不要继续测试，请保留当前 Console。" );
        }

        if (context.Scene.isDirty)
        {
            errors.Add(
                "生成测试资产后 GameScene 意外变为未保存状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 测试资产生成失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Start／Exit 独立测试资产已生成并校验通过。\n" +
            "Catalog=" + TestCatalogId +
            " | Templates=4" +
            " | StartCandidates=1" +
            " | ExitCandidates=1\n" +
            "StartMaxPerFloor=1 | ExitMaxPerFloor=1" +
            " | StandardFallbackTemplates=2\n" +
            "Hotfix=R9.4.1b" +
            " | TestPrefabsRebuilt=4/4" +
            " | PersistentRefsValidated=4/4\n" +
            "GameSceneChanged=False" +
            " | GrayboxAssetsModified=False" +
            " | BaselineHashUnchanged=" +
            baselineUnchanged,
            catalog);

        EditorUtility.DisplayDialog(
            "R9.4.1 Test Assets Ready",
            "四个独立测试 Prefab 与专用 Catalog 已生成。\n\n" +
            "GameScene 和 R3 Graybox 均未修改。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Start-Exit Tag Contract (R9.4.1)",
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
                "R9.4.1 契约校验无法开始",
                errors,
                null);
            return;
        }

        DreamRoomCatalog testCatalog =
            LoadCatalog(
                TestCatalogPath,
                TestCatalogId,
                "R9.4.1 Test Catalog",
                errors);

        DreamRoomCatalog grayboxCatalog =
            LoadCatalog(
                GrayboxCatalogPath,
                GrayboxCatalogId,
                "Graybox Catalog",
                errors);

        if (testCatalog != null)
        {
            AppendTestAssetValidationErrors(
                testCatalog,
                errors);
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 契约校验无法开始",
                errors,
                context.Generator);
            return;
        }

        string baselineHashBefore =
            BuildProtectedBaselineHashSignature();

        bool sceneDirtyBefore = context.Scene.isDirty;

        DungeonGenerator auditGenerator =
            context.Generator;

        DreamRoomCatalog originalCatalog =
            auditGenerator.TemplateFirstRoomCatalog;

        RoleMetrics taggedMetrics = default(RoleMetrics);
        RoleMetrics fallbackMetrics = default(RoleMetrics);
        bool deterministic = false;

        try
        {
            SetGeneratorCatalogTransient(
                auditGenerator,
                testCatalog);

            DungeonLayout firstTaggedLayout;
            DungeonLayout repeatedTaggedLayout;

            if (!TryGenerateForAudit(
                    auditGenerator,
                    out firstTaggedLayout,
                    errors,
                    "Tagged Catalog 第一次生成") ||
                !TryGenerateForAudit(
                    auditGenerator,
                    out repeatedTaggedLayout,
                    errors,
                    "Tagged Catalog 重复生成"))
            {
                // errors 已由 TryGenerateForAudit 填写。
            }
            else
            {
                ValidateRoleLayout(
                    firstTaggedLayout,
                    requireTaggedRoles: true,
                    requireUniqueTaggedRoles: true,
                    "Tagged Catalog",
                    errors,
                    out taggedMetrics);

                string firstSignature =
                    BuildLayoutSignature(
                        firstTaggedLayout);

                string repeatedSignature =
                    BuildLayoutSignature(
                        repeatedTaggedLayout);

                deterministic =
                    string.Equals(
                        firstSignature,
                        repeatedSignature,
                        StringComparison.Ordinal);

                if (!deterministic)
                {
                    errors.Add(
                        "Tagged Catalog 使用同一固定 Seed 重复生成不一致。" );
                }
            }

            SetGeneratorCatalogTransient(
                auditGenerator,
                grayboxCatalog);

            DungeonLayout fallbackLayout;

            if (TryGenerateForAudit(
                    auditGenerator,
                    out fallbackLayout,
                    errors,
                    "Graybox Standard 回退生成"))
            {
                ValidateRoleLayout(
                    fallbackLayout,
                    requireTaggedRoles: false,
                    requireUniqueTaggedRoles: false,
                    "Graybox Standard 回退",
                    errors,
                    out fallbackMetrics);
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "R9.4.1 只读契约校验抛出异常：\n" +
                exception);
        }
        finally
        {
            SetGeneratorCatalogTransient(
                auditGenerator,
                originalCatalog);
        }

        string baselineHashAfter =
            BuildProtectedBaselineHashSignature();

        bool baselineUnchanged =
            string.Equals(
                baselineHashBefore,
                baselineHashAfter,
                StringComparison.Ordinal);

        bool sceneChanged =
            context.Scene.isDirty != sceneDirtyBefore;

        if (!baselineUnchanged)
        {
            errors.Add(
                "只读校验改变了受保护基线的依赖哈希。" );
        }

        if (sceneChanged)
        {
            errors.Add(
                "只读校验改变了 GameScene 的 Dirty 状态。" );
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Start／Exit 标签契约失败",
                errors,
                context.Generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Start／Exit 标签选择契约通过。\n" +
            FormatMetrics(
                "Tagged",
                TestCatalogId,
                taggedMetrics) + "\n" +
            FormatMetrics(
                "Fallback",
                GrayboxCatalogId,
                fallbackMetrics) + "\n" +
            "Deterministic=" + deterministic +
            " | FallbackPolicy=StandardOnly" +
            " | FallbackWarning=SingleCombinedPerFinalLayout\n" +
            "SceneChanged=" + sceneChanged +
            " | BaselineHashUnchanged=" +
            baselineUnchanged +
            " | RuntimeObjectsModified=False",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Contract Passed",
            "Tagged 强制选择与 Graybox Standard 回退均通过。\n\n" +
            "Console 中会有一条 Graybox 回退黄色警告；" +
            "这是本轮被验证的预期行为，不是失败。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Prepare Tagged Role Runtime Test (R9.4.1)",
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
                "R9.4.1 Runtime Test 准备失败",
                errors,
                null);
            return;
        }

        DreamRoomCatalog testCatalog =
            LoadCatalog(
                TestCatalogPath,
                TestCatalogId,
                "R9.4.1 Test Catalog",
                errors);

        if (testCatalog != null)
        {
            AppendTestAssetValidationErrors(
                testCatalog,
                errors);
        }

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.4.1 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        DungeonLayout auditLayout;
        DreamRoomCatalog originalCatalog =
            context.Generator.TemplateFirstRoomCatalog;

        try
        {
            SetGeneratorCatalogTransient(
                context.Generator,
                testCatalog);

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
                    "Runtime Test 预生成",
                    errors,
                    out metrics);
            }
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
                "R9.4.1 Runtime Test 准备失败",
                errors,
                context.Generator);
            return;
        }

        SetGeneratorCatalog(
            context.Generator,
            testCatalog);

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Tagged Runtime Test 已准备。\n" +
            "Catalog=" + TestCatalogId +
            " | SceneSaved=False" +
            " | DiskBaseline=Graybox_R3\n" +
            "DoNotSaveUntilRestore=True" +
            " | ReadOnlyPreflightPassed=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Tagged Runtime Test Ready",
            "GameScene 当前仅在内存中使用 RoleTags_R941_Test。\n\n" +
            "现在可以进入 Play Mode。测试后必须执行 R9.4.1 Restore。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Validate Live Tagged Roles (R9.4.1)",
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
                "R9.4.1 Live 校验无法开始",
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
            errors.Add("Play Mode 中找不到 DungeonGenerator。" );
        }

        if (gameManager == null)
        {
            errors.Add("Play Mode 中找不到 GameManager。" );
        }

        if (generator != null &&
            (generator.TemplateFirstRoomCatalog == null ||
             !string.Equals(
                 generator.TemplateFirstRoomCatalog.CatalogId,
                 TestCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "当前 Catalog 不是 " + TestCatalogId +
                "。请退出 Play Mode，重新执行 Prepare。" );
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
                "Live Tagged Runtime",
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
                "R9.4.1 Live Tagged Roles 失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 真实运行时 Start／Exit 标签审计通过。\n" +
            FormatMetrics(
                "Live",
                TestCatalogId,
                metrics) + "\n" +
            "StartCellInTaggedRoom=True" +
            " | ExitCellInTaggedRoom=True" +
            " | DistinctRooms=True\n" +
            "GeneratedRoot=GeneratedDungeon_Floor_1" +
            " | RuntimeObjectsModified=False",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Live Roles Passed",
            "Floor 1 的 StartCell 与 ExitCell 已分别落入唯一的" +
            "标签房，且两个房间不同。",
            "OK");
    }

    [MenuItem(
        MenuRoot +
        "Restore and Save Graybox after R9.4.1",
        false,
        2450)]
    private static void RestoreGrayboxBaseline()
    {
        List<string> errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须先退出 Play Mode。" );
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。" );
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
            errors.Add("GameScene 中找不到 DungeonGenerator。" );
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
                "R9.4.1 Graybox 恢复失败",
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
                "R9.4.1 Graybox 恢复失败",
                errors,
                generator);
            return;
        }

        Debug.Log(
            "[DreamRoomRoleTagAuditR941] " +
            "R9.4.1 Graybox 基线已恢复并保存。\n" +
            "Catalog=" + GrayboxCatalogId +
            " | SceneSaved=True" +
            " | RenderMode=HybridPrefabRooms" +
            " | FixedSeed=12345\n" +
            "TestAssetsRetained=True" +
            " | RoleTagRuntimePatchRetained=True",
            generator);

        EditorUtility.DisplayDialog(
            "R9.4.1 Graybox Restored",
            "GameScene 已恢复 RoomCatalog_Graybox 并保存。\n" +
            "R9.4.1 测试资产与运行时代码均保留。",
            "OK");
    }

    private static bool TryCreateOrRefreshRolePrefab(
        RolePrefabSpec spec,
        out DreamRoomTemplate templateAsset,
        List<string> errors)
    {
        templateAsset = null;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                GrayboxSourcePrefabPath) == null)
        {
            errors.Add(
                "找不到 Graybox 源 Prefab：" +
                GrayboxSourcePrefabPath);
            return false;
        }

        string destinationPath =
            TestRoomFolder + "/" + spec.FileName;

        // 这些文件完全属于 R9.4.1 测试工具。每轮都从受保护源重建，
        // 可自动清除 R9.4.1／R9.4.1a 留下的半成品或跨 Prefab 引用。
        if (!string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(
                    destinationPath)) &&
            !AssetDatabase.DeleteAsset(destinationPath))
        {
            errors.Add(
                "无法替换旧测试 Prefab：" +
                destinationPath);
            return false;
        }

        if (!AssetDatabase.CopyAsset(
                GrayboxSourcePrefabPath,
                destinationPath))
        {
            errors.Add(
                "无法从 Graybox 源重建测试 Prefab：" +
                destinationPath);
            return false;
        }

        AssetDatabase.ImportAsset(
            destinationPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        try
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    destinationPath);

            if (root == null)
            {
                errors.Add(
                    "无法载入测试 Prefab：" +
                    destinationPath);
                return false;
            }

            DreamRoomTemplate template =
                root.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                errors.Add(
                    "测试 Prefab 根节点缺少 DreamRoomTemplate：" +
                    destinationPath);
                return false;
            }

            Transform visualRoot =
                root.transform.Find("Visual");

            Transform socketsRoot =
                root.transform.Find("Sockets");

            Transform navigationRoot =
                root.transform.Find("Navigation");

            Transform spawnPointsRoot =
                root.transform.Find("SpawnPoints");

            if (visualRoot == null ||
                socketsRoot == null ||
                navigationRoot == null ||
                spawnPointsRoot == null)
            {
                errors.Add(
                    spec.TemplateId +
                    " 无法在持久 Prefab Asset 中解析 " +
                    "Visual／Sockets／Navigation／SpawnPoints 根节点。" );
                return false;
            }

            DreamRoomDoorSocket[] sockets =
                socketsRoot.GetComponentsInChildren<
                    DreamRoomDoorSocket>(true);

            DreamRoomSpawnPoint[] spawnPoints =
                spawnPointsRoot.GetComponentsInChildren<
                    DreamRoomSpawnPoint>(true);

            if (sockets.Length != 4)
            {
                errors.Add(
                    spec.TemplateId +
                    " 应从 Graybox 源继承 4 个 Socket，实际为 " +
                    sockets.Length + "。" );
                return false;
            }

            SerializedObject serializedTemplate =
                new SerializedObject(template);

            serializedTemplate.Update();

            SetString(
                serializedTemplate,
                "templateId",
                spec.TemplateId);

            SetInt(
                serializedTemplate,
                "randomWeight",
                spec.RandomWeight);

            SetInt(
                serializedTemplate,
                "minimumFloor",
                1);

            SetInt(
                serializedTemplate,
                "maximumFloor",
                0);

            SetInt(
                serializedTemplate,
                "maximumInstancesPerFloor",
                spec.MaximumInstances);

            SetInt(
                serializedTemplate,
                "roomTags",
                (int)spec.Tags);

            SetObjectReference(
                serializedTemplate,
                "visualRoot",
                visualRoot);

            SetObjectReference(
                serializedTemplate,
                "socketsRoot",
                socketsRoot);

            SetObjectReference(
                serializedTemplate,
                "navigationRoot",
                navigationRoot);

            SetObjectReference(
                serializedTemplate,
                "spawnPointsRoot",
                spawnPointsRoot);

            SetObjectReferenceArray(
                serializedTemplate,
                "doorSockets",
                sockets);

            SetObjectReferenceArray(
                serializedTemplate,
                "spawnPoints",
                spawnPoints);

            if (!serializedTemplate
                    .ApplyModifiedPropertiesWithoutUndo())
            {
                errors.Add(
                    spec.TemplateId +
                    " 的持久 Prefab 参数没有产生可保存变更。" );
                return false;
            }

            EditorUtility.SetDirty(template);

            bool saveSucceeded;
            GameObject savedRoot =
                PrefabUtility.SavePrefabAsset(
                    root,
                    out saveSucceeded);

            if (!saveSucceeded || savedRoot == null)
            {
                errors.Add(
                    "保存持久测试 Prefab 失败：" +
                    destinationPath);
                return false;
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "写入测试 Prefab 失败：" +
                destinationPath + "\n" + exception);
            return false;
        }

        AssetDatabase.ImportAsset(
            destinationPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        templateAsset = LoadTemplateAsset(
            destinationPath);

        if (templateAsset == null)
        {
            errors.Add(
                "保存后无法重新读取 DreamRoomTemplate：" +
                destinationPath);
            return false;
        }

        List<string> persistentErrors =
            templateAsset.GetValidationErrors();

        if (persistentErrors.Count > 0)
        {
            for (int i = 0;
                 i < persistentErrors.Count;
                 i++)
            {
                errors.Add(
                    spec.TemplateId +
                    " 持久资产校验：" +
                    persistentErrors[i]);
            }

            templateAsset = null;
            return false;
        }

        return true;
    }

    private static DreamRoomCatalog CreateOrRefreshTestCatalog(
        List<DreamRoomTemplate> templates,
        List<string> errors)
    {
        if (templates == null ||
            templates.Count != RolePrefabSpecs.Length)
        {
            errors.Add(
                "测试 Catalog 需要正好 4 个模板。" );
            return null;
        }

        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                TestCatalogPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<
                    DreamRoomCatalog>();

            AssetDatabase.CreateAsset(
                catalog,
                TestCatalogPath);
        }

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        SetString(
            serializedCatalog,
            "catalogId",
            TestCatalogId);

        SerializedProperty roomTemplates =
            serializedCatalog.FindProperty(
                "roomTemplates");

        if (roomTemplates == null)
        {
            errors.Add(
                "DreamRoomCatalog 找不到 roomTemplates 字段。" );
            return null;
        }

        roomTemplates.arraySize = templates.Count;

        for (int i = 0; i < templates.Count; i++)
        {
            roomTemplates.GetArrayElementAtIndex(i).
                objectReferenceValue = templates[i];
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        return catalog;
    }

    private static void AppendTestAssetValidationErrors(
        DreamRoomCatalog catalog,
        List<string> errors)
    {
        if (catalog == null)
        {
            errors.Add("R9.4.1 Test Catalog 为空。" );
            return;
        }

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                "Test Catalog：" +
                catalogErrors[i]);
        }

        int startCandidates = 0;
        int exitCandidates = 0;

        IReadOnlyList<DreamRoomTemplate> templates =
            catalog.RoomTemplates;

        for (int i = 0;
             i < templates.Count;
             i++)
        {
            DreamRoomTemplate template = templates[i];

            if (template == null)
            {
                continue;
            }

            List<string> templateErrors =
                template.GetValidationErrors();

            for (int errorIndex = 0;
                 errorIndex < templateErrors.Count;
                 errorIndex++)
            {
                errors.Add(
                    template.TemplateId + "：" +
                    templateErrors[errorIndex]);
            }

            if (template.HasTag(
                    DreamRoomTag.StartCandidate))
            {
                startCandidates++;

                if (template.MaximumInstancesPerFloor != 1)
                {
                    errors.Add(
                        template.TemplateId +
                        " 的 MaximumInstancesPerFloor 应为 1。" );
                }
            }

            if (template.HasTag(
                    DreamRoomTag.ExitCandidate))
            {
                exitCandidates++;

                if (template.MaximumInstancesPerFloor != 1)
                {
                    errors.Add(
                        template.TemplateId +
                        " 的 MaximumInstancesPerFloor 应为 1。" );
                }
            }
        }

        if (startCandidates != 1)
        {
            errors.Add(
                "Test Catalog 应有 1 个 StartCandidate，实际为 " +
                startCandidates + "。" );
        }

        if (exitCandidates != 1)
        {
            errors.Add(
                "Test Catalog 应有 1 个 ExitCandidate，实际为 " +
                exitCandidates + "。" );
        }
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

        return layoutErrors.Count == 0;
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
            errors.Add(label + " 的 Layout 为空。" );
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

        metrics.FallbackCount =
            (metrics.StartTagged ? 0 : 1) +
            (metrics.ExitTagged ? 0 : 1);

        if (metrics.RoomCount != ExpectedRoomCount)
        {
            errors.Add(
                label + " 应生成 " + ExpectedRoomCount +
                " 间房，实际为 " +
                metrics.RoomCount + "。" );
        }

        if (metrics.StartRoomIndex < 0 ||
            metrics.ExitRoomIndex < 0)
        {
            errors.Add(
                label +
                " 的 StartCell／ExitCell 未落入房间 Walkable Cells。" );
        }

        if (metrics.StartRoomIndex ==
            metrics.ExitRoomIndex)
        {
            errors.Add(
                label + " 的 Start 与 Exit 使用了同一房间。" );
        }

        if (requireTaggedRoles)
        {
            if (!metrics.StartTagged)
            {
                errors.Add(
                    label +
                    " 的 Start Room 没有 StartCandidate 标签。" );
            }

            if (!metrics.ExitTagged)
            {
                errors.Add(
                    label +
                    " 的 Exit Room 没有 ExitCandidate 标签。" );
            }

            if (metrics.FallbackCount != 0)
            {
                errors.Add(
                    label + " 不应发生 Standard 回退。" );
            }
        }
        else
        {
            if (metrics.StartCandidateCount != 0 ||
                metrics.ExitCandidateCount != 0)
            {
                errors.Add(
                    label +
                    " 预期没有 Start／Exit 标签候选。" );
            }

            if (!metrics.StartIsStandard ||
                !metrics.ExitIsStandard)
            {
                errors.Add(
                    label +
                    " 没有把 Start／Exit 安全回退到 Standard。" );
            }

            if (metrics.StartIsSpecial ||
                metrics.ExitIsSpecial)
            {
                errors.Add(
                    label +
                    " 把带 Special 的房间用于了普通回退。" );
            }

            if (metrics.FallbackCount != 2)
            {
                errors.Add(
                    label + " 应记录 2 个角色回退。" );
            }
        }

        if (requireUniqueTaggedRoles &&
            (metrics.StartCandidateCount != 1 ||
             metrics.ExitCandidateCount != 1))
        {
            errors.Add(
                label +
                " 应正好放置 1 个 StartCandidate 和 1 个 ExitCandidate。" );
        }
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

    private static string BuildLayoutSignature(
        DungeonLayout layout)
    {
        if (layout == null)
        {
            return "<null>";
        }

        StringBuilder builder = new StringBuilder();

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

            if (placement == null)
            {
                builder.Append("<null>");
                continue;
            }

            builder.Append(
                FormatTemplateId(
                    placement.Template));
            builder.Append('@');
            builder.Append(placement.MinimumCell);
            builder.Append('#');
            builder.Append(
                DreamRoomPlacement.NormalizeQuarterTurns(
                    placement.ClockwiseQuarterTurns));
        }

        for (int i = 0;
             i < layout.Connections.Count;
             i++)
        {
            builder.Append("|C");
            builder.Append(i);
            builder.Append('=');
            builder.Append(layout.Connections[i]);
        }

        return builder.ToString();
    }

    private static string FormatMetrics(
        string label,
        string catalogId,
        RoleMetrics metrics)
    {
        return
            label + "Catalog=" + catalogId +
            " | Rooms=" + metrics.RoomCount +
            "/" + ExpectedRoomCount +
            " | Start=" + metrics.StartTemplateId +
            "(Tagged=" + metrics.StartTagged + ")" +
            " | Exit=" + metrics.ExitTemplateId +
            "(Tagged=" + metrics.ExitTagged + ")" +
            " | StartCandidates=" +
            metrics.StartCandidateCount +
            " | ExitCandidates=" +
            metrics.ExitCandidateCount +
            " | Fallbacks=" +
            metrics.FallbackCount;
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

    private static DreamRoomTemplate LoadTemplateAsset(
        string prefabPath)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);

        return prefab == null
            ? null
            : prefab.GetComponent<DreamRoomTemplate>();
    }

    private static DreamRoomCatalog LoadCatalog(
        string path,
        string expectedId,
        string label,
        List<string> errors)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                path);

        if (catalog == null)
        {
            errors.Add(label + " 不存在：" + path);
            return null;
        }

        if (!string.Equals(
                catalog.CatalogId,
                expectedId,
                StringComparison.Ordinal))
        {
            errors.Add(
                label + " 的 CatalogId 应为 " +
                expectedId + "，实际为 " +
                catalog.CatalogId + "。" );
        }

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                label + "：" + catalogErrors[i]);
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
            errors.Add("必须先退出 Play Mode。" );
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。" );
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("当前没有有效 Scene。" );
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
                "GameScene 当前有未保存修改。请先确认 Catalog 为 Graybox，" +
                "再保存场景。" );
        }

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。" );
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。" );
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
    /// 只供编辑器只读审计使用。直接写入并在 finally 中恢复，
    /// 不调用 SerializedObject／SetDirty，因此不会把临时 Catalog
    /// 记录为 Scene 修改。
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

    private static void RequireBool(
        UnityEngine.Object target,
        string propertyName,
        bool expectedValue,
        string label,
        List<string> errors)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(label + " 字段不存在。" );
            return;
        }

        if (property.boolValue != expectedValue)
        {
            errors.Add(
                label + " 应为 " + expectedValue +
                "，实际为 " + property.boolValue + "。" );
        }
    }

    private static void RequireInt(
        UnityEngine.Object target,
        string propertyName,
        int expectedValue,
        string label,
        List<string> errors)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(label + " 字段不存在。" );
            return;
        }

        if (property.intValue != expectedValue)
        {
            errors.Add(
                label + " 应为 " + expectedValue +
                "，实际为 " + property.intValue + "。" );
        }
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.stringValue = value;
    }

    private static void SetInt(
        SerializedObject serializedObject,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.intValue = value;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.objectReferenceValue = value;
    }

    private static void SetObjectReferenceArray<T>(
        SerializedObject serializedObject,
        string propertyName,
        T[] values)
        where T : UnityEngine.Object
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" + propertyName);
        }

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i)
                .objectReferenceValue = values[i];
        }
    }

    private static void EnsureAssetFolder(
        string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent =
            folderPath.Substring(
                0,
                folderPath.LastIndexOf('/'));

        string name =
            folderPath.Substring(
                folderPath.LastIndexOf('/') + 1);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
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
            builder.AppendLine("- 未提供具体错误。" );
        }
        else
        {
            for (int i = 0; i < errors.Count; i++)
            {
                builder.AppendLine("- " + errors[i]);
            }
        }

        Debug.LogError(builder.ToString(), context);

        EditorUtility.DisplayDialog(
            title,
            "操作未完成。请保留 Console 第一条完整红错。",
            "OK");
    }

    private readonly struct RolePrefabSpec
    {
        public string TemplateId { get; }
        public string FileName { get; }
        public DreamRoomTag Tags { get; }
        public int RandomWeight { get; }
        public int MaximumInstances { get; }

        public RolePrefabSpec(
            string templateId,
            string fileName,
            DreamRoomTag tags,
            int randomWeight,
            int maximumInstances)
        {
            TemplateId = templateId;
            FileName = fileName;
            Tags = tags;
            RandomWeight = randomWeight;
            MaximumInstances = maximumInstances;
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
        public int FallbackCount;
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
}
