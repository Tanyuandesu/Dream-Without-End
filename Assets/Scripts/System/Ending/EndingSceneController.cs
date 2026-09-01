using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SYS12 ending shell.
///
/// Current presentation:
/// - black background when no CG is assigned
/// - dialogue never auto-starts; first click starts it
/// - each further click advances it
/// - Return to Title appears only after dialogue completion
/// - returning to Title always requires a separate explicit button click
///
/// A future scene-authored controller can fill cgEntries and provide any
/// MonoBehaviour implementing IEndingDialogueBridge.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingSceneController : MonoBehaviour
{
    [Serializable]
    private sealed class EndingCgEntry
    {
        public string endingId =
            EndingResolver.DefaultEndingId;
        public Sprite cg;
        public string dialogueId =
            "ending_default";
    }

    [Header("未来 CG / 对话映射")]
    [SerializeField]
    private EndingCgEntry[] cgEntries =
        Array.Empty<EndingCgEntry>();

    private EndingRunData runData;
    private IEndingDialogueBridge dialogueBridge;
    private string dialogueId = "ending_default";

    private Canvas canvas;
    private Image cgImage;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI promptText;
    private Button advanceButton;
    private Button returnTitleButton;

    private bool dialogueStarted;
    private bool dialogueFinished;
    private bool returnTitleAvailable;
    private int placeholderLineIndex = -1;

    private readonly string[] placeholderLines =
    {
        "..."
    };

    private void Awake()
    {
        if (!EndingRunContext.TryConsume(out runData))
        {
            runData =
                EndingRunData.CreateDirectSceneFallback();

            Debug.LogWarning(
                "[SYS12-B4] EndingScene opened without EndingRunContext. " +
                "Using direct-scene fallback.",
                this);
        }

        EnsureEventSystem();
        BuildRuntimeUi();
        ResolvePresentation();
        ResolveDialogueBridge();

        Debug.Log(
            "[SYS12-B4] Ending shell ready" +
            " | EndingId=" + runData.endingId +
            " | CG=" +
            (cgImage != null && cgImage.sprite != null
                ? "Assigned"
                : "BlackFallback") +
            " | Dialogue=" +
            (dialogueBridge != null
                ? "Bridge"
                : "Placeholder") +
            " | AutoPlay=False" +
            " | AutoReturn=False",
            this);
    }

    private void ResolvePresentation()
    {
        EndingCgEntry selected = null;

        for (int i = 0; i < cgEntries.Length; i++)
        {
            EndingCgEntry entry = cgEntries[i];

            if (entry != null &&
                string.Equals(
                    entry.endingId,
                    runData.endingId,
                    StringComparison.Ordinal))
            {
                selected = entry;
                break;
            }
        }

        if (selected != null)
        {
            dialogueId =
                string.IsNullOrWhiteSpace(selected.dialogueId)
                    ? runData.endingId.ToLowerInvariant()
                    : selected.dialogueId.Trim();

            if (selected.cg != null)
            {
                cgImage.sprite = selected.cg;
                cgImage.gameObject.SetActive(true);
            }
        }
        else
        {
            dialogueId =
                string.IsNullOrWhiteSpace(runData.endingId)
                    ? "ending_default"
                    : runData.endingId.ToLowerInvariant();
        }
    }

    private void ResolveDialogueBridge()
    {
        MonoBehaviour[] behaviours =
            FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == this)
            {
                continue;
            }

            IEndingDialogueBridge candidate =
                behaviours[i] as IEndingDialogueBridge;

            if (candidate != null)
            {
                dialogueBridge = candidate;
                return;
            }
        }
    }

    public void AdvanceDialogue()
    {
        if (dialogueFinished)
        {
            return;
        }

        if (!dialogueStarted)
        {
            dialogueStarted = true;
            promptText.text = string.Empty;

            if (dialogueBridge != null)
            {
                EndingDialogueRequest request =
                    new EndingDialogueRequest(
                        runData.endingId,
                        dialogueId,
                        runData);

                dialogueBridge.Begin(
                    request,
                    HandleDialogueTextChanged,
                    HandleDialogueCompleted);
            }
            else
            {
                AdvancePlaceholderDialogue();
            }

            return;
        }

        if (dialogueBridge != null)
        {
            dialogueBridge.Advance();
            return;
        }

        AdvancePlaceholderDialogue();
    }

    public void ReturnToTitle()
    {
        if (!dialogueFinished || !returnTitleAvailable)
        {
            return;
        }

        if (returnTitleButton != null)
        {
            returnTitleButton.interactable = false;
        }

        GameFlowManager.GetOrCreate().ReturnToTitle();
    }

    private void AdvancePlaceholderDialogue()
    {
        placeholderLineIndex++;

        if (placeholderLineIndex >=
            placeholderLines.Length)
        {
            HandleDialogueCompleted();
            return;
        }

        HandleDialogueTextChanged(
            placeholderLines[placeholderLineIndex]);
    }

    private void HandleDialogueTextChanged(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text ?? string.Empty;
        }
    }

    private void HandleDialogueCompleted()
    {
        if (dialogueFinished)
        {
            return;
        }

        dialogueFinished = true;

        if (advanceButton != null)
        {
            advanceButton.interactable = false;
            advanceButton.gameObject.SetActive(false);
        }

        if (promptText != null)
        {
            promptText.text = string.Empty;
        }

        StartCoroutine(
            RevealReturnTitleOnNextFrame());

        Debug.Log(
            "[SYS12-B4] Ending dialogue complete" +
            " | ReturnTitleAvailable=NextFrame" +
            " | AutomaticReturn=False",
            this);
    }

    private IEnumerator RevealReturnTitleOnNextFrame()
    {
        // The click that completes the final dialogue line must never also
        // activate Return to Title. Revealing the button on the next frame
        // makes the required second, explicit click a hard interaction rule.
        yield return null;

        returnTitleAvailable = true;

        if (returnTitleButton != null)
        {
            returnTitleButton.gameObject.SetActive(true);
            returnTitleButton.interactable = true;
        }
    }

    private void BuildRuntimeUi()
    {
        GameObject canvasObject =
            new GameObject(
                "EndingUI_Runtime",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(
            transform,
            false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background =
            CreateUiObject(
                "BlackBackground",
                canvas.transform);

        StretchFull(
            background.GetComponent<RectTransform>());

        Image backgroundImage =
            background.AddComponent<Image>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;

        GameObject cgObject =
            CreateUiObject(
                "EndingCG",
                canvas.transform);

        StretchFull(
            cgObject.GetComponent<RectTransform>());

        cgImage = cgObject.AddComponent<Image>();
        cgImage.color = Color.white;
        cgImage.preserveAspect = true;
        cgImage.raycastTarget = false;
        cgImage.gameObject.SetActive(false);

        GameObject advanceObject =
            CreateUiObject(
                "DialogueAdvance",
                canvas.transform);

        StretchFull(
            advanceObject.GetComponent<RectTransform>());

        Image advanceImage =
            advanceObject.AddComponent<Image>();
        advanceImage.color = new Color(0f, 0f, 0f, 0f);
        advanceImage.raycastTarget = true;

        advanceButton =
            advanceObject.AddComponent<Button>();
        advanceButton.targetGraphic = advanceImage;
        advanceButton.onClick.AddListener(
            AdvanceDialogue);

        GameObject dialoguePanel =
            CreateUiObject(
                "DialoguePanel",
                canvas.transform);

        RectTransform dialoguePanelRect =
            dialoguePanel.GetComponent<RectTransform>();
        dialoguePanelRect.anchorMin =
            new Vector2(0.08f, 0.05f);
        dialoguePanelRect.anchorMax =
            new Vector2(0.92f, 0.30f);
        dialoguePanelRect.offsetMin = Vector2.zero;
        dialoguePanelRect.offsetMax = Vector2.zero;

        Image dialoguePanelImage =
            dialoguePanel.AddComponent<Image>();
        dialoguePanelImage.color =
            new Color(0f, 0f, 0f, 0.72f);
        dialoguePanelImage.raycastTarget = false;

        dialogueText =
            CreateText(
                "DialogueText",
                dialoguePanel.transform,
                34f,
                TextAlignmentOptions.Left);

        RectTransform dialogueRect =
            dialogueText.rectTransform;
        dialogueRect.anchorMin =
            new Vector2(0.04f, 0.18f);
        dialogueRect.anchorMax =
            new Vector2(0.96f, 0.86f);
        dialogueRect.offsetMin = Vector2.zero;
        dialogueRect.offsetMax = Vector2.zero;
        dialogueText.text = string.Empty;

        promptText =
            CreateText(
                "ClickPrompt",
                canvas.transform,
                22f,
                TextAlignmentOptions.Center);

        RectTransform promptRect =
            promptText.rectTransform;
        promptRect.anchorMin =
            promptRect.anchorMax =
                new Vector2(0.5f, 0.035f);
        promptRect.pivot =
            new Vector2(0.5f, 0f);
        promptRect.anchoredPosition =
            Vector2.zero;
        promptRect.sizeDelta =
            new Vector2(500f, 40f);
        promptText.text =
            "CLICK TO BEGIN";

        returnTitleButton =
            CreateButton(
                "ReturnTitleButton",
                canvas.transform,
                "Return to Title");

        RectTransform returnRect =
            returnTitleButton.GetComponent<RectTransform>();
        returnRect.anchorMin =
            returnRect.anchorMax =
                new Vector2(0.5f, 0.12f);
        returnRect.pivot =
            new Vector2(0.5f, 0.5f);
        returnRect.anchoredPosition =
            Vector2.zero;
        returnRect.sizeDelta =
            new Vector2(360f, 72f);

        returnTitleButton.onClick.AddListener(
            ReturnToTitle);
        returnTitleButton.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject =
            CreateUiObject(name, parent);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;

        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label)
    {
        GameObject buttonObject =
            CreateUiObject(name, parent);

        Image image =
            buttonObject.AddComponent<Image>();
        image.color =
            new Color(0.88f, 0.88f, 0.88f, 0.96f);

        Button button =
            buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text =
            CreateText(
                "Label",
                buttonObject.transform,
                26f,
                TextAlignmentOptions.Center);

        StretchFull(text.rectTransform);
        text.color = Color.black;
        text.text = label;

        return button;
    }

    private static GameObject CreateUiObject(
        string name,
        Transform parent)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform));

        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFull(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject =
            new GameObject(
                "EventSystem_Runtime",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

        eventSystemObject.transform.position =
            Vector3.zero;
    }
}

/// <summary>
/// Ensures an otherwise empty EndingScene is already functional.
/// If a handcrafted EndingSceneController exists in the scene, it wins.
/// </summary>
public static class EndingSceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneLoadHook()
    {
        // Remove first so Enter Play Mode Options / domain-reload settings
        // cannot accumulate duplicate callbacks between play sessions.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        EnsureController(scene);
    }

    private static void EnsureController(Scene scene)
    {
        string endingSceneName =
            GameFlowManager.Instance != null
                ? GameFlowManager.Instance.EndingSceneName
                : "EndingScene";

        if (scene.name != endingSceneName)
        {
            return;
        }

        EndingSceneController existing =
            UnityEngine.Object
                .FindObjectOfType<EndingSceneController>();

        if (existing != null)
        {
            return;
        }

        new GameObject(
            "EndingSceneController_Runtime")
            .AddComponent<EndingSceneController>();

        Debug.Log(
            "[SYS12-B4] EndingScene runtime shell installed" +
            " | Scene=" + scene.name);
    }
}
