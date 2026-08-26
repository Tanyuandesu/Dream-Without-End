P10.12A-1.3

Purpose: find and remove every .cs under Assets that DECLARES the unique P10.12A-1 types, then reinstall exactly one authoritative set and verify each declaration count is exactly 1.

Close Unity first. Extract to project root. Run INSTALL_P10_12A1_3_PURGE_DUPLICATES.cmd.
If it prints PASS, reopen Unity. If it prints ERROR, send P10_12A1_3_PURGE_REPORT.txt before doing anything else.

This does not touch Production_Main, GameScene, room prefabs, DungeonGenerator, or DungeonRenderer.
