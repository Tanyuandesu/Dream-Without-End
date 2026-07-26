Dream Dungeon Combat CB2
========================

Scope
-----
CB2 activates the per-enemy repeated-push resistance data established in CB0.
It remains cumulative with CB0 and CB1.

Active behaviour
----------------
- The first accepted left-mouse push uses full distance and full reaction time.
- A later accepted push inside Decay Build Window advances one resistance level.
- Every EnemyDefinition owns at least three independently editable decay tiers.
- Distance Multiplier and Stagger Multiplier are evaluated separately.
- Resistance never wraps. Hits above the final level continue using the final tier.
- After Recovery Delay, resistance falls one level per Recovery Step Interval.
- A new accepted qualifying push restarts the recovery delay.
- Reduced distance also shortens travel duration proportionally, preserving push speed.
- EnemyMotor2D remains the only component allowed to move the enemy Rigidbody2D.

Still inactive
--------------
- post-knockback pursuit speed bonus
- final left-push cooldown
- player recovery / end-lag
- right-mouse damaging attack
- combat animation, VFX and SFX

Runtime verification
--------------------
1. Enter Play Mode and wait for one player and at least one enemy.
2. In open ground, push the same enemy at least four accepted times.
   Keep every gap below that enemy's Decay Build Window, default 0.9 seconds.
3. The four accepted pushes should resolve as:
   full strength -> decay level 1 -> level 2 -> level 3.
4. Stop pushing for about four seconds with the default settings.
5. Run:
   Tools > Dream Dungeon > Combat > Run CB2 Decay Audit
6. PASS requires both a level-3 accumulation observation and at least one timed
   recovery step in the same Play Mode run.

Default shared starting values
------------------------------
Full strength: distance 1.00, stagger 1.00
Decay level 1: distance 0.75, stagger 0.60
Decay level 2: distance 0.50, stagger 0.25
Decay level 3: distance 0.30, stagger 0.00
Build window: 0.90 seconds
Recovery delay: 1.50 seconds
Recovery step interval: 0.60 seconds

These are only neutral starting values. Every enemy asset stores its own copy and
can be tuned independently in later balance work.
