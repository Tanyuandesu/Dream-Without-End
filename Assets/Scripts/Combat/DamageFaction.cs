using System;

/// <summary>
/// 受到傷害的陣營。
/// </summary>
public enum DamageFaction
{
    Neutral = 0,
    Player = 1,
    Enemy = 2,
    Environment = 3
}

/// <summary>
/// 用於傷害器選擇可以攻擊哪些陣營。
/// </summary>
[Flags]
public enum DamageFactionMask
{
    None = 0,
    Neutral = 1 << 0,
    Player = 1 << 1,
    Enemy = 1 << 2,
    Environment = 1 << 3,
    Everything = ~0
}

public static class DamageFactionMaskExtensions
{
    public static bool Contains(
        this DamageFactionMask mask,
        DamageFaction faction)
    {
        int factionBit = 1 << (int)faction;

        return (mask & (DamageFactionMask)factionBit) != 0;
    }
}
