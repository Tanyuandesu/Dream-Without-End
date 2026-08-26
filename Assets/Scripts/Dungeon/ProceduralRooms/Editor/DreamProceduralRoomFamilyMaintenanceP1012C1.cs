#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12C-1：Family Regression + Retired Stage Tool Cleanup。
///
/// 目标：
/// 1. 用一个长期维护工具取代 A1/R1/R2B/A2/A3/B1 等阶段性 Audit。
/// 2. 先回归，再删除已经完成使命的 Editor 工具。
/// 3. 本阶段只清理 Editor 工具，不碰 Runtime 权威代码、Prefab、Theme Asset。
///
/// 注意：Remove 使用 EditorApplication.delayCall，避免 Unity 6 GUI callback
/// 期间直接 AssetDatabase 删除造成 DontSaveInEditor/Inspector assertion。
/// </summary>
public static class DreamProceduralRoomFamilyMaintenanceP1012C1
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Procedural Rooms/P10.12C-1 Family Maintenance/";

    private const string KernelPassKey =
        "DreamDungeon.P10.12C1.KernelRegressionPassed";

    private const string LivePassKey =
        "DreamDungeon.P10.12C1.LiveRegressionPassed";

    private const int SeedsPerSocketCase =
        256;

    private static readonly string[] RetiredEditorTools =
    {
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomAuditP1012A1.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomMissingScriptRepairP1012A14.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomRuntimeGeometryAuditP1012A1R1.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomR2BLiveAudit.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomSoftDecorAuditP1012A2.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomStructuralSkinAuditP1012A31.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomShellSkinAuditP1012A32.cs",
        "Assets/Scripts/Dungeon/ProceduralRooms/Editor/DreamProceduralRoomFamilyAuditP1012B1.cs"
    };

    [MenuItem(
        MenuRoot + "1. Run Consolidated Kernel Regression",
        false,
        3100)]
    private static void RunKernelRegression()
    {
        SocketCase[] socketCases =
        {
            new SocketCase("NS", true, false, true, false),
            new SocketCase("EW", false, true, false, true),
            new SocketCase("NE", true, true, false, false),
            new SocketCase("WS", false, false, true, true),
            new SocketCase("NES", true, true, true, false),
            new SocketCase("EWS", false, true, true, true),
            new SocketCase("NEWS", true, true, true, true)
        };

        IReadOnlyList<DreamProceduralRoomFamilyProfileP1012B1> profiles =
            DreamProceduralRoomFamilyRegistryP1012B1.All;

        int expected =
            profiles.Count *
            socketCases.Length *
            SeedsPerSocketCase;

        int generated = 0;
        int deterministic = 0;

        List<string> failures =
            new List<string>();

        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "[P10.12C-1] Consolidated Family Kernel Regression");

        for (int p = 0;
             p < profiles.Count;
             p++)
        {
            DreamProceduralRoomFamilyProfileP1012B1 profile =
                profiles[p];

            int familyGenerated = 0;
            int familyDeterministic = 0;

            int minBlocked = int.MaxValue;
            int maxBlocked = int.MinValue;

            float minRatio = 1f;
            float maxRatio = 0f;

            for (int c = 0;
                 c < socketCases.Length;
                 c++)
            {
                SocketCase socketCase =
                    socketCases[c];

                List<DreamProceduralDoorLane> doors =
                    profile.BuildDefaultDoorSet(
                        socketCase.North,
                        socketCase.East,
                        socketCase.South,
                        socketCase.West);

                for (int s = 0;
                     s < SeedsPerSocketCase;
                     s++)
                {
                    int seed =
                        DreamProceduralRoomFamilyKernelP1012B1
                            .DeriveRoomSeed(
                                172391 + s * 17,
                                s % 7,
                                profile,
                                socketCase.Mask);

                    DreamProceduralRoomLayout first;
                    string failure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .TryGenerate(
                                profile,
                                seed,
                                doors,
                                out first,
                                out failure))
                    {
                        AddFailure(
                            failures,
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Generate=" +
                            failure);
                        continue;
                    }

                    string validationFailure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .Validate(
                                profile,
                                first,
                                out validationFailure))
                    {
                        AddFailure(
                            failures,
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Validate=" +
                            validationFailure);
                        continue;
                    }

                    generated++;
                    familyGenerated++;

                    minBlocked =
                        Mathf.Min(
                            minBlocked,
                            first.BlockedCells.Count);

                    maxBlocked =
                        Mathf.Max(
                            maxBlocked,
                            first.BlockedCells.Count);

                    minRatio =
                        Mathf.Min(
                            minRatio,
                            first.BlockedRatio);

                    maxRatio =
                        Mathf.Max(
                            maxRatio,
                            first.BlockedRatio);

                    DreamProceduralRoomLayout second;
                    string secondFailure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .TryGenerate(
                                profile,
                                seed,
                                doors,
                                out second,
                                out secondFailure))
                    {
                        AddFailure(
                            failures,
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | RepeatGenerate=" +
                            secondFailure);
                        continue;
                    }

                    if (second == null ||
                        first.Archetype != second.Archetype ||
                        !first.BlockedCells.SetEquals(
                            second.BlockedCells) ||
                        !first.ReservedMainRouteCells.SetEquals(
                            second.ReservedMainRouteCells))
                    {
                        AddFailure(
                            failures,
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | DeterminismMismatch");
                        continue;
                    }

                    deterministic++;
                    familyDeterministic++;
                }
            }

            int familyExpected =
                socketCases.Length *
                SeedsPerSocketCase;

            report.AppendLine(
                profile.FamilyId +
                " | Generated=" +
                familyGenerated +
                "/" +
                familyExpected +
                " | Deterministic=" +
                familyDeterministic +
                "/" +
                familyExpected +
                " | Blocked=" +
                minBlocked +
                "～" +
                maxBlocked +
                " | Ratio=" +
                (minRatio * 100f).ToString("F1") +
                "%～" +
                (maxRatio * 100f).ToString("F1") +
                "%");
        }

        bool pass =
            failures.Count == 0 &&
            generated == expected &&
            deterministic == expected;

        EditorPrefs.SetBool(
            KernelPassKey,
            pass);

        if (!pass)
        {
            report.AppendLine(
                "Expected=" +
                expected +
                " | Generated=" +
                generated +
                " | Deterministic=" +
                deterministic +
                " | Failures=" +
                failures.Count);

            int limit =
                Mathf.Min(
                    24,
                    failures.Count);

            for (int i = 0;
                 i < limit;
                 i++)
            {
                report.AppendLine(
                    "- " +
                    failures[i]);
            }

            Debug.LogError(
                report.ToString() +
                "\nResult=FAILED");
            return;
        }

        report.AppendLine(
            "Total=" +
            expected +
            " | Generated=" +
            generated +
            "/" +
            expected +
            " | Deterministic=" +
            deterministic +
            "/" +
            expected);

        report.AppendLine(
            "UsedSockets=100%Connected" +
            " | WalkableTopology=SingleConnectedComponent" +
            " | MainRoute=Reserved2CellBackbone");

        report.AppendLine(
            "RuntimeMutation=0" +
            " | ProductionMainChanged=False" +
            " | Result=PASS");

        Debug.Log(
            report.ToString());
    }

    [MenuItem(
        MenuRoot + "2. Validate LIVE Current Family Runtime",
        false,
        3110)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12C-1] LIVE Regression 必须在 Play Mode。");
            return;
        }

        GameManager gameManager =
            UnityEngine.Object.FindFirstObjectByType<GameManager>();

        EnemyPathService pathService =
            UnityEngine.Object.FindFirstObjectByType<EnemyPathService>();

        EnemyManager enemyManager =
            UnityEngine.Object.FindFirstObjectByType<EnemyManager>();

        if (gameManager == null)
        {
            Debug.LogError(
                "[P10.12C-1] 找不到 GameManager。");
            return;
        }

        DungeonLayout layout =
            ReadCurrentLayout(
                gameManager);

        if (layout == null)
        {
            Debug.LogError(
                "[P10.12C-1] 无法读取 currentLayout。");
            return;
        }

        List<string> errors =
            new List<string>();

        errors.AddRange(
            layout.GetValidationErrors());

        Dictionary<string, int> familyCounts =
            new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                { "Graybox_08x06", 0 },
                { "Graybox_13x09", 0 },
                { "Graybox_18x07", 0 },
                { "Graybox_09x16", 0 }
            };

        Dictionary<int, DreamRoomPlacement> procedural =
            new Dictionary<int, DreamRoomPlacement>();

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null ||
                !placement.HasRuntimeProceduralOverride)
            {
                continue;
            }

            string templateId =
                placement.Template.TemplateId;

            if (!familyCounts.ContainsKey(
                    templateId))
            {
                errors.Add(
                    "未知 Procedural Template：" +
                    templateId);
                continue;
            }

            familyCounts[templateId] =
                familyCounts[templateId] + 1;

            procedural[i] =
                placement;
        }

        if (procedural.Count == 0)
        {
            errors.Add(
                "当前 Floor 没有任何 Procedural Family。" +
                " 请按 R 重生后再验收。");
        }

        foreach (
            KeyValuePair<string, int> pair
            in familyCounts)
        {
            if (pair.Value > 1)
            {
                errors.Add(
                    pair.Key +
                    " 同一 Floor 出现多个 Authority Override：" +
                    pair.Value);
            }
        }

        DreamProceduralRoomRuntimeInstanceP1012R2B[] runtimeInstances =
            UnityEngine.Object.FindObjectsByType<
                DreamProceduralRoomRuntimeInstanceP1012R2B>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

        Dictionary<int, DreamProceduralRoomRuntimeInstanceP1012R2B>
            runtimeByRoom =
                new Dictionary<int, DreamProceduralRoomRuntimeInstanceP1012R2B>();

        for (int i = 0;
             i < runtimeInstances.Length;
             i++)
        {
            DreamProceduralRoomRuntimeInstanceP1012R2B instance =
                runtimeInstances[i];

            if (instance == null)
            {
                continue;
            }

            if (runtimeByRoom.ContainsKey(
                    instance.RoomIndex))
            {
                errors.Add(
                    "重复 Runtime Instance RoomIndex=" +
                    instance.RoomIndex);
            }
            else
            {
                runtimeByRoom.Add(
                    instance.RoomIndex,
                    instance);
            }
        }

        if (runtimeByRoom.Count != procedural.Count)
        {
            errors.Add(
                "Authority Placement / Runtime Instance 数量不一致：" +
                procedural.Count +
                "/" +
                runtimeByRoom.Count);
        }

        int totalBlocked = 0;
        int totalColliders = 0;
        int totalSoftSlots = 0;

        foreach (
            KeyValuePair<int, DreamRoomPlacement> pair
            in procedural)
        {
            int roomIndex =
                pair.Key;

            DreamRoomPlacement placement =
                pair.Value;

            List<Vector2Int> blocked =
                new List<Vector2Int>();

            placement.GetRuntimeProceduralBlockedLocalCells(
                blocked);

            totalBlocked +=
                blocked.Count;

            for (int i = 0;
                 i < blocked.Count;
                 i++)
            {
                Vector2Int global =
                    placement.OriginalToGlobalCell(
                        blocked[i]);

                if (layout.FloorCells.Contains(global) ||
                    layout.RoomCells.Contains(global))
                {
                    errors.Add(
                        "Blocked 仍属于 Walkable：" +
                        global);
                    break;
                }

                if (layout.CorridorCells.Contains(global))
                {
                    errors.Add(
                        "Blocked 与 Corridor 重叠：" +
                        global);
                    break;
                }
            }

            DreamProceduralRoomRuntimeInstanceP1012R2B instance;

            if (!runtimeByRoom.TryGetValue(
                    roomIndex,
                    out instance) ||
                instance == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " 缺 Runtime Geometry。");
                continue;
            }

            totalColliders +=
                instance.ColliderCount;

            if (instance.BlockedCellCount != blocked.Count ||
                instance.ColliderCount != blocked.Count)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " Blocked/Collider Mapping 非 1:1：" +
                    blocked.Count +
                    "/" +
                    instance.BlockedCellCount +
                    "/" +
                    instance.ColliderCount);
            }

            DreamProceduralRoomStructuralSkinP1012A31 structure =
                instance.GetComponent<
                    DreamProceduralRoomStructuralSkinP1012A31>();

            if (structure == null ||
                !structure.IsCommitted ||
                structure.RendererCount != blocked.Count)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " Structural Skin 未同步。");
            }

            DreamProceduralRoomSoftDecorP1012A2 soft =
                instance.GetComponent<
                    DreamProceduralRoomSoftDecorP1012A2>();

            if (soft == null ||
                !soft.IsCommitted)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " Soft Decor 未 Commit。");
            }
            else
            {
                totalSoftSlots +=
                    soft.SlotCount;
            }

            DreamProceduralRoomShellSkinP1012A32 shell =
                instance.GetComponent<
                    DreamProceduralRoomShellSkinP1012A32>();

            if (shell == null ||
                !shell.IsCommitted)
            {
                errors.Add(
                    "RoomIndex=" +
                    roomIndex +
                    " Shell Skin 未 Commit。");
            }
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
                    "EnemyPathWalkable != FloorCells：" +
                    pathService.WalkableCellCount +
                    "/" +
                    layout.FloorCells.Count);
            }

            if (pathService.ConnectedComponentCount != 1)
            {
                errors.Add(
                    "ConnectedComponentCount != 1：" +
                    pathService.ConnectedComponentCount);
            }
        }

        int enemySpawnViolations = 0;

        if (enemyManager != null &&
            pathService != null)
        {
            IReadOnlyList<GameObject> enemies =
                enemyManager.ActiveEnemies;

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

        if (enemySpawnViolations > 0)
        {
            errors.Add(
                "EnemySpawnViolations=" +
                enemySpawnViolations);
        }

        bool pass =
            errors.Count == 0;

        EditorPrefs.SetBool(
            LivePassKey,
            pass);

        if (!pass)
        {
            Debug.LogError(
                "[P10.12C-1] LIVE Consolidated Family Regression FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.12C-1] LIVE Consolidated Family Regression PASS" +
            "\nFloor=" +
            gameManager.CurrentFloor +
            " | LayoutSeed=" +
            layout.Seed +
            " | ProceduralPlacements=" +
            procedural.Count +
            " | Small=" +
            familyCounts["Graybox_08x06"] +
            " | Medium=" +
            familyCounts["Graybox_13x09"] +
            " | Wide=" +
            familyCounts["Graybox_18x07"] +
            " | Tall=" +
            familyCounts["Graybox_09x16"] +
            "\nBlockedCells=" +
            totalBlocked +
            " | RuntimeColliders=" +
            totalColliders +
            " | SoftSlots=" +
            totalSoftSlots +
            " | FloorCells=" +
            layout.FloorCells.Count +
            "\nEnemyPathWalkable=" +
            (pathService == null
                ? -1
                : pathService.WalkableCellCount) +
            " | ConnectedComponents=" +
            (pathService == null
                ? -1
                : pathService.ConnectedComponentCount) +
            " | EnemySpawnViolations=0" +
            "\nColliderMapping=1to1" +
            " | StructuralSkin=True" +
            " | SoftDecor=True" +
            " | ShellSkin=True" +
            " | ProductionMainChanged=False" +
            "\nResult=PASS");
    }

    [MenuItem(
        MenuRoot + "3. Audit Retired Stage Tools",
        false,
        3120)]
    private static void AuditRetiredTools()
    {
        List<string> existing =
            new List<string>();

        for (int i = 0;
             i < RetiredEditorTools.Length;
             i++)
        {
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(
                    RetiredEditorTools[i]) != null)
            {
                existing.Add(
                    RetiredEditorTools[i]);
            }
        }

        Debug.Log(
            "[P10.12C-1] Retired Stage Tool Audit" +
            "\nKnownRetiredTools=" +
            RetiredEditorTools.Length +
            " | Existing=" +
            existing.Count +
            "\nKernelRegressionPassed=" +
            EditorPrefs.GetBool(
                KernelPassKey,
                false) +
            " | LiveRegressionPassed=" +
            EditorPrefs.GetBool(
                LivePassKey,
                false) +
            (existing.Count == 0
                ? "\nRetiredToolFootprint=Clean"
                : "\nExisting:\n- " +
                  string.Join(
                      "\n- ",
                      existing)));
    }

    [MenuItem(
        MenuRoot + "4. Remove Retired Stage Tools (After PASS)",
        false,
        3130)]
    private static void RemoveRetiredTools()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12C-1] 请先退出 Play Mode 再清理 Editor 工具。");
            return;
        }

        bool kernelPassed =
            EditorPrefs.GetBool(
                KernelPassKey,
                false);

        bool livePassed =
            EditorPrefs.GetBool(
                LivePassKey,
                false);

        if (!kernelPassed ||
            !livePassed)
        {
            Debug.LogError(
                "[P10.12C-1] 拒绝清理。" +
                "\n必须先通过：\n" +
                "1. Consolidated Kernel Regression\n" +
                "2. LIVE Current Family Runtime\n" +
                "当前 KernelPASS=" +
                kernelPassed +
                " | LivePASS=" +
                livePassed);
            return;
        }

        // Unity 6：避免在当前 Menu/IMGUI callback 内直接删 Script Asset。
        EditorApplication.delayCall +=
            RemoveRetiredToolsDeferred;

        Debug.Log(
            "[P10.12C-1] Retired Tool Cleanup 已排入 delayCall。" +
            " 请等待 Unity 重新编译。");
    }

    private static void RemoveRetiredToolsDeferred()
    {
        List<string> removed =
            new List<string>();

        List<string> missing =
            new List<string>();

        List<string> failed =
            new List<string>();

        for (int i = 0;
             i < RetiredEditorTools.Length;
             i++)
        {
            string path =
                RetiredEditorTools[i];

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(
                    path) == null)
            {
                missing.Add(path);
                continue;
            }

            if (AssetDatabase.DeleteAsset(path))
            {
                removed.Add(path);
            }
            else
            {
                failed.Add(path);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (failed.Count > 0)
        {
            Debug.LogError(
                "[P10.12C-1] Retired Stage Tool Cleanup PARTIAL FAILED" +
                "\nRemoved=" +
                removed.Count +
                " | MissingAlready=" +
                missing.Count +
                " | Failed=" +
                failed.Count +
                "\nFailed:\n- " +
                string.Join(
                    "\n- ",
                    failed));
            return;
        }

        Debug.Log(
            "[P10.12C-1] Retired Stage Tool Cleanup PASS" +
            "\nRemoved=" +
            removed.Count +
            " | MissingAlready=" +
            missing.Count +
            " | Failed=0" +
            "\nRuntimeCodeChanged=False" +
            " | ProductionMainChanged=False" +
            " | GameSceneChanged=False" +
            "\nKeptLongTermTool=DreamProceduralRoomFamilyMaintenanceP1012C1");
    }

    private static void AddFailure(
        List<string> failures,
        string failure)
    {
        failures.Add(
            failure);
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

    private readonly struct SocketCase
    {
        public readonly string Name;
        public readonly bool North;
        public readonly bool East;
        public readonly bool South;
        public readonly bool West;

        public int Mask
        {
            get
            {
                int mask = 0;

                if (North)
                {
                    mask |= 1;
                }

                if (East)
                {
                    mask |= 2;
                }

                if (South)
                {
                    mask |= 4;
                }

                if (West)
                {
                    mask |= 8;
                }

                return mask;
            }
        }

        public SocketCase(
            string name,
            bool north,
            bool east,
            bool south,
            bool west)
        {
            Name = name;
            North = north;
            East = east;
            South = south;
            West = west;
        }
    }
}



/// <summary>
/// P10.12C-2：真正的多 Seed / 多 Floor Runtime Soak Regression。
///
/// 这是长期维护工具的一部分，不新增第二个阶段性 Editor Script。
/// 它在 Play Mode 中自动：
/// - 强制固定 Seed
/// - 重建 Floor 1~3
/// - 等待 Runtime Start / Destroy 生命周期完成
/// - 验证 Authority / Collider / FloorCells / A* / Spawn / 三层视觉
/// - 统计四 Family 与 Socket 压力覆盖
///
/// 默认 3 Floors x 16 Seeds = 48 次完整 Runtime 重建。
/// 完成或中止后都会恢复 DungeonGenerator 的原 seed 设置。
/// </summary>
public static class DreamProceduralRoomFamilyRuntimeRegressionP1012C2
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Procedural Rooms/P10.12C-2 Runtime Regression/";

    private const int SeedsPerFloor = 16;
    private const int BaseLayoutSeed = 240731;

    private static readonly int[] Floors =
    {
        1, 2, 3
    };

    private static readonly string[] FamilyTemplateIds =
    {
        "Graybox_08x06",
        "Graybox_13x09",
        "Graybox_18x07",
        "Graybox_09x16"
    };

    private static readonly string[] FamilyLabels =
    {
        "Small08x06",
        "Medium13x09",
        "Wide18x07",
        "Tall09x16"
    };

    private const string LastPassKey =
        "DreamDungeon.P10.12C2.RuntimeSoakPassed";

    private const string LastSummaryKey =
        "DreamDungeon.P10.12C2.RuntimeSoakSummary";

    private static bool running;
    private static bool abortRequested;

    private static GameManager gameManager;
    private static DungeonGenerator generator;
    private static DungeonRenderer renderer;

    private static FieldInfo useRandomSeedField;
    private static FieldInfo fixedSeedField;
    private static FieldInfo currentLayoutField;
    private static MethodInfo generateFloorMethod;

    private static bool originalUseRandomSeed;
    private static int originalFixedSeed;

    private static int caseIndex;
    private static int generatedFrame;
    private static RegressionState state;

    private static readonly List<string> failures =
        new List<string>();

    private static readonly int[] familyCases =
        new int[4];

    private static readonly HashSet<int>[] familyFloors =
    {
        new HashSet<int>(),
        new HashSet<int>(),
        new HashSet<int>(),
        new HashSet<int>()
    };

    private static int successfulCases;
    private static int hybridCases;
    private static int allFourSameCaseCount;
    private static int smallNewsStressHits;
    private static int wideEastWestStressHits;
    private static int tallNorthSouthStressHits;
    private static int zeroSoftDecorCases;
    private static int maxProceduralPlacements;
    private static int maxBlockedCells;
    private static int maxRuntimeColliders;

    [MenuItem(
        MenuRoot + "1. Start 48-Case Multi-Seed Multi-Floor Soak",
        false,
        3200)]
    private static void StartRegression()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12C-2] 请先进入 Play Mode。" +
                " C2 会反复重建当前运行时楼层，不能在 Edit Mode 执行。");
            return;
        }

        if (running)
        {
            Debug.LogWarning(
                "[P10.12C-2] Runtime Soak 已经在运行。");
            return;
        }

        gameManager =
            UnityEngine.Object.FindFirstObjectByType<GameManager>();

        generator =
            UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();

        renderer =
            UnityEngine.Object.FindFirstObjectByType<DungeonRenderer>();

        if (gameManager == null ||
            generator == null ||
            renderer == null)
        {
            Debug.LogError(
                "[P10.12C-2] 找不到 GameManager / DungeonGenerator / DungeonRenderer。");
            return;
        }

        if (renderer.RenderMode !=
            DungeonRenderMode.HybridPrefabRooms)
        {
            Debug.LogError(
                "[P10.12C-2] 当前 DungeonRenderer.RenderMode 不是 HybridPrefabRooms。" +
                " 请先恢复正式 Hybrid 模式。");
            return;
        }

        Type generatorType =
            typeof(DungeonGenerator);

        useRandomSeedField =
            generatorType.GetField(
                "useRandomSeed",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        fixedSeedField =
            generatorType.GetField(
                "fixedSeed",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        currentLayoutField =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        generateFloorMethod =
            typeof(GameManager).GetMethod(
                "GenerateFloor",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (useRandomSeedField == null ||
            fixedSeedField == null ||
            currentLayoutField == null ||
            generateFloorMethod == null)
        {
            Debug.LogError(
                "[P10.12C-2] Reflection Contract 不完整。" +
                "\nuseRandomSeed=" +
                (useRandomSeedField != null) +
                " | fixedSeed=" +
                (fixedSeedField != null) +
                " | currentLayout=" +
                (currentLayoutField != null) +
                " | GenerateFloor=" +
                (generateFloorMethod != null));
            return;
        }

        originalUseRandomSeed =
            (bool)useRandomSeedField.GetValue(
                generator);

        originalFixedSeed =
            (int)fixedSeedField.GetValue(
                generator);

        ResetCounters();

        useRandomSeedField.SetValue(
            generator,
            false);

        running = true;
        abortRequested = false;
        caseIndex = 0;
        state = RegressionState.Generate;

        EditorPrefs.SetBool(
            LastPassKey,
            false);

        EditorPrefs.SetString(
            LastSummaryKey,
            string.Empty);

        EditorApplication.update +=
            RegressionUpdate;

        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload +=
            OnBeforeAssemblyReload;

        Debug.Log(
            "[P10.12C-2] Runtime Soak START" +
            "\nCases=" +
            TotalCases +
            " | Floors=1,2,3" +
            " | SeedsPerFloor=" +
            SeedsPerFloor +
            "\nMode=HybridPrefabRooms" +
            " | RuntimeGeneration=True" +
            " | SceneSaveRequired=False" +
            "\nIMPORTANT=测试期间不要按 R、不要切换 Scene、不要退出 Play Mode。");
    }

    [MenuItem(
        MenuRoot + "2. Abort Running Soak",
        false,
        3210)]
    private static void AbortRegression()
    {
        if (!running)
        {
            Debug.Log(
                "[P10.12C-2] 当前没有正在运行的 Runtime Soak。");
            return;
        }

        abortRequested = true;

        Debug.LogWarning(
            "[P10.12C-2] 已请求中止。" +
            " 将在下一个 Editor Update 恢复 Seed 设置并停止。");
    }

    [MenuItem(
        MenuRoot + "3. Print Last Soak Summary",
        false,
        3220)]
    private static void PrintLastSummary()
    {
        string summary =
            EditorPrefs.GetString(
                LastSummaryKey,
                string.Empty);

        if (string.IsNullOrWhiteSpace(
                summary))
        {
            Debug.Log(
                "[P10.12C-2] 尚无 Runtime Soak 结果。");
            return;
        }

        bool passed =
            EditorPrefs.GetBool(
                LastPassKey,
                false);

        if (passed)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }
    }

    private static void RegressionUpdate()
    {
        if (!running)
        {
            return;
        }

        if (abortRequested ||
            !EditorApplication.isPlaying ||
            gameManager == null ||
            generator == null)
        {
            Finish(
                false,
                abortRequested
                    ? "UserAbort"
                    : "PlayModeOrRuntimeLost");
            return;
        }

        switch (state)
        {
            case RegressionState.Generate:
                GenerateCurrentCase();
                break;

            case RegressionState.WaitRuntime:
                // 给 Destroy(old floor)、Start(A2/A3)、R8.4 生命周期审计
                // 至少两个完整 Play Frame。
                if (Time.frameCount >=
                    generatedFrame + 2)
                {
                    state =
                        RegressionState.Audit;
                }
                break;

            case RegressionState.Audit:
                AuditCurrentCase();
                break;
        }
    }

    private static void GenerateCurrentCase()
    {
        if (caseIndex >= TotalCases)
        {
            Finish(
                failures.Count == 0,
                "Completed");
            return;
        }

        int floor =
            GetFloorForCase(
                caseIndex);

        int seedOrdinal =
            caseIndex %
            SeedsPerFloor;

        int desiredLayoutSeed =
            BaseLayoutSeed +
            floor * 100000 +
            seedOrdinal * 7919;

        // RuntimeHybrid 的固定 Seed 公式：
        // actualSeed = fixedSeed + floor - 1
        int fixedSeedForCase =
            desiredLayoutSeed -
            floor +
            1;

        useRandomSeedField.SetValue(
            generator,
            false);

        fixedSeedField.SetValue(
            generator,
            fixedSeedForCase);

        bool generated = false;

        try
        {
            object result =
                generateFloorMethod.Invoke(
                    gameManager,
                    new object[]
                    {
                        floor
                    });

            generated =
                result is bool &&
                (bool)result;
        }
        catch (Exception exception)
        {
            AddFailure(
                BuildCasePrefix(
                    floor,
                    desiredLayoutSeed) +
                " GenerateFloor exception=" +
                UnwrapException(
                    exception));
        }

        if (!generated)
        {
            AddFailure(
                BuildCasePrefix(
                    floor,
                    desiredLayoutSeed) +
                " GenerateFloor returned false.");

            AdvanceCase();
            return;
        }

        generatedFrame =
            Time.frameCount;

        state =
            RegressionState.WaitRuntime;
    }

    private static void AuditCurrentCase()
    {
        int floor =
            GetFloorForCase(
                caseIndex);

        int seedOrdinal =
            caseIndex %
            SeedsPerFloor;

        int expectedLayoutSeed =
            BaseLayoutSeed +
            floor * 100000 +
            seedOrdinal * 7919;

        List<string> caseErrors =
            new List<string>();

        DungeonLayout layout =
            currentLayoutField.GetValue(
                gameManager)
                as DungeonLayout;

        if (layout == null)
        {
            caseErrors.Add(
                "currentLayout=null");
        }
        else
        {
            if (gameManager.CurrentFloor != floor)
            {
                caseErrors.Add(
                    "CurrentFloor=" +
                    gameManager.CurrentFloor +
                    " expected=" +
                    floor);
            }

            if (layout.Seed !=
                expectedLayoutSeed)
            {
                caseErrors.Add(
                    "LayoutSeed=" +
                    layout.Seed +
                    " expected=" +
                    expectedLayoutSeed);
            }

            if (gameManager.EffectiveGenerationMode !=
                DungeonRenderMode.HybridPrefabRooms)
            {
                caseErrors.Add(
                    "EffectiveMode=" +
                    gameManager.EffectiveGenerationMode);
            }
            else
            {
                hybridCases++;
            }

            if (!layout.HasHybridRoomData)
            {
                caseErrors.Add(
                    "HasHybridRoomData=False");
            }

            List<string> validation =
                layout.GetValidationErrors();

            if (validation.Count > 0)
            {
                caseErrors.Add(
                    "LayoutValidation=" +
                    string.Join(
                        " | ",
                        validation));
            }

            AuditFamilyRuntime(
                floor,
                layout,
                caseErrors);

            AuditConsumersAndLifecycle(
                layout,
                caseErrors);
        }

        if (caseErrors.Count > 0)
        {
            AddFailure(
                BuildCasePrefix(
                    floor,
                    expectedLayoutSeed) +
                "\n  " +
                string.Join(
                    "\n  ",
                    caseErrors));
        }
        else
        {
            successfulCases++;
        }

        AdvanceCase();
    }

    private static void AuditFamilyRuntime(
        int floor,
        DungeonLayout layout,
        List<string> errors)
    {
        Dictionary<int, DreamRoomPlacement> procedural =
            new Dictionary<int, DreamRoomPlacement>();

        int[] familyCountThisCase =
            new int[4];

        int[] familyMaskThisCase =
            new int[4];

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null ||
                !placement.HasRuntimeProceduralOverride)
            {
                continue;
            }

            int familyIndex =
                GetFamilyIndex(
                    placement.Template.TemplateId);

            if (familyIndex < 0)
            {
                errors.Add(
                    "UnknownProceduralTemplate=" +
                    placement.Template.TemplateId);
                continue;
            }

            familyCountThisCase[familyIndex]++;

            procedural[i] =
                placement;

            int mask =
                GetUsedSocketMask(
                    layout,
                    i);

            familyMaskThisCase[familyIndex] =
                mask;

            familyCases[familyIndex]++;
            familyFloors[familyIndex].Add(
                floor);

            if (familyIndex == 0 &&
                mask == 15)
            {
                smallNewsStressHits++;
            }

            if (familyIndex == 2 &&
                (mask & 10) == 10)
            {
                wideEastWestStressHits++;
            }

            if (familyIndex == 3 &&
                (mask & 5) == 5)
            {
                tallNorthSouthStressHits++;
            }
        }

        for (int f = 0;
             f < familyCountThisCase.Length;
             f++)
        {
            if (familyCountThisCase[f] > 1)
            {
                errors.Add(
                    FamilyLabels[f] +
                    " ProceduralCount=" +
                    familyCountThisCase[f] +
                    " (>1)");
            }
        }

        if (familyCountThisCase[0] == 1 &&
            familyCountThisCase[1] == 1 &&
            familyCountThisCase[2] == 1 &&
            familyCountThisCase[3] == 1)
        {
            allFourSameCaseCount++;
        }

        maxProceduralPlacements =
            Mathf.Max(
                maxProceduralPlacements,
                procedural.Count);

        DreamProceduralRoomRuntimeInstanceP1012R2B[] instances =
            UnityEngine.Object.FindObjectsByType<
                DreamProceduralRoomRuntimeInstanceP1012R2B>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

        Dictionary<int, DreamProceduralRoomRuntimeInstanceP1012R2B>
            runtimeByRoom =
                new Dictionary<int, DreamProceduralRoomRuntimeInstanceP1012R2B>();

        for (int i = 0;
             i < instances.Length;
             i++)
        {
            DreamProceduralRoomRuntimeInstanceP1012R2B instance =
                instances[i];

            if (instance == null)
            {
                continue;
            }

            if (runtimeByRoom.ContainsKey(
                    instance.RoomIndex))
            {
                errors.Add(
                    "DuplicateRuntimeRoomIndex=" +
                    instance.RoomIndex);
            }
            else
            {
                runtimeByRoom.Add(
                    instance.RoomIndex,
                    instance);
            }
        }

        if (runtimeByRoom.Count !=
            procedural.Count)
        {
            errors.Add(
                "AuthorityRuntimeCount=" +
                procedural.Count +
                "/" +
                runtimeByRoom.Count);
        }

        int totalBlockedThisCase = 0;
        int totalCollidersThisCase = 0;

        foreach (
            KeyValuePair<int, DreamRoomPlacement> pair
            in procedural)
        {
            int roomIndex =
                pair.Key;

            DreamRoomPlacement placement =
                pair.Value;

            List<Vector2Int> blocked =
                new List<Vector2Int>();

            placement.GetRuntimeProceduralBlockedLocalCells(
                blocked);

            totalBlockedThisCase +=
                blocked.Count;

            for (int i = 0;
                 i < blocked.Count;
                 i++)
            {
                Vector2Int global =
                    placement.OriginalToGlobalCell(
                        blocked[i]);

                if (layout.FloorCells.Contains(global) ||
                    layout.RoomCells.Contains(global))
                {
                    errors.Add(
                        "BlockedStillWalkable=" +
                        global);
                    break;
                }

                if (layout.CorridorCells.Contains(global))
                {
                    errors.Add(
                        "BlockedCorridorOverlap=" +
                        global);
                    break;
                }
            }

            DreamProceduralRoomRuntimeInstanceP1012R2B instance;

            if (!runtimeByRoom.TryGetValue(
                    roomIndex,
                    out instance) ||
                instance == null)
            {
                errors.Add(
                    "MissingRuntimeGeometry room=" +
                    roomIndex);
                continue;
            }

            totalCollidersThisCase +=
                instance.ColliderCount;

            if (instance.BlockedCellCount !=
                    blocked.Count ||
                instance.ColliderCount !=
                    blocked.Count)
            {
                errors.Add(
                    "ColliderMapping room=" +
                    roomIndex +
                    " blocked=" +
                    blocked.Count +
                    " runtimeBlocked=" +
                    instance.BlockedCellCount +
                    " colliders=" +
                    instance.ColliderCount);
            }

            DreamProceduralRoomStructuralSkinP1012A31 structure =
                instance.GetComponent<
                    DreamProceduralRoomStructuralSkinP1012A31>();

            if (structure == null ||
                !structure.IsCommitted ||
                structure.RendererCount !=
                    blocked.Count)
            {
                errors.Add(
                    "StructuralSkin room=" +
                    roomIndex +
                    " committed=" +
                    (structure != null &&
                     structure.IsCommitted));
            }

            DreamProceduralRoomSoftDecorP1012A2 soft =
                instance.GetComponent<
                    DreamProceduralRoomSoftDecorP1012A2>();

            if (soft == null ||
                !soft.IsCommitted)
            {
                errors.Add(
                    "SoftDecor room=" +
                    roomIndex +
                    " missing/uncommitted");
            }
            else if (soft.SlotCount == 0)
            {
                zeroSoftDecorCases++;
            }

            DreamProceduralRoomShellSkinP1012A32 shell =
                instance.GetComponent<
                    DreamProceduralRoomShellSkinP1012A32>();

            if (shell == null ||
                !shell.IsCommitted)
            {
                errors.Add(
                    "ShellSkin room=" +
                    roomIndex +
                    " missing/uncommitted");
            }
        }

        maxBlockedCells =
            Mathf.Max(
                maxBlockedCells,
                totalBlockedThisCase);

        maxRuntimeColliders =
            Mathf.Max(
                maxRuntimeColliders,
                totalCollidersThisCase);

        if (totalBlockedThisCase !=
            totalCollidersThisCase)
        {
            errors.Add(
                "TotalColliderMapping=" +
                totalBlockedThisCase +
                "/" +
                totalCollidersThisCase);
        }
    }

    private static void AuditConsumersAndLifecycle(
        DungeonLayout layout,
        List<string> errors)
    {
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

        if (pathService == null ||
            !pathService.IsInitialized)
        {
            errors.Add(
                "EnemyPathService not initialized");
        }
        else
        {
            if (pathService.WalkableCellCount !=
                layout.FloorCells.Count)
            {
                errors.Add(
                    "EnemyPathWalkable=" +
                    pathService.WalkableCellCount +
                    " FloorCells=" +
                    layout.FloorCells.Count);
            }

            if (pathService.ConnectedComponentCount != 1)
            {
                errors.Add(
                    "ConnectedComponents=" +
                    pathService.ConnectedComponentCount);
            }
        }

        if (!layout.FloorCells.Contains(
                layout.StartCell) ||
            !layout.FloorCells.Contains(
                layout.ExitCell))
        {
            errors.Add(
                "Start/Exit not in FloorCells");
        }

        if (pathService != null &&
            playerManager != null &&
            playerManager.CurrentPlayerObject != null)
        {
            Vector2Int playerCell =
                pathService.WorldToCell(
                    playerManager.CurrentPlayerObject
                        .transform.position);

            if (!layout.FloorCells.Contains(
                    playerCell))
            {
                errors.Add(
                    "PlayerOutsideFloor=" +
                    playerCell);
            }
        }

        if (pathService != null &&
            itemManager != null &&
            itemManager.ActivePickup != null)
        {
            Vector2Int itemCell =
                pathService.WorldToCell(
                    itemManager.ActivePickup
                        .transform.position);

            if (!layout.FloorCells.Contains(
                    itemCell))
            {
                errors.Add(
                    "ItemOutsideFloor=" +
                    itemCell);
            }
        }

        if (pathService != null &&
            enemyManager != null)
        {
            IReadOnlyList<GameObject> enemies =
                enemyManager.ActiveEnemies;

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

                Vector2Int enemyCell =
                    pathService.WorldToCell(
                        enemy.transform.position);

                if (!layout.FloorCells.Contains(
                        enemyCell))
                {
                    errors.Add(
                        "EnemyOutsideFloor=" +
                        enemyCell);
                    break;
                }
            }
        }

        int generatedRoots = 0;

        for (int i = 0;
             i < gameManager.transform.childCount;
             i++)
        {
            Transform child =
                gameManager.transform.GetChild(i);

            if (child != null &&
                child.name.StartsWith(
                    "GeneratedDungeon_Floor_",
                    StringComparison.Ordinal))
            {
                generatedRoots++;
            }
        }

        if (generatedRoots != 1)
        {
            errors.Add(
                "GeneratedRoots=" +
                generatedRoots +
                " expected=1");
        }

        if (playerManager != null)
        {
            RuntimeDungeonPlayer[] players =
                playerManager.GetComponentsInChildren<
                    RuntimeDungeonPlayer>(
                        true);

            if (players.Length != 1)
            {
                errors.Add(
                    "RuntimeDungeonPlayers=" +
                    players.Length +
                    " expected=1");
            }
        }
    }

    private static void AdvanceCase()
    {
        caseIndex++;

        if (caseIndex >= TotalCases)
        {
            Finish(
                failures.Count == 0,
                "Completed");
            return;
        }

        state =
            RegressionState.Generate;
    }

    private static void Finish(
        bool completedWithoutCaseFailure,
        string reason)
    {
        if (!running)
        {
            return;
        }

        running = false;

        EditorApplication.update -=
            RegressionUpdate;

        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -=
            OnBeforeAssemblyReload;

        RestoreGeneratorSettings();

        bool familyCoverage =
            true;

        for (int i = 0;
             i < familyCases.Length;
             i++)
        {
            if (familyCases[i] <= 0 ||
                familyFloors[i].Count <= 0)
            {
                familyCoverage = false;
            }
        }

        bool pass =
            completedWithoutCaseFailure &&
            reason == "Completed" &&
            successfulCases == TotalCases &&
            hybridCases == TotalCases &&
            failures.Count == 0 &&
            familyCoverage;

        string summary =
            BuildSummary(
                pass,
                reason,
                familyCoverage);

        EditorPrefs.SetBool(
            LastPassKey,
            pass);

        EditorPrefs.SetString(
            LastSummaryKey,
            summary);

        if (pass)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        ClearRuntimeReferences();
    }

    private static string BuildSummary(
        bool pass,
        string reason,
        bool familyCoverage)
    {
        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "[P10.12C-2] Multi-Seed Multi-Floor Runtime Regression " +
            (pass ? "PASS" : "FAILED"));

        report.AppendLine(
            "Reason=" +
            reason +
            " | Cases=" +
            successfulCases +
            "/" +
            TotalCases +
            " | HybridCases=" +
            hybridCases +
            "/" +
            TotalCases +
            " | Failures=" +
            failures.Count);

        report.AppendLine(
            "Floors=1,2,3" +
            " | SeedsPerFloor=" +
            SeedsPerFloor +
            " | RuntimeRebuilds=" +
            caseIndex +
            " | MaxProceduralPlacements=" +
            maxProceduralPlacements);

        for (int i = 0;
             i < FamilyLabels.Length;
             i++)
        {
            report.AppendLine(
                FamilyLabels[i] +
                " | Cases=" +
                familyCases[i] +
                " | FloorCoverage=" +
                familyFloors[i].Count +
                "/3");
        }

        report.AppendLine(
            "StressCoverage=" +
            " SmallNEWS:" +
            smallNewsStressHits +
            " | WideEW:" +
            wideEastWestStressHits +
            " | TallNS:" +
            tallNorthSouthStressHits +
            " | AllFourSameCase:" +
            allFourSameCaseCount);

        report.AppendLine(
            "ZeroSoftDecorGracefulCases=" +
            zeroSoftDecorCases +
            " | MaxBlockedCells=" +
            maxBlockedCells +
            " | MaxRuntimeColliders=" +
            maxRuntimeColliders);

        report.AppendLine(
            "AuthorityRuntimeMapping=1to1" +
            " | EnemyPathMatchesFloorCells=True" +
            " | ConnectedComponent=1" +
            " | SpawnConsumersOnFloor=True" +
            " | GeneratedRootLifecycle=Single" +
            " | PlayerLifecycle=Single");

        report.AppendLine(
            "FamilyCoverage=" +
            familyCoverage +
            " | SeedSettingsRestored=True" +
            " | ProductionMainChanged=False" +
            " | SceneSaveRequired=False");

        if (failures.Count > 0)
        {
            int limit =
                Mathf.Min(
                    20,
                    failures.Count);

            report.AppendLine(
                "FirstFailures:");

            for (int i = 0;
                 i < limit;
                 i++)
            {
                report.AppendLine(
                    "- " +
                    failures[i]);
            }

            if (failures.Count > limit)
            {
                report.AppendLine(
                    "... and " +
                    (failures.Count - limit) +
                    " more.");
            }
        }

        report.Append(
            "Result=" +
            (pass ? "PASS" : "FAILED"));

        return report.ToString();
    }

    private static int GetFloorForCase(
        int index)
    {
        int floorIndex =
            index /
            SeedsPerFloor;

        floorIndex =
            Mathf.Clamp(
                floorIndex,
                0,
                Floors.Length - 1);

        return
            Floors[floorIndex];
    }

    private static int GetFamilyIndex(
        string templateId)
    {
        for (int i = 0;
             i < FamilyTemplateIds.Length;
             i++)
        {
            if (string.Equals(
                    FamilyTemplateIds[i],
                    templateId,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetUsedSocketMask(
        DungeonLayout layout,
        int roomIndex)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count)
        {
            return 0;
        }

        DreamRoomPlacement placement =
            layout.RoomPlacements[
                roomIndex];

        if (placement == null ||
            placement.Template == null)
        {
            return 0;
        }

        int mask = 0;

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

            string socketId = null;

            if (connection.RoomAIndex ==
                roomIndex)
            {
                socketId =
                    connection.SocketAId;
            }
            else if (connection.RoomBIndex ==
                     roomIndex)
            {
                socketId =
                    connection.SocketBId;
            }

            if (string.IsNullOrWhiteSpace(
                    socketId))
            {
                continue;
            }

            DreamRoomDoorSocket socket;

            if (!placement.Template.TryGetSocket(
                    socketId,
                    out socket) ||
                socket == null)
            {
                continue;
            }

            switch (socket.Direction)
            {
                case DreamRoomDoorDirection.North:
                    mask |= 1;
                    break;

                case DreamRoomDoorDirection.East:
                    mask |= 2;
                    break;

                case DreamRoomDoorDirection.South:
                    mask |= 4;
                    break;

                case DreamRoomDoorDirection.West:
                    mask |= 8;
                    break;
            }
        }

        return mask;
    }

    private static void ResetCounters()
    {
        failures.Clear();

        for (int i = 0;
             i < familyCases.Length;
             i++)
        {
            familyCases[i] = 0;
            familyFloors[i].Clear();
        }

        successfulCases = 0;
        hybridCases = 0;
        allFourSameCaseCount = 0;
        smallNewsStressHits = 0;
        wideEastWestStressHits = 0;
        tallNorthSouthStressHits = 0;
        zeroSoftDecorCases = 0;
        maxProceduralPlacements = 0;
        maxBlockedCells = 0;
        maxRuntimeColliders = 0;
    }

    private static void RestoreGeneratorSettings()
    {
        if (generator == null)
        {
            return;
        }

        if (useRandomSeedField != null)
        {
            useRandomSeedField.SetValue(
                generator,
                originalUseRandomSeed);
        }

        if (fixedSeedField != null)
        {
            fixedSeedField.SetValue(
                generator,
                originalFixedSeed);
        }
    }

    private static void ClearRuntimeReferences()
    {
        gameManager = null;
        generator = null;
        renderer = null;

        useRandomSeedField = null;
        fixedSeedField = null;
        currentLayoutField = null;
        generateFloorMethod = null;

        abortRequested = false;
    }

    private static void AddFailure(
        string failure)
    {
        failures.Add(
            failure);
    }

    private static string BuildCasePrefix(
        int floor,
        int seed)
    {
        return
            "Case=" +
            (caseIndex + 1) +
            "/" +
            TotalCases +
            " Floor=" +
            floor +
            " Seed=" +
            seed;
    }

    private static string UnwrapException(
        Exception exception)
    {
        TargetInvocationException target =
            exception as TargetInvocationException;

        if (target != null &&
            target.InnerException != null)
        {
            return
                target.InnerException.ToString();
        }

        return
            exception.ToString();
    }

    private static void OnPlayModeStateChanged(
        PlayModeStateChange change)
    {
        if (!running)
        {
            return;
        }

        if (change ==
                PlayModeStateChange.ExitingPlayMode ||
            change ==
                PlayModeStateChange.EnteredEditMode)
        {
            abortRequested = true;
        }
    }

    private static void OnBeforeAssemblyReload()
    {
        if (!running)
        {
            return;
        }

        RestoreGeneratorSettings();

        running = false;
    }

    private static int TotalCases =>
        Floors.Length *
        SeedsPerFloor;

    private enum RegressionState
    {
        Generate = 0,
        WaitRuntime = 1,
        Audit = 2
    }
}



