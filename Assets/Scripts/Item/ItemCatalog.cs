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
#endif
}
