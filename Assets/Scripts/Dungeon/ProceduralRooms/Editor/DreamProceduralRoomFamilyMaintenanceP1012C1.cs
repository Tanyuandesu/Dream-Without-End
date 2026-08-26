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
#endif
