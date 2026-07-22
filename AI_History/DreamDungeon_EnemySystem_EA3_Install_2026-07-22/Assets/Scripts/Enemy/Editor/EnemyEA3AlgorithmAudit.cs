using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyEA3AlgorithmAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Enemy System/Run EA3 Algorithm Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        int passedCases = 0;
        GameObject auditObject = null;

        try
        {
            auditObject = new GameObject("EA3_Algorithm_Audit");
            auditObject.hideFlags = HideFlags.HideAndDontSave;

            EnemyPathService service =
                auditObject.AddComponent<EnemyPathService>();

            RunStraightAndSameCellCases(
                service,
                errors,
                ref passedCases);

            RunTurnAndShortestRouteCases(
                service,
                errors,
                ref passedCases);

            RunFailureCases(
                service,
                errors,
                ref passedCases);

            RunRecoveryAndCornerCases(
                service,
                errors,
                ref passedCases);
        }
        finally
        {
            if (auditObject != null)
            {
                Object.DestroyImmediate(auditObject);
            }
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Enemy System/EA3] Algorithm Audit");
        report.AppendLine("CasesPassed=" + passedCases + "/8");
        report.AppendLine("TopologyBaseline=FourDirections");
        report.AppendLine("PriorityQueue=BinaryMinHeap");
        report.AppendLine("WaypointSimplification=Disabled");

        if (errors.Count == 0 && passedCases == 8)
        {
            report.AppendLine("Result=PASS");
            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine("Result=FAIL");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("ERROR: " + errors[i]);
        }

        Debug.LogError(report.ToString());
    }

    private static void RunStraightAndSameCellCases(
        EnemyPathService service,
        List<string> errors,
        ref int passedCases)
    {
        HashSet<Vector2Int> straightCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0));

        InitializeService(
            service,
            straightCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult straight = service.FindPathImmediate(
            Vector2.zero,
            new Vector2(4f, 0f));

        ValidateCase(
            "Straight corridor",
            straight.Success &&
            straight.WorldPath.Count == 4 &&
            straight.PathCost == 40,
            straight,
            errors,
            ref passedCases);

        EnemyPathResult sameCell = service.FindPathImmediate(
            new Vector2(2.1f, 0f),
            new Vector2(2.3f, 0f));

        ValidateCase(
            "Same-cell empty success",
            sameCell.Success &&
            sameCell.WorldPath.Count == 0 &&
            sameCell.PathCost == 0,
            sameCell,
            errors,
            ref passedCases);
    }

    private static void RunTurnAndShortestRouteCases(
        EnemyPathService service,
        List<string> errors,
        ref int passedCases)
    {
        HashSet<Vector2Int> turnCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3));

        InitializeService(
            service,
            turnCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult multiTurn = service.FindPathImmediate(
            Vector2.zero,
            new Vector2(3f, 3f));

        ValidateCase(
            "Non-rectangular multi-turn route",
            multiTurn.Success &&
            multiTurn.WorldPath.Count == 6 &&
            multiTurn.GoalCell == new Vector2Int(3, 3),
            multiTurn,
            errors,
            ref passedCases);

        HashSet<Vector2Int> detourCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(0, 1),
                new Vector2Int(2, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(2, 2));

        InitializeService(
            service,
            detourCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult shortestDetour =
            service.FindPathImmediate(
                new Vector2(0f, 1f),
                new Vector2(2f, 1f));

        ValidateCase(
            "Priority-queue shortest detour",
            shortestDetour.Success &&
            shortestDetour.WorldPath.Count == 4 &&
            shortestDetour.PathCost == 40,
            shortestDetour,
            errors,
            ref passedCases);
    }

    private static void RunFailureCases(
        EnemyPathService service,
        List<string> errors,
        ref int passedCases)
    {
        HashSet<Vector2Int> disconnectedCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(5, 0));

        InitializeService(
            service,
            disconnectedCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult unreachable =
            service.FindPathImmediate(
                Vector2.zero,
                new Vector2(5f, 0f));

        ValidateCase(
            "Connectivity precheck failure",
            !unreachable.Success &&
            unreachable.FailureReason ==
                EnemyPathFailureReason.Unreachable,
            unreachable,
            errors,
            ref passedCases);

        HashSet<Vector2Int> limitedCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0));

        InitializeService(
            service,
            limitedCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult costLimited =
            service.FindPathImmediate(
                Vector2.zero,
                new Vector2(4f, 0f),
                2);

        ValidateCase(
            "Path-cost limit failure",
            !costLimited.Success &&
            costLimited.FailureReason ==
                EnemyPathFailureReason.PathCostLimitExceeded,
            costLimited,
            errors,
            ref passedCases);
    }

    private static void RunRecoveryAndCornerCases(
        EnemyPathService service,
        List<string> errors,
        ref int passedCases)
    {
        HashSet<Vector2Int> recoveryCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

        InitializeService(
            service,
            recoveryCells,
            EnemyNavigationTopology.FourDirections);

        EnemyPathResult recoveredStart =
            service.FindPathImmediate(
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f));

        ValidateCase(
            "Off-grid start recovery",
            recoveredStart.Success &&
            recoveredStart.StartCellAdjusted &&
            recoveredStart.ResolvedStartCell ==
                Vector2Int.zero,
            recoveredStart,
            errors,
            ref passedCases);

        HashSet<Vector2Int> cornerCells =
            CreateCells(
                new Vector2Int(0, 0),
                new Vector2Int(1, 1));

        InitializeService(
            service,
            cornerCells,
            EnemyNavigationTopology.EightDirectionsNoCornerCutting);

        EnemyPathResult cornerCut =
            service.FindPathImmediate(
                Vector2.zero,
                Vector2.one);

        ValidateCase(
            "Eight-direction corner-cut rejection",
            !cornerCut.Success &&
            cornerCut.FailureReason ==
                EnemyPathFailureReason.Unreachable,
            cornerCut,
            errors,
            ref passedCases);
    }

    private static void InitializeService(
        EnemyPathService service,
        HashSet<Vector2Int> floorCells,
        EnemyNavigationTopology topology)
    {
        DungeonLayout layout = new DungeonLayout(
            new List<RectInt>(),
            floorCells,
            Vector2Int.zero,
            Vector2Int.zero,
            3003);

        service.Initialize(
            layout,
            1f,
            topology,
            2,
            4096,
            1,
            false);
    }

    private static HashSet<Vector2Int> CreateCells(
        params Vector2Int[] cells)
    {
        return new HashSet<Vector2Int>(cells);
    }

    private static void ValidateCase(
        string caseName,
        bool condition,
        EnemyPathResult result,
        List<string> errors,
        ref int passedCases)
    {
        if (condition)
        {
            passedCases++;
            return;
        }

        errors.Add(
            caseName + " failed. Success=" +
            (result != null && result.Success) +
            " | Reason=" +
            (result != null
                ? result.FailureReason.ToString()
                : "No result") +
            " | Details=" +
            (result != null ? result.Details : string.Empty));
    }
}
