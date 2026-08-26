Dream Dungeon Combat CB4
========================

CB4 completes the four-part nonlethal-push balance loop.

Ordered runtime sequence
------------------------
1. Player starts the facing-centred zero-damage push.
2. EnemyMotor2D performs collision-safe displacement.
3. After displacement ends or is wall-clipped, EnemyStateMachine begins the
   per-enemy post-knockback pause.
4. The current CB2 decay tier scales that pause through Stagger Multiplier.
5. When the pause ends, navigation resumes and EnemyMotor2D applies the
   per-enemy temporary pursuit speed multiplier.
6. The multiplier expires and navigation returns to normal speed.

Configuration ownership
-----------------------
PlayerSpawner / NonlethalPushSettings:
- range
- fan angle
- displacement distance and duration
- cooldown
- player afterlag and movement multiplier
- maximum targets

Each EnemyDefinition:
- base post-knockback pause
- pursuit speed multiplier
- pursuit boost duration
- repeated-push build/recovery windows
- independent distance and post-pause multipliers for at least three tiers

No right-mouse damage attack, combat animation, sound or VFX is added in CB4.
