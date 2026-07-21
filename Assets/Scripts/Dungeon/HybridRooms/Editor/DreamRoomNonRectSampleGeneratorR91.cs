using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.1：生成一个与 R3 灰盒库隔离的最小非矩形房间样本。
///
/// 本工具只写入 Assets/DreamDungeon/Generated/R9_1_NonRectSample，
/// 不修改 GameScene、RoomCatalog_Graybox、R4/R5/R6 或任何运行时脚本。
/// </summary>
public static class DreamRoomNonRectSampleGeneratorR91
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/";

    private const string GeneratedRoot =
        "Assets/DreamDungeon/Generated/R9_1_NonRectSample";

    private const string RoomsFolder =
        GeneratedRoot + "/Rooms";

    private const string CatalogFolder =
        GeneratedRoot + "/Catalog";

    private const string GalleryFolder =
        GeneratedRoot + "/Gallery";

    private const string PrefabPath =
        RoomsFolder + "/Room_L10x08_R91.prefab";

    private const string CatalogPath =
        CatalogFolder + "/RoomCatalog_R91_NonRectTest.asset";

    private const string GalleryScenePath =
        GalleryFolder + "/NonRectRoomGallery_R91.unity";

    private const string SharedSpritePath =
        "Assets/DreamDungeon/Generated/R3_Graybox/Shared/" +
        "GrayboxWhite.png";

    private const string PrefabName =
        "Room_L10x08_R91";

    private const string TemplateId =
        "R91_LShape_10x08";

    private const string CatalogId =
        "NonRect_R91_Test";

    private const int DoorWidthInCells = 2;
    private const float WallThickness = 0.35f;
    private const float GalleryGap = 4f;

    private static readonly Vector2Int RoomSize =
        new Vector2Int(10, 8);

    private static readonly Vector2Int BlockedCell =
        new Vector2Int(2, 1);

    private static readonly Color FloorColor =
        new Color(0.18f, 0.46f, 0.52f);

    private static readonly Color WallColor =
        new Color(0.07f, 0.16f, 0.20f);

    private static readonly Color ObstacleColor =
        new Color(0.72f, 0.22f, 0.28f);

    private static readonly SocketDefinition[] SocketDefinitions =
    {
        new SocketDefinition(
            "North_0",
            DreamRoomDoorDirection.North,
            new Vector2Int(2, 7)),

        new SocketDefinition(
            "East_0",
            DreamRoomDoorDirection.East,
            new Vector2Int(9, 2)),

        new SocketDefinition(
            "South_0",
            DreamRoomDoorDirection.South,
            new Vector2Int(5, 0)),

        new SocketDefinition(
            "West_0",
            DreamRoomDoorDirection.West,
            new Vector2Int(0, 6))
    };

    private static readonly SpawnDefinition[] SpawnDefinitions =
    {
        new SpawnDefinition(
            "Player_0",
            DreamRoomSpawnPointKind.Player,
            new Vector2Int(1, 1)),

        new SpawnDefinition(
            "Exit_0",
            DreamRoomSpawnPointKind.Exit,
            new Vector2Int(8, 1)),

        new SpawnDefinition(
            "Enemy_0",
            DreamRoomSpawnPointKind.Enemy,
            new Vector2Int(1, 6)),

        new SpawnDefinition(
            "Item_0",
            DreamRoomSpawnPointKind.Item,
            new Vector2Int(3, 4))
    };

    [MenuItem(
        MenuRoot + "Generate Minimal Non-Rect Sample (R9.1)",
        false,
        2200)]
    private static void GenerateSample()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "Return to a normal Scene before generating the " +
                "R9.1 non-rect sample.",
                "OK");
            return;
        }

        if (IsGallerySceneLoaded())
        {
            EditorUtility.DisplayDialog(
                "Close the R9.1 Gallery first",
                "The generated R9.1 Gallery Scene is currently open. " +
                "Open GameScene or another normal Scene first.",
                "OK");
            return;
        }

        if (KnownGeneratedAssetsExist() &&
            !EditorUtility.DisplayDialog(
                "Regenerate R9.1 Non-Rect Sample?",
                "The known R9.1 assets under:\n\n" +
                GeneratedRoot +
                "\n\nwill be regenerated in place. Existing asset " +
                "GUIDs are preserved. GameScene and the R3 Graybox " +
                "Catalog will not be changed.",
                "Regenerate",
                "Cancel"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R9.1",
                "Preparing isolated sample folders...",
                0.08f);

            EnsureGeneratedFolders();

            Sprite whiteSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    SharedSpritePath);

            if (whiteSprite == null)
            {
                throw new InvalidOperationException(
                    "R3 shared graybox Sprite is missing: " +
                    SharedSpritePath +
                    ". Restore the R9.0 baseline before R9.1.");
            }

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R9.1",
                "Creating the explicit L-shaped Prefab...",
                0.28f);

            DreamRoomTemplate template =
                CreateRoomPrefab(whiteSprite);

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R9.1",
                "Creating the isolated test Catalog...",
                0.62f);

            DreamRoomCatalog catalog =
                CreateCatalog(template);

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R9.1",
                "Creating the four-rotation Gallery...",
                0.78f);

            CreateGalleryScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayProgressBar(
                "Dream Dungeon R9.1",
                "Validating data, colliders and rotations...",
                0.92f);

            string summary;
            List<string> errors =
                ValidateGeneratedAssets(out summary);

            if (errors.Count > 0)
            {
                LogValidationFailure(errors);

                EditorUtility.DisplayDialog(
                    "R9.1 generation finished with errors",
                    "Assets were generated, but validation failed. " +
                    "Open Console and inspect the first error.",
                    "OK");
                return;
            }

            Debug.Log(summary);
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);

            EditorUtility.DisplayDialog(
                "R9.1 Non-Rect Sample Ready",
                "Generated and validated:\n" +
                "- 1 explicit 10x8 L-shaped room\n" +
                "- 50 Occupied / 49 Walkable / 1 Blocked\n" +
                "- 4 Door Sockets / 4 Spawn Points\n" +
                "- 1 isolated test Catalog\n" +
                "- 1 four-rotation Gallery\n\n" +
                "GameScene and RoomCatalog_Graybox are unchanged.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "R9.1 generation failed",
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
        MenuRoot + "Validate Minimal Non-Rect Sample (R9.1)",
        false,
        2201)]
    private static void ValidateSample()
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
        MenuRoot + "Open Non-Rect Rotation Gallery (R9.1)",
        false,
        2202)]
    private static void OpenGallery()
    {
        SceneAsset gallery =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GalleryScenePath);

        if (gallery == null)
        {
            Debug.LogError(
                "[DreamRoomNonRectSampleGeneratorR91] " +
                "Gallery Scene does not exist. Generate the R9.1 " +
                "sample first.");
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
        Sprite whiteSprite)
    {
        Scene previewScene =
            EditorSceneManager.NewPreviewScene();

        GameObject root = null;

        try
        {
            root = new GameObject(PrefabName);

            SceneManager.MoveGameObjectToScene(
                root,
                previewScene);

            ResetTransform(root.transform);

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

            Transform floorsRoot =
                CreateEmptyChild(visualRoot, "Floors");

            Transform wallsRoot =
                CreateEmptyChild(visualRoot, "Walls");

            Transform blockersRoot =
                CreateEmptyChild(visualRoot, "DoorBlockers");

            Transform obstaclesRoot =
                CreateEmptyChild(visualRoot, "Obstacles");

            CreateSpriteObject(
                "Floor_BottomArm",
                floorsRoot,
                whiteSprite,
                FloorColor,
                new Vector3(0f, -2.5f, 0f),
                new Vector3(10f, 3f, 1f),
                sortingOrder: -10,
                addCollider: false);

            CreateSpriteObject(
                "Floor_LeftArm",
                floorsRoot,
                whiteSprite,
                Color.Lerp(FloorColor, Color.white, 0.08f),
                new Vector3(-3f, 1.5f, 0f),
                new Vector3(4f, 5f, 1f),
                sortingOrder: -10,
                addCollider: false);

            List<Vector2Int> occupiedCells;
            List<Vector2Int> walkableCells;
            List<Vector2Int> blockedCells;

            BuildExpectedCells(
                out occupiedCells,
                out walkableCells,
                out blockedCells);

            HashSet<Vector2Int> occupiedSet =
                new HashSet<Vector2Int>(occupiedCells);

            HashSet<string> doorEdges =
                BuildDoorEdges();

            BuildPermanentBoundaryWalls(
                occupiedSet,
                doorEdges,
                wallsRoot,
                whiteSprite);

            for (int i = 0;
                 i < SocketDefinitions.Length;
                 i++)
            {
                CreateDoorSocketAndBlocker(
                    SocketDefinitions[i],
                    socketsRoot,
                    blockersRoot,
                    whiteSprite);
            }

            CreateSpriteObject(
                "Blocked_02_01",
                obstaclesRoot,
                whiteSprite,
                ObstacleColor,
                GetLocalCellCenter(BlockedCell),
                new Vector3(1f, 1f, 1f),
                sortingOrder: 1,
                addCollider: true);

            for (int i = 0;
                 i < SpawnDefinitions.Length;
                 i++)
            {
                CreateSpawnPoint(
                    SpawnDefinitions[i],
                    spawnPointsRoot);
            }

            ConfigureTemplate(
                template,
                occupiedCells,
                walkableCells,
                blockedCells,
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
                    "R9.1 room failed before-save validation:\n" +
                    string.Join("\n", templateErrors));
            }

            bool success;
            GameObject savedPrefab =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath,
                    out success);

            if (!success || savedPrefab == null)
            {
                throw new InvalidOperationException(
                    "Could not save Prefab: " + PrefabPath);
            }

            DreamRoomTemplate savedTemplate =
                savedPrefab.GetComponent<DreamRoomTemplate>();

            if (savedTemplate == null)
            {
                throw new InvalidOperationException(
                    "Saved R9.1 Prefab lost DreamRoomTemplate.");
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

    private static void BuildExpectedCells(
        out List<Vector2Int> occupiedCells,
        out List<Vector2Int> walkableCells,
        out List<Vector2Int> blockedCells)
    {
        occupiedCells = new List<Vector2Int>();

        for (int y = 0; y <= 2; y++)
        {
            for (int x = 0; x < RoomSize.x; x++)
            {
                occupiedCells.Add(new Vector2Int(x, y));
            }
        }

        for (int y = 3; y < RoomSize.y; y++)
        {
            for (int x = 0; x <= 3; x++)
            {
                occupiedCells.Add(new Vector2Int(x, y));
            }
        }

        blockedCells = new List<Vector2Int>
        {
            BlockedCell
        };

        walkableCells = new List<Vector2Int>();

        for (int i = 0; i < occupiedCells.Count; i++)
        {
            if (occupiedCells[i] != BlockedCell)
            {
                walkableCells.Add(occupiedCells[i]);
            }
        }
    }

    private static void BuildPermanentBoundaryWalls(
        HashSet<Vector2Int> occupiedCells,
        HashSet<string> doorEdges,
        Transform wallsRoot,
        Sprite sprite)
    {
        foreach (Vector2Int cell in occupiedCells)
        {
            for (int directionIndex = 0;
                 directionIndex < 4;
                 directionIndex++)
            {
                DreamRoomDoorDirection direction =
                    (DreamRoomDoorDirection)directionIndex;

                Vector2Int neighbour =
                    cell + direction.ToCellOffset();

                if (occupiedCells.Contains(neighbour))
                {
                    continue;
                }

                if (doorEdges.Contains(
                        BuildEdgeKey(cell, direction)))
                {
                    continue;
                }

                Vector3 wallPosition =
                    GetLocalCellCenter(cell) +
                    GetDirectionVector(direction) * 0.5f;

                Vector3 wallScale =
                    IsHorizontal(direction)
                        ? new Vector3(
                            1f,
                            WallThickness,
                            1f)
                        : new Vector3(
                            WallThickness,
                            1f,
                            1f);

                CreateSpriteObject(
                    "Wall_" + direction + "_" +
                    cell.x + "_" + cell.y,
                    wallsRoot,
                    sprite,
                    WallColor,
                    wallPosition,
                    wallScale,
                    sortingOrder: 0,
                    addCollider: true);
            }
        }
    }

    private static HashSet<string> BuildDoorEdges()
    {
        HashSet<string> doorEdges =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0;
             i < SocketDefinitions.Length;
             i++)
        {
            SocketDefinition definition =
                SocketDefinitions[i];

            List<Vector2Int> cells =
                GetDoorInsideCells(definition);

            for (int cellIndex = 0;
                 cellIndex < cells.Count;
                 cellIndex++)
            {
                doorEdges.Add(
                    BuildEdgeKey(
                        cells[cellIndex],
                        definition.Direction));
            }
        }

        return doorEdges;
    }

    private static void CreateDoorSocketAndBlocker(
        SocketDefinition definition,
        Transform socketsRoot,
        Transform blockersRoot,
        Sprite sprite)
    {
        Vector3 edgeCenter =
            GetDoorEdgeCenter(definition);

        Vector3 blockerScale =
            IsHorizontal(definition.Direction)
                ? new Vector3(
                    DoorWidthInCells,
                    WallThickness,
                    1f)
                : new Vector3(
                    WallThickness,
                    DoorWidthInCells,
                    1f);

        GameObject blocker =
            CreateSpriteObject(
                "Blocker_" + definition.SocketId,
                blockersRoot,
                sprite,
                Color.Lerp(
                    WallColor,
                    GetDirectionColor(definition.Direction),
                    0.42f),
                edgeCenter,
                blockerScale,
                sortingOrder: 0,
                addCollider: true);

        GameObject socketObject =
            new GameObject("Door_" + definition.SocketId);

        socketObject.transform.SetParent(
            socketsRoot,
            worldPositionStays: false);

        socketObject.transform.localPosition = edgeCenter;
        socketObject.transform.localRotation = Quaternion.identity;
        socketObject.transform.localScale = Vector3.one;

        DreamRoomDoorSocket socket =
            socketObject.AddComponent<DreamRoomDoorSocket>();

        socket.Configure(
            definition.SocketId,
            definition.Direction,
            definition.BaseInsideCell,
            DoorWidthInCells,
            blocker);
    }

    private static void CreateSpawnPoint(
        SpawnDefinition definition,
        Transform spawnPointsRoot)
    {
        GameObject pointObject =
            new GameObject(definition.SpawnPointId);

        pointObject.transform.SetParent(
            spawnPointsRoot,
            worldPositionStays: false);

        pointObject.transform.localPosition =
            GetLocalCellCenter(definition.LocalCell);

        pointObject.transform.localRotation =
            Quaternion.identity;

        pointObject.transform.localScale = Vector3.one;

        DreamRoomSpawnPoint point =
            pointObject.AddComponent<DreamRoomSpawnPoint>();

        point.Configure(
            definition.SpawnPointId,
            definition.Kind,
            definition.LocalCell,
            newRandomWeight: 1);
    }

    private static List<Vector2Int> GetDoorInsideCells(
        SocketDefinition definition)
    {
        List<Vector2Int> cells =
            new List<Vector2Int>(DoorWidthInCells);

        Vector2Int sideways =
            definition.Direction.PerpendicularCellOffset();

        int startOffset =
            -(DoorWidthInCells / 2);

        for (int i = 0; i < DoorWidthInCells; i++)
        {
            cells.Add(
                definition.BaseInsideCell +
                sideways * (startOffset + i));
        }

        return cells;
    }

    private static Vector3 GetDoorEdgeCenter(
        SocketDefinition definition)
    {
        List<Vector2Int> cells =
            GetDoorInsideCells(definition);

        Vector3 total = Vector3.zero;

        for (int i = 0; i < cells.Count; i++)
        {
            total += GetLocalCellCenter(cells[i]);
        }

        Vector3 average = total / cells.Count;

        return average +
               GetDirectionVector(definition.Direction) * 0.5f;
    }

    private static Vector3 GetLocalCellCenter(
        Vector2Int localCell)
    {
        return new Vector3(
            localCell.x - (RoomSize.x - 1) * 0.5f,
            localCell.y - (RoomSize.y - 1) * 0.5f,
            0f);
    }

    private static string BuildEdgeKey(
        Vector2Int cell,
        DreamRoomDoorDirection direction)
    {
        return cell.x + ":" + cell.y + ":" +
               (int)direction;
    }

    private static void ConfigureTemplate(
        DreamRoomTemplate template,
        List<Vector2Int> occupiedCells,
        List<Vector2Int> walkableCells,
        List<Vector2Int> blockedCells,
        Transform visualRoot,
        Transform socketsRoot,
        Transform navigationRoot,
        Transform spawnPointsRoot)
    {
        SerializedObject serialized =
            new SerializedObject(template);

        RequireProperty(serialized, "templateId")
            .stringValue = TemplateId;

        RequireProperty(serialized, "sizeInCells")
            .vector2IntValue = RoomSize;

        RequireProperty(serialized, "randomWeight")
            .intValue = 10;

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

        SetCellArray(
            RequireProperty(serialized, "occupiedCells"),
            occupiedCells);

        SetCellArray(
            RequireProperty(serialized, "walkableCells"),
            walkableCells);

        SetCellArray(
            RequireProperty(serialized, "blockedCells"),
            blockedCells);

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

    private static void SetCellArray(
        SerializedProperty property,
        List<Vector2Int> cells)
    {
        property.arraySize = cells.Count;

        for (int i = 0; i < cells.Count; i++)
        {
            property.GetArrayElementAtIndex(i)
                .vector2IntValue = cells[i];
        }
    }

    private static DreamRoomCatalog CreateCatalog(
        DreamRoomTemplate template)
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

        catalog.name = "RoomCatalog_R91_NonRectTest";

        SerializedObject serialized =
            new SerializedObject(catalog);

        RequireProperty(serialized, "catalogId")
            .stringValue = CatalogId;

        SerializedProperty templateList =
            RequireProperty(serialized, "roomTemplates");

        templateList.arraySize = 1;
        templateList.GetArrayElementAtIndex(0)
            .objectReferenceValue = template;

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
            AssetDatabase.CreateAsset(catalog, CatalogPath);
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
                new GameObject("NonRectRoomGallery_R91");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Gallery could not load Prefab: " +
                    PrefabPath);
            }

            DreamRoomTemplate template =
                prefab.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException(
                    "Gallery Prefab has no DreamRoomTemplate.");
            }

            float cursorX = 0f;
            float maximumHeight = 0f;

            for (int turns = 0; turns < 4; turns++)
            {
                Vector2Int rotatedSize =
                    template.GetRotatedSize(turns);

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        galleryScene) as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Gallery could not instantiate rotation " +
                        turns + ".");
                }

                instance.name =
                    PrefabName + "_Rotation_" +
                    (turns * 90);

                instance.transform.position =
                    new Vector3(
                        cursorX + rotatedSize.x * 0.5f,
                        0f,
                        0f);

                instance.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        -(turns * 90f));

                instance.transform.localScale = Vector3.one;
                instance.transform.SetParent(
                    galleryRoot.transform,
                    worldPositionStays: true);

                cursorX += rotatedSize.x + GalleryGap;

                maximumHeight = Mathf.Max(
                    maximumHeight,
                    rotatedSize.y);
            }

            float totalWidth = cursorX - GalleryGap;

            GameObject cameraObject =
                new GameObject("GalleryCamera");

            cameraObject.tag = "MainCamera";

            Camera galleryCamera =
                cameraObject.AddComponent<Camera>();

            galleryCamera.orthographic = true;
            galleryCamera.clearFlags =
                CameraClearFlags.SolidColor;

            galleryCamera.backgroundColor =
                new Color(0.025f, 0.035f, 0.055f);

            galleryCamera.orthographicSize =
                Mathf.Max(
                    maximumHeight * 0.65f + 1f,
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

    private static List<string> ValidateGeneratedAssets(
        out string summary)
    {
        List<string> errors = new List<string>();
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomNonRectSampleGeneratorR91] R9.1 校验通过");

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                SharedSpritePath);

        if (sprite == null)
        {
            errors.Add(
                "Shared graybox Sprite is missing: " +
                SharedSpritePath);
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        DreamRoomTemplate assetTemplate = null;

        if (prefabAsset == null)
        {
            errors.Add(
                "Generated Prefab is missing: " +
                PrefabPath);
        }
        else
        {
            assetTemplate =
                prefabAsset.GetComponent<DreamRoomTemplate>();

            if (assetTemplate == null)
            {
                errors.Add(
                    "Generated Prefab has no DreamRoomTemplate.");
            }

            if (PrefabUtility.GetPrefabAssetType(
                    prefabAsset) !=
                PrefabAssetType.Regular)
            {
                errors.Add(
                    "R9.1 sample must be an independent regular " +
                    "Prefab, not a Variant.");
            }
        }

        if (prefabAsset != null)
        {
            GameObject loadedRoot =
                PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                ValidateLoadedRoom(
                    loadedRoot,
                    errors,
                    report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    loadedRoot);
            }
        }

        ValidateCatalog(assetTemplate, errors, report);

        SceneAsset gallery =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GalleryScenePath);

        if (gallery == null)
        {
            errors.Add(
                "Four-rotation Gallery is missing: " +
                GalleryScenePath);
        }
        else
        {
            report.AppendLine(
                "- Gallery: 0/90/180/270 degree instances ready");
        }

        report.AppendLine(
            "- GameScene and RoomCatalog_Graybox: unchanged");

        summary = report.ToString();
        return errors;
    }

    private static void ValidateLoadedRoom(
        GameObject loadedRoot,
        List<string> errors,
        StringBuilder report)
    {
        if (loadedRoot == null)
        {
            errors.Add("Loaded R9.1 Prefab root is null.");
            return;
        }

        if (loadedRoot.name != PrefabName)
        {
            errors.Add("R9.1 Prefab has the wrong root name.");
        }

        if (loadedRoot.transform.localPosition != Vector3.zero ||
            loadedRoot.transform.localRotation !=
                Quaternion.identity ||
            loadedRoot.transform.localScale != Vector3.one)
        {
            errors.Add(
                "R9.1 Prefab root Transform must be reset.");
        }

        DreamRoomTemplate template =
            loadedRoot.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            errors.Add(
                "Loaded R9.1 Prefab lost DreamRoomTemplate.");
            return;
        }

        if (!string.Equals(
                template.TemplateId,
                TemplateId,
                StringComparison.Ordinal))
        {
            errors.Add("R9.1 template has the wrong Template Id.");
        }

        if (template.SizeInCells != RoomSize)
        {
            errors.Add("R9.1 template must be 10x8 cells.");
        }

        if (!template.AllowQuarterTurns)
        {
            errors.Add(
                "R9.1 template must allow Quarter Turns.");
        }

        if (!template.HasTag(DreamRoomTag.Standard))
        {
            errors.Add(
                "R9.1 template must retain the Standard tag.");
        }

        List<Vector2Int> expectedOccupied;
        List<Vector2Int> expectedWalkable;
        List<Vector2Int> expectedBlocked;

        BuildExpectedCells(
            out expectedOccupied,
            out expectedWalkable,
            out expectedBlocked);

        ValidateCellSet(
            "Occupied Cells",
            template.OccupiedCellOverrides,
            expectedOccupied,
            errors);

        ValidateCellSet(
            "Walkable Cells",
            template.WalkableCellOverrides,
            expectedWalkable,
            errors);

        ValidateCellSet(
            "Blocked Cells",
            template.BlockedCellOverrides,
            expectedBlocked,
            errors);

        List<string> templateErrors =
            template.GetValidationErrors();

        for (int i = 0; i < templateErrors.Count; i++)
        {
            errors.Add(
                "DreamRoomTemplate: " +
                templateErrors[i]);
        }

        ValidateHierarchyAndColliders(
            loadedRoot,
            template,
            expectedOccupied,
            errors);

        ValidateSpawnKinds(template, errors);

        for (int turns = 0; turns < 4; turns++)
        {
            ValidateRotation(
                template,
                turns,
                expectedOccupied.Count,
                expectedWalkable.Count,
                expectedBlocked.Count,
                errors);
        }

        if (errors.Count == 0)
        {
            report.AppendLine(
                "- Room_L10x08_R91: Occupied 50 / " +
                "Walkable 49 / Blocked 1");

            report.AppendLine(
                "- Sockets 4 / Spawn Points 4 / Rotations 4");
        }
    }

    private static void ValidateHierarchyAndColliders(
        GameObject loadedRoot,
        DreamRoomTemplate template,
        List<Vector2Int> expectedOccupied,
        List<string> errors)
    {
        ValidateExpectedRoot(
            loadedRoot.transform,
            template.VisualRoot,
            "Visual",
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.SocketsRoot,
            "Sockets",
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.NavigationRoot,
            "Navigation",
            errors);

        ValidateExpectedRoot(
            loadedRoot.transform,
            template.SpawnPointsRoot,
            "SpawnPoints",
            errors);

        Transform floorsRoot =
            FindDirectChild(template.VisualRoot, "Floors");

        Transform wallsRoot =
            FindDirectChild(template.VisualRoot, "Walls");

        Transform blockersRoot =
            FindDirectChild(
                template.VisualRoot,
                "DoorBlockers");

        Transform obstaclesRoot =
            FindDirectChild(template.VisualRoot, "Obstacles");

        ValidateFloorObject(
            floorsRoot,
            "Floor_BottomArm",
            new Vector3(0f, -2.5f, 0f),
            new Vector3(10f, 3f, 1f),
            errors);

        ValidateFloorObject(
            floorsRoot,
            "Floor_LeftArm",
            new Vector3(-3f, 1.5f, 0f),
            new Vector3(4f, 5f, 1f),
            errors);

        int expectedBoundaryEdges =
            CountBoundaryEdges(
                new HashSet<Vector2Int>(expectedOccupied));

        int expectedPermanentWalls =
            expectedBoundaryEdges -
            SocketDefinitions.Length * DoorWidthInCells;

        int permanentWallColliders =
            wallsRoot == null
                ? 0
                : wallsRoot.GetComponentsInChildren<
                    BoxCollider2D>(true).Length;

        if (permanentWallColliders !=
            expectedPermanentWalls)
        {
            errors.Add(
                "Permanent wall collider count should be " +
                expectedPermanentWalls +
                ", but found " +
                permanentWallColliders + ".");
        }

        int blockerColliders =
            blockersRoot == null
                ? 0
                : blockersRoot.GetComponentsInChildren<
                    BoxCollider2D>(true).Length;

        if (blockerColliders != SocketDefinitions.Length)
        {
            errors.Add(
                "Door blocker collider count should be 4, " +
                "but found " + blockerColliders + ".");
        }

        int obstacleColliders =
            obstaclesRoot == null
                ? 0
                : obstaclesRoot.GetComponentsInChildren<
                    BoxCollider2D>(true).Length;

        if (obstacleColliders != 1)
        {
            errors.Add(
                "Blocked Cell must have exactly one obstacle " +
                "collider, but found " +
                obstacleColliders + ".");
        }

        if (template.DoorSockets.Count != 4)
        {
            errors.Add(
                "R9.1 template must contain exactly 4 Door " +
                "Sockets.");
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
                errors.Add("R9.1 Door Sockets contains null.");
                continue;
            }

            directions.Add(socket.Direction);

            if (socket.DoorWidthInCells != DoorWidthInCells)
            {
                errors.Add(
                    socket.SocketId +
                    " must be two cells wide.");
            }

            if (socket.ClosedBlocker == null ||
                socket.ClosedBlocker.GetComponent<
                    BoxCollider2D>() == null)
            {
                errors.Add(
                    socket.SocketId +
                    " needs a closed blocker with BoxCollider2D.");
            }
            else if (!socket.ClosedBlocker.activeSelf)
            {
                errors.Add(
                    socket.SocketId +
                    " blocker must be active by default.");
            }
        }

        if (directions.Count != 4)
        {
            errors.Add(
                "R9.1 template must have one Socket in every " +
                "cardinal direction.");
        }
    }

    private static void ValidateSpawnKinds(
        DreamRoomTemplate template,
        List<string> errors)
    {
        if (template.SpawnPoints.Count != 4)
        {
            errors.Add(
                "R9.1 template must contain exactly 4 Spawn " +
                "Points.");
        }

        HashSet<DreamRoomSpawnPointKind> kinds =
            new HashSet<DreamRoomSpawnPointKind>();

        for (int i = 0;
             i < template.SpawnPoints.Count;
             i++)
        {
            DreamRoomSpawnPoint point =
                template.SpawnPoints[i];

            if (point != null)
            {
                kinds.Add(point.Kind);
            }
        }

        if (!kinds.Contains(DreamRoomSpawnPointKind.Player) ||
            !kinds.Contains(DreamRoomSpawnPointKind.Exit) ||
            !kinds.Contains(DreamRoomSpawnPointKind.Enemy) ||
            !kinds.Contains(DreamRoomSpawnPointKind.Item))
        {
            errors.Add(
                "R9.1 Spawn Points must include Player, Exit, " +
                "Enemy and Item.");
        }
    }

    private static void ValidateRotation(
        DreamRoomTemplate template,
        int quarterTurns,
        int expectedOccupiedCount,
        int expectedWalkableCount,
        int expectedBlockedCount,
        List<string> errors)
    {
        DreamRoomPlacement placement =
            new DreamRoomPlacement(
                template,
                new Vector2Int(100, 200),
                quarterTurns);

        List<Vector2Int> occupied =
            new List<Vector2Int>();

        List<Vector2Int> walkable =
            new List<Vector2Int>();

        List<Vector2Int> blocked =
            new List<Vector2Int>();

        placement.GetOccupiedGlobalCells(occupied);
        placement.GetWalkableGlobalCells(walkable);
        placement.GetBlockedGlobalCells(blocked);

        HashSet<Vector2Int> occupiedSet =
            new HashSet<Vector2Int>(occupied);

        HashSet<Vector2Int> walkableSet =
            new HashSet<Vector2Int>(walkable);

        HashSet<Vector2Int> blockedSet =
            new HashSet<Vector2Int>(blocked);

        string rotationLabel =
            (quarterTurns * 90) + " degrees";

        if (occupied.Count != expectedOccupiedCount ||
            occupiedSet.Count != expectedOccupiedCount)
        {
            errors.Add(
                rotationLabel +
                ": Occupied Cell count or uniqueness failed.");
        }

        if (walkable.Count != expectedWalkableCount ||
            walkableSet.Count != expectedWalkableCount)
        {
            errors.Add(
                rotationLabel +
                ": Walkable Cell count or uniqueness failed.");
        }

        if (blocked.Count != expectedBlockedCount ||
            blockedSet.Count != expectedBlockedCount)
        {
            errors.Add(
                rotationLabel +
                ": Blocked Cell count or uniqueness failed.");
        }

        foreach (Vector2Int cell in occupiedSet)
        {
            if (!placement.CellBounds.Contains(cell))
            {
                errors.Add(
                    rotationLabel +
                    ": an Occupied Cell lies outside rotated bounds.");
                break;
            }
        }

        foreach (Vector2Int cell in walkableSet)
        {
            if (!occupiedSet.Contains(cell) ||
                blockedSet.Contains(cell))
            {
                errors.Add(
                    rotationLabel +
                    ": Walkable/Occupied/Blocked relation failed.");
                break;
            }
        }

        List<Vector2Int> originalOccupied =
            new List<Vector2Int>();

        template.GetOccupiedCells(originalOccupied);

        for (int i = 0;
             i < originalOccupied.Count;
             i++)
        {
            Vector2Int global =
                placement.OriginalToGlobalCell(
                    originalOccupied[i]);

            Vector2Int roundTrip =
                placement.GlobalToOriginalCell(global);

            if (roundTrip != originalOccupied[i])
            {
                errors.Add(
                    rotationLabel +
                    ": Local/Global round-trip failed for " +
                    originalOccupied[i] + ".");
                break;
            }
        }

        for (int i = 0;
             i < template.SpawnPoints.Count;
             i++)
        {
            DreamRoomSpawnPoint point =
                template.SpawnPoints[i];

            if (point == null)
            {
                continue;
            }

            Vector2Int globalCell =
                placement.GetSpawnPointGlobalCell(point);

            if (!walkableSet.Contains(globalCell))
            {
                errors.Add(
                    rotationLabel +
                    ": Spawn Point '" + point.SpawnPointId +
                    "' is not on a rotated Walkable Cell.");
            }
        }

        List<Vector2Int> socketCells =
            new List<Vector2Int>();

        for (int i = 0;
             i < template.DoorSockets.Count;
             i++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[i];

            if (socket == null)
            {
                continue;
            }

            placement.GetSocketInsideCells(
                socket,
                socketCells);

            Vector2Int outsideOffset =
                placement.GetRotatedDirection(socket)
                    .ToCellOffset();

            for (int cellIndex = 0;
                 cellIndex < socketCells.Count;
                 cellIndex++)
            {
                Vector2Int insideCell =
                    socketCells[cellIndex];

                if (!walkableSet.Contains(insideCell))
                {
                    errors.Add(
                        rotationLabel +
                        ": Socket '" + socket.SocketId +
                        "' is not on a rotated Walkable Cell.");
                }

                if (occupiedSet.Contains(
                        insideCell + outsideOffset))
                {
                    errors.Add(
                        rotationLabel +
                        ": Socket '" + socket.SocketId +
                        "' does not face outside the rotated room.");
                }
            }
        }
    }

    private static void ValidateCatalog(
        DreamRoomTemplate expectedTemplate,
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

        if (!string.Equals(
                catalog.CatalogId,
                CatalogId,
                StringComparison.Ordinal))
        {
            errors.Add("R9.1 Catalog has the wrong Catalog Id.");
        }

        if (catalog.Count != 1 ||
            catalog.RoomTemplates[0] != expectedTemplate)
        {
            errors.Add(
                "R9.1 Catalog must reference only the isolated " +
                "L-shaped sample.");
        }

        List<string> catalogErrors =
            catalog.GetValidationErrors();

        for (int i = 0; i < catalogErrors.Count; i++)
        {
            errors.Add(
                "DreamRoomCatalog: " + catalogErrors[i]);
        }

        if (catalogErrors.Count == 0 &&
            catalog.Count == 1)
        {
            report.AppendLine(
                "- Catalog NonRect_R91_Test: isolated template 1");
        }
    }

    private static void ValidateCellSet(
        string label,
        IReadOnlyList<Vector2Int> actual,
        List<Vector2Int> expected,
        List<string> errors)
    {
        HashSet<Vector2Int> actualSet =
            new HashSet<Vector2Int>();

        if (actual != null)
        {
            for (int i = 0; i < actual.Count; i++)
            {
                actualSet.Add(actual[i]);
            }
        }

        HashSet<Vector2Int> expectedSet =
            new HashSet<Vector2Int>(expected);

        int actualCount = actual == null ? 0 : actual.Count;

        if (actualCount != expected.Count ||
            actualSet.Count != actualCount ||
            !actualSet.SetEquals(expectedSet))
        {
            errors.Add(
                label +
                " does not match the authoritative R9.1 cell set.");
        }
    }

    private static void ValidateExpectedRoot(
        Transform roomRoot,
        Transform referencedRoot,
        string expectedName,
        List<string> errors)
    {
        if (referencedRoot == null)
        {
            errors.Add(
                "R9.1 room has no " +
                expectedName + " Root reference.");
            return;
        }

        if (referencedRoot.parent != roomRoot ||
            referencedRoot.name != expectedName)
        {
            errors.Add(
                "R9.1 room has an invalid " +
                expectedName + " Root hierarchy.");
        }

        if (referencedRoot.localPosition != Vector3.zero ||
            referencedRoot.localRotation !=
                Quaternion.identity ||
            referencedRoot.localScale != Vector3.one)
        {
            errors.Add(
                "R9.1 " + expectedName +
                " Root Transform must be reset.");
        }
    }

    private static void ValidateFloorObject(
        Transform floorsRoot,
        string objectName,
        Vector3 expectedPosition,
        Vector3 expectedScale,
        List<string> errors)
    {
        Transform floor =
            FindDirectChild(floorsRoot, objectName);

        if (floor == null)
        {
            errors.Add("Missing floor visual: " + objectName);
            return;
        }

        if (floor.GetComponent<SpriteRenderer>() == null)
        {
            errors.Add(objectName + " needs a SpriteRenderer.");
        }

        if (floor.GetComponent<BoxCollider2D>() != null)
        {
            errors.Add(objectName + " must not have a collider.");
        }

        if (floor.localPosition != expectedPosition ||
            floor.localRotation != Quaternion.identity ||
            floor.localScale != expectedScale)
        {
            errors.Add(
                objectName +
                " does not match the L-shaped visual contract.");
        }
    }

    private static int CountBoundaryEdges(
        HashSet<Vector2Int> occupiedCells)
    {
        int count = 0;

        foreach (Vector2Int cell in occupiedCells)
        {
            for (int directionIndex = 0;
                 directionIndex < 4;
                 directionIndex++)
            {
                DreamRoomDoorDirection direction =
                    (DreamRoomDoorDirection)directionIndex;

                if (!occupiedCells.Contains(
                        cell + direction.ToCellOffset()))
                {
                    count++;
                }
            }
        }

        return count;
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
                createdObject.AddComponent<BoxCollider2D>();

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

        ResetTransform(child.transform);
        return child.transform;
    }

    private static void ResetTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static Transform FindDirectChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
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
                "' was not found. Restore the R9.0 baseline.");
        }

        return property;
    }

    private static Vector3 GetDirectionVector(
        DreamRoomDoorDirection direction)
    {
        Vector2Int offset = direction.ToCellOffset();

        return new Vector3(offset.x, offset.y, 0f);
    }

    private static bool IsHorizontal(
        DreamRoomDoorDirection direction)
    {
        return direction == DreamRoomDoorDirection.North ||
               direction == DreamRoomDoorDirection.South;
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

    private static void EnsureGeneratedFolders()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(RoomsFolder);
        EnsureFolder(CatalogFolder);
        EnsureFolder(GalleryFolder);
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
        return AssetDatabase.LoadAssetAtPath<GameObject>(
                   PrefabPath) != null ||
               AssetDatabase.LoadAssetAtPath<
                   DreamRoomCatalog>(CatalogPath) != null ||
               AssetDatabase.LoadAssetAtPath<SceneAsset>(
                   GalleryScenePath) != null;
    }

    private static bool IsGallerySceneLoaded()
    {
        for (int i = 0;
             i < SceneManager.sceneCount;
             i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.path == GalleryScenePath)
            {
                return true;
            }
        }

        return false;
    }

    private static void LogValidationFailure(
        List<string> errors)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomNonRectSampleGeneratorR91] R9.1 校验失败");

        for (int i = 0; i < errors.Count; i++)
        {
            report.Append("- ");
            report.AppendLine(errors[i]);
        }

        Debug.LogError(report.ToString());
    }

    private sealed class SocketDefinition
    {
        public string SocketId { get; }
        public DreamRoomDoorDirection Direction { get; }
        public Vector2Int BaseInsideCell { get; }

        public SocketDefinition(
            string socketId,
            DreamRoomDoorDirection direction,
            Vector2Int baseInsideCell)
        {
            SocketId = socketId;
            Direction = direction;
            BaseInsideCell = baseInsideCell;
        }
    }

    private sealed class SpawnDefinition
    {
        public string SpawnPointId { get; }
        public DreamRoomSpawnPointKind Kind { get; }
        public Vector2Int LocalCell { get; }

        public SpawnDefinition(
            string spawnPointId,
            DreamRoomSpawnPointKind kind,
            Vector2Int localCell)
        {
            SpawnPointId = spawnPointId;
            Kind = kind;
            LocalCell = localCell;
        }
    }
}
