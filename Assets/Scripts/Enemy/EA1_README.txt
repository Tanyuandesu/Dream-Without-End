Dream Dungeon Enemy System - Current Authority Note

EA1 introduced the Definition/Catalog data contract. The current runtime has
progressed through T6B.3, T7A and T7B. TestEnemyAI and ContactDamage2D
are no longer part of the system.

Authoritative runtime path:
EnemyStateMachine -> EnemyNavigationAgent -> EnemyPathService/EnemyPathfinder
-> EnemyMotor2D.

Enemy behavior and formal melee/projectile combat values are read from
EnemyDefinition assets.
Use EA1 for static configuration, EA2 for runtime bindings/activity and EA3
for navigation/algorithm regression.
