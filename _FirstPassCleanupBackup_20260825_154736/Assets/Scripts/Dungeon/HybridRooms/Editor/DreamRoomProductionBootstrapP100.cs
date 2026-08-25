using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P10.0：正式房间资产线的第一步。
///
/// 目标：
/// 1. 不修改 GameScene、不切换 Catalog、不碰 DungeonGenerator / DungeonRenderer。
/// 2. 复用已经验证过的 DreamRoomTemplate / DoorSocket 契约。
/// 3. 建立第一间正式房间 Crossroad_01 的可替换骨架。
/// 4. 固定 Floor / Objects / Effects / Sockets / Navigation / SpawnPoints 层级。
/// 5. 让后续美术只替换 Sprite，让碰撞与寻路继续由独立数据负责。
///
/// 当前 P10.0 只建立“骨架”，不会把十字路口加入正式生成。
/// </summary>
public static class DreamRoomProductionBootstrapP100
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.0/";

    private const string ProductionRoot =
        "Assets/DreamDungeon/Production";

    private const string RoomsRoot =
        ProductionRoot + "/Rooms";

    private const string CrossroadRoot =
        RoomsRoot + "/Crossroad_01";

    private const string CrossroadPrefabPath =
        CrossroadRoot + "/Room_Crossroad_01.prefab";

    // P10.0 的正式几何契约：16 x 16 Cell。
    // 现行美术标准为 64 px / Cell，因此最终 Floor 目标为 1024 x 1024 px。
    private static readonly Vector2Int CrossroadSize =
        new Vector2Int(16, 16);

    private const int DoorWidthInCells = 2;

    // 只借用 R3 已存在的 1x1 白色 Sprite 作为占位图。
    // 正式美术接入后即可删除对它的引用，不新增第二份白图资源。
    private const string GrayboxWhiteSpritePath =
        "Assets/DreamDungeon/Generated/R3_Graybox/Shared/GrayboxWhite.png";

    [MenuItem(
        MenuRoot + "Create Crossroad Production Scaffold",
        false,
        2700)]
    private static void CreateCrossroadProductionScaffold()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "请先退出 Prefab Mode，再建立 P10.0 正式房间骨架。",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath) != null &&
            !EditorUtility.DisplayDialog(
                "Regenerate Crossroad_01 scaffold?",
                "将只重建已知资产：\n\n" +
                CrossroadPrefabPath +
                "\n\n不会修改 GameScene、Catalog、DungeonGenerator、" +
                "DungeonRenderer 或现有 Graybox Prefab。",
                "Regenerate",
                "Cancel"))
        {
            return;
        }

        try
        {
            EnsureFolder(ProductionRoot);
            EnsureFolder(RoomsRoot);
            EnsureFolder(CrossroadRoot);

            Sprite placeholderSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    GrayboxWhiteSpritePath);

            if (placeholderSprite == null)
            {
                throw new InvalidOperationException(
                    "找不到 R3 GrayboxWhite 占位 Sprite：" +
                    GrayboxWhiteSpritePath +
                    "。请确认 R3 Graybox 资产仍存在。" );
            }

            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            GameObject root = null;

            try
            {
                root = BuildCrossroadScaffold(
                    previewScene,
                    placeholderSprite);

                DreamRoomTemplate template =
                    root.GetComponent<DreamRoomTemplate>();

                List<string> errors =
                    template.GetValidationErrors();

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "P10.0 Crossroad_01 保存前校验失败：\n- " +
                        string.Join("\n- ", errors));
                }

                bool success;
                GameObject savedPrefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        CrossroadPrefabPath,
                        out success);

                if (!success || savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "无法保存 Prefab：" +
                        CrossroadPrefabPath);
                }
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(
                        previewScene);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CrossroadPrefabPath);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                "[P10.0] Crossroad_01 正式房间骨架已建立。\n" +
                "Prefab=" + CrossroadPrefabPath + "\n" +
                "Contract=16x16 Cells | Cell=1 Unity Unit | " +
                "ArtTarget=1024x1024 @ PPU64\n" +
                "Sockets=North/East/South/West | Width=2 Cells\n" +
                "Visual=Floor + Objects + Effects\n" +
                "Navigation=独立 Colliders Root\n" +
                "RuntimeCatalogChanged=False | GameSceneChanged=False | " +
                "CoreDungeonCodeChanged=False" );

            EditorUtility.DisplayDialog(
                "P10.0 Crossroad Scaffold Ready",
                "已建立 Crossroad_01 正式骨架。\n\n" +
                "本阶段不会进入随机生成。\n" +
                "下一步先在 Prefab Mode 检查 16x16 格与四个 2 格 Socket。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "P10.0 creation failed",
                "建立中止。请把 Console 第一条红色错误发给我。",
                "OK");
        }
    }

    [MenuItem(
        MenuRoot + "Validate Crossroad Production Scaffold",
        false,
        2701)]
    private static void ValidateCrossroadProductionScaffold()
    {
        List<string> errors = new List<string>();

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            errors.Add(
                "找不到 Crossroad_01 Prefab：" +
                CrossroadPrefabPath);
            ReportValidation(errors, null);
            return;
        }

        GameObject loadedRoot = null;

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(
                    CrossroadPrefabPath);

            DreamRoomTemplate template =
                loadedRoot.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                errors.Add("Prefab 根节点缺少 DreamRoomTemplate。" );
            }
            else
            {
                if (!string.Equals(
                        template.TemplateId,
                        "Production_Crossroad_01",
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        "Template Id 应为 Production_Crossroad_01，实际为 " +
                        template.TemplateId + "。" );
                }

                if (template.SizeInCells != CrossroadSize)
                {
                    errors.Add(
                        "Size In Cells 应为 16x16，实际为 " +
                        template.SizeInCells.x + "x" +
                        template.SizeInCells.y + "。" );
                }

                List<string> templateErrors =
                    template.GetValidationErrors();

                for (int i = 0; i < templateErrors.Count; i++)
                {
                    errors.Add(
                        "DreamRoomTemplate：" +
                        templateErrors[i]);
                }

                ValidateVisualHierarchy(
                    loadedRoot.transform,
                    errors);

                ValidateNavigationHierarchy(
                    loadedRoot.transform,
                    errors);

                ValidateSockets(
                    template,
                    errors);
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                "Prefab Contents 校验异常：\n" +
                exception);
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    loadedRoot);
            }
        }

        ReportValidation(errors, prefab);
    }

    private static GameObject BuildCrossroadScaffold(
        Scene previewScene,
        Sprite placeholderSprite)
    {
        GameObject root =
            new GameObject("Room_Crossroad_01");

        SceneManager.MoveGameObjectToScene(
            root,
            previewScene);

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        DreamRoomTemplate template =
            root.AddComponent<DreamRoomTemplate>();

        Transform visualRoot =
            CreateEmptyChild(root.transform, "Visual");

        Transform floorRoot =
            CreateEmptyChild(visualRoot, "Floor");

        // 占位图单独放在子节点，故意不污染 Floor 根节点的 Scale。
        // 正式 1024x1024 / PPU64 Floor 接入时，Floor 根节点保持 Scale=1，
        // 删除或关闭 Floor_Placeholder 后直接放入正式 Sprite 即可。
        CreateSpriteObject(
            "Floor_Placeholder",
            floorRoot,
            placeholderSprite,
            new Color(0.12f, 0.13f, 0.16f, 1f),
            Vector3.zero,
            new Vector3(
                CrossroadSize.x,
                CrossroadSize.y,
                1f),
            sortingOrder: -10,
            addCollider: false);

        Transform objectsRoot =
            CreateEmptyChild(visualRoot, "Objects");

        CreateEmptyChild(visualRoot, "Effects");

        Transform blockersRoot =
            CreateEmptyChild(objectsRoot, "ClosedBlockers");

        Transform socketsRoot =
            CreateEmptyChild(root.transform, "Sockets");

        Transform navigationRoot =
            CreateEmptyChild(root.transform, "Navigation");

        CreateEmptyChild(navigationRoot, "Colliders");

        Transform spawnPointsRoot =
            CreateEmptyChild(root.transform, "SpawnPoints");

        Dictionary<DreamRoomDoorDirection, GameObject>
            blockers =
                new Dictionary<DreamRoomDoorDirection, GameObject>();

        blockers.Add(
            DreamRoomDoorDirection.North,
            CreateDoorBlocker(
                DreamRoomDoorDirection.North,
                blockersRoot,
                placeholderSprite));

        blockers.Add(
            DreamRoomDoorDirection.East,
            CreateDoorBlocker(
                DreamRoomDoorDirection.East,
                blockersRoot,
                placeholderSprite));

        blockers.Add(
            DreamRoomDoorDirection.South,
            CreateDoorBlocker(
                DreamRoomDoorDirection.South,
                blockersRoot,
                placeholderSprite));

        blockers.Add(
            DreamRoomDoorDirection.West,
            CreateDoorBlocker(
                DreamRoomDoorDirection.West,
                blockersRoot,
                placeholderSprite));

        CreateDoorSocket(
            DreamRoomDoorDirection.North,
            socketsRoot,
            blockers[DreamRoomDoorDirection.North]);

        CreateDoorSocket(
            DreamRoomDoorDirection.East,
            socketsRoot,
            blockers[DreamRoomDoorDirection.East]);

        CreateDoorSocket(
            DreamRoomDoorDirection.South,
            socketsRoot,
            blockers[DreamRoomDoorDirection.South]);

        CreateDoorSocket(
            DreamRoomDoorDirection.West,
            socketsRoot,
            blockers[DreamRoomDoorDirection.West]);

        ConfigureTemplate(
            template,
            visualRoot,
            socketsRoot,
            navigationRoot,
            spawnPointsRoot);

        template.RefreshDoorSockets();
        template.RefreshSpawnPoints();

        return root;
    }

    private static GameObject CreateDoorBlocker(
        DreamRoomDoorDirection direction,
        Transform parent,
        Sprite sprite)
    {
        bool horizontal =
            direction == DreamRoomDoorDirection.North ||
            direction == DreamRoomDoorDirection.South;

        Vector3 position = Vector3.zero;

        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position.y = CrossroadSize.y * 0.5f;
                break;

            case DreamRoomDoorDirection.East:
                position.x = CrossroadSize.x * 0.5f;
                break;

            case DreamRoomDoorDirection.South:
                position.y = -CrossroadSize.y * 0.5f;
                break;

            case DreamRoomDoorDirection.West:
                position.x = -CrossroadSize.x * 0.5f;
                break;
        }

        Vector3 scale = horizontal
            ? new Vector3(DoorWidthInCells, 0.35f, 1f)
            : new Vector3(0.35f, DoorWidthInCells, 1f);

        return CreateSpriteObject(
            "Blocker_" + direction + "_0",
            parent,
            sprite,
            new Color(0.55f, 0.18f, 0.22f, 1f),
            position,
            scale,
            sortingOrder: 20,
            addCollider: true);
    }

    private static void CreateDoorSocket(
        DreamRoomDoorDirection direction,
        Transform socketsRoot,
        GameObject blocker)
    {
        string socketId = direction + "_0";

        GameObject socketObject =
            new GameObject("Door_" + socketId);

        socketObject.transform.SetParent(
            socketsRoot,
            worldPositionStays: false);

        socketObject.transform.localPosition =
            GetDoorCenterLocal(direction);

        socketObject.transform.localRotation =
            Quaternion.identity;

        socketObject.transform.localScale =
            Vector3.one;

        DreamRoomDoorSocket socket =
            socketObject.AddComponent<DreamRoomDoorSocket>();

        socket.Configure(
            socketId,
            direction,
            GetDoorInsideCell(direction),
            DoorWidthInCells,
            blocker);
    }

    private static Vector2Int GetDoorInsideCell(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Vector2Int(
                    CrossroadSize.x / 2,
                    CrossroadSize.y - 1);

            case DreamRoomDoorDirection.East:
                return new Vector2Int(
                    CrossroadSize.x - 1,
                    CrossroadSize.y / 2);

            case DreamRoomDoorDirection.South:
                return new Vector2Int(
                    CrossroadSize.x / 2,
                    0);

            case DreamRoomDoorDirection.West:
                return new Vector2Int(
                    0,
                    CrossroadSize.y / 2);

            default:
                return Vector2Int.zero;
        }
    }

    private static Vector3 GetDoorCenterLocal(
        DreamRoomDoorDirection direction)
    {
        Vector2Int baseCell =
            GetDoorInsideCell(direction);

        Vector2Int sideways =
            direction.PerpendicularCellOffset();

        int startOffset =
            -(DoorWidthInCells / 2);

        Vector2 total = Vector2.zero;

        for (int i = 0; i < DoorWidthInCells; i++)
        {
            Vector2Int cell =
                baseCell +
                sideways * (startOffset + i);

            total += new Vector2(cell.x, cell.y);
        }

        Vector2 average =
            total / DoorWidthInCells;

        return new Vector3(
            average.x - (CrossroadSize.x - 1) * 0.5f,
            average.y - (CrossroadSize.y - 1) * 0.5f,
            0f);
    }

    private static void ConfigureTemplate(
        DreamRoomTemplate template,
        Transform visualRoot,
        Transform socketsRoot,
        Transform navigationRoot,
        Transform spawnPointsRoot)
    {
        SerializedObject serialized =
            new SerializedObject(template);

        RequireProperty(serialized, "templateId")
            .stringValue = "Production_Crossroad_01";

        RequireProperty(serialized, "sizeInCells")
            .vector2IntValue = CrossroadSize;

        // P10.0 还没有进入 Production Catalog，因此选择权重只是安全占位。
        RequireProperty(serialized, "randomWeight")
            .intValue = 1;

        RequireProperty(serialized, "minimumFloor")
            .intValue = 1;

        RequireProperty(serialized, "maximumFloor")
            .intValue = 0;

        RequireProperty(serialized, "maximumInstancesPerFloor")
            .intValue = 0;

        // 第一间正式房先锁定朝向，减少接入变量。
        RequireProperty(serialized, "allowQuarterTurns")
            .boolValue = false;

        RequireProperty(serialized, "roomTags")
            .intValue = (int)DreamRoomTag.Standard;

        // P10.1 再按十字路口视觉建立真正的几何契约。
        // P10.0 保持完整矩形占格、默认全可行走，以免提前猜图。
        RequireProperty(serialized, "occupiedCells")
            .arraySize = 0;

        RequireProperty(serialized, "walkableCells")
            .arraySize = 0;

        RequireProperty(serialized, "blockedCells")
            .arraySize = 0;

        RequireProperty(serialized, "visualRoot")
            .objectReferenceValue = visualRoot;

        RequireProperty(serialized, "socketsRoot")
            .objectReferenceValue = socketsRoot;

        RequireProperty(serialized, "navigationRoot")
            .objectReferenceValue = navigationRoot;

        RequireProperty(serialized, "spawnPointsRoot")
            .objectReferenceValue = spawnPointsRoot;

        RequireProperty(serialized, "autoCollectDoorSockets")
            .boolValue = true;

        RequireProperty(serialized, "doorSockets")
            .arraySize = 0;

        RequireProperty(serialized, "autoCollectSpawnPoints")
            .boolValue = true;

        RequireProperty(serialized, "spawnPoints")
            .arraySize = 0;

        RequireProperty(serialized, "drawCellGrid")
            .boolValue = true;

        RequireProperty(serialized, "drawDoorCells")
            .boolValue = true;

        RequireProperty(serialized, "drawCellOverrides")
            .boolValue = true;

        RequireProperty(serialized, "drawSpawnPoints")
            .boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(template);
    }

    private static void ValidateVisualHierarchy(
        Transform root,
        List<string> errors)
    {
        Transform visual = root.Find("Visual");

        if (visual == null)
        {
            errors.Add("缺少 Visual。" );
            return;
        }

        Transform floor = visual.Find("Floor");
        Transform objects = visual.Find("Objects");
        Transform effects = visual.Find("Effects");

        if (floor == null)
        {
            errors.Add("缺少 Visual/Floor。" );
        }
        else
        {
            if (floor.localScale != Vector3.one)
            {
                errors.Add("Visual/Floor 根节点 Scale 必须保持 1,1,1。" );
            }

            if (floor.GetComponentInChildren<SpriteRenderer>(true) == null)
            {
                errors.Add("Visual/Floor 下找不到占位 SpriteRenderer。" );
            }
        }

        if (objects == null)
        {
            errors.Add("缺少 Visual/Objects。" );
        }

        if (effects == null)
        {
            errors.Add("缺少 Visual/Effects。" );
        }
    }

    private static void ValidateNavigationHierarchy(
        Transform root,
        List<string> errors)
    {
        Transform navigation = root.Find("Navigation");

        if (navigation == null)
        {
            errors.Add("缺少 Navigation。" );
            return;
        }

        if (navigation.Find("Colliders") == null)
        {
            errors.Add("缺少 Navigation/Colliders。" );
        }
    }

    private static void ValidateSockets(
        DreamRoomTemplate template,
        List<string> errors)
    {
        if (template.DoorSockets.Count != 4)
        {
            errors.Add(
                "Crossroad_01 应有 4 个 Socket，实际为 " +
                template.DoorSockets.Count + "。" );
            return;
        }

        DreamRoomDoorDirection[] expectedDirections =
        {
            DreamRoomDoorDirection.North,
            DreamRoomDoorDirection.East,
            DreamRoomDoorDirection.South,
            DreamRoomDoorDirection.West
        };

        for (int i = 0; i < expectedDirections.Length; i++)
        {
            DreamRoomDoorDirection direction =
                expectedDirections[i];

            string socketId = direction + "_0";
            DreamRoomDoorSocket socket;

            if (!template.TryGetSocket(
                    socketId,
                    out socket))
            {
                errors.Add("缺少 Socket：" + socketId + "。" );
                continue;
            }

            if (socket.Direction != direction)
            {
                errors.Add(
                    socketId + " Direction 不匹配。" );
            }

            if (socket.DoorWidthInCells != DoorWidthInCells)
            {
                errors.Add(
                    socketId + " 门宽应为 2 Cell。" );
            }

            if (socket.LocalInsideCell !=
                GetDoorInsideCell(direction))
            {
                errors.Add(
                    socketId + " Local Inside Cell 不匹配。" );
            }

            if (socket.ClosedBlocker == null)
            {
                errors.Add(
                    socketId + " 缺少 Closed Blocker。" );
            }
        }
    }

    private static void ReportValidation(
        List<string> errors,
        UnityEngine.Object context)
    {
        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.0] Crossroad_01 正式骨架校验失败：\n- " +
                string.Join("\n- ", errors),
                context);

            EditorUtility.DisplayDialog(
                "P10.0 validation failed",
                "校验失败。请把 Console 第一条红色错误发给我。",
                "OK");
            return;
        }

        Debug.Log(
            "[P10.0] Crossroad_01 正式骨架校验通过。\n" +
            "Size=16x16 | ArtTarget=1024x1024 @ PPU64\n" +
            "VisualHierarchy=Floor/Objects/Effects\n" +
            "Sockets=4x2Cells | ClosedBlockers=Ready\n" +
            "Navigation/Colliders=ReadyForP10.1\n" +
            "RuntimeIntegration=NotStartedByDesign",
            context);

        EditorUtility.DisplayDialog(
            "P10.0 validation passed",
            "Crossroad_01 的正式 Prefab 骨架与四向 Socket 契约正常。",
            "OK");
    }

    private static Transform CreateEmptyChild(
        Transform parent,
        string name)
    {
        GameObject child = new GameObject(name);

        child.transform.SetParent(
            parent,
            worldPositionStays: false);

        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.transform;
    }

    private static GameObject CreateSpriteObject(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector3 localPosition,
        Vector3 localScale,
        int sortingOrder,
        bool addCollider)
    {
        GameObject gameObject = new GameObject(name);

        gameObject.transform.SetParent(
            parent,
            worldPositionStays: false);

        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = localScale;

        SpriteRenderer renderer =
            gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }

        return gameObject;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(
                serialized.targetObject.GetType().Name,
                propertyName);
        }

        return property;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        int slash = assetPath.LastIndexOf('/');

        if (slash <= 0)
        {
            throw new InvalidOperationException(
                "无效的 Assets 文件夹路径：" + assetPath);
        }

        string parent = assetPath.Substring(0, slash);
        string folderName = assetPath.Substring(slash + 1);

        EnsureFolder(parent);

        string guid = AssetDatabase.CreateFolder(
            parent,
            folderName);

        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException(
                "无法建立文件夹：" + assetPath);
        }
    }
}
