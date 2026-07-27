CB5 Direct Attack Skeleton
==========================

CB5 activates the existing right mouse / K / X entry point as a configurable,
eight-direction facing-based damage action.

Implemented:
- Separate DirectAttackSettings for damage, range, fan, target limit, cooldown,
  afterlag and player movement multiplier.
- Right mouse, K and X enter one action pipeline.
- Same-frame facing refresh and stationary last-facing retention.
- One CombatAttackId per started action and multi-collider target deduplication.
- Player-attributed test damage through CombatHit and EnemyCombatReceiver.
- No displacement, no enemy reaction, no knockback decay and no CB4 pursuit recovery.
- CB5 runtime audit.

Intentionally deferred:
- Weak direct-hit displacement/reaction (CB7).
- Full death/manager/ending audit (CB6).
- Cross-action cancellation and shared recovery arbitration (CB8).
- Animation, hit flash, sound and camera feedback (CB10).
