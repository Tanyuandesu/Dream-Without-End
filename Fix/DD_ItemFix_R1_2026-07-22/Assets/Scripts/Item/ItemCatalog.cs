using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本局可出現的核心道具清單。
/// </summary>
[CreateAssetMenu(
    fileName = "ItemCatalog",
    menuName = "Game/Items/Item Catalog")]
public sealed class ItemCatalog : ScriptableObject
{
    [Header("第一個固定道具")]
    [SerializeField] private ItemDefinition firstGuaranteedItem;

    [Header("後續隨機池")]
    [SerializeField] private List<ItemDefinition> subsequentItems =
        new List<ItemDefinition>();

    public ItemDefinition FirstGuaranteedItem =>
        firstGuaranteedItem;

    public IReadOnlyList<ItemDefinition> SubsequentItems =>
        subsequentItems;

    /// <summary>
    /// 檢查 Catalog 本身，以及目前通關門檻是否真的可達成。
    /// 這裡只檢查資料容量，不改動任何道具內容。
    /// </summary>
    public List<string> GetProgressionValidationErrors(
        int requiredValue,
        bool useProgressionScore)
    {
        List<string> errors =
            GetCatalogValidationErrors();

        if (requiredValue < 1)
        {
            errors.Add(
                "Victory requirement must be at least 1.");

            return errors;
        }

        int maximumValue =
            CalculateMaximumProgressionValue(
                useProgressionScore);

        ValidateUniqueInRunRequirements(errors);

        if (maximumValue < requiredValue)
        {
            string valueLabel =
                useProgressionScore
                    ? "progression score"
                    : "collected items";

            errors.Add(
                "Victory requires " +
                requiredValue + " " + valueLabel +
                ", but the catalog contains at most " +
                maximumValue +
                " from distinct configured Item IDs.");
        }

        return errors;
    }

    /// <summary>
    /// 檢查空引用、空 Item ID 與重複 Item ID。
    /// Item ID 是之後文本、存檔與台詞查找的穩定鍵。
    /// </summary>
    public List<string> GetCatalogValidationErrors()
    {
        List<string> errors =
            new List<string>();

        HashSet<string> itemIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        ValidateDefinition(
            firstGuaranteedItem,
            "First Guaranteed Item",
            itemIds,
            errors);

        if (subsequentItems == null)
        {
            errors.Add(
                "Subsequent Items list is null.");

            return errors;
        }

        for (int i = 0; i < subsequentItems.Count; i++)
        {
            ValidateDefinition(
                subsequentItems[i],
                "Subsequent Items[" + i + "]",
                itemIds,
                errors);
        }

        return errors;
    }

    public ItemDefinition FindById(string itemId)
    {
        if (firstGuaranteedItem != null &&
            firstGuaranteedItem.ItemId == itemId)
        {
            return firstGuaranteedItem;
        }

        for (int i = 0; i < subsequentItems.Count; i++)
        {
            ItemDefinition definition =
                subsequentItems[i];

            if (definition != null &&
                definition.ItemId == itemId)
            {
                return definition;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        ItemDefinition firstItem,
        List<ItemDefinition> laterItems)
    {
        firstGuaranteedItem = firstItem;
        subsequentItems =
            laterItems ?? new List<ItemDefinition>();
    }

    /// <summary>
    /// 只補上缺少的生成測試道具，不清空或覆蓋既有 Catalog。
    /// 正式貼圖、文本或額外道具已配置後仍可安全重跑生成器。
    /// </summary>
    public bool EnsureContainsForEditor(
        ItemDefinition firstItem,
        IReadOnlyList<ItemDefinition> laterItems)
    {
        bool changed = false;

        if (firstGuaranteedItem == null &&
            firstItem != null)
        {
            firstGuaranteedItem = firstItem;
            changed = true;
        }

        if (subsequentItems == null)
        {
            subsequentItems =
                new List<ItemDefinition>();

            changed = true;
        }

        if (laterItems == null)
        {
            return changed;
        }

        for (int i = 0; i < laterItems.Count; i++)
        {
            ItemDefinition definition =
                laterItems[i];

            if (definition == null ||
                definition == firstGuaranteedItem ||
                subsequentItems.Contains(definition))
            {
                continue;
            }

            subsequentItems.Add(definition);
            changed = true;
        }

        return changed;
    }
#endif

    private int CalculateMaximumProgressionValue(
        bool useProgressionScore)
    {
        HashSet<string> countedIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int maximumValue = 0;

        if (IsDefinitionCountable(firstGuaranteedItem) &&
            countedIds.Add(firstGuaranteedItem.ItemId))
        {
            maximumValue +=
                useProgressionScore
                    ? Mathf.Max(
                        0,
                        firstGuaranteedItem.ProgressionValue)
                    : 1;
        }

        if (subsequentItems == null)
        {
            return maximumValue;
        }

        for (int i = 0; i < subsequentItems.Count; i++)
        {
            ItemDefinition definition =
                subsequentItems[i];

            if (!IsDefinitionCountable(definition) ||
                !countedIds.Add(definition.ItemId))
            {
                continue;
            }

            maximumValue +=
                useProgressionScore
                    ? Mathf.Max(
                        0,
                        definition.ProgressionValue)
                    : 1;
        }

        return maximumValue;
    }

    private void ValidateUniqueInRunRequirements(
        List<string> errors)
    {
        ValidateUniqueInRun(
            firstGuaranteedItem,
            "First Guaranteed Item",
            errors);

        if (subsequentItems == null)
        {
            return;
        }

        for (int i = 0; i < subsequentItems.Count; i++)
        {
            ValidateUniqueInRun(
                subsequentItems[i],
                "Subsequent Items[" + i + "]",
                errors);
        }
    }

    private static void ValidateUniqueInRun(
        ItemDefinition definition,
        string location,
        List<string> errors)
    {
        if (definition == null ||
            definition.UniqueInRun)
        {
            return;
        }

        errors.Add(
            location + " ('" + definition.ItemId +
            "') must enable Unique In Run. Repeatable core items " +
            "must not be used to satisfy the victory requirement.");
    }

    private static bool IsDefinitionCountable(
        ItemDefinition definition)
    {
        return definition != null &&
               !string.IsNullOrWhiteSpace(
                   definition.ItemId);
    }

    private static void ValidateDefinition(
        ItemDefinition definition,
        string location,
        HashSet<string> itemIds,
        List<string> errors)
    {
        if (definition == null)
        {
            errors.Add(location + " is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.ItemId))
        {
            errors.Add(
                location + " (" + definition.name +
                ") has an empty Item ID.");

            return;
        }

        if (!itemIds.Add(definition.ItemId))
        {
            errors.Add(
                "Duplicate Item ID '" +
                definition.ItemId + "' at " + location + ".");
        }
    }
}