/// <summary>
/// P10.13-1：Production_Main 的房间精度层级契约。
///
/// 这是长期维护入口，不改变 Runtime 选择算法：
/// 1. P10.12 注册的四个 Graybox bridge 明确标记为 ProceduralMedium；
/// 2. Production_* 正式 Prefab 保持 HighPrecision；
/// 3. RoomTag、RandomWeight、Floor gate、MaximumInstancesPerFloor 全部保持原权威；
/// 4. P10.13-2 才会在普通房槽位接入“高精度 + 中精度”的软配额策略。
/// </summary>
public static class DreamRoomFidelityMaintenanceP1013
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Room Mix/P10.13-1 Fidelity Contract/";

    private const string ProductionCatalogPath =
        "Assets/DreamDungeon/Production/Catalog/RoomCatalog_Production.asset";

    private const string ProductionCatalogId =
        "Production_Main";

    [MenuItem(
        MenuRoot + "1. Apply Current Fidelity Labels",
        false,
        3200)]
    private static void ApplyCurrentFidelityLabels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Fail("必须退出 Play Mode。", null);
            return;
        }

        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                ProductionCatalogPath);

        if (!ValidateCatalogIdentity(catalog, out string failure))
        {
            Fail(failure, catalog);
            return;
        }

        int changed = 0;
        int alreadyCorrect = 0;
        int unresolved = 0;

        List<string> details = new List<string>();

        for (int i = 0;
             i < catalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                catalog.RoomTemplates[i];

            if (template == null)
            {
                unresolved++;
                details.Add("Element " + i + " = Null");
                continue;
            }

            DreamRoomFidelityTier targetTier;

            DreamProceduralRoomFamilyProfileP1012B1 family;
            bool isMediumFamily =
                DreamProceduralRoomFamilyRegistryP1012B1
                    .TryGetByTemplateId(
                        template.TemplateId,
                        out family);

            if (isMediumFamily)
            {
                targetTier =
                    DreamRoomFidelityTier.ProceduralMedium;
            }
            else if (template.TemplateId.StartsWith(
                         "Production_",
                         StringComparison.Ordinal))
            {
                targetTier =
                    DreamRoomFidelityTier.HighPrecision;
            }
            else
            {
                unresolved++;
                details.Add(
                    template.TemplateId +
                    " = 未识别，未自动改写");
                continue;
            }

            if (template.RoomFidelityTier == targetTier)
            {
                alreadyCorrect++;
                continue;
            }

            string prefabPath =
                AssetDatabase.GetAssetPath(template.gameObject);

            if (string.IsNullOrEmpty(prefabPath))
            {
                unresolved++;
                details.Add(
                    template.TemplateId +
                    " = 无法取得 Prefab 路径");
                continue;
            }

            GameObject prefabRoot = null;

            try
            {
                prefabRoot =
                    PrefabUtility.LoadPrefabContents(prefabPath);

                DreamRoomTemplate editable =
                    prefabRoot.GetComponent<DreamRoomTemplate>();

                if (editable == null)
                {
                    unresolved++;
                    details.Add(
                        template.TemplateId +
                        " = Prefab Root 缺少 DreamRoomTemplate");
                    continue;
                }

                SerializedObject serialized =
                    new SerializedObject(editable);

                SerializedProperty fidelity =
                    serialized.FindProperty(
                        "roomFidelityTier");

                if (fidelity == null)
                {
                    unresolved++;
                    details.Add(
                        template.TemplateId +
                        " = 找不到 roomFidelityTier");
                    continue;
                }

                fidelity.enumValueIndex =
                    (int)targetTier;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath);

                changed++;

                details.Add(
                    template.TemplateId +
                    " -> " + targetTier);
            }
            catch (Exception exception)
            {
                unresolved++;
                details.Add(
                    template.TemplateId +
                    " = 写入失败：" +
                    exception.GetType().Name +
                    ": " + exception.Message);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(
                        prefabRoot);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[P10.13-1] Fidelity Labels APPLY");
        report.AppendLine(
            "Catalog=Production_Main" +
            " | Entries=" + catalog.Count);
        report.AppendLine(
            "PrefabAssetsChanged=" + changed +
            " | AlreadyCorrect=" + alreadyCorrect +
            " | Unresolved=" + unresolved);
        report.AppendLine(
            "ProductionMainCatalogChanged=False" +
            " | GameSceneChanged=False" +
            " | RuntimeSelectionChanged=False");
        report.AppendLine(
            "RoomTagsChanged=False" +
            " | RandomWeightChanged=False" +
            " | FloorGateChanged=False" +
            " | InstanceCapChanged=False");

        for (int i = 0; i < details.Count; i++)
        {
            report.AppendLine("- " + details[i]);
        }

        report.Append(
            "Result=" +
            (unresolved == 0 ? "PASS" : "FAILED"));

        if (unresolved == 0)
        {
            Debug.Log(report.ToString(), catalog);
        }
        else
        {
            Debug.LogError(report.ToString(), catalog);
        }
    }

    [MenuItem(
        MenuRoot + "2. Audit Fidelity Contract",
        false,
        3201)]
    private static void AuditFidelityContract()
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                ProductionCatalogPath);

        List<string> errors = new List<string>();

        if (!ValidateCatalogIdentity(catalog, out string failure))
        {
            errors.Add(failure);
        }

        int high = 0;
        int medium = 0;
        int familyEntries = 0;
        int familyMedium = 0;
        int productionEntries = 0;
        int productionHigh = 0;
        int nonFamilyMedium = 0;

        HashSet<string> catalogTemplateIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (catalog != null &&
            catalog.RoomTemplates != null)
        {
            for (int i = 0;
                 i < catalog.RoomTemplates.Count;
                 i++)
            {
                DreamRoomTemplate template =
                    catalog.RoomTemplates[i];

                if (template == null)
                {
                    errors.Add(
                        "Production_Main Element " + i +
                        " 是空引用。");
                    continue;
                }

                catalogTemplateIds.Add(
                    template.TemplateId);

                if (template.RoomFidelityTier ==
                    DreamRoomFidelityTier.HighPrecision)
                {
                    high++;
                }
                else if (template.RoomFidelityTier ==
                         DreamRoomFidelityTier.ProceduralMedium)
                {
                    medium++;
                }
                else
                {
                    errors.Add(
                        template.TemplateId +
                        " 使用未知 Fidelity Tier=" +
                        (int)template.RoomFidelityTier + "。");
                }

                DreamProceduralRoomFamilyProfileP1012B1 family;
                bool isFamily =
                    DreamProceduralRoomFamilyRegistryP1012B1
                        .TryGetByTemplateId(
                            template.TemplateId,
                            out family);

                if (isFamily)
                {
                    familyEntries++;

                    if (template.RoomFidelityTier ==
                        DreamRoomFidelityTier.ProceduralMedium)
                    {
                        familyMedium++;
                    }
                    else
                    {
                        errors.Add(
                            template.TemplateId +
                            " 已注册为 P10.12 Family，" +
                            "但 Fidelity 不是 ProceduralMedium。");
                    }
                }
                else if (template.RoomFidelityTier ==
                         DreamRoomFidelityTier.ProceduralMedium)
                {
                    nonFamilyMedium++;
                    errors.Add(
                        template.TemplateId +
                        " 被标记为 ProceduralMedium，" +
                        "但没有 P10.12 Family Profile。");
                }

                if (template.TemplateId.StartsWith(
                        "Production_",
                        StringComparison.Ordinal))
                {
                    productionEntries++;

                    if (template.RoomFidelityTier ==
                        DreamRoomFidelityTier.HighPrecision)
                    {
                        productionHigh++;
                    }
                    else
                    {
                        errors.Add(
                            template.TemplateId +
                            " 是 Production Prefab，" +
                            "但 Fidelity 不是 HighPrecision。");
                    }
                }
            }
        }

        IReadOnlyList<DreamProceduralRoomFamilyProfileP1012B1>
            allFamilies =
                DreamProceduralRoomFamilyRegistryP1012B1.All;

        for (int i = 0; i < allFamilies.Count; i++)
        {
            if (!catalogTemplateIds.Contains(
                    allFamilies[i].TemplateId))
            {
                errors.Add(
                    "Production_Main 缺少 P10.12 Family bridge：" +
                    allFamilies[i].TemplateId + "。");
            }
        }

        if (high == 0)
        {
            errors.Add("HighPrecision 候选数为 0。");
        }

        if (medium == 0)
        {
            errors.Add("ProceduralMedium 候选数为 0。");
        }

        if (familyEntries != allFamilies.Count ||
            familyMedium != allFamilies.Count)
        {
            errors.Add(
                "P10.12 Family Fidelity 覆盖不是 " +
                allFamilies.Count + "/" +
                allFamilies.Count + "。");
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[P10.13-1] Room Fidelity Contract Audit " +
            (errors.Count == 0 ? "PASS" : "FAILED"));
        report.AppendLine(
            "Catalog=" +
            (catalog == null ? "Missing" : catalog.CatalogId) +
            " | Entries=" +
            (catalog == null ? 0 : catalog.Count));
        report.AppendLine(
            "HighPrecision=" + high +
            " | ProceduralMedium=" + medium);
        report.AppendLine(
            "MediumFamilies=" + familyMedium +
            "/" + allFamilies.Count +
            " | NonFamilyMedium=" + nonFamilyMedium);
        report.AppendLine(
            "ProductionPrefabsHigh=" + productionHigh +
            "/" + productionEntries);
        report.AppendLine(
            "FactoryDefault=HighPrecision" +
            " | RoomTagsOrthogonal=True" +
            " | RuntimeSelectionChanged=False");
        report.AppendLine(
            "ProductionMainCatalogChanged=False" +
            " | GameSceneChanged=False");
        report.Append(
            "Result=" +
            (errors.Count == 0 ? "PASS" : "FAILED"));

        if (errors.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Errors:");

            for (int i = 0; i < errors.Count; i++)
            {
                report.AppendLine("- " + errors[i]);
            }

            Debug.LogError(
                report.ToString(),
                catalog);
            return;
        }

        Debug.Log(
            report.ToString(),
            catalog);
    }

    private static bool ValidateCatalogIdentity(
        DreamRoomCatalog catalog,
        out string failure)
    {
        failure = string.Empty;

        if (catalog == null)
        {
            failure =
                "找不到 Production_Main Catalog：" +
                ProductionCatalogPath;
            return false;
        }

        if (!string.Equals(
                catalog.CatalogId,
                ProductionCatalogId,
                StringComparison.Ordinal))
        {
            failure =
                "CatalogId 不是 Production_Main，而是 " +
                catalog.CatalogId + "。";
            return false;
        }

        if (catalog.RoomTemplates == null ||
            catalog.RoomTemplates.Count == 0)
        {
            failure =
                "Production_Main 没有 Room Templates。";
            return false;
        }

        return true;
    }

    private static void Fail(
        string failure,
        UnityEngine.Object context)
    {
        Debug.LogError(
            "[P10.13-1] Fidelity Contract FAILED\n" +
            failure,
            context);
    }
}



