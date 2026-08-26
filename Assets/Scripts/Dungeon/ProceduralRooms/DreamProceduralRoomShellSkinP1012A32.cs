using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-3.2：中精度 Procedural Room 的 Shell / Floor / Wall / Transition 视觉层。
///
/// 核心边界：
/// 1. 只创建 SpriteRenderer，不创建 Collider2D。
/// 2. 不修改 DreamRoomPlacement / DungeonLayout / FloorCells / A*。
/// 3. 旧 Graybox 的 Visual SpriteRenderer 只在 Runtime Instance 上关闭。
/// 4. DoorBlocker、Wall Collider、Socket、R2B Collider 全部保留。
/// 5. 实际 Open Socket 决定边界墙缺口与 Corridor Transition。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class DreamProceduralRoomShellSkinP1012A32 :
    MonoBehaviour
{
    public const string ResourceThemePath =
        "DreamDungeon/Procedural/ProceduralMediumShellTheme";

    public const string ShellRootName =
        "RuntimeShellSkin_P10_12A3_2";

    public const string FloorRootName =
        "Floor";

    public const string WallsRootName =
        "Walls";

    public const string TransitionRootName =
        "SocketTransitions";

    private static Sprite fallbackSprite;

    private DreamRoomPlacement placement;
    private int roomIndex = -1;

    private DreamRoomTemplate roomTemplate;
    private DreamProceduralRoomShellThemeP1012A32
        theme;

    private bool prepared;
    private bool committed;
    private string lastFailureReason =
        string.Empty;

    private int floorRendererCount;
    private int wallRendererCount;
    private int transitionRendererCount;
    private int disabledLegacyVisualRendererCount;
    private int openSocketCount;

    private readonly HashSet<Vector2Int>
        openNorthCells =
            new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int>
        openEastCells =
            new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int>
        openSouthCells =
            new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int>
        openWestCells =
            new HashSet<Vector2Int>();

    public bool IsPrepared => prepared;
    public bool IsCommitted => committed;
    public string LastFailureReason =>
        lastFailureReason;
    public int RoomIndex => roomIndex;

    public int FloorRendererCount =>
        floorRendererCount;

    public int WallRendererCount =>
        wallRendererCount;

    public int TransitionRendererCount =>
        transitionRendererCount;

    public int DisabledLegacyVisualRendererCount =>
        disabledLegacyVisualRendererCount;

    public int OpenSocketCount =>
        openSocketCount;

    public string ThemeId =>
        theme == null
            ? "FallbackShellTheme"
            : theme.ThemeId;

    public void Prepare(
        DreamRoomPlacement sourcePlacement,
        int sourceRoomIndex)
    {
        if (sourcePlacement == null ||
            !sourcePlacement.HasRuntimeProceduralOverride)
        {
            throw new InvalidOperationException(
                "Shell Skin 需要已经提交 R2B Runtime Procedural Override 的 Placement。");
        }

        placement = sourcePlacement;
        roomIndex = sourceRoomIndex;
        prepared = true;
    }

    private void Start()
    {
        if (!prepared || committed)
        {
            return;
        }

        TryCommit();
    }

    public bool TryCommit()
    {
        lastFailureReason =
            string.Empty;

        if (!Application.isPlaying)
        {
            return Fail(
                "Shell Skin 只允许在 Play Mode 提交。");
        }

        if (!prepared ||
            placement == null ||
            !placement.HasRuntimeProceduralOverride)
        {
            return Fail(
                "Shell Skin 尚未 Prepare 或 Placement Override 已失效。");
        }

        roomTemplate =
            GetComponent<DreamRoomTemplate>();

        if (roomTemplate == null)
        {
            return Fail(
                "房间实例根节点缺少 DreamRoomTemplate。");
        }

        if (!CollectOpenSocketCells())
        {
            return false;
        }

        theme =
            Resources.Load<
                DreamProceduralRoomShellThemeP1012A32>(
                    ResourceThemePath);

        Transform oldRoot =
            transform.Find(
                ShellRootName);

        if (oldRoot != null)
        {
            Destroy(
                oldRoot.gameObject);
        }

        GameObject shellRootObject =
            new GameObject(
                ShellRootName);

        Transform shellRoot =
            shellRootObject.transform;

        shellRoot.SetParent(
            transform,
            false);

        shellRoot.localPosition =
            Vector3.zero;

        shellRoot.localRotation =
            Quaternion.identity;

        shellRoot.localScale =
            Vector3.one;

        Transform floorRoot =
            CreateChildRoot(
                FloorRootName,
                shellRoot);

        Transform wallsRoot =
            CreateChildRoot(
                WallsRootName,
                shellRoot);

        Transform transitionRoot =
            CreateChildRoot(
                TransitionRootName,
                shellRoot);

        System.Random random =
            new System.Random(
                unchecked(
                    placement.RuntimeProceduralSeed ^
                    0x6C8E9CF5));

        BuildFloor(
            floorRoot,
            random);

        BuildWalls(
            wallsRoot,
            random);

        BuildTransitions(
            transitionRoot,
            random);

        Collider2D[] shellColliders =
            shellRoot.GetComponentsInChildren<
                Collider2D>(
                    true);

        if (shellColliders.Length != 0)
        {
            return Fail(
                "Shell Skin Root 意外产生 Collider2D：" +
                shellColliders.Length);
        }

        disabledLegacyVisualRendererCount =
            DisableLegacyGrayboxVisualRenderers();

        committed = true;

        Debug.Log(
            "[P10.12A-3.2] Shell Skin COMMIT" +
            "\nRoomIndex=" +
            roomIndex +
            " | TemplateId=" +
            roomTemplate.TemplateId +
            " | ProceduralSeed=" +
            placement.RuntimeProceduralSeed +
            " | Theme=" +
            ThemeId +
            "\nFloorRenderers=" +
            floorRendererCount +
            " | WallRenderers=" +
            wallRendererCount +
            " | TransitionRenderers=" +
            transitionRendererCount +
            " | OpenSockets=" +
            openSocketCount +
            " | DisabledLegacyVisualRenderers=" +
            disabledLegacyVisualRendererCount +
            "\nShellColliderCount=0" +
            " | GeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=ShellThemeSpritePoolsOnly",
            this);

        return true;
    }

    public int CountEnabledLegacyVisualRenderers()
    {
        Transform visual =
            transform.Find("Visual");

        if (visual == null)
        {
            return 0;
        }

        SpriteRenderer[] renderers =
            visual.GetComponentsInChildren<
                SpriteRenderer>(
                    true);

        int enabled = 0;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (renderers[i] != null &&
                renderers[i].enabled)
            {
                enabled++;
            }
        }

        return enabled;
    }

    private void BuildFloor(
        Transform parent,
        System.Random random)
    {
        floorRendererCount = 0;

        Vector2Int size =
            roomTemplate.SizeInCells;

        for (int x = 0;
             x < size.x;
             x++)
        {
            for (int y = 0;
                 y < size.y;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);

                if (!roomTemplate.IsOccupiedCell(
                        cell))
                {
                    continue;
                }

                GameObject tile =
                    new GameObject(
                        "Floor_" +
                        x + "_" +
                        y);

                tile.transform.SetParent(
                    parent,
                    false);

                tile.transform.localPosition =
                    roomTemplate.GetLocalCellCenter(
                        cell);

                tile.transform.localRotation =
                    Quaternion.identity;

                float scale =
                    theme == null
                        ? 1.02f
                        : theme.FloorScale;

                tile.transform.localScale =
                    new Vector3(
                        scale,
                        scale,
                        1f);

                SpriteRenderer renderer =
                    tile.AddComponent<
                        SpriteRenderer>();

                Sprite sprite =
                    theme == null
                        ? null
                        : theme.PickFloor(
                            random);

                renderer.sprite =
                    sprite != null
                        ? sprite
                        : GetFallbackSprite();

                renderer.color =
                    sprite != null
                        ? Color.white
                        : GetFloorFallbackColor();

                renderer.sortingOrder =
                    theme == null
                        ? -10
                        : theme.FloorSortingOrder;

                floorRendererCount++;
            }
        }
    }

    private void BuildWalls(
        Transform parent,
        System.Random random)
    {
        wallRendererCount = 0;

        Vector2Int size =
            roomTemplate.SizeInCells;

        for (int x = 0;
             x < size.x;
             x++)
        {
            Vector2Int southCell =
                new Vector2Int(
                    x,
                    0);

            if (!openSouthCells.Contains(
                    southCell))
            {
                CreateWallSegment(
                    parent,
                    random,
                    southCell,
                    DreamRoomDoorDirection.South);
            }

            Vector2Int northCell =
                new Vector2Int(
                    x,
                    size.y - 1);

            if (!openNorthCells.Contains(
                    northCell))
            {
                CreateWallSegment(
                    parent,
                    random,
                    northCell,
                    DreamRoomDoorDirection.North);
            }
        }

        for (int y = 0;
             y < size.y;
             y++)
        {
            Vector2Int westCell =
                new Vector2Int(
                    0,
                    y);

            if (!openWestCells.Contains(
                    westCell))
            {
                CreateWallSegment(
                    parent,
                    random,
                    westCell,
                    DreamRoomDoorDirection.West);
            }

            Vector2Int eastCell =
                new Vector2Int(
                    size.x - 1,
                    y);

            if (!openEastCells.Contains(
                    eastCell))
            {
                CreateWallSegment(
                    parent,
                    random,
                    eastCell,
                    DreamRoomDoorDirection.East);
            }
        }
    }

    private void CreateWallSegment(
        Transform parent,
        System.Random random,
        Vector2Int boundaryCell,
        DreamRoomDoorDirection direction)
    {
        GameObject wall =
            new GameObject(
                "Wall_" +
                direction +
                "_" +
                boundaryCell.x +
                "_" +
                boundaryCell.y);

        wall.transform.SetParent(
            parent,
            false);

        Vector3 position =
            roomTemplate.GetLocalCellCenter(
                boundaryCell);

        Vector2Int outward =
            GetOutwardOffset(
                direction);

        position.x +=
            outward.x * 0.5f;

        position.y +=
            outward.y * 0.5f;

        wall.transform.localPosition =
            position;

        bool vertical =
            direction ==
                DreamRoomDoorDirection.East ||
            direction ==
                DreamRoomDoorDirection.West;

        wall.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                vertical
                    ? 90f
                    : 0f);

        float wallLength =
            theme == null
                ? 1.03f
                : theme.WallLength;

        float wallThickness =
            theme == null
                ? 0.22f
                : theme.WallThickness;

        wall.transform.localScale =
            new Vector3(
                wallLength,
                wallThickness,
                1f);

        SpriteRenderer renderer =
            wall.AddComponent<
                SpriteRenderer>();

        Sprite sprite =
            theme == null
                ? null
                : theme.PickWall(
                    random);

        renderer.sprite =
            sprite != null
                ? sprite
                : GetFallbackSprite();

        renderer.color =
            sprite != null
                ? Color.white
                : GetWallFallbackColor();

        renderer.sortingOrder =
            theme == null
                ? 5
                : theme.WallSortingOrder;

        wallRendererCount++;
    }

    private void BuildTransitions(
        Transform parent,
        System.Random random)
    {
        transitionRendererCount = 0;

        IReadOnlyList<DreamRoomDoorSocket>
            sockets =
                roomTemplate.DoorSockets;

        int insideDepth =
            theme == null
                ? 2
                : theme.TransitionInsideDepth;

        int outsideDepth =
            theme == null
                ? 1
                : theme.TransitionOutsideDepth;

        float scale =
            theme == null
                ? 1.03f
                : theme.TransitionScale;

        for (int i = 0;
             i < sockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                sockets[i];

            if (socket == null ||
                !socket.IsOpen)
            {
                continue;
            }

            Vector2Int inward =
                GetInwardOffset(
                    socket.Direction);

            Vector2Int outward =
                -inward;

            List<Vector2Int> doorCells =
                socket.GetLocalInsideCells();

            for (int c = 0;
                 c < doorCells.Count;
                 c++)
            {
                Vector2Int doorCell =
                    doorCells[c];

                // 门格本身。
                CreateTransitionTile(
                    parent,
                    random,
                    doorCell,
                    scale);

                // 门内安全带。
                for (int depth = 1;
                     depth <= insideDepth;
                     depth++)
                {
                    CreateTransitionTile(
                        parent,
                        random,
                        doorCell +
                        inward * depth,
                        scale);
                }

                // 向外覆盖 Corridor 起始区域，纯视觉，不带 Collider。
                for (int depth = 1;
                     depth <= outsideDepth;
                     depth++)
                {
                    CreateTransitionTile(
                        parent,
                        random,
                        doorCell +
                        outward * depth,
                        scale);
                }
            }
        }
    }

    private void CreateTransitionTile(
        Transform parent,
        System.Random random,
        Vector2Int localCell,
        float scale)
    {
        GameObject tile =
            new GameObject(
                "Transition_" +
                localCell.x +
                "_" +
                localCell.y +
                "_" +
                transitionRendererCount);

        tile.transform.SetParent(
            parent,
            false);

        tile.transform.localPosition =
            roomTemplate.GetLocalCellCenter(
                localCell);

        tile.transform.localRotation =
            Quaternion.identity;

        tile.transform.localScale =
            new Vector3(
                scale,
                scale,
                1f);

        SpriteRenderer renderer =
            tile.AddComponent<
                SpriteRenderer>();

        Sprite sprite =
            theme == null
                ? null
                : theme.PickTransition(
                    random);

        renderer.sprite =
            sprite != null
                ? sprite
                : GetFallbackSprite();

        renderer.color =
            sprite != null
                ? Color.white
                : GetTransitionFallbackColor();

        renderer.sortingOrder =
            theme == null
                ? -9
                : theme.TransitionSortingOrder;

        transitionRendererCount++;
    }

    private bool CollectOpenSocketCells()
    {
        openNorthCells.Clear();
        openEastCells.Clear();
        openSouthCells.Clear();
        openWestCells.Clear();

        IReadOnlyList<DreamRoomDoorSocket>
            sockets =
                roomTemplate.DoorSockets;

        if (sockets == null)
        {
            return Fail(
                "DoorSockets 为空。");
        }

        openSocketCount = 0;

        for (int i = 0;
             i < sockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                sockets[i];

            if (socket == null ||
                !socket.IsOpen)
            {
                continue;
            }

            openSocketCount++;

            List<Vector2Int> cells =
                socket.GetLocalInsideCells();

            HashSet<Vector2Int> target =
                GetOpenSet(
                    socket.Direction);

            for (int c = 0;
                 c < cells.Count;
                 c++)
            {
                target.Add(
                    cells[c]);
            }
        }

        if (openSocketCount == 0)
        {
            return Fail(
                "Shell Skin Start 时没有任何 Open Socket。" +
                " 说明 Renderer 门提交时序与 A3.2 契约不一致。");
        }

        return true;
    }

    private HashSet<Vector2Int> GetOpenSet(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return openNorthCells;

            case DreamRoomDoorDirection.East:
                return openEastCells;

            case DreamRoomDoorDirection.South:
                return openSouthCells;

            case DreamRoomDoorDirection.West:
                return openWestCells;

            default:
                return openNorthCells;
        }
    }

    private int DisableLegacyGrayboxVisualRenderers()
    {
        Transform visual =
            transform.Find("Visual");

        if (visual == null)
        {
            return 0;
        }

        SpriteRenderer[] renderers =
            visual.GetComponentsInChildren<
                SpriteRenderer>(
                    true);

        int disabled = 0;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                renderers[i];

            if (renderer == null ||
                !renderer.enabled)
            {
                continue;
            }

            renderer.enabled = false;
            disabled++;
        }

        return disabled;
    }

    private static Vector2Int GetInwardOffset(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return Vector2Int.down;

            case DreamRoomDoorDirection.East:
                return Vector2Int.left;

            case DreamRoomDoorDirection.South:
                return Vector2Int.up;

            case DreamRoomDoorDirection.West:
                return Vector2Int.right;

            default:
                return Vector2Int.zero;
        }
    }

    private static Vector2Int GetOutwardOffset(
        DreamRoomDoorDirection direction)
    {
        return
            -GetInwardOffset(
                direction);
    }

    private static Transform CreateChildRoot(
        string name,
        Transform parent)
    {
        GameObject child =
            new GameObject(name);

        Transform childTransform =
            child.transform;

        childTransform.SetParent(
            parent,
            false);

        childTransform.localPosition =
            Vector3.zero;

        childTransform.localRotation =
            Quaternion.identity;

        childTransform.localScale =
            Vector3.one;

        return childTransform;
    }

    private bool Fail(
        string reason)
    {
        lastFailureReason =
            string.IsNullOrWhiteSpace(reason)
                ? "Unknown failure."
                : reason;

        Debug.LogError(
            "[P10.12A-3.2] Shell Skin FAILED" +
            "\nRoomIndex=" +
            roomIndex +
            "\n" +
            lastFailureReason +
            "\nGeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | ProductionMainChanged=False",
            this);

        return false;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "P10_12A3_2_ShellDebugTexture";

        texture.hideFlags =
            HideFlags.HideAndDontSave;

        texture.SetPixel(
            0,
            0,
            Color.white);

        texture.Apply(
            false,
            true);

        fallbackSprite =
            Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f),
                new Vector2(
                    0.5f,
                    0.5f),
                1f);

        fallbackSprite.name =
            "P10_12A3_2_ShellDebugSprite";

        fallbackSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return fallbackSprite;
    }

    private Color GetFloorFallbackColor()
    {
        return
            theme == null
                ? new Color(
                    0.24f,
                    0.31f,
                    0.40f,
                    1f)
                : theme.FloorFallbackColor;
    }

    private Color GetWallFallbackColor()
    {
        return
            theme == null
                ? new Color(
                    0.10f,
                    0.13f,
                    0.18f,
                    1f)
                : theme.WallFallbackColor;
    }

    private Color GetTransitionFallbackColor()
    {
        return
            theme == null
                ? new Color(
                    0.30f,
                    0.38f,
                    0.46f,
                    1f)
                : theme.TransitionFallbackColor;
    }
}
