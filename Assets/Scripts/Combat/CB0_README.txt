DREAM DUNGEON COMBAT CB0
========================

Historical contract layer
-------------------------
CB0 established combat ownership and data contracts without activating player
attacks. CB1 and later phases build on these boundaries and may now activate
input through them.

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
6. PlayerCombatController: automatically created by PlayerSpawner.
7. CombatCB0ContractAudit:
   Tools > Dream Dungeon > Combat > Run CB0 Contract Audit

After CB1 installation
----------------------
- The left mouse action is activated by CB1 through the CB0 contracts.
- The right mouse action remains inactive.
- The CB0 audit continues to verify ownership and wiring but no longer requires
  combat input to remain disabled.
