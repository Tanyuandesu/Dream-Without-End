using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R3：自动生成可长期保留的灰盒房间骨架、Catalog 与预览 Scene。
///
/// 本工具只写入 Assets/DreamDungeon/Generated/R3_Graybox，
/// 不修改当前 Scene、DungeonGenerator、DungeonRenderer 或现有测试 Prefab。
/// </summary>
public static class DreamRoomGrayboxLibraryGenerator
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/";

    private const string GeneratedRoot =
        "Assets/DreamDungeon/Generated/R3_Graybox";

    private const string RoomsFolder =
        GeneratedRoot + "/Rooms";

    private const string CatalogFolder =
        GeneratedRoot + "/Catalog";

    private const string GalleryFolder =
        GeneratedRoot + "/Gallery";

    private const string SharedFolder =
        GeneratedRoot + "/Shared";

    private const string WhiteSpritePath =
        SharedFolder + "/GrayboxWhite.png";

    private const string CatalogPath =
        CatalogFolder + "/RoomCatalog_Graybox.asset";

    private const string GalleryScenePath =
        GalleryFolder + "/GrayboxRoomGallery_R3.unity";

    private const float WallThickness = 0.35f;
    private const int DoorWidthInCells = 2;
    private const float GalleryGap = 3f;

    private static readonly RoomDefinition[] Definitions =
    {
        new RoomDefinition(
            "Room_08x06",
            "Graybox_08x06",
            new Vector2Int(8, 6),
            10,
            new Color(0.26f, 0.48f, 0.66f)),

        new RoomDefinition(
            "Room_13x09",
            "Graybox_13x09",
            new Vector2Int(13, 9),
            8,
            new Color(0.48f, 0.38f, 0.66f)),

        new RoomDefinition(
            "Room_18x07",
            "Graybox_18x07",
            new Vector2Int(18, 7),
            6,
            new Color(0.24f, 0.58f, 0.52f)),

        new RoomDefinition(
            "Room_09x16",
            "Graybox_09x16",
            new Vector2Int(9, 16),
            6,
            new Color(0.62f, 0.47f, 0.25f))
    };

    [MenuItem(
        MenuRoot + "Generate Graybox Room Library (R3)",
        false,
        2100)]
    private static void GenerateGrayboxRoomLibrary()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "Return to a normal Scene before generating the " +
                "R3 graybox library.",
                "OK");
            return;
        }

        if (IsGallerySceneLoaded())
        {
            EditorUtility.DisplayDialog(
                "Close the R3 Gallery first",
                "The generated Gallery Scene is currently open. " +
                "Open your normal development Scene, then run " +
                "the generator again.",
                "OK");
            return;
        }

        if (KnownGeneratedAssetsExist() &&
            !EditorUtility.DisplayDialog(
                "Regenerate R3 Graybox Library?",
                "The known R3-generated assets under:\n\n" +
                GeneratedRoot +
                "\n\nwill be regenerated in place. Existing asset " +
                "GUIDs are preserved. Unrelated assets and test " +
                "Prefabs will not be changed.",
                "Regenerate",
                "Cancel"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R3",
                "Preparing generated folders...",
                0.05f);

            EnsureGeneratedFolders();

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R3",
                "Creating shared graybox Sprite...",
                0.12f);

            Sprite whiteSprite = CreateWhiteSpriteAsset();

            List<DreamRoomTemplate> templates =
                new List<DreamRoomTemplate>();

            for (int i = 0; i < Definitions.Length; i++)
            {
                float progress =
                    0.2f + 0.45f *
                    ((float)i / Definitions.Length);

                EditorUtility.DisplayProgressBar(
                    "Dream Dungeon R3",
                    "Creating " +
                    Definitions[i].PrefabName + "...",
                    progress);

                templates.Add(
                    CreateRoomPrefab(
                        Definitions[i],
                        whiteSprite));
            }

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R3",
                "Creating Room Catalog...",
                0.7f);

            DreamRoomCatalog catalog =
                CreateCatalog(templates);

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R3",
                "Creating gallery Scene...",
                0.8f);

            CreateGalleryScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R3",
                "Validating generated library...",
                0.92f);

            string summary;
            List<string> errors =
                ValidateGeneratedAssets(out summary);

            if (errors.Count > 0)
            {
                LogValidationFailure(errors);

                EditorUtility.DisplayDialog(
                    "R3 generation finished with errors",
                    "Assets were generated, but validation failed. " +
                    "Open Console and inspect the first error.",
                    "OK");

                return;
            }

            Debug.Log(summary);
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);

            EditorUtility.DisplayDialog(
                "R3 Graybox Library Ready",
                "Generated and validated:\n" +
                "- 4 room Prefabs\n" +
                "- 1 Room Catalog\n" +
                "- 1 gallery Scene\n" +
                "- 1 shared Sprite\n\n" +
                "No live dungeon scripts or Scenes were modified.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "R3 generation failed",
                "Generation stopped. Open Console and inspect " +
                "the first exception.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(
        MenuRoot + "Validate Generated Graybox Library (R3)",
        false,
        2101)]
    private static void ValidateGeneratedGrayboxLibrary()
    {
        string summary;
        List<string> errors =
            ValidateGeneratedAssets(out summary);

        if (errors.Count > 0)
        {
            LogValidationFailure(errors);
            return;
        }

        Debug.Log(summary);
    }

    [MenuItem(
        MenuRoot + "Open Graybox Room Gallery (R3)",
        false,
        2102)]
    private static void OpenGrayboxRoomGallery()
    {
        SceneAsset gallery =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GalleryScenePath);

        if (gallery == null)
        {
            Debug.LogError(
                "[DreamRoomGrayboxLibraryGenerator] " +
                "Gallery Scene does not exist. Generate the R3 " +
                "library first.");
            return;
        }

        if (!EditorSceneManager
                .SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(
            GalleryScenePath,
            OpenSceneMode.Single);
    }

    private static DreamRoomTemplate CreateRoomPrefab(
        RoomDefinition definition,
        Sprite whiteSprite)
    {
        string prefabPath = GetPrefabPath(definition);

        Scene previewScene =
            EditorSceneManager.NewPreviewScene();

        GameObject root = null;

        try
        {
            root = new GameObject(definition.PrefabName);

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

            Transform socketsRoot =
                CreateEmptyChild(root.transform, "Sockets");

            Transform navigationRoot =
                CreateEmptyChild(root.transform, "Navigation");

            Transform spawnPointsRoot =
                CreateEmptyChild(root.transform, "SpawnPoints");

            CreateSpriteObject(
                "Floor",
                visualRoot,
                whiteSprite,
                definition.FloorColor,
                Vector3.zero,
                new Vector3(
                    definition.Size.x,
                    definition.Size.y,
                    1f),
                sortingOrder: -10,
                addCollider: false);

            Transform wallsRoot =
                CreateEmptyChild(visualRoot, "Walls");

            Transform blockersRoot =
                CreateEmptyChild(visualRoot, "DoorBlockers");

            Color wallColor =
                Color.Lerp(
                    definition.FloorColor,
                    Color.black,
                    0.62f);

            Dictionary<DreamRoomDoorDirection, GameObject>
                blockers = BuildWallsAndBlockers(
                    definition,
                    wallsRoot,
                    blockersRoot,
                    whiteSprite,
                    wallColor);

            CreateDoorSocket(
                definition.Size,
                DreamRoomDoorDirection.North,
                socketsRoot,
                blockers[DreamRoomDoorDirection.North]);

            CreateDoorSocket(
                definition.Size,
                DreamRoomDoorDirection.East,
                socketsRoot,
                blockers[DreamRoomDoorDirection.East]);

            CreateDoorSocket(
                definition.Size,
                DreamRoomDoorDirection.South,
                socketsRoot,
                blockers[DreamRoomDoorDirection.South]);

            CreateDoorSocket(
                definition.Size,
                DreamRoomDoorDirection.West,
                socketsRoot,
                blockers[DreamRoomDoorDirection.West]);

            ConfigureTemplate(
                template,
                definition,
                visualRoot,
                socketsRoot,
                navigationRoot,
                spawnPointsRoot);

            template.RefreshDoorSockets();
            template.RefreshSpawnPoints();

            List<string> templateErrors =
                template.GetValidationErrors();

            if (templateErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Generated room '" +
                    definition.PrefabName +
                    "' failed before-save validation:\n" +
                    string.Join("\n", templateErrors));
            }

            bool success;
            GameObject savedPrefab =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath,
                    out success);

            if (!success || savedPrefab == null)
            {
                throw new InvalidOperationException(
                    "Could not save Prefab: " + prefabPath);
            }

            DreamRoomTemplate savedTemplate =
                savedPrefab.GetComponent<DreamRoomTemplate>();

            if (savedTemplate == null)
            {
                throw new InvalidOperationException(
                    "Saved Prefab lost DreamRoomTemplate: " +
                    prefabPath);
            }

            return savedTemplate;
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
    }

    private static Dictionary<
        DreamRoomDoorDirection,
        GameObject> BuildWallsAndBlockers(
            RoomDefinition definition,
            Transform wallsRoot,
            Transform blockersRoot,
            Sprite whiteSprite,
            Color wallColor)
    {
        Dictionary<DreamRoomDoorDirection, GameObject>
            blockers =
                new Dictionary<
                    DreamRoomDoorDirection,
                    GameObject>();

        float halfWidth = definition.Size.x * 0.5f;
        float halfHeight = definition.Size.y * 0.5f;

        float horizontalDoorCenter =
            GetDoorCenterLocal(
                definition.Size,
                DreamRoomDoorDirection.North).x;

        float verticalDoorCenter =
            GetDoorCenterLocal(
                definition.Size,
                DreamRoomDoorDirection.East).y;

        blockers.Add(
            DreamRoomDoorDirection.North,
            CreateHorizontalWallSide(
                DreamRoomDoorDirection.North,
                halfHeight,
                definition.Size.x,
                horizontalDoorCenter,
                wallsRoot,
                blockersRoot,
                whiteSprite,
                wallColor));

        blockers.Add(
            DreamRoomDoorDirection.South,
            CreateHorizontalWallSide(
                DreamRoomDoorDirection.South,
                -halfHeight,
                definition.Size.x,
                horizontalDoorCenter,
                wallsRoot,
                blockersRoot,
                whiteSprite,
                wallColor));

        blockers.Add(
            DreamRoomDoorDirection.East,
            CreateVerticalWallSide(
                DreamRoomDoorDirection.East,
                halfWidth,
                definition.Size.y,
                verticalDoorCenter,
                wallsRoot,
                blockersRoot,
                whiteSprite,
                wallColor));

        blockers.Add(
            DreamRoomDoorDirection.West,
            CreateVerticalWallSide(
                DreamRoomDoorDirection.West,
                -halfWidth,
                definition.Size.y,
                verticalDoorCenter,
                wallsRoot,
                blockersRoot,
                whiteSprite,
                wallColor));

        return blockers;
    }

    private static GameObject CreateHorizontalWallSide(
        DreamRoomDoorDirection direction,
        float y,
        float totalLength,
        float doorCenter,
        Transform wallsRoot,
        Transform blockersRoot,
        Sprite whiteSprite,
        Color wallColor)
    {
        string sideName = direction.ToString();
        float minimum = -totalLength * 0.5f;
        float maximum = totalLength * 0.5f;
        float openingMinimum =
            doorCenter - DoorWidthInCells * 0.5f;
        float openingMaximum =
            doorCenter + DoorWidthInCells * 0.5f;

        float firstLength = openingMinimum - minimum;
        float secondLength = maximum - openingMaximum;

        CreateWallSegment(
            "Wall_" + sideName + "_Left",
            wallsRoot,
            whiteSprite,
            wallColor,
            new Vector3(
                minimum + firstLength * 0.5f,
                y,
                0f),
            new Vector3(
                firstLength,
                WallThickness,
                1f));

        CreateWallSegment(
            "Wall_" + sideName + "_Right",
            wallsRoot,
            whiteSprite,
            wallColor,
            new Vector3(
                openingMaximum + secondLength * 0.5f,
                y,
                0f),
            new Vector3(
                secondLength,
                WallThickness,
                1f));

        return CreateSpriteObject(
            "Blocker_" + sideName + "_0",
            blockersRoot,
            whiteSprite,
            Color.Lerp(
                wallColor,
                GetDirectionColor(direction),
                0.38f),
            new Vector3(doorCenter, y, 0f),
            new Vector3(
                DoorWidthInCells,
                WallThickness,
                1f),
            sortingOrder: 0,
            addCollider: true);
    }

    private static GameObject CreateVerticalWallSide(
        DreamRoomDoorDirection direction,
        float x,
        float totalLength,
        float doorCenter,
        Transform wallsRoot,
        Transform blockersRoot,
        Sprite whiteSprite,
        Color wallColor)
    {
        string sideName = direction.ToString();
        float minimum = -totalLength * 0.5f;
        float maximum = totalLength * 0.5f;
        float openingMinimum =
            doorCenter - DoorWidthInCells * 0.5f;
        float openingMaximum =
            doorCenter + DoorWidthInCells * 0.5f;

        float firstLength = openingMinimum - minimum;
        float secondLength = maximum - openingMaximum;

        CreateWallSegment(
            "Wall_" + sideName + "_Bottom",
            wallsRoot,
            whiteSprite,
            wallColor,
            new Vector3(
                x,
                minimum + firstLength * 0.5f,
                0f),
            new Vector3(
                WallThickness,
                firstLength,
                1f));

        CreateWallSegment(
            "Wall_" + sideName + "_Top",
            wallsRoot,
            whiteSprite,
            wallColor,
            new Vector3(
                x,
                openingMaximum + secondLength * 0.5f,
                0f),
            new Vector3(
                WallThickness,
                secondLength,
                1f));

        return CreateSpriteObject(
            "Blocker_" + sideName + "_0",
            blockersRoot,
            whiteSprite,
            Color.Lerp(
                wallColor,
                GetDirectionColor(direction),
                0.38f),
            new Vector3(x, doorCenter, 0f),
            new Vector3(
                WallThickness,
                DoorWidthInCells,
                1f),
            sortingOrder: 0,
            addCollider: true);
    }

    private static void CreateWallSegment(
        string objectName,
        Transform parent,
        Sprite whiteSprite,
        Color color,
        Vector3 localPosition,
        Vector3 localScale)
    {
        if (localScale.x <= 0.001f ||
            localScale.y <= 0.001f)
        {
            throw new InvalidOperationException(
                "Wall segment '" + objectName +
                "' has a non-positive size.");
        }

        CreateSpriteObject(
            objectName,
            parent,
            whiteSprite,
            color,
            localPosition,
            localScale,
            sortingOrder: 0,
            addCollider: true);
    }

    private static DreamRoomDoorSocket CreateDoorSocket(
        Vector2Int roomSize,
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
            GetDoorCenterLocal(roomSize, direction);

        socketObject.transform.localRotation =
            Quaternion.identity;

        socketObject.transform.localScale = Vector3.one;

        DreamRoomDoorSocket socket =
            socketObject.AddComponent<
                DreamRoomDoorSocket>();

        socket.Configure(
            socketId,
            direction,
            GetDoorInsideCell(roomSize, direction),
            DoorWidthInCells,
            blocker);

        return socket;
    }

    private static Vector2Int GetDoorInsideCell(
        Vector2Int roomSize,
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Vector2Int(
                    roomSize.x / 2,
                    roomSize.y - 1);

            case DreamRoomDoorDirection.East:
                return new Vector2Int(
                    roomSize.x - 1,
                    roomSize.y / 2);

            case DreamRoomDoorDirection.South:
                return new Vector2Int(
                    roomSize.x / 2,
                    0);

            case DreamRoomDoorDirection.West:
                return new Vector2Int(
                    0,
                    roomSize.y / 2);

            default:
                return Vector2Int.zero;
        }
    }

    private static Vector3 GetDoorCenterLocal(
        Vector2Int roomSize,
        DreamRoomDoorDirection direction)
    {
        Vector2Int baseCell =
            GetDoorInsideCell(roomSize, direction);

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

        Vector2 average = total / DoorWidthInCells;

        return new Vector3(
            average.x - (roomSize.x - 1) * 0.5f,
            average.y - (roomSize.y - 1) * 0.5f,
            0f);
    }

    private static void ConfigureTemplate(
        DreamRoomTemplate template,
        RoomDefinition definition,
        Transform visualRoot,
        Transform socketsRoot,
        Transform navigationRoot,
        Transform spawnPointsRoot)
    {
        SerializedObject serialized =
            new SerializedObject(template);

        RequireProperty(serialized, "templateId")
            .stringValue = definition.TemplateId;

        RequireProperty(serialized, "sizeInCells")
            .vector2IntValue = definition.Size;

        RequireProperty(serialized, "randomWeight")
            .intValue = definition.RandomWeight;

        RequireProperty(serialized, "minimumFloor")
            .intValue = 1;

        RequireProperty(serialized, "maximumFloor")
            .intValue = 0;

        RequireProperty(
            serialized,
            "maximumInstancesPerFloor").intValue = 0;

        RequireProperty(serialized, "allowQuarterTurns")
            .boolValue = true;

        RequireProperty(serialized, "roomTags")
            .intValue = (int)DreamRoomTag.Standard;

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

    private static DreamRoomCatalog CreateCatalog(
        List<DreamRoomTemplate> templates)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomCatalog>(CatalogPath);

        bool isNewAsset = catalog == null;

        if (isNewAsset)
        {
            catalog = ScriptableObject.CreateInstance<
                DreamRoomCatalog>();
        }

        catalog.name = "RoomCatalog_Graybox";

        SerializedObject serialized =
            new SerializedObject(catalog);

        RequireProperty(serialized, "catalogId")
            .stringValue = "Graybox_R3";

        SerializedProperty templateList =
            RequireProperty(serialized, "roomTemplates");

        templateList.arraySize = templates.Count;

        for (int i = 0; i < templates.Count; i++)
        {
            templateList.GetArrayElementAtIndex(i)
                .objectReferenceValue = templates[i];
        }

        RequireProperty(serialized, "previewFloorNumber")
            .intValue = 1;

        RequireProperty(serialized, "previewUsedTemplateId")
            .stringValue = string.Empty;

        RequireProperty(serialized, "previewExistingInstances")
            .intValue = 0;

        RequireProperty(serialized, "previewRollCount")
            .intValue = 1000;

        RequireProperty(serialized, "previewRandomSeed")
            .intValue = 12345;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (isNewAsset)
        {
            AssetDatabase.CreateAsset(
                catalog,
                CatalogPath);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        return catalog;
    }

    private static void CreateGalleryScene()
    {
        Scene previousActiveScene =
            SceneManager.GetActiveScene();

        Scene galleryScene =
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);

        try
        {
            SceneManager.SetActiveScene(galleryScene);

            GameObject galleryRoot =
                new GameObject("GrayboxRoomGallery_R3");

            float cursorX = 0f;
            float maximumHeight = 0f;

            for (int i = 0; i < Definitions.Length; i++)
            {
                RoomDefinition definition =
                    Definitions[i];

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        GetPrefabPath(definition));

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Gallery could not load Prefab: " +
                        GetPrefabPath(definition));
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        galleryScene) as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Gallery could not instantiate: " +
                        definition.PrefabName);
                }

                instance.name = definition.PrefabName;
                instance.transform.position =
                    new Vector3(
                        cursorX + definition.Size.x * 0.5f,
                        0f,
                        0f);

                instance.transform.rotation =
                    Quaternion.identity;

                instance.transform.localScale = Vector3.one;
                instance.transform.SetParent(
                    galleryRoot.transform,
                    worldPositionStays: true);

                cursorX +=
                    definition.Size.x + GalleryGap;

                maximumHeight = Mathf.Max(
                    maximumHeight,
                    definition.Size.y);
            }

            float totalWidth =
                cursorX - GalleryGap;

            GameObject cameraObject =
                new GameObject("GalleryCamera");

            cameraObject.tag = "MainCamera";

            Camera galleryCamera =
                cameraObject.AddComponent<Camera>();

            galleryCamera.orthographic = true;
            galleryCamera.clearFlags =
                CameraClearFlags.SolidColor;

            galleryCamera.backgroundColor =
                new Color(0.035f, 0.05f, 0.09f);

            galleryCamera.orthographicSize =
                Mathf.Max(
                    maximumHeight * 0.65f,
                    totalWidth / (2f * (16f / 9f)) + 2f);

            cameraObject.transform.position =
                new Vector3(
                    totalWidth * 0.5f,
                    0f,
                    -10f);

            cameraObject.transform.SetParent(
                galleryRoot.transform,
                worldPositionStays: true);

            if (!EditorSceneManager.SaveScene(
                    galleryScene,
                    GalleryScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save Gallery Scene: " +
                    GalleryScenePath);
            }
        }
        finally
        {
            if (previousActiveScene.IsValid() &&
                previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(
                    previousActiveScene);
            }

            if (galleryScene.IsValid() &&
                galleryScene.isLoaded)
            {
                EditorSceneManager.CloseScene(
                    galleryScene,
                    removeScene: true);
            }
        }
    }

    private static Sprite CreateWhiteSpriteAsset()
    {
        Texture2D texture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);

        try
        {
            texture.name = "GrayboxWhite";
            texture.SetPixels(
                new[]
                {
                    Color.white,
                    Color.white,
                    Color.white,
                    Color.white
                });

            texture.Apply();

            byte[] pngBytes = texture.EncodeToPNG();

            File.WriteAllBytes(
                GetAbsoluteAssetPath(WhiteSpritePath),
                pngBytes);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(
            WhiteSpritePath,
            ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer =
            AssetImporter.GetAtPath(
                WhiteSpritePath) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Could not configure generated Sprite importer.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 2f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                WhiteSpritePath);

        if (sprite == null)
        {
            throw new InvalidOperationException(
                "Generated white Sprite could not be loaded.");
        }

        return sprite;
    }

    private static GameObject CreateSpriteObject(
        string objectName,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector3 localPosition,
        Vector3 localScale,
        int sortingOrder,
        bool addCollider)
    {
        GameObject createdObject =
            new GameObject(objectName);

        createdObject.transform.SetParent(
            parent,
            worldPositionStays: false);

        createdObject.transform.localPosition =
            localPosition;

        createdObject.transform.localRotation =
            Quaternion.identity;

        createdObject.transform.localScale =
            localScale;

        SpriteRenderer spriteRenderer =
            createdObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            BoxCollider2D collider =
                createdObject.AddComponent<
                    BoxCollider2D>();

            collider.size = Vector2.one;
        }

        return createdObject;
    }

    private static Transform CreateEmptyChild(
        Transform parent,
        string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(
            parent,
            worldPositionStays: false);

        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.transform;
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
                "Required serialized field '" +
                propertyName +
                "' was not found. Confirm that R1 is installed.");
        }

        return property;
    }

    private static List<string> ValidateGeneratedAssets(
        out string summary)
    {
        List<string> errors = new List<string>();
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomGrayboxLibraryGenerator] R3 校验通过");

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                WhiteSpritePath);

        if (sprite == null)
        {
            errors.Add(
                "Shared graybox Sprite is missing: " +
                WhiteSpritePath);
        }

        HashSet<DreamRoomTemplate> expectedTemplates =
            new HashSet<DreamRoomTemplate>();

        for (int i = 0; i < Definitions.Length; i++)
        {
            RoomDefinition definition =
                Definitions[i];

            string prefabPath =
                GetPrefabPath(definition);

            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefabAsset == null)
            {
                errors.Add(
                    "Generated Prefab is missing: " +
                    prefabPath);
                continue;
            }

            DreamRoomTemplate assetTemplate =
                prefabAsset.GetComponent<
                    DreamRoomTemplate>();

            if (assetTemplate == null)
            {
                errors.Add(
                    definition.PrefabName +
                    " has no DreamRoomTemplate.");
                continue;
            }

            expectedTemplates.Add(assetTemplate);

            if (PrefabUtility.GetPrefabAssetType(
                    prefabAsset) !=
                PrefabAssetType.Regular)
            {
                errors.Add(
                    definition.PrefabName +
                    " must be an independent regular Prefab, " +
                    "not a Variant.");
            }

            GameObject loadedRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath);

            try
            {
                ValidateLoadedRoom(
                    loadedRoot,
                    definition,
                    errors,
                    report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    loadedRoot);
            }
        }

        ValidateCatalog(
            expectedTemplates,
            errors,
            report);

        SceneAsset gallery =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GalleryScenePath);

        if (gallery == null)
        {
            errors.Add(
                "Gallery Scene is missing: " +
                GalleryScenePath);
        }
        else
        {
            report.AppendLine(
                "- Gallery Scene: " +
                GalleryScenePath);
        }

        report.AppendLine(
            "- Existing generator, renderer and live Scenes: unchanged");

        summary = report.ToString();
        return errors;
    }

    private static void ValidateLoadedRoom(
        GameObject loadedRoot,
        RoomDefinition definition,
        List<string> errors,
        StringBuilder report)
    {
        DreamRoomTemplate template =
            loadedRoot.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            errors.Add(
                definition.PrefabName +
                " lost DreamRoomTemplate after full load.");
            return;
        }

        if (loadedRoot.transform.localPosition !=
                Vector3.zero ||
            loadedRoot.transform.localRotation !=
                Quaternion.identity ||
            loadedRoot.transform.localScale !=
                Vector3.one)
        {
            errors.Add(
                definition.PrefabName +
                " root Transform must be reset with Scale (1,1,1).");
        }

        if (!string.Equals(
                template.TemplateId,
                definition.TemplateId,
                StringComparison.Ordinal))
        {
            errors.Add(
                definition.PrefabName +
                " has the wrong Template Id.");
        }

        if (template.SizeInCells != definition.Size)
        {
            errors.Add(
                definition.PrefabName +
                " has the wrong Size In Cells.");
        }

        if (template.RandomWeight !=
            definition.RandomWeight)
        {
            errors.Add(
                definition.PrefabName +
                " has the wrong Random Weight.");
        }

        if (!template.AllowQuarterTurns)
        {
            errors.Add(
                definition.PrefabName +
                " must allow Quarter Turns.");
        }

        if (!template.HasTag(DreamRoomTag.Standard))
        {
            errors.Add(
                definition.PrefabName +
                " must have the Standard room tag.");
        }

        if (template.OccupiedCellOverrides.Count != 0 ||
            template.WalkableCellOverrides.Count != 0 ||
            template.BlockedCellOverrides.Count != 0)
        {
            errors.Add(
                definition.PrefabName +
                " must use the empty-list rectangular defaults.");
        }

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.VisualRoot,
            "Visual",
            definition.PrefabName,
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.SocketsRoot,
            "Sockets",
            definition.PrefabName,
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.NavigationRoot,
            "Navigation",
            definition.PrefabName,
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.SpawnPointsRoot,
            "SpawnPoints",
            definition.PrefabName,
            errors);

        GameObject floor =
            template.VisualRoot == null
                ? null
                : FindDirectChild(
                    template.VisualRoot,
                    "Floor");

        if (floor == null ||
            floor.GetComponent<SpriteRenderer>() == null)
        {
            errors.Add(
                definition.PrefabName +
                " must contain Visual/Floor with a SpriteRenderer.");
        }
        else if (floor.GetComponent<BoxCollider2D>() != null)
        {
            errors.Add(
                definition.PrefabName +
                " Floor must not have a BoxCollider2D.");
        }

        if (template.DoorSockets.Count != 4)
        {
            errors.Add(
                definition.PrefabName +
                " must contain exactly 4 Door Sockets.");
        }

        HashSet<DreamRoomDoorDirection> directions =
            new HashSet<DreamRoomDoorDirection>();

        for (int i = 0;
             i < template.DoorSockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[i];

            if (socket == null)
            {
                errors.Add(
                    definition.PrefabName +
                    " contains a null Door Socket.");
                continue;
            }

            directions.Add(socket.Direction);

            if (socket.DoorWidthInCells !=
                DoorWidthInCells)
            {
                errors.Add(
                    definition.PrefabName +
                    "/" + socket.SocketId +
                    " must be two cells wide.");
            }

            if (socket.ClosedBlocker == null)
            {
                errors.Add(
                    definition.PrefabName +
                    "/" + socket.SocketId +
                    " has no Closed Blocker.");
            }
            else
            {
                if (!socket.ClosedBlocker.activeSelf)
                {
                    errors.Add(
                        definition.PrefabName +
                        "/" + socket.SocketId +
                        " blocker must be active by default.");
                }

                if (socket.ClosedBlocker
                        .GetComponent<BoxCollider2D>() == null)
                {
                    errors.Add(
                        definition.PrefabName +
                        "/" + socket.SocketId +
                        " blocker needs BoxCollider2D.");
                }
            }
        }

        if (directions.Count != 4 ||
            !directions.Contains(
                DreamRoomDoorDirection.North) ||
            !directions.Contains(
                DreamRoomDoorDirection.East) ||
            !directions.Contains(
                DreamRoomDoorDirection.South) ||
            !directions.Contains(
                DreamRoomDoorDirection.West))
        {
            errors.Add(
                definition.PrefabName +
                " must have one Socket on every direction.");
        }

        int colliderCount =
            template.VisualRoot == null
                ? 0
                : template.VisualRoot
                    .GetComponentsInChildren<
                        BoxCollider2D>(true).Length;

        if (colliderCount != 12)
        {
            errors.Add(
                definition.PrefabName +
                " should contain 12 wall/blocker colliders, " +
                "but found " + colliderCount + ".");
        }

        List<string> templateErrors =
            template.GetValidationErrors();

        for (int i = 0; i < templateErrors.Count; i++)
        {
            errors.Add(
                definition.PrefabName + ": " +
                templateErrors[i]);
        }

        if (templateErrors.Count == 0)
        {
            report.AppendLine(
                "- " + definition.PrefabName +
                ": " + definition.Size.x +
                "x" + definition.Size.y +
                " | Doors 4 | Colliders " +
                colliderCount);
        }
    }

    private static void ValidateCatalog(
        HashSet<DreamRoomTemplate> expectedTemplates,
        List<string> errors,
        StringBuilder report)
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                DreamRoomCatalog>(CatalogPath);

        if (catalog == null)
        {
            errors.Add(
                "Generated Catalog is missing: " +
                CatalogPath);
            return;
        }

        if (catalog.Count != Definitions.Length)
        {
            errors.Add(
                "Generated Catalog should contain " +
                Definitions.Length +
                " templates, but found " +
                catalog.Count + ".");
        }

        HashSet<DreamRoomTemplate> catalogTemplates =
            new HashSet<DreamRoomTemplate>();

        for (int i = 0;
             i < catalog.RoomTemplates.Count;
             i++)
        {
            DreamRoomTemplate template =
                catalog.RoomTemplates[i];

            if (template != null)
            {
                catalogTemplates.Add(template);
            }
        }

        if (!catalogTemplates.SetEquals(
                expectedTemplates))
        {
            errors.Add(
                "Generated Catalog references do not match " +
                "the four generated Prefabs.");
        }

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                "Catalog: " + catalogErrors[i]);
        }

        if (catalogErrors.Count == 0)
        {
            report.AppendLine(
                "- Catalog Graybox_R3: Templates " +
                catalog.Count);
        }
    }

    private static void ValidateExpectedRoot(
        Transform roomRoot,
        Transform referencedRoot,
        string expectedName,
        string prefabName,
        List<string> errors)
    {
        if (referencedRoot == null)
        {
            errors.Add(
                prefabName +
                " has no " + expectedName +
                " Root reference.");
            return;
        }

        if (referencedRoot.parent != roomRoot ||
            referencedRoot.name != expectedName)
        {
            errors.Add(
                prefabName +
                " has an invalid " + expectedName +
                " Root hierarchy.");
        }

        if (referencedRoot.localPosition != Vector3.zero ||
            referencedRoot.localRotation !=
                Quaternion.identity ||
            referencedRoot.localScale != Vector3.one)
        {
            errors.Add(
                prefabName + "/" + expectedName +
                " Transform must be reset.");
        }
    }

    private static GameObject FindDirectChild(
        Transform parent,
        string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void LogValidationFailure(
        List<string> errors)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(
            "[DreamRoomGrayboxLibraryGenerator] R3 校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        Debug.LogError(report.ToString());
    }

    private static void EnsureGeneratedFolders()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(RoomsFolder);
        EnsureFolder(CatalogFolder);
        EnsureFolder(GalleryFolder);
        EnsureFolder(SharedFolder);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');

        if (segments.Length == 0 ||
            segments[0] != "Assets")
        {
            throw new ArgumentException(
                "Generated folder must be under Assets.",
                nameof(folderPath));
        }

        string currentPath = "Assets";

        for (int i = 1; i < segments.Length; i++)
        {
            string nextPath =
                currentPath + "/" + segments[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                string guid = AssetDatabase.CreateFolder(
                    currentPath,
                    segments[i]);

                if (string.IsNullOrWhiteSpace(guid))
                {
                    throw new InvalidOperationException(
                        "Could not create folder: " +
                        nextPath);
                }
            }

            currentPath = nextPath;
        }
    }

    private static bool KnownGeneratedAssetsExist()
    {
        if (AssetDatabase.LoadMainAssetAtPath(
                CatalogPath) != null ||
            AssetDatabase.LoadMainAssetAtPath(
                GalleryScenePath) != null ||
            AssetDatabase.LoadMainAssetAtPath(
                WhiteSpritePath) != null)
        {
            return true;
        }

        for (int i = 0; i < Definitions.Length; i++)
        {
            if (AssetDatabase.LoadMainAssetAtPath(
                    GetPrefabPath(Definitions[i])) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGallerySceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.IsValid() &&
                string.Equals(
                    scene.path,
                    GalleryScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPrefabPath(
        RoomDefinition definition)
    {
        return RoomsFolder + "/" +
               definition.PrefabName + ".prefab";
    }

    private static string GetAbsoluteAssetPath(
        string assetPath)
    {
        const string assetsPrefix = "Assets/";

        if (!assetPath.StartsWith(
                assetsPrefix,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Asset path must begin with 'Assets/'.",
                nameof(assetPath));
        }

        string relativePath =
            assetPath.Substring(assetsPrefix.Length);

        return Path.Combine(
            Application.dataPath,
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }

    private static Color GetDirectionColor(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Color(0.25f, 0.9f, 1f);

            case DreamRoomDoorDirection.East:
                return new Color(1f, 0.8f, 0.2f);

            case DreamRoomDoorDirection.South:
                return new Color(1f, 0.35f, 0.8f);

            case DreamRoomDoorDirection.West:
                return new Color(0.35f, 1f, 0.45f);

            default:
                return Color.white;
        }
    }

    private sealed class RoomDefinition
    {
        public string PrefabName { get; }
        public string TemplateId { get; }
        public Vector2Int Size { get; }
        public int RandomWeight { get; }
        public Color FloorColor { get; }

        public RoomDefinition(
            string prefabName,
            string templateId,
            Vector2Int size,
            int randomWeight,
            Color floorColor)
        {
            PrefabName = prefabName;
            TemplateId = templateId;
            Size = size;
            RandomWeight = randomWeight;
            FloorColor = floorColor;
        }
    }
}
