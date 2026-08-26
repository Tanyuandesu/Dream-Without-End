using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12B-2：受控 Procedural Family Authority Commit。
///
/// 当前正式接入：
/// - Medium 13x09
/// - Small 08x06
///
/// 每个 Family 每层最多只转换第一个对应 Graybox Placement。
/// Wide / Tall 仍保持 Graybox，不在本阶段启用。
///
/// 注意：为了不增加第二条权威链，仍沿用原 R2B 文件位置与 RuntimeHybrid 调用点；
/// 但实现已经升级为 Generic Family Kernel。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const string P1012B2MediumTemplateId =
        "Graybox_13x09";

    private const string P1012B2SmallTemplateId =
        "Graybox_08x06";

    private const string P1012B2SourcePrefix =
        "P10.12B-2_Family_";

    [Header("P10.12B-2 中精度 Family 权威接入")]
    [Tooltip(
        "保留原序列化字段名以兼容现有 GameScene。" +
        "开启时每层第一个 Graybox_13x09 使用 Generic Family Kernel。")]
    [SerializeField]
    private bool p1012R2BEnableMedium13x9AuthorityCommit = true;

    [Tooltip(
        "B2 新增：每层第一个 Graybox_08x06 转换为 Small Procedural Family。" +
        "关闭后 Small 会完整回退为原 Graybox。")]
    [SerializeField]
    private bool p1012B2EnableSmall08x06AuthorityCommit = true;

    [Tooltip(
        "R2B/A3 调试桥。A3.1 Structural Skin 提交后会关闭旧红色 Debug Renderer；" +
        "此开关不会影响 FloorCells / Collider。")]
    [SerializeField]
    private bool p1012R2BDrawDebugObstacles = true;

    public bool P1012R2BEnabled =>
        p1012R2BEnableMedium13x9AuthorityCommit;

    public bool P1012B2Small08x06Enabled =>
        p1012B2EnableSmall08x06AuthorityCommit;

    /// <summary>
    /// 保留原方法名，避免修改已经稳定的 RuntimeHybrid 插入点。
    /// 实际上 B2 已经处理 Medium + Small 两个 Family。
    /// </summary>
    private bool P1012R2BTryApplyControlledMedium13x9Authority(
        DungeonLayout sourceLayout,
        out DungeonLayout committedLayout,
        out string report)
    {
        committedLayout = sourceLayout;

        if (!p1012R2BEnableMedium13x9AuthorityCommit &&
            !p1012B2EnableSmall08x06AuthorityCommit)
        {
            report =
                "[DungeonGenerator/P10.12B-2] Family Authority Commit disabled." +
                " LayoutUnchanged=True";
            return true;
        }

        if (sourceLayout == null ||
            !sourceLayout.HasHybridRoomData)
        {
            committedLayout = null;
            report =
                P1012B2BuildFailureReport(
                    "BaseLayout",
                    -1,
                    "B2 只接受有效 Hybrid DungeonLayout。");
            return false;
        }

        List<P1012B2Plan> plans =
            new List<P1012B2Plan>();

        if (p1012R2BEnableMedium13x9AuthorityCommit)
        {
            string mediumFailure;

            if (!P1012B2TryBuildPlanForFirstFamilyInstance(
                    sourceLayout,
                    DreamProceduralRoomFamilyRegistryP1012B1.Medium13x09,
                    out P1012B2Plan mediumPlan,
                    out mediumFailure))
            {
                committedLayout = null;
                report =
                    P1012B2BuildFailureReport(
                        "MediumPlan",
                        mediumPlan == null
                            ? -1
                            : mediumPlan.RoomIndex,
                        mediumFailure);
                return false;
            }

            if (mediumPlan != null)
            {
                plans.Add(mediumPlan);
            }
        }

        if (p1012B2EnableSmall08x06AuthorityCommit)
        {
            string smallFailure;

            if (!P1012B2TryBuildPlanForFirstFamilyInstance(
                    sourceLayout,
                    DreamProceduralRoomFamilyRegistryP1012B1.Small08x06,
                    out P1012B2Plan smallPlan,
                    out smallFailure))
            {
                committedLayout = null;
                report =
                    P1012B2BuildFailureReport(
                        "SmallPlan",
                        smallPlan == null
                            ? -1
                            : smallPlan.RoomIndex,
                        smallFailure);
                return false;
            }

            if (smallPlan != null)
            {
                plans.Add(smallPlan);
            }
        }

        // 某些随机楼层恰好没有 Medium / Small Graybox，这不是失败。
        if (plans.Count == 0)
        {
            report =
                "[DungeonGenerator/P10.12B-2] No enabled Family target this floor." +
                " MediumEnabled=" +
                p1012R2BEnableMedium13x9AuthorityCommit +
                " | SmallEnabled=" +
                p1012B2EnableSmall08x06AuthorityCommit +
                " | LayoutUnchanged=True";
            return true;
        }

        HashSet<Vector2Int> combinedGlobalBlocked =
            new HashSet<Vector2Int>();

        int expectedRemovedCells = 0;

        for (int i = 0;
             i < plans.Count;
             i++)
        {
            P1012B2Plan plan =
                plans[i];

            expectedRemovedCells +=
                plan.GlobalBlockedCells.Count;

            foreach (Vector2Int cell in
                     plan.GlobalBlockedCells)
            {
                if (!combinedGlobalBlocked.Add(cell))
                {
                    committedLayout = null;
                    report =
                        P1012B2BuildFailureReport(
                            "CrossFamilyOverlap",
                            plan.RoomIndex,
                            "不同 Procedural Family 的 Global Blocked Cell 重叠：" +
                            cell + "。");
                    return false;
                }
            }
        }

        List<DreamRoomPlacement> appliedPlacements =
            new List<DreamRoomPlacement>();

        for (int i = 0;
             i < plans.Count;
             i++)
        {
            P1012B2Plan plan =
                plans[i];

            string placementFailure;

            if (!plan.Placement
                    .TryApplyRuntimeProceduralOverride(
                        plan.ProceduralLayout.BlockedCells,
                        plan.ProceduralSeed,
                        plan.ProceduralLayout.Archetype,
                        P1012B2SourcePrefix +
                        plan.Profile.FamilyId,
                        p1012R2BDrawDebugObstacles,
                        out placementFailure))
            {
                P1012B2Rollback(
                    appliedPlacements);

                committedLayout = null;
                report =
                    P1012B2BuildFailureReport(
                        "PlacementCommit",
                        plan.RoomIndex,
                        plan.Profile.FamilyId +
                        "：" +
                        placementFailure);
                return false;
            }

            appliedPlacements.Add(
                plan.Placement);
        }

        DungeonLayout candidateLayout;

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

            int actualRemoved =
                sourceLayout.FloorCells.Count -
                candidateLayout.FloorCells.Count;

            if (actualRemoved !=
                expectedRemovedCells)
            {
                errors.Add(
                    "FloorCells 数量变化不等于 Family Blocked 总数。" +
                    " Before=" +
                    sourceLayout.FloorCells.Count +
                    " After=" +
                    candidateLayout.FloorCells.Count +
                    " Removed=" +
                    actualRemoved +
                    " ExpectedBlocked=" +
                    expectedRemovedCells + "。");
            }

            foreach (Vector2Int blocked in
                     combinedGlobalBlocked)
            {
                if (candidateLayout.FloorCells.Contains(
                        blocked) ||
                    candidateLayout.RoomCells.Contains(
                        blocked))
                {
                    errors.Add(
                        "Family Blocked Cell 仍存在于权威 Walkable：" +
                        blocked + "。");
                    break;
                }

                if (candidateLayout.CorridorCells.Contains(
                        blocked))
                {
                    errors.Add(
                        "Family Blocked Cell 与 Corridor 重叠：" +
                        blocked + "。");
                    break;
                }
            }

            if (!P1012B2AllFloorCellsConnected(
                    candidateLayout.FloorCells,
                    candidateLayout.StartCell))
            {
                errors.Add(
                    "Family 提交后 FloorCells 不再全局连通。");
            }

            if (errors.Count > 0)
            {
                P1012B2Rollback(
                    appliedPlacements);

                committedLayout = null;
                report =
                    P1012B2BuildFailureReport(
                        "CandidateValidation",
                        -1,
                        string.Join(
                            "\n",
                            errors));

                return false;
            }
        }
        catch (Exception exception)
        {
            P1012B2Rollback(
                appliedPlacements);

            committedLayout = null;
            report =
                P1012B2BuildFailureReport(
                    "Exception",
                    -1,
                    exception.ToString());
            return false;
        }

        committedLayout =
            candidateLayout;

        report =
            P1012B2BuildSuccessReport(
                sourceLayout,
                candidateLayout,
                plans,
                expectedRemovedCells);

        return true;
    }

    private bool P1012B2TryBuildPlanForFirstFamilyInstance(
        DungeonLayout sourceLayout,
        DreamProceduralRoomFamilyProfileP1012B1 profile,
        out P1012B2Plan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;

        if (profile == null)
        {
            failureReason =
                "Family Profile 为空。";
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
                    profile.TemplateId,
                    StringComparison.Ordinal))
            {
                targetRoomIndex = i;
                targetPlacement = candidate;
                break;
            }
        }

        // 本楼层没有这个 Family，不算失败。
        if (targetPlacement == null)
        {
            return true;
        }

        if (targetPlacement.Template.SizeInCells !=
            profile.SizeInCells)
        {
            failureReason =
                profile.FamilyId +
                " Size 不符合 Profile。" +
                " Prefab=" +
                targetPlacement.Template.SizeInCells +
                " Profile=" +
                profile.SizeInCells;
            return false;
        }

        List<DreamRoomDoorSocket> usedSockets;
        string socketFailure;

        if (!P1012B2TryCollectUsedSockets(
                sourceLayout,
                targetRoomIndex,
                targetPlacement,
                out usedSockets,
                out socketFailure))
        {
            failureReason =
                profile.FamilyId +
                " Used Socket 解析失败：" +
                socketFailure;
            return false;
        }

        bool north = false;
        bool east = false;
        bool south = false;
        bool west = false;

        for (int i = 0;
             i < usedSockets.Count;
             i++)
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
            profile.BuildDefaultDoorSet(
                north,
                east,
                south,
                west);

        string contractFailure;

        if (!P1012B2ValidateSocketContract(
                profile,
                usedSockets,
                kernelDoors,
                out contractFailure))
        {
            failureReason =
                contractFailure;
            return false;
        }

        int socketMask =
            (north ? 1 : 0) |
            (east ? 2 : 0) |
            (south ? 4 : 0) |
            (west ? 8 : 0);

        int proceduralSeed =
            DreamProceduralRoomFamilyKernelP1012B1
                .DeriveRoomSeed(
                    sourceLayout.Seed,
                    targetRoomIndex,
                    profile,
                    socketMask);

        DreamProceduralRoomLayout proceduralLayout;
        string generationFailure;

        if (!DreamProceduralRoomFamilyKernelP1012B1
                .TryGenerate(
                    profile,
                    proceduralSeed,
                    kernelDoors,
                    out proceduralLayout,
                    out generationFailure))
        {
            failureReason =
                profile.FamilyId +
                " Kernel 生成失败：" +
                generationFailure;
            return false;
        }

        string validationFailure;

        if (!DreamProceduralRoomFamilyKernelP1012B1
                .Validate(
                    profile,
                    proceduralLayout,
                    out validationFailure))
        {
            failureReason =
                profile.FamilyId +
                " Kernel 校验失败：" +
                validationFailure;
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

            if (!targetPlacement.CellBounds.Contains(
                    globalCell))
            {
                failureReason =
                    profile.FamilyId +
                    " Blocked 越出 Placement：" +
                    globalCell + "。";
                return false;
            }

            if (!sourceLayout.FloorCells.Contains(
                    globalCell))
            {
                failureReason =
                    profile.FamilyId +
                    " Blocked 原本不属于 FloorCells：" +
                    globalCell + "。";
                return false;
            }

            if (sourceLayout.CorridorCells.Contains(
                    globalCell))
            {
                failureReason =
                    profile.FamilyId +
                    " Blocked 与 Corridor 重叠：" +
                    globalCell + "。";
                return false;
            }

            if (globalCell == sourceLayout.StartCell ||
                globalCell == sourceLayout.ExitCell)
            {
                failureReason =
                    profile.FamilyId +
                    " Blocked 命中 R5 Start/Exit：" +
                    globalCell + "。";
                return false;
            }

            if (!globalBlocked.Add(
                    globalCell))
            {
                failureReason =
                    profile.FamilyId +
                    " Local→Global Blocked 映射发生重复：" +
                    globalCell + "。";
                return false;
            }
        }

        List<Vector2Int> usedDoorCells =
            new List<Vector2Int>();

        for (int i = 0;
             i < usedSockets.Count;
             i++)
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
                    failureReason =
                        profile.FamilyId +
                        " Blocked 命中 Used Socket：" +
                        usedDoorCells[c] + "。";
                    return false;
                }
            }
        }

        plan =
            new P1012B2Plan(
                targetRoomIndex,
                targetPlacement,
                profile,
                proceduralSeed,
                socketMask,
                usedSockets.Count,
                proceduralLayout,
                globalBlocked);

        return true;
    }

    private static bool P1012B2TryCollectUsedSockets(
        DungeonLayout layout,
        int roomIndex,
        DreamRoomPlacement placement,
        out List<DreamRoomDoorSocket> sockets,
        out string failureReason)
    {
        sockets =
            new List<DreamRoomDoorSocket>();

        failureReason =
            string.Empty;

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

    private static bool P1012B2ValidateSocketContract(
        DreamProceduralRoomFamilyProfileP1012B1 profile,
        List<DreamRoomDoorSocket> actualSockets,
        IReadOnlyList<DreamProceduralDoorLane> expectedDoors,
        out string failureReason)
    {
        failureReason =
            string.Empty;

        for (int i = 0;
             i < actualSockets.Count;
             i++)
        {
            DreamRoomDoorSocket actualSocket =
                actualSockets[i];

            DreamProceduralDoorLane expected =
                null;

            for (int d = 0;
                 d < expectedDoors.Count;
                 d++)
            {
                if (expectedDoors[d].Direction ==
                    actualSocket.Direction)
                {
                    expected =
                        expectedDoors[d];
                    break;
                }
            }

            if (expected == null)
            {
                failureReason =
                    profile.FamilyId +
                    " Kernel 缺少方向 " +
                    actualSocket.Direction + "。";
                return false;
            }

            HashSet<Vector2Int> actualCells =
                new HashSet<Vector2Int>(
                    actualSocket
                        .GetLocalInsideCells());

            HashSet<Vector2Int> expectedCells =
                new HashSet<Vector2Int>(
                    expected
                        .LocalInsideCells);

            if (!actualCells.SetEquals(
                    expectedCells))
            {
                failureReason =
                    profile.FamilyId +
                    " Socket " +
                    actualSocket.SocketId +
                    " 的 LocalInsideCells 与 Family Profile 契约不一致。" +
                    " Actual=" +
                    P1012B2FormatCells(
                        actualCells) +
                    " Expected=" +
                    P1012B2FormatCells(
                        expectedCells);
                return false;
            }
        }

        return true;
    }

    private static bool P1012B2AllFloorCellsConnected(
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
            Vector2Int current =
                queue.Dequeue();

            for (int i = 0;
                 i < directions.Length;
                 i++)
            {
                Vector2Int next =
                    current +
                    directions[i];

                if (floorCells.Contains(next) &&
                    visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return
            visited.Count ==
            floorCells.Count;
    }

    private static void P1012B2Rollback(
        List<DreamRoomPlacement> placements)
    {
        if (placements == null)
        {
            return;
        }

        for (int i = 0;
             i < placements.Count;
             i++)
        {
            if (placements[i] != null)
            {
                placements[i]
                    .ClearRuntimeProceduralOverride();
            }
        }
    }

    private static string P1012B2BuildSuccessReport(
        DungeonLayout sourceLayout,
        DungeonLayout candidateLayout,
        List<P1012B2Plan> plans,
        int removedCells)
    {
        List<string> familyLines =
            new List<string>();

        for (int i = 0;
             i < plans.Count;
             i++)
        {
            P1012B2Plan plan =
                plans[i];

            familyLines.Add(
                plan.Profile.FamilyId +
                " RoomIndex=" +
                plan.RoomIndex +
                " Template=" +
                plan.Placement.Template.TemplateId +
                " Seed=" +
                plan.ProceduralSeed +
                " Archetype=" +
                plan.ProceduralLayout.Archetype +
                " UsedSockets=" +
                plan.UsedSocketCount +
                " SocketMask=" +
                plan.SocketMask +
                " Blocked=" +
                plan.GlobalBlockedCells.Count);
        }

        return
            "[DungeonGenerator/P10.12B-2] Controlled Family Authority Commit PASS" +
            "\nProceduralFamilies=" +
            plans.Count +
            " | " +
            string.Join(
                "\n | ",
                familyLines) +
            "\nFloorCells=" +
            sourceLayout.FloorCells.Count +
            "->" +
            candidateLayout.FloorCells.Count +
            " | Removed=" +
            removedCells +
            " | RoomCells=" +
            sourceLayout.RoomCells.Count +
            "->" +
            candidateLayout.RoomCells.Count +
            " | CorridorCells=" +
            candidateLayout.CorridorCells.Count +
            "\nAuthority=DreamRoomPlacement.RuntimeProceduralOverride" +
            " | Kernel=GenericFamilyP1012B1" +
            " | StartExitPreserved=True" +
            " | ProductionMainChanged=False";
    }

    private static string P1012B2BuildFailureReport(
        string stage,
        int roomIndex,
        string reason)
    {
        return
            "[DungeonGenerator/P10.12B-2] Controlled Family Authority Commit FAILED" +
            "\nStage=" +
            stage +
            " | RoomIndex=" +
            roomIndex +
            "\n" +
            reason +
            "\nPartialCommit=False" +
            " | ProductionMainChanged=False";
    }

    private static string P1012B2FormatCells(
        IEnumerable<Vector2Int> cells)
    {
        List<Vector2Int> ordered =
            new List<Vector2Int>(
                cells);

        ordered.Sort(
            delegate(
                Vector2Int a,
                Vector2Int b)
            {
                int x =
                    a.x.CompareTo(
                        b.x);

                return
                    x != 0
                        ? x
                        : a.y.CompareTo(
                            b.y);
            });

        return
            "[" +
            string.Join(
                ",",
                ordered) +
            "]";
    }

    private sealed class P1012B2Plan
    {
        public int RoomIndex { get; }
        public DreamRoomPlacement Placement { get; }

        public DreamProceduralRoomFamilyProfileP1012B1
            Profile { get; }

        public int ProceduralSeed { get; }
        public int SocketMask { get; }
        public int UsedSocketCount { get; }

        public DreamProceduralRoomLayout
            ProceduralLayout { get; }

        public HashSet<Vector2Int>
            GlobalBlockedCells { get; }

        public P1012B2Plan(
            int roomIndex,
            DreamRoomPlacement placement,
            DreamProceduralRoomFamilyProfileP1012B1 profile,
            int proceduralSeed,
            int socketMask,
            int usedSocketCount,
            DreamProceduralRoomLayout proceduralLayout,
            HashSet<Vector2Int> globalBlockedCells)
        {
            RoomIndex =
                roomIndex;

            Placement =
                placement;

            Profile =
                profile;

            ProceduralSeed =
                proceduralSeed;

            SocketMask =
                socketMask;

            UsedSocketCount =
                usedSocketCount;

            ProceduralLayout =
                proceduralLayout;

            GlobalBlockedCells =
                globalBlockedCells;
        }
    }
}
