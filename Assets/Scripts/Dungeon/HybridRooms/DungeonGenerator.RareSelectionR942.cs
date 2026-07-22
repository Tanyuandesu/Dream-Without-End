using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// R9.4.2：正式启用普通房槽位中的 Rare 候选，并继续复用
/// DreamRoomTemplate.RandomWeight 与 MaximumInstancesPerFloor。
///
/// 规则边界：
/// 1. Start／Exit 槽位继续完全由 R9.4.1 处理；
/// 2. 其余普通槽位只接受 Standard 或 Rare；
/// 3. CoreItemCandidate／Special 即使同时带有其他标签，也不进入普通槽位，
///    而由 R9.4.3／R9.4.4 的保留角色处理；
/// 4. 单层计数仍只在房间成功落位后增加，不把失败尝试计入配额；
/// 5. MaximumInstancesPerFloor 是“每个 Template”的上限，不是全部 Rare
///    共用一个全局上限。
/// </summary>
public sealed partial class DungeonGenerator
{
    private const DreamRoomTag R942DeferredOrdinaryTags =
        DreamRoomTag.CoreItemCandidate |
        DreamRoomTag.Special;

    /// <summary>
    /// 对 R9.4.1 未保留角色的普通槽位施加当前阶段的标签边界。
    /// 本方法只缩小候选池，不调用随机数，因此 Graybox 的固定 Seed
    /// 随机调用序列保持不变。
    /// </summary>
    private static bool R942RestrictCandidatesForOrdinarySlot(
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

            bool isCurrentOrdinaryCategory =
                template != null &&
                (template.HasTag(DreamRoomTag.Standard) ||
                 template.HasTag(DreamRoomTag.Rare));

            bool carriesDeferredCategory =
                template != null &&
                template.RoomTags.HasAny(
                    R942DeferredOrdinaryTags);

            if (!isCurrentOrdinaryCategory ||
                carriesDeferredCategory)
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count > 0)
        {
            return true;
        }

        failureReason =
            "当前普通槽位没有 Standard／Rare 候选；" +
            "CoreItemCandidate 与 Special 必须由各自保留角色处理。";

        return false;
    }

    /// <summary>
    /// 对已经形成的布局做防御性 Rare 配额检查。
    /// R4 正常路径本来就会通过 Catalog 候选筛选保证上限；这里让该契约
    /// 进入正式生成结果校验，避免未来重构或手工布局静默越界。
    /// </summary>
    private static void R942AppendRareQuotaValidationErrors(
        DungeonLayout layout,
        List<string> errors)
    {
        if (layout == null || errors == null)
        {
            return;
        }

        Dictionary<string, int> rareCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, DreamRoomTemplate> rareTemplates =
            new Dictionary<string, DreamRoomTemplate>(
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

            if (template == null ||
                !template.HasTag(DreamRoomTag.Rare))
            {
                continue;
            }

            string templateId = template.TemplateId;

            if (string.IsNullOrWhiteSpace(templateId))
            {
                continue;
            }

            int currentCount;
            rareCounts.TryGetValue(
                templateId,
                out currentCount);

            rareCounts[templateId] =
                currentCount + 1;

            if (!rareTemplates.ContainsKey(templateId))
            {
                rareTemplates.Add(templateId, template);
            }
        }

        foreach (KeyValuePair<string, int> pair
                 in rareCounts)
        {
            DreamRoomTemplate template =
                rareTemplates[pair.Key];

            int maximum =
                template.MaximumInstancesPerFloor;

            if (maximum > 0 && pair.Value > maximum)
            {
                errors.Add(
                    "R9.4.2 Rare 模板 '" + pair.Key +
                    "' 已放置 " + pair.Value +
                    " 次，超过单层上限 " + maximum + "。" );
            }
        }
    }

    /// <summary>
    /// 完整 R6 布局成功后输出一次 Rare 选择摘要。
    /// 只读布局与 Catalog，不修改对象，也不产生额外随机调用。
    /// </summary>
    private static string R942BuildResolvedRareReport(
        DungeonLayout layout,
        int floorNumber,
        DreamRoomCatalog catalog)
    {
        int catalogRareTemplates = 0;
        int eligibleRareTemplates = 0;

        if (catalog != null &&
            catalog.RoomTemplates != null)
        {
            for (int i = 0;
                 i < catalog.RoomTemplates.Count;
                 i++)
            {
                DreamRoomTemplate template =
                    catalog.RoomTemplates[i];

                if (template == null ||
                    !template.HasTag(DreamRoomTag.Rare))
                {
                    continue;
                }

                catalogRareTemplates++;

                if (template.CanAppearOnFloor(floorNumber))
                {
                    eligibleRareTemplates++;
                }
            }
        }

        Dictionary<string, int> placedCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, DreamRoomTemplate> placedTemplates =
            new Dictionary<string, DreamRoomTemplate>(
                StringComparer.OrdinalIgnoreCase);

        int rarePlacements = 0;
        int reservedRolePlacements = 0;

        if (layout != null)
        {
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
                    continue;
                }

                if (template.RoomTags.HasAny(
                        R942DeferredOrdinaryTags))
                {
                    reservedRolePlacements++;
                }

                if (!template.HasTag(DreamRoomTag.Rare))
                {
                    continue;
                }

                rarePlacements++;

                string templateId = template.TemplateId;

                if (string.IsNullOrWhiteSpace(templateId))
                {
                    templateId = "<empty>";
                }

                int currentCount;
                placedCounts.TryGetValue(
                    templateId,
                    out currentCount);

                placedCounts[templateId] =
                    currentCount + 1;

                if (!placedTemplates.ContainsKey(templateId))
                {
                    placedTemplates.Add(templateId, template);
                }
            }
        }

        int capBreaches = 0;
        StringBuilder details = new StringBuilder();

        foreach (KeyValuePair<string, int> pair
                 in placedCounts)
        {
            DreamRoomTemplate template =
                placedTemplates[pair.Key];

            int maximum =
                template.MaximumInstancesPerFloor;

            if (maximum > 0 && pair.Value > maximum)
            {
                capBreaches++;
            }

            if (details.Length > 0)
            {
                details.Append(", ");
            }

            details.Append(pair.Key);
            details.Append('=');
            details.Append(pair.Value);
            details.Append('/');
            details.Append(
                maximum == 0
                    ? "Unlimited"
                    : maximum.ToString());
            details.Append("(Weight=");
            details.Append(template.RandomWeight);
            details.Append(')');
        }

        if (details.Length == 0)
        {
            details.Append("None");
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.2] Rare 权重／单层上限已应用");

        builder.AppendLine(
            "Catalog=" +
            (catalog == null
                ? "<missing>"
                : catalog.CatalogId) +
            " | Floor=" + floorNumber +
            " | RareCatalogTemplates=" +
            catalogRareTemplates +
            " | EligibleRareTemplates=" +
            eligibleRareTemplates);

        builder.AppendLine(
            "RarePlacements=" + rarePlacements +
            " | RareCapBreaches=" + capBreaches +
            " | ReservedCoreSpecialPlacements=" +
            reservedRolePlacements);

        builder.AppendLine(
            "RareUsage=" + details);

        builder.Append(
            "WeightPolicy=RandomWeight" +
            " | CountPolicy=CommittedPlacementsOnly" +
            " | CapScope=PerTemplatePerFloor" +
            " | OrdinaryPool=Standard+Rare" +
            " | CoreAndSpecialExcludedFromOrdinary=True");

        return builder.ToString();
    }
}
