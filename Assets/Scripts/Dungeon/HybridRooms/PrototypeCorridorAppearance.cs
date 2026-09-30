using UnityEngine;

/// <summary>
/// Re-skins already-created corridor squares; never replaces wall collider
/// GameObjects or changes the DungeonLayout. Supports future art sprites too.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrototypeCorridorAppearance : MonoBehaviour
{
    private struct Original
    {
        public SpriteRenderer Renderer;
        public Sprite Sprite;
        public Color Color;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;
    }

    private Sprite fallbackSprite;
    private Color plainColor;
    private bool isWall;
    private bool configured;
    private Original[] originals;
    private SystemSettingsManager settings;

    public void Configure(Sprite whiteSprite, Color baseColor, bool wall)
    {
        fallbackSprite = whiteSprite;
        plainColor = baseColor;
        isWall = wall;
        configured = true;
        if (isActiveAndEnabled) BindAndApply();
    }

    private void OnEnable()
    {
        if (configured) BindAndApply();
    }

    private void OnDisable()
    {
        if (settings != null) settings.VisualModeChanged -= Apply;
    }

    private void BindAndApply()
    {
        settings = SystemSettingsManager.GetOrCreate();
        if (settings == null) return;
        settings.VisualModeChanged -= Apply;
        settings.VisualModeChanged += Apply;
        if (originals == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            originals = new Original[renderers.Length];
            for (int i = 0; i < renderers.Length; ++i)
            {
                SpriteRenderer r = renderers[i];
                originals[i] = new Original
                {
                    Renderer = r,
                    Sprite = r.sprite,
                    Color = r.color,
                    LocalPosition = r.transform.localPosition,
                    LocalScale = r.transform.localScale
                };
            }
        }
        Apply(settings.VisualMode);
    }

    private void Apply(GameVisualMode mode)
    {
        if (originals == null) return;
        bool prototype = mode == GameVisualMode.Prototype;
        for (int i = 0; i < originals.Length; ++i)
        {
            Original original = originals[i];
            if (original.Renderer == null) continue;
            original.Renderer.sprite = prototype ? fallbackSprite : original.Sprite;
            original.Renderer.color = prototype ? plainColor : original.Color;
            // C2 wall visuals may have an inset: preserve full collider root,
            // restore native inset when returning to Normal.
            if (isWall && original.Renderer.transform.name == "WallVisual_C2")
            {
                original.Renderer.transform.localPosition = prototype
                    ? Vector3.zero : original.LocalPosition;
                original.Renderer.transform.localScale = prototype
                    ? Vector3.one : original.LocalScale;
            }
        }
    }
}
