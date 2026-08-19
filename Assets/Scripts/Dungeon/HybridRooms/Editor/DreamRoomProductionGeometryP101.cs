using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.1：为第一间正式房 Crossroad_01 建立“游戏几何契约”。
///
/// 本阶段只做：
/// 1. Blocked Cells：四角 5x5 固体区 + 中央 2x2 安全岛。
/// 2. Navigation/Colliders：与 Blocked Cells 对应的 5 个内部 BoxCollider2D。
/// 3. 房间外周：沿用 R3/R7 的 0.35 厚边界墙，并为四个 2 Cell Socket 留口。
///
/// 本阶段不做：
/// - 不导入正式 Floor 图片。
/// - 不修改 Catalog / GameScene。
/// - 不修改 DungeonGenerator / DungeonRenderer / A* / Enemy AI。
/// - Occupied Cells 保持空列表（即完整 16x16 矩形占格）。
/// - Walkable Cells 保持空列表（即 Occupied - Blocked 自动计算）。
/// </summary>
public static class DreamRoomProductionGeometryP101
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.1/";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string GeneratedRootName =
        "P10_1_Geometry";

    private static readonly Vector2Int ExpectedSize =
        new Vector2Int(16, 16);

    private const float WallThickness = 0.35f;
    private const int DoorWidthInCells = 2;

    [MenuItem(
        MenuRoot + "Apply Crossroad Geometry Contract",
        false,
        2710)]
    private static void ApplyCrossroadGeometryContract()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "请先退出 Prefab Mode，再执行 P10.1。",
                "OK");
            return;
        }

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            ReportFailure(
                "找不到 P10.0 Crossroad Prefab：\n" +
                CrossroadPrefabPath +
                "\n\n请先完成 P10.0。" );
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(
                CrossroadPrefabPath);

            DreamRoomTemplate template =
                root.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException(
                    "Crossroad_01 根节点缺少 DreamRoomTemplate。" );
            }

            if (template.SizeInCells != ExpectedSize)
            {
                throw new InvalidOperationException(
                    "P10.1 只接受 16x16 Crossroad。当前 Size=" +
                    template.SizeInCells.x + "x" +
                    template.SizeInCells.y + "。" );
            }

            Transform collidersRoot =
                root.transform.Find("Navigation/Colliders");

            if (collidersRoot == null)
            {
                throw new InvalidOperationException(
                    "找不到 Navigation/Colliders。请先完成 P10.0 骨架。" );
            }

            ApplyCellContract(template);
            RebuildGeneratedColliders(collidersRoot);

            template.RefreshDoorSockets();

            List<string> errors =
                ValidateLoadedPrefab(root, template);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "P10.1 保存前校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                CrossroadPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject savedPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CrossroadPrefabPath);

            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log(
                "[P10.1] Crossroad_01 游戏几何契约已应用。\n" +
                "Occupied=16x16 Rectangle (256 Cells)\n" +
                "Blocked=104 Cells (4x 5x5 Corners + 2x2 Center Island)\n" +
                "Walkable=152 Cells (Default Occupied-Blocked)\n" +
                "InteriorColliders=5 BoxCollider2D\n" +
                "PerimeterWalls=8 BoxCollider2D | Thickness=0.35\n" +
                "DoorOpenings=4 x 2 Cells\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False" );

            EditorUtility.DisplayDialog(
                "P10.1 Geometry Ready",
                "Crossroad_01 的正式游戏几何已建立。\n\n" +
                "现在 Scene 中应看到：\n" +
                "- 四角各 5x5 红色 Blocked Cells\n" +
                "- 正中央 2x2 红色 Blocked Cells\n" +
                "- 四向 2 Cell 门口保持可行走\n\n" +
                "下一步才接入 1024x1024 Floor。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ReportFailure(
                "P10.1 已中止，没有要求修改 GameScene 或 Catalog。\n\n" +
                "请把 Console 第一条红色错误发给我。" );
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    [MenuItem(
        MenuRoot + "Validate Crossroad Geometry Contract",
        false,
        2711)]
    private static void ValidateCrossroadGeometryContract()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "[P10.1] 找不到 Crossroad_01 Prefab：" +
                CrossroadPrefabPath);
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(
                CrossroadPrefabPath);

            DreamRoomTemplate template =
                root.GetComponent<DreamRoomTemplate>();

            List<string> errors =
                ValidateLoadedPrefab(root, template);

            if (errors.Count > 0)
            {
                Debug.LogError(
                    "[P10.1] Crossroad_01 游戏几何校验失败：\n- " +
                    string.Join("\n- ", errors));
                return;
            }

            List<Vector2Int> blocked =
                new List<Vector2Int>();
            List<Vector2Int> walkable =
                new List<Vector2Int>();

            template.GetBlockedCells(blocked);
            template.GetWalkableCells(walkable);

            Debug.Log(
                "[P10.1] Crossroad_01 游戏几何校验通过。\n" +
                "Size=16x16 | Occupied=256 | Blocked=" +
                blocked.Count + " | Walkable=" + walkable.Count + "\n" +
                "CornerBlocks=4x5x5 | CenterIsland=2x2\n" +
                "InteriorColliders=5 | PerimeterWalls=8\n" +
                "DoorCells=North/East/South/West all walkable\n" +
                "RuntimeIntegration=NotStartedByDesign" );
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ApplyCellContract(
        DreamRoomTemplate template)
    {
        List<Vector2Int> blocked =
            BuildExpectedBlockedCells();

        SerializedObject serialized =
            new SerializedObject(template);

        SerializedProperty occupied =
            RequireProperty(serialized, "occupiedCells");
        SerializedProperty walkable =
            RequireProperty(serialized, "walkableCells");
        SerializedProperty blockedProperty =
            RequireProperty(serialized, "blockedCells");

        // 保持单一规则：完整矩形占格；可行走自动由 Blocked 扣除。
        occupied.arraySize = 0;
        walkable.arraySize = 0;

        blockedProperty.arraySize = blocked.Count;

        for (int i = 0; i < blocked.Count; i++)
        {
            blockedProperty
                .GetArrayElementAtIndex(i)
                .vector2IntValue = blocked[i];
        }

        RequireProperty(serialized, "drawCellGrid")
            .boolValue = true;
        RequireProperty(serialized, "drawDoorCells")
            .boolValue = true;
        RequireProperty(serialized, "drawCellOverrides")
            .boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(template);
    }

    private static List<Vector2Int> BuildExpectedBlockedCells()
    {
        List<Vector2Int> cells =
            new List<Vector2Int>(104);

        AddRect(cells, 0, 0, 5, 5);       // Bottom Left
        AddRect(cells, 11, 0, 5, 5);      // Bottom Right
        AddRect(cells, 0, 11, 5, 5);      // Top Left
        AddRect(cells, 11, 11, 5, 5);     // Top Right
        AddRect(cells, 7, 7, 2, 2);       // Center Island

        return cells;
    }

    private static void AddRect(
        List<Vector2Int> cells,
        int startX,
        int startY,
        int width,
        int height)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
    }

    private static void RebuildGeneratedColliders(
        Transform collidersRoot)
    {
        Transform existing =
            collidersRoot.Find(GeneratedRootName);

        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(
                existing.gameObject);
        }

        Transform generatedRoot =
            CreateEmptyChild(
                collidersRoot,
                GeneratedRootName);

        Transform interiorRoot =
            CreateEmptyChild(
                generatedRoot,
                "InteriorBlocks");

        CreateBoxCollider(
            "Block_BottomLeft_5x5",
            interiorRoot,
            new Vector2(-5.5f, -5.5f),
            new Vector2(5f, 5f));

        CreateBoxCollider(
            "Block_BottomRight_5x5",
            interiorRoot,
            new Vector2(5.5f, -5.5f),
            new Vector2(5f, 5f));

        CreateBoxCollider(
            "Block_TopLeft_5x5",
            interiorRoot,
            new Vector2(-5.5f, 5.5f),
            new Vector2(5f, 5f));

        CreateBoxCollider(
            "Block_TopRight_5x5",
            interiorRoot,
            new Vector2(5.5f, 5.5f),
            new Vector2(5f, 5f));

        CreateBoxCollider(
            "Block_CenterIsland_2x2",
            interiorRoot,
            Vector2.zero,
            new Vector2(2f, 2f));

        Transform perimeterRoot =
            CreateEmptyChild(
                generatedRoot,
                "PerimeterWalls");

        // 16 单位边长，中央保留 2 单位 Socket 开口。
        // 每侧因此是 7 + 2(open) + 7。
        float segmentLength =
            (ExpectedSize.x - DoorWidthInCells) * 0.5f;
        float segmentCenter =
            DoorWidthInCells * 0.5f + segmentLength * 0.5f;
        float halfWidth = ExpectedSize.x * 0.5f;
        float halfHeight = ExpectedSize.y * 0.5f;

        CreateBoxCollider(
            "Wall_North_Left",
            perimeterRoot,
            new Vector2(-segmentCenter, halfHeight),
            new Vector2(segmentLength, WallThickness));

        CreateBoxCollider(
            "Wall_North_Right",
            perimeterRoot,
            new Vector2(segmentCenter, halfHeight),
            new Vector2(segmentLength, WallThickness));

        CreateBoxCollider(
            "Wall_South_Left",
            perimeterRoot,
            new Vector2(-segmentCenter, -halfHeight),
            new Vector2(segmentLength, WallThickness));

        CreateBoxCollider(
            "Wall_South_Right",
            perimeterRoot,
            new Vector2(segmentCenter, -halfHeight),
            new Vector2(segmentLength, WallThickness));

        CreateBoxCollider(
            "Wall_East_Bottom",
            perimeterRoot,
            new Vector2(halfWidth, -segmentCenter),
            new Vector2(WallThickness, segmentLength));

        CreateBoxCollider(
            "Wall_East_Top",
            perimeterRoot,
            new Vector2(halfWidth, segmentCenter),
            new Vector2(WallThickness, segmentLength));

        CreateBoxCollider(
            "Wall_West_Bottom",
            perimeterRoot,
            new Vector2(-halfWidth, -segmentCenter),
            new Vector2(WallThickness, segmentLength));

        CreateBoxCollider(
            "Wall_West_Top",
            perimeterRoot,
            new Vector2(-halfWidth, segmentCenter),
            new Vector2(WallThickness, segmentLength));
    }

    private static Transform CreateEmptyChild(
        Transform parent,
        string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static void CreateBoxCollider(
        string objectName,
        Transform parent,
        Vector2 localPosition,
        Vector2 size)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition =
            new Vector3(localPosition.x, localPosition.y, 0f);
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        BoxCollider2D collider =
            child.AddComponent<BoxCollider2D>();

        collider.offset = Vector2.zero;
        collider.size = size;
        collider.isTrigger = false;
    }

    private static List<string> ValidateLoadedPrefab(
        GameObject root,
        DreamRoomTemplate template)
    {
        List<string> errors = new List<string>();

        if (root == null)
        {
            errors.Add("Prefab 内容为空。" );
            return errors;
        }

        if (template == null)
        {
            errors.Add("根节点缺少 DreamRoomTemplate。" );
            return errors;
        }

        if (template.SizeInCells != ExpectedSize)
        {
            errors.Add(
                "Size 应为 16x16。" );
        }

        List<string> templateErrors =
            template.GetValidationErrors();

        for (int i = 0; i < templateErrors.Count; i++)
        {
            errors.Add(
                "DreamRoomTemplate：" + templateErrors[i]);
        }

        List<Vector2Int> expectedBlocked =
            BuildExpectedBlockedCells();
        List<Vector2Int> actualBlocked =
            new List<Vector2Int>();
        List<Vector2Int> walkable =
            new List<Vector2Int>();
        List<Vector2Int> occupied =
            new List<Vector2Int>();

        template.GetBlockedCells(actualBlocked);
        template.GetWalkableCells(walkable);
        template.GetOccupiedCells(occupied);

        if (occupied.Count != 256)
        {
            errors.Add(
                "Occupied 应为完整 16x16=256 Cells，实际 " +
                occupied.Count + "。" );
        }

        HashSet<Vector2Int> expectedSet =
            new HashSet<Vector2Int>(expectedBlocked);
        HashSet<Vector2Int> actualSet =
            new HashSet<Vector2Int>(actualBlocked);

        if (!expectedSet.SetEquals(actualSet))
        {
            errors.Add(
                "Blocked Cells 与 P10.1 契约不一致。" );
        }

        if (actualBlocked.Count != 104)
        {
            errors.Add(
                "Blocked 应为 104 Cells，实际 " +
                actualBlocked.Count + "。" );
        }

        if (walkable.Count != 152)
        {
            errors.Add(
                "Walkable 应为 152 Cells，实际 " +
                walkable.Count + "。" );
        }

        ValidateDoorCells(template, errors);
        ValidateColliderHierarchy(root.transform, errors);

        return errors;
    }

    private static void ValidateDoorCells(
        DreamRoomTemplate template,
        List<string> errors)
    {
        DreamRoomDoorDirection[] directions =
        {
            DreamRoomDoorDirection.North,
            DreamRoomDoorDirection.East,
            DreamRoomDoorDirection.South,
            DreamRoomDoorDirection.West
        };

        for (int i = 0; i < directions.Length; i++)
        {
            string socketId = directions[i] + "_0";
            DreamRoomDoorSocket socket;

            if (!template.TryGetSocket(socketId, out socket))
            {
                errors.Add(
                    "缺少 Socket：" + socketId + "。" );
                continue;
            }

            if (socket.DoorWidthInCells != DoorWidthInCells)
            {
                errors.Add(
                    socketId + " 门宽不是 2 Cell。" );
            }

            List<Vector2Int> doorCells =
                socket.GetLocalInsideCells();

            for (int cellIndex = 0;
                 cellIndex < doorCells.Count;
                 cellIndex++)
            {
                Vector2Int cell = doorCells[cellIndex];

                if (!template.IsWalkableCell(cell))
                {
                    errors.Add(
                        socketId + " 门内格 " + cell +
                        " 必须保持 Walkable。" );
                }
            }
        }
    }

    private static void ValidateColliderHierarchy(
        Transform root,
        List<string> errors)
    {
        Transform generated =
            root.Find(
                "Navigation/Colliders/" +
                GeneratedRootName);

        if (generated == null)
        {
            errors.Add(
                "缺少 Navigation/Colliders/" +
                GeneratedRootName + "。" );
            return;
        }

        Transform interior =
            generated.Find("InteriorBlocks");
        Transform perimeter =
            generated.Find("PerimeterWalls");

        if (interior == null)
        {
            errors.Add("缺少 InteriorBlocks。" );
        }
        else
        {
            int count =
                interior.GetComponentsInChildren<
                    BoxCollider2D>(true).Length;

            if (count != 5)
            {
                errors.Add(
                    "Interior BoxCollider2D 应为 5 个，实际 " +
                    count + "。" );
            }
        }

        if (perimeter == null)
        {
            errors.Add("缺少 PerimeterWalls。" );
        }
        else
        {
            int count =
                perimeter.GetComponentsInChildren<
                    BoxCollider2D>(true).Length;

            if (count != 8)
            {
                errors.Add(
                    "Perimeter BoxCollider2D 应为 8 个，实际 " +
                    count + "。" );
            }
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "DreamRoomTemplate 找不到序列化字段：" +
                propertyName + "。" );
        }

        return property;
    }

    private static void ReportFailure(string message)
    {
        Debug.LogError("[P10.1] " + message);
        EditorUtility.DisplayDialog(
            "P10.1 failed",
            message,
            "OK");
    }
}
