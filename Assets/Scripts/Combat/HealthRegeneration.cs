using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 通用自動回血組件。
///
/// 規則：
/// 1. 受到傷害後，等待 Regen Delay。
/// 2. 延遲結束後，每隔 Regen Interval 恢復一次生命。
/// 3. 每次恢復 Regen Amount Per Tick。
/// 4. 再次受傷時，延遲與回血計時都會重新開始。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class HealthRegeneration : MonoBehaviour
{
    [Header("自動回血")]
    [SerializeField] private bool regenerationEnabled = true;

    [Tooltip("最後一次受到傷害後，需要等待多少秒才開始回血。")]
    [Min(0f)]
    [SerializeField] private float regenDelay = 3f;

    [Tooltip("每隔多少秒恢復一次生命。")]
    [Min(0.01f)]
    [SerializeField] private float regenInterval = 1f;

    [Tooltip("每次回血恢復多少生命。")]
    [FormerlySerializedAs("regenPerSecond")]
    [Min(0f)]
    [SerializeField] private float regenAmountPerTick = 5f;

    [Tooltip("開啟後，角色必須先受傷，才會啟動第一次回血。")]
    [SerializeField] private bool requireDamageBeforeRegeneration = true;

    private Health health;
    private float regenerationStartTime;
    private float nextRegenTickTime;
    private bool hasTakenDamage;

    public bool RegenerationEnabled => regenerationEnabled;
    public float RegenDelay => regenDelay;
    public float RegenInterval => regenInterval;
    public float RegenAmountPerTick => regenAmountPerTick;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (!CanRegenerate())
        {
            return;
        }

        if (Time.time < nextRegenTickTime)
        {
            return;
        }

        health.Heal(regenAmountPerTick);

        nextRegenTickTime =
            Time.time + regenInterval;
    }

    public void Initialize(
        bool enabled,
        float newRegenDelay,
        float newRegenInterval,
        float newRegenAmountPerTick,
        bool newRequireDamageBeforeRegeneration)
    {
        regenerationEnabled = enabled;
        regenDelay = Mathf.Max(0f, newRegenDelay);
        regenInterval = Mathf.Max(0.01f, newRegenInterval);
        regenAmountPerTick = Mathf.Max(0f, newRegenAmountPerTick);

        requireDamageBeforeRegeneration =
            newRequireDamageBeforeRegeneration;

        hasTakenDamage =
            !requireDamageBeforeRegeneration;

        RestartRegenerationTimer();
    }

    public void SetRegenerationEnabled(bool enabled)
    {
        regenerationEnabled = enabled;

        if (enabled)
        {
            RestartRegenerationTimer();
        }
    }

    public void RestartRegenerationTimer()
    {
        regenerationStartTime =
            Time.time + regenDelay;

        // 冷卻結束時立即進行第一跳回血，
        // 之後才按照 regenInterval 持續回血。
        nextRegenTickTime =
            regenerationStartTime;
    }

    private bool CanRegenerate()
    {
        if (!regenerationEnabled ||
            health == null ||
            health.IsDead ||
            regenAmountPerTick <= 0f)
        {
            return false;
        }

        if (health.CurrentHealth >= health.MaxHealth)
        {
            return false;
        }

        if (requireDamageBeforeRegeneration &&
            !hasTakenDamage)
        {
            return false;
        }

        return Time.time >= regenerationStartTime;
    }

    private void HandleDamaged(
        Health damagedHealth,
        DamageInfo damageInfo)
    {
        hasTakenDamage = true;
        RestartRegenerationTimer();
    }

    private void HandleDied(Health deadHealth)
    {
        hasTakenDamage = false;
    }

    private void OnValidate()
    {
        regenDelay = Mathf.Max(0f, regenDelay);
        regenInterval = Mathf.Max(0.01f, regenInterval);
        regenAmountPerTick = Mathf.Max(0f, regenAmountPerTick);
    }
}
