using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 DungeonLayout 的数据转换成可见的地板、墙壁与碰撞体。
///
/// Phase 0：
/// 1. 完整保留现有的逐格程序化渲染。
/// 2. 增加未来混合 Prefab 房间模式的稳定入口。
/// 3. 混合模式尚未安装时，自动回退到旧地图，避免空白楼层。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRenderer : MonoBehaviour
{
    [Header("渲染模式")]
    [SerializeField]
    private DungeonRenderMode renderMode =
        DungeonRenderMode.ProceduralCells;

    [Tooltip(
        "Phase 0 中 HybridPrefabRooms 尚未接入正式生成器。" +
        "选择它时会安全回退到 ProceduralCells。")]
    [SerializeField]
    private bool logHybridFallback = true;

    [Header("显示")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Color floorColor =
        new Color(0.16f, 0.17f, 0.21f);
    [SerializeField] private Color wallColor =
        new Color(0.35f, 0.37f, 0.44f);

    private Sprite whiteSprite;
    private bool hybridFallbackWarningShown;

    private static readonly Vector2Int[] EightDirections =
    {
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1,  1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  1),
        new Vector2Int(-1, -1)
    };

    public float CellSize => cellSize;
    public DungeonRenderMode RenderMode => renderMode;

    private void Awake()
    {
        cellSize = Mathf.Max(0.25f, cellSize);
        CreateWhiteSprite();
    }

    public void Render(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        if (layout == null)
        {
            Debug.LogError(
                "DungeonRenderer 收到空的 DungeonLayout。",
                this);
            return;
        }

        if (dungeonRoot == null)
        {
            Debug.LogError(
                "DungeonRenderer 收到空的 dungeonRoot。",
                this);
            return;
        }

        switch (renderMode)
        {
            case DungeonRenderMode.HybridPrefabRooms:
                RenderHybridPrefabRoomsOrFallback(
                    layout,
                    dungeonRoot);
                break;

            case DungeonRenderMode.ProceduralCells:
            default:
                RenderProceduralCells(
                    layout,
                    dungeonRoot);
                break;
        }
    }

    /// <summary>
    /// 当前正式工作的旧地图路径。
    /// 后续混合模式的开发不会修改这个方法的职责。
    /// </summary>
    private void RenderProceduralCells(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        BuildFloors(layout, dungeonRoot);
        BuildWalls(layout, dungeonRoot);
    }

    /// <summary>
    /// Phase 0 的安全占位入口。
    /// 后续阶段会把这里替换为真正的房间 Prefab + 程序走廊渲染。
    /// 在那之前始终回退到旧地图，不允许生成空白楼层。
    /// </summary>
    private void RenderHybridPrefabRoomsOrFallback(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        if (logHybridFallback &&
            !hybridFallbackWarningShown)
        {
            Debug.LogWarning(
                "DungeonRenderer：HybridPrefabRooms 尚未安装。" +
                "本层已自动使用 ProceduralCells，" +
                "这是 Phase 0 的预期行为。",
                this);

            hybridFallbackWarningShown = true;
        }

        RenderProceduralCells(layout, dungeonRoot);
    }

    private void BuildFloors(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform floorRoot =
            new GameObject("Floors").transform;

        floorRoot.SetParent(dungeonRoot);

        foreach (Vector2Int cell in layout.FloorCells)
        {
            CreateSquare(
                "Floor_" + cell.x + "_" + cell.y,
                cell,
                floorColor,
                floorRoot,
                -10,
                false,
                1f);
        }
    }

    private void BuildWalls(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform wallRoot =
            new GameObject("Walls").transform;

        wallRoot.SetParent(dungeonRoot);

        HashSet<Vector2Int> wallCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int floorCell in layout.FloorCells)
        {
            for (int i = 0;
                 i < EightDirections.Length;
                 i++)
            {
                Vector2Int neighbour =
                    floorCell + EightDirections[i];

                if (!layout.FloorCells.Contains(neighbour))
                {
                    wallCells.Add(neighbour);
                }
            }
        }

        foreach (Vector2Int wallCell in wallCells)
        {
            CreateSquare(
                "Wall_" + wallCell.x + "_" + wallCell.y,
                wallCell,
                wallColor,
                wallRoot,
                0,
                true,
                1f);
        }
    }

    /// <summary>
    /// PlayerManager、EnemyManager、ItemManager 与 ExitSpawner
    /// 仍可继续使用这个公开工厂；Phase 0 不改变其签名。
    /// </summary>
    public GameObject CreateSquare(
        string objectName,
        Vector2Int cell,
        Color color,
        Transform parent,
        int sortingOrder,
        bool addCollider,
        float scaleMultiplier)
    {
        if (whiteSprite == null)
        {
            CreateWhiteSprite();
        }

        GameObject createdObject =
            new GameObject(objectName);

        createdObject.transform.SetParent(parent);
        createdObject.transform.position =
            CellToWorld(cell);

        createdObject.transform.localScale =
            new Vector3(
                cellSize * scaleMultiplier,
                cellSize * scaleMultiplier,
                1f);

        SpriteRenderer spriteRenderer =
            createdObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = whiteSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            BoxCollider2D collider =
                createdObject.AddComponent<BoxCollider2D>();

            collider.size = Vector2.one;
        }

        return createdObject;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            cell.x * cellSize,
            cell.y * cellSize,
            0f);
    }

    private void CreateWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return;
        }

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "RuntimeDungeonWhiteTexture";

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        whiteSprite.name =
            "RuntimeDungeonWhiteSprite";
    }

    private void OnDestroy()
    {
        if (whiteSprite == null)
        {
            return;
        }

        Texture2D texture = whiteSprite.texture;
        Destroy(whiteSprite);

        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
