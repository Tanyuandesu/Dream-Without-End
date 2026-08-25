using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.10：MusicRoom_01 正式几何落地。
///
/// 数据来源：用户已确认的 MusicRoom 16x11 V4 碰撞预览。
/// 只修改 Production_MusicRoom_01 Prefab 内：
/// - Blocked Cells
/// - Navigation/Colliders/Interior
/// - East / West Socket 的纵向位置
/// - 与 Socket 对应的 ClosedBlocker 位置
/// - Navigation/Colliders/Perimeter
///
/// 不修改任何图片、Foreground、Catalog、GameScene、DungeonGenerator、A*、Enemy AI。
/// </summary>
public static class DreamRoomMusicRoomGeometryP1010
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.10 MusicRoom Geometry/";

    private const string MusicRoomPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/MusicRoom_01/Room_MusicRoom_01.prefab";

    private const string ExpectedTemplateId = "Production_MusicRoom_01";
    private static readonly Vector2Int ExpectedSize = new Vector2Int(16, 11);

    private const float PerimeterWallThickness = 0.35f;
    private const float ClosedBlockerThickness = 0.35f;

    private struct Footprint
    {
        public readonly string Name;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public Footprint(string name, int x, int y, int width, int height)
        {
            Name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    // 坐标以房间左下角为 (0,0)。
    // 这些矩形的并集严格等于用户确认的 V4 红格，共 73 个 Blocked Cells。
    private static readonly Footprint[] ExpectedFootprints =
    {
        new Footprint("Structure_Top_16x2",       0, 9, 16, 2),
        new Footprint("Structure_UpperLeft_3x1",  0, 8,  3, 1),
        new Footprint("Structure_UpperRight_1x1",15, 8,  1, 1),

        new Footprint("Piano_2x2",               11, 7,  2, 2),
        new Footprint("ConductorPodium_2x1",      7, 7,  2, 1),
        new Footprint("BassStation_2x2",           3, 5,  2, 2),
        new Footprint("RightInstrumentStation_2x2",11,4, 2, 2),

        new Footprint("Structure_LeftSide_1x2",    0, 3,  1, 2),
        new Footprint("Structure_RightSide_1x2",  15, 3,  1, 2),

        new Footprint("CenterStand_Left_1x1",       7, 4,  1, 1),
        new Footprint("CenterStand_Right_1x1",      9, 4,  1, 1),
        new Footprint("FloorViolin_1x1",            6, 3,  1, 1),
        new Footprint("OpenViolinCase_2x1",        10, 2,  2, 1),

        new Footprint("Structure_BottomLeft_7x1",   0, 0,  7, 1),
        new Footprint("Structure_BottomRight_7x1",  9, 0,  7, 1),
    };

    [MenuItem(MenuRoot + "1. Apply Approved V4 Geometry", false, 2790)]
    private static void ApplyApprovedGeometry()
    {
        if (!CanEditPrefab())
        {
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(MusicRoomPrefabPath);
            DreamRoomTemplate template = RequireMusicRoomTemplate(root);

            Transform interior = root.transform.Find("Navigation/Colliders/Interior");
            Transform perimeter = root.transform.Find("Navigation/Colliders/Perimeter");

            if (interior == null || perimeter == null)
            {
                throw new InvalidOperationException(
                    "缺少 P10.7 标准几何层级 Navigation/Colliders/Interior 或 Perimeter。" );
            }

            // 1) 写入用户批准的 73 个 Blocked Cells。
            List<Vector2Int> blocked = BuildExpectedBlockedCells();
            WriteBlockedCells(template, blocked);

            // 2) Interior Collider 只以已批准格子为权威重建。
            DestroyAllChildren(interior);
            for (int i = 0; i < ExpectedFootprints.Length; i++)
            {
                CreateFootprintCollider(template, interior, ExpectedFootprints[i]);
            }

            // 3) V4 的左右门不在 Factory 默认中心位置。
            //    目标门格（左下原点）：West/East = y 6,7；South = x 7,8。
            ReconfigureSocketsAndBlockers(root, template);

            // 4) Socket 位置变化后重建 Perimeter，确保墙洞跟门一致。
            RebuildMusicRoomPerimeter(perimeter);

            template.RefreshDoorSockets();
            EditorUtility.SetDirty(template);
            PrefabUtility.SaveAsPrefabAsset(root, MusicRoomPrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "P10.10 Apply Failed",
                "MusicRoom_01 V4 几何写入中止。请查看 Console 第一条红色错误。",
                "OK");
            return;
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MusicRoomPrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log(
            "[P10.10] MusicRoom_01 已写入用户批准的 V4 几何。\n" +
            "Size=16x11 | Occupied=176 | Blocked=73 | Walkable=103\n" +
            "InteriorColliders=15 | PerimeterWalls=7 | DoorSockets=3\n" +
            "Sockets=West(y6-7) + East(y6-7) + South(x7-8) | North=None\n" +
            "Art=Preserved | Foreground=Preserved | ProductionMainChanged=False\n" +
            "GameSceneChanged=False | RuntimeCoreCodeChanged=False");

        EditorUtility.DisplayDialog(
            "P10.10 Applied",
            "MusicRoom_01 V4 几何已写入。\n\n" +
            "请进入 Prefab Mode 打开 Grid / Cell Overrides，确认红格与 V4 预览一致，" +
            "然后执行 P10.10 Validate 与 P10.7 通用 Validate。",
            "OK");
    }

    [MenuItem(MenuRoot + "2. Validate Approved V4 Geometry", false, 2791)]
    private static void ValidateApprovedGeometry()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(MusicRoomPrefabPath);
            DreamRoomTemplate template = RequireMusicRoomTemplate(root);

            List<string> errors = new List<string>();
            ValidateBlockedCells(template, errors);
            ValidateInterior(root, template, errors);
            ValidateSockets(root, template, errors);
            ValidatePerimeter(root, errors);
            ValidateArtLayers(root, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "MusicRoom_01 V4 Geometry 校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            List<Vector2Int> occupied = new List<Vector2Int>();
            List<Vector2Int> blocked = new List<Vector2Int>();
            List<Vector2Int> walkable = new List<Vector2Int>();
            template.GetOccupiedCells(occupied);
            template.GetBlockedCells(blocked);
            template.GetWalkableCells(walkable);

            Debug.Log(
                "[P10.10] MusicRoom_01 V4 几何校验通过。\n" +
                "Size=16x11 | Occupied=" + occupied.Count +
                " | Blocked=" + blocked.Count +
                " | Walkable=" + walkable.Count + "\n" +
                "InteriorColliders=15 | PerimeterWalls=7 | DoorSockets=3\n" +
                "WestDoorCells=(0,6),(0,7) | EastDoorCells=(15,6),(15,7)\n" +
                "SouthDoorCells=(7,0),(8,0) | NorthSocket=None\n" +
                "ArtLayers=Floor/Objects/Foreground/Effects Preserved\n" +
                "ProductionMainPublished=FalseExpectedAtThisStage | RuntimeCoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.10 Geometry Passed",
                "MusicRoom_01 V4 几何校验通过。\n\n" +
                "Blocked 73 / Walkable 103 / Interior Collider 15。\n" +
                "下一步可以开始正式拆分并覆盖 Floor / Objects / Foreground / Effects。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "P10.10 Validation Failed",
                "MusicRoom_01 V4 几何校验失败。请查看 Console 第一条红色错误。",
                "OK");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool CanEditPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("P10.10", "请先退出 Play Mode。", "OK");
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("P10.10", "请先退出 Prefab Mode，再执行 Apply。", "OK");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MusicRoomPrefabPath) == null)
        {
            EditorUtility.DisplayDialog(
                "P10.10",
                "找不到 Room_MusicRoom_01.prefab。请先用 P10.7 Factory 建立 MusicRoom_01。",
                "OK");
            return false;
        }

        return true;
    }

    private static DreamRoomTemplate RequireMusicRoomTemplate(GameObject root)
    {
        if (root == null)
        {
            throw new InvalidOperationException("MusicRoom Prefab Contents 为 null。");
        }

        DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
        if (template == null)
        {
            throw new InvalidOperationException("MusicRoom 根节点缺少 DreamRoomTemplate。");
        }

        if (!string.Equals(template.TemplateId, ExpectedTemplateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TemplateId 不匹配。Expected=" + ExpectedTemplateId +
                " | Actual=" + template.TemplateId);
        }

        if (template.SizeInCells != ExpectedSize)
        {
            throw new InvalidOperationException(
                "MusicRoom 尺寸不匹配。Expected=16x11 | Actual=" +
                template.SizeInCells.x + "x" + template.SizeInCells.y);
        }

        return template;
    }

    private static List<Vector2Int> BuildExpectedBlockedCells()
    {
        HashSet<Vector2Int> unique = new HashSet<Vector2Int>();

        for (int i = 0; i < ExpectedFootprints.Length; i++)
        {
            Footprint footprint = ExpectedFootprints[i];
            for (int y = footprint.Y; y < footprint.Y + footprint.Height; y++)
            {
                for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                {
                    unique.Add(new Vector2Int(x, y));
                }
            }
        }

        List<Vector2Int> cells = new List<Vector2Int>(unique);
        cells.Sort(CompareCells);

        if (cells.Count != 73)
        {
            throw new InvalidOperationException(
                "P10.10 内建 V4 数据异常：Blocked Cells 应为 73，Actual=" + cells.Count);
        }

        return cells;
    }

    private static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int yCompare = a.y.CompareTo(b.y);
        return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
    }

    private static void WriteBlockedCells(DreamRoomTemplate template, List<Vector2Int> blocked)
    {
        SerializedObject serialized = new SerializedObject(template);
        SerializedProperty property = serialized.FindProperty("blockedCells");

        if (property == null || !property.isArray)
        {
            throw new InvalidOperationException(
                "DreamRoomTemplate.blockedCells SerializedProperty 不存在。" );
        }

        property.ClearArray();
        for (int i = 0; i < blocked.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).vector2IntValue = blocked[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateFootprintCollider(
        DreamRoomTemplate template,
        Transform parent,
        Footprint footprint)
    {
        GameObject node = new GameObject(footprint.Name);
        node.transform.SetParent(parent, false);

        float centerX =
            footprint.X + (footprint.Width - 1) * 0.5f -
            (template.SizeInCells.x - 1) * 0.5f;
        float centerY =
            footprint.Y + (footprint.Height - 1) * 0.5f -
            (template.SizeInCells.y - 1) * 0.5f;

        node.transform.localPosition = new Vector3(centerX, centerY, 0f);
        node.transform.localRotation = Quaternion.identity;
        node.transform.localScale = Vector3.one;

        BoxCollider2D collider = node.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(footprint.Width, footprint.Height);
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
    }

    private static void ReconfigureSocketsAndBlockers(
        GameObject root,
        DreamRoomTemplate template)
    {
        template.RefreshDoorSockets();

        if (template.DoorSockets.Count != 3)
        {
            throw new InvalidOperationException(
                "MusicRoom 应有 3 个 Socket（E/S/W），Actual=" + template.DoorSockets.Count);
        }

        DreamRoomDoorSocket east = FindSocket(template, DreamRoomDoorDirection.East);
        DreamRoomDoorSocket south = FindSocket(template, DreamRoomDoorDirection.South);
        DreamRoomDoorSocket west = FindSocket(template, DreamRoomDoorDirection.West);

        if (FindSocketOptional(template, DreamRoomDoorDirection.North) != null)
        {
            throw new InvalidOperationException("MusicRoom 不应存在 North Socket。");
        }

        ConfigureSocketAndBlocker(template, east, new Vector2Int(15, 7));
        ConfigureSocketAndBlocker(template, south, new Vector2Int(8, 0));
        ConfigureSocketAndBlocker(template, west, new Vector2Int(0, 7));
    }

    private static DreamRoomDoorSocket FindSocket(
        DreamRoomTemplate template,
        DreamRoomDoorDirection direction)
    {
        DreamRoomDoorSocket socket = FindSocketOptional(template, direction);
        if (socket == null)
        {
            throw new InvalidOperationException("缺少 " + direction + " Socket。");
        }
        return socket;
    }

    private static DreamRoomDoorSocket FindSocketOptional(
        DreamRoomTemplate template,
        DreamRoomDoorDirection direction)
    {
        for (int i = 0; i < template.DoorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket = template.DoorSockets[i];
            if (socket != null && socket.Direction == direction)
            {
                return socket;
            }
        }
        return null;
    }

    private static void ConfigureSocketAndBlocker(
        DreamRoomTemplate template,
        DreamRoomDoorSocket socket,
        Vector2Int baseCell)
    {
        const int doorWidth = 2;
        GameObject blocker = socket.ClosedBlocker;

        if (blocker == null)
        {
            throw new InvalidOperationException(socket.SocketId + " 缺少 ClosedBlocker 引用。");
        }

        Vector3 socketPosition = GetDoorCenterLocal(
            template.SizeInCells,
            socket.Direction,
            baseCell,
            doorWidth);

        socket.transform.localPosition = socketPosition;
        socket.transform.localRotation = Quaternion.identity;
        socket.transform.localScale = Vector3.one;

        blocker.transform.localPosition = GetBoundaryPosition(
            template.SizeInCells,
            socket.Direction,
            socketPosition);
        blocker.transform.localRotation = Quaternion.identity;
        blocker.transform.localScale = Vector3.one;

        BoxCollider2D blockerCollider = blocker.GetComponent<BoxCollider2D>();
        if (blockerCollider == null)
        {
            blockerCollider = blocker.AddComponent<BoxCollider2D>();
        }

        bool horizontal =
            socket.Direction == DreamRoomDoorDirection.North ||
            socket.Direction == DreamRoomDoorDirection.South;

        blockerCollider.size = horizontal
            ? new Vector2(doorWidth, ClosedBlockerThickness)
            : new Vector2(ClosedBlockerThickness, doorWidth);
        blockerCollider.offset = Vector2.zero;
        blockerCollider.isTrigger = false;

        SpriteRenderer debugSprite = blocker.GetComponent<SpriteRenderer>();
        if (debugSprite != null)
        {
            UnityEngine.Object.DestroyImmediate(debugSprite);
        }

        socket.Configure(
            socket.SocketId,
            socket.Direction,
            baseCell,
            doorWidth,
            blocker);
    }

    private static Vector3 GetDoorCenterLocal(
        Vector2Int size,
        DreamRoomDoorDirection direction,
        Vector2Int baseCell,
        int doorWidth)
    {
        Vector2Int sideways = direction.PerpendicularCellOffset();
        int startOffset = -(doorWidth / 2);
        Vector2 total = Vector2.zero;

        for (int i = 0; i < doorWidth; i++)
        {
            Vector2Int cell = baseCell + sideways * (startOffset + i);
            total += new Vector2(cell.x, cell.y);
        }

        Vector2 average = total / doorWidth;
        return new Vector3(
            average.x - (size.x - 1) * 0.5f,
            average.y - (size.y - 1) * 0.5f,
            0f);
    }

    private static Vector3 GetBoundaryPosition(
        Vector2Int size,
        DreamRoomDoorDirection direction,
        Vector3 socketLocalPosition)
    {
        Vector3 position = socketLocalPosition;

        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position.y = size.y * 0.5f;
                break;
            case DreamRoomDoorDirection.East:
                position.x = size.x * 0.5f;
                break;
            case DreamRoomDoorDirection.South:
                position.y = -size.y * 0.5f;
                break;
            case DreamRoomDoorDirection.West:
                position.x = -size.x * 0.5f;
                break;
        }

        return position;
    }

    private static void RebuildMusicRoomPerimeter(Transform perimeter)
    {
        DestroyAllChildren(perimeter);

        // North：无 Socket，整边封闭。
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_North_0",
            new Vector2(0f, 5.5f),
            new Vector2(16f, PerimeterWallThickness));

        // South：中心 2 Cell 门洞，左右各 7 Cell。
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_South_0",
            new Vector2(-4.5f, -5.5f),
            new Vector2(7f, PerimeterWallThickness));
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_South_1",
            new Vector2(4.5f, -5.5f),
            new Vector2(7f, PerimeterWallThickness));

        // West / East：门中心 y=1.5，门洞区间 y=0.5..2.5。
        // 下段 6 Cell，上段 3 Cell。
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_West_0",
            new Vector2(-8f, -2.5f),
            new Vector2(PerimeterWallThickness, 6f));
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_West_1",
            new Vector2(-8f, 4f),
            new Vector2(PerimeterWallThickness, 3f));
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_East_0",
            new Vector2(8f, -2.5f),
            new Vector2(PerimeterWallThickness, 6f));
        CreatePerimeterSegment(
            perimeter,
            "Perimeter_East_1",
            new Vector2(8f, 4f),
            new Vector2(PerimeterWallThickness, 3f));
    }

    private static void CreatePerimeterSegment(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        GameObject node = new GameObject(name);
        node.transform.SetParent(parent, false);
        node.transform.localPosition = new Vector3(position.x, position.y, 0f);
        node.transform.localRotation = Quaternion.identity;
        node.transform.localScale = Vector3.one;

        BoxCollider2D collider = node.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
    }

    private static void DestroyAllChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void ValidateBlockedCells(
        DreamRoomTemplate template,
        List<string> errors)
    {
        List<Vector2Int> actual = new List<Vector2Int>();
        template.GetBlockedCells(actual);

        HashSet<Vector2Int> expected = new HashSet<Vector2Int>(BuildExpectedBlockedCells());
        HashSet<Vector2Int> actualSet = new HashSet<Vector2Int>(actual);

        if (!actualSet.SetEquals(expected))
        {
            errors.Add(
                "Blocked Cells 与批准的 V4 不一致。Expected=" +
                expected.Count + " | Actual=" + actualSet.Count + "。" );
        }

        if (actual.Count != actualSet.Count)
        {
            errors.Add("Blocked Cells 中存在重复格。" );
        }
    }

    private static void ValidateInterior(
        GameObject root,
        DreamRoomTemplate template,
        List<string> errors)
    {
        Transform interior = root.transform.Find("Navigation/Colliders/Interior");
        if (interior == null)
        {
            errors.Add("缺少 Navigation/Colliders/Interior。" );
            return;
        }

        if (interior.childCount != ExpectedFootprints.Length)
        {
            errors.Add(
                "Interior Collider 数量不正确。Expected=" +
                ExpectedFootprints.Length + " | Actual=" + interior.childCount + "。" );
        }

        for (int i = 0; i < ExpectedFootprints.Length; i++)
        {
            Footprint footprint = ExpectedFootprints[i];
            Transform node = interior.Find(footprint.Name);
            if (node == null)
            {
                errors.Add("缺少 Interior Collider：" + footprint.Name + "。" );
                continue;
            }

            float expectedX =
                footprint.X + (footprint.Width - 1) * 0.5f -
                (template.SizeInCells.x - 1) * 0.5f;
            float expectedY =
                footprint.Y + (footprint.Height - 1) * 0.5f -
                (template.SizeInCells.y - 1) * 0.5f;

            if (Vector3.Distance(node.localPosition, new Vector3(expectedX, expectedY, 0f)) > 0.001f)
            {
                errors.Add(footprint.Name + " Position 与 V4 格子脚印不一致。" );
            }

            if (node.localRotation != Quaternion.identity ||
                Vector3.Distance(node.localScale, Vector3.one) > 0.0001f)
            {
                errors.Add(footprint.Name + " Transform Rotation/Scale 应为 Identity。" );
            }

            BoxCollider2D collider = node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                errors.Add(footprint.Name + " 缺少 BoxCollider2D。" );
                continue;
            }

            if (Vector2.Distance(collider.size, new Vector2(footprint.Width, footprint.Height)) > 0.001f)
            {
                errors.Add(footprint.Name + " Collider Size 不正确。" );
            }

            if (collider.isTrigger)
            {
                errors.Add(footprint.Name + " 不应为 Trigger。" );
            }
        }
    }

    private static void ValidateSockets(
        GameObject root,
        DreamRoomTemplate template,
        List<string> errors)
    {
        template.RefreshDoorSockets();

        if (template.DoorSockets.Count != 3)
        {
            errors.Add("DoorSockets 应为 3，Actual=" + template.DoorSockets.Count + "。" );
            return;
        }

        DreamRoomDoorSocket east = FindSocketOptional(template, DreamRoomDoorDirection.East);
        DreamRoomDoorSocket south = FindSocketOptional(template, DreamRoomDoorDirection.South);
        DreamRoomDoorSocket west = FindSocketOptional(template, DreamRoomDoorDirection.West);
        DreamRoomDoorSocket north = FindSocketOptional(template, DreamRoomDoorDirection.North);

        if (north != null)
        {
            errors.Add("North Socket 应不存在。" );
        }

        ValidateSocketCells(template, west, new[] { new Vector2Int(0,6), new Vector2Int(0,7) }, errors);
        ValidateSocketCells(template, east, new[] { new Vector2Int(15,6), new Vector2Int(15,7) }, errors);
        ValidateSocketCells(template, south, new[] { new Vector2Int(7,0), new Vector2Int(8,0) }, errors);
    }

    private static void ValidateSocketCells(
        DreamRoomTemplate template,
        DreamRoomDoorSocket socket,
        Vector2Int[] expectedCells,
        List<string> errors)
    {
        if (socket == null)
        {
            errors.Add("缺少预期 Socket。" );
            return;
        }

        List<Vector2Int> actual = socket.GetLocalInsideCells();
        HashSet<Vector2Int> actualSet = new HashSet<Vector2Int>(actual);
        HashSet<Vector2Int> expectedSet = new HashSet<Vector2Int>(expectedCells);

        if (!actualSet.SetEquals(expectedSet))
        {
            errors.Add(socket.SocketId + " 门格位置不符合 V4。" );
        }

        for (int i = 0; i < actual.Count; i++)
        {
            if (template.IsBlockedCell(actual[i]))
            {
                errors.Add(socket.SocketId + " 门格 " + actual[i] + " 被 Blocked Cells 封死。" );
            }
        }

        if (socket.ClosedBlocker == null)
        {
            errors.Add(socket.SocketId + " 缺少 ClosedBlocker。" );
        }
    }

    private static void ValidatePerimeter(GameObject root, List<string> errors)
    {
        Transform perimeter = root.transform.Find("Navigation/Colliders/Perimeter");
        if (perimeter == null)
        {
            errors.Add("缺少 Navigation/Colliders/Perimeter。" );
            return;
        }

        if (perimeter.childCount != 7)
        {
            errors.Add("MusicRoom Perimeter 应为 7 段，Actual=" + perimeter.childCount + "。" );
        }
    }

    private static void ValidateArtLayers(GameObject root, List<string> errors)
    {
        string[] required =
        {
            "Visual/Floor/Floor_Runtime",
            "Visual/Objects/Objects_Runtime",
            "Visual/Foreground/Foreground_Runtime",
            "Visual/Effects/Effects_Runtime"
        };

        for (int i = 0; i < required.Length; i++)
        {
            if (root.transform.Find(required[i]) == null)
            {
                errors.Add("缺少美术层：" + required[i] + "。" );
            }
        }
    }
}
