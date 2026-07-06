using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EndingScreenController : MonoBehaviour
{
    [Header("按鈕")]
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    private GameFlowManager flowManager;

    private void Awake()
    {
        flowManager =
            GameFlowManager.GetOrCreate();
    }

    private void OnEnable()
    {
        AddListeners();
    }

    private void Start()
    {
        SelectDefaultButton();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    public void ReturnToTitle()
    {
        flowManager.ReturnToTitle();
    }

    public void RestartDemo()
    {
        flowManager.StartNewGame();
    }

    public void QuitGame()
    {
        flowManager.QuitGame();
    }

    private void AddListeners()
    {
        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.AddListener(ReturnToTitle);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartDemo);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void RemoveListeners()
    {
        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveListener(ReturnToTitle);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartDemo);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void SelectDefaultButton()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        if (returnToTitleButton != null)
        {
            EventSystem.current.SetSelectedGameObject(
                returnToTitleButton.gameObject);
            return;
        }

        if (restartButton != null)
        {
            EventSystem.current.SetSelectedGameObject(
                restartButton.gameObject);
        }
    }
}
