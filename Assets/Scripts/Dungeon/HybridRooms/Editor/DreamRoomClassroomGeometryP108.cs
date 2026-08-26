using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.8.2：Classroom_01 家具 + 上下结构墙几何修订。
///
/// 保留 P10.8.1 已移除的 StudentDesk_CenterMid 2x2 碰撞，并按用户标注新增上、下透明结构区域的碰撞。
/// 只处理 Production_Classroom_01 的 Blocked Cells 与
/// Navigation/Colliders/Interior，不改图片、Socket、Perimeter、Catalog、GameScene
/// 或任何 Runtime 核心代码。
///
/// 碰撞原则：家具按“地面占用/脚印”阻挡；固定结构按明确格区阻挡。
/// 所有新增碰撞都同步写入 Blocked Cells，确保 A* 与 Physics 一致。
/// </summary>
public static class DreamRoomClassroomGeometryP108
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.8 Classroom Geometry/";

    private const string ClassroomPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Classroom_01/Room_Classroom_01.prefab";

    private const string ProductionCatalogPath =
        "Assets/DreamDungeon/Production/Catalog/RoomCatalog_Production.asset";

    private const string ExpectedTemplateId = "Production_Classroom_01";
    private static readonly Vector2Int ExpectedSize = new Vector2Int(16, 12);

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
    // 这里按家具与地面接触/实际占地来取格，不按 Sprite 的视觉高度取格。
    private static readonly Footprint[] FurnitureFootprints =
    {
        new Footprint("Bookshelf_Left",       1, 9, 2, 1),
        new Footprint("TeacherDesk",          5, 8, 3, 1),
        new Footprint("Bookshelf_Right",      12, 9, 2, 1),

        new Footprint("StudentDesk_LeftMid",  2, 5, 2, 2),
        new Footprint("StudentDesk_UpperRight",9, 6, 1, 2),
        new Footprint("StudentDesk_RightMid", 12, 4, 1, 2),
        new Footprint("StudentDesk_LeftBottom",3, 2, 1, 2),
        new Footprint("StudentDesk_CenterBottom",6, 2, 1, 2),
        new Footprint("StudentDesk_RightBottom",10, 2, 1, 2),
    };

    // 用户本轮白色标注对应的固定结构区域。
    // 北侧左右各占 7x2，南侧左右各占 7x1；中间保留 2 Cell 门洞 (x=7,8)。
    private static readonly Footprint[] StructuralFootprints =
    {
        new Footprint("Structure_TopLeft",     0, 10, 7, 2),
        new Footprint("Structure_TopRight",    9, 10, 7, 2),
        new Footprint("Structure_BottomLeft",  0,  0, 7, 1),
        new Footprint("Structure_BottomRight", 9,  0, 7, 1),
    };

    [MenuItem(MenuRoot + "1. Apply Classroom_01 First-Pass Geometry", false, 2780)]
    private static void ApplyGeometry()
    {
        if (!CanEditPrefab())
        {
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(ClassroomPrefabPath);
            DreamRoomTemplate template = RequireClassroomTemplate(root);

            Transform interior =
                root.transform.Find("Navigation/Colliders/Interior");

            if (interior == null)
            {
                throw new InvalidOperationException(
                    "缺少 Navigation/Colliders/Interior。请确认 Classroom_01 由 P10.7 Factory 建立。" );
            }

            // 只清理 Interior，绝不触碰 P10.7 已生成并验证的 Perimeter。
            DestroyAllChildren(interior);

            List<Vector2Int> blocked = BuildExpectedBlockedCells();
            WriteBlockedCells(template, blocked);

            for (int i = 0; i < FurnitureFootprints.Length; i++)
            {
                CreateFootprintCollider(
                    template,
                    interior,
                    FurnitureFootprints[i]);
            }

            for (int i = 0; i < StructuralFootprints.Length; i++)
            {
                CreateFootprintCollider(
                    template,
                    interior,
                    StructuralFootprints[i]);
            }

            EditorUtility.SetDirty(template);
            PrefabUtility.SaveAsPrefabAsset(root, ClassroomPrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "P10.8 Apply Failed",
                "Classroom_01 几何写入中止。请查看 Console 第一条红色错误。",
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

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ClassroomPrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log(
            "[P10.8.2] Classroom_01 家具+结构几何已写入。\n" +
            "Blocked=63 | Walkable=129 | InteriorColliders=13\n" +
            "Footprints=2 Bookshelves + 1 TeacherDesk + 6 StudentDesks + 4 StructuralZones\n" +
            "Removed=StudentDesk_CenterMid (P10.8.1 retained)\n" +
            "AddedStructure=TopLeft7x2 + TopRight7x2 + BottomLeft7x1 + BottomRight7x1\n" +
            "TrashCan=VisualOnly | LoosePapers=VisualOnly\n" +
            "Perimeter=Preserved | Sockets=Preserved | Art=Preserved\n" +
            "CatalogChanged=False | GameSceneChanged=False | RuntimeCoreCodeChanged=False");

        EditorUtility.DisplayDialog(
            "P10.8 Applied",
            "Classroom_01 家具与上下结构碰撞已经写入。\n\n" +
            "接下来进入 Prefab Mode 查看红色 Blocked Cells 是否贴合家具，" +
            "然后执行 P10.8 Validate 与 P10.7 通用 Validate。",
            "OK");
    }

    [MenuItem(MenuRoot + "2. Validate Classroom_01 Geometry", false, 2781)]
    private static void ValidateGeometry()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(ClassroomPrefabPath);
            DreamRoomTemplate template = RequireClassroomTemplate(root);

            List<string> errors = new List<string>();
            ValidateBlockedCells(template, errors);
            ValidateInteriorColliders(root, template, errors);
            ValidateDoorCells(template, errors);
            ValidatePerimeter(root, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Classroom_01 Geometry 校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            List<Vector2Int> occupied = new List<Vector2Int>();
            List<Vector2Int> blocked = new List<Vector2Int>();
            List<Vector2Int> walkable = new List<Vector2Int>();
            template.GetOccupiedCells(occupied);
            template.GetBlockedCells(blocked);
            template.GetWalkableCells(walkable);

            bool published = IsPublished(template.TemplateId);

            Debug.Log(
                "[P10.8.2] Classroom_01 家具+结构几何校验通过。\n" +
                "Size=16x12 | Occupied=" + occupied.Count +
                " | Blocked=" + blocked.Count +
                " | Walkable=" + walkable.Count + "\n" +
                "InteriorColliders=13 | PerimeterWalls=8 | DoorSockets=4\n" +
                "FurnitureFootprints=GroundContact/GridAligned\n" +
                "TrashCanCollider=0 | LoosePaperCollider=0\n" +
                "DoorCells=N/E/S/W Clear\n" +
                "ArtLayers=Preserved | ProductionMainPublished=" + published + "\n" +
                "RuntimeCoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.8 Geometry Passed",
                "Classroom_01 家具与上下结构几何校验通过。\n\n" +
                "Blocked 63 / Walkable 129 / Interior Collider 13。\n" +
                "下一步请目视检查 Grid，然后做 Play 手感验收。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "P10.8 Validation Failed",
                "Classroom_01 几何校验失败。请查看 Console 第一条红色错误。",
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
            EditorUtility.DisplayDialog(
                "P10.8",
                "请先退出 Play Mode。",
                "OK");
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "P10.8",
                "请先退出 Prefab Mode，再执行 Apply。",
                "OK");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ClassroomPrefabPath) == null)
        {
            EditorUtility.DisplayDialog(
                "P10.8",
                "找不到 Room_Classroom_01.prefab。请确认 P10.7 Factory 已完成。",
                "OK");
            return false;
        }

        return true;
    }

    private static DreamRoomTemplate RequireClassroomTemplate(GameObject root)
    {
        if (root == null)
        {
            throw new InvalidOperationException("Classroom Prefab Contents 为 null。");
        }

        DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
        if (template == null)
        {
            throw new InvalidOperationException("Classroom 根节点缺少 DreamRoomTemplate。");
        }

        if (!string.Equals(
                template.TemplateId,
                ExpectedTemplateId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TemplateId 不匹配。Expected=" + ExpectedTemplateId +
                " | Actual=" + template.TemplateId);
        }

        if (template.SizeInCells != ExpectedSize)
        {
            throw new InvalidOperationException(
                "Classroom 尺寸不匹配。Expected=16x12 | Actual=" +
                template.SizeInCells.x + "x" + template.SizeInCells.y);
        }

        return template;
    }

    private static List<Vector2Int> BuildExpectedBlockedCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        HashSet<Vector2Int> unique = new HashSet<Vector2Int>();

        for (int i = 0; i < FurnitureFootprints.Length; i++)
        {
            Footprint footprint = FurnitureFootprints[i];

            for (int y = footprint.Y; y < footprint.Y + footprint.Height; y++)
            {
                for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (unique.Add(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        for (int i = 0; i < StructuralFootprints.Length; i++)
        {
            Footprint footprint = StructuralFootprints[i];

            for (int y = footprint.Y; y < footprint.Y + footprint.Height; y++)
            {
                for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (unique.Add(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        cells.Sort(CompareCells);
        return cells;
    }

    private static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int yCompare = a.y.CompareTo(b.y);
        return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
    }

    private static void WriteBlockedCells(
        DreamRoomTemplate template,
        List<Vector2Int> blocked)
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
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.vector2IntValue = blocked[i];
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

        HashSet<Vector2Int> expectedSet =
            new HashSet<Vector2Int>(BuildExpectedBlockedCells());
        HashSet<Vector2Int> actualSet =
            new HashSet<Vector2Int>(actual);

        if (!actualSet.SetEquals(expectedSet))
        {
            errors.Add(
                "Blocked Cells 与 P10.8.2 家具+结构区域不一致。Expected=" +
                expectedSet.Count + " | Actual=" + actualSet.Count + "。" );
        }

        if (actual.Count != actualSet.Count)
        {
            errors.Add("Blocked Cells 中存在重复格。" );
        }
    }

    private static void ValidateInteriorColliders(
        GameObject root,
        DreamRoomTemplate template,
        List<string> errors)
    {
        Transform interior =
            root.transform.Find("Navigation/Colliders/Interior");

        if (interior == null)
        {
            errors.Add("缺少 Navigation/Colliders/Interior。" );
            return;
        }

        int expectedColliderCount = FurnitureFootprints.Length + StructuralFootprints.Length;
        if (interior.childCount != expectedColliderCount)
        {
            errors.Add(
                "Interior Collider 节点数量不正确。Expected=" +
                expectedColliderCount + " | Actual=" + interior.childCount + "。" );
        }

        for (int i = 0; i < FurnitureFootprints.Length; i++)
        {
            Footprint footprint = FurnitureFootprints[i];
            Transform node = interior.Find(footprint.Name);

            if (node == null)
            {
                errors.Add("缺少 Interior Collider：" + footprint.Name + "。" );
                continue;
            }

            if (node.localRotation != Quaternion.identity ||
                Vector3.Distance(node.localScale, Vector3.one) > 0.0001f)
            {
                errors.Add(footprint.Name + " Transform Rotation/Scale 应保持 Identity。" );
            }

            float expectedX =
                footprint.X + (footprint.Width - 1) * 0.5f -
                (template.SizeInCells.x - 1) * 0.5f;
            float expectedY =
                footprint.Y + (footprint.Height - 1) * 0.5f -
                (template.SizeInCells.y - 1) * 0.5f;

            Vector3 expectedPosition = new Vector3(expectedX, expectedY, 0f);
            if (Vector3.Distance(node.localPosition, expectedPosition) > 0.001f)
            {
                errors.Add(footprint.Name + " Position 与格子脚印不一致。" );
            }

            BoxCollider2D collider = node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                errors.Add(footprint.Name + " 缺少 BoxCollider2D。" );
                continue;
            }

            Vector2 expectedSize = new Vector2(footprint.Width, footprint.Height);
            if (Vector2.Distance(collider.size, expectedSize) > 0.001f)
            {
                errors.Add(footprint.Name + " Collider Size 不正确。" );
            }

            if (collider.isTrigger)
            {
                errors.Add(footprint.Name + " 不应为 Trigger。" );
            }

            if (node.GetComponent<SpriteRenderer>() != null)
            {
                errors.Add(footprint.Name + " 几何节点不应携带 SpriteRenderer。" );
            }
        }

        for (int i = 0; i < StructuralFootprints.Length; i++)
        {
            Footprint footprint = StructuralFootprints[i];
            Transform node = interior.Find(footprint.Name);

            if (node == null)
            {
                errors.Add("缺少 Interior Collider：" + footprint.Name + "。" );
                continue;
            }

            if (node.localRotation != Quaternion.identity ||
                Vector3.Distance(node.localScale, Vector3.one) > 0.0001f)
            {
                errors.Add(footprint.Name + " Transform Rotation/Scale 应保持 Identity。" );
            }

            float expectedX =
                footprint.X + (footprint.Width - 1) * 0.5f -
                (template.SizeInCells.x - 1) * 0.5f;
            float expectedY =
                footprint.Y + (footprint.Height - 1) * 0.5f -
                (template.SizeInCells.y - 1) * 0.5f;

            Vector3 expectedPosition = new Vector3(expectedX, expectedY, 0f);
            if (Vector3.Distance(node.localPosition, expectedPosition) > 0.001f)
            {
                errors.Add(footprint.Name + " Position 与格子脚印不一致。" );
            }

            BoxCollider2D collider = node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                errors.Add(footprint.Name + " 缺少 BoxCollider2D。" );
                continue;
            }

            Vector2 expectedSize = new Vector2(footprint.Width, footprint.Height);
            if (Vector2.Distance(collider.size, expectedSize) > 0.001f)
            {
                errors.Add(footprint.Name + " Collider Size 不正确。" );
            }

            if (collider.isTrigger)
            {
                errors.Add(footprint.Name + " 不应为 Trigger。" );
            }

            if (node.GetComponent<SpriteRenderer>() != null)
            {
                errors.Add(footprint.Name + " 几何节点不应携带 SpriteRenderer。" );
            }
        }
    }

    private static void ValidateDoorCells(
        DreamRoomTemplate template,
        List<string> errors)
    {
        if (template.DoorSockets.Count != 4)
        {
            errors.Add(
                "DoorSockets 应为 4，Actual=" + template.DoorSockets.Count + "。" );
        }

        for (int i = 0; i < template.DoorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket = template.DoorSockets[i];
            if (socket == null)
            {
                errors.Add("DoorSockets 存在 null。" );
                continue;
            }

            List<Vector2Int> doorCells = socket.GetLocalInsideCells();
            for (int c = 0; c < doorCells.Count; c++)
            {
                if (template.IsBlockedCell(doorCells[c]))
                {
                    errors.Add(
                        socket.SocketId + " 门内格 " + doorCells[c] +
                        " 被 Blocked Cells 封死。" );
                }
            }
        }
    }

    private static void ValidatePerimeter(
        GameObject root,
        List<string> errors)
    {
        Transform perimeter =
            root.transform.Find("Navigation/Colliders/Perimeter");

        if (perimeter == null)
        {
            errors.Add("缺少 Navigation/Colliders/Perimeter。" );
            return;
        }

        // 四边各一个居中 2 Cell Socket，因此每边应被切成两个墙段。
        if (perimeter.childCount != 8)
        {
            errors.Add(
                "P10.7 Perimeter 应保持 8 个墙段，Actual=" +
                perimeter.childCount + "。" );
        }
    }

    private static bool IsPublished(string templateId)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(ProductionCatalogPath);

        if (catalog == null)
        {
            return false;
        }

        DreamRoomTemplate found;
        return catalog.TryGetTemplate(templateId, out found) && found != null;
    }
}
