using UnityEngine;

/// <summary>
/// 一次傷害事件攜帶的資料。
///
/// 現在只使用 Amount；HitPoint、Direction、Source
/// 可供之後的擊退、受擊特效、音效與傷害來源統計使用。
/// </summary>
public struct DamageInfo
{
    public float Amount;
    public GameObject Source;
    public DamageFaction SourceFaction;
    public Vector2 HitPoint;
    public Vector2 Direction;

    public DamageInfo(
        float amount,
        GameObject source,
        DamageFaction sourceFaction,
        Vector2 hitPoint,
        Vector2 direction)
    {
        Amount = amount;
        Source = source;
        SourceFaction = sourceFaction;
        HitPoint = hitPoint;
        Direction = direction;
    }
}
