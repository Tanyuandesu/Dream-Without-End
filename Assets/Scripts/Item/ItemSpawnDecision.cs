/// <summary>
/// 某層的道具刷新判定。
/// 可供 Debug UI 或後續統計系統使用。
/// </summary>
public sealed class ItemSpawnDecision
{
    public int FloorNumber { get; }
    public float Chance { get; }
    public float Roll { get; }
    public bool ShouldSpawn { get; }
    public ItemDefinition Definition { get; }

    public ItemSpawnDecision(
        int floorNumber,
        float chance,
        float roll,
        bool shouldSpawn,
        ItemDefinition definition)
    {
        FloorNumber = floorNumber;
        Chance = chance;
        Roll = roll;
        ShouldSpawn = shouldSpawn;
        Definition = definition;
    }
}
