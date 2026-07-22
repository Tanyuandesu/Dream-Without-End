Dream Dungeon Enemy System - EA1
Date: 2026-07-22

EXPECTED RESULT

- GameScene still spawns three visually identical temporary stick cats.
- Their baseline values remain HP 30, Speed 4.7, Detection 20/24,
  Contact Damage 5 and Cooldown 0.75.
- AI and A* behaviour remain the pre-EA1 TestEnemyAI baseline.
- Runtime objects are named Wanderer_1..3 and carry EnemyRuntimeIdentity.
- The debug overlay adds run enemy totals.

NEW AUTHORING DATA

Assets/GeneratedEnemySettings contains:

- Five stable Enemy Definitions:
  dream_wanderer, dream_scout, dream_hunter, dream_brute, dream_gazer.
- EnemyCatalog_Main, which registers exactly those five identities.
- RoomEncounter_LegacyBaseline.
- RoomEncounter_MixedAuthoringTemplate, an unbound authoring example for
  complete-option weights plus fixed and random members.
- RoomEncounterCatalog_Main. Its binding list intentionally stays empty in
  EA1; real room probability authoring belongs to EA6.

Only Enemy_Wanderer is assigned to the current EnemySpawner. The other four
Definitions do not spawn yet and therefore do not create fake enemy variety.

CONFIGURATION AUDIT

1. Open Assets/Scenes/GameScene.unity.
2. Wait for Unity compilation to finish.
3. Run:
   Tools > Dream Dungeon > Enemy System > Run EA1 Configuration Audit
4. Console must show Result=PASS and no red compile Error.

The audit validates five IDs, catalog membership, encounter data, scene
references, duplicate-safe runtime registration and ending-record totals.

PLAY MODE REGRESSION

1. Enter Play Mode: three Wanderers should spawn as the same temporary cat.
2. Confirm movement, chase, collision damage and animation look unchanged.
3. Overlay should show Run Enemies: 3 and Player Kills: 0.
4. Enter the next floor. Current Enemies should remain 3, while Run Enemies
   becomes 6 and Survived Floors becomes 3.
5. Press R once and confirm one regenerated floor still has exactly three
   active enemies and no duplicate runtime object remains.
6. Optional data test: change Enemy_Wanderer Move Speed from 4.7 to 2.0,
   verify the change in Play Mode, then restore 4.7.

Player-attributed gameplay kills cannot yet be exercised through normal input,
because the formal player attack belongs to the combat phase. EA1 validates
the attribution and ending-query contract through the editor audit instead.

RUN RECORD CONTRACT

EnemyManager.RunRecord and EnemyManager.CurrentRunSnapshot expose:

- all/eligible spawned enemies;
- player-attributed deaths and other deaths;
- survivors when leaving a floor;
- unexpected removals and currently active enemies;
- no-enemy-death, no-player-kill, all-dead and all-player-killed queries.

Destroying a floor is not counted as a kill. NPCs are not registered in this
system. The record is memory-only for the current run; save serialization and
ending selection are intentionally not implemented in EA1.

ROLLBACK

Restore the six modified baseline files (GameScene, GameManager, Health,
DamageInfo, EnemyManager and EnemySpawner), then remove GeneratedEnemySettings,
DamageAttribution and the new Enemy data scripts. A supplied rollback archive
performs this replacement; use the project backup if Unity has already saved
unrelated scene changes over GameScene.
