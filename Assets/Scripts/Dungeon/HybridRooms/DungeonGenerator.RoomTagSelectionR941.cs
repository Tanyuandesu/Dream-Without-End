using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// R9.4.1：让 StartCandidate／ExitCandidate 在房间集合形成时
/// 正式参与选择，并为缺少标签候选的 Catalog 提供 Standard 回退。
///
/// 阶段边界：
/// 1. 只启用 Start／Exit 两种角色标签；
/// 2. Rare、CoreItemCandidate、Special 留给后续 R9.4 小步；
/// 3. 不新增楼层、权重或单层上限字段，继续复用 DreamRoomTemplate；
/// 4. 回退只接受 Standard，避免把 Rare／Special 静默当作出生房。
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
    /// 对 R4 当前已经通过楼层、单层上限和地图尺寸筛选的候选池，
    /// 进一步施加角色约束。Start／Exit 标签不存在时，依次回退到：
    /// 普通 Standard、允许带 Special 的 Standard；若连 Standard 都没有，
    /// 明确失败，不会静默选择任意特殊房。
    /// </summary>
    private static bool R941RestrictCandidatesForRole(
        R941RequiredRoomRole requiredRole,
        List<DreamRoomTemplate> candidates,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (requiredRole == R941RequiredRoomRole.None)
        {
            return candidates != null && candidates.Count > 0;
        }

        if (candidates == null || candidates.Count == 0)
        {
            failureReason =
                "楼层、Maximum Instances 与地图尺寸筛选后没有任何候选。";
            return false;
        }

        DreamRoomTag requiredTag =
            requiredRole == R941RequiredRoomRole.Start
                ? DreamRoomTag.StartCandidate
                : DreamRoomTag.ExitCandidate;

        if (R941HasMatchingCandidate(
                candidates,
                requiredTag,
                excludeSpecialRooms: false))
        {
            R941KeepMatchingCandidates(
                candidates,
                requiredTag,
                excludeSpecialRooms: false);

            return candidates.Count > 0;
        }

        if (R941HasMatchingCandidate(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: true))
        {
            R941KeepMatchingCandidates(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: true);

            return candidates.Count > 0;
        }

        if (R941HasMatchingCandidate(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: false))
        {
            R941KeepMatchingCandidates(
                candidates,
                DreamRoomTag.Standard,
                excludeSpecialRooms: false);

            return candidates.Count > 0;
        }

        failureReason =
            "没有合格的 " + requiredTag +
            "，同时也没有可安全回退的 Standard 模板。";

        return false;
    }

    private static bool R941HasMatchingCandidate(
        List<DreamRoomTemplate> candidates,
        DreamRoomTag requiredTag,
        bool excludeSpecialRooms)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            DreamRoomTemplate template = candidates[i];

            if (template == null ||
                !template.HasTag(requiredTag))
            {
                continue;
            }

            if (excludeSpecialRooms &&
                template.HasTag(DreamRoomTag.Special))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void R941KeepMatchingCandidates(
        List<DreamRoomTemplate> candidates,
        DreamRoomTag requiredTag,
        bool excludeSpecialRooms)
    {
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
    }

    /// <summary>
    /// R6 最终成功后生成一次角色摘要。放在 R6 而不是 R4 输出，
    /// 可以避免坐标重试或走廊重试产生重复警告。
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
            startTemplate.HasTag(
                DreamRoomTag.StartCandidate);

        bool exitTagged =
            exitTemplate != null &&
            exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate);

        int startCandidateCount =
            R941CountPlacedCandidates(
                layout,
                DreamRoomTag.StartCandidate,
                excludeRoomIndex: -1);

        int exitCandidateCount =
            R941CountPlacedCandidates(
                layout,
                DreamRoomTag.ExitCandidate,
                excludeRoomIndex: startRoomIndex);

        int fallbackCount =
            (startTagged ? 0 : 1) +
            (exitTagged ? 0 : 1);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.1] Start／Exit RoomTags 选择完成");

        builder.AppendLine(
            "Catalog=" + catalogId +
            " | Floor=" + floorNumber +
            " | StartCandidates=" + startCandidateCount +
            " | ExitCandidates=" + exitCandidateCount);

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
            "Fallbacks=" + fallbackCount +
            " | FallbackPolicy=StandardOnly");

        return builder.ToString();
    }

    /// <summary>
    /// 返回空字符串表示无需警告；否则返回本次最终布局唯一的一条合并警告。
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

        bool startFallback =
            startTemplate == null ||
            !startTemplate.HasTag(
                DreamRoomTag.StartCandidate);

        bool exitFallback =
            exitTemplate == null ||
            !exitTemplate.HasTag(
                DreamRoomTag.ExitCandidate);

        if (!startFallback && !exitFallback)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/R9.4.1] RoomTags 安全回退（本层仅此一条）");

        builder.AppendLine(
            "Catalog=" + catalogId +
            " | Floor=" + floorNumber);

        builder.Append(
            "Start=" +
            (startFallback
                ? "StandardFallback(" +
                  R941FormatTemplateId(startTemplate) + ")"
                : "Tagged(" +
                  R941FormatTemplateId(startTemplate) + ")"));

        builder.Append(
            " | Exit=" +
            (exitFallback
                ? "StandardFallback(" +
                  R941FormatTemplateId(exitTemplate) + ")"
                : "Tagged(" +
                  R941FormatTemplateId(exitTemplate) + ")"));

        builder.Append(
            " | GenerationContinued=True");

        return builder.ToString();
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
        UnityEngine.Vector2Int cell)
    {
        if (layout == null)
        {
            return -1;
        }

        List<UnityEngine.Vector2Int> walkableCells =
            new List<UnityEngine.Vector2Int>();

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
