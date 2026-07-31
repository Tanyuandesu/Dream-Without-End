Enemy System EA3 / T7 Navigation Authority

T7A removed TestEnemyAI. No compatibility bridge or parallel movement loop
remains. EnemyNavigationAgent is the only runtime navigation controller, while
EnemyStateMachine owns behavior transitions and EnemyMotor2D owns movement.

Use Tools/Dream Dungeon/Enemy System/Run EA3 Navigation Audit in Play Mode.
The report must show Result=PASS and:
T7AuthorityPath=EnemyStateMachine->EnemyNavigationAgent
