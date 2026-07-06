using UnityEngine;

/// <summary>
/// 道具收集事件資料。
/// 正式 UI、文本、音效、存檔與任務系統都可以訂閱。
/// </summary>
public sealed class ItemCollectedEvent
{
    public ItemDefinition Definition { get; }
    public int FloorNumber { get; }
    public GameObject Collector { get; }
    public int TotalCollected { get; }
    public int ProgressionScore { get; }

    public ItemCollectedEvent(
        ItemDefinition definition,
        int floorNumber,
        GameObject collector,
        int totalCollected,
        int progressionScore)
    {
        Definition = definition;
        FloorNumber = floorNumber;
        Collector = collector;
        TotalCollected = totalCollected;
        ProgressionScore = progressionScore;
    }
}
