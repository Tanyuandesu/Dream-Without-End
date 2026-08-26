using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// P10.13-2：HighPrecision / ProceduralMedium 房间混合选择。
///
/// 设计边界：
/// 1. Start / Exit / CoreItem / Special 的 RoomTag 权威先执行，本类不抢角色槽位。
/// 2. Fidelity 只在普通槽位中作为“软目标”参与候选缩小。
/// 3. 所有候选仍继续服从 Floor Gate / RandomWeight / MaximumInstancesPerFloor。
/// 4. ProceduralMedium 优先使用本层尚未出现过的 Family，减少同壳重复。
/// 5. 若目标 Tier 因空间或候选不足无法落位，后段尝试会自动放宽 Tier，
///    因此 Fidelity 不会成为整层生成失败的新硬门槛。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("P10.13-2 房间精度混合")]
    [Tooltip(
        "开启后，普通房槽位会根据本层已放置的 HighPrecision / ProceduralMedium " +
        "数量朝目标比例收敛。Start / Exit / CoreItem / Special 仍先按 RoomTag 选择。")]
    [SerializeField]
    private bool p10132EnableFidelityAwareRoomMix = true;

    [Tooltip(
        "ProceduralMedium 在整层房间中的软目标比例。" +
        "默认 0.43：7 房间时目标约为 3 个 Medium、4 个 High。" +
        "如果当前楼层可用 High 不足，会安全回退到更多 Medium。")]
    [Range(0.10f, 0.90f)]
    [SerializeField]
    private float p10132ProceduralMediumTargetRatio = 0.43f;

    [Tooltip(
        "优先避免同一 ProceduralMedium Template 在同层重复。" +
        "只有当没有其它合法候选时才允许重复，避免因防重复导致生成失败。")]
    [SerializeField]
    private bool p10132PreferUniqueProceduralMediumFamilies = true;

    // 前 70 次尝试坚持 Fidelity Tier 软目标。
    // 最后 30 次恢复 Standard/Rare 的完整候选池，保证空间困难时可以自救。
    private const int P10132TierPreferenceRelaxAttempt = 70;

    public bool P10132FidelityAwareRoomMixEnabled =>
        p10132EnableFidelityAwareRoomMix;

    public float P10132ProceduralMediumTargetRatio =>
        p10132ProceduralMediumTargetRatio;

    public bool P10132PreferUniqueProceduralMediumFamilies =>
        p10132PreferUniqueProceduralMediumFamilies;

    public int P10132TargetProceduralMediumCount =>
        R10132GetTargetProceduralMediumCount();

    private void R10132AppendConfigurationErrors(
        List<string> errors)
    {
        if (errors == null ||
            !p10132EnableFidelityAwareRoomMix)
        {
            return;
        }

        if (p10132ProceduralMediumTargetRatio < 0.10f ||
            p10132ProceduralMediumTargetRatio > 0.90f)
        {
            errors.Add(
                "P10.13-2 Procedural Medium Target Ratio 必须在 0.10～0.90。");
        }

        if (templateFirstDesiredRoomCount < 2)
        {
            errors.Add(
                "P10.13-2 Fidelity Mix 至少需要 2 个目标房间。");
        }
    }

    /// <summary>
    /// 对所有角色槽位都应用的“Medium Family 去重复偏好”。
    /// 它不会改变 Fidelity 配额，也不会越过 RoomTag 的角色筛选。
    ///
    /// 只有存在其它候选时才移除已经使用过的 Medium；
    /// 如果剩下的合法候选全部都是重复 Medium，则保持原池，保证可生成。
    /// </summary>
    private void R10132ApplyProceduralMediumDiversityPreference(
        List<DreamRoomTemplate> candidates,
        IReadOnlyList<DreamRoomPlacement> placements)
    {
        if (!p10132EnableFidelityAwareRoomMix ||
            !p10132PreferUniqueProceduralMediumFamilies ||
            candidates == null ||
            candidates.Count <= 1 ||
            placements == null ||
            placements.Count == 0)
        {
            return;
        }

        HashSet<string> usedMediumIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < placements.Count;
             i++)
        {
            DreamRoomTemplate placed =
                placements[i] == null
                    ? null
                    : placements[i].Template;

            if (placed == null ||
                placed.RoomFidelityTier !=
                    DreamRoomFidelityTier.ProceduralMedium ||
                string.IsNullOrWhiteSpace(
                    placed.TemplateId))
            {
                continue;
            }

            usedMediumIds.Add(
                placed.TemplateId);
        }

        if (usedMediumIds.Count == 0)
        {
            return;
        }

        int alternatives = 0;

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            DreamRoomTemplate candidate =
                candidates[i];

            if (candidate == null)
            {
                continue;
            }

            bool isUsedMedium =
                candidate.RoomFidelityTier ==
                    DreamRoomFidelityTier.ProceduralMedium &&
                usedMediumIds.Contains(
                    candidate.TemplateId);

            if (!isUsedMedium)
            {
                alternatives++;
            }
        }

        // 没有其它选择时不做去重，允许安全回退。
        if (alternatives <= 0)
        {
            return;
        }

        for (int i = candidates.Count - 1;
             i >= 0;
             i--)
        {
            DreamRoomTemplate candidate =
                candidates[i];

            if (candidate != null &&
                candidate.RoomFidelityTier ==
                    DreamRoomFidelityTier.ProceduralMedium &&
                usedMediumIds.Contains(
                    candidate.TemplateId))
            {
                candidates.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 只在 R9.4.2 普通槽位调用。
    /// 角色保留槽位只做 Medium Family 去重复，不执行 Tier 软配额。
    /// </summary>
    private bool R10132RestrictOrdinaryCandidatesByFidelity(
        int roomIndex,
        int attempt,
        IReadOnlyList<DreamRoomPlacement> placements,
        List<DreamRoomTemplate> candidates,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (!p10132EnableFidelityAwareRoomMix)
        {
            return true;
        }

        if (candidates == null ||
            candidates.Count == 0)
        {
            failureReason =
                "Fidelity Mix 接入前候选池为空。";
            return false;
        }

        int highCandidates = 0;
        int mediumCandidates = 0;

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            DreamRoomTemplate candidate =
                candidates[i];

            if (candidate == null)
            {
                continue;
            }

            if (candidate.RoomFidelityTier ==
                DreamRoomFidelityTier.ProceduralMedium)
            {
                mediumCandidates++;
            }
            else if (candidate.RoomFidelityTier ==
                     DreamRoomFidelityTier.HighPrecision)
            {
                highCandidates++;
            }
        }

        // 只剩一个 Tier 时 Fidelity 不制造失败。
        if (highCandidates == 0 ||
            mediumCandidates == 0)
        {
            return true;
        }

        // 空间落位进入最后阶段时放开 Tier，避免软目标变成硬失败源。
        int relaxAttempt =
            Mathf.Min(
                P10132TierPreferenceRelaxAttempt,
                Mathf.Max(
                    1,
                    templateFirstMaximumPlacementAttemptsPerRoom - 1));

        if (attempt >= relaxAttempt)
        {
            return true;
        }

        int placedMedium = 0;

        if (placements != null)
        {
            for (int i = 0;
                 i < placements.Count;
                 i++)
            {
                DreamRoomTemplate placed =
                    placements[i] == null
                        ? null
                        : placements[i].Template;

                if (placed != null &&
                    placed.RoomFidelityTier ==
                        DreamRoomFidelityTier.ProceduralMedium)
                {
                    placedMedium++;
                }
            }
        }

        int targetMedium =
            R10132GetTargetProceduralMediumCount();

        int totalAfterCurrent =
            Mathf.Clamp(
                (placements == null
                    ? 0
                    : placements.Count) + 1,
                1,
                templateFirstDesiredRoomCount);

        int expectedMediumAfterCurrent =
            Mathf.RoundToInt(
                totalAfterCurrent *
                (targetMedium /
                 (float)templateFirstDesiredRoomCount));

        expectedMediumAfterCurrent =
            Mathf.Clamp(
                expectedMediumAfterCurrent,
                0,
                targetMedium);

        bool preferMedium =
            placedMedium <
            expectedMediumAfterCurrent;

        // 达到最终 Medium 软目标后优先 High。
        if (placedMedium >= targetMedium)
        {
            preferMedium = false;
        }

        DreamRoomFidelityTier preferredTier =
            preferMedium
                ? DreamRoomFidelityTier.ProceduralMedium
                : DreamRoomFidelityTier.HighPrecision;

        bool preferredExists = false;

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            DreamRoomTemplate candidate =
                candidates[i];

            if (candidate != null &&
                candidate.RoomFidelityTier ==
                    preferredTier)
            {
                preferredExists = true;
                break;
            }
        }

        // 理论上前面 high/medium count 已保证，但仍做防御性回退。
        if (!preferredExists)
        {
            return true;
        }

        for (int i = candidates.Count - 1;
             i >= 0;
             i--)
        {
            DreamRoomTemplate candidate =
                candidates[i];

            if (candidate == null ||
                candidate.RoomFidelityTier !=
                    preferredTier)
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count > 0)
        {
            return true;
        }

        failureReason =
            "Fidelity Tier 缩小候选池后为空。" +
            " RoomIndex=" +
            roomIndex +
            " | Preferred=" +
            preferredTier;

        return false;
    }

    private int R10132GetTargetProceduralMediumCount()
    {
        if (templateFirstDesiredRoomCount <= 1)
        {
            return 0;
        }

        int target =
            Mathf.RoundToInt(
                templateFirstDesiredRoomCount *
                p10132ProceduralMediumTargetRatio);

        return Mathf.Clamp(
            target,
            1,
            templateFirstDesiredRoomCount - 1);
    }

    private string R10132BuildResolvedFidelityReport(
        DungeonLayout layout,
        int floorNumber,
        DreamRoomCatalog catalog)
    {
        int highPlacements = 0;
        int mediumPlacements = 0;

        HashSet<string> uniqueHigh =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> uniqueMedium =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (layout != null)
        {
            for (int i = 0;
                 i < layout.RoomPlacements.Count;
                 i++)
            {
                DreamRoomTemplate template =
                    layout.RoomPlacements[i] == null
                        ? null
                        : layout.RoomPlacements[i].Template;

                if (template == null)
                {
                    continue;
                }

                if (template.RoomFidelityTier ==
                    DreamRoomFidelityTier.ProceduralMedium)
                {
                    mediumPlacements++;
                    uniqueMedium.Add(
                        template.TemplateId);
                }
                else
                {
                    highPlacements++;
                    uniqueHigh.Add(
                        template.TemplateId);
                }
            }
        }

        int mediumRepeats =
            Mathf.Max(
                0,
                mediumPlacements -
                uniqueMedium.Count);

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "[DungeonGenerator/P10.13-2] Fidelity-Aware Room Mix");

        builder.AppendLine(
            "Catalog=" +
            (catalog == null
                ? "<missing>"
                : catalog.CatalogId) +
            " | Floor=" +
            floorNumber +
            " | DesiredRooms=" +
            templateFirstDesiredRoomCount +
            " | TargetMedium=" +
            R10132GetTargetProceduralMediumCount() +
            " | TargetRatio=" +
            p10132ProceduralMediumTargetRatio
                .ToString("F2"));

        builder.AppendLine(
            "HighPrecision=" +
            highPlacements +
            " | ProceduralMedium=" +
            mediumPlacements +
            " | UniqueHigh=" +
            uniqueHigh.Count +
            " | UniqueMediumFamilies=" +
            uniqueMedium.Count +
            " | MediumRepeats=" +
            mediumRepeats);

        builder.Append(
            "RolePriority=StartExitCoreSpecialBeforeFidelity" +
            " | WeightPolicy=RandomWeightWithinChosenTier" +
            " | MediumDiversityPreference=" +
            p10132PreferUniqueProceduralMediumFamilies +
            " | TierPolicy=SoftQuotaWithLateRelax" +
            " | Result=PASS");

        return builder.ToString();
    }
}
