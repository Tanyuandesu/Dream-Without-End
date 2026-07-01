using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleScreenController : MonoBehaviour
{
    [Header("主要按鈕")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    [Header("可選拓展按鈕")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("面板")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    private GameFlowManager flowManager;

    private void Awake()
    {
        flowManager = GameFlowManager.GetOrCreate();
    }

    private void OnEnable()
    {
        AddListeners();
        ShowMainMenu();
    }

    private void Start()
    {
        SelectStartButton();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    public void StartGame()
    {
        if (startButton != null)
        {
            startButton.interactable = false;
        }

        flowManager.StartNewGame();
    }

    public void QuitGame()
    {
        flowManager.QuitGame();
    }

    public void ShowMainMenu()
    {
        SetPanelActive(mainPanel, true);
        SetPanelActive(optionsPanel, false);
        SetPanelActive(creditsPanel, false);
        SelectStartButton();
    }

    public void ShowOptions()
    {
        SetPanelActive(mainPanel, false);
        SetPanelActive(optionsPanel, true);
        SetPanelActive(creditsPanel, false);
    }

    public void ShowCredits()
    {
        SetPanelActive(mainPanel, false);
        SetPanelActive(optionsPanel, false);
        SetPanelActive(creditsPanel, true);
    }

    private void AddListeners()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(ShowOptions);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCredits);

        if (optionsBackButton != null)
            optionsBackButton.onClick.AddListener(ShowMainMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(ShowMainMenu);
    }

    private void RemoveListeners()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(ShowOptions);

        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(ShowCredits);

        if (optionsBackButton != null)
            optionsBackButton.onClick.RemoveListener(ShowMainMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(ShowMainMenu);
    }

    private void SelectStartButton()
    {
        if (startButton == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(
            startButton.gameObject);
    }

    private static void SetPanelActive(
        GameObject panel,
        bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
