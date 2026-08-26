P10.12A-1.2 Duplicate Definition Clean Reinstall

Why this exists
---------------
The previous hotfix could leave two generations of P10.12A-1 scripts side by side.
Unity then reports CS0101/CS0111 duplicate type/member errors.
This is an Editor-script installation conflict, not a Production_Main or runtime-room failure.

Safest install
--------------
1. Close Unity completely.
2. Extract this ZIP into the UNITY PROJECT ROOT.
   The project root is the folder that contains Assets / Packages / ProjectSettings.
3. Double-click INSTALL_P10_12A1_2_CLEAN.cmd.
4. Wait until it says Done, then press a key to close it.
5. Reopen Unity and wait for compilation.

The script removes ONLY these known P10.12A-1 script names, then installs one authoritative set:
- DreamProceduralRoomKernelP1012A1.cs
- DreamProceduralRoomPrototypeP1012A1.cs
- Editor/DreamProceduralRoomAuditP1012A1.cs

It does NOT delete:
- Production_Main
- GameScene
- Crossroad / Classroom / MusicRoom
- Graybox prefab assets
- ProcRoom_Medium_13x09.prefab
- DungeonGenerator / DungeonRenderer

After Unity compiles
--------------------
Run:
Tools > Dream Dungeon > Procedural Rooms > P10.12A-1
> 1B. Repair Existing Prototype Component

Then open ProcRoom_Medium_13x09.prefab and select its root.
After the red/green preview appears, run:
> 2. Validate 13x9 Prototype + Print Layout
> 3. Run 256-Seed Kernel Audit

Do not recreate the prototype from the Graybox unless we explicitly decide to do so.
