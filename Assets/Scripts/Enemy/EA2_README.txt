Enemy System EA2 / T7C Runtime Authority

Runtime enemies use one authority path only:
EnemyStateMachine
  -> EnemyNavigationAgent
  -> EnemyPathService / EnemyPathfinder
  -> EnemyMotor2D

Every runtime enemy must have one catalog-owned EnemyDefinition. Use
Tools/Dream Dungeon/Enemy System/Run EA2 Runtime Audit in Play Mode.
The report must show:
- Result=PASS
- T7AuthoritativeNavigationAgents matching RuntimeEnemies
- T7CDefinitionOnlyRuntime matching RuntimeEnemies
