using System;

/// <summary>
/// R7.1 的正式 Hybrid 运行时入口。
///
/// 这里只负责沿用旧生成器的种子规则，并把请求转交给已经通过 R6
/// 验证的 Socket Corridor 生成器；不会复制 R4、R5 或 R6 算法。
/// </summary>
public sealed partial class DungeonGenerator
{
    /// <summary>
    /// 为正式 Play Mode 生成一份完整的 R6 Hybrid Layout。
    ///
    /// 固定种子与旧 Generate(int) 使用相同的逐层规则；随机种子只在
    /// 本次调用开始时计算一次，并作为 R6 的 External Seed 使用。
    /// </summary>
    public bool TryGenerateHybridRuntimeLayout(
        int floorNumber,
        out DungeonLayout layout,
        out string report)
    {
        int seed = useRandomSeed
            ? unchecked(
                Environment.TickCount ^
                (floorNumber * 73856093))
            : fixedSeed + floorNumber - 1;

        return TryGenerateSocketCorridorLayout(
            floorNumber,
            seed,
            out layout,
            out report);
    }
}
