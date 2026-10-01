using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ArtRoom_01 重做 Stage 1。
///
/// 目的：
/// - 用干净的 11x18 高精度房间骨架覆盖旧 13x21 Prefab
/// - 保留原 Prefab / Runtime PNG 的 asset path 与 .meta GUID
/// - 清空旧 BlockedCells / Interior Collider，不继承旧碰撞垃圾
/// - 保留单南门、2 格门宽
/// - 统一视觉层级：Floor -10 / Objects 0 / Actor 20 / Effects 25 / Foreground 30
///
/// 注意：此工具不会把房间重新发布到 Production_Main。
/// Stage 2 完成正式美术与碰撞后，再执行发布。
/// </summary>
public static class ArtRoomRebuildStage1
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Production Rooms/ArtRoom Rebuild/" +
        "Stage 1 - Rebuild 11x18 Scaffold";

    private const string RoomFolder =
        "Assets/DreamDungeon/Production/Rooms/ArtRoom_01";

    private const string PrefabPath =
        RoomFolder + "/Room_ArtRoom_01.prefab";

    private const string RuntimeFolder =
        RoomFolder + "/Art/Runtime";

    private const string FloorPath =
        RuntimeFolder + "/Room_ArtRoom_01_Floor.png";

    private const string ObjectsPath =
        RuntimeFolder + "/Room_ArtRoom_01_Objects.png";

    private const string ForegroundPath =
        RuntimeFolder + "/Room_ArtRoom_01_Foreground.png";

    private const string EffectsPath =
        RuntimeFolder + "/Room_ArtRoom_01_Effects.png";

    private static readonly Vector2Int RoomSize =
        new Vector2Int(11, 18);

    private const int PixelsPerCell = 64;
    private const int DoorWidthInCells = 2;

    private const int FloorSortingOrder = -10;
    private const int ObjectsSortingOrder = 0;
    private const int EffectsSortingOrder = 25;
    private const int ForegroundSortingOrder = 30;

    private const float ClosedBlockerThickness = 0.35f;
    private const float PerimeterWallThickness = 0.35f;

    [MenuItem(MenuPath, false, 2780)]
    private static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Rebuild",
                "请先退出 Play Mode。",
                "OK");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Rebuild",
                "请先退出 Prefab Mode。",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Rebuild",
                "找不到旧 ArtRoom Prefab：\n" + PrefabPath,
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "ArtRoom_01 Stage 1",
            "将把旧 13x21 ArtRoom_01 覆盖成干净的 11x18 骨架。\n\n" +
            "会保留原 asset path 与 .meta GUID，" +
            "但会清空旧房间内部碰撞、BlockedCells 与旧 Runtime PNG 内容。\n\n" +
            "Production_Main 当前已移除此房间，因此不会影响运行时随机生成。\n\n" +
            "继续？",
            "Rebuild",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        Scene previewScene = default;
        GameObject root = null;

        try
        {
            EnsureRuntimeFolder();

            RewriteTransparentRuntimePng(FloorPath);
            RewriteTransparentRuntimePng(ObjectsPath);
            RewriteTransparentRuntimePng(ForegroundPath);
            RewriteTransparentRuntimePng(EffectsPath);

            Sprite floorSprite = RequireRuntimeSprite(FloorPath);
            Sprite objectsSprite = RequireRuntimeSprite(ObjectsPath);
            Sprite foregroundSprite = RequireRuntimeSprite(ForegroundPath);
            Sprite effectsSprite = RequireRuntimeSprite(EffectsPath);

            previewScene = EditorSceneManager.NewPreviewScene();

            root = BuildCleanScaffold(
                previewScene,
                floorSprite,
                objectsSprite,
                foregroundSprite,
                effectsSprite);

            List<string> validationErrors =
                root.GetComponent<DreamRoomTemplate>()
                    .GetValidationErrors();

            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "新 ArtRoomTemplate 校验失败：\n- " +
                    string.Join("\n- ", validationErrors));
            }

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath);

            if (saved == null)
            {
                throw new InvalidOperationException(
                    "PrefabUtility.SaveAsPrefabAsset 返回 null。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateSavedPrefab();

            Debug.Log(
                "[ArtRoom Rebuild Stage 1] 完成。\n" +
                "Prefab=" + PrefabPath + "\n" +
                "Size=11x18 | Pixels=704x1152 @ PPU64\n" +
                "Door=South_0 / Width=2\n" +
                "BlockedCells=0 | InteriorColliders=0\n" +
                "Sorting=Floor:-10 / Objects:0 / Actor:20 / Effects:25 / Foreground:30\n" +
                "ProductionMainPublished=False");

            EditorUtility.DisplayDialog(
                "ArtRoom Stage 1 Passed",
                "ArtRoom_01 已重建为 11x18 干净骨架。\n\n" +
                "旧碰撞与 BlockedCells 已清空。\n" +
                "四张 Runtime PNG 已重置为 704x1152 真透明占位图。\n" +
                "房间仍未重新加入 Production_Main。\n\n" +
                "下一步：替换正式画室美术，再重新制作碰撞。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "ArtRoom Stage 1 Failed",
                "重建失败。请不要继续后续步骤。\n" +
                "把 Console 第一条红色错误发给我。",
                "OK");
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }
    }

    private static GameObject BuildCleanScaffold(
        Scene previewScene,
        Sprite floorSprite,
        Sprite objectsSprite,
        Sprite foregroundSprite,
        Sprite effectsSprite)
    {
        GameObject root =
            new GameObject("Room_ArtRoom_01");

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

        Transform objectsRoot =
            CreateEmptyChild(visualRoot, "Objects");

        Transform foregroundRoot =
            CreateEmptyChild(visualRoot, "Foreground");

        Transform effectsRoot =
            CreateEmptyChild(visualRoot, "Effects");

        CreateRuntimeSprite(
            "Floor_Runtime",
            floorRoot,
            floorSprite,
            FloorSortingOrder);

        CreateRuntimeSprite(
            "Objects_Runtime",
            objectsRoot,
            objectsSprite,
            ObjectsSortingOrder);

        CreateRuntimeSprite(
            "Foreground_Runtime",
            foregroundRoot,
            foregroundSprite,
            ForegroundSortingOrder);

        CreateRuntimeSprite(
            "Effects_Runtime",
            effectsRoot,
            effectsSprite,
            EffectsSortingOrder);

        Transform blockersRoot =
            CreateEmptyChild(
                objectsRoot,
                "ClosedBlockers");

        Transform socketsRoot =
            CreateEmptyChild(
                root.transform,
                "Sockets");

        Transform navigationRoot =
            CreateEmptyChild(
                root.transform,
                "Navigation");

        Transform collidersRoot =
            CreateEmptyChild(
                navigationRoot,
                "Colliders");

        CreateEmptyChild(
            collidersRoot,
            "Interior");

        Transform perimeterRoot =
            CreateEmptyChild(
                collidersRoot,
                "Perimeter");

        Transform spawnPointsRoot =
            CreateEmptyChild(
                root.transform,
                "SpawnPoints");

        CreateSouthSocket(
            socketsRoot,
            blockersRoot);

        ConfigureTemplate(
            template,
            visualRoot,
            socketsRoot,
            navigationRoot,
            spawnPointsRoot);

        template.RefreshDoorSockets();
        template.RefreshSpawnPoints();

        BuildPerimeter(
            template,
            perimeterRoot);

        return root;
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
            .stringValue = "Production_ArtRoom_01";

        RequireProperty(serialized, "sizeInCells")
            .vector2IntValue = RoomSize;

        RequireProperty(serialized, "randomWeight")
            .intValue = 10;

        RequireProperty(serialized, "minimumFloor")
            .intValue = 1;

        RequireProperty(serialized, "maximumFloor")
            .intValue = 0;

        RequireProperty(serialized, "maximumInstancesPerFloor")
            .intValue = 1;

        RequireProperty(serialized, "allowQuarterTurns")
            .boolValue = false;

        RequireProperty(serialized, "roomTags")
            .intValue = (int)DreamRoomTag.Standard;

        RequireProperty(serialized, "roomFidelityTier")
            .enumValueIndex =
                (int)DreamRoomFidelityTier.HighPrecision;

        // Stage 1 不继承旧 13x21 房间的格子覆盖。
        // 空列表 = 完整 11x18 矩形，且暂时全部可走。
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

    private static void CreateSouthSocket(
        Transform socketsRoot,
        Transform blockersRoot)
    {
        Vector2Int insideCell =
            new Vector2Int(
                RoomSize.x / 2,
                0);

        Vector2Int sideways =
            DreamRoomDoorDirection.South
                .PerpendicularCellOffset();

        int startOffset =
            -(DoorWidthInCells / 2);

        Vector2 average = Vector2.zero;

        for (int i = 0; i < DoorWidthInCells; i++)
        {
            Vector2Int cell =
                insideCell +
                sideways * (startOffset + i);

            average += new Vector2(
                cell.x,
                cell.y);
        }

        average /= DoorWidthInCells;

        Vector3 socketPosition =
            new Vector3(
                average.x -
                    (RoomSize.x - 1) * 0.5f,
                average.y -
                    (RoomSize.y - 1) * 0.5f,
                0f);

        GameObject blocker =
            new GameObject("Blocker_South_0");

        blocker.transform.SetParent(
            blockersRoot,
            false);

        blocker.transform.localPosition =
            new Vector3(
                socketPosition.x,
                -RoomSize.y * 0.5f,
                0f);

        BoxCollider2D blockerCollider =
            blocker.AddComponent<BoxCollider2D>();

        blockerCollider.size =
            new Vector2(
                DoorWidthInCells,
                ClosedBlockerThickness);

        blockerCollider.offset = Vector2.zero;
        blockerCollider.isTrigger = false;

        GameObject socketObject =
            new GameObject("Door_South_0");

        socketObject.transform.SetParent(
            socketsRoot,
            false);

        socketObject.transform.localPosition =
            socketPosition;

        DreamRoomDoorSocket socket =
            socketObject.AddComponent<DreamRoomDoorSocket>();

        socket.Configure(
            "South_0",
            DreamRoomDoorDirection.South,
            insideCell,
            DoorWidthInCells,
            blocker);
    }

    private static void BuildPerimeter(
        DreamRoomTemplate template,
        Transform perimeterRoot)
    {
        BuildSidePerimeter(
            template,
            perimeterRoot,
            DreamRoomDoorDirection.North);

        BuildSidePerimeter(
            template,
            perimeterRoot,
            DreamRoomDoorDirection.East);

        BuildSidePerimeter(
            template,
            perimeterRoot,
            DreamRoomDoorDirection.South);

        BuildSidePerimeter(
            template,
            perimeterRoot,
            DreamRoomDoorDirection.West);
    }

    private static void BuildSidePerimeter(
        DreamRoomTemplate template,
        Transform parent,
        DreamRoomDoorDirection direction)
    {
        bool horizontal =
            direction == DreamRoomDoorDirection.North ||
            direction == DreamRoomDoorDirection.South;

        float sideLength =
            horizontal
                ? RoomSize.x
                : RoomSize.y;

        float sideMin =
            -sideLength * 0.5f;

        float sideMax =
            sideLength * 0.5f;

        List<DoorGap> gaps =
            new List<DoorGap>();

        for (int i = 0;
             i < template.DoorSockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[i];

            if (socket == null ||
                socket.Direction != direction)
            {
                continue;
            }

            float center =
                horizontal
                    ? socket.transform.localPosition.x
                    : socket.transform.localPosition.y;

            float halfGap =
                socket.DoorWidthInCells * 0.5f;

            gaps.Add(
                new DoorGap(
                    center - halfGap,
                    center + halfGap));
        }

        gaps.Sort(
            (a, b) =>
                a.Min.CompareTo(b.Min));

        float cursor = sideMin;
        int segmentIndex = 0;

        for (int i = 0;
             i < gaps.Count;
             i++)
        {
            DoorGap gap = gaps[i];

            float gapMin =
                Mathf.Clamp(
                    gap.Min,
                    sideMin,
                    sideMax);

            float gapMax =
                Mathf.Clamp(
                    gap.Max,
                    sideMin,
                    sideMax);

            if (gapMin >
                cursor + 0.001f)
            {
                CreatePerimeterSegment(
                    parent,
                    direction,
                    cursor,
                    gapMin,
                    segmentIndex++);

                cursor =
                    Mathf.Max(
                        cursor,
                        gapMax);
            }
            else
            {
                cursor =
                    Mathf.Max(
                        cursor,
                        gapMax);
            }
        }

        if (cursor <
            sideMax - 0.001f)
        {
            CreatePerimeterSegment(
                parent,
                direction,
                cursor,
                sideMax,
                segmentIndex);
        }
    }

    private static void CreatePerimeterSegment(
        Transform parent,
        DreamRoomDoorDirection direction,
        float segmentMin,
        float segmentMax,
        int index)
    {
        float length =
            segmentMax - segmentMin;

        if (length <= 0.001f)
        {
            return;
        }

        float center =
            (segmentMin + segmentMax) * 0.5f;

        float halfWidth =
            RoomSize.x * 0.5f;

        float halfHeight =
            RoomSize.y * 0.5f;

        Vector2 position;
        Vector2 colliderSize;

        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position =
                    new Vector2(
                        center,
                        halfHeight);

                colliderSize =
                    new Vector2(
                        length,
                        PerimeterWallThickness);
                break;

            case DreamRoomDoorDirection.East:
                position =
                    new Vector2(
                        halfWidth,
                        center);

                colliderSize =
                    new Vector2(
                        PerimeterWallThickness,
                        length);
                break;

            case DreamRoomDoorDirection.South:
                position =
                    new Vector2(
                        center,
                        -halfHeight);

                colliderSize =
                    new Vector2(
                        length,
                        PerimeterWallThickness);
                break;

            case DreamRoomDoorDirection.West:
                position =
                    new Vector2(
                        -halfWidth,
                        center);

                colliderSize =
                    new Vector2(
                        PerimeterWallThickness,
                        length);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    null);
        }

        GameObject wall =
            new GameObject(
                "Wall_" +
                direction +
                "_" +
                index);

        wall.transform.SetParent(
            parent,
            false);

        wall.transform.localPosition =
            new Vector3(
                position.x,
                position.y,
                0f);

        BoxCollider2D collider =
            wall.AddComponent<BoxCollider2D>();

        collider.size = colliderSize;
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
    }

    private static void RewriteTransparentRuntimePng(
        string assetPath)
    {
        string absolutePath =
            ToAbsolutePath(assetPath);

        Texture2D texture =
            new Texture2D(
                RoomSize.x * PixelsPerCell,
                RoomSize.y * PixelsPerCell,
                TextureFormat.RGBA32,
                false);

        try
        {
            texture.name =
                Path.GetFileNameWithoutExtension(
                    assetPath);

            texture.SetPixels32(
                new Color32[
                    texture.width *
                    texture.height]);

            texture.Apply(
                false,
                false);

            File.WriteAllBytes(
                absolutePath,
                texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                texture);
        }

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        ConfigureRuntimeSpriteImporter(
            assetPath);
    }

    private static void ConfigureRuntimeSpriteImporter(
        string assetPath)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(
                assetPath) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "无法取得 TextureImporter：" +
                assetPath);
        }

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.spritePixelsPerUnit =
            PixelsPerCell;

        importer.filterMode =
            FilterMode.Point;

        importer.mipmapEnabled =
            false;

        importer.alphaIsTransparency =
            true;

        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.wrapMode =
            TextureWrapMode.Clamp;

        importer.SaveAndReimport();
    }

    private static Sprite RequireRuntimeSprite(
        string assetPath)
    {
        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                assetPath);

        if (sprite == null)
        {
            throw new InvalidOperationException(
                "无法读取 Runtime Sprite：" +
                assetPath);
        }

        return sprite;
    }

    private static void ValidateSavedPrefab()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        if (prefab == null)
        {
            throw new InvalidOperationException(
                "保存后无法重新读取 ArtRoom Prefab。");
        }

        DreamRoomTemplate template =
            prefab.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            throw new InvalidOperationException(
                "保存后的 Prefab 缺少 DreamRoomTemplate。");
        }

        if (template.SizeInCells != RoomSize)
        {
            throw new InvalidOperationException(
                "保存后的尺寸不是 11x18。");
        }

        if (template.BlockedCellOverrides.Count != 0)
        {
            throw new InvalidOperationException(
                "Stage 1 的 BlockedCells 必须为空。");
        }

        if (template.DoorSockets.Count != 1)
        {
            throw new InvalidOperationException(
                "Stage 1 必须只有 1 个 Socket。");
        }

        DreamRoomDoorSocket socket =
            template.DoorSockets[0];

        if (socket == null ||
            socket.Direction !=
                DreamRoomDoorDirection.South ||
            socket.DoorWidthInCells !=
                DoorWidthInCells)
        {
            throw new InvalidOperationException(
                "Stage 1 的 South_0 / 2格门配置异常。");
        }

        RequireSortingOrder(
            prefab,
            "Visual/Floor/Floor_Runtime",
            FloorSortingOrder);

        RequireSortingOrder(
            prefab,
            "Visual/Objects/Objects_Runtime",
            ObjectsSortingOrder);

        RequireSortingOrder(
            prefab,
            "Visual/Effects/Effects_Runtime",
            EffectsSortingOrder);

        RequireSortingOrder(
            prefab,
            "Visual/Foreground/Foreground_Runtime",
            ForegroundSortingOrder);

        Transform interior =
            prefab.transform.Find(
                "Navigation/Colliders/Interior");

        if (interior == null)
        {
            throw new InvalidOperationException(
                "缺少 Navigation/Colliders/Interior。");
        }

        if (interior.GetComponentsInChildren<Collider2D>(true).Length != 0)
        {
            throw new InvalidOperationException(
                "Stage 1 的 Interior Collider 必须为空。");
        }

        // Persistent Prefab Asset 上直接调用 GetComponentInParent
        // 不保证能把子 Socket 的 owner 解析回根 DreamRoomTemplate。
        // 现有 Production Pipeline 已经使用 LoadPrefabContents
        // 规避这个 Unity 资产上下文假阴性，因此这里保持同一做法。
        GameObject loadedRoot = null;

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(
                    PrefabPath);

            if (loadedRoot == null)
            {
                throw new InvalidOperationException(
                    "无法加载保存后的 Prefab Contents。");
            }

            DreamRoomTemplate loadedTemplate =
                loadedRoot.GetComponent<DreamRoomTemplate>();

            if (loadedTemplate == null)
            {
                throw new InvalidOperationException(
                    "加载后的 Prefab 根节点缺少 DreamRoomTemplate。");
            }

            loadedTemplate.RefreshDoorSockets();
            loadedTemplate.RefreshSpawnPoints();

            List<string> errors =
                loadedTemplate.GetValidationErrors();

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "保存后的 Template 校验失败：\n- " +
                    string.Join("\n- ", errors));
            }
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    loadedRoot);
            }
        }
    }

    private static void RequireSortingOrder(
        GameObject prefab,
        string path,
        int expected)
    {
        Transform node =
            prefab.transform.Find(path);

        if (node == null)
        {
            throw new InvalidOperationException(
                "缺少节点：" + path);
        }

        SpriteRenderer renderer =
            node.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            throw new InvalidOperationException(
                "缺少 SpriteRenderer：" +
                path);
        }

        if (renderer.sortingOrder != expected)
        {
            throw new InvalidOperationException(
                path +
                " SortingOrder 应为 " +
                expected +
                "，实际=" +
                renderer.sortingOrder);
        }
    }

    private static void EnsureRuntimeFolder()
    {
        if (!AssetDatabase.IsValidFolder(RoomFolder))
        {
            throw new InvalidOperationException(
                "找不到 ArtRoom 房间目录：" +
                RoomFolder);
        }

        if (!AssetDatabase.IsValidFolder(
                RuntimeFolder))
        {
            throw new InvalidOperationException(
                "找不到 Runtime 目录：" +
                RuntimeFolder);
        }
    }

    private static Transform CreateEmptyChild(
        Transform parent,
        string name)
    {
        GameObject child =
            new GameObject(name);

        child.transform.SetParent(
            parent,
            false);

        child.transform.localPosition =
            Vector3.zero;

        child.transform.localRotation =
            Quaternion.identity;

        child.transform.localScale =
            Vector3.one;

        return child.transform;
    }

    private static void CreateRuntimeSprite(
        string name,
        Transform parent,
        Sprite sprite,
        int sortingOrder)
    {
        GameObject child =
            new GameObject(name);

        child.transform.SetParent(
            parent,
            false);

        child.transform.localPosition =
            Vector3.zero;

        child.transform.localRotation =
            Quaternion.identity;

        child.transform.localScale =
            Vector3.one;

        SpriteRenderer renderer =
            child.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder =
            sortingOrder;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" +
                propertyName);
        }

        return property;
    }

    private static string ToAbsolutePath(
        string assetPath)
    {
        string projectRoot =
            Directory.GetParent(
                Application.dataPath)
                .FullName;

        return Path.Combine(
            projectRoot,
            assetPath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }

    private readonly struct DoorGap
    {
        public readonly float Min;
        public readonly float Max;

        public DoorGap(
            float min,
            float max)
        {
            Min = min;
            Max = max;
        }
    }
}
