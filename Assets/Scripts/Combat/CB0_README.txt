DREAM DUNGEON COMBAT CB0
========================

Purpose
-------
CB0 establishes combat ownership and data contracts without activating player
attacks or changing the accepted movement/AI hand feel.

Installed boundaries
--------------------
1. CombatAttackId: one action id shared by every target hit by that action.
2. CombatHit: independent damage, displacement and reaction payloads.
3. CombatDisplacementRequest: callers request movement; EnemyMotor2D remains
   the only component allowed to move the enemy Rigidbody2D.
4. CombatReactionRequest: EnemyStateMachine formally supports Hit/Stunned and
   resumes perception/navigation from the post-reaction position.
5. KnockbackResistanceSettings: every EnemyDefinition owns at least three
   configurable repeated-push decay tiers, with independent distance and
   stagger multipliers plus tier recovery timing.
6. PlayerCombatController: automatically created by PlayerSpawner, initialized
   but intentionally leaves combat input disabled in CB0.
7. CombatCB0ContractAudit:
   Tools > Dream Dungeon > Combat > Run CB0 Contract Audit

Expected baseline behaviour
---------------------------
- Left mouse: unchanged / no combat action yet.
- Right mouse: unchanged / no combat action yet.
- Enemy navigation and contact damage: unchanged.
- No current runtime system calls TryBeginCombatDisplacement or
  TryBeginCombatReaction. CB1 will connect the nonlethal push to these entries
  after adding wall-safe displacement.

CB0 acceptance
--------------
1. Project compiles with no red Console errors.
2. Existing floor generation and enemy chase still behave as before.
3. In Play Mode, run the CB0 Contract Audit after enemies spawn.
4. Audit prints PASS and confirms combat input remains disabled.
