using UnityEngine;

[DisallowMultipleComponent]
public sealed class DemoVictoryController : MonoBehaviour
{
    [Header("道具進度")]
    [SerializeField] private ItemManager itemManager;

    [Header("通關條件")]
    [SerializeField] private DemoVictoryConditionMode conditionMode =
        DemoVictoryConditionMode.CollectedItemCount;

    [Min(1)]
    [SerializeField] private int requiredValue = 7;

    [Header("通關流程")]
    [Min(0f)]
    [SerializeField] private float loadEndingDelay = 1.25f;

    [SerializeField] private bool freezeGameOnVictory = true;

    [Tooltip("可選。拖入 CLEAR / COMPLETE 類提示 Panel。")]
    [SerializeField] private GameObject victoryOverlay;

    [Header("可選：勝利後停用")]
    [Tooltip("可拖入 GameManager，避免勝利延遲期間仍可按 R。")]
    [SerializeField] private MonoBehaviour gameplayInputController;

    private bool victoryHandled;
    private GameFlowManager flowManager;

    private void Reset()
    {
        itemManager =
            FindObjectOfType<ItemManager>();
    }

    private void Awake()
    {
        if (itemManager == null)
        {
            itemManager =
                FindObjectOfType<ItemManager>();
        }

        if (itemManager == null)
        {
            Debug.LogError(
                "DemoVictoryController：找不到 ItemManager，" +
                "無法驗證或判定通關條件。",
                this);
        }
        else
        {
            itemManager.ValidateProgressionConfiguration(
                requiredValue,
                conditionMode ==
                DemoVictoryConditionMode.ProgressionScore);
        }

        flowManager =
            GameFlowManager.GetOrCreate();

        if (victoryOverlay != null)
        {
            victoryOverlay.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (itemManager != null)
        {
            itemManager.ItemCollected += HandleItemCollected;
            itemManager.ProgressChanged += HandleProgressChanged;
        }
    }

    private void Start()
    {
        CheckVictory(
            itemManager != null
                ? itemManager.CreateProgressSnapshot()
                : null);
    }

    private void OnDisable()
    {
        if (itemManager != null)
        {
            itemManager.ItemCollected -= HandleItemCollected;
            itemManager.ProgressChanged -= HandleProgressChanged;
        }
    }

    private void HandleItemCollected(
        ItemCollectedEvent collectedEvent)
    {
        CheckVictory(
            itemManager != null
                ? itemManager.CreateProgressSnapshot()
                : null);
    }

    private void HandleProgressChanged(
        ItemProgressSnapshot snapshot)
    {
        CheckVictory(snapshot);
    }

    private void CheckVictory(
        ItemProgressSnapshot snapshot)
    {
        if (victoryHandled ||
            snapshot == null)
        {
            return;
        }

        int currentValue =
            GetCurrentValue(snapshot);

        if (currentValue < requiredValue)
        {
            return;
        }

        TriggerVictory();
    }

    private int GetCurrentValue(
        ItemProgressSnapshot snapshot)
    {
        switch (conditionMode)
        {
            case DemoVictoryConditionMode.ProgressionScore:
                return snapshot.ProgressionScore;

            case DemoVictoryConditionMode.CollectedItemCount:
            default:
                return snapshot.CollectedCount;
        }
    }

    private void TriggerVictory()
    {
        if (victoryHandled)
        {
            return;
        }

        if (!flowManager.TryBeginVictory())
        {
            return;
        }

        victoryHandled = true;

        if (victoryOverlay != null)
        {
            victoryOverlay.SetActive(true);
        }

        if (gameplayInputController != null)
        {
            gameplayInputController.enabled = false;
        }

        if (freezeGameOnVictory)
        {
            Time.timeScale = 0f;
        }

        Debug.Log(
            "Demo Victory: required value reached. Loading ending scene.");

        flowManager.LoadEndingAfter(loadEndingDelay);
    }

    private void OnValidate()
    {
        requiredValue =
            Mathf.Max(1, requiredValue);

        loadEndingDelay =
            Mathf.Max(0f, loadEndingDelay);
    }
}
