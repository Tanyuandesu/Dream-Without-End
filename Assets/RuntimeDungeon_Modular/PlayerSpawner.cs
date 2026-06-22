using UnityEngine;

/// <summary>
/// 只負責建立玩家物件與安裝最小移動組件。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("玩家")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float visualScale = 0.68f;
    [SerializeField] private Color playerColor =
        new Color(0.25f, 0.95f, 0.65f);

    public GameObject Spawn(
        Vector2Int spawnCell,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer)
    {
        GameObject player = dungeonRenderer.CreateSquare(
            "Player",
            spawnCell,
            playerColor,
            dungeonRoot,
            20,
            false,
            visualScale);

        player.tag = "Player";

        BoxCollider2D playerCollider =
            player.AddComponent<BoxCollider2D>();

        playerCollider.size = Vector2.one;

        Rigidbody2D rigidbody2D =
            player.AddComponent<Rigidbody2D>();

        rigidbody2D.gravityScale = 0f;
        rigidbody2D.freezeRotation = true;
        rigidbody2D.interpolation =
            RigidbodyInterpolation2D.Interpolate;
        rigidbody2D.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;

        RuntimeDungeonPlayer controller =
            player.AddComponent<RuntimeDungeonPlayer>();

        controller.Initialize(moveSpeed);

        return player;
    }
}
