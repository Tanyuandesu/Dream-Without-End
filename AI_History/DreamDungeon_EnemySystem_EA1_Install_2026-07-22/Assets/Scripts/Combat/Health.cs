using System;
using UnityEngine;

/// <summary>
/// 玩家、敵人與將來可破壞物件共用的生命組件。
/// </summary>
[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour, IDamageable
{
    [Header("目前生命")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("傷害規則")]
    [SerializeField] private DamageFaction faction =
        DamageFaction.Neutral;

    [Tooltip("受到一次傷害後，多少秒內不再接受下一次傷害。")]
    [Min(0f)]
    [SerializeField] private float invulnerabilityDuration = 0.2f;

    [Tooltip("忽略同陣營造成的傷害。")]
    [SerializeField] private bool ignoreFriendlyFire = true;

    [Header("死亡")]
    [Tooltip("敵人通常開啟；玩家通常關閉，由 PlayerManager 處理死亡。")]
    [SerializeField] private bool destroyGameObjectOnDeath = false;

    private float nextDamageAllowedTime;
    private bool initialized;
    private bool hasLastAcceptedDamage;
    private DamageInfo lastAcceptedDamage;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float NormalizedHealth =>
        maxHealth > 0f ? currentHealth / maxHealth : 0f;

    public bool IsDead { get; private set; }
    public DamageFaction Faction => faction;
    public bool HasLastAcceptedDamage => hasLastAcceptedDamage;
    public DamageInfo LastAcceptedDamage => lastAcceptedDamage;

    public event Action<Health, float, float> HealthChanged;
    public event Action<Health, DamageInfo> Damaged;
    public event Action<Health> Died;
    public event Action<Health, float> Healed;

    private void Awake()
    {
        if (!initialized)
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(
                currentHealth,
                0f,
                maxHealth);

            IsDead = currentHealth <= 0f;
        }
    }

    public void Initialize(
        float newMaxHealth,
        DamageFaction newFaction,
        float newInvulnerabilityDuration,
        bool destroyOnDeath)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = maxHealth;

        faction = newFaction;
        invulnerabilityDuration =
            Mathf.Max(0f, newInvulnerabilityDuration);

        destroyGameObjectOnDeath = destroyOnDeath;

        nextDamageAllowedTime = 0f;
        IsDead = false;
        hasLastAcceptedDamage = false;
        lastAcceptedDamage = default(DamageInfo);
        initialized = true;

        HealthChanged?.Invoke(
            this,
            currentHealth,
            maxHealth);
    }

    public bool ApplyDamage(DamageInfo damageInfo)
    {
        if (IsDead || damageInfo.Amount <= 0f)
        {
            return false;
        }

        if (Time.time < nextDamageAllowedTime)
        {
            return false;
        }

        if (ignoreFriendlyFire &&
            faction != DamageFaction.Neutral &&
            damageInfo.SourceFaction == faction)
        {
            return false;
        }

        float previousHealth = currentHealth;

        currentHealth = Mathf.Max(
            0f,
            currentHealth - damageInfo.Amount);

        lastAcceptedDamage = damageInfo;
        hasLastAcceptedDamage = true;

        nextDamageAllowedTime =
            Time.time + invulnerabilityDuration;

        Damaged?.Invoke(this, damageInfo);
        HealthChanged?.Invoke(
            this,
            currentHealth,
            maxHealth);

        if (previousHealth > 0f &&
            currentHealth <= 0f)
        {
            Die();
        }

        return true;
    }

    public float Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return 0f;
        }

        float previousHealth = currentHealth;

        currentHealth = Mathf.Min(
            maxHealth,
            currentHealth + amount);

        float healedAmount =
            currentHealth - previousHealth;

        if (healedAmount > 0f)
        {
            Healed?.Invoke(this, healedAmount);
            HealthChanged?.Invoke(
                this,
                currentHealth,
                maxHealth);
        }

        return healedAmount;
    }

    public void Revive(float healthAmount = -1f)
    {
        IsDead = false;
        nextDamageAllowedTime = 0f;
        hasLastAcceptedDamage = false;
        lastAcceptedDamage = default(DamageInfo);

        currentHealth = healthAmount < 0f
            ? maxHealth
            : Mathf.Clamp(
                healthAmount,
                1f,
                maxHealth);

        gameObject.SetActive(true);

        HealthChanged?.Invoke(
            this,
            currentHealth,
            maxHealth);
    }

    public void SetMaxHealth(
        float newMaxHealth,
        bool refillHealth)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);

        currentHealth = refillHealth
            ? maxHealth
            : Mathf.Min(currentHealth, maxHealth);

        HealthChanged?.Invoke(
            this,
            currentHealth,
            maxHealth);
    }

    [ContextMenu("Refill Health")]
    private void RefillHealthFromInspector()
    {
        IsDead = false;
        currentHealth = maxHealth;
        nextDamageAllowedTime = 0f;
        hasLastAcceptedDamage = false;
        lastAcceptedDamage = default(DamageInfo);

        HealthChanged?.Invoke(
            this,
            currentHealth,
            maxHealth);
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Died?.Invoke(this);

        if (destroyGameObjectOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
