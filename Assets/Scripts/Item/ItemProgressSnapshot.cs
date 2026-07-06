using System.Collections.Generic;

/// <summary>
/// 某一時刻的道具進度快照。
///
/// 地圖、敵人、難度與 UI 應讀取快照，
/// 不要直接修改 ItemManager 內部資料。
/// </summary>
public sealed class ItemProgressSnapshot
{
    private readonly List<ItemDefinition> collectedItems;

    public int CollectedCount { get; }
    public int ProgressionScore { get; }
    public int LastCollectedFloor { get; }

    public IReadOnlyList<ItemDefinition> CollectedItems =>
        collectedItems;

    public bool HasCollectedAnyItem =>
        CollectedCount > 0;

    public ItemProgressSnapshot(
        List<ItemDefinition> sourceItems,
        int progressionScore,
        int lastCollectedFloor)
    {
        collectedItems = sourceItems != null
            ? new List<ItemDefinition>(sourceItems)
            : new List<ItemDefinition>();

        CollectedCount = collectedItems.Count;
        ProgressionScore = progressionScore;
        LastCollectedFloor = lastCollectedFloor;
    }

    public bool HasCollected(string itemId)
    {
        for (int i = 0; i < collectedItems.Count; i++)
        {
            ItemDefinition item = collectedItems[i];

            if (item != null &&
                item.ItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }

    public int CountWithTag(string tag)
    {
        int count = 0;

        for (int i = 0; i < collectedItems.Count; i++)
        {
            ItemDefinition item = collectedItems[i];

            if (item != null &&
                item.HasTag(tag))
            {
                count++;
            }
        }

        return count;
    }
}
