CB8 - Dual-action timing arbitration

Default policy
- Nonlethal push and direct attack retain independent cooldowns.
- One action cannot start during the other action's afterlag window.
- No action cancels the other action by default.
- If push and direct-attack inputs occur in the same rendered frame,
  nonlethal push wins and direct attack is suppressed.
- The preferred action does not fall back to the other action when it is
  unavailable. This prevents accidental damage during no-kill play.

Authoring
PlayerSpawner > Combat: CB8 dual-action arbitration
- Enabled
- Simultaneous Input Policy
- Push During Direct Attack Recovery
- Direct Attack During Push Recovery

Cross-action recovery policies may be changed to Cancel Current Recovery,
but the conservative default is Block New Action.

Runtime guarantees
- At most one combat action starts in one rendered frame.
- Push/direct attack ids remain one-to-one with started actions.
- Cooldowns remain independent after the other action's afterlag ends.
- Player Rigidbody2D movement remains owned by RuntimeDungeonPlayer.
