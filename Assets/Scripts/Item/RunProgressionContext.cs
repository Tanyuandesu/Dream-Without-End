/// <summary>
/// 建立下一層前廣播給各系統的單局進度。
/// </summary>
public sealed class RunProgressionContext
{
    public int FloorNumber { get; }
    public ItemProgressSnapshot ItemProgress { get; }

    public RunProgressionContext(
        int floorNumber,
        ItemProgressSnapshot itemProgress)
    {
        FloorNumber = floorNumber;
        ItemProgress = itemProgress;
    }
}

/// <summary>
/// 未來 DungeonGenerator、EnemyManager 或房間配置器
/// 可以實作此介面，接收下一層建立前的進度。
/// </summary>
public interface IRunProgressionConsumer
{
    void ApplyRunProgression(
        RunProgressionContext context);
}
