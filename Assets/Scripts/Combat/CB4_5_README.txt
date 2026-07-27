Dream Dungeon Combat CB4.5
==========================

CB4.5 adds configurable mouse and full-keyboard combat input bindings while
preserving the complete CB4 nonlethal-push pipeline.

Default bindings
----------------
Nonlethal Push:
- Left Mouse Button
- J (WASD-side primary key)
- Z (arrow-key-side secondary key)

Direct Attack, reserved for the next damage phase:
- Right Mouse Button
- K (WASD-side primary key)
- X (arrow-key-side secondary key)

All enabled nonlethal-push inputs call the same TryPerformNonlethalPush method
and therefore share facing, cooldown, afterlag, hit detection, attack id,
knockback decay and CB4 enemy recovery.

Direct-attack inputs currently enter one reserved TryPerformDirectAttack method.
CB4.5 intentionally performs no damage, issues no attack id and starts no player
action timing from these reserved inputs.

Configuration
-------------
Select PlayerSystem > PlayerSpawner in GameScene and edit:
Combat: CB4.5 Mouse and Full Keyboard Input.

Validation
----------
In Play Mode, successfully push an enemy with left mouse, J and Z. Press right
mouse, K and X once each. Then run:
Tools > Dream Dungeon > Combat > Run CB4.5 Multi-Input Audit
