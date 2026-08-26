using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// R9.4.4：让 Special 成为每层全局唯一的保留角色。
///
/// 规则边界：
/// 1. 有合格 Special 时保证最终布局恰好放置一个；
/// 2. 全部 Special 模板共用每层一个的全局上限；
/// 3. RandomWeight、楼层范围与每 Template 上限继续复用既有字段；
/// 4. CoreItemCandidate + Special 可由同一个房间同时满足两个角色；
/// 5. 计数只读取已成功落位的房间，失败尝试不消耗名额；
/// 6. 不读取进度 Manager，进度条件仍留给 R9.6。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const int R944MaximumSpecialRoomsPerFloor = 1;

    /// <summary>
    /// 在任何角色筛选前应用全局 Special 上限。若已经有一个 Special
    /// 成功落位，后续 Start／Exit／Core／普通槽位都不能再选 Special。
    /// </summary>
    private static void R944ApplyCommittedSpecialCap(
        List<DreamRoomTemplate> candidates,
        IReadOnlyList<DreamRoomPlacement> committedPlacements)
    {
        if (candidates == null ||
            R944CountPlacedSpecialRooms(committedPlacements) <
                R944MaximumSpecialRoomsPerFloor)
        {
            return;
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            DreamRoomTemplate template = candidates[i];

            if (template != null &&
                template.HasTag(DreamRoomTag.Special))
            {
                candidates.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Core 槽位优先处理。若本层仍没有 Special，当前第一个非 Core 的
    /// 非角色槽位就成为 Special 保留槽，因此 Core + Special 选中后不会
    /// 再额外建立第二个 Special 房。
    /// </summary>
    private bool R944ShouldUseSpecialReservationSlot(
        int floorNumber,
        int roomIndex,
        IReadOnlyList<DreamRoomPlacement> committedPlacements)
    {
        if (roomIndex < 2 ||
            R944CountPlacedSpecialRooms(committedPlacements) >=
                R944MaximumSpecialRoomsPerFloor)
        {
            return false;
        }

        return R944CountCatalogSpecialCandidates(
                   floorNumber,
                   requireMapFit: true) > 0;
    }

    private static bool R944RestrictCandidatesForSpecialSlot(
        List<DreamRoomTemplate> candidates,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (candidates == null || candidates.Count == 0)
        {
            failureReason =
                "楼层、Maximum Instances、地图尺寸或 Special 全局上限" +
                "筛选后没有候选。";
            return false;
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (!R944IsEnabledSpecialCandidate(candidates[i]))
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count > 0)
        {
            return true;
        }

        failureReason =
            "当前 Special 保留槽没有可用候选；" +
            "Special 不可同时承担 StartCandidate／ExitCandidate。";

        return false;
    }

    private void R944AppendConfigurationErrors(
        int floorNumber,
        List<string> errors)
    {
        if (errors == null ||
            templateFirstRoomCatalog == null ||
            templateFirstRoomCatalog.RoomTemplates == null)
        {
            return;
        }

        int floorEligibleSpecialCandidates =
            R944CountCatalogSpecialCandidates(
                floorNumber,
                requireMapFit: false);

        for (int i = 0;
             i < templateFirstRoomCatalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                templateFirstRoomCatalog.RoomTemplates[i];

            if (template == null ||
                !template.HasTag(DreamRoomTag.Special) ||
                (!template.HasTag(DreamRoomTag.StartCandidate) &&
                 !template.HasTag(DreamRoomTag.ExitCandidate)))
            {
                continue;
            }

            errors.Add(
                "Special 模板 '" + template.TemplateId +
                "' 不可同时带 StartCandidate／ExitCandidate；" +
                "请把结构角色与每层唯一事件角色拆成独立模板。" );
        }

        if (floorEligibleSpecialCandidates == 0)
        {
            return;
        }

        int fittingSpecialCandidates =
            R944CountCatalogSpecialCandidates(
                floorNumber,
                requireMapFit: true);

        if (fittingSpecialCandidates == 0)
        {
            errors.Add(
                "当前楼层存在 Special，但没有任何一个能放入 R4 地图内部。" );
        }

        int usableCoreCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: true);

        int usableNonSpecialCoreCandidates =
            R944CountCatalogNonSpecialCoreCandidates(
                floorNumber,
                requireMapFit: true);

        int requiredRoomCount = 2;

        if (usableCoreCandidates > 0)
        {
            requiredRoomCount++;
        }

        if (fittingSpecialCandidates > 0 &&
            (usableCoreCandidates == 0 ||
             usableNonSpecialCoreCandidates > 0))
        {
            requiredRoomCount++;
        }

        if (templateFirstDesiredRoomCount < requiredRoomCount)
        {
            errors.Add(
                "当前楼层的 Core／Special 角色组合最多需要 " +
                requiredRoomCount +
                " 个房间槽位（含 Start／Exit），" +
                "但 Desired Room Count 为 " +
                templateFirstDesiredRoomCount + "。" );
        }
    }

    private int R944CountCatalogSpecialCandidates(
        int floorNumber,
        bool requireMapFit)
    {
        if (templateFirstRoomCatalog == null ||
            templateFirstRoomCatalog.RoomTemplates == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < templateFirstRoomCatalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                templateFirstRoomCatalog.RoomTemplates[i];

            if (!R944IsEnabledSpecialCandidate(template) ||
                !template.CanAppearOnFloor(floorNumber) ||
                (requireMapFit &&
                 !R4CanTemplateFitMap(template)))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private int R944CountCatalogNonSpecialCoreCandidates(
        int floorNumber,
        bool requireMapFit)
    {
        if (templateFirstRoomCatalog == null ||
            templateFirstRoomCatalog.RoomTemplates == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < templateFirstRoomCatalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                templateFirstRoomCatalog.RoomTemplates[i];

            if (template == null ||
                !template.HasTag(DreamRoomTag.CoreItemCandidate) ||
                template.HasTag(DreamRoomTag.Special) ||
                !template.CanAppearOnFloor(floorNumber) ||
                (requireMapFit &&
                 !R4CanTemplateFitMap(template)))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool R944IsEnabledSpecialCandidate(
        DreamRoomTemplate template)
    {
        return
            template != null &&
            template.HasTag(DreamRoomTag.Special) &&
            !template.HasTag(DreamRoomTag.StartCandidate) &&
            !template.HasTag(DreamRoomTag.ExitCandidate);
    }

    private static int R944CountPlacedSpecialRooms(
        IReadOnlyList<DreamRoomPlacement> placements)
    {
        if (placements == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < placements.Count; i++)
        {
            DreamRoomPlacement placement = placements[i];
            DreamRoomTemplate template =
                placement == null
                    ? null
                    : placement.Template;

            if (template != null &&
                template.HasTag(DreamRoomTag.Special))
            {
                count++;
            }
        }

        return count;
    }

    private void R944AppendSpecialValidationErrors(
        DungeonLayout layout,
        int floorNumber,
        List<string> errors)
    {
        if (layout == null || errors == null)
        {
            return;
        }

        int usableCatalogCandidates =
            R944CountCatalogSpecialCandidates(
                floorNumber,
                requireMapFit: true);

        int placedSpecialRooms =
            R944CountPlacedSpecialRooms(
                layout.RoomPlacements);

        if (usableCatalogCandidates > 0 &&
            placedSpecialRooms == 0)
        {
            errors.Add(
                "R9.4.4 Catalog 有可用 Special，" +
                "但最终布局没有放置 Special 房。" );
        }

        if (placedSpecialRooms >
            R944MaximumSpecialRoomsPerFloor)
        {
            errors.Add(
                "R9.4.4 最终布局放置了 " +
                placedSpecialRooms +
                " 个 Special，超过每层全局上限 " +
                R944MaximumSpecialRoomsPerFloor + "。" );
        }
    }

    private string R944BuildResolvedSpecialReport(
        DungeonLayout layout,
        int floorNumber,
        DreamRoomCatalog catalog)
    {
        List<int> roomIndices = new List<int>();

        DungeonSpecialRoomScopeR944.CollectSpecialRoomIndices(
            layout,
            roomIndices);

        StringBuilder ids = new StringBuilder();
        int coreSpecialPlacements = 0;

        for (int i = 0; i < roomIndices.Count; i++)
        {
            if (ids.Length > 0)
            {
                ids.Append(',');
            }

            int roomIndex = roomIndices[i];
            DreamRoomTemplate template =
                layout.RoomPlacements[roomIndex].Template;

            ids.Append(roomIndex);
            ids.Append(':');
            ids.Append(
                string.IsNullOrWhiteSpace(template.TemplateId)
                    ? "<empty>"
                    : template.TemplateId);

            if (template.HasTag(DreamRoomTag.CoreItemCandidate))
            {
                coreSpecialPlacements++;
            }
        }

        if (ids.Length == 0)
        {
            ids.Append("None");
        }

        int catalogCandidates =
            R944CountCatalogSpecialCandidates(
                floorNumber,
                requireMapFit: false);

        int usableCandidates =
            R944CountCatalogSpecialCandidates(
                floorNumber,
                requireMapFit: true);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.4] Special 唯一房已解析");

        builder.AppendLine(
            "Catalog=" +
            (catalog == null
                ? "<missing>"
                : catalog.CatalogId) +
            " | Floor=" + floorNumber +
            " | CatalogCandidates=" + catalogCandidates +
            " | UsableCandidates=" + usableCandidates);

        builder.AppendLine(
            "SpecialPlacements=" + roomIndices.Count +
            " | GlobalCap=" +
            R944MaximumSpecialRoomsPerFloor +
            " | CapBreaches=" +
            Math.Max(
                0,
                roomIndices.Count -
                R944MaximumSpecialRoomsPerFloor) +
            " | RoomIndices=" + ids);

        builder.Append(
            "CoreSpecialPlacements=" +
            coreSpecialPlacements +
            " | Reservation=" +
            (usableCandidates == 0
                ? "NotRequired"
                : roomIndices.Count == 0
                    ? "Missing"
                    : coreSpecialPlacements > 0
                        ? "SatisfiedSharedWithCore"
                        : "SatisfiedDedicated") +
            " | WeightPolicy=RandomWeight" +
            " | TemplateCapPolicy=MaximumInstancesPerFloor" +
            " | CountPolicy=CommittedPlacementsOnly" +
            " | OrdinaryPoolContainsSpecial=False" +
            " | ProgressConditionalSelectionDeferredTo=R9.6");

        return builder.ToString();
    }
}

/// <summary>
/// 生成器、验收工具与后续事件系统共用的只读 Special 房作用域。
/// </summary>
public static class DungeonSpecialRoomScopeR944
{
    public static void CollectSpecialRoomIndices(
        DungeonLayout layout,
        List<int> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();

        if (layout == null ||
            layout.RoomPlacements == null)
        {
            return;
        }

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

            if (template != null &&
                template.HasTag(DreamRoomTag.Special))
            {
                results.Add(i);
            }
        }
    }

    public static bool ContainsRoomIndex(
        IReadOnlyList<int> roomIndices,
        int roomIndex)
    {
        if (roomIndices == null)
        {
            return false;
        }

        for (int i = 0; i < roomIndices.Count; i++)
        {
            if (roomIndices[i] == roomIndex)
            {
                return true;
            }
        }

        return false;
    }
}
