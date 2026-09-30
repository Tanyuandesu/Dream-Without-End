using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-precision prefab visual adapter. Does not change room geometry,
/// socket GameObjects, collision, navigation or any serialized prefab asset.
/// Uses the prefab's own occupied/blocked data; never regenerates the room.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrototypeRoomAppearance : MonoBehaviour
{
    private const string OverlayName = "PrototypeGeometry_Runtime";
    private static readonly Color Floor = new Color(0.20f, 0.25f, 0.29f, 1f);
    private static readonly Color Obstacle = new Color(0.65f, 0.43f, 0.31f, 1f);
    private static readonly Color Boundary = new Color(0.38f, 0.46f, 0.53f, 1f);

    private DreamRoomTemplate template;
    private SystemSettingsManager settings;
    private SpriteRenderer[] originalRenderers;
    private bool[] originalEnabled;
    private GameObject geometryRoot;

    public void Configure(DreamRoomTemplate instance)
    {
        template = instance;
    }

    private void OnEnable()
    {
        if (template == null) template = GetComponent<DreamRoomTemplate>();
        if (template == null ||
            template.RoomFidelityTier != DreamRoomFidelityTier.HighPrecision)
            return;

        settings = SystemSettingsManager.GetOrCreate();
        if (settings == null) return;
        settings.VisualModeChanged += ApplyMode;
        ApplyMode(settings.VisualMode);
    }

    private void OnDisable()
    {
        if (settings != null) settings.VisualModeChanged -= ApplyMode;
    }

    private void ApplyMode(GameVisualMode mode)
    {
        // Only capture native visual renderers once. Collider owners, including
        // Visual/Objects/ClosedBlockers, remain active in both modes.
        if (originalRenderers == null)
        {
            Transform visual = template.VisualRoot != null
                ? template.VisualRoot
                : transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogWarning("[Prototype] Missing visual root: " + name, this);
                return;
            }
            originalRenderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            originalEnabled = new bool[originalRenderers.Length];
            for (int i = 0; i < originalRenderers.Length; ++i)
                originalEnabled[i] = originalRenderers[i] != null &&
                                     originalRenderers[i].enabled;
        }

        bool prototype = mode == GameVisualMode.Prototype;
        // Create geometry before hiding formal sprites: a failed build must
        // leave the original visual intact instead of creating an invisible room.
        if (prototype && geometryRoot == null && !BuildGeometry()) return;

        for (int i = 0; i < originalRenderers.Length; ++i)
        {
            if (originalRenderers[i] != null)
                originalRenderers[i].enabled = !prototype && originalEnabled[i];
        }
        if (geometryRoot != null) geometryRoot.SetActive(prototype);
    }

    private bool BuildGeometry()
    {
        Vector2Int size = template.SizeInCells;
        if (size.x <= 0 || size.y <= 0 || size.x > 64 || size.y > 64)
        {
            Debug.LogWarning("[Prototype] Invalid room grid: " + name, this);
            return false;
        }

        GameObject root = new GameObject(OverlayName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        Sprite sprite = ProceduralSpriteUtility.GetWhiteSprite();
        try
        {
            for (int y = 0; y < size.y; ++y)
            {
                for (int x = 0; x < size.x; ++x)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!template.IsOccupiedCell(cell)) continue;
                    Vector3 position = template.GetLocalCellCenter(cell);
                    CreateTile(root.transform, sprite, "Floor", position,
                        new Vector2(1.015f, 1.015f), Floor, -10);
                    if (!template.IsBlockedCell(cell)) continue;
                    bool atEdge = x == 0 || y == 0 ||
                                  x == size.x - 1 || y == size.y - 1;
                    CreateTile(root.transform, sprite, "Blocked", position,
                        new Vector2(0.98f, 0.98f), atEdge ? Boundary : Obstacle, 2);
                }
            }

            // Perimeter edge strips clarify walls without inventing furniture.
            // Open socket cells are removed only from the VISUAL boundary.
            // Real door blockers and collision are still owned by the prefab.
            HashSet<Vector2Int> north = new HashSet<Vector2Int>();
            HashSet<Vector2Int> east = new HashSet<Vector2Int>();
            HashSet<Vector2Int> south = new HashSet<Vector2Int>();
            HashSet<Vector2Int> west = new HashSet<Vector2Int>();
            IReadOnlyList<DreamRoomDoorSocket> sockets = template.DoorSockets;
            if (sockets != null)
            {
                for (int i = 0; i < sockets.Count; ++i)
                {
                    DreamRoomDoorSocket socket = sockets[i];
                    if (socket == null || !socket.IsOpen) continue;
                    HashSet<Vector2Int> target;
                    switch (socket.Direction)
                    {
                        case DreamRoomDoorDirection.North: target = north; break;
                        case DreamRoomDoorDirection.East: target = east; break;
                        case DreamRoomDoorDirection.South: target = south; break;
                        default: target = west; break;
                    }
                    List<Vector2Int> cells = socket.GetLocalInsideCells();
                    for (int c = 0; c < cells.Count; ++c) target.Add(cells[c]);
                }
            }
            for (int x = 0; x < size.x; ++x)
            {
                Vector2Int bottom = new Vector2Int(x, 0);
                Vector2Int top = new Vector2Int(x, size.y - 1);
                if (template.IsOccupiedCell(bottom) && !south.Contains(bottom))
                    CreateTile(root.transform, sprite, "SouthWall",
                        template.GetLocalCellCenter(bottom) + Vector3.down * 0.5f,
                        new Vector2(1f, 0.22f), Boundary, 3);
                if (template.IsOccupiedCell(top) && !north.Contains(top))
                    CreateTile(root.transform, sprite, "NorthWall",
                        template.GetLocalCellCenter(top) + Vector3.up * 0.5f,
                        new Vector2(1f, 0.22f), Boundary, 3);
            }
            for (int y = 0; y < size.y; ++y)
            {
                Vector2Int left = new Vector2Int(0, y);
                Vector2Int right = new Vector2Int(size.x - 1, y);
                if (template.IsOccupiedCell(left) && !west.Contains(left))
                    CreateTile(root.transform, sprite, "WestWall",
                        template.GetLocalCellCenter(left) + Vector3.left * 0.5f,
                        new Vector2(0.22f, 1f), Boundary, 3);
                if (template.IsOccupiedCell(right) && !east.Contains(right))
                    CreateTile(root.transform, sprite, "EastWall",
                        template.GetLocalCellCenter(right) + Vector3.right * 0.5f,
                        new Vector2(0.22f, 1f), Boundary, 3);
            }
            geometryRoot = root;
            return true;
        }
        catch (System.Exception error)
        {
            Debug.LogError("[Prototype] Geometry creation failed in " + name + ": " + error, this);
            Destroy(root);
            return false;
        }
    }

    private static void CreateTile(Transform parent, Sprite sprite, string label,
        Vector3 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject tile = new GameObject(label);
        tile.transform.SetParent(parent, false);
        tile.transform.localPosition = position;
        tile.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }
}
