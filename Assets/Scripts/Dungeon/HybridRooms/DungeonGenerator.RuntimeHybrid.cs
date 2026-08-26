using System;

/// <summary>
/// R7.1 的正式 Hybrid 运行时入口；R8.2 在 R6 成功后，
/// 再把 Player／Exit Spawn Cell 写入最终 DungeonLayout。
///
/// 这里只负责沿用旧生成器的种子规则，并把请求转交给已经通过 R6
/// 验证的 Socket Corridor 生成器；不会复制 R4、R5 或 R6 算法。
/// </summary>
public sealed partial class DungeonGenerator
{
    /// <summary>
    /// 为正式 Play Mode 生成一份完整的 Hybrid Layout。
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

        DungeonLayout r6Layout;
        string r6Report;

        if (!TryGenerateSocketCorridorLayout(
                floorNumber,
                seed,
                out r6Layout,
                out r6Report))
        {
            layout = null;
            report = r6Report;
            return false;
        }

        DungeonLayout authorityLayout;
        string authorityReport;

        if (!P1012R2BTryApplyControlledMedium13x9Authority(
                r6Layout,
                out authorityLayout,
                out authorityReport))
        {
            layout = null;
            report = authorityReport + "\n" + r6Report;
            return false;
        }

        DungeonLayout r82Layout;
        string r82Report;

        if (!R82TryApplyPlayerAndExitSpawnCells(
                authorityLayout,
                out r82Layout,
                out r82Report))
        {
            layout = null;
            report = authorityReport + "\n" + r6Report + "\n" + r82Report;
            return false;
        }

        layout = r82Layout;
        // 让正常 Console 的首行直接显示 R8.2 的 Requested／Effective
        // Spawn Source；完整 R6 报告仍保留在同一条多行日志下方。
        report = r82Report + "\n" + authorityReport + "\n" + r6Report;
        return true;
    }
}
