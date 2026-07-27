Dream Dungeon Combat CB6 - Death Lifecycle and Run Statistics
Date: 2026-07-27

CB6 preserves CB0-CB5 and formalizes the direct-attack death chain.

Runtime order:
1. Health accepts the player-attributed lethal hit.
2. EnemyStateMachine enters Dead and cancels displacement/boost/reaction.
3. EnemyDeathLifecycle immediately disables contact damage, colliders,
   Rigidbody2D simulation and remaining AI update components.
4. EnemyManager records the attribution and removes the object from its
   active list.
5. Health destroys the enemy GameObject at the end of the frame.

EnemyManager diagnostics expose registered/dead/unregistered totals, floor
setup/clear observations and the last attribution. EnemyRunRecord remains the
authoritative memory-only source for no-player-kill and all-player-killed
ending queries.

Runtime audit:
Tools > Dream Dungeon > Combat > Run CB6 Death Lifecycle Audit

Recommended observation:
- Kill at least one enemy with right mouse/K/X.
- Leave at least one enemy alive if convenient.
- Enter the next floor once.
- Run the audit there.

The audit accepts zero survivors as well, but requires both a player kill and
a floor transition so death cleanup, survivor finalization and new-floor
active references are observed in one run.
