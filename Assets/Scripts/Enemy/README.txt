Enemy System Authority (T7C)

Runtime authority:
1. EnemyStateMachine - behavior and combat states
2. EnemyNavigationAgent - navigation requests and path following
3. EnemyPathService / EnemyPathfinder - shared A* and local facade
4. EnemyMotor2D - all Rigidbody2D movement and attack-facing presentation
5. EnemyMeleeAttackController - sole melee damage path
6. EnemyProjectileAttackController / EnemyProjectile - sole projectile path

EnemySpawner owns spawn selection, shared navigation-service settings and the
shared temporary-health-bar presentation only. Every spawned enemy requires a
catalog-owned EnemyDefinition. Vitals, movement, perception, behavior,
presentation, collision and attack values are read directly from that asset.

Retired systems:
- TestEnemyAI compatibility bridge: deleted in T7A
- ContactDamage2D and contact fallback data: deleted in T7B
- EnemySpawner hidden gameplay fallback fields and old overloads: deleted in T7C
