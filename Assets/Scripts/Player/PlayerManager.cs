using System;
using UnityEngine;

/// <summary>
/// 管理玩家生命週期與跨樓層存在。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSpawner))]
public sealed class PlayerManager : MonoBehaviour
{
    [Header("玩家系統")]
    [SerializeField] private PlayerSpawner playerSpawner;

    private GameObject currentPlayer;
    private Health currentHealth;

    public GameObject CurrentPlayerObject => currentPlayer;

    public Transform CurrentPlayer =>
        currentPlayer != null
            ? currentPlayer.transform
            : null;

    public Health CurrentHealth => currentHealth;

    public event Action<Health> PlayerDied;

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

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

            BindHealth();
        }
        else
        {
            MoveExistingPlayer(spawnPosition);
        }

        return CurrentPlayer;
    }

    public void RevivePlayer()
    {
        if (currentHealth == null)
        {
            return;
        }

        currentHealth.Revive();

        RuntimeDungeonPlayer controller =
            currentPlayer.GetComponent<
                RuntimeDungeonPlayer>();

        if (controller != null)
        {
            controller.enabled = true;
        }

        Rigidbody2D body =
            currentPlayer.GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.simulated = true;
            body.WakeUp();
        }
    }

    private void BindHealth()
    {
        if (currentPlayer == null)
        {
            return;
        }

        if (currentHealth != null)
        {
            currentHealth.Died -= HandlePlayerDied;
        }

        currentHealth =
            currentPlayer.GetComponent<Health>();

        if (currentHealth != null)
        {
            currentHealth.Died += HandlePlayerDied;
        }
    }

    private void HandlePlayerDied(Health health)
    {
        RuntimeDungeonPlayer controller =
            currentPlayer.GetComponent<
                RuntimeDungeonPlayer>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody2D body =
            currentPlayer.GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        PlayerDied?.Invoke(health);

        Debug.Log("Player died.");
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

    private void OnDestroy()
    {
        if (currentHealth != null)
        {
            currentHealth.Died -= HandlePlayerDied;
        }
    }
}
