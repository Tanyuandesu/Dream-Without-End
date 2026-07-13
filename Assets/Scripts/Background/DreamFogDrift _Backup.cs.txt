using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DreamFogDrift : MonoBehaviour
{
    [Header("漂移")]
    [SerializeField] private Vector2 driftAmplitude = new Vector2(0.35f, 0.22f);
    [SerializeField] private Vector2 driftSpeed = new Vector2(0.06f, 0.04f);

    [Header("呼吸透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float baseAlpha = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float alphaPulseAmount = 0.04f;

    [Min(0f)]
    [SerializeField] private float alphaPulseSpeed = 0.45f;

    [Header("可選縮放呼吸")]
    [SerializeField] private bool enableScalePulse = true;

    [Range(0f, 0.1f)]
    [SerializeField] private float scalePulseAmount = 0.015f;

    [Min(0f)]
    [SerializeField] private float scalePulseSpeed = 0.25f;

    [Header("相位")]
    [SerializeField] private float phaseOffset = 0f;

    private SpriteRenderer spriteRenderer;
    private Vector3 initialLocalPosition;
    private Vector3 initialLocalScale;
    private Color initialColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
        initialLocalScale = transform.localScale;
        initialColor = spriteRenderer.color;
    }

    private void Update()
    {
        float t = Time.unscaledTime + phaseOffset;

        float x = Mathf.Sin(t * driftSpeed.x) * driftAmplitude.x;
        float y = Mathf.Cos(t * driftSpeed.y) * driftAmplitude.y;

        transform.localPosition =
            initialLocalPosition + new Vector3(x, y, 0f);

        float alpha =
            baseAlpha + Mathf.Sin(t * alphaPulseSpeed) * alphaPulseAmount;

        Color color = initialColor;
        color.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = color;

        if (enableScalePulse)
        {
            float scale =
                1f + Mathf.Sin(t * scalePulseSpeed) * scalePulseAmount;

            transform.localScale =
                initialLocalScale * scale;
        }
    }

    private void OnValidate()
    {
        baseAlpha = Mathf.Clamp01(baseAlpha);
        alphaPulseAmount = Mathf.Clamp01(alphaPulseAmount);
        alphaPulseSpeed = Mathf.Max(0f, alphaPulseSpeed);
        scalePulseAmount = Mathf.Clamp(scalePulseAmount, 0f, 0.1f);
        scalePulseSpeed = Mathf.Max(0f, scalePulseSpeed);
    }
}
