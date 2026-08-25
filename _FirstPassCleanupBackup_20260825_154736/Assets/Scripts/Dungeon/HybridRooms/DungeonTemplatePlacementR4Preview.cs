using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// R4 Template First 放置的独立诊断与 Scene Gizmo 预览。
///
/// 本组件没有 Awake/Start/Update，不参加正式游戏流程；
/// 只有从组件菜单主动执行校验时，才会在内存中调用 DungeonGenerator
/// 的 R4 方法。它不会实例化房间 Prefab，也不会改写当前地图。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonTemplatePlacementR4Preview : MonoBehaviour
{
    [Header("R4 诊断目标")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    [Min(1)]
    [SerializeField]
    private int previewFloorNumber = 1;

    [SerializeField]
    private int previewFixedSeed = 12345;

    [Header("确定性与覆盖测试")]
    [Tooltip(
        "从 Fixed Seed 往后检查多少个 Seed，确认改变 Seed 能改变结果。")]
    [Range(1, 32)]
    [SerializeField]
    private int changedSeedSearchCount = 8;

    [Tooltip(
        "搜索一张同时包含当前楼层所有可放置模板的布局。" +
        "R3 的四个灰盒模板默认可在该范围内找到。")]
    [Range(1, 256)]
    [SerializeField]
    private int catalogCoverageSeedSearchCount = 64;

    [Header("Scene Gizmo")]
    [Min(0.05f)]
    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private bool drawMapBounds = true;

    [SerializeField]
    private bool drawRoomPadding = true;

    [SerializeField]
    private bool drawOccupiedCells;

    private DungeonLayout lastPreviewLayout;
    private int lastPreviewSeed;

    [ContextMenu("Validate R4 Template First Placement")]
    public void ValidateR4TemplateFirstPlacement()
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError(
                "[DungeonTemplatePlacementR4Preview] " +
                "Dungeon Generator 不能为空。" +
                "请把本组件放在 GameManager 上，或拖入其 DungeonGenerator。",
                this);

            return;
        }

        DungeonLayout primaryLayout;
        string primaryReport;

        if (!dungeonGenerator.TryGenerateTemplateFirstLayout(
                previewFloorNumber,
                previewFixedSeed,
                out primaryLayout,
                out primaryReport))
        {
            lastPreviewLayout = null;

            Debug.LogError(
                "[DungeonTemplatePlacementR4Preview] " +
                "R4 正式校验失败\n" +
                primaryReport,
                this);

            return;
        }

        List<string> errors = new List<string>();

        R4AppendErrors(
            "DungeonLayout",
            primaryLayout.GetValidationErrors(),
            errors);

        R4ValidatePlacementContract(
            primaryLayout,
            errors);

        string primarySignature =
            R4BuildPlacementSignature(primaryLayout);

        R4ValidateSameSeedReproducibility(
            primarySignature,
            errors);

        int changedSeed;
        R4ValidateChangedSeed(
            primarySignature,
            out changedSeed,
            errors);

        DungeonLayout coverageLayout;
        string coverageReport;
        int coverageSeed;
        int requiredCatalogTemplateCount;

        bool foundCatalogCoverage =
            R4TryFindCatalogCoverageLayout(
                out coverageLayout,
                out coverageReport,
                out coverageSeed,
                out requiredCatalogTemplateCount,
                errors);

        DungeonLayout displayedLayout = primaryLayout;
        string displayedGenerationReport = primaryReport;
        int displayedSeed = previewFixedSeed;

        if (foundCatalogCoverage &&
            coverageLayout != null)
        {
            displayedLayout = coverageLayout;
            displayedGenerationReport = coverageReport;
            displayedSeed = coverageSeed;

            R4ValidatePlacementContract(
                coverageLayout,
                errors);
        }

        lastPreviewLayout = displayedLayout;
        lastPreviewSeed = displayedSeed;

        if (errors.Count > 0)
        {
            Debug.LogError(
                R4BuildFailureReport(
                    errors,
                    displayedGenerationReport),
                this);

            return;
        }

        Debug.Log(
            R4BuildSuccessReport(
                primaryLayout,
                displayedLayout,
                changedSeed,
                coverageSeed,
                requiredCatalogTemplateCount,
                displayedGenerationReport),
            this);
    }

    private void R4ValidatePlacementContract(
        DungeonLayout layout,
        List<string> errors)
    {
        if (layout == null)
        {
            errors.Add("诊断布局为空。");
            return;
        }

        if (!layout.HasHybridRoomData)
        {
            errors.Add("布局没有被识别为 Hybrid Room 数据。");
        }

        if (layout.RoomPlacements.Count !=
            dungeonGenerator.TemplateFirstDesiredRoomCount)
        {
            errors.Add(
                "RoomPlacements 数量应为 " +
                dungeonGenerator.TemplateFirstDesiredRoomCount +
                "，实际为 " +
                layout.RoomPlacements.Count + "。");
        }

        if (layout.Connections.Count != 0)
        {
            errors.Add(
                "R4 不应提前生成 Connections；实际为 " +
                layout.Connections.Count + "。");
        }

        if (layout.CorridorCells.Count != 0)
        {
            errors.Add(
                "R4 不应提前生成 CorridorCells；实际为 " +
                layout.CorridorCells.Count + "。");
        }

        if (!layout.FloorCells.SetEquals(
                layout.RoomCells))
        {
            errors.Add(
                "R4 尚无走廊，因此 FloorCells 必须与 RoomCells 完全一致。");
        }

        int minimumCell =
            dungeonGenerator.TemplateFirstMapBorder;

        int maximumExclusiveX =
            dungeonGenerator.TemplateFirstMapWidth -
            dungeonGenerator.TemplateFirstMapBorder;

        int maximumExclusiveY =
            dungeonGenerator.TemplateFirstMapHeight -
            dungeonGenerator.TemplateFirstMapBorder;

        Dictionary<string, int> templateCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                errors.Add(
                    "RoomPlacement " + i +
                    " 缺少 Template。");
                continue;
            }

            RectInt bounds = placement.CellBounds;

            if (bounds.xMin < minimumCell ||
                bounds.yMin < minimumCell ||
                bounds.xMax > maximumExclusiveX ||
                bounds.yMax > maximumExclusiveY)
            {
                errors.Add(
                    "RoomPlacement " + i +
                    " 超出地图 Border 内部范围：" +
                    placement + "。");
            }

            if (!placement.Template.CanAppearOnFloor(
                    previewFloorNumber))
            {
                errors.Add(
                    "模板 '" +
                    placement.Template.TemplateId +
                    "' 不允许出现在 Floor " +
                    previewFloorNumber + "。");
            }

            int currentCount;

            templateCounts.TryGetValue(
                placement.Template.TemplateId,
                out currentCount);

            templateCounts[
                placement.Template.TemplateId] =
                currentCount + 1;

            for (int otherIndex = 0;
                 otherIndex < i;
                 otherIndex++)
            {
                DreamRoomPlacement other =
                    layout.RoomPlacements[otherIndex];

                if (other != null &&
                    placement.OverlapsWithPadding(
                        other,
                        dungeonGenerator.TemplateFirstRoomPadding))
                {
                    errors.Add(
                        "RoomPlacement " + i +
                        " 与 " + otherIndex +
                        " 重叠或违反 Room Padding。");
                }
            }
        }

        foreach (KeyValuePair<string, int> pair in
                 templateCounts)
        {
            DreamRoomTemplate template;

            if (!dungeonGenerator.TemplateFirstRoomCatalog.
                    TryGetTemplate(pair.Key, out template))
            {
                errors.Add(
                    "布局使用了 Catalog 中不存在的 Template Id '" +
                    pair.Key + "'。");
                continue;
            }

            int maximum =
                template.MaximumInstancesPerFloor;

            if (maximum > 0 && pair.Value > maximum)
            {
                errors.Add(
                    "模板 '" + pair.Key +
                    "' 使用 " + pair.Value +
                    " 次，超过单层上限 " +
                    maximum + "。");
            }
        }
    }

    private void R4ValidateSameSeedReproducibility(
        string primarySignature,
        List<string> errors)
    {
        DungeonLayout repeatedLayout;
        string repeatedReport;

        if (!dungeonGenerator.TryGenerateTemplateFirstLayout(
                previewFloorNumber,
                previewFixedSeed,
                out repeatedLayout,
                out repeatedReport))
        {
            errors.Add(
                "相同 Seed 的第二次生成失败，无法证明可复现。\n" +
                repeatedReport);
            return;
        }

        string repeatedSignature =
            R4BuildPlacementSignature(repeatedLayout);

        if (!string.Equals(
                primarySignature,
                repeatedSignature,
                StringComparison.Ordinal))
        {
            errors.Add(
                "相同 Floor 与 Seed 得到了不同的房间选择／旋转／坐标。");
        }
    }

    private void R4ValidateChangedSeed(
        string primarySignature,
        out int changedSeed,
        List<string> errors)
    {
        changedSeed = previewFixedSeed;

        for (int offset = 1;
             offset <= changedSeedSearchCount;
             offset++)
        {
            int candidateSeed = unchecked(
                previewFixedSeed + offset);

            DungeonLayout candidateLayout;
            string candidateReport;

            if (!dungeonGenerator.TryGenerateTemplateFirstLayout(
                    previewFloorNumber,
                    candidateSeed,
                    out candidateLayout,
                    out candidateReport))
            {
                continue;
            }

            string candidateSignature =
                R4BuildPlacementSignature(candidateLayout);

            if (!string.Equals(
                    primarySignature,
                    candidateSignature,
                    StringComparison.Ordinal))
            {
                changedSeed = candidateSeed;
                return;
            }
        }

        errors.Add(
            "从 Seed " + previewFixedSeed +
            " 开始检查了 " + changedSeedSearchCount +
            " 个其他 Seed，但没有找到不同布局。");
    }

    private bool R4TryFindCatalogCoverageLayout(
        out DungeonLayout coverageLayout,
        out string coverageReport,
        out int coverageSeed,
        out int requiredTemplateCount,
        List<string> errors)
    {
        coverageLayout = null;
        coverageReport = string.Empty;
        coverageSeed = previewFixedSeed;
        requiredTemplateCount = 0;

        DreamRoomCatalog catalog =
            dungeonGenerator.TemplateFirstRoomCatalog;

        if (catalog == null)
        {
            errors.Add("Template First Room Catalog 为空。");
            return false;
        }

        List<DreamRoomTemplate> eligibleTemplates =
            new List<DreamRoomTemplate>();

        catalog.GetEligibleTemplates(
            previewFloorNumber,
            null,
            eligibleTemplates);

        HashSet<string> requiredTemplateIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            DreamRoomTemplate template =
                eligibleTemplates[i];

            if (R4CanFitPreviewMap(template))
            {
                requiredTemplateIds.Add(
                    template.TemplateId);
            }
        }

        requiredTemplateCount = requiredTemplateIds.Count;

        if (requiredTemplateCount == 0)
        {
            errors.Add("当前楼层没有可放进 R4 地图的 Catalog 模板。");
            return false;
        }

        if (requiredTemplateCount >
            dungeonGenerator.TemplateFirstDesiredRoomCount)
        {
            errors.Add(
                "Catalog 当前有 " + requiredTemplateCount +
                " 个可用模板，但 Desired Room Count 只有 " +
                dungeonGenerator.TemplateFirstDesiredRoomCount +
                "，不可能在同一布局覆盖全部模板。");
            return false;
        }

        for (int offset = 0;
             offset < catalogCoverageSeedSearchCount;
             offset++)
        {
            int candidateSeed = unchecked(
                previewFixedSeed + offset);

            DungeonLayout candidateLayout;
            string candidateReport;

            if (!dungeonGenerator.TryGenerateTemplateFirstLayout(
                    previewFloorNumber,
                    candidateSeed,
                    out candidateLayout,
                    out candidateReport))
            {
                continue;
            }

            HashSet<string> usedTemplateIds =
                R4CollectUsedTemplateIds(candidateLayout);

            if (usedTemplateIds.IsSupersetOf(
                    requiredTemplateIds))
            {
                coverageLayout = candidateLayout;
                coverageReport = candidateReport;
                coverageSeed = candidateSeed;
                return true;
            }
        }

        errors.Add(
            "在 " + catalogCoverageSeedSearchCount +
            " 个 Seed 内，没有找到一张同时包含全部 " +
            requiredTemplateCount +
            " 个当前可用 Catalog 模板的布局。" +
            "R3 默认 Catalog 应能通过该检查。");

        return false;
    }

    private bool R4CanFitPreviewMap(
        DreamRoomTemplate template)
    {
        if (template == null)
        {
            return false;
        }

        int usableWidth =
            dungeonGenerator.TemplateFirstMapWidth -
            dungeonGenerator.TemplateFirstMapBorder * 2;

        int usableHeight =
            dungeonGenerator.TemplateFirstMapHeight -
            dungeonGenerator.TemplateFirstMapBorder * 2;

        int rotationCount =
            dungeonGenerator.TemplateFirstUseAllowedQuarterTurns &&
            template.AllowQuarterTurns
                ? 4
                : 1;

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
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> R4CollectUsedTemplateIds(
        DungeonLayout layout)
    {
        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (layout == null)
        {
            return ids;
        }

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement != null &&
                placement.Template != null)
            {
                ids.Add(
                    placement.Template.TemplateId);
            }
        }

        return ids;
    }

    private static string R4BuildPlacementSignature(
        DungeonLayout layout)
    {
        StringBuilder builder = new StringBuilder();

        if (layout == null)
        {
            return "null";
        }

        for (int i = 0;
             i < layout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                builder.Append("null;");
                continue;
            }

            builder.Append(
                placement.Template.TemplateId);
            builder.Append('|');
            builder.Append(placement.MinimumCell.x);
            builder.Append(',');
            builder.Append(placement.MinimumCell.y);
            builder.Append('|');
            builder.Append(
                placement.ClockwiseQuarterTurns);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private string R4BuildSuccessReport(
        DungeonLayout primaryLayout,
        DungeonLayout displayedLayout,
        int changedSeed,
        int coverageSeed,
        int requiredCatalogTemplateCount,
        string generationReport)
    {
        HashSet<string> primaryTemplates =
            R4CollectUsedTemplateIds(primaryLayout);

        HashSet<string> displayedTemplates =
            R4CollectUsedTemplateIds(displayedLayout);

        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonTemplatePlacementR4Preview] " +
            "R4 Template First 正式校验通过");

        builder.AppendLine(
            "固定 Seed " + previewFixedSeed +
            "：房间 " +
            primaryLayout.RoomPlacements.Count +
            " | 模板种类 " +
            primaryTemplates.Count);

        builder.AppendLine(
            "相同 Seed 重现：通过 | 改变 Seed：" +
            changedSeed + " 得到不同布局");

        builder.AppendLine(
            "Catalog 同图覆盖：Seed " +
            coverageSeed +
            " 同时包含 " +
            displayedTemplates.Count + "/" +
            requiredCatalogTemplateCount +
            " 个当前可用模板");

        builder.AppendLine(
            "Scene Gizmo 当前显示覆盖用 Seed：" +
            lastPreviewSeed);

        builder.AppendLine(
            "边界、Padding、楼层限制、单层实例上限、" +
            "DungeonLayout 合集：全部通过");

        builder.AppendLine(
            "阶段边界确认：Connections 0 | CorridorCells 0 | " +
            "未实例化 Prefab | 未修改旧 Generate()");

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static string R4BuildFailureReport(
        List<string> errors,
        string generationReport)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[DungeonTemplatePlacementR4Preview] " +
            "R4 正式校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(
                "- " + errors[i]);
        }

        builder.AppendLine();
        builder.Append(generationReport);

        return builder.ToString();
    }

    private static void R4AppendErrors(
        string scope,
        List<string> source,
        List<string> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(
                scope + "：" + source[i]);
        }
    }

    private void Reset()
    {
        dungeonGenerator =
            GetComponent<DungeonGenerator>();
    }

    private void OnDrawGizmosSelected()
    {
        if (dungeonGenerator == null)
        {
            return;
        }

        float safeCellSize =
            Mathf.Max(0.05f, cellSize);

        // 只改变诊断图的显示偏移：把整张地图居中画在本组件周围，
        // 数据中的全局格坐标仍保持 (0,0)～(Map-1)。
        Vector3 origin =
            transform.position -
            new Vector3(
                (dungeonGenerator.TemplateFirstMapWidth - 1) *
                0.5f * safeCellSize,
                (dungeonGenerator.TemplateFirstMapHeight - 1) *
                0.5f * safeCellSize,
                0f);

        if (drawMapBounds)
        {
            R4DrawMapBounds(origin, safeCellSize);
        }

        if (lastPreviewLayout == null)
        {
            return;
        }

        List<Vector2Int> occupiedCells =
            new List<Vector2Int>();

        for (int i = 0;
             i < lastPreviewLayout.RoomPlacements.Count;
             i++)
        {
            DreamRoomPlacement placement =
                lastPreviewLayout.RoomPlacements[i];

            if (placement == null ||
                placement.Template == null)
            {
                continue;
            }

            Color color = R4GetTemplateColor(
                placement.Template.TemplateId);

            if (drawRoomPadding)
            {
                RectInt paddedBounds =
                    placement.GetPaddedBounds(
                        dungeonGenerator.
                            TemplateFirstRoomPadding);

                Color paddingColor = color;
                paddingColor.a = 0.35f;
                Gizmos.color = paddingColor;

                R4DrawWireRect(
                    paddedBounds,
                    origin,
                    safeCellSize);
            }

            Color fillColor = color;
            fillColor.a = 0.18f;
            Gizmos.color = fillColor;

            R4DrawFilledRect(
                placement.CellBounds,
                origin,
                safeCellSize);

            color.a = 0.95f;
            Gizmos.color = color;

            R4DrawWireRect(
                placement.CellBounds,
                origin,
                safeCellSize);

            if (drawOccupiedCells)
            {
                placement.GetOccupiedGlobalCells(
                    occupiedCells);

                Color cellColor = color;
                cellColor.a = 0.38f;
                Gizmos.color = cellColor;

                for (int cellIndex = 0;
                     cellIndex < occupiedCells.Count;
                     cellIndex++)
                {
                    Vector2Int cell =
                        occupiedCells[cellIndex];

                    Gizmos.DrawWireCube(
                        origin +
                        new Vector3(
                            cell.x * safeCellSize,
                            cell.y * safeCellSize,
                            0f),
                        new Vector3(
                            safeCellSize * 0.88f,
                            safeCellSize * 0.88f,
                            0f));
                }
            }
        }

        Gizmos.color = new Color(
            0.3f, 1f, 0.35f, 1f);

        R4DrawMarker(
            lastPreviewLayout.StartCell,
            origin,
            safeCellSize);

        Gizmos.color = new Color(
            1f, 0.3f, 0.8f, 1f);

        R4DrawMarker(
            lastPreviewLayout.ExitCell,
            origin,
            safeCellSize);
    }

    private void R4DrawMapBounds(
        Vector3 origin,
        float safeCellSize)
    {
        RectInt mapBounds = new RectInt(
            0,
            0,
            dungeonGenerator.TemplateFirstMapWidth,
            dungeonGenerator.TemplateFirstMapHeight);

        Gizmos.color = new Color(
            0.85f, 0.9f, 1f, 0.9f);

        R4DrawWireRect(
            mapBounds,
            origin,
            safeCellSize);

        int border =
            dungeonGenerator.TemplateFirstMapBorder;

        RectInt usableBounds = new RectInt(
            border,
            border,
            dungeonGenerator.TemplateFirstMapWidth -
                border * 2,
            dungeonGenerator.TemplateFirstMapHeight -
                border * 2);

        if (usableBounds.width > 0 &&
            usableBounds.height > 0)
        {
            Gizmos.color = new Color(
                1f, 0.8f, 0.25f, 0.7f);

            R4DrawWireRect(
                usableBounds,
                origin,
                safeCellSize);
        }
    }

    private static void R4DrawFilledRect(
        RectInt rect,
        Vector3 origin,
        float cellSize)
    {
        Vector3 center;
        Vector3 size;

        R4GetWorldRect(
            rect,
            origin,
            cellSize,
            out center,
            out size);

        Gizmos.DrawCube(center, size);
    }

    private static void R4DrawWireRect(
        RectInt rect,
        Vector3 origin,
        float cellSize)
    {
        Vector3 center;
        Vector3 size;

        R4GetWorldRect(
            rect,
            origin,
            cellSize,
            out center,
            out size);

        Gizmos.DrawWireCube(center, size);
    }

    private static void R4GetWorldRect(
        RectInt rect,
        Vector3 origin,
        float cellSize,
        out Vector3 center,
        out Vector3 size)
    {
        center = origin +
                 new Vector3(
                     (rect.xMin +
                      (rect.width - 1) * 0.5f) *
                     cellSize,
                     (rect.yMin +
                      (rect.height - 1) * 0.5f) *
                     cellSize,
                     0f);

        size = new Vector3(
            rect.width * cellSize,
            rect.height * cellSize,
            0f);
    }

    private static void R4DrawMarker(
        Vector2Int cell,
        Vector3 origin,
        float cellSize)
    {
        Gizmos.DrawSphere(
            origin +
            new Vector3(
                cell.x * cellSize,
                cell.y * cellSize,
                0f),
            cellSize * 0.35f);
    }

    private static Color R4GetTemplateColor(
        string templateId)
    {
        uint hash = 2166136261u;

        string safeId = templateId ?? string.Empty;

        for (int i = 0; i < safeId.Length; i++)
        {
            hash ^= safeId[i];
            hash *= 16777619u;
        }

        float hue =
            (hash % 360u) / 360f;

        return Color.HSVToRGB(
            hue,
            0.6f,
            0.95f);
    }
}
