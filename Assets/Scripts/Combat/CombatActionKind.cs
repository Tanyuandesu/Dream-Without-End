/// <summary>
/// Stable vocabulary for the source action that produced a CombatHit.
/// It separates player intent from damage, displacement and reaction data.
/// </summary>
public enum CombatActionKind
{
    Unspecified = 0,
    NonlethalPush = 10,
    DirectAttack = 20,
    EnemyAttack = 30,
    Environment = 40
}
