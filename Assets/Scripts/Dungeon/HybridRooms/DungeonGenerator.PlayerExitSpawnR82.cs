using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R8.2：把 Player／Exit 的安全出生格提交为 DungeonLayout 权威数据。
///
/// R5 仍决定 Start Room 与图距离最远的 Exit Room；
/// R6 仍决定 Socket 与走廊。本文件只在两者完成后：
/// 1. 在 R5 已选中的房间内调用 R8.1 Resolver；
/// 2. 有对应 SpawnPoint 时按 Kind／RandomWeight 确定性选择；
/// 3. 没有时明确回退到原代表性 Walkable Cell；
/// 4. 用最终格重建 DungeonLayout，不给 Manager 增加临时偏移。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const int R82PlayerSelectionSalt = 820201;
    private const int R82ExitSelectionSalt = 820202;

    [Header("R8.2 受控失败测试")]
    [Tooltip(
        "只用于 R8.2 受控失败：把全部 FloorCells 视为已保留，" +
        "强制 Player Spawn Cell 解析被拒绝。" +
        "正常运行必须关闭；不会修改 Layout 或 Prefab。")]
    [SerializeField]
    private bool r82InjectNoLegalPlayerCellForControlledFailure;

    /// <summary>
    /// 只在正式 Hybrid 运行时入口中调用。
    /// 成功时返回一份结构与 R6 相同、但 StartCell／ExitCell 已提交的布局；
    /// 失败时返回 null，让 GameManager 使用既有 Requested／Effective 回退流程。
    /// </summary>
    private bool R82TryApplyPlayerAndExitSpawnCells(
        DungeonLayout sourceLayout,
        out DungeonLayout committedLayout,
        out string report)
    {
        committedLayout = null;

        if (sourceLayout == null)
        {
            report = R82BuildFailureReport(
                "BaseLayout",
                null,
                -1,
                -1,
                "R6 Layout 不能为空。");

            return false;
        }

        List<string> sourceErrors =
            sourceLayout.GetValidationErrors();

        sourceErrors.AddRange(
            GetSocketCorridorValidationErrors(
                sourceLayout));

        if (!sourceLayout.HasHybridRoomData)
        {
            sourceErrors.Add(
                "R8.2 只接受 HasHybridRoomData=True 的布局。");
        }

        if (sourceErrors.Count > 0)
        {
            report = R82BuildFailureReport(
                "BaseLayoutValidation",
                sourceLayout,
                -1,
                -1,
                R82JoinErrors(sourceErrors));

            return false;
        }

        int startRoomIndex =
            R82FindRoomIndexContainingWalkableCell(
                sourceLayout,
                sourceLayout.StartCell);

        int exitRoomIndex =
            R82FindRoomIndexContainingWalkableCell(
                sourceLayout,
                sourceLayout.ExitCell);

        if (startRoomIndex < 0 || exitRoomIndex < 0)
        {
            report = R82BuildFailureReport(
                "ResolveR5RoomIndices",
                sourceLayout,
                startRoomIndex,
                exitRoomIndex,
                "无法从 R5 StartCell／ExitCell 找到所属 " +
                "RoomPlacement。");

            return false;
        }

        if (startRoomIndex == exitRoomIndex)
        {
            report = R82BuildFailureReport(
                "ResolveR5RoomIndices",
                sourceLayout,
                startRoomIndex,
                exitRoomIndex,
                "R5 Start Room 与 Exit Room 不能相同。");

            return false;
        }

        HashSet<Vector2Int> reservedCells =
            new HashSet<Vector2Int>();

        if (r82InjectNoLegalPlayerCellForControlledFailure)
        {
            reservedCells.UnionWith(
                sourceLayout.FloorCells);
        }

        DungeonSpawnCellRequest playerRequest =
            new DungeonSpawnCellRequest(
                sourceLayout,
                DreamRoomSpawnPointKind.Player,
                new int[] { startRoomIndex },
                selectionSalt: R82PlayerSelectionSalt,
                reservedCells: reservedCells,
                excludeExitCell: true,
                preferredCell: sourceLayout.StartCell,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        DungeonSpawnCellResult playerResult;
        string playerFailureReason;

        if (!DungeonSpawnCellResolver.TryResolve(
                playerRequest,
                out playerResult,
                out playerFailureReason))
        {
            report = R82BuildFailureReport(
                "Player",
                sourceLayout,
                startRoomIndex,
                exitRoomIndex,
                playerFailureReason);

            return false;
        }

        reservedCells.Add(playerResult.Cell);

        DungeonSpawnCellRequest exitRequest =
            new DungeonSpawnCellRequest(
                sourceLayout,
                DreamRoomSpawnPointKind.Exit,
                new int[] { exitRoomIndex },
                selectionSalt: R82ExitSelectionSalt,
                reservedCells: reservedCells,
                excludeStartCell: true,
                preferredCell: sourceLayout.ExitCell,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        DungeonSpawnCellResult exitResult;
        string exitFailureReason;

        if (!DungeonSpawnCellResolver.TryResolve(
                exitRequest,
                out exitResult,
                out exitFailureReason))
        {
            report = R82BuildFailureReport(
                "Exit",
                sourceLayout,
                startRoomIndex,
                exitRoomIndex,
                exitFailureReason);

            return false;
        }

        DungeonLayout candidateLayout =
            DungeonLayout.CreateHybrid(
                sourceLayout.RoomPlacements,
                sourceLayout.CorridorCells,
                sourceLayout.Connections,
                playerResult.Cell,
                exitResult.Cell,
                sourceLayout.Seed);

        List<string> commitErrors =
            R82GetCommitValidationErrors(
                sourceLayout,
                candidateLayout,
                startRoomIndex,
                exitRoomIndex,
                playerResult,
                exitResult);

        if (commitErrors.Count > 0)
        {
            report = R82BuildFailureReport(
                "CommitValidation",
                sourceLayout,
                startRoomIndex,
                exitRoomIndex,
                R82JoinErrors(commitErrors));

            return false;
        }

        committedLayout = candidateLayout;
        report = R82BuildSuccessReport(
            sourceLayout,
            committedLayout,
            startRoomIndex,
            exitRoomIndex,
            playerResult,
            exitResult);

        return true;
    }

    private List<string> R82GetCommitValidationErrors(
        DungeonLayout sourceLayout,
        DungeonLayout candidateLayout,
        int startRoomIndex,
        int exitRoomIndex,
        DungeonSpawnCellResult playerResult,
        DungeonSpawnCellResult exitResult)
    {
        List<string> errors = new List<string>();

        if (candidateLayout == null)
        {
            errors.Add("提交候选 DungeonLayout 不能为空。");
            return errors;
        }

        if (playerResult == null || exitResult == null)
        {
            errors.Add("Player／Exit Resolver Result 不能为空。");
            return errors;
        }

        if (playerResult.Cell == exitResult.Cell)
        {
            errors.Add("Player 与 Exit 不能提交到同一个 Cell。");
        }

        if (candidateLayout.StartCell != playerResult.Cell)
        {
            errors.Add(
                "DungeonLayout.StartCell 没有采用 Player Resolver Result。");
        }

        if (candidateLayout.ExitCell != exitResult.Cell)
        {
            errors.Add(
                "DungeonLayout.ExitCell 没有采用 Exit Resolver Result。");
        }

        if (!candidateLayout.FloorCells.Contains(
                candidateLayout.StartCell) ||
            !candidateLayout.FloorCells.Contains(
                candidateLayout.ExitCell))
        {
            errors.Add(
                "最终 StartCell／ExitCell 必须属于 FloorCells。");
        }

        if (!R82PlacementContainsWalkableCell(
                sourceLayout,
                startRoomIndex,
                candidateLayout.StartCell))
        {
            errors.Add(
                "最终 StartCell 不属于 R5 选择的 Start Room Walkable Cells。");
        }

        if (!R82PlacementContainsWalkableCell(
                sourceLayout,
                exitRoomIndex,
                candidateLayout.ExitCell))
        {
            errors.Add(
                "最终 ExitCell 不属于 R5 选择的 Exit Room Walkable Cells。");
        }

        R82ValidateR6StructureUnchanged(
            sourceLayout,
            candidateLayout,
            errors);

        errors.AddRange(
            candidateLayout.GetValidationErrors());

        errors.AddRange(
            GetSocketCorridorValidationErrors(
                candidateLayout));

        return errors;
    }

    private static void R82ValidateR6StructureUnchanged(
        DungeonLayout sourceLayout,
        DungeonLayout candidateLayout,
        List<string> errors)
    {
        if (candidateLayout.Seed != sourceLayout.Seed)
        {
            errors.Add("R8.2 修改了 R6 Layout Seed。");
        }

        if (!candidateLayout.FloorCells.SetEquals(
                sourceLayout.FloorCells))
        {
            errors.Add("R8.2 修改了 R6 FloorCells。");
        }

        if (!candidateLayout.RoomCells.SetEquals(
                sourceLayout.RoomCells))
        {
            errors.Add("R8.2 修改了 R6 RoomCells。");
        }

        if (!candidateLayout.CorridorCells.SetEquals(
                sourceLayout.CorridorCells))
        {
            errors.Add("R8.2 修改了 R6 CorridorCells。");
        }

        if (candidateLayout.Rooms.Count !=
            sourceLayout.Rooms.Count)
        {
            errors.Add("R8.2 修改了 R6 Rooms 数量。");
        }
        else
        {
            for (int i = 0;
                 i < sourceLayout.Rooms.Count;
                 i++)
            {
                if (!candidateLayout.Rooms[i].Equals(
                        sourceLayout.Rooms[i]))
                {
                    errors.Add(
                        "R8.2 修改了 R6 Rooms Element " +
                        i + "。");
                    break;
                }
            }
        }

        if (candidateLayout.RoomPlacements.Count !=
            sourceLayout.RoomPlacements.Count)
        {
            errors.Add(
                "R8.2 修改了 R6 RoomPlacements 数量。");
        }
        else
        {
            for (int i = 0;
                 i < sourceLayout.RoomPlacements.Count;
                 i++)
            {
                if (!object.ReferenceEquals(
                        candidateLayout.RoomPlacements[i],
                        sourceLayout.RoomPlacements[i]))
                {
                    errors.Add(
                        "R8.2 替换了 R6 RoomPlacement " +
                        i + " 的数据对象。");
                    break;
                }
            }
        }

        if (candidateLayout.Connections.Count !=
            sourceLayout.Connections.Count)
        {
            errors.Add(
                "R8.2 修改了 R6 Connections 数量。");
        }
        else
        {
            for (int i = 0;
                 i < sourceLayout.Connections.Count;
                 i++)
            {
                if (!object.ReferenceEquals(
                        candidateLayout.Connections[i],
                        sourceLayout.Connections[i]))
                {
                    errors.Add(
                        "R8.2 替换了 R6 Connection " +
                        i + " 的数据对象。");
                    break;
                }
            }
        }
    }

    private static int
        R82FindRoomIndexContainingWalkableCell(
            DungeonLayout layout,
            Vector2Int requestedCell)
    {
        if (layout == null)
        {
            return -1;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int roomIndex = 0;
             roomIndex < layout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            if (placement == null)
            {
                continue;
            }

            placement.GetWalkableGlobalCells(
                walkableCells);

            if (walkableCells.Contains(requestedCell))
            {
                return roomIndex;
            }
        }

        return -1;
    }

    private static bool R82PlacementContainsWalkableCell(
        DungeonLayout layout,
        int roomIndex,
        Vector2Int requestedCell)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count)
        {
            return false;
        }

        DreamRoomPlacement placement =
            layout.RoomPlacements[roomIndex];

        if (placement == null)
        {
            return false;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        placement.GetWalkableGlobalCells(
            walkableCells);

        return walkableCells.Contains(requestedCell);
    }

    private string R82BuildSuccessReport(
        DungeonLayout sourceLayout,
        DungeonLayout committedLayout,
        int startRoomIndex,
        int exitRoomIndex,
        DungeonSpawnCellResult playerResult,
        DungeonSpawnCellResult exitResult)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R8.2] Player/Exit SpawnCell " +
            "已写入 DungeonLayout");

        builder.AppendLine(
            "Player Requested=SpawnPoint(Player)" +
            " | Effective=" + playerResult.Source +
            " | RoomIndex=" + startRoomIndex +
            " | " + playerResult);

        builder.AppendLine(
            "Exit Requested=SpawnPoint(Exit)" +
            " | Effective=" + exitResult.Source +
            " | RoomIndex=" + exitRoomIndex +
            " | " + exitResult);

        builder.AppendLine(
            "Original Start=" + sourceLayout.StartCell +
            " | Final Start=" + committedLayout.StartCell +
            " | Original Exit=" + sourceLayout.ExitCell +
            " | Final Exit=" + committedLayout.ExitCell);

        builder.AppendLine(
            "R5 Room Choice=Preserved" +
            " | R6 Structure=Unchanged" +
            " | Layout Authority=StartCell/ExitCell" +
            " | Manager Offset=None" +
            " | ControlledFailure=False");

        return builder.ToString().TrimEnd();
    }

    private string R82BuildFailureReport(
        string stage,
        DungeonLayout sourceLayout,
        int startRoomIndex,
        int exitRoomIndex,
        string failureReason)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R8.2] Player/Exit SpawnCell " +
            "提交被拒绝");

        builder.AppendLine(
            "Stage=" + stage +
            " | ControlledFailure=" +
            r82InjectNoLegalPlayerCellForControlledFailure +
            " | StartRoomIndex=" + startRoomIndex +
            " | ExitRoomIndex=" + exitRoomIndex);

        if (sourceLayout != null)
        {
            builder.AppendLine(
                "Seed=" + sourceLayout.Seed +
                " | FloorCells=" +
                sourceLayout.FloorCells.Count +
                " | InjectedReserved=" +
                (r82InjectNoLegalPlayerCellForControlledFailure
                    ? sourceLayout.FloorCells.Count
                    : 0));
        }

        builder.AppendLine(
            "R6 Layout Mutation=None" +
            " | Hybrid Runtime Layout Commit=None");

        builder.Append(
            "Reason=" +
            (string.IsNullOrEmpty(failureReason)
                ? "Unknown"
                : failureReason));

        return builder.ToString();
    }

    private static string R82JoinErrors(
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.Append(errors[i]);

            if (i < errors.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
