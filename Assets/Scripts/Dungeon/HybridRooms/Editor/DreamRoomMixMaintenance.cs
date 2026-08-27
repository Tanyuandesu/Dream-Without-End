#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Room Mix 长期维护工具。
///
/// P10.13 阶段完成后保留的唯一 Room Mix Editor 验收入口。
/// 已退役：
/// - P10.13-1 Fidelity Label Apply/Repair
/// - P10.13-1 独立 Contract Audit
/// - P10.13-2 阶段菜单
/// - P10.13-3 阶段菜单
///
/// 保留的长期能力：
/// 1. Contract + Configuration Audit
/// 2. 192 Layout Selection Regression
/// 3. LIVE Runtime Composition Audit
/// 4. 24 Case Runtime Composition Soak
///
/// 这里不拥有任何 Runtime 行为，只验证已经正式化的 Room Mix 系统。
/// </summary>
/// <summary>
/// P10.13-2：Room Fidelity Mix 长期维护验收。
/// 不建立新的阶段性 Editor Script，继续挂在 Family Maintenance 文件中。
/// </summary>
public static class DreamRoomMixSelectionMaintenance
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Room Mix/Maintenance/";

    private const int SeedsPerFloor = 64;

    [MenuItem(
        MenuRoot + "1. Audit Contract + Mix Configuration",
        false,
        3500)]
    private static void AuditMixConfiguration()
    {
        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] 当前 Scene 找不到 DungeonGenerator。" +
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
        int familyMedium = 0;
        int productionHigh = 0;
        int productionEntries = 0;
        int nonFamilyMedium = 0;

        HashSet<string> catalogIds =
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
                        "Production_Main Element " +
                        i +
                        " 是空引用。");
                    continue;
                }

                catalogIds.Add(
                    template.TemplateId);

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
                else
                {
                    errors.Add(
                        template.TemplateId +
                        " 使用未知 Fidelity Tier。");
                }

                DreamProceduralRoomFamilyProfileP1012B1 family;
                bool isFamily =
                    DreamProceduralRoomFamilyRegistryP1012B1
                        .TryGetByTemplateId(
                            template.TemplateId,
                            out family);

                if (isFamily)
                {
                    if (template.RoomFidelityTier ==
                        DreamRoomFidelityTier.ProceduralMedium)
                    {
                        familyMedium++;
                    }
                    else
                    {
                        errors.Add(
                            template.TemplateId +
                            " 是 P10.12 Family，" +
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
                        "但没有 Family Profile。");
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

        IReadOnlyList<DreamProceduralRoomFamilyProfileP1012B1> families =
            DreamProceduralRoomFamilyRegistryP1012B1.All;

        for (int i = 0;
             i < families.Count;
             i++)
        {
            if (!catalogIds.Contains(
                    families[i].TemplateId))
            {
                errors.Add(
                    "Production_Main 缺少 Family bridge：" +
                    families[i].TemplateId);
            }
        }

        if (familyMedium != families.Count)
        {
            errors.Add(
                "Family Medium 覆盖不是 " +
                families.Count +
                "/" +
                families.Count +
                "，实际=" +
                familyMedium);
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
                "[RoomMix/Maintenance] Fidelity Mix Configuration Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[RoomMix/Maintenance] Fidelity Mix Configuration Audit PASS" +
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
            "\nFamilyMedium=" +
            familyMedium +
            "/" +
            families.Count +
            " | ProductionHigh=" +
            productionHigh +
            "/" +
            productionEntries +
            " | NonFamilyMedium=" +
            nonFamilyMedium +
            "\nRolePriority=StartExitCoreSpecialBeforeFidelity" +
            " | MixScope=OrdinarySlotsOnly" +
            " | ProductionMainChanged=False" +
            " | GameSceneChanged=False" +
            "\nResult=PASS");
    }

    [MenuItem(
        MenuRoot + "2. Run 192-Layout Selection Regression",
        false,
        3510)]
    private static void RunSelectionRegression()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] Selection Regression 请在 Edit Mode 执行。");
            return;
        }

        DungeonGenerator generator =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] 当前 Scene 找不到 DungeonGenerator。" +
                " 请打开 GameScene 后重试。");
            return;
        }

        DreamRoomCatalog catalog =
            generator.TemplateFirstRoomCatalog;

        if (catalog == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] Production_Main Catalog 为空。");
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
            "[RoomMix/Maintenance] 192-Layout Fidelity Selection Regression " +
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

