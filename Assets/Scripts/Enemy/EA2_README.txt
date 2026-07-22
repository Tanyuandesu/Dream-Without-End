Dream Dungeon Enemy System - EA2
Date: 2026-07-22

EXPECTED RESULT

- GameScene still spawns three visually identical Wanderers.
- Their speed, detection, contact damage, collision and CA1 animation remain
  identical to the accepted EA1 baseline.
- EA2 does not activate Scout, Hunter, Brute or Gazer.
- EA2 does not add patrol, area search, return-home or active attacks.

NEW RUNTIME STRUCTURE

Every generated enemy now has:

- EnemyRuntimeIdentity: immutable ID and spawn provenance from EA1.
- EnemyRuntimeContext: shared references, Home Anchor, current target, last
  known target position and current navigation destination.
- EnemyStateMachine: observable lifecycle and state transitions.
- TestEnemyAI: a temporary EA2 compatibility adapter that preserves the old
  path request and movement implementation. EA3 will replace this navigation
  path without changing the state contract.

EA2 activates only these states:

Spawn -> Idle -> Chase -> InvestigateLastKnownPosition -> Idle
                                      \-> Chase when target is reacquired
Any live state -> Dead when Health reaches zero

The longer future state vocabulary is present for stable integrations, but
Patrol, Alert, SearchLastKnownPosition, ReturnToHomeOrPatrol, Attack, Hit and
Stunned are intentionally not entered in EA2.

INSPECTOR DEBUGGING

During Play Mode, expand GeneratedDungeon_Floor_1 and select Wanderer_1.

- Enemy Runtime Context shows Enemy Id, Current Target, Home Anchor, Last
  Known Target Position and Navigation Destination.
- Enemy State Machine shows Current State, Previous State, transition count,
  state-entered time and the most recent transition reason.
- Test Enemy AI shows the temporary path snapshot and queued destination.
- Log State Transitions is off by default. If manually enabled during Play
  Mode it logs only actual changes, never one message per frame.

AUDITS

1. Open Assets/Scenes/GameScene.unity and wait for compilation.
2. Outside Play Mode, rerun:
   Tools > Dream Dungeon > Enemy System > Run EA1 Configuration Audit
3. Enter Play Mode and wait until all three enemies have spawned.
4. Run:
   Tools > Dream Dungeon > Enemy System > Run EA2 Runtime Audit
5. Console must show RuntimeEnemies=3, InitializedContexts=3,
   InitializedStateMachines=3, LegacyChaseAdapters=3,
   EnemyManagerActive=3 and Result=PASS.

PLAY MODE REGRESSION

1. Before an enemy detects the player, its state is Idle.
2. Move within detection range: state becomes Chase and the old A* movement
   continues to follow the player.
3. Move beyond the lose radius or break configured line of sight: state becomes
   InvestigateLastKnownPosition and the enemy finishes travelling to the last
   observed position.
4. When that position is reached without reacquiring the player, state returns
   to Idle. This is not the final EA4 area-search behaviour yet.
5. Confirm contact damage and the circular HP display still work.
6. Confirm the temporary cat still plays Idle/Walk from actual root movement.
7. Enter the next floor and press R once. Exactly three fresh enemies should
   exist, each with one initialized context and state machine.
8. Console must have no new red Error.

DEATH AND MANAGER CONTRACT

EnemyStateMachine subscribes to Health.Died before EnemyManager registration.
It enters Dead and clears movement intent; EnemyManager then records the death
and removes the object from ActiveEnemies exactly as in EA1. The object is
still destroyed by Health at the end of the frame. Death animation delay is
not introduced in EA2.

ROLLBACK

Restore the EA1 versions of EnemySpawner.cs and TestEnemyAI.cs. The new EA2
scripts may remain as unreferenced files without changing runtime behaviour,
or may be removed while Unity is closed. Use the supplied EA2 rollback archive
or restore the full project backup.
