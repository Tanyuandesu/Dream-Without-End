using UnityEngine;

/// <summary>
/// 只负责建立出口物件。
/// VIS-HF1：把纯色方块出口替换成简易向下梯子，提升可读性。
/// </summary>
[DisallowMultipleComponent]
public sealed class ExitSpawner : MonoBehaviour
{
    [Header("出口可读性热修")]
    [SerializeField] private int exitSortingOrder = 10;
    [SerializeField] private float triggerRadius = 0.42f;

    public GameObject Spawn(
        Vector2Int exitCell,
        Transform dungeonRoot,
        DungeonRenderer dungeonRenderer,
        GameManager gameManager)
    {
        GameObject exit = new GameObject("Exit");
        exit.transform.SetParent(dungeonRoot, false);
        exit.transform.position =
            dungeonRenderer.CellToWorld(exitCell);

        PixelLadderExitVisual visual =
            exit.AddComponent<PixelLadderExitVisual>();
        visual.Build(
            dungeonRenderer.CellSize,
            exitSortingOrder);

        CircleCollider2D trigger =
            exit.AddComponent<CircleCollider2D>();

        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, triggerRadius);

        RuntimeDungeonExit exitController =
            exit.AddComponent<RuntimeDungeonExit>();

        exitController.Initialize(gameManager);

        return exit;
    }
}