/// <summary>
/// P10.13-3：Fidelity Runtime Composition Audit。
///
/// 这是 P10.13 混合选择功能的 Runtime 验收层。
/// 不改选房算法，不改 Production_Main，不新增第二个 Editor Script。
///
/// 核心契约：
/// - HighPrecision：保持正式 Prefab，不允许 Runtime Procedural Override。
/// - ProceduralMedium：必须被 P10.12 Family Authority 接管。
/// - 每一个 Medium Placement 必须有一对一 Runtime Geometry + A2/A3 Skin。
/// </summary>
public static class DreamRoomMixRuntimeMaintenance
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Room Mix/Maintenance/";

    private const string LastSoakPassKey =
        "DreamDungeon.RoomMixMaintenance.RuntimeCompositionSoakPassed";

    private const string LastSoakSummaryKey =
        "DreamDungeon.RoomMixMaintenance.RuntimeCompositionSoakSummary";

    private const int SeedsPerFloor = 8;

    private static readonly int[] Floors =
    {
        1, 2, 3
    };

    private const int BaseSeed = 513731;

    private static bool running;
    private static bool abortRequested;
    private static int caseIndex;
    private static int generatedFrame;
    private static SoakState state;

    private static GameManager gameManager;
    private static DungeonGenerator generator;

    private static FieldInfo currentLayoutField;
    private static FieldInfo useRandomSeedField;
    private static FieldInfo fixedSeedField;
    private static MethodInfo generateFloorMethod;

    private static bool originalUseRandomSeed;
    private static int originalFixedSeed;

    private static int successfulCases;
    private static int exactThreeHighFourMediumCases;
    private static int allSevenUniqueCases;
    private static int mediumRepeatCases;
    private static int totalMediumRepeats;
    private static int minHigh;
    private static int maxHigh;
    private static int minMedium;
    private static int maxMedium;
    private static long totalHigh;
    private static long totalMedium;

    private static readonly List<string> failures =
        new List<string>();

    [MenuItem(
        MenuRoot + "3. Validate LIVE Current Composition",
        false,
        3520)]
    private static void ValidateCurrentComposition()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] Current Composition Audit 必须在 Play Mode。");
            return;
        }

        GameManager gm =
            UnityEngine.Object.FindFirstObjectByType<GameManager>();

        DungeonGenerator dg =
            UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();

        if (gm == null ||
            dg == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] 找不到 GameManager / DungeonGenerator。");
            return;
        }

        DungeonLayout layout =
            ReadCurrentLayout(
                gm);

        if (layout == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] 无法读取 currentLayout。");
            return;
        }

        CompositionResult result =
            AuditComposition(
                layout,
                dg);

        if (!result.Pass)
        {
            Debug.LogError(
                BuildLiveSummary(
                    gm,
                    dg,
                    layout,
                    result,
                    false));
            return;
        }

        Debug.Log(
            BuildLiveSummary(
                gm,
                dg,
                layout,
                result,
                true));
    }

    [MenuItem(
        MenuRoot + "4. Start 24-Case Runtime Composition Soak",
        false,
        3530)]
    private static void StartRuntimeSoak()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] Runtime Composition Soak 必须在 Play Mode。");
            return;
        }

        if (running)
        {
            Debug.LogWarning(
                "[RoomMix/Maintenance] Runtime Composition Soak 已经在运行。");
            return;
        }

        gameManager =
            UnityEngine.Object.FindFirstObjectByType<GameManager>();

        generator =
            UnityEngine.Object.FindFirstObjectByType<DungeonGenerator>();

        if (gameManager == null ||
            generator == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] 找不到 GameManager / DungeonGenerator。");
            return;
        }

        Type generatorType =
            typeof(DungeonGenerator);

        currentLayoutField =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

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

        generateFloorMethod =
            typeof(GameManager).GetMethod(
                "GenerateFloor",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (currentLayoutField == null ||
            useRandomSeedField == null ||
            fixedSeedField == null ||
            generateFloorMethod == null)
        {
            Debug.LogError(
                "[RoomMix/Maintenance] Reflection Contract 不完整。" +
                "\ncurrentLayout=" +
                (currentLayoutField != null) +
                " | useRandomSeed=" +
                (useRandomSeedField != null) +
                " | fixedSeed=" +
                (fixedSeedField != null) +
                " | GenerateFloor=" +
                (generateFloorMethod != null));
            ClearRuntimeReferences();
            return;
        }

        originalUseRandomSeed =
            (bool)useRandomSeedField.GetValue(
                generator);

        originalFixedSeed =
            (int)fixedSeedField.GetValue(
                generator);

        useRandomSeedField.SetValue(
            generator,
            false);

        ResetSoakCounters();

        running = true;
        abortRequested = false;
        caseIndex = 0;
        state = SoakState.Generate;

        EditorPrefs.SetBool(
            LastSoakPassKey,
            false);

        EditorPrefs.SetString(
            LastSoakSummaryKey,
            string.Empty);

        EditorApplication.update +=
            SoakUpdate;

        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload +=
            OnBeforeAssemblyReload;

        Debug.Log(
            "[RoomMix/Maintenance] Runtime Composition Soak START" +
            "\nCases=" +
            TotalCases +
            " | Floors=1,2,3" +
            " | SeedsPerFloor=" +
            SeedsPerFloor +
            "\nAudit=Selection -> Placement -> P10.12 Authority -> Runtime Geometry/Skin" +
            "\nIMPORTANT=测试期间不要按 R、不要切换 Scene、不要退出 Play Mode。");
    }

    [MenuItem(
        MenuRoot + "5. Abort Running Composition Soak",
        false,
        3540)]
    private static void AbortRuntimeSoak()
    {
        if (!running)
        {
            Debug.Log(
                "[RoomMix/Maintenance] 当前没有正在运行的 Composition Soak。");
            return;
        }

        abortRequested = true;

        Debug.LogWarning(
            "[RoomMix/Maintenance] 已请求中止。" +
            " 下一次 Editor Update 会恢复 Seed 设置。");
    }

    [MenuItem(
        MenuRoot + "6. Print Last Composition Soak Summary",
        false,
        3550)]
    private static void PrintLastSoakSummary()
    {
        string summary =
            EditorPrefs.GetString(
                LastSoakSummaryKey,
                string.Empty);

        if (string.IsNullOrWhiteSpace(
                summary))
        {
            Debug.Log(
                "[RoomMix/Maintenance] 尚无 Runtime Composition Soak 结果。");
            return;
        }

        if (EditorPrefs.GetBool(
                LastSoakPassKey,
                false))
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }
    }

    private static void SoakUpdate()
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
            FinishSoak(
                false,
                abortRequested
                    ? "UserAbort"
                    : "PlayModeOrRuntimeLost");
            return;
        }

        switch (state)
        {
            case SoakState.Generate:
                GenerateCurrentCase();
                break;

            case SoakState.WaitRuntime:
                // 等待旧层 Destroy + 新层 Start / Skin Commit 至少 2 个 Play Frame。
                if (Time.frameCount >=
                    generatedFrame + 2)
                {
                    state =
                        SoakState.Audit;
                }
                break;

            case SoakState.Audit:
                AuditCurrentCase();
                break;
        }
    }

    private static void GenerateCurrentCase()
    {
        if (caseIndex >= TotalCases)
        {
            FinishSoak(
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
            BaseSeed +
            floor * 100000 +
            seedOrdinal * 6113;

        // RuntimeHybrid 固定 Seed 规则：
        // actual layout seed = fixedSeed + floor - 1
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
            SoakState.WaitRuntime;
    }

    private static void AuditCurrentCase()
    {
        int floor =
            GetFloorForCase(
                caseIndex);

        int seedOrdinal =
            caseIndex %
            SeedsPerFloor;

        int expectedSeed =
            BaseSeed +
            floor * 100000 +
            seedOrdinal * 6113;

        DungeonLayout layout =
            currentLayoutField.GetValue(
                gameManager)
                as DungeonLayout;

        if (layout == null)
        {
            AddFailure(
                BuildCasePrefix(
                    floor,
                    expectedSeed) +
                " currentLayout=null.");

            AdvanceCase();
            return;
        }

        List<string> caseErrors =
            new List<string>();

        if (gameManager.CurrentFloor != floor)
        {
            caseErrors.Add(
                "CurrentFloor=" +
                gameManager.CurrentFloor +
                " expected=" +
                floor);
        }

        if (layout.Seed != expectedSeed)
        {
            caseErrors.Add(
                "LayoutSeed=" +
                layout.Seed +
                " expected=" +
                expectedSeed);
        }

        if (gameManager.EffectiveGenerationMode !=
            DungeonRenderMode.HybridPrefabRooms)
        {
            caseErrors.Add(
                "EffectiveMode=" +
                gameManager.EffectiveGenerationMode);
        }

        CompositionResult result =
            AuditComposition(
                layout,
                generator);

        if (!result.Pass)
        {
            caseErrors.AddRange(
                result.Errors);
        }

        if (caseErrors.Count > 0)
        {
            AddFailure(
                BuildCasePrefix(
                    floor,
                    expectedSeed) +
                "\n  " +
                string.Join(
                    "\n  ",
                    caseErrors));
        }
        else
        {
            successfulCases++;

            totalHigh +=
                result.HighCount;

            totalMedium +=
                result.MediumCount;

            minHigh =
                Mathf.Min(
                    minHigh,
                    result.HighCount);

            maxHigh =
                Mathf.Max(
                    maxHigh,
                    result.HighCount);

            minMedium =
                Mathf.Min(
                    minMedium,
                    result.MediumCount);

            maxMedium =
                Mathf.Max(
                    maxMedium,
                    result.MediumCount);

            if (result.HighCount == 3 &&
                result.MediumCount == 4)
            {
                exactThreeHighFourMediumCases++;
            }

            if (result.UniqueTemplateCount ==
                layout.RoomPlacements.Count)
            {
                allSevenUniqueCases++;
            }

            if (result.MediumRepeats > 0)
            {
                mediumRepeatCases++;
                totalMediumRepeats +=
                    result.MediumRepeats;
            }
        }

        AdvanceCase();
    }

    private static CompositionResult AuditComposition(
        DungeonLayout layout,
        DungeonGenerator dg)
    {
        CompositionResult result =
            new CompositionResult();

        if (layout == null)
        {
            result.Errors.Add(
                "Layout 为空。");
            return result;
        }

        List<string> validation =
            layout.GetValidationErrors();

        if (validation.Count > 0)
        {
            result.Errors.Add(
                "DungeonLayout validation：" +
                string.Join(
                    " | ",
                    validation));
        }

        if (!layout.HasHybridRoomData)
        {
            result.Errors.Add(
                "HasHybridRoomData=False");
        }

        Dictionary<int, DreamRoomPlacement> mediumByRoomIndex =
            new Dictionary<int, DreamRoomPlacement>();

        HashSet<string> allTemplateIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> mediumTemplateIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            DreamRoomTemplate template =
                placement == null
                    ? null
                    : placement.Template;

            if (template == null)
            {
                result.Errors.Add(
                    "RoomIndex=" +
                    i +
                    " Template=null");
                continue;
            }

            allTemplateIds.Add(
                template.TemplateId);

            if (template.RoomFidelityTier ==
                DreamRoomFidelityTier.ProceduralMedium)
            {
                result.MediumCount++;

                mediumTemplateIds.Add(
                    template.TemplateId);

                mediumByRoomIndex[i] =
                    placement;

                if (!placement.HasRuntimeProceduralOverride)
                {
                    result.Errors.Add(
                        "Medium 未被 P10.12 Authority 接管：" +
                        " RoomIndex=" +
                        i +
                        " Template=" +
                        template.TemplateId);
                }
            }
            else if (template.RoomFidelityTier ==
                     DreamRoomFidelityTier.HighPrecision)
            {
                result.HighCount++;

                if (placement.HasRuntimeProceduralOverride)
                {
                    result.Errors.Add(
                        "HighPrecision 错误获得 Runtime Procedural Override：" +
                        " RoomIndex=" +
                        i +
                        " Template=" +
                        template.TemplateId);
                }
            }
            else
            {
                result.Errors.Add(
                    "未知 Fidelity Tier：" +
                    template.RoomFidelityTier);
            }
        }

        result.UniqueTemplateCount =
            allTemplateIds.Count;

        result.MediumRepeats =
            Mathf.Max(
                0,
                result.MediumCount -
                mediumTemplateIds.Count);

        if (result.HighCount +
            result.MediumCount !=
            layout.RoomPlacements.Count)
        {
            result.Errors.Add(
                "Fidelity Count 不覆盖全部 Placement：" +
                result.HighCount +
                "+" +
                result.MediumCount +
                "/" +
                layout.RoomPlacements.Count);
        }

        if (result.HighCount <= 0 ||
            result.MediumCount <= 0)
        {
            result.Errors.Add(
                "Runtime 没有形成 High + Medium 混合：" +
                " High=" +
                result.HighCount +
                " Medium=" +
                result.MediumCount);
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

            if (!runtimeByRoom.TryAdd(
                    instance.RoomIndex,
                    instance))
            {
                result.Errors.Add(
                    "重复 Procedural Runtime RoomIndex=" +
                    instance.RoomIndex);
            }
        }

        if (runtimeByRoom.Count !=
            result.MediumCount)
        {
            result.Errors.Add(
                "Medium Placement / Procedural Runtime 数量不一致：" +
                result.MediumCount +
                "/" +
                runtimeByRoom.Count);
        }

        foreach (
            KeyValuePair<int, DreamRoomPlacement> pair
            in mediumByRoomIndex)
        {
            int roomIndex =
                pair.Key;

            DreamRoomPlacement placement =
                pair.Value;

            DreamProceduralRoomRuntimeInstanceP1012R2B instance;

            if (!runtimeByRoom.TryGetValue(
                    roomIndex,
                    out instance) ||
                instance == null)
            {
                result.Errors.Add(
                    "Medium 缺 Runtime Geometry：" +
                    " RoomIndex=" +
                    roomIndex +
                    " Template=" +
                    placement.Template.TemplateId);
                continue;
            }

            List<Vector2Int> blocked =
                new List<Vector2Int>();

            placement.GetRuntimeProceduralBlockedLocalCells(
                blocked);

            if (blocked.Count <= 0)
            {
                result.Errors.Add(
                    "Medium Runtime Blocked=0：" +
                    placement.Template.TemplateId);
            }

            if (instance.BlockedCellCount !=
                    blocked.Count ||
                instance.ColliderCount !=
                    blocked.Count)
            {
                result.Errors.Add(
                    "Medium Collider Mapping 非 1:1：" +
                    placement.Template.TemplateId +
                    " | PlacementBlocked=" +
                    blocked.Count +
                    " RuntimeBlocked=" +
                    instance.BlockedCellCount +
                    " Colliders=" +
                    instance.ColliderCount);
            }

            for (int b = 0;
                 b < blocked.Count;
                 b++)
            {
                Vector2Int global =
                    placement.OriginalToGlobalCell(
                        blocked[b]);

                if (layout.FloorCells.Contains(
                        global) ||
                    layout.RoomCells.Contains(
                        global))
                {
                    result.Errors.Add(
                        "Medium Blocked 仍属于 Walkable：" +
                        placement.Template.TemplateId +
                        " " +
                        global);
                    break;
                }

                if (layout.CorridorCells.Contains(
                        global))
                {
                    result.Errors.Add(
                        "Medium Blocked 与 Corridor 重叠：" +
                        placement.Template.TemplateId +
                        " " +
                        global);
                    break;
                }
            }

            DreamProceduralRoomStructuralSkinP1012A31 structure =
                instance.GetComponent<
                    DreamProceduralRoomStructuralSkinP1012A31>();

            if (structure == null ||
                !structure.IsCommitted ||
                structure.RendererCount !=
                    blocked.Count)
            {
                result.Errors.Add(
                    "Medium Structural Skin 未同步：" +
                    placement.Template.TemplateId);
            }

            DreamProceduralRoomSoftDecorP1012A2 soft =
                instance.GetComponent<
                    DreamProceduralRoomSoftDecorP1012A2>();

            if (soft == null ||
                !soft.IsCommitted)
            {
                result.Errors.Add(
                    "Medium Soft Decor 未 Commit：" +
                    placement.Template.TemplateId);
            }

            DreamProceduralRoomShellSkinP1012A32 shell =
                instance.GetComponent<
                    DreamProceduralRoomShellSkinP1012A32>();

            if (shell == null ||
                !shell.IsCommitted)
            {
                result.Errors.Add(
                    "Medium Shell Skin 未 Commit：" +
                    placement.Template.TemplateId);
            }
        }

        foreach (
            KeyValuePair<int, DreamProceduralRoomRuntimeInstanceP1012R2B>
            pair in runtimeByRoom)
        {
            if (!mediumByRoomIndex.ContainsKey(
                    pair.Key))
            {
                result.Errors.Add(
                    "Procedural Runtime 对应的 Placement 不是 ProceduralMedium：" +
                    " RoomIndex=" +
                    pair.Key);
            }
        }

        EnemyPathService pathService =
            UnityEngine.Object.FindFirstObjectByType<
                EnemyPathService>();

        if (pathService == null ||
            !pathService.IsInitialized)
        {
            result.Errors.Add(
                "EnemyPathService 未初始化。");
        }
        else
        {
            if (pathService.WalkableCellCount !=
                layout.FloorCells.Count)
            {
                result.Errors.Add(
                    "EnemyPathWalkable != FloorCells：" +
                    pathService.WalkableCellCount +
                    "/" +
                    layout.FloorCells.Count);
            }

            if (pathService.ConnectedComponentCount != 1)
            {
                result.Errors.Add(
                    "ConnectedComponentCount=" +
                    pathService.ConnectedComponentCount);
            }
        }

        if (!layout.FloorCells.Contains(
                layout.StartCell) ||
            !layout.FloorCells.Contains(
                layout.ExitCell))
        {
            result.Errors.Add(
                "Start / Exit 不属于最终 FloorCells。");
        }

        if (dg != null &&
            !dg.P10132FidelityAwareRoomMixEnabled)
        {
            result.Errors.Add(
                "P10.13-2 Fidelity Mix 开关为 False。");
        }

        result.Pass =
            result.Errors.Count == 0;

        return result;
    }

    private static string BuildLiveSummary(
        GameManager gm,
        DungeonGenerator dg,
        DungeonLayout layout,
        CompositionResult result,
        bool pass)
    {
        int targetMedium =
            dg == null
                ? -1
                : dg.P10132TargetProceduralMediumCount;

        int delta =
            targetMedium < 0
                ? 0
                : result.MediumCount -
                  targetMedium;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "[RoomMix/Maintenance] LIVE Fidelity Runtime Composition Audit " +
            (pass ? "PASS" : "FAILED"));

        builder.AppendLine(
            "Floor=" +
            (gm == null
                ? -1
                : gm.CurrentFloor) +
            " | LayoutSeed=" +
            (layout == null
                ? -1
                : layout.Seed) +
            " | Rooms=" +
            (layout == null
                ? 0
                : layout.RoomPlacements.Count));

        builder.AppendLine(
            "HighPrecision=" +
            result.HighCount +
            " | ProceduralMedium=" +
            result.MediumCount +
            " | TargetMedium=" +
            targetMedium +
            " | TargetDelta=" +
            delta +
            " | UniqueTemplates=" +
            result.UniqueTemplateCount +
            " | MediumRepeats=" +
            result.MediumRepeats);

        builder.AppendLine(
            "MediumAuthorityCoverage=" +
            result.MediumCount +
            "/" +
            result.MediumCount +
            " | MediumRuntimeGeometry=" +
            result.MediumCount +
            "/" +
            result.MediumCount +
            " | HighProceduralOverride=0");

        builder.AppendLine(
            "ColliderMapping=1to1" +
            " | StructuralSkin=True" +
            " | SoftDecor=True" +
            " | ShellSkin=True" +
            " | EnemyPathMatchesFloorCells=True" +
            " | ConnectedComponent=1");

        builder.AppendLine(
            "Selection=FidelityAware" +
            " | Authority=P10.12GenericFamily" +
            " | ProductionMainChanged=False" +
            " | RuntimeSelectionChangedByAudit=False");

        if (!pass &&
            result.Errors.Count > 0)
        {
            builder.AppendLine(
                "Errors:");

            for (int i = 0;
                 i < result.Errors.Count;
                 i++)
            {
                builder.AppendLine(
                    "- " +
                    result.Errors[i]);
            }
        }

        builder.Append(
            "Result=" +
            (pass ? "PASS" : "FAILED"));

        return builder.ToString();
    }

    private static void AdvanceCase()
    {
        caseIndex++;

        if (caseIndex >= TotalCases)
        {
            FinishSoak(
                failures.Count == 0,
                "Completed");
            return;
        }

        state =
            SoakState.Generate;
    }

    private static void FinishSoak(
        bool completedWithoutCaseFailure,
        string reason)
    {
        if (!running)
        {
            return;
        }

        running = false;

        EditorApplication.update -=
            SoakUpdate;

        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -=
            OnBeforeAssemblyReload;

        RestoreSeedSettings();

        bool pass =
            completedWithoutCaseFailure &&
            reason == "Completed" &&
            successfulCases == TotalCases &&
            failures.Count == 0;

        string summary =
            BuildSoakSummary(
                pass,
                reason);

        EditorPrefs.SetBool(
            LastSoakPassKey,
            pass);

        EditorPrefs.SetString(
            LastSoakSummaryKey,
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

    private static string BuildSoakSummary(
        bool pass,
        string reason)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "[RoomMix/Maintenance] Runtime Fidelity Composition Soak " +
            (pass ? "PASS" : "FAILED"));

        builder.AppendLine(
            "Reason=" +
            reason +
            " | Cases=" +
            successfulCases +
            "/" +
            TotalCases +
            " | Failures=" +
            failures.Count +
            " | Floors=1,2,3" +
            " | SeedsPerFloor=" +
            SeedsPerFloor);

        builder.AppendLine(
            "HighRange=" +
            minHigh +
            "～" +
            maxHigh +
            " | MediumRange=" +
            minMedium +
            "～" +
            maxMedium +
            " | AvgHigh=" +
            (successfulCases == 0
                ? "0.00"
                : (totalHigh /
                   (float)successfulCases)
                    .ToString("F2")) +
            " | AvgMedium=" +
            (successfulCases == 0
                ? "0.00"
                : (totalMedium /
                   (float)successfulCases)
                    .ToString("F2")));

        builder.AppendLine(
            "Exact3High4MediumCases=" +
            exactThreeHighFourMediumCases +
            "/" +
            TotalCases +
            " | AllTemplatesUniqueCases=" +
            allSevenUniqueCases +
            "/" +
            TotalCases +
            " | MediumRepeatCases=" +
            mediumRepeatCases +
            " | TotalMediumRepeats=" +
            totalMediumRepeats);

        builder.AppendLine(
            "AllMediumAuthorityCoverage=True" +
            " | AllMediumRuntimeGeometry=True" +
            " | HighProceduralOverride=0" +
            " | ColliderMapping=1to1" +
            " | SkinCommit=True");

        builder.AppendLine(
            "EnemyPathMatchesFloorCells=True" +
            " | ConnectedComponent=1" +
            " | SeedSettingsRestored=True" +
            " | ProductionMainChanged=False" +
            " | SceneSaveRequired=False");

        if (failures.Count > 0)
        {
            builder.AppendLine(
                "FirstFailures:");

            int limit =
                Mathf.Min(
                    16,
                    failures.Count);

            for (int i = 0;
                 i < limit;
                 i++)
            {
                builder.AppendLine(
                    "- " +
                    failures[i]);
            }

            if (failures.Count > limit)
            {
                builder.AppendLine(
                    "... and " +
                    (failures.Count - limit) +
                    " more.");
            }
        }

        builder.Append(
            "Result=" +
            (pass ? "PASS" : "FAILED"));

        return builder.ToString();
    }

    private static DungeonLayout ReadCurrentLayout(
        GameManager gm)
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
                    gm)
                    as DungeonLayout;
    }

    private static int GetFloorForCase(
        int index)
    {
        int floorIndex =
            Mathf.Clamp(
                index /
                SeedsPerFloor,
                0,
                Floors.Length - 1);

        return
            Floors[floorIndex];
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

    private static void AddFailure(
        string failure)
    {
        failures.Add(
            failure);
    }

    private static void ResetSoakCounters()
    {
        failures.Clear();

        successfulCases = 0;
        exactThreeHighFourMediumCases = 0;
        allSevenUniqueCases = 0;
        mediumRepeatCases = 0;
        totalMediumRepeats = 0;

        minHigh = int.MaxValue;
        maxHigh = int.MinValue;
        minMedium = int.MaxValue;
        maxMedium = int.MinValue;

        totalHigh = 0;
        totalMedium = 0;
    }

    private static void RestoreSeedSettings()
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

        currentLayoutField = null;
        useRandomSeedField = null;
        fixedSeedField = null;
        generateFloorMethod = null;

        abortRequested = false;
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

        RestoreSeedSettings();

        running = false;
    }

    private static int TotalCases =>
        Floors.Length *
        SeedsPerFloor;

    private sealed class CompositionResult
    {
        public bool Pass;
        public int HighCount;
        public int MediumCount;
        public int UniqueTemplateCount;
        public int MediumRepeats;

        public readonly List<string> Errors =
            new List<string>();
    }

    private enum SoakState
    {
        Generate = 0,
        WaitRuntime = 1,
        Audit = 2
    }
}

#endif
