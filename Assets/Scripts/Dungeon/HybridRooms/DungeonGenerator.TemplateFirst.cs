using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// DungeonGenerator 的 R4 Template First 扩展。
///
/// 重要阶段边界：
/// 1. 这里只选择、旋转和放置 RoomTemplate，不建立连接图或走廊。
/// 2. 不实例化 Prefab，不调用 DungeonRenderer，也不修改当前 Scene。
/// 3. 原有 Generate(int floorNumber) 保持原样，旧地图运行路径不受影响。
/// 4. 只有调用 TryGenerateTemplateFirstLayout 才会执行本文件的算法。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("R4 Template First（尚未接入正式运行）")]
    [Tooltip(
        "拖入 R3 生成的 RoomCatalog_Graybox。" +
        "该引用只供 R4 放置诊断使用，旧 Generate() 不读取它。")]
    [SerializeField]
    private DreamRoomCatalog templateFirstRoomCatalog;

    [Header("R4 地图与房间数量")]
    [Min(1)]
    [SerializeField]
    private int templateFirstMapWidth = 80;

    [Min(1)]
    [SerializeField]
    private int templateFirstMapHeight = 50;

    [Min(2)]
    [SerializeField]
    private int templateFirstDesiredRoomCount = 7;

    [Tooltip(
        "房间与地图外边缘至少保留的格子数。" +
        "它与 Room Padding 是两个不同的约束。")]
    [Min(0)]
    [SerializeField]
    private int templateFirstMapBorder = 2;

    [Header("R4 放置重试")]
    [Tooltip("两个房间矩形边界之间至少保留的空白格数量。")]
    [Min(0)]
    [SerializeField]
    private int templateFirstRoomPadding = 2;

    [Tooltip(
        "放置一个房间时，最多尝试多少组 Template、旋转和坐标。")]
    [Min(1)]
    [SerializeField]
    private int templateFirstMaximumPlacementAttemptsPerRoom = 100;

    [Tooltip(
        "整层未放满目标数量时，从空布局重新开始的最大次数。" +
        "失败时不会返回只有一半房间的布局。")]
    [Min(1)]
    [SerializeField]
    private int templateFirstMaximumFloorAttempts = 20;

    [Tooltip(
        "开启后，仍只旋转 Allow Quarter Turns 已勾选的 Template。")]
    [SerializeField]
    private bool templateFirstUseAllowedQuarterTurns = true;

    public DreamRoomCatalog TemplateFirstRoomCatalog =>
        templateFirstRoomCatalog;

    public int TemplateFirstMapWidth =>
        templateFirstMapWidth;

    public int TemplateFirstMapHeight =>
        templateFirstMapHeight;

    public int TemplateFirstDesiredRoomCount =>
        templateFirstDesiredRoomCount;

    public int TemplateFirstMapBorder =>
        templateFirstMapBorder;

    public int TemplateFirstRoomPadding =>
        templateFirstRoomPadding;

    public int TemplateFirstMaximumPlacementAttemptsPerRoom =>
        templateFirstMaximumPlacementAttemptsPerRoom;

    public int TemplateFirstMaximumFloorAttempts =>
        templateFirstMaximumFloorAttempts;

    public bool TemplateFirstUseAllowedQuarterTurns =>
        templateFirstUseAllowedQuarterTurns;

    /// <summary>
    /// 使用指定种子生成“只有房间摆放、还没有连接和走廊”的 R4 布局。
    ///
    /// 成功时：
    /// - layout 包含完整数量的 RoomPlacements；
    /// - RoomCells/FloorCells 来自 Template 的真实 Walkable Cells；
    /// - CorridorCells 与 Connections 为空。
    ///
    /// 失败时：
    /// - layout 一定为 null；
    /// - report 会说明配置错误或每次整层重试失败的位置。
    /// </summary>
    public bool TryGenerateTemplateFirstLayout(
        int floorNumber,
        int seed,
        out DungeonLayout layout,
        out string report)
    {
        layout = null;

        List<string> configurationErrors =
            R4GetConfigurationErrors(floorNumber);

        if (configurationErrors.Count > 0)
        {
            report = R4BuildConfigurationErrorReport(
                floorNumber,
                seed,
                configurationErrors);

            return false;
        }

        List<string> floorFailureReasons =
            new List<string>();

        int totalCandidateAttempts = 0;

        for (int floorAttemptIndex = 0;
             floorAttemptIndex <
             templateFirstMaximumFloorAttempts;
             floorAttemptIndex++)
        {
            int attemptSeed = R4CreateAttemptSeed(
                seed,
                floorAttemptIndex);

            List<DreamRoomPlacement> placements;
            int candidateAttempts;
            string failureReason;

            bool placedAllRooms =
                R4TryBuildPlacementSet(
                    floorNumber,
                    attemptSeed,
                    out placements,
                    out candidateAttempts,
                    out failureReason);

            totalCandidateAttempts += candidateAttempts;

            if (!placedAllRooms)
            {
                floorFailureReasons.Add(
                    "整层尝试 " +
                    (floorAttemptIndex + 1) +
                    "（派生 Seed " +
                    attemptSeed +
                    "）：" +
                    failureReason);

                continue;
            }

            string layoutFailureReason;

            if (!R4TryCreatePlacementOnlyLayout(
                    placements,
                    seed,
                    out layout,
                    out layoutFailureReason))
            {
                report =
                    "[DungeonGenerator/R4] Template First 数据建立失败\n" +
                    layoutFailureReason +
                    "\n没有返回部分布局。";

                layout = null;
                return false;
            }

            List<string> layoutErrors =
                layout.GetValidationErrors();

            R942AppendRareQuotaValidationErrors(
                layout,
                layoutErrors);

            R943AppendCoreItemValidationErrors(
                layout,
                floorNumber,
                layoutErrors);

            R944AppendSpecialValidationErrors(
                layout,
                floorNumber,
                layoutErrors);

            if (layoutErrors.Count > 0)
            {
                report = R4BuildLayoutErrorReport(
                    layoutErrors);

                layout = null;
                return false;
            }

            report = R4BuildSuccessReport(
                layout,
                floorNumber,
                seed,
                floorAttemptIndex + 1,
                attemptSeed,
                totalCandidateAttempts);

            return true;
        }

        report = R4BuildPlacementFailureReport(
            floorNumber,
            seed,
            totalCandidateAttempts,
            floorFailureReasons);

        return false;
    }

    private List<string> R4GetConfigurationErrors(
        int floorNumber)
    {
        List<string> errors = new List<string>();

        if (floorNumber < 1)
        {
            errors.Add("Floor Number 必须至少为 1。");
        }

        if (templateFirstRoomCatalog == null)
        {
            errors.Add(
                "Template First Room Catalog 不能为空。" +
                "请拖入 R3 的 RoomCatalog_Graybox.asset。");
        }
        else
        {
            List<string> catalogErrors =
                templateFirstRoomCatalog.GetValidationErrors();

            for (int i = 0; i < catalogErrors.Count; i++)
            {
                errors.Add(
                    "Room Catalog：" +
                    catalogErrors[i]);
            }
        }

        if (templateFirstMapWidth < 1 ||
            templateFirstMapHeight < 1)
        {
            errors.Add("R4 Map Width 和 Map Height 必须为正数。");
        }

        if (templateFirstDesiredRoomCount < 2)
        {
            errors.Add("R4 Desired Room Count 必须至少为 2。");
        }

        if (templateFirstMapBorder < 0)
        {
            errors.Add("R4 Map Border 不能小于 0。");
        }

        if (templateFirstRoomPadding < 0)
        {
            errors.Add("R4 Room Padding 不能小于 0。");
        }

        if (templateFirstMaximumPlacementAttemptsPerRoom < 1)
        {
            errors.Add(
                "R4 Maximum Placement Attempts Per Room " +
                "必须至少为 1。");
        }

        if (templateFirstMaximumFloorAttempts < 1)
        {
            errors.Add(
                "R4 Maximum Floor Attempts 必须至少为 1。");
        }

        int usableWidth =
            templateFirstMapWidth -
            templateFirstMapBorder * 2;

        int usableHeight =
            templateFirstMapHeight -
            templateFirstMapBorder * 2;

        if (usableWidth < 1 || usableHeight < 1)
        {
            errors.Add(
                "R4 Map Border 过大，地图内部没有可放置区域。" +
                "当前可用尺寸为 " +
                usableWidth + "x" + usableHeight + "。");
        }

        if (errors.Count > 0 ||
            templateFirstRoomCatalog == null)
        {
            return errors;
        }

        R943AppendConfigurationErrors(
            floorNumber,
            errors);

        R944AppendConfigurationErrors(
            floorNumber,
            errors);

        R10132AppendConfigurationErrors(
            errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        List<DreamRoomTemplate> eligibleTemplates =
            new List<DreamRoomTemplate>();

        templateFirstRoomCatalog.GetEligibleTemplates(
            floorNumber,
            null,
            eligibleTemplates);

        if (eligibleTemplates.Count == 0)
        {
            errors.Add(
                "当前楼层没有符合 Minimum/Maximum Floor 的房间模板。");
            return errors;
        }

        long finiteCapacity = 0;
        bool hasUnlimitedTemplate = false;
        int fittingTemplateCount = 0;

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            DreamRoomTemplate template =
                eligibleTemplates[i];

            if (!R4CanTemplateFitMap(template))
            {
                continue;
            }

            fittingTemplateCount++;

            template.GetWalkableCells(walkableCells);

            if (walkableCells.Count == 0)
            {
                errors.Add(
                    "模板 '" + template.TemplateId +
                    "' 没有任何 Walkable Cell，不能用于 R4 放置。");
            }

            if (template.MaximumInstancesPerFloor == 0)
            {
                hasUnlimitedTemplate = true;
            }
            else
            {
                finiteCapacity +=
                    template.MaximumInstancesPerFloor;
            }
        }

        if (fittingTemplateCount == 0)
        {
            errors.Add(
                "当前楼层的候选模板在任何允许旋转下都放不进地图内部。" +
                "请增大 R4 地图或减小 Map Border。");
        }
        else if (!hasUnlimitedTemplate &&
                 finiteCapacity <
                 templateFirstDesiredRoomCount)
        {
            errors.Add(
                "所有可放入地图的模板，其单层最大实例数合计只有 " +
                finiteCapacity +
                "，小于 Desired Room Count " +
                templateFirstDesiredRoomCount + "。");
        }

        return errors;
    }

    private bool R4TryBuildPlacementSet(
        int floorNumber,
        int attemptSeed,
        out List<DreamRoomPlacement> placements,
        out int candidateAttempts,
        out string failureReason)
    {
        placements = new List<DreamRoomPlacement>(
            templateFirstDesiredRoomCount);

        candidateAttempts = 0;
        failureReason = string.Empty;

        Dictionary<string, int> instanceCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        List<DreamRoomTemplate> fittingTemplates =
            new List<DreamRoomTemplate>();

        List<int> fittingQuarterTurns =
            new List<int>(4);

        System.Random r4Random =
            new System.Random(attemptSeed);

        for (int roomIndex = 0;
             roomIndex < templateFirstDesiredRoomCount;
             roomIndex++)
        {
            bool placedRoom = false;

            R941RequiredRoomRole requiredRole =
                R941GetRequiredRoleForPlacementSlot(
                    roomIndex);

            for (int attempt = 0;
                 attempt <
                 templateFirstMaximumPlacementAttemptsPerRoom;
                 attempt++)
            {
                candidateAttempts++;

                R4CollectFittingEligibleTemplates(
                    floorNumber,
                    instanceCounts,
                    fittingTemplates);

                R944ApplyCommittedSpecialCap(
                    fittingTemplates,
                    placements);

                if (fittingTemplates.Count == 0)
                {
                    failureReason =
                        "准备第 " +
                        (roomIndex + 1) +
                        " 个房间时已经没有可用模板。" +
                        "请检查楼层限制、Maximum Instances、地图尺寸" +
                        "与 Special 全局上限。";

                    return false;
                }

                string roleSelectionFailure;

                if (!R941RestrictCandidatesForRole(
                        requiredRole,
                        fittingTemplates,
                        out roleSelectionFailure))
                {
                    failureReason =
                        "第 " +
                        (roomIndex + 1) +
                        " 个房间的 R9.4.1 角色候选无效：" +
                        roleSelectionFailure;

                    return false;
                }

                bool useFidelityOrdinaryMix = false;

                if (requiredRole ==
                        R941RequiredRoomRole.None)
                {
                    bool useCoreItemReservationSlot =
                        R943ShouldUseCoreItemReservationSlot(
                            floorNumber,
                            roomIndex,
                            placements);

                    bool useSpecialReservationSlot =
                        !useCoreItemReservationSlot &&
                        R944ShouldUseSpecialReservationSlot(
                            floorNumber,
                            roomIndex,
                            placements);

                    string slotSelectionFailure;

                    bool validSlotCandidates;

                    if (useCoreItemReservationSlot)
                    {
                        validSlotCandidates =
                            R943RestrictCandidatesForCoreItemSlot(
                                fittingTemplates,
                                out slotSelectionFailure);
                    }
                    else if (useSpecialReservationSlot)
                    {
                        validSlotCandidates =
                            R944RestrictCandidatesForSpecialSlot(
                                fittingTemplates,
                                out slotSelectionFailure);
                    }
                    else
                    {
                        validSlotCandidates =
                            R942RestrictCandidatesForOrdinarySlot(
                                fittingTemplates,
                                out slotSelectionFailure);

                        useFidelityOrdinaryMix =
                            validSlotCandidates;
                    }

                    if (!validSlotCandidates)
                    {
                        failureReason =
                            "第 " +
                            (roomIndex + 1) +
                            " 个房间的 " +
                            (useCoreItemReservationSlot
                                ? "R9.4.3 Core Item"
                                : useSpecialReservationSlot
                                    ? "R9.4.4 Special"
                                    : "R9.4.2 普通") +
                            "候选无效：" +
                            slotSelectionFailure;

                        return false;
                    }
                }

                // P10.13-2：
                // RoomTag 角色筛选已经完成后，才允许 Fidelity 介入。
                // 所有槽位先做 Medium Family 去重复偏好；
                // 只有真正的 R9.4.2 普通槽位才执行 High/Medium 软配额。
                R10132ApplyProceduralMediumDiversityPreference(
                    fittingTemplates,
                    placements);

                if (useFidelityOrdinaryMix)
                {
                    string fidelityFailure;

                    if (!R10132RestrictOrdinaryCandidatesByFidelity(
                            roomIndex,
                            attempt,
                            placements,
                            fittingTemplates,
                            out fidelityFailure))
                    {
                        failureReason =
                            "第 " +
                            (roomIndex + 1) +
                            " 个房间的 P10.13-2 Fidelity Mix 候选无效：" +
                            fidelityFailure;

                        return false;
                    }
                }

                DreamRoomTemplate selectedTemplate =
                    R4ChooseWeightedTemplate(
                        fittingTemplates,
                        r4Random);

                if (selectedTemplate == null)
                {
                    failureReason =
                        "候选模板的有效 Random Weight 合计为 0。";

                    return false;
                }

                R4GetFittingQuarterTurns(
                    selectedTemplate,
                    fittingQuarterTurns);

                if (fittingQuarterTurns.Count == 0)
                {
                    continue;
                }

                int quarterTurns =
                    fittingQuarterTurns[
                        r4Random.Next(
                            0,
                            fittingQuarterTurns.Count)];

                Vector2Int rotatedSize =
                    selectedTemplate.GetRotatedSize(
                        quarterTurns);

                int minimumX = templateFirstMapBorder;
                int minimumY = templateFirstMapBorder;

                int maximumX =
                    templateFirstMapWidth -
                    templateFirstMapBorder -
                    rotatedSize.x;

                int maximumY =
                    templateFirstMapHeight -
                    templateFirstMapBorder -
                    rotatedSize.y;

                int x = r4Random.Next(
                    minimumX,
                    maximumX + 1);

                int y = r4Random.Next(
                    minimumY,
                    maximumY + 1);

                DreamRoomPlacement candidate =
                    new DreamRoomPlacement(
                        selectedTemplate,
                        new Vector2Int(x, y),
                        quarterTurns);

                if (R4OverlapsExistingPlacement(
                        candidate,
                        placements))
                {
                    continue;
                }

                placements.Add(candidate);
                R4IncrementInstanceCount(
                    selectedTemplate.TemplateId,
                    instanceCounts);

                placedRoom = true;
                break;
            }

            if (!placedRoom)
            {
                failureReason =
                    "第 " +
                    (roomIndex + 1) +
                    " 个房间在 " +
                    templateFirstMaximumPlacementAttemptsPerRoom +
                    " 次 Template／旋转／坐标尝试后仍无法放置。" +
                    "本次整层结果已丢弃。";

                return false;
            }
        }

        return true;
    }

    private void R4CollectFittingEligibleTemplates(
        int floorNumber,
        IReadOnlyDictionary<string, int> instanceCounts,
        List<DreamRoomTemplate> results)
    {
        templateFirstRoomCatalog.GetEligibleTemplates(
            floorNumber,
            instanceCounts,
            results);

        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (!R4CanTemplateFitMap(results[i]))
            {
                results.RemoveAt(i);
            }
        }
    }

    private bool R4CanTemplateFitMap(
        DreamRoomTemplate template)
    {
        if (template == null)
        {
            return false;
        }

        List<int> quarterTurns = new List<int>(4);
        R4GetFittingQuarterTurns(template, quarterTurns);
        return quarterTurns.Count > 0;
    }

    private void R4GetFittingQuarterTurns(
        DreamRoomTemplate template,
        List<int> results)
    {
        results.Clear();

        if (template == null)
        {
            return;
        }

        int rotationCount =
            templateFirstUseAllowedQuarterTurns &&
            template.AllowQuarterTurns
                ? 4
                : 1;

        int usableWidth =
            templateFirstMapWidth -
            templateFirstMapBorder * 2;

        int usableHeight =
            templateFirstMapHeight -
            templateFirstMapBorder * 2;

        for (int quarterTurns = 0;
             quarterTurns < rotationCount;
             quarterTurns++)
        {
            Vector2Int size =
                template.GetRotatedSize(quarterTurns);

            if (size.x > 0 &&
                size.y > 0 &&
                size.x <= usableWidth &&
                size.y <= usableHeight)
            {
                results.Add(quarterTurns);
            }
        }
    }

    private static DreamRoomTemplate R4ChooseWeightedTemplate(
        List<DreamRoomTemplate> templates,
        System.Random randomSource)
    {
        long totalWeight = 0;

        for (int i = 0; i < templates.Count; i++)
        {
            DreamRoomTemplate template = templates[i];

            if (template != null)
            {
                totalWeight += Math.Max(
                    0,
                    template.RandomWeight);
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        double roll =
            randomSource.NextDouble() * totalWeight;

        long cumulativeWeight = 0;
        DreamRoomTemplate lastValidTemplate = null;

        for (int i = 0; i < templates.Count; i++)
        {
            DreamRoomTemplate template = templates[i];

            if (template == null ||
                template.RandomWeight <= 0)
            {
                continue;
            }

            lastValidTemplate = template;
            cumulativeWeight += template.RandomWeight;

            if (roll < cumulativeWeight)
            {
                return template;
            }
        }

        return lastValidTemplate;
    }

    private bool R4OverlapsExistingPlacement(
        DreamRoomPlacement candidate,
        List<DreamRoomPlacement> existingPlacements)
    {
        for (int i = 0; i < existingPlacements.Count; i++)
        {
            if (candidate.OverlapsWithPadding(
                    existingPlacements[i],
                    templateFirstRoomPadding))
            {
                return true;
            }
        }

        return false;
    }

    private static void R4IncrementInstanceCount(
        string templateId,
        Dictionary<string, int> instanceCounts)
    {
        int currentCount;

        instanceCounts.TryGetValue(
            templateId,
            out currentCount);

        instanceCounts[templateId] =
            currentCount + 1;
    }

    private static bool R4TryCreatePlacementOnlyLayout(
        List<DreamRoomPlacement> placements,
        int seed,
        out DungeonLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = string.Empty;

        if (placements == null || placements.Count == 0)
        {
            failureReason = "没有可用于建立布局的 RoomPlacement。";
            return false;
        }

        List<Vector2Int> representativeCells =
            new List<Vector2Int>(placements.Count);

        for (int i = 0; i < placements.Count; i++)
        {
            Vector2Int representativeCell;

            if (!R4TryGetRepresentativeWalkableCell(
                    placements[i],
                    out representativeCell))
            {
                failureReason =
                    "RoomPlacement " + i +
                    "（" + placements[i] +
                    "）没有 Walkable Cell。";

                return false;
            }

            representativeCells.Add(
                representativeCell);
        }

        Vector2Int startCell = representativeCells[0];
        Vector2Int exitCell = startCell;
        int farthestDistance = -1;

        for (int i = 0;
             i < representativeCells.Count;
             i++)
        {
            Vector2Int cell = representativeCells[i];

            int distance =
                Mathf.Abs(cell.x - startCell.x) +
                Mathf.Abs(cell.y - startCell.y);

            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                exitCell = cell;
            }
        }

        layout = DungeonLayout.CreateHybrid(
            placements,
            new Vector2Int[0],
            new DreamRoomConnection[0],
            startCell,
            exitCell,
            seed);

        return true;
    }

    private static bool R4TryGetRepresentativeWalkableCell(
        DreamRoomPlacement placement,
        out Vector2Int representativeCell)
    {
        representativeCell = Vector2Int.zero;

        if (placement == null)
        {
            return false;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        placement.GetWalkableGlobalCells(
            walkableCells);

        if (walkableCells.Count == 0)
        {
            return false;
        }

        RectInt bounds = placement.CellBounds;

        int doubledCenterX =
            bounds.xMin + bounds.xMax - 1;

        int doubledCenterY =
            bounds.yMin + bounds.yMax - 1;

        long bestDoubledDistance = long.MaxValue;

        for (int i = 0; i < walkableCells.Count; i++)
        {
            Vector2Int cell = walkableCells[i];

            long doubledDistance =
                Math.Abs(
                    (long)cell.x * 2 -
                    doubledCenterX) +
                Math.Abs(
                    (long)cell.y * 2 -
                    doubledCenterY);

            if (doubledDistance < bestDoubledDistance ||
                (doubledDistance == bestDoubledDistance &&
                 R4ComesBefore(cell, representativeCell)))
            {
                bestDoubledDistance = doubledDistance;
                representativeCell = cell;
            }
        }

        return true;
    }

    private static bool R4ComesBefore(
        Vector2Int first,
        Vector2Int second)
    {
        return first.x < second.x ||
               (first.x == second.x &&
                first.y < second.y);
    }

    private static int R4CreateAttemptSeed(
        int baseSeed,
        int floorAttemptIndex)
    {
        if (floorAttemptIndex <= 0)
        {
            return baseSeed;
        }

        unchecked
        {
            uint value =
                (uint)baseSeed +
                0x9E3779B9u *
                (uint)floorAttemptIndex;

            value ^= value >> 16;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;

            return (int)value;
        }
    }

    private string R4BuildSuccessReport(
        DungeonLayout layout,
        int floorNumber,
        int baseSeed,
        int successfulFloorAttempt,
        int successfulAttemptSeed,
        int totalCandidateAttempts)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R4] Template First 放置成功");

        builder.AppendLine(
            "Catalog：" +
            templateFirstRoomCatalog.CatalogId +
            " | Floor " + floorNumber +
            " | Seed " + baseSeed);

        builder.AppendLine(
            "Map：" +
            templateFirstMapWidth + "x" +
            templateFirstMapHeight +
            " | Border " + templateFirstMapBorder +
            " | Padding " + templateFirstRoomPadding);

        builder.AppendLine(
            "Rooms：" +
            layout.RoomPlacements.Count + "/" +
            templateFirstDesiredRoomCount +
            " | 成功于整层尝试 " +
            successfulFloorAttempt +
            " | 派生 Seed " +
            successfulAttemptSeed +
            " | 候选尝试合计 " +
            totalCandidateAttempts);

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            builder.AppendLine(
                "  [" + i + "] " +
                layout.RoomPlacements[i]);
        }

        builder.AppendLine(
            "阶段边界：CorridorCells 0 | Connections 0 | " +
            "未实例化 Prefab。连接图将在阶段5处理。");

        return builder.ToString();
    }

    private string R4BuildConfigurationErrorReport(
        int floorNumber,
        int seed,
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R4] Template First 配置无效");

        builder.AppendLine(
            "Floor " + floorNumber +
            " | Seed " + seed);

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(
                "- " + errors[i]);
        }

        builder.AppendLine("没有返回部分布局。");
        return builder.ToString();
    }

    private static string R4BuildLayoutErrorReport(
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R4] 生成结果未通过 DungeonLayout 校验");

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(
                "- " + errors[i]);
        }

        builder.AppendLine("没有返回部分布局。");
        return builder.ToString();
    }

    private string R4BuildPlacementFailureReport(
        int floorNumber,
        int seed,
        int totalCandidateAttempts,
        List<string> floorFailureReasons)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R4] Template First 放置失败");

        builder.AppendLine(
            "Catalog：" +
            templateFirstRoomCatalog.CatalogId +
            " | Floor " + floorNumber +
            " | Seed " + seed);

        builder.AppendLine(
            "目标房间：" +
            templateFirstDesiredRoomCount +
            " | Map " +
            templateFirstMapWidth + "x" +
            templateFirstMapHeight +
            " | Border " + templateFirstMapBorder +
            " | Padding " + templateFirstRoomPadding);

        builder.AppendLine(
            "整层尝试：" +
            templateFirstMaximumFloorAttempts +
            " | 候选尝试合计：" +
            totalCandidateAttempts);

        for (int i = 0;
             i < floorFailureReasons.Count;
             i++)
        {
            builder.AppendLine(
                "- " + floorFailureReasons[i]);
        }

        builder.AppendLine(
            "结果：没有返回房间数量不足的部分布局。" +
            "请增大地图、减小 Padding／房间数，或增加重试次数。");

        return builder.ToString();
    }
}
