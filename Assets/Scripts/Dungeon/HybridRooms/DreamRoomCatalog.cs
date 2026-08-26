using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 保存可供混合地牢系统使用的房间模板集合。
///
/// 责任：
/// 1. 集中保存 DreamRoomTemplate Prefab 引用。
/// 2. 按楼层和单层已使用次数筛选候选模板。
/// 3. 按模板自身的 Random Weight 进行加权选择。
/// 4. 在接入生成器前检查空引用、重复引用、重复 Template Id
///    以及可直接从Prefab资产读取的选择规则。
///
/// 本脚本不会实例化房间，也不会修改现有 DungeonGenerator。
/// 房间内部层级必须在Prefab Mode中由DreamRoomTemplate自行校验；
/// Catalog不会对未加载的Prefab资产执行层级遍历。
/// </summary>
[CreateAssetMenu(
    fileName = "DreamRoomCatalog",
    menuName = "Dream Dungeon/Hybrid Rooms/Room Catalog")]
public sealed class DreamRoomCatalog : ScriptableObject
{
    [Header("目录身份")]
    [SerializeField]
    private string catalogId = "Room_Catalog";

    [Header("房间模板 Prefab")]
    [Tooltip(
        "拖入带有 DreamRoomTemplate 组件的 Prefab 资产。" +
        "不要使用同一个 Prefab 两次，也不要让 Template Id 重复。")]
    [SerializeField]
    private List<DreamRoomTemplate> roomTemplates =
        new List<DreamRoomTemplate>();

    [Header("编辑器诊断（不参与正式生成）")]
    [Min(1)]
    [SerializeField]
    private int previewFloorNumber = 1;

    [Tooltip(
        "可选。填写某个 Template Id，用来模拟它已经在当前楼层出现。")]
    [SerializeField]
    private string previewUsedTemplateId = string.Empty;

    [Min(0)]
    [SerializeField]
    private int previewExistingInstances;

    [Min(1)]
    [SerializeField]
    private int previewRollCount = 1000;

    [SerializeField]
    private int previewRandomSeed = 12345;

    public string CatalogId => catalogId;
    public int Count => roomTemplates == null
        ? 0
        : roomTemplates.Count;

    public IReadOnlyList<DreamRoomTemplate> RoomTemplates =>
        roomTemplates;

    /// <summary>
    /// 按 Template Id 查找模板，忽略英文大小写。
    /// </summary>
    public bool TryGetTemplate(
        string templateId,
        out DreamRoomTemplate template)
    {
        template = null;

        if (string.IsNullOrWhiteSpace(templateId) ||
            roomTemplates == null)
        {
            return false;
        }

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate candidate = roomTemplates[i];

            if (candidate != null &&
                string.Equals(
                    candidate.TemplateId,
                    templateId,
                    StringComparison.OrdinalIgnoreCase))
            {
                template = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断模板在指定楼层和当前使用次数下是否仍可作为候选。
    /// currentInstanceCounts 使用 Template Id 作为键；传入 null 表示全部为 0。
    /// </summary>
    public bool IsTemplateEligible(
        DreamRoomTemplate template,
        int floorNumber,
        IReadOnlyDictionary<string, int> currentInstanceCounts)
    {
        if (template == null ||
            !template.CanAppearOnFloor(floorNumber))
        {
            return false;
        }

        int maximumInstances =
            template.MaximumInstancesPerFloor;

        if (maximumInstances == 0)
        {
            return true;
        }

        int currentInstances = GetCurrentInstanceCount(
            template.TemplateId,
            currentInstanceCounts);

        return currentInstances < maximumInstances;
    }

    /// <summary>
    /// 把当前可以使用的模板写入 results。
    /// results 会先被清空，以便生成器重复使用同一个 List，减少垃圾回收。
    /// </summary>
    public void GetEligibleTemplates(
        int floorNumber,
        IReadOnlyDictionary<string, int> currentInstanceCounts,
        List<DreamRoomTemplate> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();

        if (roomTemplates == null)
        {
            return;
        }

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate template = roomTemplates[i];

            if (IsTemplateEligible(
                    template,
                    floorNumber,
                    currentInstanceCounts))
            {
                results.Add(template);
            }
        }
    }

    /// <summary>
    /// 从合格模板中按照 Random Weight 进行一次选择。
    /// System.Random 由调用方传入，未来可用固定种子重现同一张地牢。
    /// </summary>
    public bool TryChooseWeighted(
        int floorNumber,
        IReadOnlyDictionary<string, int> currentInstanceCounts,
        System.Random random,
        out DreamRoomTemplate selectedTemplate)
    {
        selectedTemplate = null;

        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        long totalWeight = 0;

        if (roomTemplates == null)
        {
            return false;
        }

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate candidate = roomTemplates[i];

            if (IsTemplateEligible(
                    candidate,
                    floorNumber,
                    currentInstanceCounts))
            {
                totalWeight += Math.Max(
                    0,
                    candidate.RandomWeight);
            }
        }

        if (totalWeight <= 0)
        {
            return false;
        }

        double roll = random.NextDouble() * totalWeight;
        long cumulativeWeight = 0;

        DreamRoomTemplate lastEligibleTemplate = null;

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate candidate =
                roomTemplates[i];

            if (!IsTemplateEligible(
                    candidate,
                    floorNumber,
                    currentInstanceCounts))
            {
                continue;
            }

            lastEligibleTemplate = candidate;

            cumulativeWeight += Math.Max(
                0,
                candidate.RandomWeight);

            if (roll < cumulativeWeight)
            {
                selectedTemplate = candidate;
                return true;
            }
        }

