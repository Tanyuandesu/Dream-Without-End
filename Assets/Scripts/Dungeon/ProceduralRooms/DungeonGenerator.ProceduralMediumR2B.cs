using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-1 R2B：第一個受控 Authoritative Procedural Room Commit。
///
/// 只處理 TemplateId == Graybox_13x09 的第一個 RoomPlacement。
/// 生成發生在 R6 Socket/Connection 已提交之後、R8.2 Player/Exit 選點之前。
/// 因此後續 Renderer、Spawn 與 EnemyPathService 都讀同一份重建後 DungeonLayout。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const string P1012R2BTargetTemplateId =
        "Graybox_13x09";

    private const string P1012R2BSourceId =
        "P10.12A-1_R2B_Medium13x9";

    [Header("P10.12A-1 R2B 中精度权威接入")]
    [Tooltip(
        "受控阶段只把每层第一个 Graybox_13x09 转成中精度程序房。" +
        "关闭即可恢复原有 R6 -> R8.2 行为。")]
    [SerializeField]
    private bool p1012R2BEnableMedium13x9AuthorityCommit = true;

    [Tooltip(
        "测试阶段在程序障碍上显示半透明红色方块。" +
        "只影响调试显示，不影响 FloorCells / Collider。")]
    [SerializeField]
    private bool p1012R2BDrawDebugObstacles = true;

    public bool P1012R2BEnabled =>
        p1012R2BEnableMedium13x9AuthorityCommit;

    private bool P1012R2BTryApplyControlledMedium13x9Authority(
        DungeonLayout sourceLayout,
        out DungeonLayout committedLayout,
        out string report)
    {
        committedLayout = sourceLayout;

        if (!p1012R2BEnableMedium13x9AuthorityCommit)
        {
            report =
                "[DungeonGenerator/P10.12A-1 R2B] Authority Commit disabled." +
                " LayoutUnchanged=True";
            return true;
        }

        if (sourceLayout == null ||
            !sourceLayout.HasHybridRoomData)
        {
            report =
                P1012R2BBuildFailureReport(
                    "BaseLayout",
                    -1,
                    "R2B 只接受有效 Hybrid DungeonLayout。");
            committedLayout = null;
            return false;
        }

        int targetRoomIndex = -1;
        DreamRoomPlacement targetPlacement = null;

        for (int i = 0;
             i < sourceLayout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement candidate =
                sourceLayout.RoomPlacements[i];

            if (candidate == null ||
                candidate.Template == null)
            {
                continue;
            }

            if (string.Equals(
                    candidate.Template.TemplateId,
                    P1012R2BTargetTemplateId,
                    StringComparison.Ordinal))
            {
                targetRoomIndex = i;
                targetPlacement = candidate;
                break;
            }
        }

        // 某些随机布局没有 13x9 Graybox，这不是失败。
        if (targetPlacement == null)
        {
            report =
                "[DungeonGenerator/P10.12A-1 R2B] No target this floor." +
                " Target=" + P1012R2BTargetTemplateId +
                " | LayoutUnchanged=True";
            return true;
        }

        if (targetPlacement.Template.SizeInCells !=
            DreamProceduralRoomKernelP1012A1.MediumSize)
        {
            report =
                P1012R2BBuildFailureReport(
                    "TargetSize",
                    targetRoomIndex,
                    "目标模板不是 13x9。");
            committedLayout = null;
            return false;
        }

        List<DreamRoomDoorSocket> usedSockets;
        string socketFailure;

        if (!P1012R2BTryCollectUsedSockets(
                sourceLayout,
                targetRoomIndex,
                targetPlacement,
                out usedSockets,
                out socketFailure))
        {
            report =
                P1012R2BBuildFailureReport(
                    "UsedSockets",
                    targetRoomIndex,
                    socketFailure);
            committedLayout = null;
            return false;
        }

        bool north = false;
        bool east = false;
        bool south = false;
        bool west = false;

        for (int i = 0; i < usedSockets.Count; i++)
        {
            switch (usedSockets[i].Direction)
            {
                case DreamRoomDoorDirection.North:
                    north = true;
                    break;
                case DreamRoomDoorDirection.East:
                    east = true;
                    break;
                case DreamRoomDoorDirection.South:
                    south = true;
                    break;
                case DreamRoomDoorDirection.West:
                    west = true;
                    break;
            }
        }

        List<DreamProceduralDoorLane> kernelDoors =
            DreamProceduralRoomKernelP1012A1.BuildDefaultDoorSet(
                north,
                east,
                south,
                west);

        string doorContractFailure;

        if (!P1012R2BValidateSocketContract(
                usedSockets,
                kernelDoors,
                out doorContractFailure))
        {
            report =
                P1012R2BBuildFailureReport(
                    "SocketContract",
                    targetRoomIndex,
                    doorContractFailure);
            committedLayout = null;
            return false;
        }

        int socketMask =
            (north ? 1 : 0) |
            (east ? 2 : 0) |
            (south ? 4 : 0) |
            (west ? 8 : 0);

        int proceduralSeed =
            DreamProceduralRoomKernelP1012A1.DeriveRoomSeed(
                sourceLayout.Seed,
                targetRoomIndex,
                1309,
                socketMask);

        DreamProceduralRoomLayout proceduralLayout;
        string generationFailure;

        if (!DreamProceduralRoomKernelP1012A1.TryGenerate(
                proceduralSeed,
                kernelDoors,
                out proceduralLayout,
                out generationFailure))
        {
            report =
                P1012R2BBuildFailureReport(
                    "Kernel",
                    targetRoomIndex,
                    generationFailure);
            committedLayout = null;
            return false;
        }

        HashSet<Vector2Int> globalBlocked =
            new HashSet<Vector2Int>();

        foreach (Vector2Int localBlocked in
                 proceduralLayout.BlockedCells)
        {
            Vector2Int globalCell =
                targetPlacement.OriginalToGlobalCell(
                    localBlocked);

            if (!targetPlacement.CellBounds.Contains(globalCell))
            {
                report =
                    P1012R2BBuildFailureReport(
                        "GlobalMapping",
                        targetRoomIndex,
                        "Blocked Cell 越出 Placement：" +
                        globalCell + "。");
                committedLayout = null;
                return false;
            }

            if (!sourceLayout.FloorCells.Contains(globalCell))
            {
                report =
                    P1012R2BBuildFailureReport(
                        "FloorMembership",
                        targetRoomIndex,
                        "Blocked Cell 原本不属于 FloorCells：" +
                        globalCell + "。");
                committedLayout = null;
                return false;
            }

            if (sourceLayout.CorridorCells.Contains(globalCell))
            {
                report =
                    P1012R2BBuildFailureReport(
                        "CorridorOverlap",
                        targetRoomIndex,
                        "Blocked Cell 与 Corridor 重叠：" +
                        globalCell + "。");
                committedLayout = null;
                return false;
            }

            if (globalCell == sourceLayout.StartCell ||
                globalCell == sourceLayout.ExitCell)
            {
                report =
                    P1012R2BBuildFailureReport(
                        "StartExitOverlap",
                        targetRoomIndex,
                        "Blocked Cell 命中 R5 Start/Exit：" +
                        globalCell + "。");
                committedLayout = null;
                return false;
            }

            globalBlocked.Add(globalCell);
        }

        List<Vector2Int> usedDoorCells =
            new List<Vector2Int>();

        for (int i = 0; i < usedSockets.Count; i++)
        {
            targetPlacement.GetSocketInsideCells(
                usedSockets[i],
                usedDoorCells);

            for (int c = 0;
                 c < usedDoorCells.Count;
                 c++)
            {
                if (globalBlocked.Contains(
                        usedDoorCells[c]))
                {
                    report =
                        P1012R2BBuildFailureReport(
                            "UsedDoorOverlap",
                            targetRoomIndex,
                            "Blocked Cell 命中 Used Socket：" +
                            usedDoorCells[c] + "。");
                    committedLayout = null;
                    return false;
                }
            }
        }

        string placementFailure;

        if (!targetPlacement.TryApplyRuntimeProceduralOverride(
                proceduralLayout.BlockedCells,
                proceduralSeed,
                proceduralLayout.Archetype,
                P1012R2BSourceId,
                p1012R2BDrawDebugObstacles,
                out placementFailure))
        {
            report =
                P1012R2BBuildFailureReport(
                    "PlacementCommit",
                    targetRoomIndex,
                    placementFailure);
            committedLayout = null;
            return false;
        }

        DungeonLayout candidateLayout = null;

        try
        {
            candidateLayout =
                DungeonLayout.CreateHybrid(
                    sourceLayout.RoomPlacements,
                    sourceLayout.CorridorCells,
                    sourceLayout.Connections,
                    sourceLayout.StartCell,
                    sourceLayout.ExitCell,
                    sourceLayout.Seed);

            List<string> errors =
                candidateLayout.GetValidationErrors();

            errors.AddRange(
                GetSocketCorridorValidationErrors(
                    candidateLayout));

            if (candidateLayout.FloorCells.Count !=
                sourceLayout.FloorCells.Count -
                globalBlocked.Count)
            {
                errors.Add(
                    "FloorCells 数量变化不等于 Procedural Blocked 数量。" +
                    " Before=" + sourceLayout.FloorCells.Count +
                    " After=" + candidateLayout.FloorCells.Count +
                    " Blocked=" + globalBlocked.Count + "。");
            }

            foreach (Vector2Int blocked in globalBlocked)
            {
                if (candidateLayout.FloorCells.Contains(blocked) ||
                    candidateLayout.RoomCells.Contains(blocked))
                {
                    errors.Add(
                        "程序 Blocked Cell 仍存在于权威 Walkable 集合：" +
                        blocked + "。");
                    break;
                }
            }

            if (!P1012R2BAllFloorCellsConnected(
                    candidateLayout.FloorCells,
                    candidateLayout.StartCell))
            {
                errors.Add(
                    "程序化提交后 FloorCells 不再全局连通。");
            }

            if (errors.Count > 0)
            {
                targetPlacement.ClearRuntimeProceduralOverride();
                committedLayout = null;
                report =
                    P1012R2BBuildFailureReport(
                        "CandidateValidation",
                        targetRoomIndex,
                        string.Join("\n", errors));
                return false;
            }
        }
        catch (Exception exception)
        {
            targetPlacement.ClearRuntimeProceduralOverride();
            committedLayout = null;
            report =
                P1012R2BBuildFailureReport(
                    "Exception",
                    targetRoomIndex,
                    exception.ToString());
            return false;
        }

        committedLayout = candidateLayout;
        report =
            "[DungeonGenerator/P10.12A-1 R2B] Controlled Authority Commit PASS" +
            "\nTargetRoomIndex=" + targetRoomIndex +
            " | TemplateId=" +
            targetPlacement.Template.TemplateId +
            " | Seed=" + proceduralSeed +
            " | Archetype=" + proceduralLayout.Archetype +
            " | QuarterTurns=" +
            targetPlacement.ClockwiseQuarterTurns +
            "\nUsedSockets=" + usedSockets.Count +
            " | SocketMask=" + socketMask +
            " | LocalBlocked=" +
            proceduralLayout.BlockedCells.Count +
            " | GlobalBlocked=" + globalBlocked.Count +
            "\nFloorCells=" +
            sourceLayout.FloorCells.Count +
            "->" + candidateLayout.FloorCells.Count +
            " | RoomCells=" +
            sourceLayout.RoomCells.Count +
            "->" + candidateLayout.RoomCells.Count +
            " | CorridorCells=" +
            candidateLayout.CorridorCells.Count +
            "\nAuthority=DreamRoomPlacement.RuntimeOverride" +
            " | ColliderSource=SameLocalBlockedCells" +
            " | StartExitPreserved=True" +
            " | ProductionMainChanged=False";

        return true;
    }

    private static bool P1012R2BTryCollectUsedSockets(
        DungeonLayout layout,
        int roomIndex,
        DreamRoomPlacement placement,
        out List<DreamRoomDoorSocket> sockets,
        out string failureReason)
    {
        sockets = new List<DreamRoomDoorSocket>();
        failureReason = string.Empty;
        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

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

            if (connection.RoomAIndex == roomIndex)
            {
                socketId = connection.SocketAId;
            }
            else if (connection.RoomBIndex == roomIndex)
            {
                socketId = connection.SocketBId;
            }

            if (string.IsNullOrWhiteSpace(socketId))
            {
                continue;
            }

            if (!ids.Add(socketId))
            {
                failureReason =
                    "同一 Socket 被多条 Connection 重复使用：" +
                    socketId + "。";
                return false;
            }

            DreamRoomDoorSocket socket;

            if (!placement.Template.TryGetSocket(
                    socketId,
                    out socket) ||
                socket == null)
            {
                failureReason =
                    "Template 找不到 Connection Socket：" +
                    socketId + "。";
                return false;
            }

            sockets.Add(socket);
        }

        if (sockets.Count == 0)
        {
            failureReason =
                "目标房间没有任何 Used Socket。";
            return false;
        }

        return true;
    }

    private static bool P1012R2BValidateSocketContract(
        List<DreamRoomDoorSocket> actualSockets,
        IReadOnlyList<DreamProceduralDoorLane> expectedDoors,
        out string failureReason)
    {
        failureReason = string.Empty;

        for (int i = 0; i < actualSockets.Count; i++)
        {
            DreamRoomDoorSocket actualSocket =
                actualSockets[i];

            DreamProceduralDoorLane expected = null;

            for (int d = 0;
                 d < expectedDoors.Count;
                 d++)
            {
                if (expectedDoors[d].Direction ==
                    actualSocket.Direction)
                {
                    expected = expectedDoors[d];
                    break;
                }
            }

            if (expected == null)
            {
                failureReason =
                    "Kernel 缺少方向 " +
                    actualSocket.Direction + "。";
                return false;
            }

            HashSet<Vector2Int> actualCells =
                new HashSet<Vector2Int>(
                    actualSocket.GetLocalInsideCells());

            HashSet<Vector2Int> expectedCells =
                new HashSet<Vector2Int>(
                    expected.LocalInsideCells);

            if (!actualCells.SetEquals(expectedCells))
            {
                failureReason =
                    "Socket " + actualSocket.SocketId +
                    " 的 LocalInsideCells 与 13x9 Kernel 契约不一致。";
                return false;
            }
        }

        return true;
    }

    private static bool P1012R2BAllFloorCellsConnected(
        HashSet<Vector2Int> floorCells,
        Vector2Int startCell)
    {
        if (floorCells == null ||
            floorCells.Count == 0 ||
            !floorCells.Contains(startCell))
        {
            return false;
        }

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        visited.Add(startCell);
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int i = 0;
                 i < directions.Length;
                 i++)
            {
                Vector2Int next =
                    current + directions[i];

                if (floorCells.Contains(next) &&
                    visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited.Count == floorCells.Count;
    }

    private static string P1012R2BBuildFailureReport(
        string stage,
        int roomIndex,
        string reason)
    {
        return
            "[DungeonGenerator/P10.12A-1 R2B] Controlled Authority Commit FAILED" +
            "\nStage=" + stage +
            " | RoomIndex=" + roomIndex +
            " | Target=" + P1012R2BTargetTemplateId +
            "\n" + reason +
            "\nPartialCommit=False" +
            " | ProductionMainChanged=False";
    }
}
