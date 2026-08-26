CB9.5 Temporary Enemy Health Bars

Runtime enemy health bars are presentation-only SpriteRenderer children.
They listen to Health.Damaged, Health.HealthChanged and Health.Died.
They never modify damage, AI, Rigidbody2D, Collider2D or ending records.

Default behavior:
- hidden at full health;
- accepted damage reveals the bar for 1.5 seconds;
- repeated accepted damage refreshes the timer;
- the bar fades for 0.25 seconds and then disables its visual root;
- death hides it immediately;
- nonlethal push does not reveal it because it never emits accepted damage.

Shared settings are authored on EnemySpawner.
Per-enemy enable, size multiplier and additional offset are authored on EnemyDefinition.
