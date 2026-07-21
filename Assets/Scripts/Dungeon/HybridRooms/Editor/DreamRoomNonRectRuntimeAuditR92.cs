using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.2 编辑器验收工具。
///
/// 职责只有三项：
/// 1. 把 GameScene 的 R4 Catalog 临时切到 R9.1 独立测试 Catalog；
/// 2. 只读生成一份 Floor 1 Hybrid Layout，区分矩形边界框与真实占格；
/// 3. 把 Catalog 恢复为 R3 Graybox 并保存 GameScene。
///
/// 本工具不修改 R4/R5/R6、Renderer、Spawn Resolver 或任何运行时算法。
/// </summary>
public static class DreamRoomNonRectRuntimeAuditR92
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string TestCatalogPath =
        "Assets/DreamDungeon/Generated/R9_1_NonRectSample/" +
        "Catalog/RoomCatalog_R91_NonRectTest.asset";

    private const string GrayboxCatalogPath =
        "Assets/DreamDungeon/Generated/R3_Graybox/" +
        "Catalog/RoomCatalog_Graybox.asset";

    private const string ExpectedTestCatalogId =
        "NonRect_R91_Test";

    private const string ExpectedTemplateId =
        "R91_LShape_10x08";

    private const int ExpectedBoundsCellsPerRoom = 80;
    private const int ExpectedOccupiedCellsPerRoom = 50;
    private const int ExpectedWalkableCellsPerRoom = 49;
    private const int ExpectedBlockedCellsPerRoom = 1;
    private const int ExpectedGapCellsPerRoom = 30;

    [MenuItem(
        MenuRoot + "Prepare Non-Rect Runtime Test (R9.2)",
        false,
        2210)]
    private static void PrepareRuntimeTest()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetSceneContext(
                requireTestCatalog: false,
                out context,
                out errors))
        {
            ReportSetupFailure(
                "R9.2 准备失败",
                errors);
            return;
        }

        DreamRoomCatalog testCatalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                TestCatalogPath);

        if (testCatalog == null)
        {
            errors.Add(
                "找不到 R9.1 测试 Catalog：" +
                TestCatalogPath);

            ReportSetupFailure(
                "R9.2 准备失败",
                errors);
            return;
        }

        List<string> catalogErrors =
            testCatalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                "R9.1 Test Catalog：" +
                catalogErrors[i]);
        }

        if (!string.Equals(
                testCatalog.CatalogId,
                ExpectedTestCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "测试 Catalog Id 应为 '" +
                ExpectedTestCatalogId +
                "'，实际为 '" +
                testCatalog.CatalogId + "'。");
        }

        if (errors.Count > 0)
        {
            ReportSetupFailure(
                "R9.2 准备失败",
                errors);
            return;
        }

        SetGeneratorCatalog(
            context.Generator,
            testCatalog,
            "R9.2 Use Non-Rect Test Catalog");

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        string auditReport;
        List<string> auditErrors;

        bool auditPassed = TryBuildAuditReport(
            context.Generator,
            out auditReport,
            out auditErrors);

        if (!auditPassed)
        {
            Debug.LogError(
                BuildFailureReport(
                    "[DreamRoomNonRectRuntimeAuditR92] " +
                    "R9.2 边界框／真实轮廓审计失败",
                    auditErrors),
                context.Generator);

            EditorUtility.DisplayDialog(
                "R9.2 Audit Failed",
                "测试 Catalog 已临时接入，但只读审计失败。\n\n" +
                "不要进入 Play Mode，也不要保存 GameScene。" +
                "请保留 Console 第一条红错。",
                "OK");

            return;
        }

        Debug.Log(
            "[DreamRoomNonRectRuntimeAuditR92] " +
            "R9.2 测试 Catalog 已临时接入。\n" +
            "SceneSaved=False | DiskBaseline=Graybox | " +
            "DoNotSaveUntilRestore=True\n" +
            auditReport,
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.2 Non-Rect Runtime Test Ready",
            "只读审计通过。GameScene 当前在内存中临时使用 " +
            "NonRect_R91_Test。\n\n" +
            "现在可以进入 Play Mode。测试结束后必须执行：\n" +
            "Tools > Dream Dungeon > " +
            "Restore and Save Graybox Baseline (R9.2)",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Validate Bounding vs Footprint (R9.2)",
        false,
        2220)]
    private static void ValidateBoundingVsFootprint()
    {
        SceneContext context;
        List<string> setupErrors;

        if (!TryGetSceneContext(
                requireTestCatalog: true,
                out context,
                out setupErrors))
        {
            ReportSetupFailure(
                "R9.2 校验无法开始",
                setupErrors);
            return;
        }

        string auditReport;
        List<string> auditErrors;

        if (!TryBuildAuditReport(
                context.Generator,
                out auditReport,
                out auditErrors))
        {
            Debug.LogError(
                BuildFailureReport(
                    "[DreamRoomNonRectRuntimeAuditR92] " +
                    "R9.2 边界框／真实轮廓审计失败",
                    auditErrors),
                context.Generator);

            return;
        }

        Debug.Log(
            auditReport,
            context.Generator);
    }

    [MenuItem(
        MenuRoot + "Restore and Save Graybox Baseline (R9.2)",
        false,
        2230)]
    private static void RestoreAndSaveGrayboxBaseline()
    {
        SceneContext context;
        List<string> errors;

        if (!TryGetSceneContext(
                requireTestCatalog: false,
                out context,
                out errors))
        {
            ReportSetupFailure(
                "R9.2 恢复失败",
                errors);
            return;
        }

        DreamRoomCatalog grayboxCatalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(
                GrayboxCatalogPath);

        if (grayboxCatalog == null)
        {
            errors.Add(
                "找不到 Graybox Catalog：" +
                GrayboxCatalogPath);

            ReportSetupFailure(
                "R9.2 恢复失败",
                errors);
            return;
        }

        List<string> catalogErrors =
            grayboxCatalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                "Graybox Catalog：" +
                catalogErrors[i]);
        }

        if (errors.Count > 0)
        {
            ReportSetupFailure(
                "R9.2 恢复失败",
                errors);
            return;
        }

        SetGeneratorCatalog(
            context.Generator,
            grayboxCatalog,
            "R9.2 Restore Graybox Catalog");

        EditorSceneManager.MarkSceneDirty(
            context.Scene);

        if (!EditorSceneManager.SaveScene(context.Scene))
        {
            Debug.LogError(
                "[DreamRoomNonRectRuntimeAuditR92] " +
                "Graybox Catalog 已恢复到 Scene 内存，" +
                "但 GameScene 保存失败。请手动 Ctrl+S。",
                context.Generator);

            return;
        }

        Debug.Log(
            "[DreamRoomNonRectRuntimeAuditR92] " +
            "R9.2 Graybox 基线已恢复并保存。\n" +
            "Catalog=" + grayboxCatalog.CatalogId +
            " | SceneSaved=True" +
            " | RenderMode=HybridPrefabRooms" +
            " | FixedSeed=12345" +
            " | TestAssetsRetained=True",
            context.Generator);

        EditorUtility.DisplayDialog(
            "R9.2 Graybox Baseline Restored",
            "GameScene 已恢复 RoomCatalog_Graybox 并保存。\n" +
            "R9.1 的独立测试资产仍完整保留。",
            "OK");
    }

    private static bool TryGetSceneContext(
        bool requireTestCatalog,
        out SceneContext context,
        out List<string> errors)
    {
        context = default(SceneContext);
        errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须先退出 Play Mode。");
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("当前没有已加载的有效 Scene。");
            return false;
        }

        if (!string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add(
                "必须先打开 GameScene：" +
                GameScenePath +
                "。当前 Scene=" + scene.path);
            return false;
        }

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

        EnemySpawner enemySpawner =
            FindSceneComponent<EnemySpawner>(scene);

        ItemSpawner itemSpawner =
            FindSceneComponent<ItemSpawner>(scene);

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。");
        }

        if (gameManager == null)
        {
            errors.Add("GameScene 中找不到 GameManager。");
        }

        if (enemySpawner == null)
        {
            errors.Add("GameScene 中找不到 EnemySpawner。");
        }

        if (itemSpawner == null)
        {
            errors.Add("GameScene 中找不到 ItemSpawner。");
        }

        if (errors.Count > 0)
        {
            return false;
        }

        if (renderer.RenderMode !=
            DungeonRenderMode.HybridPrefabRooms)
        {
            errors.Add(
                "DungeonRenderer.Render Mode 必须是 " +
                "Hybrid Prefab Rooms。");
        }

        RequireBool(
            generator,
            "useRandomSeed",
            expectedValue: false,
            "DungeonGenerator.Use Random Seed",
            errors);

        RequireInt(
            generator,
            "fixedSeed",
            expectedValue: 12345,
            "DungeonGenerator.Fixed Seed",
            errors);

        RequireBool(
            generator,
            "r82InjectNoLegalPlayerCellForControlledFailure",
            expectedValue: false,
            "R8.2 受控失败",
            errors);

        RequireBool(
            renderer,
            "r73InjectMissingSocketForControlledFailure",
            expectedValue: false,
            "R7.3 受控失败",
            errors);

        RequireBool(
            renderer,
            "r74InjectInvalidCorridorCellForControlledFailure",
            expectedValue: false,
            "R7.4 受控失败",
            errors);

        RequireBool(
            gameManager,
            "r84RejectNextFloorCommitForControlledFailure",
            expectedValue: false,
            "R8.4 受控失败",
            errors);

        RequireBool(
            enemySpawner,
            "r83InjectNoLegalEnemyCellForControlledFailure",
            expectedValue: false,
            "R8.3 Enemy 受控失败",
            errors);

        RequireBool(
            itemSpawner,
            "r83InjectNoLegalItemCellForControlledFailure",
            expectedValue: false,
            "R8.3 Item 受控失败",
            errors);

        if (requireTestCatalog &&
            (generator.TemplateFirstRoomCatalog == null ||
             !string.Equals(
                 generator.TemplateFirstRoomCatalog.CatalogId,
                 ExpectedTestCatalogId,
                 StringComparison.Ordinal)))
        {
            errors.Add(
                "当前 R4 Catalog 不是 " +
                ExpectedTestCatalogId +
                "。请先执行 Prepare Non-Rect Runtime Test (R9.2)。");
        }

        context = new SceneContext(
            scene,
            generator,
            renderer,
            gameManager,
            enemySpawner,
            itemSpawner);

        return errors.Count == 0;
    }

    private static bool TryBuildAuditReport(
        DungeonGenerator generator,
        out string report,
        out List<string> errors)
    {
        errors = new List<string>();
        report = string.Empty;

        if (generator == null)
        {
            errors.Add("DungeonGenerator 为空。");
            return false;
        }

        DreamRoomCatalog catalog =
            generator.TemplateFirstRoomCatalog;

        if (catalog == null ||
            !string.Equals(
                catalog.CatalogId,
                ExpectedTestCatalogId,
                StringComparison.Ordinal))
        {
            errors.Add(
                "R4 Catalog 必须是 " +
                ExpectedTestCatalogId + "。");
            return false;
        }

        DungeonLayout layout;
        string generationReport;

        bool generated;

        try
        {
            generated =
                generator.TryGenerateHybridRuntimeLayout(
                    1,
                    out layout,
                    out generationReport);
        }
        catch (Exception exception)
        {
            errors.Add(
                "只读生成抛出异常：\n" +
                exception);
            return false;
        }

        if (!generated || layout == null)
        {
            errors.Add(
                "Floor 1 Hybrid Layout 生成失败：\n" +
                generationReport);
            return false;
        }

        List<string> layoutErrors =
            layout.GetValidationErrors();

        for (int i = 0; i < layoutErrors.Count; i++)
        {
            errors.Add(
                "DungeonLayout：" +
                layoutErrors[i]);
        }

        int roomCount = layout.RoomPlacements.Count;
        int expectedRoomCount =
            generator.TemplateFirstDesiredRoomCount;

        if (roomCount != expectedRoomCount)
        {
            errors.Add(
                "RoomPlacements 应为 " +
                expectedRoomCount +
                "，实际为 " + roomCount + "。");
        }

        if (layout.Rooms.Count != roomCount)
        {
            errors.Add(
                "Legacy Rooms 数量应与 RoomPlacements 相同。" +
                " Rooms=" + layout.Rooms.Count +
                " / Placements=" + roomCount);
        }

        HashSet<Vector2Int> allBounds =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> allOccupied =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> allWalkable =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> allBlocked =
            new HashSet<Vector2Int>();

        HashSet<Vector2Int> allGaps =
            new HashSet<Vector2Int>();

        int[] rotationCounts = new int[4];
        int duplicateBoundsCells = 0;
        int duplicateOccupiedCells = 0;

        List<Vector2Int> occupiedBuffer =
            new List<Vector2Int>();

        List<Vector2Int> walkableBuffer =
            new List<Vector2Int>();

        List<Vector2Int> blockedBuffer =
            new List<Vector2Int>();

        for (int roomIndex = 0;
             roomIndex < roomCount;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null ||
                placement.Template == null)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 或其 Template 为空。");
                continue;
            }

            if (!string.Equals(
                    placement.Template.TemplateId,
                    ExpectedTemplateId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 使用了非测试模板 '" +
                    placement.Template.TemplateId + "'。");
            }

            int turns =
                DreamRoomPlacement.NormalizeQuarterTurns(
                    placement.ClockwiseQuarterTurns);

            rotationCounts[turns]++;

            RectInt bounds = placement.CellBounds;

            if (bounds.width * bounds.height !=
                ExpectedBoundsCellsPerRoom)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 的旋转后 Bounding Box 应为 80 格，实际为 " +
                    (bounds.width * bounds.height) + "。");
            }

            if (bounds.xMin < generator.TemplateFirstMapBorder ||
                bounds.yMin < generator.TemplateFirstMapBorder ||
                bounds.xMax >
                    generator.TemplateFirstMapWidth -
                    generator.TemplateFirstMapBorder ||
                bounds.yMax >
                    generator.TemplateFirstMapHeight -
                    generator.TemplateFirstMapBorder)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 超出 Map Border 安全范围。");
            }

            if (roomIndex < layout.Rooms.Count &&
                !SameRect(bounds, layout.Rooms[roomIndex]))
            {
                errors.Add(
                    "Legacy Rooms[" + roomIndex +
                    "] 没有保留该 Placement 的旋转后 Bounding Box。");
            }

            placement.GetOccupiedGlobalCells(
                occupiedBuffer);

            placement.GetWalkableGlobalCells(
                walkableBuffer);

            placement.GetBlockedGlobalCells(
                blockedBuffer);

            if (occupiedBuffer.Count !=
                ExpectedOccupiedCellsPerRoom)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 的 Occupied 应为 50，实际为 " +
                    occupiedBuffer.Count + "。");
            }

            if (walkableBuffer.Count !=
                ExpectedWalkableCellsPerRoom)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 的 Walkable 应为 49，实际为 " +
                    walkableBuffer.Count + "。");
            }

            if (blockedBuffer.Count !=
                ExpectedBlockedCellsPerRoom)
            {
                errors.Add(
                    "RoomPlacement " + roomIndex +
                    " 的 Blocked 应为 1，实际为 " +
                    blockedBuffer.Count + "。");
            }

            HashSet<Vector2Int> roomOccupied =
                new HashSet<Vector2Int>(occupiedBuffer);

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (!allBounds.Add(cell))
                    {
                        duplicateBoundsCells++;
                    }

                    if (!roomOccupied.Contains(cell))
                    {
                        allGaps.Add(cell);
                    }
                }
            }

            for (int i = 0; i < occupiedBuffer.Count; i++)
            {
                Vector2Int cell = occupiedBuffer[i];

                if (!bounds.Contains(cell))
                {
                    errors.Add(
                        "RoomPlacement " + roomIndex +
                        " 有 Occupied Cell 位于 Bounding Box 外：" +
                        cell);
                }

                if (!allOccupied.Add(cell))
                {
                    duplicateOccupiedCells++;
                }
            }

            allWalkable.UnionWith(walkableBuffer);
            allBlocked.UnionWith(blockedBuffer);
        }

        int paddedBoundsOverlaps = 0;

        for (int first = 0; first < roomCount; first++)
        {
            DreamRoomPlacement firstPlacement =
                layout.RoomPlacements[first];

            if (firstPlacement == null)
            {
                continue;
            }

            for (int second = first + 1;
                 second < roomCount;
                 second++)
            {
                DreamRoomPlacement secondPlacement =
                    layout.RoomPlacements[second];

                if (secondPlacement != null &&
                    firstPlacement.OverlapsWithPadding(
                        secondPlacement,
                        generator.TemplateFirstRoomPadding))
                {
                    paddedBoundsOverlaps++;
                }
            }
        }

        if (duplicateBoundsCells != 0)
        {
            errors.Add(
                "矩形 Bounding Box 出现重叠格：" +
                duplicateBoundsCells + "。");
        }

        if (paddedBoundsOverlaps != 0)
        {
            errors.Add(
                "Bounding Box + Padding 出现重叠房间对：" +
                paddedBoundsOverlaps + "。");
        }

        if (duplicateOccupiedCells != 0)
        {
            errors.Add(
                "真实 Occupied Cells 出现重叠格：" +
                duplicateOccupiedCells + "。");
        }

        int expectedBounds =
            roomCount * ExpectedBoundsCellsPerRoom;

        int expectedOccupied =
            roomCount * ExpectedOccupiedCellsPerRoom;

        int expectedWalkable =
            roomCount * ExpectedWalkableCellsPerRoom;

        int expectedBlocked =
            roomCount * ExpectedBlockedCellsPerRoom;

        int expectedGaps =
            roomCount * ExpectedGapCellsPerRoom;

        RequireCount(
            "Unique Bounding Cells",
            allBounds.Count,
            expectedBounds,
            errors);

        RequireCount(
            "Unique Occupied Cells",
            allOccupied.Count,
            expectedOccupied,
            errors);

        RequireCount(
            "Unique Walkable Cells",
            allWalkable.Count,
            expectedWalkable,
            errors);

        RequireCount(
            "Unique Blocked Cells",
            allBlocked.Count,
            expectedBlocked,
            errors);

        RequireCount(
            "Unique Bounding Gap Cells",
            allGaps.Count,
            expectedGaps,
            errors);

        if (!layout.RoomCells.SetEquals(allWalkable))
        {
            errors.Add(
                "Layout.RoomCells 不等于七间房的真实 Walkable 合集。");
        }

        HashSet<Vector2Int> expectedFloor =
            new HashSet<Vector2Int>(layout.RoomCells);

        expectedFloor.UnionWith(layout.CorridorCells);

        if (!layout.FloorCells.SetEquals(expectedFloor))
        {
            errors.Add(
                "Layout.FloorCells 不等于 RoomCells ∪ CorridorCells。");
        }

        HashSet<Vector2Int> gapInRoomCells =
            Intersect(allGaps, layout.RoomCells);

        HashSet<Vector2Int> gapInFloorCells =
            Intersect(allGaps, layout.FloorCells);

        HashSet<Vector2Int> gapUsedByCorridor =
            Intersect(allGaps, layout.CorridorCells);

        HashSet<Vector2Int> implicitGapFloor =
            new HashSet<Vector2Int>(gapInFloorCells);

        implicitGapFloor.ExceptWith(gapUsedByCorridor);

        HashSet<Vector2Int> blockedInFloor =
            Intersect(allBlocked, layout.FloorCells);

        HashSet<Vector2Int> corridorInOccupied =
            Intersect(layout.CorridorCells, allOccupied);

        if (gapInRoomCells.Count != 0)
        {
            errors.Add(
                "L 形缺口被错误写入 RoomCells：" +
                gapInRoomCells.Count + " 格。");
        }

        if (implicitGapFloor.Count != 0)
        {
            errors.Add(
                "L 形缺口中出现既非房间、也非走廊的隐式 FloorCells：" +
                implicitGapFloor.Count + " 格。");
        }

        if (!gapInFloorCells.SetEquals(gapUsedByCorridor))
        {
            errors.Add(
                "缺口中的 FloorCells 与合法 CorridorCells 不一致。");
        }

        if (blockedInFloor.Count != 0)
        {
            errors.Add(
                "Blocked Cells 被错误写入 FloorCells：" +
                blockedInFloor.Count + " 格。");
        }

        if (corridorInOccupied.Count != 0)
        {
            errors.Add(
                "CorridorCells 穿入真实 Occupied Cells：" +
                corridorInOccupied.Count + " 格。");
        }

        int routedConnections = 0;

        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DreamRoomConnection connection =
                layout.Connections[i];

            if (connection != null &&
                connection.HasAssignedSockets &&
                connection.HasCorridor)
            {
                routedConnections++;
            }
        }

        if (layout.Connections.Count <
            Math.Max(0, roomCount - 1))
        {
            errors.Add(
                "Connections 少于连接全部房间所需的树边数量。");
        }

        if (routedConnections != layout.Connections.Count)
        {
            errors.Add(
                "并非全部 Connection 都有 Socket 与 Corridor：" +
                routedConnections + "/" +
                layout.Connections.Count + "。");
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DreamRoomNonRectRuntimeAuditR92] " +
            (errors.Count == 0
                ? "R9.2 边界框／真实轮廓审计通过"
                : "R9.2 边界框／真实轮廓审计未通过"));

        builder.AppendLine(
            "Catalog=" + catalog.CatalogId +
            " | Floor=1" +
            " | Seed=" + layout.Seed);

        builder.AppendLine(
            "PlacementPolicy=BoundingBox+Padding(" +
            generator.TemplateFirstRoomPadding + ")" +
            " | OccupiedNesting=DisabledByDesign" +
            " | PaddedBoundsOverlaps=" +
            paddedBoundsOverlaps);

        builder.AppendLine(
            "Rooms=" + roomCount + "/" + expectedRoomCount +
            " | Bounds=" + allBounds.Count +
            " | Occupied=" + allOccupied.Count +
            " | Walkable(RoomCells)=" + allWalkable.Count +
            " | Blocked=" + allBlocked.Count +
            " | BoundingGaps=" + allGaps.Count);

        builder.AppendLine(
            "GapInRoomCells=" + gapInRoomCells.Count +
            " | GapInFloorCells=" + gapInFloorCells.Count +
            " | GapUsedByCorridor=" +
            gapUsedByCorridor.Count +
            " | ImplicitGapFloor=" +
            implicitGapFloor.Count);

        builder.AppendLine(
            "CorridorCells=" + layout.CorridorCells.Count +
            " | FloorCells=" + layout.FloorCells.Count +
            " | CorridorInOccupied=" +
            corridorInOccupied.Count +
            " | BlockedInFloor=" +
            blockedInFloor.Count);

        builder.AppendLine(
            "Connections=" + layout.Connections.Count +
            " | Routed=" + routedConnections +
            " | Rotations=" +
            "0°:" + rotationCounts[0] +
            "/90°:" + rotationCounts[1] +
            "/180°:" + rotationCounts[2] +
            "/270°:" + rotationCounts[3]);

        builder.AppendLine(
            "Result=" +
            (errors.Count == 0
                ? "Bounding box only protects placement; " +
                  "true footprint owns RoomCells."
                : "Rejected; see errors."));

        report = builder.ToString();
        return errors.Count == 0;
    }

    private static void SetGeneratorCatalog(
        DungeonGenerator generator,
        DreamRoomCatalog catalog,
        string undoLabel)
    {
        Undo.RecordObject(generator, undoLabel);

        SerializedObject serialized =
            new SerializedObject(generator);

        SerializedProperty property =
            serialized.FindProperty(
                "templateFirstRoomCatalog");

        if (property == null)
        {
            throw new InvalidOperationException(
                "DungeonGenerator 缺少 serialized field：" +
                "templateFirstRoomCatalog");
        }

        serialized.Update();
        property.objectReferenceValue = catalog;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(generator);
    }

    private static void RequireBool(
        UnityEngine.Object target,
        string propertyName,
        bool expectedValue,
        string label,
        List<string> errors)
    {
        SerializedObject serialized =
            new SerializedObject(target);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(
                label +
                "：找不到 serialized field '" +
                propertyName + "'。");
            return;
        }

        if (property.boolValue != expectedValue)
        {
            errors.Add(
                label +
                " 必须为 " + expectedValue +
                "，当前为 " + property.boolValue + "。");
        }
    }

    private static void RequireInt(
        UnityEngine.Object target,
        string propertyName,
        int expectedValue,
        string label,
        List<string> errors)
    {
        SerializedObject serialized =
            new SerializedObject(target);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            errors.Add(
                label +
                "：找不到 serialized field '" +
                propertyName + "'。");
            return;
        }

        if (property.intValue != expectedValue)
        {
            errors.Add(
                label +
                " 必须为 " + expectedValue +
                "，当前为 " + property.intValue + "。");
        }
    }

    private static void RequireCount(
        string label,
        int actual,
        int expected,
        List<string> errors)
    {
        if (actual != expected)
        {
            errors.Add(
                label +
                " 应为 " + expected +
                "，实际为 " + actual + "。");
        }
    }

    private static HashSet<Vector2Int> Intersect(
        IEnumerable<Vector2Int> first,
        IEnumerable<Vector2Int> second)
    {
        HashSet<Vector2Int> result =
            new HashSet<Vector2Int>(first);

        result.IntersectWith(second);
        return result;
    }

    private static bool SameRect(
        RectInt first,
        RectInt second)
    {
        return
            first.xMin == second.xMin &&
            first.yMin == second.yMin &&
            first.width == second.width &&
            first.height == second.height;
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] candidates =
            Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];

            if (candidate != null &&
                !EditorUtility.IsPersistent(candidate) &&
                candidate.gameObject.scene == scene)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void ReportSetupFailure(
        string title,
        List<string> errors)
    {
        string report = BuildFailureReport(
            "[DreamRoomNonRectRuntimeAuditR92] " + title,
            errors);

        Debug.LogError(report);

        EditorUtility.DisplayDialog(
            title,
            "Console 已列出阻断原因。修正前不要进入 Play Mode。",
            "OK");
    }

    private static string BuildFailureReport(
        string title,
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(title);

        if (errors == null || errors.Count == 0)
        {
            builder.AppendLine("- 未提供具体错误。");
            return builder.ToString();
        }

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine("- " + errors[i]);
        }

        return builder.ToString();
    }

    private struct SceneContext
    {
        public Scene Scene { get; }
        public DungeonGenerator Generator { get; }
        public DungeonRenderer Renderer { get; }
        public GameManager GameManager { get; }
        public EnemySpawner EnemySpawner { get; }
        public ItemSpawner ItemSpawner { get; }

        public SceneContext(
            Scene scene,
            DungeonGenerator generator,
            DungeonRenderer renderer,
            GameManager gameManager,
            EnemySpawner enemySpawner,
            ItemSpawner itemSpawner)
        {
            Scene = scene;
            Generator = generator;
            Renderer = renderer;
            GameManager = gameManager;
            EnemySpawner = enemySpawner;
            ItemSpawner = itemSpawner;
        }
    }
}
