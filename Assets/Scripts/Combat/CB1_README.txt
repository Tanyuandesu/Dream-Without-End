Dream Dungeon Combat CB1 - Left-Mouse Nonlethal Push

Scope
-----
CB1 activates only the player's left-mouse push.

Implemented
-----------
1. Mouse world-space aim and a configurable near-range fan query.
2. Zero damage. Health and kill attribution are not changed by the push.
3. Solid-geometry line-of-sight prevents pushing through walls.
4. EnemyCombatReceiver routes the hit to EnemyStateMachine and EnemyMotor2D.
5. EnemyMotor2D remains the only Rigidbody2D movement owner.
6. Collider2D shape casting clips displacement against static/kinematic walls.
7. Hit reaction suspends navigation; reaction completion requests a fresh route
   from the enemy's new position through the existing EA3 state/navigation flow.
8. One attack id is shared by all targets and duplicate collider hits are rejected.

Intentionally not implemented in CB1
------------------------------------
- Right-mouse damage attack.
- Knockback decay tier execution.
- Post-knockback pursuit speed bonus.
- Final left-push cooldown and player recovery/backswing.
- Combat animation, sound or visual effects.

Default CB1 push values
-----------------------
Range: 1.35 world units
Fan angle: 120 degrees
Requested displacement: 0.90 world units
Displacement duration: 0.18 seconds
Reaction duration: 0.22 seconds
Maximum targets: 8

Authoring location
------------------
Select the GameScene object that owns PlayerSpawner. The new
"CB1 左键无伤害击退" foldout stores the values above.

Runtime audit
-------------
Tools > Dream Dungeon > Combat > Run CB1 Runtime Audit

Run in Play Mode after player/enemy generation. For an exercised result, aim
at a nearby enemy, left-click, wait a moment and run the audit. Test in open
space, facing a wall, at a doorway and in a narrow corridor.
