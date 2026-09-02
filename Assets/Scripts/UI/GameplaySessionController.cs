using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplaySessionController : MonoBehaviour
{
    [Header("玩家")]
    [SerializeField] private PlayerManager playerManager;

    [Header("死亡流程")]
    [Min(0f)]
    [SerializeField] private float returnToTitleDelay = 1.25f;
    [SerializeField] private bool freezeGameOnDeath = true;

    [Tooltip("可選。拖入死亡提示 Panel。")]
    [SerializeField] private GameObject deathOverlay;

    [Header("可選：死亡後停用")]
    [Tooltip("可拖入 GameManager，避免死亡延遲期間仍可按 R。")]
    [SerializeField] private MonoBehaviour gameplayInputController;

    private bool deathHandled;
    private GameFlowManager flowManager;

    private void Reset()
    {
        playerManager = FindFirstObjectByType<PlayerManager>();
    }

    private void Awake()
    {
        if (playerManager == null)
        {
            playerManager = FindFirstObjectByType<PlayerManager>();
        }

        flowManager = GameFlowManager.GetOrCreate();

        if (deathOverlay != null)
        {
            deathOverlay.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (playerManager != null)
        {
            playerManager.PlayerDied += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (playerManager != null)
        {
            playerManager.PlayerDied -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied(Health playerHealth)
    {
        if (deathHandled)
        {
            return;
        }

        if (flowManager == null)
        {
            flowManager = GameFlowManager.GetOrCreate();
        }

        if (!flowManager.TryBeginGameOver())
        {
            return;
        }

        deathHandled = true;

        if (deathOverlay != null)
        {
            deathOverlay.SetActive(true);
        }

        if (gameplayInputController != null)
        {
            gameplayInputController.enabled = false;
        }

        if (freezeGameOnDeath)
        {
            Time.timeScale = 0f;
        }

        flowManager.ReturnToTitleAfter(returnToTitleDelay);
    }
}
