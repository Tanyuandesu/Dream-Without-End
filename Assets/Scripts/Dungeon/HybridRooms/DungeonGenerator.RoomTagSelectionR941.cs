using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R9.4.1：让 StartCandidate／ExitCandidate 在房间集合形成时
/// 正式参与选择，并为缺少角色标签的 Catalog 提供 Standard 回退。
///
/// 本小步只启用 Start／Exit。Rare、CoreItemCandidate 与 Special 的
/// 配额仍留给 R9.4 后续步骤；楼层、权重和单层上限继续使用模板现有字段。
/// </summary>
public sealed partial class DungeonGenerator
{
    private enum R941RequiredRoomRole
    {
        None = 0,
        Start = 1,
        Exit = 2
    }

    private static R941RequiredRoomRole
        R941GetRequiredRoleForPlacementSlot(int roomIndex)
    {
        if (roomIndex == 0)
        {
            return R941RequiredRoomRole.Start;
        }

        if (roomIndex == 1)
        {
            return R941RequiredRoomRole.Exit;
        }

        return R941RequiredRoomRole.None;
    }

    /// <summary>
    /// 对 R4 已完成楼层、单层上限和地图尺寸筛选的候选池施加角色约束。
    /// 角色标签不存在时，依次回退到普通 Standard、带 Special 的 Standard；
    /// 若连 Standard 都没有则明确失败，不会选择 Rare／Special-only 房间。
    /// </summary>
    private static bool R941RestrictCandidatesForRole(
        R941RequiredRoomRole requiredRole,
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

        if (requiredRole == R941RequiredRoomRole.None)
        {
            return true;
        }

        DreamRoomTag requiredTag =
            R941GetRequiredTag(requiredRole);

        if (R941KeepCandidateTemplates(
                candidates,
                requiredTag,
                excludeSpecialRooms: false))
        {
            return true;
        }

        if (R941KeepCandidateTemplates(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: true))
        {
            return true;
        }

        if (R941KeepCandidateTemplates(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: false))
        {
            return true;
        }

        failureReason =
            "没有合格的 " + requiredTag +
            "，也没有可安全回退的 Standard 模板。";

        return false;
    }

    /// <summary>
    /// 为 R5 收集最终 Start／Exit 候选。与 R4 使用完全相同的回退层级，
    /// 因此手工建立的 Placement Layout 也不会把 Rare／Special-only 房间
    /// 静默选作角色房。
    /// </summary>
    private static bool R941CollectPlacementCandidatesForRole(
        IReadOnlyList<DreamRoomPlacement> placements,
        R941RequiredRoomRole requiredRole,
        int excludeRoomIndex,
        List<int> results)
    {
        results.Clear();

        if (placements == null ||
            requiredRole == R941RequiredRoomRole.None)
        {
            return false;
        }

        R5CollectTaggedRoomIndices(
            placements,
            R941GetRequiredTag(requiredRole),
            excludeRoomIndex,
            excludeSpecialRooms: false,
            results: results);

        if (results.Count > 0)
        {
            return true;
        }

        R5CollectTaggedRoomIndices(
            placements,
            DreamRoomTag.Standard,
            excludeRoomIndex,
            excludeSpecialRooms: true,
            results: results);

        if (results.Count > 0)
        {
            return true;
        }

        R5CollectTaggedRoomIndices(
            placements,
            DreamRoomTag.Standard,
            excludeRoomIndex,
            excludeSpecialRooms: false,
            results: results);

        return results.Count > 0;
    }

    private static DreamRoomTag R941GetRequiredTag(
        R941RequiredRoomRole requiredRole)
    {
        return requiredRole == R941RequiredRoomRole.Start
            ? DreamRoomTag.StartCandidate
            : DreamRoomTag.ExitCandidate;
    }

    /// <summary>
    /// 只保留指定类别。若该类别不存在，列表保持原样，方便调用方继续
    /// 尝试下一层回退；成功时才真正缩小候选池。
    /// </summary>
    private static bool R941KeepCandidateTemplates(
        List<DreamRoomTemplate> candidates,
        DreamRoomTag requiredTag,
        bool excludeSpecialRooms)
    {
        bool found = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            DreamRoomTemplate template = candidates[i];

            if (template != null &&
                template.HasTag(requiredTag) &&
                (!excludeSpecialRooms ||
                 !template.HasTag(DreamRoomTag.Special)))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            DreamRoomTemplate template = candidates[i];

            bool keep =
                template != null &&
                template.HasTag(requiredTag) &&
                (!excludeSpecialRooms ||
                 !template.HasTag(DreamRoomTag.Special));

            if (!keep)
            {
                candidates.RemoveAt(i);
            }
        }

