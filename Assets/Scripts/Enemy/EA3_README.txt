Dream Dungeon Enemy System - EA3
Date: 2026-07-22

EXPECTED RESULT

- GameScene still spawns three visually identical Wanderers.
- Enemy speed, detection, contact damage, collision and CA1 animation remain
  compatible with the accepted EA2 baseline.
- EA3 does not activate Scout, Hunter, Brute or Gazer.
- EA3 does not add patrol, area search, return-home or active attacks.

EA3 NAVIGATION STACK

One EnemyPathService is created on each generated floor root. It owns the
FloorCells copy, four-direction topology, connectivity precheck, binary-heap
A*, request queue and a per-frame query budget.

Every generated enemy now has:

- EnemyNavigationAgent: destination changes, throttled requests, waypoint
  following, failure state and stuck recovery.
- EnemyMotor2D: the only component that issues Rigidbody2D movement.
- EnemyPathfinder: a compatibility facade that delegates all queries to the
  shared service; it no longer contains A*.
- TestEnemyAI: a compatibility bridge with no Update, FixedUpdate, A* or
  movement implementation.

The only runtime loops involved in EA3 navigation are:

- EnemyStateMachine.Update / FixedUpdate: state and fixed-step agent tick.
- EnemyPathService.Update: centrally budgeted path queries.

NAVIGATION RULES

- Topology is FourDirections.
- Eight-direction character animation remains independent.
- Collinear waypoint simplification is disabled; each cell-center turn remains
  explicit for corner and non-rectangular-room verification.
- Runtime path requests are limited to two per frame by default.
- Each enemy only requests again when the target changes grid cell, a safe
  waypoint boundary is reached and its minimum repath interval has elapsed.
- A disconnected or invalid target produces a visible failure reason and is
  retried after a configured delay; an empty failed path cannot silently stop
  the agent forever.
- If expected movement makes no progress, the agent clears the stale path,
  recenters only within a bounded recovery distance and requests again. After
  repeated failure it exposes RecoveryAttemptsExhausted and continues delayed
  retries instead of logging every frame.

The existing EnemyDefinition.MaximumChasePathCost remains reserved for EA4.
EA3 passes an unlimited chase-cost value so the accepted EA2 chase reach is not
silently reduced during the navigation replacement.

AUDITS

Outside Play Mode:

1. Tools > Dream Dungeon > Enemy System > Run EA1 Configuration Audit
2. Tools > Dream Dungeon > Enemy System > Run EA3 Algorithm Audit

The EA3 algorithm audit covers eight deterministic cases: straight path,
same-cell success, non-rectangular multi-turn path, priority-queue shortest
detour, disconnected target, path-cost rejection, off-grid start recovery and
eight-direction corner-cut prevention. Expected: CasesPassed=8/8, Result=PASS.

In Play Mode, after the floor and three enemies exist:

1. Tools > Dream Dungeon > Enemy System > Run EA2 Runtime Audit
2. Tools > Dream Dungeon > Enemy System > Run EA3 Navigation Audit

The EA2 audit remains valid because TestEnemyAI now acts as a compatibility
bridge. The EA3 audit expects three initialized agents, motors, pathfinder
facades and bridges; one shared path service; FourDirections; one connected
floor component; three successful immediate connectivity probes; no active
navigation failure; and EnemyManagerActive=3.

INSPECTOR DEBUGGING

- Select GeneratedDungeon_Floor_1 to inspect Enemy Path Service queue,
  connectivity and aggregate query counters.
- Select a Wanderer to inspect Enemy Navigation Agent request, path, waypoint,
  failure, stuck and recovery snapshots plus Enemy Motor 2D commands.
- Path and runtime Gizmos can be disabled independently.
- No path request emits a normal per-frame log.

ROLLBACK

Restore the EA2 versions of EnemySpawner.cs, TestEnemyAI.cs,
EnemyStateMachine.cs and EnemyRuntimeContext.cs, plus the pre-EA3 versions of
EnemyPathfinder.cs and EnemyDefinition.cs. The new EA3 scripts are deliberately
compile-safe and unreferenced after that restore, so they may remain. For a
file-level cleanup, remove every EA3-added script and matching .meta as one set
while Unity is closed.
