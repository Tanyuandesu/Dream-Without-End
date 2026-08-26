Dream Dungeon Combat CB3
========================

Scope
-----
CB3 activates the two player-side rhythm controls from the four-part balance
plan. CB0, CB1 and CB2 remain cumulative and unchanged.

Active behaviour
----------------
- Every accepted left-push action starts a configurable 0.55-second cooldown.
- A missed push still consumes cooldown because the player performed the action.
- Every action also starts a configurable 0.18-second recovery window.
- During recovery, RuntimeDungeonPlayer keeps reading steering input but moves at
  a configurable 0.45 multiplier instead of being fully locked.
- RuntimeDungeonPlayer remains the sole owner of player Rigidbody2D movement.
- Inputs during recovery or cooldown are counted and rejected without issuing an
  attack id, querying targets or changing enemy resistance.
- CB2 per-enemy multi-level decay and stepped recovery remain active.

Still inactive
--------------
- post-knockback pursuit speed bonus
- right-mouse damaging attack
- attack and hurt animation
- combat VFX and SFX

Runtime verification
--------------------
1. Enter Play Mode and wait for one player and at least one enemy.
2. Hold a movement key and successfully push an enemy once. Movement should feel
   briefly weighted, not completely frozen, and steering should remain possible.
3. Click several times rapidly. Extra clicks should not create extra pushes.
4. Wait beyond 0.55 seconds and successfully push again.
5. Wait at least 0.25 seconds after the final action.
6. Run:
   Tools > Dream Dungeon > Combat > Run CB3 Action Timing Audit
7. PASS requires at least two started actions, at least one timing rejection, at
   least one completed movement recovery and one successful enemy push.
