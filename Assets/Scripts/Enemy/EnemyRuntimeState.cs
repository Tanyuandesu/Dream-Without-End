/// <summary>
/// Stable runtime state vocabulary shared by every enemy type.
/// T5A activates Patrol, Chase, InvestigateLastKnownPosition,
/// SearchLastKnownPosition, ReturnToHomeOrPatrol and Dead. T5C activates
/// Alert; T6A/T6B activate Attack for formal melee and projectile profiles.
/// </summary>
public enum EnemyRuntimeState
{
    Spawn = 0,
    Idle = 10,
    Patrol = 20,
    Alert = 30,
    Chase = 40,
    InvestigateLastKnownPosition = 50,
    SearchLastKnownPosition = 60,
    ReturnToHomeOrPatrol = 70,
    Attack = 80,
    Hit = 90,
    Stunned = 100,
    Dead = 110
}
