Dream Dungeon Enemy System - T7C Static Authority

EA1 validates the final Definition/Catalog data contract and all current
behavior/combat authoring. T7C removes EnemySpawner's retired hidden gameplay
fields and old runtime overloads.

EnemySpawner now owns only:
- EnemyCatalog / SpawnMode / baseline Definition selection
- enemy count and room-selection switches
- shared EnemyPathService limits
- shared temporary-health-bar presentation

All per-enemy gameplay and presentation data comes from EnemyDefinition.
After importing T7C, open GameScene and save it once so Unity removes retired
serialized field residue, then run EA1. Result must be PASS.
