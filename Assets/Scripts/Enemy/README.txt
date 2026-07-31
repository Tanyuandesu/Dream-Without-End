Enemy System Authority (T7B)

Runtime authority:
1. EnemyStateMachine - behavior and combat states
2. EnemyNavigationAgent - navigation requests and path following
3. EnemyPathService / EnemyPathfinder - shared A* and local facade
4. EnemyMotor2D - all Rigidbody2D movement

The retired TestEnemyAI compatibility bridge was deleted in T7A.
The retired ContactDamage2D path and its fallback configuration were deleted
in T7B. Formal melee/projectile attacks are the sole Definition-driven damage
authority.
