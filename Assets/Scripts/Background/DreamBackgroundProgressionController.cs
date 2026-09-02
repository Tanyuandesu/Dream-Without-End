using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamBackgroundProgressionController : MonoBehaviour
{
    [Header("進度來源")]
    [SerializeField] private ItemManager itemManager;

    [Header("設定檔")]
    [SerializeField] private DreamBackgroundProgressionProfile profile;

    [Header("背景物件")]
    [SerializeField] private SpriteRenderer backgroundBase;
    [SerializeField] private DreamFogDrift[] fogLayers =
        new DreamFogDrift[0];

    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private ParticleSystem glowParticles;

    [Header("除錯")]
    [SerializeField] private bool logStageChanges = true;

    private Color baseBackgroundColor;

    private float baseDustEmission = 1f;
    private float baseDustSize = 1f;
    private float baseGlowEmission = 1f;
    private float baseGlowSize = 1f;

    private RuntimeValues currentValues;
    private RuntimeValues startValues;
    private RuntimeValues targetValues;

    private float transitionElapsed;
    private float transitionDuration = 1f;
    private int currentStageMinimum = -1;

    private void Reset()
    {
        itemManager = FindFirstObjectByType<ItemManager>();
        fogLayers = GetComponentsInChildren<DreamFogDrift>(true);

        ParticleSystem[] particles =
            GetComponentsInChildren<ParticleSystem>(true);

        if (particles.Length > 0) dustParticles = particles[0];
        if (particles.Length > 1) glowParticles = particles[1];
    }

    private void Awake()
    {
        if (itemManager == null)
        {
            itemManager = FindFirstObjectByType<ItemManager>();
        }

        CacheBaseValues();

        currentValues = RuntimeValues.Default;
        startValues = currentValues;
        targetValues = currentValues;
    }

    private void OnEnable()
    {
        if (itemManager != null)
        {
            itemManager.ProgressChanged += HandleProgressChanged;
        }
    }

    private void Start()
    {
        int count =
            itemManager != null ? itemManager.CollectedItemCount : 0;

        ApplyStageForCount(count, true);
    }

    private void OnDisable()
    {
        if (itemManager != null)
        {
            itemManager.ProgressChanged -= HandleProgressChanged;
        }
    }

    private void Update()
    {
        if (transitionElapsed >= transitionDuration)
        {
            return;
        }

        transitionElapsed += Time.unscaledDeltaTime;

        float t =
            transitionDuration > 0f
                ? Mathf.Clamp01(transitionElapsed / transitionDuration)
                : 1f;

        t = t * t * (3f - 2f * t);

        currentValues =
            RuntimeValues.Lerp(startValues, targetValues, t);

        ApplyRuntimeValues(currentValues);
    }

    private void HandleProgressChanged(ItemProgressSnapshot snapshot)
    {
        if (snapshot != null)
        {
            ApplyStageForCount(snapshot.CollectedCount, false);
        }
    }

    public void ApplyStageForCount(int collectedCount, bool immediate)
    {
        if (profile == null)
        {
            Debug.LogWarning(
                "DreamBackgroundProgressionController：尚未設定 Profile。");
            return;
        }

        DreamBackgroundStageSettings stage =
            profile.GetStage(collectedCount);

        if (stage == null)
        {
            return;
        }

        if (!immediate &&
            stage.minimumCollectedItems == currentStageMinimum)
        {
            return;
        }

        currentStageMinimum = stage.minimumCollectedItems;
        RuntimeValues newTarget = RuntimeValues.FromStage(stage);

        if (immediate)
        {
            currentValues = newTarget;
            startValues = newTarget;
            targetValues = newTarget;
            transitionElapsed = 1f;
            transitionDuration = 1f;
            ApplyRuntimeValues(currentValues);
        }
        else
        {
            startValues = currentValues;
            targetValues = newTarget;
            transitionElapsed = 0f;
            transitionDuration =
                Mathf.Max(0.01f, stage.transitionDuration);
        }

        if (logStageChanges)
        {
            Debug.Log(
                "Dream background stage changed. Collected Items: " +
                collectedCount +
                " | Stage Minimum: " +
                stage.minimumCollectedItems);
        }
    }

    public void PreviewCollectedCount(int collectedCount)
    {
        ApplyStageForCount(Mathf.Max(0, collectedCount), false);
    }

    private void CacheBaseValues()
    {
        if (backgroundBase != null)
        {
            baseBackgroundColor = backgroundBase.color;
        }

        if (dustParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                dustParticles.emission;
            ParticleSystem.MainModule main =
                dustParticles.main;

            baseDustEmission = emission.rateOverTimeMultiplier;
            baseDustSize = main.startSizeMultiplier;
        }

        if (glowParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                glowParticles.emission;
            ParticleSystem.MainModule main =
                glowParticles.main;

            baseGlowEmission = emission.rateOverTimeMultiplier;
            baseGlowSize = main.startSizeMultiplier;
        }
    }

    private void ApplyRuntimeValues(RuntimeValues values)
    {
        if (backgroundBase != null)
        {
            Color color = baseBackgroundColor;

            color.r = Mathf.Clamp01(
                baseBackgroundColor.r * values.backgroundBrightness);
            color.g = Mathf.Clamp01(
                baseBackgroundColor.g * values.backgroundBrightness);
            color.b = Mathf.Clamp01(
                baseBackgroundColor.b * values.backgroundBrightness);

            backgroundBase.color = color;
        }

        if (fogLayers != null)
        {
            for (int i = 0; i < fogLayers.Length; i++)
            {
                DreamFogDrift fog = fogLayers[i];

                if (fog != null)
                {
                    fog.SetRuntimeIntensity(
                        values.fogAlphaMultiplier,
                        values.fogSpeedMultiplier,
                        values.fogAmplitudeMultiplier);
                }
            }
        }

        if (dustParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                dustParticles.emission;
            emission.rateOverTimeMultiplier =
                baseDustEmission * values.dustEmissionMultiplier;

            ParticleSystem.MainModule main =
                dustParticles.main;
            main.startSizeMultiplier =
                baseDustSize * values.dustSizeMultiplier;
        }

        if (glowParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                glowParticles.emission;
            emission.rateOverTimeMultiplier =
                baseGlowEmission * values.glowEmissionMultiplier;

            ParticleSystem.MainModule main =
                glowParticles.main;
            main.startSizeMultiplier =
                baseGlowSize * values.glowSizeMultiplier;
        }
    }

    private struct RuntimeValues
    {
        public float backgroundBrightness;
        public float fogAlphaMultiplier;
        public float fogSpeedMultiplier;
        public float fogAmplitudeMultiplier;
        public float dustEmissionMultiplier;
        public float dustSizeMultiplier;
        public float glowEmissionMultiplier;
        public float glowSizeMultiplier;

        public static RuntimeValues Default =>
            new RuntimeValues
            {
                backgroundBrightness = 1f,
                fogAlphaMultiplier = 1f,
                fogSpeedMultiplier = 1f,
                fogAmplitudeMultiplier = 1f,
                dustEmissionMultiplier = 1f,
                dustSizeMultiplier = 1f,
                glowEmissionMultiplier = 1f,
                glowSizeMultiplier = 1f
            };

        public static RuntimeValues FromStage(
            DreamBackgroundStageSettings stage)
        {
            return new RuntimeValues
            {
                backgroundBrightness = stage.backgroundBrightness,
                fogAlphaMultiplier = stage.fogAlphaMultiplier,
                fogSpeedMultiplier = stage.fogSpeedMultiplier,
                fogAmplitudeMultiplier = stage.fogAmplitudeMultiplier,
                dustEmissionMultiplier = stage.dustEmissionMultiplier,
                dustSizeMultiplier = stage.dustSizeMultiplier,
                glowEmissionMultiplier = stage.glowEmissionMultiplier,
                glowSizeMultiplier = stage.glowSizeMultiplier
            };
        }

        public static RuntimeValues Lerp(
            RuntimeValues from,
            RuntimeValues to,
            float t)
        {
            return new RuntimeValues
            {
                backgroundBrightness =
                    Mathf.Lerp(from.backgroundBrightness, to.backgroundBrightness, t),
                fogAlphaMultiplier =
                    Mathf.Lerp(from.fogAlphaMultiplier, to.fogAlphaMultiplier, t),
                fogSpeedMultiplier =
                    Mathf.Lerp(from.fogSpeedMultiplier, to.fogSpeedMultiplier, t),
                fogAmplitudeMultiplier =
                    Mathf.Lerp(from.fogAmplitudeMultiplier, to.fogAmplitudeMultiplier, t),
                dustEmissionMultiplier =
                    Mathf.Lerp(from.dustEmissionMultiplier, to.dustEmissionMultiplier, t),
                dustSizeMultiplier =
                    Mathf.Lerp(from.dustSizeMultiplier, to.dustSizeMultiplier, t),
                glowEmissionMultiplier =
                    Mathf.Lerp(from.glowEmissionMultiplier, to.glowEmissionMultiplier, t),
                glowSizeMultiplier =
                    Mathf.Lerp(from.glowSizeMultiplier, to.glowSizeMultiplier, t)
            };
        }
    }
}