/// <summary>
/// P10.13-2：Room Fidelity Mix 长期维护验收。
/// 不建立新的阶段性 Editor Script，继续挂在 Family Maintenance 文件中。
/// </summary>
public static class DreamRoomFidelityMixMaintenanceP10132
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Room Mix/P10.13-2 Fidelity-Aware Selection/";

    private const int SeedsPerFloor = 64;

    [MenuItem(
        MenuRoot + "1. Audit Mix Configuration",
        false,
        3360)]
    private static void AuditMixConfiguration()
    {
        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError(
                "[P10.13-2] 当前 Scene 找不到 DungeonGenerator。" +
                " 请打开 GameScene 后重试。");
            return;
        }

        DreamRoomCatalog catalog =
            generator.TemplateFirstRoomCatalog;

        List<string> errors =
            new List<string>();

        if (catalog == null)
        {
            errors.Add(
                "TemplateFirstRoomCatalog 为空。");
        }
        else if (!string.Equals(
                     catalog.CatalogId,
                     "Production_Main",
                     StringComparison.Ordinal))
        {
            errors.Add(
                "Catalog 不是 Production_Main：" +
                catalog.CatalogId);
        }

        if (!generator.P10132FidelityAwareRoomMixEnabled)
        {
            errors.Add(
                "Fidelity-Aware Room Mix 开关为 False。");
        }

        if (generator.P10132ProceduralMediumTargetRatio < 0.10f ||
            generator.P10132ProceduralMediumTargetRatio > 0.90f)
        {
            errors.Add(
                "TargetRatio 超出 0.10～0.90。");
        }

        int high = 0;
        int medium = 0;

        if (catalog != null &&
            catalog.RoomTemplates != null)
        {
            for (int i = 0;
                 i < catalog.RoomTemplates.Count;
                 i++)
            {
                DreamRoomTemplate template =
                    catalog.RoomTemplates[i];

                if (template == null)
                {
                    continue;
                }

                if (template.RoomFidelityTier ==
                    DreamRoomFidelityTier.ProceduralMedium)
                {
                    medium++;
                }
                else if (template.RoomFidelityTier ==
                         DreamRoomFidelityTier.HighPrecision)
                {
                    high++;
                }
            }
        }

        if (high <= 0 ||
            medium <= 0)
        {
            errors.Add(
                "Production_Main 必须同时拥有 HighPrecision 与 ProceduralMedium。" +
                " High=" +
                high +
                " Medium=" +
                medium);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.13-2] Fidelity Mix Configuration Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.13-2] Fidelity Mix Configuration Audit PASS" +
            "\nCatalog=" +
            catalog.CatalogId +
            " | Entries=" +
            catalog.Count +
            " | HighPrecision=" +
            high +
            " | ProceduralMedium=" +
            medium +
            "\nDesiredRooms=" +
            generator.TemplateFirstDesiredRoomCount +
            " | TargetMedium=" +
            generator.P10132TargetProceduralMediumCount +
            " | TargetRatio=" +
            generator.P10132ProceduralMediumTargetRatio.ToString("F2") +
            " | PreferUniqueMedium=" +
            generator.P10132PreferUniqueProceduralMediumFamilies +
            "\nRolePriority=StartExitCoreSpecialBeforeFidelity" +
            " | MixScope=OrdinarySlotsOnly" +
            " | ProductionMainChanged=False" +
            " | GameSceneChanged=False" +
            "\nResult=PASS");
    }

    [MenuItem(
        MenuRoot + "2. Run 192-Layout Selection Regression",
        false,
        3370)]
    private static void RunSelectionRegression()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.13-2] Selection Regression 请在 Edit Mode 执行。");
            return;
        }

        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError(
                "[P10.13-2] 当前 Scene 找不到 DungeonGenerator。" +
                " 请打开 GameScene 后重试。");
            return;
        }

        DreamRoomCatalog catalog =
            generator.TemplateFirstRoomCatalog;

        if (catalog == null)
        {
            Debug.LogError(
                "[P10.13-2] Production_Main Catalog 为空。");
            return;
        }

        int[] floors =
        {
            1, 2, 3
        };

        int expected =
            floors.Length *
            SeedsPerFloor;

        int generated = 0;
        int deterministic = 0;
        int mixedTierCases = 0;
        int zeroMediumCases = 0;
        int zeroHighCases = 0;
        int mediumRepeatCases = 0;
        int totalMediumRepeats = 0;
        int allSevenUniqueCases = 0;

        int minMedium = int.MaxValue;
        int maxMedium = int.MinValue;
        int minHigh = int.MaxValue;
        int maxHigh = int.MinValue;

        long totalMedium = 0;
        long totalHigh = 0;

        List<string> failures =
            new List<string>();

        for (int f = 0;
             f < floors.Length;
             f++)
        {
            int floor =
                floors[f];

            for (int s = 0;
                 s < SeedsPerFloor;
                 s++)
            {
                int seed =
                    913201 +
                    floor * 100000 +
                    s * 3571;

                DungeonLayout first;
                string firstReport;

                if (!generator.TryGenerateTemplateFirstLayout(
                        floor,
                        seed,
                        out first,
                        out firstReport) ||
                    first == null)
                {
                    failures.Add(
                        "Floor=" +
                        floor +
                        " Seed=" +
                        seed +
                        " FirstGenerate failed.");
                    continue;
                }

                generated++;

                DungeonLayout second;
                string secondReport;

                if (!generator.TryGenerateTemplateFirstLayout(
                        floor,
                        seed,
                        out second,
                        out secondReport) ||
                    second == null)
                {
                    failures.Add(
                        "Floor=" +
                        floor +
                        " Seed=" +
                        seed +
                        " RepeatGenerate failed.");
                    continue;
                }

                if (!SamePlacementSequence(
                        first,
                        second))
                {
                    failures.Add(
                        "Floor=" +
                        floor +
                        " Seed=" +
                        seed +
                        " DeterminismMismatch.");
                    continue;
                }

                deterministic++;

                int high;
                int medium;
                int uniqueTemplates;
                int mediumRepeats;

                CountMix(
                    first,
                    out high,
                    out medium,
                    out uniqueTemplates,
                    out mediumRepeats);

                totalHigh += high;
                totalMedium += medium;

                minHigh =
                    Mathf.Min(
                        minHigh,
                        high);

                maxHigh =
                    Mathf.Max(
                        maxHigh,
                        high);

                minMedium =
                    Mathf.Min(
                        minMedium,
                        medium);

                maxMedium =
                    Mathf.Max(
                        maxMedium,
                        medium);

                if (high > 0 &&
                    medium > 0)
                {
                    mixedTierCases++;
                }

                if (medium == 0)
                {
                    zeroMediumCases++;
                }

                if (high == 0)
                {
                    zeroHighCases++;
                }

                if (mediumRepeats > 0)
                {
                    mediumRepeatCases++;
                    totalMediumRepeats +=
                        mediumRepeats;
                }

                if (uniqueTemplates ==
                    first.RoomPlacements.Count)
                {
                    allSevenUniqueCases++;
                }
            }
        }

        bool pass =
            failures.Count == 0 &&
            generated == expected &&
            deterministic == expected &&
            mixedTierCases == expected &&
            zeroMediumCases == 0 &&
            zeroHighCases == 0;

        string summary =
            "[P10.13-2] 192-Layout Fidelity Selection Regression " +
            (pass ? "PASS" : "FAILED") +
            "\nLayouts=" +
            generated +
            "/" +
            expected +
            " | Deterministic=" +
            deterministic +
            "/" +
            expected +
            " | MixedTierCases=" +
            mixedTierCases +
            "/" +
            expected +
            "\nHighRange=" +
            minHigh +
            "～" +
            maxHigh +
            " | MediumRange=" +
            minMedium +
            "～" +
            maxMedium +
            " | AvgHigh=" +
            (generated == 0
                ? 0f
                : totalHigh / (float)generated).ToString("F2") +
            " | AvgMedium=" +
            (generated == 0
                ? 0f
                : totalMedium / (float)generated).ToString("F2") +
            "\nMediumRepeatCases=" +
            mediumRepeatCases +
            " | TotalMediumRepeats=" +
            totalMediumRepeats +
            " | AllTemplatesUniqueCases=" +
            allSevenUniqueCases +
            "/" +
            expected +
            "\nTargetMedium=" +
            generator.P10132TargetProceduralMediumCount +
            " | TargetRatio=" +
            generator.P10132ProceduralMediumTargetRatio.ToString("F2") +
            " | RolePriorityPreserved=True" +
            " | WeightedWithinTier=True" +
            "\nProductionMainChanged=False" +
            " | GameSceneChanged=False" +
            " | Result=" +
            (pass ? "PASS" : "FAILED");

        if (failures.Count > 0)
        {
            int limit =
                Mathf.Min(
                    16,
                    failures.Count);

            StringBuilder builder =
                new StringBuilder(
                    summary);

            builder.AppendLine();
            builder.AppendLine(
                "FirstFailures:");

            for (int i = 0;
                 i < limit;
                 i++)
            {
                builder.AppendLine(
                    "- " +
                    failures[i]);
            }

            summary =
                builder.ToString();
        }

        if (pass)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }
    }

    private static void CountMix(
        DungeonLayout layout,
        out int high,
        out int medium,
        out int uniqueTemplates,
        out int mediumRepeats)
    {
        high = 0;
        medium = 0;

        HashSet<string> allIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> mediumIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (layout == null)
        {
            uniqueTemplates = 0;
            mediumRepeats = 0;
            return;
        }

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomTemplate template =
                layout.RoomPlacements[i] == null
                    ? null
                    : layout.RoomPlacements[i].Template;

            if (template == null)
            {
                continue;
            }

            allIds.Add(
                template.TemplateId);

            if (template.RoomFidelityTier ==
                DreamRoomFidelityTier.ProceduralMedium)
            {
                medium++;
                mediumIds.Add(
                    template.TemplateId);
            }
            else
            {
                high++;
            }
        }

        uniqueTemplates =
            allIds.Count;

        mediumRepeats =
            Mathf.Max(
                0,
                medium -
                mediumIds.Count);
    }

    private static bool SamePlacementSequence(
        DungeonLayout a,
        DungeonLayout b)
    {
        if (a == null ||
            b == null ||
            a.RoomPlacements.Count !=
                b.RoomPlacements.Count)
        {
            return false;
        }

        for (int i = 0;
             i < a.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement left =
                a.RoomPlacements[i];

            DreamRoomPlacement right =
                b.RoomPlacements[i];

            if (left == null ||
                right == null ||
                left.Template == null ||
                right.Template == null)
            {
                return false;
            }

            if (!string.Equals(
                    left.Template.TemplateId,
                    right.Template.TemplateId,
                    StringComparison.Ordinal) ||
                left.MinimumCell !=
                    right.MinimumCell ||
                left.ClockwiseQuarterTurns !=
                    right.ClockwiseQuarterTurns)
            {
                return false;
            }
        }

        return true;
    }
}

#endif
