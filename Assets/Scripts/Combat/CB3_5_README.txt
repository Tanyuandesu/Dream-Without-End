Dream Dungeon Combat CB3.5
==========================

CB3.5 changes only the player-side direction source for the existing
left-mouse nonlethal push.

Direction contract
------------------
- Left mouse still triggers the action.
- Mouse world position no longer selects the attack direction.
- RuntimeDungeonPlayer owns an authoritative eight-direction facing.
- Non-zero movement input updates that facing.
- No movement input preserves the last facing.
- PlayerCombatController refreshes movement input immediately before a valid
  action, so a direction change and click in the same rendered frame use the
  new facing regardless of Unity Update execution order.
- One action snapshots one facing. Turning after the action starts cannot bend
  the already-issued fan.
- DirectionalSpriteAnimator is synchronized to the snapped action facing, but
  combat never reads animation state as gameplay authority.

Default fan
-----------
- Range: 1.35
- Total angle: 100 degrees
- Centre: current eight-direction facing

Preserved systems
-----------------
- CB1 zero damage, line-of-sight and collision-safe EnemyMotor2D displacement
- CB2 per-enemy multi-tier decay and stepped recovery
- CB3 0.55 second cooldown and 0.18 second steerable movement afterlag
- CB4 pursuit recovery remains disabled
- Right mouse damage attack remains disabled
