using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P10.7：正式房间生产线。
///
/// 目的：把 Crossroad_01 首房间中已经验证过的做法收敛成一个通用 Editor 工具，
/// 后续房间不再复制 P10.0~P10.6 的阶段脚本。
///
/// 生产线只复用现有 DreamRoomTemplate / DreamRoomDoorSocket / DreamRoomCatalog；
/// 不新增第二套 Runtime 房间系统，不修改 DungeonGenerator / DungeonRenderer / A* / Enemy AI。
///
/// 新房间标准：
/// - 1 Cell = 1 Unity Unit
/// - 64 px / Cell
/// - Visual/Floor, Objects, Effects
/// - Navigation/Colliders/Interior, Perimeter
/// - Sockets + ClosedBlockers
/// - Runtime 三张同尺寸 PNG，可直接覆盖更新
/// - BlockedCells / Interior Collider 由每个房间按美术手工配置
/// </summary>
public sealed class DreamRoomProductionPipelineP107 : EditorWindow
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.7/";

    private const string ProductionRoot =
        "Assets/DreamDungeon/Production";

    private const string RoomsRoot =
        ProductionRoot + "/Rooms";

    private const string ProductionCatalogPath =
        ProductionRoot + "/Catalog/RoomCatalog_Production.asset";

    private const string ProductionCatalogId = "Production_Main";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    private const int PixelsPerCell = 64;
    private const int MaximumCellDimension = 64;
    private const float ClosedBlockerThickness = 0.35f;
    private const float PerimeterWallThickness = 0.35f;

    private static readonly Regex SafeRoomKeyRegex =
        new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    [SerializeField]
    private string roomKey = "Room02";

    [SerializeField]
    private Vector2Int sizeInCells = new Vector2Int(13, 9);

    [SerializeField]
    private bool northSocket = true;

    [SerializeField]
    private bool eastSocket = true;

    [SerializeField]
    private bool southSocket = true;

    [SerializeField]
    private bool westSocket = true;

    [SerializeField]
    private int doorWidthInCells = 2;

    [SerializeField]
    private int randomWeight = 10;

    [SerializeField]
    private int minimumFloor = 1;

    [SerializeField]
    private int maximumFloor;

    [SerializeField]
    private int maximumInstancesPerFloor = 1;

    [SerializeField]
    private bool allowQuarterTurns;

    [SerializeField]
    private DreamRoomTag roomTags = DreamRoomTag.Standard;

    private Vector2 scrollPosition;

    [MenuItem(MenuRoot + "1. Open Production Room Factory", false, 2770)]
    private static void OpenWindow()
    {
        DreamRoomProductionPipelineP107 window =
            GetWindow<DreamRoomProductionPipelineP107>();

        window.titleContent = new GUIContent("Production Rooms");
        window.minSize = new Vector2(470f, 610f);
        window.Show();
    }

    [MenuItem(MenuRoot + "2. Validate Selected Production Room", false, 2771)]
    private static void ValidateSelectedProductionRoomMenu()
    {
        GameObject prefab = GetSelectedPrefabAsset();
        if (prefab == null)
        {
            FailDialog(
                "请在 Project 窗口选择一个 Production Room Prefab，" +
                "然后再执行校验。" );
            return;
        }

        ValidateAndReportPrefab(prefab, showDialog: true);
    }

    [MenuItem(MenuRoot + "3. Publish Selected Room to Production_Main", false, 2772)]
    private static void PublishSelectedRoomMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            FailDialog("请先退出 Prefab Mode。");
            return;
        }

        GameObject prefab = GetSelectedPrefabAsset();
        if (prefab == null)
        {
            FailDialog("请先在 Project 窗口选择要发布的 Production Room Prefab。");
            return;
        }

        try
        {
            List<string> validationErrors = ValidatePrefabAsset(prefab);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "发布前校验失败：\n- " +
                    string.Join("\n- ", validationErrors));
            }

            DreamRoomTemplate template = prefab.GetComponent<DreamRoomTemplate>();
            DreamRoomCatalog catalog = RequireProductionCatalog();

            if (!string.Equals(catalog.CatalogId, ProductionCatalogId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production Catalog Id 应为 " + ProductionCatalogId +
                    "，实际=" + catalog.CatalogId + "。\n" +
                    "请确认 P10.6 已完成。" );
            }

            DreamRoomTemplate existing;
            if (catalog.TryGetTemplate(template.TemplateId, out existing))
            {
                if (existing == template)
                {
                    Debug.Log(
                        "[P10.7] 房间已经位于 Production_Main，无需重复发布。\n" +
                        "TemplateId=" + template.TemplateId +
                        " | CatalogCount=" + catalog.Count,
                        prefab);

                    EditorUtility.DisplayDialog(
                        "Already Published",
                        template.TemplateId + " 已经在 Production_Main 中。",
                        "OK");
                    return;
                }

                throw new InvalidOperationException(
                    "Production_Main 已存在相同 Template Id，但引用不同：" +
                    template.TemplateId + "。" );
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty roomTemplates =
                RequireProperty(serializedCatalog, "roomTemplates");

            int newIndex = roomTemplates.arraySize;
            roomTemplates.InsertArrayElementAtIndex(newIndex);
            roomTemplates.GetArrayElementAtIndex(newIndex)
                .objectReferenceValue = template;

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            List<string> catalogErrors = catalog.GetValidationErrors();
            if (catalogErrors.Count > 0)
            {
                // 回滚新条目，避免把坏 Catalog 落盘。
                serializedCatalog.Update();
                roomTemplates = RequireProperty(serializedCatalog, "roomTemplates");
                int rollbackIndex = roomTemplates.arraySize - 1;
                roomTemplates.GetArrayElementAtIndex(rollbackIndex)
                    .objectReferenceValue = null;
                roomTemplates.DeleteArrayElementAtIndex(rollbackIndex);
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);

                throw new InvalidOperationException(
                    "加入后 Production_Main 校验失败，已自动回滚：\n- " +
                    string.Join("\n- ", catalogErrors));
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[P10.7] Production Room 已发布到 Production_Main。\n" +
                "TemplateId=" + template.TemplateId +
                " | Prefab=" + AssetDatabase.GetAssetPath(prefab) + "\n" +
                "CatalogCount=" + catalog.Count +
                " | GameSceneCatalogAuthority=Unchanged\n" +
                "RuntimeCoreCodeChanged=False",
                prefab);

            EditorUtility.DisplayDialog(
                "P10.7 Published",
                template.TemplateId +
                " 已加入 Production_Main。\n\n" +
                "没有创建第二个 Catalog，也没有修改 GameScene。\n" +
                "下一步直接 Play 做运行时验收即可。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog("P10.7 发布中止。请查看 Console 第一条红色错误。");
        }
    }

    [MenuItem(MenuRoot + "4. Rebuild Selected Room Perimeter From Sockets", false, 2773)]
    private static void RebuildSelectedPerimeterMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            FailDialog("请先退出 Prefab Mode，再从 Project 选择 Prefab 执行重建。");
            return;
        }

        GameObject prefab = GetSelectedPrefabAsset();
        if (prefab == null)
        {
            FailDialog("请先选择一个 Production Room Prefab。");
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException("Prefab 根节点缺少 DreamRoomTemplate。");
            }

            Transform collidersRoot = root.transform.Find("Navigation/Colliders");
            if (collidersRoot == null)
            {
                throw new InvalidOperationException("缺少 Navigation/Colliders。");
            }

            Transform perimeter = collidersRoot.Find("Perimeter");
            if (perimeter == null)
            {
                throw new InvalidOperationException(
                    "当前 Prefab 不是 P10.7 标准几何层级，缺少 Navigation/Colliders/Perimeter。\n" +
                    "为避免给 Crossroad_01 等旧房间叠加第二套外周碰撞，本工具不会自动创建。" );
            }

            DestroyAllChildren(perimeter);

            template.RefreshDoorSockets();
            RebuildPerimeterFromSockets(template, perimeter);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[P10.7] Perimeter 已按当前 Sockets 重建。\n" +
                "TemplateId=" + template.TemplateId +
                " | InteriorCollidersPreserved=True\n" +
                "BlockedCellsPreserved=True",
                prefab);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog("Perimeter 重建失败。请查看 Console 第一条红色错误。");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    [MenuItem(MenuRoot + "5. Audit Production_Main", false, 2774)]
    private static void AuditProductionMainMenu()
    {
        try
        {
            DreamRoomCatalog catalog = RequireProductionCatalog();
            List<string> errors = new List<string>();

            if (!string.Equals(catalog.CatalogId, ProductionCatalogId, StringComparison.Ordinal))
            {
                errors.Add(
                    "Catalog Id 应为 Production_Main，实际=" + catalog.CatalogId + "。" );
            }

            errors.AddRange(catalog.GetValidationErrors());

            int productionCount = 0;
            int grayboxCount = 0;
            int validatedProductionRooms = 0;

            for (int i = 0; i < catalog.RoomTemplates.Count; i++)
            {
                DreamRoomTemplate template = catalog.RoomTemplates[i];
                if (template == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(template.gameObject);
                bool isProduction =
                    path.StartsWith(RoomsRoot + "/", StringComparison.Ordinal);

                if (isProduction)
                {
                    productionCount++;

                    GameObject prefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    List<string> roomErrors = ValidatePrefabAsset(prefab);
                    if (roomErrors.Count == 0)
                    {
                        validatedProductionRooms++;
                    }
                    else
                    {
                        for (int e = 0; e < roomErrors.Count; e++)
                        {
                            errors.Add(
                                template.TemplateId + "：" + roomErrors[e]);
                        }
                    }
                }
                else
                {
                    grayboxCount++;
                }
            }

            bool gameSceneAuthority =
                CheckGameSceneCatalogAuthorityWithoutSaving(catalog, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production_Main Audit 失败：\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log(
                "[P10.7] Production_Main 通用生产线校验通过。\n" +
                "CatalogId=" + catalog.CatalogId +
                " | Entries=" + catalog.Count + "\n" +
                "ProductionRooms=" + productionCount +
                " | ValidatedProductionRooms=" + validatedProductionRooms +
                " | GrayboxBridge=" + grayboxCount + "\n" +
                "GameSceneSingleCatalogAuthority=" + gameSceneAuthority + "\n" +
                "Pipeline=DreamRoomTemplate + RuntimeArt3Layers + Sockets + IndependentGeometry\n" +
                "RuntimeCoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.7 Pipeline Passed",
                "Production_Main 与现有 Production Room 均通过通用校验。\n\n" +
                "从下一间房开始，可以直接使用 Production Room Factory，" +
                "不再重走 P10.0~P10.6。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog("P10.7 Production_Main Audit 失败。请查看 Console 第一条红色错误。");
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("P10.7 Production Room Factory", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "此工具只建立新的正式房间资产骨架。\n" +
            "不会自动猜内部碰撞，也不会自动发布到 Production_Main。\n" +
            "先建房 → 覆盖图片 → 手工 BlockedCells / Interior Collider → Validate → Publish。",
            MessageType.Info);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("房间身份", EditorStyles.boldLabel);
        roomKey = EditorGUILayout.TextField("Room Key", roomKey);

        string normalizedKey = NormalizeRoomKey(roomKey);
        string templateId = string.IsNullOrEmpty(normalizedKey)
            ? "Production_<RoomKey>"
            : "Production_" + normalizedKey;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Template Id", templateId);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("尺寸", EditorStyles.boldLabel);
        sizeInCells = EditorGUILayout.Vector2IntField("Size In Cells", sizeInCells);
        sizeInCells.x = Mathf.Clamp(sizeInCells.x, 1, MaximumCellDimension);
        sizeInCells.y = Mathf.Clamp(sizeInCells.y, 1, MaximumCellDimension);

        int pixelWidth = sizeInCells.x * PixelsPerCell;
        int pixelHeight = sizeInCells.y * PixelsPerCell;

        EditorGUILayout.LabelField(
            "Runtime PNG",
            pixelWidth + " × " + pixelHeight + " px @ PPU64");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Sockets", EditorStyles.boldLabel);
        northSocket = EditorGUILayout.ToggleLeft("North", northSocket);
        eastSocket = EditorGUILayout.ToggleLeft("East", eastSocket);
        southSocket = EditorGUILayout.ToggleLeft("South", southSocket);
        westSocket = EditorGUILayout.ToggleLeft("West", westSocket);

        doorWidthInCells = EditorGUILayout.IntField("Door Width (Cells)", doorWidthInCells);
        doorWidthInCells = Mathf.Max(1, doorWidthInCells);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("随机选择规则", EditorStyles.boldLabel);
        randomWeight = Mathf.Max(1, EditorGUILayout.IntField("Random Weight", randomWeight));
        minimumFloor = Mathf.Max(1, EditorGUILayout.IntField("Minimum Floor", minimumFloor));
        maximumFloor = Mathf.Max(0, EditorGUILayout.IntField("Maximum Floor", maximumFloor));
        maximumInstancesPerFloor = Mathf.Max(
            0,
            EditorGUILayout.IntField("Max Instances / Floor", maximumInstancesPerFloor));
        allowQuarterTurns = EditorGUILayout.Toggle("Allow Quarter Turns", allowQuarterTurns);
        roomTags = (DreamRoomTag)EditorGUILayout.EnumFlagsField("Room Tags", roomTags);

        EditorGUILayout.Space(12f);
        EditorGUILayout.HelpBox(
            "Create 只生成：标准层级、三张透明 Runtime PNG、中心 Socket、ClosedBlocker、Perimeter。\n" +
            "Interior 与 BlockedCells 故意保持空白，等待你按正式图片手工配置。",
            MessageType.None);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
        if (GUILayout.Button("Create New Production Room", GUILayout.Height(34f)))
        {
            CreateRoomFromWindow();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "重要：Create 不会覆盖已存在的 Prefab 或 PNG。\n" +
            "正式图片后续直接覆盖 Art/Runtime 下同名 PNG 即可，Prefab Transform 保持 Identity。",
            MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void CreateRoomFromWindow()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            FailDialog("请先退出 Prefab Mode。");
            return;
        }

        string key = NormalizeRoomKey(roomKey);
        List<string> inputErrors = ValidateFactoryInput(
            key,
            sizeInCells,
            doorWidthInCells,
            northSocket,
            eastSocket,
            southSocket,
            westSocket,
            minimumFloor,
            maximumFloor);

        if (inputErrors.Count > 0)
        {
            FailDialog("输入无效：\n\n- " + string.Join("\n- ", inputErrors));
            return;
        }

        string roomFolder = RoomsRoot + "/" + key;
        string prefabPath = roomFolder + "/Room_" + key + ".prefab";
        string runtimeFolder = roomFolder + "/Art/Runtime";
        string floorPath = runtimeFolder + "/Room_" + key + "_Floor.png";
        string objectsPath = runtimeFolder + "/Room_" + key + "_Objects.png";
        string effectsPath = runtimeFolder + "/Room_" + key + "_Effects.png";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(floorPath) != null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(objectsPath) != null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(effectsPath) != null)
        {
            FailDialog(
                "目标房间已经存在。P10.7 为避免洗掉正式资产，不提供覆盖重建。\n\n" +
                "Prefab=" + prefabPath);
            return;
        }

        try
        {
            EnsureFolder(ProductionRoot);
            EnsureFolder(RoomsRoot);
            EnsureFolder(roomFolder);
            EnsureFolder(roomFolder + "/Art");
            EnsureFolder(runtimeFolder);

            int pixelWidth = sizeInCells.x * PixelsPerCell;
            int pixelHeight = sizeInCells.y * PixelsPerCell;

            CreateTransparentPng(floorPath, pixelWidth, pixelHeight);
            CreateTransparentPng(objectsPath, pixelWidth, pixelHeight);
            CreateTransparentPng(effectsPath, pixelWidth, pixelHeight);

            ConfigureTextureImporter(floorPath);
            ConfigureTextureImporter(objectsPath);
            ConfigureTextureImporter(effectsPath);

            Sprite floorSprite = RequireSprite(floorPath);
            Sprite objectsSprite = RequireSprite(objectsPath);
            Sprite effectsSprite = RequireSprite(effectsPath);

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;

            try
            {
                FactoryConfig config = new FactoryConfig
                {
                    Key = key,
                    Size = sizeInCells,
                    North = northSocket,
                    East = eastSocket,
                    South = southSocket,
                    West = westSocket,
                    DoorWidth = doorWidthInCells,
                    RandomWeight = randomWeight,
                    MinimumFloor = minimumFloor,
                    MaximumFloor = maximumFloor,
                    MaximumInstancesPerFloor = maximumInstancesPerFloor,
                    AllowQuarterTurns = allowQuarterTurns,
                    RoomTags = roomTags,
                    FloorSprite = floorSprite,
                    ObjectsSprite = objectsSprite,
                    EffectsSprite = effectsSprite
                };

                root = BuildRoomScaffold(previewScene, config);

                List<string> templateErrors =
                    root.GetComponent<DreamRoomTemplate>().GetValidationErrors();

                if (templateErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Prefab 保存前 DreamRoomTemplate 校验失败：\n- " +
                        string.Join("\n- ", templateErrors));
                }

                bool success;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath,
                    out success);

                if (!success || saved == null)
                {
                    throw new InvalidOperationException("Prefab 保存失败：" + prefabPath);
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
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                "[P10.7] 新 Production Room 骨架已建立。\n" +
                "TemplateId=Production_" + key + "\n" +
                "Size=" + sizeInCells.x + "x" + sizeInCells.y +
                " | RuntimeArt=" + pixelWidth + "x" + pixelHeight + " @ PPU64\n" +
                "Sockets=" + BuildSocketSummary(
                    northSocket,
                    eastSocket,
                    southSocket,
                    westSocket) +
                " | DoorWidth=" + doorWidthInCells + "\n" +
                "Art=Floor/Objects/Effects transparent placeholders\n" +
                "Geometry=Interior empty + BlockedCells empty by design\n" +
                "Perimeter=GeneratedFromSockets\n" +
                "ProductionMainChanged=False | GameSceneChanged=False | RuntimeCoreCodeChanged=False",
                prefab);

            EditorUtility.DisplayDialog(
                "P10.7 Room Created",
                "房间骨架已建立，但尚未加入 Production_Main。\n\n" +
                "下一步：\n" +
                "1. 覆盖 Art/Runtime 三张同名 PNG\n" +
                "2. 在 Prefab 中配置 Blocked Cells + Interior Collider\n" +
                "3. 选择 Prefab 执行 P10.7 Validate\n" +
                "4. 最后才 Publish 到 Production_Main",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog(
                "P10.7 建房中止。已存在资产不会被覆盖。\n" +
                "请查看 Console 第一条红色错误。" );
        }
    }

    private static GameObject BuildRoomScaffold(
        Scene previewScene,
        FactoryConfig config)
    {
        GameObject root = new GameObject("Room_" + config.Key);
        SceneManager.MoveGameObjectToScene(root, previewScene);

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        DreamRoomTemplate template = root.AddComponent<DreamRoomTemplate>();

        Transform visualRoot = CreateEmptyChild(root.transform, "Visual");
        Transform floorRoot = CreateEmptyChild(visualRoot, "Floor");
        Transform objectsRoot = CreateEmptyChild(visualRoot, "Objects");
        Transform effectsRoot = CreateEmptyChild(visualRoot, "Effects");

        CreateRuntimeSprite("Floor_Runtime", floorRoot, config.FloorSprite, -10);
        CreateRuntimeSprite("Objects_Runtime", objectsRoot, config.ObjectsSprite, 0);
        CreateRuntimeSprite("Effects_Runtime", effectsRoot, config.EffectsSprite, 10);

        Transform blockersRoot = CreateEmptyChild(objectsRoot, "ClosedBlockers");
        Transform socketsRoot = CreateEmptyChild(root.transform, "Sockets");
        Transform navigationRoot = CreateEmptyChild(root.transform, "Navigation");
        Transform collidersRoot = CreateEmptyChild(navigationRoot, "Colliders");
        CreateEmptyChild(collidersRoot, "Interior");
        Transform perimeterRoot = CreateEmptyChild(collidersRoot, "Perimeter");
        Transform spawnPointsRoot = CreateEmptyChild(root.transform, "SpawnPoints");

        CreateConfiguredSocketIfEnabled(
            config,
            DreamRoomDoorDirection.North,
            config.North,
            socketsRoot,
            blockersRoot);

        CreateConfiguredSocketIfEnabled(
            config,
            DreamRoomDoorDirection.East,
            config.East,
            socketsRoot,
            blockersRoot);

        CreateConfiguredSocketIfEnabled(
            config,
            DreamRoomDoorDirection.South,
            config.South,
            socketsRoot,
            blockersRoot);

        CreateConfiguredSocketIfEnabled(
            config,
            DreamRoomDoorDirection.West,
            config.West,
            socketsRoot,
            blockersRoot);

        ConfigureTemplate(
            template,
            config,
            visualRoot,
            socketsRoot,
            navigationRoot,
            spawnPointsRoot);

        template.RefreshDoorSockets();
        template.RefreshSpawnPoints();
        RebuildPerimeterFromSockets(template, perimeterRoot);

        return root;
    }

    private static void CreateConfiguredSocketIfEnabled(
        FactoryConfig config,
        DreamRoomDoorDirection direction,
        bool enabled,
        Transform socketsRoot,
        Transform blockersRoot)
    {
        if (!enabled)
        {
            return;
        }

        string socketId = direction + "_0";
        Vector2Int insideCell = GetCenteredDoorBaseCell(
            config.Size,
            direction);

        Vector3 socketLocalPosition = GetDoorCenterLocal(
            config.Size,
            direction,
            insideCell,
            config.DoorWidth);

        GameObject blocker = new GameObject("Blocker_" + socketId);
        blocker.transform.SetParent(blockersRoot, false);
        blocker.transform.localPosition = GetBoundaryPosition(
            config.Size,
            direction,
            socketLocalPosition);
        blocker.transform.localRotation = Quaternion.identity;
        blocker.transform.localScale = Vector3.one;

        BoxCollider2D blockerCollider = blocker.AddComponent<BoxCollider2D>();
        bool horizontal =
            direction == DreamRoomDoorDirection.North ||
            direction == DreamRoomDoorDirection.South;

        blockerCollider.size = horizontal
            ? new Vector2(config.DoorWidth, ClosedBlockerThickness)
            : new Vector2(ClosedBlockerThickness, config.DoorWidth);
        blockerCollider.offset = Vector2.zero;
        blockerCollider.isTrigger = false;

        GameObject socketObject = new GameObject("Door_" + socketId);
        socketObject.transform.SetParent(socketsRoot, false);
        socketObject.transform.localPosition = socketLocalPosition;
        socketObject.transform.localRotation = Quaternion.identity;
        socketObject.transform.localScale = Vector3.one;

        DreamRoomDoorSocket socket =
            socketObject.AddComponent<DreamRoomDoorSocket>();

        socket.Configure(
            socketId,
            direction,
            insideCell,
            config.DoorWidth,
            blocker);
    }

    private static void ConfigureTemplate(
        DreamRoomTemplate template,
        FactoryConfig config,
        Transform visualRoot,
        Transform socketsRoot,
        Transform navigationRoot,
        Transform spawnPointsRoot)
    {
        SerializedObject serialized = new SerializedObject(template);

        RequireProperty(serialized, "templateId")
            .stringValue = "Production_" + config.Key;
        RequireProperty(serialized, "sizeInCells")
            .vector2IntValue = config.Size;
        RequireProperty(serialized, "randomWeight")
            .intValue = config.RandomWeight;
        RequireProperty(serialized, "minimumFloor")
            .intValue = config.MinimumFloor;
        RequireProperty(serialized, "maximumFloor")
            .intValue = config.MaximumFloor;
        RequireProperty(serialized, "maximumInstancesPerFloor")
            .intValue = config.MaximumInstancesPerFloor;
        RequireProperty(serialized, "allowQuarterTurns")
            .boolValue = config.AllowQuarterTurns;
        RequireProperty(serialized, "roomTags")
            .intValue = (int)config.RoomTags;

        // 通用建房器不猜内部玩法几何。
        // 完整矩形 Occupied + 默认 Walkable，之后只由房间作者写 BlockedCells。
        RequireProperty(serialized, "occupiedCells").arraySize = 0;
        RequireProperty(serialized, "walkableCells").arraySize = 0;
        RequireProperty(serialized, "blockedCells").arraySize = 0;

        RequireProperty(serialized, "visualRoot").objectReferenceValue = visualRoot;
        RequireProperty(serialized, "socketsRoot").objectReferenceValue = socketsRoot;
        RequireProperty(serialized, "navigationRoot").objectReferenceValue = navigationRoot;
        RequireProperty(serialized, "spawnPointsRoot").objectReferenceValue = spawnPointsRoot;

        RequireProperty(serialized, "autoCollectDoorSockets").boolValue = true;
        RequireProperty(serialized, "doorSockets").arraySize = 0;
        RequireProperty(serialized, "autoCollectSpawnPoints").boolValue = true;
        RequireProperty(serialized, "spawnPoints").arraySize = 0;

        RequireProperty(serialized, "drawCellGrid").boolValue = true;
        RequireProperty(serialized, "drawDoorCells").boolValue = true;
        RequireProperty(serialized, "drawCellOverrides").boolValue = true;
        RequireProperty(serialized, "drawSpawnPoints").boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(template);
    }

    private static void RebuildPerimeterFromSockets(
        DreamRoomTemplate template,
        Transform perimeterRoot)
    {
        if (template == null || perimeterRoot == null)
        {
            return;
        }

        DestroyAllChildren(perimeterRoot);

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
        Vector2Int size = template.SizeInCells;
        bool horizontal =
            direction == DreamRoomDoorDirection.North ||
            direction == DreamRoomDoorDirection.South;

        float sideLength = horizontal ? size.x : size.y;
        float sideMin = -sideLength * 0.5f;
        float sideMax = sideLength * 0.5f;

        List<DoorGap> gaps = new List<DoorGap>();

        for (int i = 0; i < template.DoorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket = template.DoorSockets[i];
            if (socket == null || socket.Direction != direction)
            {
                continue;
            }

            float center = horizontal
                ? socket.transform.localPosition.x
                : socket.transform.localPosition.y;

            float halfGap = socket.DoorWidthInCells * 0.5f;
            gaps.Add(new DoorGap(center - halfGap, center + halfGap));
        }

        gaps.Sort((a, b) => a.Min.CompareTo(b.Min));

        float cursor = sideMin;
        int segmentIndex = 0;

        for (int i = 0; i < gaps.Count; i++)
        {
            DoorGap gap = gaps[i];
            float gapMin = Mathf.Clamp(gap.Min, sideMin, sideMax);
            float gapMax = Mathf.Clamp(gap.Max, sideMin, sideMax);

            if (gapMin > cursor + 0.001f)
            {
                CreatePerimeterSegment(
                    parent,
                    direction,
                    cursor,
                    gapMin,
                    segmentIndex++,
                    size);
            }

            cursor = Mathf.Max(cursor, gapMax);
        }

        if (cursor < sideMax - 0.001f)
        {
            CreatePerimeterSegment(
                parent,
                direction,
                cursor,
                sideMax,
                segmentIndex,
                size);
        }
    }

    private static void CreatePerimeterSegment(
        Transform parent,
        DreamRoomDoorDirection direction,
        float segmentMin,
        float segmentMax,
        int index,
        Vector2Int roomSize)
    {
        float length = segmentMax - segmentMin;
        if (length <= 0.001f)
        {
            return;
        }

        float center = (segmentMin + segmentMax) * 0.5f;
        float halfWidth = roomSize.x * 0.5f;
        float halfHeight = roomSize.y * 0.5f;

        Vector2 position;
        Vector2 colliderSize;

        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position = new Vector2(center, halfHeight);
                colliderSize = new Vector2(length, PerimeterWallThickness);
                break;

            case DreamRoomDoorDirection.South:
                position = new Vector2(center, -halfHeight);
                colliderSize = new Vector2(length, PerimeterWallThickness);
                break;

            case DreamRoomDoorDirection.East:
                position = new Vector2(halfWidth, center);
                colliderSize = new Vector2(PerimeterWallThickness, length);
                break;

            case DreamRoomDoorDirection.West:
                position = new Vector2(-halfWidth, center);
                colliderSize = new Vector2(PerimeterWallThickness, length);
                break;

            default:
                return;
        }

        GameObject wall = new GameObject(
            "Wall_" + direction + "_" + index);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = colliderSize;
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
    }

    private static List<string> ValidateFactoryInput(
        string key,
        Vector2Int size,
        int doorWidth,
        bool north,
        bool east,
        bool south,
        bool west,
        int minFloor,
        int maxFloor)
    {
        List<string> errors = new List<string>();

        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add("Room Key 不能为空。");
        }
        else if (!SafeRoomKeyRegex.IsMatch(key))
        {
            errors.Add("Room Key 只能使用英文字母、数字、下划线。");
        }

        if (size.x < 1 || size.y < 1 ||
            size.x > MaximumCellDimension ||
            size.y > MaximumCellDimension)
        {
            errors.Add("Size In Cells 必须在 1~64 之间。");
        }

        if (!north && !east && !south && !west)
        {
            errors.Add("至少需要一个 Socket。");
        }

        if (doorWidth < 1)
        {
            errors.Add("Door Width 必须至少为 1 Cell。");
        }

        if ((north || south) && doorWidth > size.x)
        {
            errors.Add("North/South 门宽不能超过房间宽度。");
        }

        if ((east || west) && doorWidth > size.y)
        {
            errors.Add("East/West 门宽不能超过房间高度。");
        }

        if (minFloor < 1)
        {
            errors.Add("Minimum Floor 必须至少为 1。");
        }

        if (maxFloor > 0 && maxFloor < minFloor)
        {
            errors.Add("Maximum Floor 不能小于 Minimum Floor。");
        }

        return errors;
    }

    private static void ValidateAndReportPrefab(
        GameObject prefab,
        bool showDialog)
    {
        List<string> errors = ValidatePrefabAsset(prefab);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.7] Production Room 通用校验失败：\n- " +
                string.Join("\n- ", errors),
                prefab);

            if (showDialog)
            {
                FailDialog("P10.7 校验失败。请查看 Console。");
            }

            return;
        }

        DreamRoomTemplate template = prefab.GetComponent<DreamRoomTemplate>();
        List<Vector2Int> occupied = new List<Vector2Int>();
        List<Vector2Int> blocked = new List<Vector2Int>();
        List<Vector2Int> walkable = new List<Vector2Int>();
        template.GetOccupiedCells(occupied);
        template.GetBlockedCells(blocked);
        template.GetWalkableCells(walkable);

        bool published = false;
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(ProductionCatalogPath);

        if (catalog != null)
        {
            DreamRoomTemplate catalogTemplate;
            published = catalog.TryGetTemplate(template.TemplateId, out catalogTemplate) &&
                        catalogTemplate == template;
        }

        Debug.Log(
            "[P10.7] Production Room 通用校验通过。\n" +
            "TemplateId=" + template.TemplateId +
            " | Size=" + template.SizeInCells.x + "x" + template.SizeInCells.y + "\n" +
            "Occupied=" + occupied.Count +
            " | Blocked=" + blocked.Count +
            " | Walkable=" + walkable.Count + "\n" +
            "Sockets=" + template.DoorSockets.Count +
            " | ArtLayers=Floor/Objects/Effects\n" +
            "RuntimeArtPixels=" +
            (template.SizeInCells.x * PixelsPerCell) + "x" +
            (template.SizeInCells.y * PixelsPerCell) +
            " @ PPU64\n" +
            "PublishedToProductionMain=" + published + "\n" +
            "RuntimeCoreCodeChanged=False",
            prefab);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "P10.7 Room Passed",
                template.TemplateId + " 通用校验通过。\n\n" +
                (published
                    ? "它已经位于 Production_Main。"
                    : "它尚未发布。确认图片与碰撞后，可执行 P10.7 Publish。"),
                "OK");
        }
    }

    private static List<string> ValidatePrefabAsset(GameObject prefab)
    {
        List<string> errors = new List<string>();

        if (prefab == null)
        {
            errors.Add("Prefab 为 null。");
            return errors;
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrWhiteSpace(prefabPath) ||
            !prefabPath.StartsWith(RoomsRoot + "/", StringComparison.Ordinal) ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("选择对象不是 Assets/DreamDungeon/Production/Rooms 下的 Prefab。");
            return errors;
        }

        DreamRoomTemplate template = prefab.GetComponent<DreamRoomTemplate>();
        if (template == null)
        {
            errors.Add("Prefab 根节点缺少 DreamRoomTemplate。");
            return errors;
        }

        // Prefab Asset 本体上的子组件引用可以被序列化读取，
        // 但 Unity 对 persistent Prefab Asset 直接执行 GetComponentInParent 时
        // 不保证返回 Prefab 根的 DreamRoomTemplate。
        // DreamRoomTemplate.GetValidationErrors() 内含 Socket/SpawnPoint 的 owner 检查，
        // 因此必须在 LoadPrefabContents 的真实层级上下文中执行。
        AddTemplateValidationErrorsFromLoadedPrefab(prefabPath, errors);

        if (!template.TemplateId.StartsWith("Production_", StringComparison.Ordinal))
        {
            errors.Add("Template Id 必须以 Production_ 开头。");
        }

        Vector2Int size = template.SizeInCells;
        if (size.x < 1 || size.y < 1 ||
            size.x > MaximumCellDimension || size.y > MaximumCellDimension)
        {
            errors.Add("Size In Cells 超出 1~64 范围。");
        }

        Transform visual = prefab.transform.Find("Visual");
        Transform floor = prefab.transform.Find("Visual/Floor");
        Transform objects = prefab.transform.Find("Visual/Objects");
        Transform effects = prefab.transform.Find("Visual/Effects");
        Transform sockets = prefab.transform.Find("Sockets");
        Transform navigation = prefab.transform.Find("Navigation");
        Transform colliders = prefab.transform.Find("Navigation/Colliders");
        Transform spawnPoints = prefab.transform.Find("SpawnPoints");

        RequireTransform(visual, "Visual", errors);
        RequireTransform(floor, "Visual/Floor", errors);
        RequireTransform(objects, "Visual/Objects", errors);
        RequireTransform(effects, "Visual/Effects", errors);
        RequireTransform(sockets, "Sockets", errors);
        RequireTransform(navigation, "Navigation", errors);
        RequireTransform(colliders, "Navigation/Colliders", errors);
        RequireTransform(spawnPoints, "SpawnPoints", errors);

        if (visual != null && template.VisualRoot != visual)
        {
            errors.Add("DreamRoomTemplate.VisualRoot 引用不匹配。");
        }

        if (sockets != null && template.SocketsRoot != sockets)
        {
            errors.Add("DreamRoomTemplate.SocketsRoot 引用不匹配。");
        }

        if (navigation != null && template.NavigationRoot != navigation)
        {
            errors.Add("DreamRoomTemplate.NavigationRoot 引用不匹配。");
        }

        if (spawnPoints != null && template.SpawnPointsRoot != spawnPoints)
        {
            errors.Add("DreamRoomTemplate.SpawnPointsRoot 引用不匹配。");
        }

        ValidateIdentityTransform(floor, "Visual/Floor", errors);
        ValidateIdentityTransform(objects, "Visual/Objects", errors);
        ValidateIdentityTransform(effects, "Visual/Effects", errors);

        string roomFolder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        string stem = prefabName.StartsWith("Room_", StringComparison.Ordinal)
            ? prefabName
            : "Room_" + prefab.name;
        string runtimeFolder = roomFolder + "/Art/Runtime";

        ValidateRuntimeArt(
            runtimeFolder + "/" + stem + "_Floor.png",
            floor,
            "Floor_Runtime",
            size,
            errors);
        ValidateRuntimeArt(
            runtimeFolder + "/" + stem + "_Objects.png",
            objects,
            "Objects_Runtime",
            size,
            errors);
        ValidateRuntimeArt(
            runtimeFolder + "/" + stem + "_Effects.png",
            effects,
            "Effects_Runtime",
            size,
            errors);

        Transform blockersRoot = prefab.transform.Find("Visual/Objects/ClosedBlockers");
        if (blockersRoot == null)
        {
            errors.Add("缺少 Visual/Objects/ClosedBlockers。");
        }

        HashSet<string> socketIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<Vector2Int> blockedCells = new List<Vector2Int>();
        template.GetBlockedCells(blockedCells);
        HashSet<Vector2Int> blockedSet = new HashSet<Vector2Int>(blockedCells);

        for (int i = 0; i < template.DoorSockets.Count; i++)
        {
            DreamRoomDoorSocket socket = template.DoorSockets[i];
            if (socket == null)
            {
                errors.Add("DoorSockets 包含 null。");
                continue;
            }

            if (!socketIds.Add(socket.SocketId))
            {
                errors.Add("Socket Id 重复：" + socket.SocketId + "。");
            }

            if (!template.ContainsLocalCell(socket.LocalInsideCell))
            {
                errors.Add(socket.SocketId + " LocalInsideCell 超出房间。");
            }

            if (socket.DoorWidthInCells < 1)
            {
                errors.Add(socket.SocketId + " DoorWidthInCells 必须至少为 1。");
            }

            if (socket.ClosedBlocker == null)
            {
                errors.Add(socket.SocketId + " 缺少 ClosedBlocker。");
            }
            else
            {
                if (socket.ClosedBlocker.GetComponent<SpriteRenderer>() != null)
                {
                    errors.Add(socket.SocketId + " ClosedBlocker 仍带 Debug SpriteRenderer。");
                }

                BoxCollider2D blockerCollider =
                    socket.ClosedBlocker.GetComponent<BoxCollider2D>();

                if (blockerCollider == null)
                {
                    errors.Add(socket.SocketId + " ClosedBlocker 缺少 BoxCollider2D。");
                }
            }

            List<Vector2Int> doorCells = GetSocketDoorCells(socket);
            for (int d = 0; d < doorCells.Count; d++)
            {
                Vector2Int cell = doorCells[d];
                if (!template.ContainsLocalCell(cell))
                {
                    errors.Add(socket.SocketId + " Door Cell 超出房间：" + cell + "。");
                }
                else if (blockedSet.Contains(cell))
                {
                    errors.Add(socket.SocketId + " Door Cell 被 BlockedCells 封死：" + cell + "。");
                }
            }
        }

        // 视觉 Runtime 节点不允许携带碰撞；ClosedBlocker 是独立逻辑节点，不在 Runtime Sprite 上。
        ValidateRuntimeNodeNoCollider(floor, "Floor_Runtime", errors);
        ValidateRuntimeNodeNoCollider(objects, "Objects_Runtime", errors);
        ValidateRuntimeNodeNoCollider(effects, "Effects_Runtime", errors);

        return errors;
    }

    private static void AddTemplateValidationErrorsFromLoadedPrefab(
        string prefabPath,
        List<string> errors)
    {
        GameObject loadedRoot = null;

        try
        {
            loadedRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (loadedRoot == null)
            {
                errors.Add("无法加载 Prefab Contents：" + prefabPath + "。");
                return;
            }

            DreamRoomTemplate loadedTemplate =
                loadedRoot.GetComponent<DreamRoomTemplate>();

            if (loadedTemplate == null)
            {
                errors.Add("加载后的 Prefab 根节点缺少 DreamRoomTemplate。");
                return;
            }

            // 在真实 Prefab 层级里重新收集，避免 persistent asset 的
            // GetComponentInParent owner 判定产生假阴性。
            loadedTemplate.RefreshDoorSockets();
            loadedTemplate.RefreshSpawnPoints();

            errors.AddRange(loadedTemplate.GetValidationErrors());
        }
        catch (Exception exception)
        {
            errors.Add(
                "Prefab Contents 校验失败：" +
                exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(loadedRoot);
            }
        }
    }

    private static void ValidateRuntimeArt(
        string path,
        Transform layerRoot,
        string runtimeNodeName,
        Vector2Int size,
        List<string> errors)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (texture == null || sprite == null)
        {
            errors.Add("缺少 Runtime Art：" + path + "。");
            return;
        }

        int expectedWidth = size.x * PixelsPerCell;
        int expectedHeight = size.y * PixelsPerCell;

        if (texture.width != expectedWidth || texture.height != expectedHeight)
        {
            errors.Add(
                Path.GetFileName(path) + " 尺寸应为 " +
                expectedWidth + "x" + expectedHeight +
                "，实际=" + texture.width + "x" + texture.height + "。");
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            errors.Add("找不到 TextureImporter：" + path + "。");
        }
        else
        {
            if (importer.textureType != TextureImporterType.Sprite)
            {
                errors.Add(Path.GetFileName(path) + " Texture Type 必须为 Sprite。");
            }

            if (Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerCell) > 0.001f)
            {
                errors.Add(Path.GetFileName(path) + " PPU 必须为 64。");
            }

            if (importer.filterMode != FilterMode.Point)
            {
                errors.Add(Path.GetFileName(path) + " Filter 必须为 Point。");
            }

            if (importer.mipmapEnabled)
            {
                errors.Add(Path.GetFileName(path) + " MipMap 必须关闭。");
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                errors.Add(Path.GetFileName(path) + " Compression 必须为 None/Uncompressed。");
            }
        }

        if (layerRoot == null)
        {
            return;
        }

        Transform runtimeNode = layerRoot.Find(runtimeNodeName);
        if (runtimeNode == null)
        {
            errors.Add("缺少 " + layerRoot.name + "/" + runtimeNodeName + "。");
            return;
        }

        ValidateIdentityTransform(runtimeNode, runtimeNodeName, errors);

        SpriteRenderer renderer = runtimeNode.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            errors.Add(runtimeNodeName + " 缺少 SpriteRenderer。");
        }
        else if (renderer.sprite != sprite)
        {
            errors.Add(runtimeNodeName + " 没有引用对应 Runtime PNG Sprite。");
        }
    }

    private static void ValidateRuntimeNodeNoCollider(
        Transform layerRoot,
        string runtimeNodeName,
        List<string> errors)
    {
        if (layerRoot == null)
        {
            return;
        }

        Transform runtimeNode = layerRoot.Find(runtimeNodeName);
        if (runtimeNode != null &&
            runtimeNode.GetComponent<Collider2D>() != null)
        {
            errors.Add(runtimeNodeName + " 不应携带 Collider2D。");
        }
    }

    private static bool CheckGameSceneCatalogAuthorityWithoutSaving(
        DreamRoomCatalog expectedCatalog,
        List<string> errors)
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            !string.Equals(scene.path, GameScenePath, StringComparison.Ordinal))
        {
            // 不为了 Audit 强行开关 Scene。当前不是 GameScene 时只报告“未现场核验”。
            return false;
        }

        DungeonGenerator generator = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && generator == null; i++)
        {
            generator = roots[i].GetComponentInChildren<DungeonGenerator>(true);
        }

        if (generator == null)
        {
            errors.Add("当前 GameScene 中找不到 DungeonGenerator。");
            return false;
        }

        SerializedObject serialized = new SerializedObject(generator);
        UnityEngine.Object sceneCatalog =
            RequireProperty(serialized, "templateFirstRoomCatalog").objectReferenceValue;

        if (sceneCatalog != expectedCatalog)
        {
            errors.Add("GameScene 的 Template First Room Catalog 不是 Production_Main。");
            return false;
        }

        return true;
    }

    private static List<Vector2Int> GetSocketDoorCells(DreamRoomDoorSocket socket)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        Vector2Int sideways = socket.Direction.PerpendicularCellOffset();
        int startOffset = -(socket.DoorWidthInCells / 2);

        for (int i = 0; i < socket.DoorWidthInCells; i++)
        {
            cells.Add(
                socket.LocalInsideCell +
                sideways * (startOffset + i));
        }

        return cells;
    }

    private static Vector2Int GetCenteredDoorBaseCell(
        Vector2Int size,
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Vector2Int(size.x / 2, size.y - 1);

            case DreamRoomDoorDirection.East:
                return new Vector2Int(size.x - 1, size.y / 2);

            case DreamRoomDoorDirection.South:
                return new Vector2Int(size.x / 2, 0);

            case DreamRoomDoorDirection.West:
                return new Vector2Int(0, size.y / 2);

            default:
                return Vector2Int.zero;
        }
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

    private static Transform CreateEmptyChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static void CreateRuntimeSprite(
        string name,
        Transform parent,
        Sprite sprite,
        int sortingOrder)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
    }

    private static void CreateTransparentPng(
        string assetPath,
        int width,
        int height)
    {
        if (File.Exists(ToAbsolutePath(assetPath)))
        {
            throw new InvalidOperationException(
                "为避免覆盖用户资产，文件已存在：" + assetPath);
        }

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);

        try
        {
            texture.SetPixels32(new Color32[width * height]);
            texture.Apply(false, false);
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(ToAbsolutePath(assetPath), bytes);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureTextureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("找不到 TextureImporter：" + path);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerCell;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static Sprite RequireSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new InvalidOperationException("找不到 Sprite：" + path);
        }

        return sprite;
    }

    private static DreamRoomCatalog RequireProductionCatalog()
    {
        DreamRoomCatalog catalog =
            AssetDatabase.LoadAssetAtPath<DreamRoomCatalog>(ProductionCatalogPath);

        if (catalog == null)
        {
            throw new InvalidOperationException(
                "找不到 Production Catalog：\n" + ProductionCatalogPath +
                "\n请确认 P10.6 已完成。" );
        }

        return catalog;
    }

    private static GameObject GetSelectedPrefabAsset()
    {
        GameObject selected = Selection.activeObject as GameObject;
        if (selected == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到 SerializedProperty：" + propertyName);
        }

        return property;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(projectRoot, normalized);
    }

    private static void DestroyAllChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void RequireTransform(
        Transform transform,
        string path,
        List<string> errors)
    {
        if (transform == null)
        {
            errors.Add("缺少 " + path + "。");
        }
    }

    private static void ValidateIdentityTransform(
        Transform transform,
        string label,
        List<string> errors)
    {
        if (transform == null)
        {
            return;
        }

        if (transform.localPosition != Vector3.zero ||
            transform.localRotation != Quaternion.identity ||
            transform.localScale != Vector3.one)
        {
            errors.Add(label + " Transform 必须保持 Position0 / Rotation0 / Scale1。");
        }
    }

    private static string NormalizeRoomKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string BuildSocketSummary(
        bool north,
        bool east,
        bool south,
        bool west)
    {
        List<string> sides = new List<string>();
        if (north) sides.Add("N");
        if (east) sides.Add("E");
        if (south) sides.Add("S");
        if (west) sides.Add("W");
        return string.Join("/", sides);
    }

    private static void FailDialog(string message)
    {
        EditorUtility.DisplayDialog("P10.7", message, "OK");
    }

    private struct DoorGap
    {
        public readonly float Min;
        public readonly float Max;

        public DoorGap(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    private sealed class FactoryConfig
    {
        public string Key;
        public Vector2Int Size;
        public bool North;
        public bool East;
        public bool South;
        public bool West;
        public int DoorWidth;
        public int RandomWeight;
        public int MinimumFloor;
        public int MaximumFloor;
        public int MaximumInstancesPerFloor;
        public bool AllowQuarterTurns;
        public DreamRoomTag RoomTags;
        public Sprite FloorSprite;
        public Sprite ObjectsSprite;
        public Sprite EffectsSprite;
    }
}
