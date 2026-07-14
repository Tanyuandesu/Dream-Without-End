/// <summary>
/// 地牢的可视化生成方式。
/// Phase 0 只正式启用 ProceduralCells；
/// HybridPrefabRooms 是后续混合房间系统的稳定入口。
/// </summary>
public enum DungeonRenderMode
{
    ProceduralCells = 0,
    HybridPrefabRooms = 1
}
