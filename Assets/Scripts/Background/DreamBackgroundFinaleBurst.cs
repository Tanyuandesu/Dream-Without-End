using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamBackgroundFinaleBurst : MonoBehaviour
{
    [Header("進度來源")]
    [SerializeField] private ItemManager itemManager;

    [Min(1)]
    [SerializeField] private int finalCollectedCount = 7;

    [Header("背景")]
    [SerializeField] private SpriteRenderer backgroundBase;

    [Header("粒子")]
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private ParticleSystem glowParticles;

    [Header("Burst 數量")]
    [Min(0)]
    [SerializeField] private int dustBurstCount = 18;

    [Min(0)]
    [SerializeField] private int glowBurstCount = 8;

    [Header("亮度脈衝")]
    [Min(0.05f)]
    [SerializeField] private float pulseDuration = 1.1f;

    [Range(1f, 2f)]
    [SerializeField] private float peakBrightnessMultiplier = 1.18f;

    [SerializeField] private AnimationCurve pulseCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("除錯")]
    [SerializeField] private bool logFinaleTrigger = true;

    private bool hasTriggered;
    private Coroutine pulseRoutine;

    private void Reset()
    {
        itemManager = FindFirstObjectByType<ItemManager>();

        if (backgroundBase == null)
        {
            SpriteRenderer[] renderers =
                GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers.Length > 0)
            {
                backgroundBase = renderers[0];
            }
        }

        ParticleSystem[] particles =
            GetComponentsInChildren<ParticleSystem>(true);

        if (particles.Length > 0)
        {
            dustParticles = particles[0];
        }

        if (particles.Length > 1)
        {
            glowParticles = particles[1];
        }
    }

    private void Awake()
    {
        if (itemManager == null)
        {
            itemManager = FindFirstObjectByType<ItemManager>();
        }
    }

    private void OnEnable()
    {
        if (itemManager != null)
        {
            itemManager.ProgressChanged += HandleProgressChanged;
        }
    }

    private void OnDisable()
    {
        if (itemManager != null)
        {
            itemManager.ProgressChanged -= HandleProgressChanged;
        }

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }
    }

    private void HandleProgressChanged(ItemProgressSnapshot snapshot)
    {
        if (snapshot == null || hasTriggered)
        {
            return;
        }

        if (snapshot.CollectedCount < finalCollectedCount)
        {
            return;
        }

        TriggerFinale();
    }

    [ContextMenu("Test Finale Burst")]
    public void TriggerFinale()
    {
        if (hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (dustParticles != null && dustBurstCount > 0)
        {
            dustParticles.Emit(dustBurstCount);
        }

        if (glowParticles != null && glowBurstCount > 0)
        {
            glowParticles.Emit(glowBurstCount);
        }

        if (backgroundBase != null)
        {
            pulseRoutine = StartCoroutine(PlayBackgroundPulse());
        }

        if (logFinaleTrigger)
        {
            Debug.Log("Dream background finale burst triggered.");
        }
    }

    private IEnumerator PlayBackgroundPulse()
    {
        Color startingColor = backgroundBase.color;
        float elapsed = 0f;

        while (elapsed < pulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsed / pulseDuration);

            float trianglePulse =
                normalizedTime <= 0.5f
                    ? normalizedTime * 2f
                    : (1f - normalizedTime) * 2f;

            float evaluatedPulse =
                pulseCurve.Evaluate(trianglePulse);

            float brightness =
                Mathf.Lerp(
                    1f,
                    peakBrightnessMultiplier,
                    evaluatedPulse);

            Color color = startingColor;
            color.r = Mathf.Clamp01(startingColor.r * brightness);
            color.g = Mathf.Clamp01(startingColor.g * brightness);
            color.b = Mathf.Clamp01(startingColor.b * brightness);

            backgroundBase.color = color;

            yield return null;
        }

        backgroundBase.color = startingColor;
        pulseRoutine = null;
    }

    public void ResetFinaleForDebug()
    {
        hasTriggered = false;
    }
}
