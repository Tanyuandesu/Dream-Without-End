/// <summary>
/// Credit used by run statistics and future ending checks.
/// It is intentionally separate from the target/source faction so future
/// player-owned traps can still grant player credit.
/// </summary>
public enum DamageAttribution
{
    Unspecified = 0,
    Player = 1,
    Enemy = 2,
    Environment = 3,
    Other = 4
}