        // 浮点边界保护。正常情况下循环内已经返回。
        selectedTemplate = lastEligibleTemplate;
        return selectedTemplate != null;
    }

    /// <summary>
    /// 返回会阻止目录进入正式生成流程的配置错误。
    /// </summary>
    public List<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();

        if (string.IsNullOrWhiteSpace(catalogId))
        {
            errors.Add("Catalog Id 不能为空。");
        }

        if (roomTemplates == null ||
            roomTemplates.Count == 0)
        {
            errors.Add("Room Templates 至少需要一个房间模板 Prefab。");
            return errors;
        }

        HashSet<DreamRoomTemplate> usedReferences =
            new HashSet<DreamRoomTemplate>();

        HashSet<string> usedTemplateIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate template = roomTemplates[i];

            if (template == null)
            {
                errors.Add(
                    "Room Templates 的 Element " +
                    i +
                    " 是空引用。");

                continue;
            }

            if (!usedReferences.Add(template))
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 被重复加入目录。");
            }

            if (string.IsNullOrWhiteSpace(
                    template.TemplateId))
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 的 Template Id 不能为空。");
            }
            else if (!usedTemplateIds.Add(
                         template.TemplateId))
            {
                errors.Add(
                    "Template Id '" +
                    template.TemplateId +
                    "' 在目录中重复。");
            }

            if (template.RandomWeight < 1)
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 的 Random Weight 必须至少为 1。");
            }

            if (template.MinimumFloor < 1)
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 的 Minimum Floor 必须至少为 1。");
            }

            if (template.MaximumFloor > 0 &&
                template.MaximumFloor <
                template.MinimumFloor)
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 的 Maximum Floor 不能小于 Minimum Floor。");
            }

            if (template.MaximumInstancesPerFloor < 0)
            {
                errors.Add(
                    "房间 Prefab '" +
                    template.name +
                    "' 的单层最大出现次数不能小于 0。");
            }
        }

        return errors;
    }

    [ContextMenu("Validate Room Catalog")]
    public void ValidateAndLog()
    {
        NormalizeSerializedValues();

        List<string> errors = GetValidationErrors();

        if (errors.Count == 0)
        {
            Debug.Log(
                "[DreamRoomCatalog] 校验通过：" +
                catalogId +
                " | 模板 " +
                roomTemplates.Count,
                this);

            return;
        }

        Debug.LogError(
            BuildErrorReport(errors),
            this);
    }

    /// <summary>
    /// 使用 Inspector 中的诊断参数，显示当前楼层的全部候选模板。
    /// </summary>
    [ContextMenu("Preview Eligible Templates")]
    public void PreviewEligibleTemplatesAndLog()
    {
        NormalizeSerializedValues();

        List<string> errors = GetValidationErrors();

        if (errors.Count > 0)
        {
            Debug.LogError(
                BuildErrorReport(errors),
                this);

            return;
        }

        Dictionary<string, int> previewCounts =
            BuildPreviewInstanceCounts();

        List<DreamRoomTemplate> eligibleTemplates =
            new List<DreamRoomTemplate>();

        GetEligibleTemplates(
            previewFloorNumber,
            previewCounts,
            eligibleTemplates);

        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomCatalog] 候选预览：" +
            catalogId);

        report.AppendLine(
            "楼层：" + previewFloorNumber);

        AppendPreviewUsage(report);

        if (eligibleTemplates.Count == 0)
        {
            report.AppendLine("没有合格模板。");
            Debug.LogWarning(report.ToString(), this);
            return;
        }

        long totalWeight = 0;

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            totalWeight += eligibleTemplates[i].RandomWeight;
        }

        report.AppendLine(
            "合格模板：" + eligibleTemplates.Count);

        report.AppendLine(
            "总权重：" + totalWeight);

        for (int i = 0; i < eligibleTemplates.Count; i++)
        {
            DreamRoomTemplate template =
                eligibleTemplates[i];

            report.Append("- ");
            report.Append(template.TemplateId);
            report.Append(" | 权重 ");
            report.Append(template.RandomWeight);
            report.Append(" | 楼层 ");
            report.Append(FormatFloorRange(template));
            report.Append(" | 单层上限 ");
            report.AppendLine(
                template.MaximumInstancesPerFloor == 0
                    ? "无限制"
                    : template.MaximumInstancesPerFloor.ToString());
        }

        Debug.Log(report.ToString(), this);
    }

    /// <summary>
    /// 使用固定随机种子重复抽取，直观看权重比例是否生效。
    /// 诊断不会累加实例数量；单层上限由 Preview Existing Instances 模拟。
    /// </summary>
    [ContextMenu("Preview Weighted Selection")]
    public void PreviewWeightedSelectionAndLog()
    {
        NormalizeSerializedValues();

        List<string> errors = GetValidationErrors();

        if (errors.Count > 0)
        {
            Debug.LogError(
                BuildErrorReport(errors),
                this);

            return;
        }

        Dictionary<string, int> previewCounts =
            BuildPreviewInstanceCounts();

        System.Random random =
            new System.Random(previewRandomSeed);

        Dictionary<string, int> resultCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        int successfulRolls = 0;

        for (int i = 0; i < previewRollCount; i++)
        {
            DreamRoomTemplate selectedTemplate;

            if (!TryChooseWeighted(
                    previewFloorNumber,
                    previewCounts,
                    random,
                    out selectedTemplate))
            {
                break;
            }

            successfulRolls++;

            int currentCount;
            resultCounts.TryGetValue(
                selectedTemplate.TemplateId,
                out currentCount);

            resultCounts[selectedTemplate.TemplateId] =
                currentCount + 1;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomCatalog] 加权抽取预览：" +
            catalogId);

        report.AppendLine(
            "楼层：" + previewFloorNumber +
            " | 种子：" + previewRandomSeed +
            " | 成功抽取：" + successfulRolls +
            "/" + previewRollCount);

        AppendPreviewUsage(report);

        if (successfulRolls == 0)
        {
            report.AppendLine("没有可供抽取的合格模板。");
            Debug.LogWarning(report.ToString(), this);
            return;
        }

        for (int i = 0; i < roomTemplates.Count; i++)
        {
            DreamRoomTemplate template = roomTemplates[i];

            if (template == null)
            {
                continue;
            }

            int selectedCount;

            if (!resultCounts.TryGetValue(
                    template.TemplateId,
                    out selectedCount))
            {
                continue;
            }

            float percentage =
                selectedCount * 100f / successfulRolls;

            report.Append("- ");
            report.Append(template.TemplateId);
            report.Append("：");
            report.Append(selectedCount);
            report.Append(" 次（");
            report.Append(percentage.ToString("F1"));
            report.AppendLine("%）");
        }

        Debug.Log(report.ToString(), this);
    }

    private void OnValidate()
    {
        NormalizeSerializedValues();
    }

    private void OnEnable()
    {
        NormalizeSerializedValues();
    }

    private void NormalizeSerializedValues()
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            catalogId = name;
        }

        if (roomTemplates == null)
        {
            roomTemplates =
                new List<DreamRoomTemplate>();
        }

        previewFloorNumber =
            Mathf.Max(1, previewFloorNumber);

        previewExistingInstances =
            Mathf.Max(0, previewExistingInstances);

        previewRollCount =
            Mathf.Max(1, previewRollCount);
    }

    private Dictionary<string, int>
        BuildPreviewInstanceCounts()
    {
        Dictionary<string, int> counts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(
                previewUsedTemplateId))
        {
            counts[previewUsedTemplateId] =
                previewExistingInstances;
        }

        return counts;
    }

    private void AppendPreviewUsage(StringBuilder report)
    {
        if (string.IsNullOrWhiteSpace(
                previewUsedTemplateId))
        {
            report.AppendLine(
                "模拟已使用次数：全部为 0");

            return;
        }

        report.AppendLine(
            "模拟已使用次数：" +
            previewUsedTemplateId +
            " = " +
            previewExistingInstances);
    }

    private static int GetCurrentInstanceCount(
        string templateId,
        IReadOnlyDictionary<string, int> currentInstanceCounts)
    {
        if (string.IsNullOrWhiteSpace(templateId) ||
            currentInstanceCounts == null)
        {
            return 0;
        }

        int count;

        if (currentInstanceCounts.TryGetValue(
                templateId,
                out count))
        {
            return Math.Max(0, count);
        }

        // 即使调用方使用区分大小写的 Dictionary，
        // Template Id 的查找仍然保持不区分大小写。
        foreach (KeyValuePair<string, int> pair
                 in currentInstanceCounts)
        {
            if (string.Equals(
                    pair.Key,
                    templateId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0, pair.Value);
            }
        }

        return 0;
    }

    private string BuildErrorReport(List<string> errors)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomCatalog] 校验失败：" +
            catalogId);

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        return report.ToString();
    }

    private static string FormatFloorRange(
        DreamRoomTemplate template)
    {
        if (template.MaximumFloor == 0)
        {
            return template.MinimumFloor + "～无限制";
        }

        return template.MinimumFloor +
               "～" +
               template.MaximumFloor;
    }
}
