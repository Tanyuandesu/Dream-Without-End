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
            bool committed =
                gameManager.TryPlayerReachedExit();

            if (!committed)
            {
                activated = false;

                Debug.Log(
                    "[RuntimeDungeonExit/R8.4] 楼层切换未提交，" +
                    "当前出口已重新待命；玩家离开后可再次进入。",
                    this);
            }
        }
    }
}