        return candidates.Count > 0;
    }

    /// <summary>
    /// R6 完整成功后才建立摘要，避免 R4/R6 重试期间刷屏。
    /// </summary>
    private static string R941BuildResolvedRoleReport(
        DungeonLayout layout,
        int floorNumber,
        string catalogId)
    {
        int startRoomIndex;
        int exitRoomIndex;

        R941ResolveRoleRoomIndices(
            layout,
            out startRoomIndex,
            out exitRoomIndex);

        DreamRoomTemplate startTemplate =
            R941GetTemplateAt(layout, startRoomIndex);

        DreamRoomTemplate exitTemplate =
            R941GetTemplateAt(layout, exitRoomIndex);

        bool startTagged =
            startTemplate != null &&
            startTemplate.HasTag(DreamRoomTag.StartCandidate);

        bool exitTagged =
            exitTemplate != null &&
            exitTemplate.HasTag(DreamRoomTag.ExitCandidate);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.1] Start／Exit RoomTags 选择完成");

        builder.AppendLine(
            "Catalog=" + catalogId +
            " | Floor=" + floorNumber +
            " | StartCandidates=" +
            R941CountPlacedCandidates(
                layout,
                DreamRoomTag.StartCandidate,
                excludeRoomIndex: -1) +
            " | ExitCandidates=" +
            R941CountPlacedCandidates(
                layout,
                DreamRoomTag.ExitCandidate,
                excludeRoomIndex: startRoomIndex));

        builder.AppendLine(
            "StartRoom=" + startRoomIndex +
            " | Template=" +
            R941FormatTemplateId(startTemplate) +
            " | Tagged=" + startTagged);

        builder.AppendLine(
            "ExitRoom=" + exitRoomIndex +
            " | Template=" +
            R941FormatTemplateId(exitTemplate) +
            " | Tagged=" + exitTagged +
            " | Distinct=" +
            (startRoomIndex >= 0 &&
             exitRoomIndex >= 0 &&
             startRoomIndex != exitRoomIndex));

        builder.Append(
            "Fallbacks=" +
            ((startTagged ? 0 : 1) +
             (exitTagged ? 0 : 1)) +
            " | FallbackPolicy=StandardOnly");

        return builder.ToString();
    }

    /// <summary>
    /// 返回空字符串表示无需警告；否则返回这一份最终布局唯一的合并警告。
    /// </summary>
    private static string R941BuildFallbackWarning(
        DungeonLayout layout,
        int floorNumber,
        string catalogId)
    {
        int startRoomIndex;
        int exitRoomIndex;

        R941ResolveRoleRoomIndices(
            layout,
            out startRoomIndex,
            out exitRoomIndex);

        DreamRoomTemplate startTemplate =
            R941GetTemplateAt(layout, startRoomIndex);

        DreamRoomTemplate exitTemplate =
            R941GetTemplateAt(layout, exitRoomIndex);

        bool startTagged =
            startTemplate != null &&
            startTemplate.HasTag(DreamRoomTag.StartCandidate);

        bool exitTagged =
            exitTemplate != null &&
            exitTemplate.HasTag(DreamRoomTag.ExitCandidate);

        if (startTagged && exitTagged)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.1] RoomTags 安全回退（本布局仅此一条）");

        builder.AppendLine(
            "Catalog=" + catalogId +
            " | Floor=" + floorNumber);

        builder.Append(
            "Start=" +
            R941FormatRoleResolution(
                startTemplate,
                DreamRoomTag.StartCandidate));

        builder.Append(
            " | Exit=" +
            R941FormatRoleResolution(
                exitTemplate,
                DreamRoomTag.ExitCandidate));

        builder.Append(
            " | FallbackPolicy=StandardOnly" +
            " | GenerationContinued=True");

        return builder.ToString();
    }

    private static string R941FormatRoleResolution(
        DreamRoomTemplate template,
        DreamRoomTag requiredTag)
    {
        if (template == null)
        {
            return "Invalid(<missing>)";
        }

        string templateId =
            R941FormatTemplateId(template);

        if (template.HasTag(requiredTag))
        {
            return "Tagged(" + templateId + ")";
        }

        if (template.HasTag(DreamRoomTag.Standard))
        {
            return "StandardFallback(" + templateId + ")";
        }

        return "InvalidNonStandard(" + templateId + ")";
    }

    private static void R941ResolveRoleRoomIndices(
        DungeonLayout layout,
        out int startRoomIndex,
        out int exitRoomIndex)
    {
        startRoomIndex = -1;
        exitRoomIndex = -1;

        if (layout == null)
        {
            return;
        }

        startRoomIndex =
            R941FindWalkableRoomIndex(
                layout,
                layout.StartCell);

        exitRoomIndex =
            R941FindWalkableRoomIndex(
                layout,
                layout.ExitCell);
    }

    private static int R941FindWalkableRoomIndex(
        DungeonLayout layout,
        Vector2Int cell)
    {
        if (layout == null)
        {
            return -1;
        }

        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

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

            placement.GetWalkableGlobalCells(
                walkableCells);

            for (int cellIndex = 0;
                 cellIndex < walkableCells.Count;
                 cellIndex++)
            {
                if (walkableCells[cellIndex] == cell)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static DreamRoomTemplate R941GetTemplateAt(
        DungeonLayout layout,
        int roomIndex)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count ||
            layout.RoomPlacements[roomIndex] == null)
        {
            return null;
        }

        return layout.RoomPlacements[roomIndex].Template;
    }

    private static int R941CountPlacedCandidates(
        DungeonLayout layout,
        DreamRoomTag tag,
        int excludeRoomIndex)
    {
        if (layout == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            if (i == excludeRoomIndex)
            {
                continue;
            }

            DreamRoomTemplate template =
                R941GetTemplateAt(layout, i);

            if (template != null &&
                template.HasTag(tag))
            {
                count++;
            }
        }

        return count;
    }

    private static string R941FormatTemplateId(
        DreamRoomTemplate template)
    {
        if (template == null)
        {
            return "<missing>";
        }

        return string.IsNullOrWhiteSpace(template.TemplateId)
            ? "<empty>"
            : template.TemplateId;
    }
}
