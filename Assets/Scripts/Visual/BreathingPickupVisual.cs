using UnityEngine;

/// <summary>
/// 把临时道具表现升级为“呼吸光球”。
/// 只影响视觉：中心亮球 + 外层柔光晕 + 轻微呼吸。
/// 不修改拾取判定与道具数据。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BreathingPickupVisual : MonoBehaviour
{
    [Header("中心颜色")]
    [SerializeField] private Color dimCoreColor =
        new Color(0.78f, 0.82f, 0.88f, 0.95f);

    [SerializeField] private Color brightCoreColor =
        new Color(0.95f, 0.98f, 1f, 1f);

    [Header("外层光晕")]
    [SerializeField] private Color dimHaloColor =
        new Color(0.70f, 0.76f, 0.90f, 0.22f);

    [SerializeField] private Color brightHaloColor =
        new Color(0.88f, 0.93f, 1f, 0.42f);

    [Min(1f)]
    [SerializeField] private float haloScaleMultiplier = 1.8f;

    [Header("呼吸")]
    [Min(0.2f)]
    [SerializeField] private float pulsePeriod = 1.15f;

    [Range(0f, 0.2f)]
    [SerializeField] private float coreScaleAmplitude = 0.05f;

    [Range(0f, 0.3f)]
    [SerializeField] private float haloScaleAmplitude = 0.12f;

    [Range(-1f, 1f)]
    [SerializeField] private float phaseOffset;

    private SpriteRenderer coreRenderer;
    private SpriteRenderer haloRenderer;
    private Vector3 coreBaseScale;
    private Vector3 haloBaseScale;

    private void Awake()
    {
        coreRenderer = GetComponent<SpriteRenderer>();
        coreBaseScale = transform.localScale;

        if (coreRenderer != null)
        {
            coreRenderer.sprite =
                ProceduralSpriteUtility.GetSoftCircleSprite();
            coreRenderer.color = dimCoreColor;
        }

        EnsureHalo();
    }

    private void Update()
    {
        if (coreRenderer == null || haloRenderer == null)
        {
            return;
        }

        float safePeriod = Mathf.Max(0.2f, pulsePeriod);
        float cycle = (Time.time / safePeriod) + phaseOffset;
        float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
        float t = wave * 0.5f + 0.5f;

        coreRenderer.color = Color.Lerp(dimCoreColor, brightCoreColor, t);
        haloRenderer.color = Color.Lerp(dimHaloColor, brightHaloColor, t);

        float coreScale =
            1f + ((t - 0.5f) * 2f * coreScaleAmplitude);
        float haloScale =
            haloScaleMultiplier +
            ((t - 0.5f) * 2f * haloScaleAmplitude);

        transform.localScale = coreBaseScale * coreScale;
        haloRenderer.transform.localScale = Vector3.one * haloScale;
    }

    private void EnsureHalo()
    {
        Transform existingHalo = transform.Find("OrbHalo");
        GameObject haloObject;

        if (existingHalo != null)
        {
            haloObject = existingHalo.gameObject;
            haloRenderer = haloObject.GetComponent<SpriteRenderer>();
        }
        else
        {
            haloObject = new GameObject("OrbHalo");
            haloObject.transform.SetParent(transform, false);
            haloObject.transform.localPosition = Vector3.zero;
            haloObject.transform.localRotation = Quaternion.identity;
            haloRenderer = haloObject.AddComponent<SpriteRenderer>();
        }

        if (haloRenderer == null)
        {
            haloRenderer = haloObject.AddComponent<SpriteRenderer>();
        }

        haloRenderer.sprite = ProceduralSpriteUtility.GetSoftCircleSprite();
        haloRenderer.color = dimHaloColor;
        haloRenderer.sortingOrder = coreRenderer != null
            ? coreRenderer.sortingOrder - 1
            : 0;

        haloObject.transform.localScale = Vector3.one * haloScaleMultiplier;
        haloBaseScale = haloObject.transform.localScale;
    }
}
