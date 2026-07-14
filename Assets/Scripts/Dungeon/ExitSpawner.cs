using UnityEngine;

/// <summary>
/// 只負責建立出口物件。
/// </summary>
[DisallowMultipleComponent]
public sealed class ExitSpawner : MonoBehaviour
{
    [Header("出口")]
    [SerializeField] private float visualScale = 0.75f;
    [SerializeField] private Color exitColor =
        new Color(1f, 0.78f, 0.12f);

    public GameObject Spawn(
        Vector2Int exitCell,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        GameManager gameManager)
    {
        GameObject exit = dungeonRenderer.CreateSquare(
            "Exit",
            exitCell,
            exitColor,
            dungeonRoot,
            10,
            false,
            visualScale);

        CircleCollider2D trigger =
            exit.AddComponent<CircleCollider2D>();

        trigger.isTrigger = true;
        trigger.radius = 0.5f;

        RuntimeDungeonExit exitController =
            exit.AddComponent<RuntimeDungeonExit>();

        exitController.Initialize(gameManager);

        return exit;
    }
}
