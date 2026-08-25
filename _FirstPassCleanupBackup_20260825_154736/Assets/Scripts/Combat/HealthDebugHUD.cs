using UnityEngine;

/// <summary>
/// 僅供目前測試使用的玩家 HP 顯示。
/// 正式 UI 完成後可以刪除。
/// </summary>
[DisallowMultipleComponent]
public sealed class HealthDebugHUD : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;

    private void Awake()
    {
        if (playerManager == null)
        {
            playerManager =
                GetComponent<PlayerManager>();
        }
    }

    private void OnGUI()
    {
        if (playerManager == null ||
            playerManager.CurrentPlayerObject == null)
        {
            return;
        }

        Health health =
            playerManager.CurrentPlayerObject
                .GetComponent<Health>();

        if (health == null)
        {
            return;
        }

        GUI.Box(
            new Rect(12f, 126f, 220f, 48f),
            string.Empty);

        GUI.Label(
            new Rect(24f, 138f, 200f, 24f),
            "Player HP: " +
            Mathf.CeilToInt(health.CurrentHealth) +
            " / " +
            Mathf.CeilToInt(health.MaxHealth));
    }
}
