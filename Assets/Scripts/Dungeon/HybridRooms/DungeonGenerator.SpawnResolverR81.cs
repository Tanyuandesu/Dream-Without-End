using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R8.1 的只读诊断入口。
///
/// Context Menu 只在内存中生成 Layout 并调用解析器；
/// 不建立 GameObject、不修改 Prefab、不替换正式地图，也不接入 Manager。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const int R81PreviewFloor = 1;
    private const int R81WeightedSampleCount = 256;

    [ContextMenu("R8.1/Validate Spawn Cell Resolver")]
    private void ValidateR81SpawnCellResolver()
    {
        List<string> errors = new List<string>();

        DungeonLayout hybridLayout;
        string hybridReport;

        if (!TryGenerateHybridRuntimeLayout(
                R81PreviewFloor,
                out hybridLayout,
                out hybridReport) ||
            hybridLayout == null)
        {
            errors.Add(
                "无法建立 R8.1 Hybrid 诊断 Layout。\n" +
                hybridReport);
            LogR81Errors(errors);
            return;
        }

        AppendR81Errors(
            "Hybrid Layout",
            hybridLayout.GetValidationErrors(),
            errors);

        HashSet<Vector2Int> originalFloorCells =
            new HashSet<Vector2Int>(
                hybridLayout.FloorCells);

        HashSet<Vector2Int> originalRoomCells =
            new HashSet<Vector2Int>(
                hybridLayout.RoomCells);

        HashSet<Vector2Int> originalCorridorCells =
            new HashSet<Vector2Int>(
                hybridLayout.CorridorCells);

        int startRoomIndex = FindR81RoomIndex(
            hybridLayout,
            hybridLayout.StartCell);

        int exitRoomIndex = FindR81RoomIndex(
            hybridLayout,
            hybridLayout.ExitCell);

        if (startRoomIndex < 0)
        {
            errors.Add("找不到包含 StartCell 的 RoomPlacement。");
        }

        if (exitRoomIndex < 0)
        {
            errors.Add("找不到包含 ExitCell 的 RoomPlacement。");
        }

        DungeonSpawnCellResult playerResult = null;
        DungeonSpawnCellResult exitResult = null;
        DungeonSpawnCellResult itemResult = null;

        HashSet<Vector2Int> reservedCells =
            new HashSet<Vector2Int>();

        if (startRoomIndex >= 0)
        {
            DungeonSpawnCellRequest playerRequest =
                new DungeonSpawnCellRequest(
                    hybridLayout,
                    DreamRoomSpawnPointKind.Player,
                    new int[] { startRoomIndex },
                    selectionSalt: 8101,
                    reservedCells: reservedCells,
                    excludeExitCell: true,
                    preferredCell: hybridLayout.StartCell,
                    allowWalkableFallback: true);

            TryResolveR81(
                "Player",
                playerRequest,
                errors,
                out playerResult);

            if (playerResult != null)
            {
                reservedCells.Add(playerResult.Cell);
            }
        }

        if (exitRoomIndex >= 0)
        {
            DungeonSpawnCellRequest exitRequest =
                new DungeonSpawnCellRequest(
                    hybridLayout,
                    DreamRoomSpawnPointKind.Exit,
                    new int[] { exitRoomIndex },
                    selectionSalt: 8102,
                    reservedCells: reservedCells,
                    excludeStartCell: true,
                    preferredCell: hybridLayout.ExitCell,
                    allowWalkableFallback: true);

            TryResolveR81(
                "Exit",
                exitRequest,
                errors,
                out exitResult);

            if (exitResult != null)
            {
                reservedCells.Add(exitResult.Cell);
            }
        }

        List<DungeonSpawnCellResult> enemyResults =
            ResolveR81UniqueEnemies(
                hybridLayout,
                reservedCells,
                errors);

        DungeonSpawnCellRequest itemRequest =
            new DungeonSpawnCellRequest(
                hybridLayout,
                DreamRoomSpawnPointKind.Item,
                allowedRoomIndices: null,
                selectionSalt: 8301,
                reservedCells: reservedCells,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 4,
                minimumDistanceFromExit: 2,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        TryResolveR81(
            "Item",
            itemRequest,
            errors,
            out itemResult);

        ValidateR81Determinism(
            hybridLayout,
            reservedCells,
            errors);

        int lightWeightSelections;
        int heavyWeightSelections;

        ValidateR81WeightedSelection(
            errors,
            out lightWeightSelections,
            out heavyWeightSelections);

        DungeonSpawnCellResult legacyResult =
            ValidateR81LegacyLayout(errors);

        ValidateR81LayoutUnchanged(
            hybridLayout,
            originalFloorCells,
            originalRoomCells,
            originalCorridorCells,
            errors);

        if (errors.Count > 0)
        {
            LogR81Errors(errors);
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DungeonSpawnCellResolver/R8.1] 自检通过");

        report.AppendLine(
            "Hybrid：Floor=" + R81PreviewFloor +
            " | Seed=" + hybridLayout.Seed +
            " | Rooms=" + hybridLayout.RoomPlacements.Count +
            " | FloorCells=" + hybridLayout.FloorCells.Count);

        report.AppendLine(
            "Player：" + FormatR81Result(playerResult));

        report.AppendLine(
            "Exit：" + FormatR81Result(exitResult));

        report.AppendLine(
            "Enemy Unique：" + enemyResults.Count +
            "/4 | ReservedCells 无重复：通过");

        report.AppendLine(
            "Item：" + FormatR81Result(itemResult));

        report.AppendLine(
            "Deterministic Repeat：通过");

        report.AppendLine(
            "Weighted Selection：通过" +
            " | Weight1=" + lightWeightSelections +
            " | Weight4=" + heavyWeightSelections +
            " | Samples=" + R81WeightedSampleCount);

        report.AppendLine(
            "Legacy Procedural：" +
            FormatR81Result(legacyResult));

        report.AppendLine(
            "Layout／Prefab／Scene Mutation：None");

        report.AppendLine(
            "Manager Integration：Player/Exit=R8.2 Integrated" +
            " | Enemy/Item=Not Yet（R8.3）");

        Debug.Log(report.ToString(), this);
    }

    [ContextMenu("R8.1/Run Controlled Failure (No Legal Cells)")]
    private void RunR81ControlledFailure()
    {
        DungeonLayout layout;
        string hybridReport;

        if (!TryGenerateHybridRuntimeLayout(
                R81PreviewFloor,
                out layout,
                out hybridReport) ||
            layout == null)
        {
            Debug.LogError(
                "[DungeonSpawnCellResolver/R8.1] " +
                "受控失败准备失败：无法建立 Hybrid Layout。\n" +
                hybridReport,
                this);
            return;
        }

        HashSet<Vector2Int> allFloorCellsReserved =
            new HashSet<Vector2Int>(layout.FloorCells);

        HashSet<Vector2Int> originalRoomCells =
            new HashSet<Vector2Int>(layout.RoomCells);

        HashSet<Vector2Int> originalCorridorCells =
            new HashSet<Vector2Int>(layout.CorridorCells);

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Enemy,
                allowedRoomIndices: null,
                selectionSalt: 8999,
                reservedCells: allFloorCellsReserved,
                excludeStartCell: true,
                excludeExitCell: true,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        DungeonSpawnCellResult unexpectedResult;
        string rejectionReason;

        bool resolved = DungeonSpawnCellResolver.TryResolve(
            request,
            out unexpectedResult,
            out rejectionReason);

        bool layoutUnchanged =
            layout.FloorCells.SetEquals(
                allFloorCellsReserved) &&
            layout.RoomCells.SetEquals(
                originalRoomCells) &&
            layout.CorridorCells.SetEquals(
                originalCorridorCells);

        if (resolved)
        {
            Debug.LogError(
                "[DungeonSpawnCellResolver/R8.1] " +
                "受控失败测试失败：全部 FloorCells 已保留，" +
                "解析器却返回了 " + unexpectedResult + "。",
                this);
            return;
        }

        Debug.LogWarning(
            "[DungeonSpawnCellResolver/R8.1] " +
            "ControlledFailure=RejectedAsExpected" +
            " | Reserved=" + allFloorCellsReserved.Count +
            "/" + layout.FloorCells.Count +
            " | LayoutUnchanged=" + layoutUnchanged +
            " | SceneMutation=None" +
            "\nReason=" + rejectionReason,
            this);
    }

    private static List<DungeonSpawnCellResult>
        ResolveR81UniqueEnemies(
            DungeonLayout layout,
            HashSet<Vector2Int> reservedCells,
            List<string> errors)
    {
        List<DungeonSpawnCellResult> results =
            new List<DungeonSpawnCellResult>();

        for (int i = 0; i < 4; i++)
        {
            DungeonSpawnCellRequest request =
                new DungeonSpawnCellRequest(
                    layout,
                    DreamRoomSpawnPointKind.Enemy,
                    allowedRoomIndices: null,
                    selectionSalt: 8200 + i,
                    reservedCells: reservedCells,
                    excludeStartCell: true,
                    excludeExitCell: true,
                    allowWalkableFallback: true,
                    allowLayoutWideFallback: true);

            DungeonSpawnCellResult result;

            TryResolveR81(
                "Enemy " + (i + 1),
                request,
                errors,
                out result);

            if (result == null)
            {
                continue;
            }

            if (!reservedCells.Add(result.Cell))
            {
                errors.Add(
                    "Enemy " + (i + 1) +
                    " 返回了重复 Reserved Cell " +
                    result.Cell + "。");
            }

            results.Add(result);
        }

        if (results.Count != 4)
        {
            errors.Add(
                "应解析 4 个唯一 Enemy Cell，实际为 " +
                results.Count + "。");
        }

        return results;
    }

    private static void ValidateR81Determinism(
        DungeonLayout layout,
        HashSet<Vector2Int> reservedCells,
        List<string> errors)
    {
        HashSet<Vector2Int> firstReservedCopy =
            new HashSet<Vector2Int>(reservedCells);

        HashSet<Vector2Int> secondReservedCopy =
            new HashSet<Vector2Int>(reservedCells);

        DungeonSpawnCellRequest firstRequest =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Generic,
                allowedRoomIndices: null,
                selectionSalt: 8401,
                reservedCells: firstReservedCopy,
                excludeStartCell: true,
                excludeExitCell: true,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        DungeonSpawnCellRequest secondRequest =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Generic,
                allowedRoomIndices: null,
                selectionSalt: 8401,
                reservedCells: secondReservedCopy,
                excludeStartCell: true,
                excludeExitCell: true,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        DungeonSpawnCellResult firstResult;
        DungeonSpawnCellResult secondResult;
        string firstFailure;
        string secondFailure;

        bool firstResolved = DungeonSpawnCellResolver.TryResolve(
            firstRequest,
            out firstResult,
            out firstFailure);

        bool secondResolved = DungeonSpawnCellResolver.TryResolve(
            secondRequest,
            out secondResult,
            out secondFailure);

        if (!firstResolved || !secondResolved)
        {
            errors.Add(
                "确定性重复测试无法解析。First=" +
                firstFailure + " | Second=" + secondFailure);
            return;
        }

        if (firstResult.Cell != secondResult.Cell ||
            firstResult.Source != secondResult.Source ||
            firstResult.SelectionSeed !=
            secondResult.SelectionSeed)
        {
            errors.Add(
                "相同 Layout Seed／Salt／候选集合未返回相同结果。" +
                " First=" + firstResult +
                " | Second=" + secondResult);
        }
    }

    private static void ValidateR81WeightedSelection(
        List<string> errors,
        out int lightSelections,
        out int heavySelections)
    {
        lightSelections = 0;
        heavySelections = 0;

        DungeonSpawnCellCandidate lightCandidate =
            new DungeonSpawnCellCandidate(
                new Vector2Int(10, 10),
                weight: 1,
                roomIndex: 0,
                sourceId: "Weight_1",
                source: DungeonSpawnCellSource
                    .ExplicitSpawnPoint);

        DungeonSpawnCellCandidate heavyCandidate =
            new DungeonSpawnCellCandidate(
                new Vector2Int(20, 20),
                weight: 4,
                roomIndex: 0,
                sourceId: "Weight_4",
                source: DungeonSpawnCellSource
                    .ExplicitSpawnPoint);

        List<DungeonSpawnCellCandidate> candidates =
            new List<DungeonSpawnCellCandidate>
            {
                heavyCandidate,
                lightCandidate
            };

        DungeonSpawnCellCandidate firstPassResult;
        int firstPassSeed;
        string firstPassFailure;

        if (!DungeonSpawnCellResolver
                .TrySelectCandidateForDiagnostics(
                    candidates,
                    layoutSeed: 24680,
                    kind: DreamRoomSpawnPointKind.Enemy,
                    selectionSalt: 0,
                    selected: out firstPassResult,
                    selectionSeed: out firstPassSeed,
                    failureReason: out firstPassFailure))
        {
            errors.Add(
                "权重选择基础测试失败：" +
                firstPassFailure);
            return;
        }

        DungeonSpawnCellCandidate repeatedResult;
        int repeatedSeed;
        string repeatedFailure;

        if (!DungeonSpawnCellResolver
                .TrySelectCandidateForDiagnostics(
                    candidates,
                    layoutSeed: 24680,
                    kind: DreamRoomSpawnPointKind.Enemy,
                    selectionSalt: 0,
                    selected: out repeatedResult,
                    selectionSeed: out repeatedSeed,
                    failureReason: out repeatedFailure) ||
            repeatedResult.Cell != firstPassResult.Cell ||
            repeatedSeed != firstPassSeed)
        {
            errors.Add(
                "权重选择相同输入重复结果不一致。" +
                " Reason=" + repeatedFailure);
            return;
        }

        for (int salt = 0;
             salt < R81WeightedSampleCount;
             salt++)
        {
            DungeonSpawnCellCandidate selected;
            int selectionSeed;
            string failureReason;

            if (!DungeonSpawnCellResolver
                    .TrySelectCandidateForDiagnostics(
                        candidates,
                        layoutSeed: 24680,
                        kind: DreamRoomSpawnPointKind.Enemy,
                        selectionSalt: salt,
                        selected: out selected,
                        selectionSeed: out selectionSeed,
                        failureReason: out failureReason))
            {
                errors.Add(
                    "权重样本 " + salt +
                    " 无法选择：" + failureReason);
                return;
            }

            if (selected.Cell == lightCandidate.Cell)
            {
                lightSelections++;
            }
            else if (selected.Cell == heavyCandidate.Cell)
            {
                heavySelections++;
            }
            else
            {
                errors.Add(
                    "权重样本返回未知候选 " +
                    selected.Cell + "。");
                return;
            }
        }

        if (heavySelections <= lightSelections)
        {
            errors.Add(
                "Weight 4 候选没有比 Weight 1 候选获得更多样本。" +
                " Weight1=" + lightSelections +
                " | Weight4=" + heavySelections + "。");
        }
    }

    private DungeonSpawnCellResult ValidateR81LegacyLayout(
        List<string> errors)
    {
        DungeonLayout legacyLayout;

        try
        {
            legacyLayout = Generate(R81PreviewFloor);
        }
        catch (System.Exception exception)
        {
            errors.Add(
                "Legacy DungeonGenerator.Generate(int) 抛出异常：\n" +
                exception);
            return null;
        }

        if (legacyLayout == null)
        {
            errors.Add(
                "Legacy DungeonGenerator.Generate(int) 返回 null。");
            return null;
        }

        AppendR81Errors(
            "Legacy Layout",
            legacyLayout.GetValidationErrors(),
            errors);

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                legacyLayout,
                DreamRoomSpawnPointKind.Enemy,
                allowedRoomIndices: null,
                selectionSalt: 8501,
                excludeStartCell: true,
                excludeExitCell: true,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        DungeonSpawnCellResult result;

        TryResolveR81(
            "Legacy Procedural",
            request,
            errors,
            out result);

        if (result != null && result.UsedExplicitSpawnPoint)
        {
            errors.Add(
                "Legacy Layout 不应返回 Explicit SpawnPoint。");
        }

        return result;
    }

    private static void ValidateR81LayoutUnchanged(
        DungeonLayout layout,
        HashSet<Vector2Int> originalFloorCells,
        HashSet<Vector2Int> originalRoomCells,
        HashSet<Vector2Int> originalCorridorCells,
        List<string> errors)
    {
        if (!layout.FloorCells.SetEquals(originalFloorCells))
        {
            errors.Add("解析器修改了 Layout.FloorCells。");
        }

        if (!layout.RoomCells.SetEquals(originalRoomCells))
        {
            errors.Add("解析器修改了 Layout.RoomCells。");
        }

        if (!layout.CorridorCells.SetEquals(
                originalCorridorCells))
        {
            errors.Add("解析器修改了 Layout.CorridorCells。");
        }
    }

    private static bool TryResolveR81(
        string label,
        DungeonSpawnCellRequest request,
        List<string> errors,
        out DungeonSpawnCellResult result)
    {
        string failureReason;

        if (!DungeonSpawnCellResolver.TryResolve(
                request,
                out result,
                out failureReason))
        {
            errors.Add(label + "：" + failureReason);
            return false;
        }

        if (!request.Layout.FloorCells.Contains(result.Cell))
        {
            errors.Add(
                label + " 返回了 FloorCells 之外的格子 " +
                result.Cell + "。");
            return false;
        }

        if (request.IsReserved(result.Cell))
        {
            errors.Add(
                label + " 返回了 Reserved Cell " +
                result.Cell + "。");
            return false;
        }

        return true;
    }

    private static int FindR81RoomIndex(
        DungeonLayout layout,
        Vector2Int requestedCell)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

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

            placement.GetWalkableGlobalCells(cells);

            if (cells.Contains(requestedCell))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AppendR81Errors(
        string label,
        List<string> sourceErrors,
        List<string> targetErrors)
    {
        for (int i = 0; i < sourceErrors.Count; i++)
        {
            targetErrors.Add(
                label + "：" + sourceErrors[i]);
        }
    }

    private static string FormatR81Result(
        DungeonSpawnCellResult result)
    {
        return result == null
            ? "None"
            : result.ToString();
    }

    private void LogR81Errors(List<string> errors)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DungeonSpawnCellResolver/R8.1] 自检失败");

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        report.AppendLine(
            "没有接入 Manager；正式运行行为与 Prefab Asset 未修改。");

        Debug.LogError(report.ToString(), this);
    }
}
