Enemy System EA2 / T7 Authority Note

T7A removed the retired TestEnemyAI compatibility bridge.
Runtime enemies now use one authority path only:

EnemyStateMachine
  -> EnemyNavigationAgent
  -> EnemyPathService / EnemyPathfinder
  -> EnemyMotor2D

Use Tools/Dream Dungeon/Enemy System/Run EA2 Runtime Audit in Play Mode.
The report must show Result=PASS and T7AuthoritativeNavigationAgents matching
RuntimeEnemies.
