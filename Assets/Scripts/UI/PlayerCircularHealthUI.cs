using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家圓環生命 UI。
///
/// 使用 Image.fillAmount 顯示 0～100% 血量。
/// Image 必須設定為：
/// Type = Filled
/// Fill Method = Radial 360
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCircularHealthUI : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("拖入 PlayerSystem 上的 PlayerManager。留空時會自動尋找。")]
    [SerializeField] private PlayerManager playerManager;

    [Header("圓環 UI")]
    [Tooltip("拖入作為血量圓環的 Image。")]
    [SerializeField] private Image healthRingImage;

    [Tooltip("可選。顯示 HP 數字。")]
    [SerializeField] private TMP_Text healthText;

    [Tooltip("可選。玩家尚未生成時隱藏的視覺容器。")]
    [SerializeField] private GameObject visualRoot;

    [Header("顯示格式")]
    [SerializeField] private bool showNumericText = true;
    [SerializeField] private string textPrefix = "";
    [SerializeField] private bool showMaxHealth = true;
    [SerializeField] private bool roundToWholeNumbers = true;

    [Header("顏色")]
    [SerializeField] private bool useLowHealthColor = true;

    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.25f;

    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color lowHealthColor =
        new Color(1f, 0.35f, 0.1f, 1f);

    private Health boundHealth;

    private void Reset()
    {
        playerManager =
            FindObjectOfType<PlayerManager>();

        healthRingImage =
            GetComponentInChildren<Image>(true);

        healthText =
            GetComponentInChildren<TMP_Text>(true);

        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (healthRingImage != null)
        {
            healthRingImage.type =
                Image.Type.Filled;

            healthRingImage.fillMethod =
                Image.FillMethod.Radial360;

            healthRingImage.fillAmount = 1f;
        }
    }

    private void OnEnable()
    {
        SetVisualVisible(false);
        TryBindPlayerHealth();
    }

    private void Update()
    {
        if (boundHealth == null)
        {
            TryBindPlayerHealth();
        }
    }

    private void OnDisable()
    {
        UnbindHealth();
    }

    private void TryBindPlayerHealth()
    {
        if (playerManager == null)
        {
            playerManager =
                FindObjectOfType<PlayerManager>();
        }

        if (playerManager == null ||
            playerManager.CurrentHealth == null)
        {
            return;
        }

        Health newHealth =
            playerManager.CurrentHealth;

        if (boundHealth == newHealth)
        {
            return;
        }

        UnbindHealth();

        boundHealth = newHealth;
        boundHealth.HealthChanged +=
            HandleHealthChanged;

        boundHealth.Died += HandleDied;

        SetVisualVisible(true);

        Refresh(
            boundHealth.CurrentHealth,
            boundHealth.MaxHealth);
    }

    private void UnbindHealth()
    {
        if (boundHealth == null)
        {
            return;
        }

        boundHealth.HealthChanged -=
            HandleHealthChanged;

        boundHealth.Died -= HandleDied;

        boundHealth = null;
    }

    private void HandleHealthChanged(
        Health health,
        float currentHealth,
        float maxHealth)
    {
        Refresh(currentHealth, maxHealth);
    }

    private void HandleDied(Health health)
    {
        Refresh(0f, health.MaxHealth);
    }

    private void Refresh(
        float currentHealth,
        float maxHealth)
    {
        float normalized =
            maxHealth > 0f
                ? Mathf.Clamp01(
                    currentHealth / maxHealth)
                : 0f;

        if (healthRingImage != null)
        {
            healthRingImage.fillAmount =
                normalized;

            if (useLowHealthColor &&
                normalized <= lowHealthThreshold)
            {
                healthRingImage.color =
                    lowHealthColor;
            }
            else
            {
                healthRingImage.color =
                    normalColor;
            }
        }

        if (healthText == null)
        {
            return;
        }

        healthText.gameObject.SetActive(
            showNumericText);

        if (!showNumericText)
        {
            return;
        }

        if (roundToWholeNumbers)
        {
            int current =
                Mathf.CeilToInt(currentHealth);

            int maximum =
                Mathf.CeilToInt(maxHealth);

            healthText.text = showMaxHealth
                ? textPrefix +
                  current +
                  " / " +
                  maximum
                : textPrefix +
                  current;
        }
        else
        {
            healthText.text = showMaxHealth
                ? textPrefix +
                  currentHealth.ToString("0.0") +
                  " / " +
                  maxHealth.ToString("0.0")
                : textPrefix +
                  currentHealth.ToString("0.0");
        }
    }

    private void SetVisualVisible(bool visible)
    {
        if (visualRoot != null &&
            visualRoot != gameObject)
        {
            visualRoot.SetActive(visible);
        }
    }
}
