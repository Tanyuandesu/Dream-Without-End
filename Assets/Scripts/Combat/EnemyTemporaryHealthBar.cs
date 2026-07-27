using UnityEngine;

/// <summary>
/// Lightweight world-space enemy health bar using SpriteRenderers.
/// It listens to Health events and never participates in damage, AI or physics.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyTemporaryHealthBar : MonoBehaviour
{
    private const float FullHealthTolerance = 0.0001f;

    private static Sprite sharedUnitSprite;

    [SerializeField] private Health health;
    [SerializeField] private Transform barRoot;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer fillRenderer;

    [SerializeField] private bool configured;
    [SerializeField] private bool currentlyVisible;
    [SerializeField] private bool fadeStarted;
    [SerializeField] private float visibleUntilTime;
    [SerializeField] private float currentAlpha;
    [SerializeField] private float currentNormalizedHealth = 1f;

    private float visibleDuration;
    private float fadeDuration;
    private Vector2 barSize;
    private float borderThickness;
    private Color backgroundColor;
    private Color fillColor;
    private bool onlyPlayerDamage;
    private bool hideWhenFull;
    private bool hideImmediatelyOnDeath;

    public Health Health => health;
    public bool IsConfigured => configured;
    public bool IsVisible => currentlyVisible;
    public float CurrentAlpha => currentAlpha;
    public float NormalizedHealth => currentNormalizedHealth;
    public Transform BarRoot => barRoot;
    public SpriteRenderer BackgroundRenderer => backgroundRenderer;
    public SpriteRenderer FillRenderer => fillRenderer;

    public void Initialize(
        Health targetHealth,
        EnemyVisual enemyVisual,
        EnemyTemporaryHealthBarSettings settings,
        bool enemyEnabled,
        float enemySizeMultiplier,
        Vector2 enemyOffset)
    {
        DetachHealthEvents();

        health = targetHealth;
        configured = false;

        if (health == null ||
            settings == null ||
            !settings.Enabled ||
            !enemyEnabled)
        {
            HideImmediate(false);
            return;
        }

        settings.EnsureValid();

        visibleDuration = settings.VisibleDuration;
        fadeDuration = settings.FadeDuration;
        barSize = settings.BarSize * Mathf.Max(
            0.1f,
            enemySizeMultiplier);
        borderThickness = Mathf.Min(
            settings.BorderThickness,
            Mathf.Min(barSize.x, barSize.y) * 0.49f);
        backgroundColor = settings.BackgroundColor;
        fillColor = settings.FillColor;
        onlyPlayerDamage = settings.OnlyPlayerDamage;
        hideWhenFull = settings.HideWhenFull;
        hideImmediatelyOnDeath = settings.HideImmediatelyOnDeath;

        int sortingLayerId = 0;
        int baseSortingOrder = 0;

        if (enemyVisual != null &&
            enemyVisual.Renderer != null)
        {
            sortingLayerId =
                enemyVisual.Renderer.sortingLayerID;
            baseSortingOrder =
                enemyVisual.Renderer.sortingOrder;
        }

        EnsureVisualObjects();

        barRoot.localPosition =
            settings.WorldOffset + enemyOffset;
        barRoot.localRotation = Quaternion.identity;
        barRoot.localScale = Vector3.one;

        backgroundRenderer.sortingLayerID = sortingLayerId;
        backgroundRenderer.sortingOrder =
            baseSortingOrder + settings.SortingOrderOffset;

        fillRenderer.sortingLayerID = sortingLayerId;
        fillRenderer.sortingOrder =
            baseSortingOrder + settings.SortingOrderOffset + 1;

        configured = true;
        currentNormalizedHealth = health.NormalizedHealth;
        ApplyFill(currentNormalizedHealth);
        HideImmediate(false);
        AttachHealthEvents();

        EnemyTemporaryHealthBarDiagnostics.RecordCreated();
    }

    private void Update()
    {
        if (!configured ||
            !currentlyVisible ||
            barRoot == null)
        {
            return;
        }

        if (Time.time <= visibleUntilTime)
        {
            SetAlpha(1f);
            return;
        }

        if (!fadeStarted)
        {
            fadeStarted = true;
            EnemyTemporaryHealthBarDiagnostics.RecordFadeStarted();
        }

        if (fadeDuration <= 0f)
        {
            HideImmediate(false);
            EnemyTemporaryHealthBarDiagnostics.RecordFadeCompletedHide();
            return;
        }

        float elapsed = Time.time - visibleUntilTime;
        float fadeProgress = Mathf.Clamp01(
            elapsed / fadeDuration);
        SetAlpha(1f - fadeProgress);

        if (fadeProgress >= 1f)
        {
            HideImmediate(false);
            EnemyTemporaryHealthBarDiagnostics.RecordFadeCompletedHide();
        }
    }

    private void OnDestroy()
    {
        DetachHealthEvents();
    }

    private void AttachHealthEvents()
    {
        if (health == null)
        {
            return;
        }

        health.Damaged += HandleDamaged;
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDied;
    }

    private void DetachHealthEvents()
    {
        if (health == null)
        {
            return;
        }

        health.Damaged -= HandleDamaged;
        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDied;
    }

    private void HandleDamaged(
        Health sourceHealth,
        DamageInfo damageInfo)
    {
        if (!configured || sourceHealth != health)
        {
            return;
        }

        DamageAttribution attribution =
            damageInfo.ResolvedAttribution;

        if (onlyPlayerDamage &&
            attribution != DamageAttribution.Player)
        {
            EnemyTemporaryHealthBarDiagnostics.RecordRejectedSource();
            return;
        }

        float normalized = health != null
            ? health.NormalizedHealth
            : currentNormalizedHealth;

        EnemyTemporaryHealthBarDiagnostics.RecordDamageTrigger(
            GetInstanceID(),
            attribution,
            normalized);

        ShowOrRefresh();
    }

    private void HandleHealthChanged(
        Health sourceHealth,
        float currentHealth,
        float maximumHealth)
    {
        if (!configured || sourceHealth != health)
        {
            return;
        }

        currentNormalizedHealth = maximumHealth > 0f
            ? Mathf.Clamp01(currentHealth / maximumHealth)
            : 0f;

        ApplyFill(currentNormalizedHealth);

        if (hideWhenFull &&
            currentNormalizedHealth >= 1f - FullHealthTolerance &&
            currentlyVisible)
        {
            HideImmediate(false);
            EnemyTemporaryHealthBarDiagnostics.RecordFullHealthHide();
        }
    }

    private void HandleDied(Health sourceHealth)
    {
        if (!configured ||
            sourceHealth != health ||
            !hideImmediatelyOnDeath)
        {
            return;
        }

        bool wasVisible = currentlyVisible;
        HideImmediate(false);

        if (wasVisible)
        {
            EnemyTemporaryHealthBarDiagnostics.RecordDeathHide();
        }
    }

    private void ShowOrRefresh()
    {
        if (barRoot == null ||
            health == null ||
            health.IsDead)
        {
            return;
        }

        bool wasVisible = currentlyVisible;

        currentNormalizedHealth =
            health.NormalizedHealth;
        ApplyFill(currentNormalizedHealth);

        visibleUntilTime = Time.time + visibleDuration;
        fadeStarted = false;
        currentlyVisible = true;
        barRoot.gameObject.SetActive(true);
        SetAlpha(1f);

        if (wasVisible)
        {
            EnemyTemporaryHealthBarDiagnostics.RecordTimerRefreshed();
        }
        else
        {
            EnemyTemporaryHealthBarDiagnostics.RecordShowStarted();
        }
    }

    private void HideImmediate(bool recordFadeCompletion)
    {
        currentlyVisible = false;
        fadeStarted = false;
        visibleUntilTime = 0f;
        SetAlpha(0f);

        if (barRoot != null)
        {
            barRoot.gameObject.SetActive(false);
        }

        if (recordFadeCompletion)
        {
            EnemyTemporaryHealthBarDiagnostics.RecordFadeCompletedHide();
        }
    }

    private void ApplyFill(float normalized)
    {
        if (backgroundRenderer == null ||
            fillRenderer == null)
        {
            return;
        }

        float safeRatio = Mathf.Clamp01(normalized);
        float availableWidth = Mathf.Max(
            0.001f,
            barSize.x - borderThickness * 2f);
        float availableHeight = Mathf.Max(
            0.001f,
            barSize.y - borderThickness * 2f);
        float fillWidth = availableWidth * safeRatio;

        backgroundRenderer.transform.localPosition =
            Vector3.zero;
        backgroundRenderer.transform.localScale =
            new Vector3(barSize.x, barSize.y, 1f);

        fillRenderer.transform.localPosition =
            new Vector3(
                -barSize.x * 0.5f +
                borderThickness +
                fillWidth * 0.5f,
                0f,
                0f);

        fillRenderer.transform.localScale =
            new Vector3(fillWidth, availableHeight, 1f);
    }

    private void SetAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);

        if (backgroundRenderer != null)
        {
            Color background = backgroundColor;
            background.a *= currentAlpha;
            backgroundRenderer.color = background;
        }

        if (fillRenderer != null)
        {
            Color fill = fillColor;
            fill.a *= currentAlpha;
            fillRenderer.color = fill;
        }
    }

    private void EnsureVisualObjects()
    {
        if (barRoot == null)
        {
            Transform existing =
                transform.Find("TemporaryHealthBar");

            if (existing != null)
            {
                barRoot = existing;
            }
            else
            {
                GameObject rootObject =
                    new GameObject("TemporaryHealthBar");
                rootObject.transform.SetParent(transform, false);
                barRoot = rootObject.transform;
            }
        }

        backgroundRenderer = EnsureRenderer(
            barRoot,
            "Background",
            backgroundRenderer);

        fillRenderer = EnsureRenderer(
            barRoot,
            "Fill",
            fillRenderer);
    }

    private static SpriteRenderer EnsureRenderer(
        Transform parent,
        string childName,
        SpriteRenderer current)
    {
        if (current != null)
        {
            current.sprite = GetUnitSprite();
            return current;
        }

        Transform child = parent.Find(childName);
        GameObject childObject;

        if (child != null)
        {
            childObject = child.gameObject;
        }
        else
        {
            childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
        }

        SpriteRenderer renderer =
            childObject.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer =
                childObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = GetUnitSprite();
        renderer.transform.localRotation = Quaternion.identity;
        return renderer;
    }

    private static Sprite GetUnitSprite()
    {
        if (sharedUnitSprite != null)
        {
            return sharedUnitSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedUnitSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);

        sharedUnitSprite.name = "EnemyTemporaryHealthBar_UnitSprite";
        sharedUnitSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedUnitSprite;
    }
}
