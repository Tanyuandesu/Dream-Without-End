using UnityEngine;

/// <summary>
/// 玩家進入出口後通知 GameManager 切換樓層。
/// </summary>
public sealed class RuntimeDungeonExit : MonoBehaviour
{
    private GameManager gameManager;
    private bool activated;

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || !other.CompareTag("Player"))
        {
            return;
        }

        activated = true;

        if (gameManager != null)
        {
            gameManager.PlayerReachedExit();
        }
    }
}
