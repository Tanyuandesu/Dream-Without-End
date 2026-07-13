using UnityEngine;

/// <summary>
/// 開發階段用的背景進度測試器。
/// 掛在 DreamBackgroundSystem 上，只在 Play Mode 測試使用。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamBackgroundProgressionDebug : MonoBehaviour
{
    [SerializeField]
    private DreamBackgroundProgressionController controller;

    [Header("測試方式")]
    [SerializeField] private bool showDebugButtons = true;
    [SerializeField] private bool enableKeyboardPreview = true;

    [Header("畫面按鈕位置")]
    [SerializeField] private Vector2 panelPosition =
        new Vector2(15f, 15f);

    private void Reset()
    {
        controller =
            GetComponent<DreamBackgroundProgressionController>();
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller =
                GetComponent<DreamBackgroundProgressionController>();
        }
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!enableKeyboardPreview || controller == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            ApplyPreview(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplyPreview(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyPreview(4);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            ApplyPreview(6);
        }
#endif
    }

    private void OnGUI()
    {
        if (!showDebugButtons || controller == null)
        {
            return;
        }

        const float width = 155f;
        const float buttonHeight = 32f;
        const float gap = 5f;

        Rect boxRect = new Rect(
            panelPosition.x,
            panelPosition.y,
            width + 20f,
            4f * buttonHeight + 5f * gap + 30f);

        GUI.Box(boxRect, "Background Stage");

        float x = panelPosition.x + 10f;
        float y = panelPosition.y + 28f;

        if (GUI.Button(
            new Rect(x, y, width, buttonHeight),
            "Stage 0"))
        {
            ApplyPreview(0);
        }

        y += buttonHeight + gap;

        if (GUI.Button(
            new Rect(x, y, width, buttonHeight),
            "Stage 2"))
        {
            ApplyPreview(2);
        }

        y += buttonHeight + gap;

        if (GUI.Button(
            new Rect(x, y, width, buttonHeight),
            "Stage 4"))
        {
            ApplyPreview(4);
        }

        y += buttonHeight + gap;

        if (GUI.Button(
            new Rect(x, y, width, buttonHeight),
            "Stage 6"))
        {
            ApplyPreview(6);
        }
    }

    public void PreviewStage0()
    {
        ApplyPreview(0);
    }

    public void PreviewStage2()
    {
        ApplyPreview(2);
    }

    public void PreviewStage4()
    {
        ApplyPreview(4);
    }

    public void PreviewStage6()
    {
        ApplyPreview(6);
    }

    private void ApplyPreview(int collectedCount)
    {
        if (controller == null)
        {
            Debug.LogWarning(
                "DreamBackgroundProgressionDebug：" +
                "尚未設定 Controller。");
            return;
        }

        controller.PreviewCollectedCount(collectedCount);

        Debug.Log(
            "Dream background preview count: " +
            collectedCount);
    }
}
