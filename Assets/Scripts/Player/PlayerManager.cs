using UnityEngine;

/// <summary>
/// 管理玩家的生命週期。
///
/// 玩家只建立一次，之後切換迷宮時只重新定位，
/// 不會跟著 GeneratedDungeon_Floor_X 一起被銷毀。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSpawner))]
public sealed class PlayerManager : MonoBehaviour
{
    [Header("玩家系統")]
    [SerializeField] private PlayerSpawner playerSpawner;

    private GameObject currentPlayer;

    public GameObject CurrentPlayerObject => currentPlayer;

    public Transform CurrentPlayer =>
        currentPlayer != null
            ? currentPlayer.transform
            : null;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// 玩家不存在時生成一次；已存在時移動到新出生點。
    /// </summary>
    public Transform PlacePlayer(
        Vector2Int spawnCell,
        DungeonRenderer dungeonRenderer)
    {
        if (dungeonRenderer == null)
        {
            Debug.LogError(
                "PlayerManager：DungeonRenderer 為空。");

            return null;
        }

        CacheComponents();

        Vector3 spawnPosition =
            dungeonRenderer.CellToWorld(spawnCell);

        if (currentPlayer == null)
        {
            currentPlayer = playerSpawner.Spawn(
                spawnPosition,
                transform);
        }
        else
        {
            MoveExistingPlayer(spawnPosition);
        }

        return currentPlayer != null
            ? currentPlayer.transform
            : null;
    }

    private void MoveExistingPlayer(Vector3 worldPosition)
    {
        currentPlayer.SetActive(true);
        currentPlayer.transform.SetParent(transform);
        currentPlayer.transform.position = worldPosition;

        Rigidbody2D body =
            currentPlayer.GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.position = worldPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
        }
    }

    private void CacheComponents()
    {
        if (playerSpawner == null)
        {
            playerSpawner =
                GetComponent<PlayerSpawner>();
        }
    }
}
