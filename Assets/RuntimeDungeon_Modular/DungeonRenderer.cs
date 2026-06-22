using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 DungeonLayout 的資料轉換成可見的地板、牆壁與碰撞體。
/// 目前每格生成一個 GameObject，方便觀察與學習。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRenderer : MonoBehaviour
{
    [Header("顯示")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Color floorColor =
        new Color(0.16f, 0.17f, 0.21f);
    [SerializeField] private Color wallColor =
        new Color(0.35f, 0.37f, 0.44f);

    private Sprite whiteSprite;

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

    private void Awake()
    {
        cellSize = Mathf.Max(0.25f, cellSize);
        CreateWhiteSprite();
    }

    public void Render(DungeonLayout layout, Transform dungeonRoot)
    {
        if (layout == null)
        {
            Debug.LogError("DungeonRenderer 收到空的 DungeonLayout。");
            return;
        }

        BuildFloors(layout, dungeonRoot);
        BuildWalls(layout, dungeonRoot);
    }

    private void BuildFloors(
        DungeonLayout layout,
        Transform dungeonRoot)
    {
        Transform floorRoot = new GameObject("Floors").transform;
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
        Transform wallRoot = new GameObject("Walls").transform;
        wallRoot.SetParent(dungeonRoot);

        HashSet<Vector2Int> wallCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int floorCell in layout.FloorCells)
        {
            for (int i = 0; i < EightDirections.Length; i++)
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
    /// PlayerSpawner 與 ExitSpawner 也可以使用同一個方形 Sprite 工廠。
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

        GameObject createdObject = new GameObject(objectName);
        createdObject.transform.SetParent(parent);
        createdObject.transform.position = CellToWorld(cell);
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
            new Texture2D(1, 1, TextureFormat.RGBA32, false);

        texture.name = "RuntimeDungeonWhiteTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        whiteSprite.name = "RuntimeDungeonWhiteSprite";
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
