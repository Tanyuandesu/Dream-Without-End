using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime dialogue UI for SYS14.
/// Uses unscaled time because dialogue deliberately freezes gameplay time.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcDialogueController : MonoBehaviour
{
    private const int CanvasSortOrder = 5100;
    private const float CharactersPerSecond = 34f;

    private GameObject canvasRoot;
    private GameObject promptRoot;
    private TMP_Text promptText;
    private GameObject dialogueRoot;
    private TMP_Text nameText;
    private TMP_Text bodyText;
    private TMP_Text continueText;

    private LocalizationManager localization;
    private LocalizationFontProfile fontProfile;
    private GameFlowManager flowManager;

    private readonly List<string> resolvedLines = new List<string>();
    private int lineIndex;
    private int visibleCharacterCount;
    private float typeAccumulator;
    private int beginFrame = -1;
    private bool active;
    private float previousTimeScale = 1f;
    private Action completion;

    public bool IsDialogueActive => active;

    private void Awake()
    {
        localization = LocalizationManager.GetOrCreate();
        fontProfile = LocalizationFontProfile.LoadRuntimeProfile();
        flowManager = GameFlowManager.GetOrCreate();
        BuildRuntimeUi();
        SetPromptVisible(false);
        SetDialogueVisible(false);
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        AdvanceTypewriter();

        if (Time.frameCount == beginFrame ||
            !Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        string currentLine = resolvedLines[lineIndex];

        if (visibleCharacterCount < currentLine.Length)
        {
            visibleCharacterCount = currentLine.Length;
            ApplyVisibleCharacters();
            return;
        }

        if (lineIndex + 1 < resolvedLines.Count)
        {
            lineIndex++;
            visibleCharacterCount = 0;
            typeAccumulator = 0f;
            ApplyCurrentLine();
            return;
        }

        FinishDialogue(true);
    }

    public void ShowInteractionPrompt(bool visible)
    {
        if (promptRoot == null)
        {
            BuildRuntimeUi();
        }

        if (promptText != null && visible)
        {
            ApplyLocalizedText(promptText, "NPC_UI_TALK");
        }

        SetPromptVisible(visible && !active);
    }

    public bool TryBeginDialogue(
        NpcDefinition definition,
        NpcDialogueEntry entry,
        Action onCompleted)
    {
        if (active || definition == null || entry == null)
        {
            return false;
        }

        flowManager = flowManager != null
            ? flowManager
            : GameFlowManager.GetOrCreate();

        if (flowManager == null || !flowManager.TryBeginDialogue())
        {
            return false;
        }

        resolvedLines.Clear();
        IReadOnlyList<string> keys = entry.LineKeys;

        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            resolvedLines.Add(
                localization != null
                    ? localization.GetText(key)
                    : "[MISSING: " + key + "]");
        }

        if (resolvedLines.Count == 0)
        {
            flowManager.TryEndDialogue();
            return false;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        active = true;
        completion = onCompleted;
        beginFrame = Time.frameCount;
        lineIndex = 0;
        visibleCharacterCount = 0;
        typeAccumulator = 0f;

        ApplyLocalizedText(nameText, definition.DisplayNameKey);
        ApplyCurrentLine();
        ApplyLocalizedText(continueText, "NPC_UI_CONTINUE");
        SetPromptVisible(false);
        SetDialogueVisible(true);
        return true;
    }

    public void CancelDialogue()
    {
        if (!active)
        {
            return;
        }

        FinishDialogue(false);
    }

    private void AdvanceTypewriter()
    {
        if (lineIndex < 0 || lineIndex >= resolvedLines.Count)
        {
            return;
        }

        string currentLine = resolvedLines[lineIndex];
        if (visibleCharacterCount >= currentLine.Length)
        {
            return;
        }

        typeAccumulator += Time.unscaledDeltaTime * CharactersPerSecond;
        int add = Mathf.FloorToInt(typeAccumulator);
        if (add <= 0)
        {
            return;
        }

        typeAccumulator -= add;
        visibleCharacterCount = Mathf.Min(
            currentLine.Length,
            visibleCharacterCount + add);
        ApplyVisibleCharacters();
    }

    private void ApplyCurrentLine()
    {
        if (bodyText == null ||
            lineIndex < 0 ||
            lineIndex >= resolvedLines.Count)
        {
            return;
        }

        bodyText.text = resolvedLines[lineIndex];
        bodyText.maxVisibleCharacters = 0;
        ApplyFont(bodyText);
    }

    private void ApplyVisibleCharacters()
    {
        if (bodyText != null)
        {
            bodyText.maxVisibleCharacters = visibleCharacterCount;
        }
    }

    private void FinishDialogue(bool invokeCompletion)
    {
        Action callback = completion;
        completion = null;
        active = false;
        resolvedLines.Clear();
        SetDialogueVisible(false);

        Time.timeScale = Mathf.Approximately(previousTimeScale, 0f)
            ? 1f
            : previousTimeScale;

        if (flowManager != null &&
            flowManager.State == GameFlowState.Dialogue)
        {
            flowManager.TryEndDialogue();
        }

        if (invokeCompletion)
        {
            callback?.Invoke();
        }
    }

    private void BuildRuntimeUi()
    {
        if (canvasRoot != null)
        {
            return;
        }

        canvasRoot = new GameObject("NpcDialogueUI_Runtime");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        promptRoot = CreatePanel(
            "TalkPrompt",
            canvasRoot.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 92f),
            new Vector2(310f, 64f),
            new Color(0.04f, 0.05f, 0.07f, 0.82f));

        promptText = CreateText(
            "TalkText",
            promptRoot.transform,
            28f,
            TextAlignmentOptions.Center,
            new Vector2(12f, 8f),
            new Vector2(-12f, -8f));

        dialogueRoot = CreatePanel(
            "DialoguePanel",
            canvasRoot.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 42f),
            new Vector2(1500f, 300f),
            new Color(0.025f, 0.03f, 0.045f, 0.94f));

        // Dialogue content uses explicit, non-overlapping vertical bands.
        // The previous stretch-offset layout let the name/body rectangles overlap,
        // which became especially visible with CJK fonts.
        nameText = CreateAnchoredText(
            "NpcName",
            dialogueRoot.transform,
            27f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(-96f, 42f));
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableWordWrapping = false;

        bodyText = CreateAnchoredText(
            "DialogueText",
            dialogueRoot.transform,
            32f,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -84f),
            new Vector2(-96f, 160f));
        bodyText.enableWordWrapping = true;

        continueText = CreateAnchoredText(
            "ContinueText",
            dialogueRoot.transform,
            22f,
            TextAlignmentOptions.BottomRight,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 18f),
            new Vector2(-96f, 36f));
        continueText.enableWordWrapping = false;
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        ApplyFont(text);
        return text;
    }

    private TMP_Text CreateAnchoredText(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        ApplyFont(text);
        return text;
    }

    private void ApplyLocalizedText(TMP_Text target, string key)
    {
        if (target == null)
        {
            return;
        }

        target.text = localization != null
            ? localization.GetText(key)
            : "[MISSING: " + key + "]";
        target.maxVisibleCharacters = int.MaxValue;
        ApplyFont(target);
    }

    private void ApplyFont(TMP_Text target)
    {
        if (target == null || fontProfile == null)
        {
            return;
        }

        GameLanguage language = localization != null
            ? localization.CurrentLanguage
            : SystemSettingsManager.DefaultLanguage;

        TMP_FontAsset resolved =
            fontProfile.ResolveFont(language, target.font);

        if (resolved != null)
        {
            target.font = resolved;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(visible);
        }
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(visible);
        }
    }
}
