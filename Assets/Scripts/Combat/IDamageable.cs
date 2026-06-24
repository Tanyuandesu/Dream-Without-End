public interface IDamageable
{
    bool IsDead { get; }
    DamageFaction Faction { get; }

    bool ApplyDamage(DamageInfo damageInfo);
    float Heal(float amount);
}
