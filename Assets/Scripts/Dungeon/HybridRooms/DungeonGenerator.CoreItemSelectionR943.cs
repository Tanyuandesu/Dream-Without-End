using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// R9.4.3：让 CoreItemCandidate 成为独立的房间保留角色，并为
/// ItemSpawner 提供同一份布局作用域真相。
///
/// 阶段边界：
/// 1. Catalog 没有可用 CoreItemCandidate 时保持旧 Graybox 行为；
/// 2. 有可用候选时，在 Start／Exit 之后保留一个 Core Item 槽位；
/// 3. 多个候选继续复用 RandomWeight、楼层限制与 Template 单层上限；
/// 4. 带 Special 的 Core 候选继续推迟到 R9.4.4；
/// 5. 本阶段只建立候选房与道具出生作用域，不让房间池读取道具进度。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const int R943CoreItemReservationRoomIndex = 2;

    private bool R943ShouldUseCoreItemReservationSlot(
        int floorNumber,
        int roomIndex,
        IReadOnlyList<DreamRoomPlacement> committedPlacements)
    {
        if (roomIndex != R943CoreItemReservationRoomIndex ||
            R943CountPlacedCoreItemCandidateRooms(
                committedPlacements) > 0)
        {
            return false;
        }

        return R943CountCatalogCoreItemCandidates(
                   floorNumber,
                   requireMapFit: true) > 0;
    }

    /// <summary>
    /// 核心道具保留槽只接受当前阶段已启用的 CoreItemCandidate。
    /// Special 即使同时带 Core 标签也不会提前进入本层。
    /// </summary>
    private static bool R943RestrictCandidatesForCoreItemSlot(
        List<DreamRoomTemplate> candidates,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (candidates == null || candidates.Count == 0)
        {
            failureReason =
                "楼层、Maximum Instances 与地图尺寸筛选后没有候选。";
            return false;
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            DreamRoomTemplate template = candidates[i];

            bool isEnabledCoreCandidate =
                template != null &&
                template.HasTag(DreamRoomTag.CoreItemCandidate) &&
                !template.HasTag(DreamRoomTag.Special);

            if (!isEnabledCoreCandidate)
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count > 0)
        {
            return true;
        }

        failureReason =
            "当前 Core Item 保留槽没有可用的 CoreItemCandidate；" +
            "带 Special 的候选要到 R9.4.4 才会启用。";

        return false;
    }

    private void R943AppendConfigurationErrors(
        int floorNumber,
        List<string> errors)
    {
        if (errors == null ||
            templateFirstRoomCatalog == null)
        {
            return;
        }

        int floorEligibleCoreCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: false);

        if (floorEligibleCoreCandidates == 0)
        {
            return;
        }

        int fittingCoreCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: true);

        if (fittingCoreCandidates == 0)
        {
            errors.Add(
                "当前楼层存在 CoreItemCandidate，" +
                "但没有任何一个能放入 R4 地图内部。" );
        }

        if (templateFirstDesiredRoomCount <=
            R943CoreItemReservationRoomIndex)
        {
            errors.Add(
                "当前楼层存在 CoreItemCandidate，" +
                "但 Desired Room Count 至少需要 3，" +
                "才能在 Start／Exit 之后建立 Core Item 保留槽。" );
        }
    }

    private int R943CountCatalogCoreItemCandidates(
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

            if (!R943IsEnabledCoreItemCandidate(template) ||
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

    private static bool R943IsEnabledCoreItemCandidate(
        DreamRoomTemplate template)
    {
        return
            template != null &&
            template.HasTag(DreamRoomTag.CoreItemCandidate) &&
            !template.HasTag(DreamRoomTag.Special);
    }

    private static int R943CountPlacedCoreItemCandidateRooms(
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

            if (placement != null &&
                R943IsEnabledCoreItemCandidate(
                    placement.Template))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 已声明且可放置的 Core 候选必须至少有一个进入最终布局。
    /// Catalog 完全没有候选时不报错，以保留 Graybox 兼容路径。
    /// </summary>
    private void R943AppendCoreItemValidationErrors(
        DungeonLayout layout,
        int floorNumber,
        List<string> errors)
    {
        if (layout == null || errors == null)
        {
            return;
        }

        int usableCatalogCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: true);

        int placedCandidates =
            R943CountPlacedCoreItemCandidateRooms(
                layout.RoomPlacements);

        if (usableCatalogCandidates > 0 &&
            placedCandidates == 0)
        {
            errors.Add(
                "R9.4.3 Catalog 有可用 CoreItemCandidate，" +
                "但最终布局没有放置任何核心道具候选房。" );
        }
    }

    /// <summary>
    /// R6 成功后输出一次候选房摘要；不读取 ItemManager，
    /// 因而不会把 R9.6 的进度条件提前塞进房间生成器。
    /// </summary>
    private string R943BuildResolvedCoreItemReport(
        DungeonLayout layout,
        int floorNumber,
        DreamRoomCatalog catalog)
    {
        List<int> roomIndices = new List<int>();

        DungeonCoreItemRoomScopeR943
            .CollectCandidateRoomIndices(
                layout,
                roomIndices);

        StringBuilder ids = new StringBuilder();

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
        }

        if (ids.Length == 0)
        {
            ids.Append("None");
        }

        int catalogCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: false);

        int usableCandidates =
            R943CountCatalogCoreItemCandidates(
                floorNumber,
                requireMapFit: true);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.3] Core Item 候选房已解析");

        builder.AppendLine(
            "Catalog=" +
            (catalog == null
                ? "<missing>"
                : catalog.CatalogId) +
            " | Floor=" + floorNumber +
            " | CatalogCandidates=" + catalogCandidates +
            " | UsableCandidates=" + usableCandidates);

        builder.AppendLine(
            "PlacedCandidateRooms=" + roomIndices.Count +
            " | RoomIndices=" + ids +
            " | ReservedSlot=" +
            (usableCandidates > 0
                ? R943CoreItemReservationRoomIndex.ToString()
                : "NotRequired"));

        builder.Append(
            "ItemSpawnScope=" +
            (roomIndices.Count > 0
                ? "CoreItemCandidateRooms"
                : "LegacyLayoutWideFallback") +
            " | SpecialCoreCandidatesDeferred=True" +
            " | ProgressConditionalSelectionDeferredTo=R9.6");

        return builder.ToString();
    }
}

/// <summary>
/// 由生成器与 ItemSpawner 共用的只读作用域解析器。
/// 只接受当前阶段启用的 CoreItemCandidate；带 Special 的组合标签仍推迟。
/// </summary>
public static class DungeonCoreItemRoomScopeR943
{
    public static void CollectCandidateRoomIndices(
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
                template.HasTag(DreamRoomTag.CoreItemCandidate) &&
                !template.HasTag(DreamRoomTag.Special))
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
