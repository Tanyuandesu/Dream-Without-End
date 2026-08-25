using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.2.5：根据 Crossroad_01 当前 1024x1024 Composite Draft，
/// 把 P10.1 的四角 5x5 粗方块修正为与画面转角更吻合的阶梯式几何。
///
/// 本阶段只改：
/// 1. DreamRoomTemplate.blockedCells：四角阶梯形 + 中央 2x2 安全岛。
/// 2. Navigation/Colliders/P10_1_Geometry：同步重建为同一套阶梯式 BoxCollider2D。
///
/// 本阶段保持不变：
/// - 16x16 房间尺寸。
/// - 四方向 2 Cell Socket。
/// - 中央 2x2 安全岛。
/// - 外周 0.35 厚边界墙及四个 Socket 开口。
/// - P10.2 Composite Draft 视觉。
/// - Catalog / GameScene / DungeonGenerator / DungeonRenderer / A* / Enemy AI。
///
/// 说明：为了不破坏 P10.2 已建立的引用/校验路径，本阶段继续复用
/// Navigation/Colliders/P10_1_Geometry 这个生成根节点名，但其中内容会被完整替换；
/// 不保留旧 5x5 Collider 并行版本。
/// </summary>
public static class DreamRoomProductionGeometryRefineP1025
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.2.5/";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string GeneratedRootName =
        "P10_1_Geometry";

    private const string CompositeDraftName =
        "CompositeDraft_Runtime";

    private static readonly Vector2Int ExpectedSize =
        new Vector2Int(16, 16);

    private const float WallThickness = 0.35f;
    private const int DoorWidthInCells = 2;

    private const int ExpectedOccupiedCount = 256;
    private const int ExpectedBlockedCount = 108;
    private const int ExpectedWalkableCount = 148;
    private const int ExpectedInteriorColliderCount = 21;
    private const int ExpectedPerimeterColliderCount = 8;

    [MenuItem(
        MenuRoot + "Apply Refined Corner Geometry",
        false,
        2725)]
    private static void ApplyRefinedCornerGeometry()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "请先退出 Prefab Mode，再执行 P10.2.5。",
                "OK");
            return;
        }

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            ReportFailure(
                "找不到 Crossroad_01 Prefab：\n" +
                CrossroadPrefabPath +
                "\n\n请先完成 P10.0 ~ P10.2。" );
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
                    "P10.2.5 只接受 16x16 Crossroad。当前 Size=" +
                    template.SizeInCells.x + "x" +
                    template.SizeInCells.y + "。" );
            }

            Transform compositeDraft =
                root.transform.Find(
                    "Visual/Floor/" + CompositeDraftName);

            if (compositeDraft == null)
            {
                throw new InvalidOperationException(
                    "找不到 P10.2 Composite Draft。请先完成 P10.2。" );
            }

            Transform collidersRoot =
                root.transform.Find("Navigation/Colliders");

            if (collidersRoot == null)
            {
                throw new InvalidOperationException(
                    "找不到 Navigation/Colliders。" );
            }

            ApplyRefinedCellContract(template);
            RebuildRefinedColliders(collidersRoot);

            template.RefreshDoorSockets();

            List<string> errors =
                ValidateLoadedPrefab(root, template);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "P10.2.5 保存前校验失败：\n- " +
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
                "[P10.2.5] Crossroad_01 四角阶梯几何已应用。\n" +
                "Size=16x16 | Occupied=256 | Blocked=108 | Walkable=148\n" +
                "CornerShape=4x Stair(6,6,5,4,3,2) | CenterIsland=2x2\n" +
                "InteriorColliders=21 | PerimeterWalls=8\n" +
                "CompositeDraftPreserved=True | SocketContractPreserved=True\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False" );

            EditorUtility.DisplayDialog(
                "P10.2.5 Geometry Refined",
                "Crossroad_01 的四角已由 5x5 粗方块改为阶梯式几何。\n\n" +
                "请重新打开 Prefab，并保持 Cell Grid 可见。\n" +
                "四个转角应更贴近图片中的阶梯/斜切边界。\n\n" +
                "中央岛、Socket、外周墙均未改动。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ReportFailure(
                "P10.2.5 已中止。\n\n" +
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
        MenuRoot + "Validate Refined Corner Geometry",
        false,
        2726)]
    private static void ValidateRefinedCornerGeometry()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "[P10.2.5] 找不到 Crossroad_01 Prefab：" +
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
                    "[P10.2.5] Crossroad_01 精修几何校验失败：\n- " +
                    string.Join("\n- ", errors));
                return;
            }

            List<Vector2Int> blocked =
                new List<Vector2Int>();
            List<Vector2Int> walkable =
                new List<Vector2Int>();
            List<Vector2Int> occupied =
                new List<Vector2Int>();

            template.GetBlockedCells(blocked);
            template.GetWalkableCells(walkable);
            template.GetOccupiedCells(occupied);

            Debug.Log(
                "[P10.2.5] Crossroad_01 精修几何校验通过。\n" +
                "Size=16x16 | Occupied=" + occupied.Count +
                " | Blocked=" + blocked.Count +
                " | Walkable=" + walkable.Count + "\n" +
                "Corners=4x Stair(6,6,5,4,3,2) | CenterIsland=2x2\n" +
                "InteriorColliders=21 | PerimeterWalls=8\n" +
                "DoorCells=North/East/South/West all walkable\n" +
                "Visual=P10.2 Composite Draft preserved\n" +
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

    private static void ApplyRefinedCellContract(
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

        // 单一权威：完整 16x16 占格；Walkable 继续由 Occupied - Blocked 自动计算。
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
            new List<Vector2Int>(ExpectedBlockedCount);

        // Bottom Left：从外角向交叉口逐层退让。
        AddRect(cells, 0, 0, 6, 2);
        AddRect(cells, 0, 2, 5, 1);
        AddRect(cells, 0, 3, 4, 1);
        AddRect(cells, 0, 4, 3, 1);
        AddRect(cells, 0, 5, 2, 1);

        // Bottom Right：水平镜像。
        AddRect(cells, 10, 0, 6, 2);
        AddRect(cells, 11, 2, 5, 1);
        AddRect(cells, 12, 3, 4, 1);
        AddRect(cells, 13, 4, 3, 1);
        AddRect(cells, 14, 5, 2, 1);

        // Top Left：垂直镜像。
        AddRect(cells, 0, 14, 6, 2);
        AddRect(cells, 0, 13, 5, 1);
        AddRect(cells, 0, 12, 4, 1);
        AddRect(cells, 0, 11, 3, 1);
        AddRect(cells, 0, 10, 2, 1);

        // Top Right：水平 + 垂直镜像。
        AddRect(cells, 10, 14, 6, 2);
        AddRect(cells, 11, 13, 5, 1);
        AddRect(cells, 12, 12, 4, 1);
        AddRect(cells, 13, 11, 3, 1);
        AddRect(cells, 14, 10, 2, 1);

        // 中央安全岛保持 P10.1 的 2x2。
        AddRect(cells, 7, 7, 2, 2);

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

    private static void RebuildRefinedColliders(
        Transform collidersRoot)
    {
        // 不保留旧 P10.1 5x5 Collider。删除后在原路径下完整重建。
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

        // Cell 坐标 -> Local 坐标：
        // 16x16 房间中心为 (0,0)，Cell(0,0) 中心为 (-7.5,-7.5)。
        // 每个 Box 都严格覆盖整数 Cell 联集，因此与 A* Blocked Cells 保持同一认知。

        // Bottom Left
        CreateCellRectCollider("BL_Row01_6x2", interiorRoot, 0, 0, 6, 2);
        CreateCellRectCollider("BL_Row2_5x1", interiorRoot, 0, 2, 5, 1);
        CreateCellRectCollider("BL_Row3_4x1", interiorRoot, 0, 3, 4, 1);
        CreateCellRectCollider("BL_Row4_3x1", interiorRoot, 0, 4, 3, 1);
        CreateCellRectCollider("BL_Row5_2x1", interiorRoot, 0, 5, 2, 1);

        // Bottom Right
        CreateCellRectCollider("BR_Row01_6x2", interiorRoot, 10, 0, 6, 2);
        CreateCellRectCollider("BR_Row2_5x1", interiorRoot, 11, 2, 5, 1);
        CreateCellRectCollider("BR_Row3_4x1", interiorRoot, 12, 3, 4, 1);
        CreateCellRectCollider("BR_Row4_3x1", interiorRoot, 13, 4, 3, 1);
        CreateCellRectCollider("BR_Row5_2x1", interiorRoot, 14, 5, 2, 1);

        // Top Left
        CreateCellRectCollider("TL_Row1415_6x2", interiorRoot, 0, 14, 6, 2);
        CreateCellRectCollider("TL_Row13_5x1", interiorRoot, 0, 13, 5, 1);
        CreateCellRectCollider("TL_Row12_4x1", interiorRoot, 0, 12, 4, 1);
        CreateCellRectCollider("TL_Row11_3x1", interiorRoot, 0, 11, 3, 1);
        CreateCellRectCollider("TL_Row10_2x1", interiorRoot, 0, 10, 2, 1);

        // Top Right
        CreateCellRectCollider("TR_Row1415_6x2", interiorRoot, 10, 14, 6, 2);
        CreateCellRectCollider("TR_Row13_5x1", interiorRoot, 11, 13, 5, 1);
        CreateCellRectCollider("TR_Row12_4x1", interiorRoot, 12, 12, 4, 1);
        CreateCellRectCollider("TR_Row11_3x1", interiorRoot, 13, 11, 3, 1);
        CreateCellRectCollider("TR_Row10_2x1", interiorRoot, 14, 10, 2, 1);

        // Center Island
        CreateCellRectCollider(
            "Block_CenterIsland_2x2",
            interiorRoot,
            7,
            7,
            2,
            2);

        Transform perimeterRoot =
            CreateEmptyChild(
                generatedRoot,
                "PerimeterWalls");

        RebuildPerimeterWalls(perimeterRoot);
    }

    private static void CreateCellRectCollider(
        string objectName,
        Transform parent,
        int startX,
        int startY,
        int width,
        int height)
    {
        float localX =
            startX + width * 0.5f - ExpectedSize.x * 0.5f;
        float localY =
            startY + height * 0.5f - ExpectedSize.y * 0.5f;

        CreateBoxCollider(
            objectName,
            parent,
            new Vector2(localX, localY),
            new Vector2(width, height));
    }

    private static void RebuildPerimeterWalls(
        Transform perimeterRoot)
    {
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
            errors.Add("Size 应为 16x16。" );
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

        if (occupied.Count != ExpectedOccupiedCount)
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
                "Blocked Cells 与 P10.2.5 阶梯契约不一致。" );
        }

        if (actualBlocked.Count != ExpectedBlockedCount)
        {
            errors.Add(
                "Blocked 应为 " + ExpectedBlockedCount +
                " Cells，实际 " + actualBlocked.Count + "。" );
        }

        if (walkable.Count != ExpectedWalkableCount)
        {
            errors.Add(
                "Walkable 应为 " + ExpectedWalkableCount +
                " Cells，实际 " + walkable.Count + "。" );
        }

        Transform compositeDraft =
            root.transform.Find(
                "Visual/Floor/" + CompositeDraftName);

        if (compositeDraft == null)
        {
            errors.Add(
                "P10.2 Composite Draft 不存在。" );
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

            if (count != ExpectedInteriorColliderCount)
            {
                errors.Add(
                    "Interior BoxCollider2D 应为 " +
                    ExpectedInteriorColliderCount +
                    " 个，实际 " + count + "。" );
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

            if (count != ExpectedPerimeterColliderCount)
            {
                errors.Add(
                    "Perimeter BoxCollider2D 应为 " +
                    ExpectedPerimeterColliderCount +
                    " 个，实际 " + count + "。" );
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
        Debug.LogError("[P10.2.5] " + message);
        EditorUtility.DisplayDialog(
            "P10.2.5 failed",
            message,
            "OK");
    }
}
