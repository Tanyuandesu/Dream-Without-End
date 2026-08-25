using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.4：Crossroad_01 正式三层美术资产契约。
///
/// 目标：
/// 1. 把 P10.2 CompositeDraft 从 Runtime 依赖降级为 Working 参考。
/// 2. 建立唯一正式 Runtime 路径：Floor / Objects / Effects。
/// 3. 当前视觉保持不变：第一次执行时用 CompositeDraft 复制出 Floor，
///    Objects / Effects 先生成 1024x1024 真透明占位 PNG。
/// 4. 以后用户只需覆盖三张 Runtime PNG，不再需要重新拖 Sprite 或修改 Transform。
/// 5. 视觉层绝不携带 Collider；P10.2.5 Navigation / BlockedCells 保持唯一游戏几何。
///
/// 本阶段不修改：Catalog / GameScene / DungeonGenerator / DungeonRenderer / A* / Enemy AI。
/// </summary>
public static class DreamRoomProductionArtLayersP104
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.4/";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string ArtRoot =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Art";

    private const string RuntimeFolder = ArtRoot + "/Runtime";
    private const string WorkingFolder = ArtRoot + "/Working";

    private const string LegacyDraftPath =
        ArtRoot + "/Room_Crossroad_01_CompositeDraft.png";

    private const string WorkingDraftPath =
        WorkingFolder + "/Room_Crossroad_01_CompositeDraft.png";

    private const string FloorPath =
        RuntimeFolder + "/Room_Crossroad_01_Floor.png";

    private const string ObjectsPath =
        RuntimeFolder + "/Room_Crossroad_01_Objects.png";

    private const string EffectsPath =
        RuntimeFolder + "/Room_Crossroad_01_Effects.png";

    private const string FloorRuntimeName = "Floor_Runtime";
    private const string ObjectsRuntimeName = "Objects_Runtime";
    private const string EffectsRuntimeName = "Effects_Runtime";

    private const int ExpectedPixels = 1024;
    private const float ExpectedPpu = 64f;
    private const int ExpectedBlocked = 108;
    private const int ExpectedWalkable = 148;
    private static readonly Vector2Int ExpectedSize = new Vector2Int(16, 16);

    [MenuItem(MenuRoot + "1. Apply Formal Art Layer Contract", false, 2740)]
    private static void ApplyFormalArtLayerContract()
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

        try
        {
            EnsureFolder("Assets/DreamDungeon/Production/Rooms/Crossroad_01");
            EnsureFolder(ArtRoot);
            EnsureFolder(RuntimeFolder);
            EnsureFolder(WorkingFolder);

            // 第一次正式化时，从当前 CompositeDraft 复制出 Floor。
            // 若 Floor 已存在，则永不覆盖，避免日后用户手工修改被工具洗掉。
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FloorPath) == null)
            {
                string draftSource = ResolveDraftSource();

                if (string.IsNullOrEmpty(draftSource))
                {
                    throw new InvalidOperationException(
                        "找不到 CompositeDraft，且正式 Floor 尚不存在。\n" +
                        "预期位置：\n" + LegacyDraftPath + "\n或\n" + WorkingDraftPath);
                }

                if (!AssetDatabase.CopyAsset(draftSource, FloorPath))
                {
                    throw new InvalidOperationException(
                        "无法从 CompositeDraft 复制正式 Floor：\n" + draftSource);
                }
            }

            CreateTransparentPngIfMissing(ObjectsPath);
            CreateTransparentPngIfMissing(EffectsPath);

            ConfigureTextureImporter(FloorPath);
            ConfigureTextureImporter(ObjectsPath);
            ConfigureTextureImporter(EffectsPath);

            Sprite floorSprite = RequireSprite(FloorPath);
            Sprite objectsSprite = RequireSprite(ObjectsPath);
            Sprite effectsSprite = RequireSprite(EffectsPath);

            ValidateSpriteTexture(floorSprite, "Floor");
            ValidateSpriteTexture(objectsSprite, "Objects");
            ValidateSpriteTexture(effectsSprite, "Effects");

            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadPrefabPath);

            if (prefabAsset == null)
            {
                throw new InvalidOperationException(
                    "找不到 Crossroad_01 Prefab：\n" + CrossroadPrefabPath);
            }

            GameObject root = null;

            try
            {
                root = PrefabUtility.LoadPrefabContents(CrossroadPrefabPath);

                DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
                if (template == null)
                {
                    throw new InvalidOperationException(
                        "Crossroad_01 根节点缺少 DreamRoomTemplate。");
                }

                ValidateGeometryPrerequisites(root, template);

                Transform visualRoot = root.transform.Find("Visual");
                Transform floorRoot = root.transform.Find("Visual/Floor");
                Transform objectsRoot = root.transform.Find("Visual/Objects");
                Transform effectsRoot = root.transform.Find("Visual/Effects");

                if (visualRoot == null || floorRoot == null ||
                    objectsRoot == null || effectsRoot == null)
                {
                    throw new InvalidOperationException(
                        "Visual/Floor/Objects/Effects 层级不完整。请先完成 P10.0。");
                }

                RebuildSingleRuntimeSprite(
                    floorRoot,
                    FloorRuntimeName,
                    floorSprite,
                    -10);

                // 只移除本项目已知的 Runtime 占位，不碰 ClosedBlockers 等游戏对象。
                RebuildSingleRuntimeSprite(
                    objectsRoot,
                    ObjectsRuntimeName,
                    objectsSprite,
                    0,
                    preserveChildrenExceptRuntime: true);

                RebuildSingleRuntimeSprite(
                    effectsRoot,
                    EffectsRuntimeName,
                    effectsSprite,
                    10,
                    preserveChildrenExceptRuntime: true);

                List<string> errors =
                    ValidateLoadedPrefab(root, template);

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "P10.4 保存前校验失败：\n- " + string.Join("\n- ", errors));
                }

                PrefabUtility.SaveAsPrefabAsset(root, CrossroadPrefabPath);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // Prefab 已不再依赖旧 Draft 后，再把 Draft 归档到 Working。
            ArchiveCompositeDraftIfNeeded();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[P10.4] Crossroad_01 正式三层美术契约已建立。\n" +
                "Runtime/Floor=Room_Crossroad_01_Floor.png\n" +
                "Runtime/Objects=Room_Crossroad_01_Objects.png\n" +
                "Runtime/Effects=Room_Crossroad_01_Effects.png\n" +
                "All=1024x1024 | PPU64 | Point | Uncompressed | MipMapOff\n" +
                "Prefab=Visual/Floor/Floor_Runtime + Visual/Objects/Objects_Runtime + Visual/Effects/Effects_Runtime\n" +
                "CurrentLookPreserved=True | FloorInitializedFromComposite=True/Existing\n" +
                "ObjectsAndEffects=TransparentSlotsUntilUserOverwrites\n" +
                "P10.2.5GeometryPreserved=True | CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.4 Art Layers Ready",
                "正式三层 Runtime 路径已经建立。\n\n" +
                "当前画面不会改变：Floor 首次由 CompositeDraft 复制，Objects / Effects 先是真透明占位。\n\n" +
                "以后只需覆盖 Runtime 下三张同名 1024x1024 PNG，Prefab 不需要重新绑定。\n\n" +
                "现在执行 P10.4 第 2 项 Validate。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog(
                "P10.4 已中止。请把 Console 第一条红色错误发给我。");
        }
    }

    [MenuItem(MenuRoot + "2. Validate Formal Art Layer Contract", false, 2741)]
    private static void ValidateFormalArtLayerContract()
    {
        try
        {
            List<string> errors = new List<string>();

            ValidateTextureAsset(FloorPath, "Floor", errors);
            ValidateTextureAsset(ObjectsPath, "Objects", errors);
            ValidateTextureAsset(EffectsPath, "Effects", errors);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadPrefabPath);

            if (prefab == null)
            {
                errors.Add("找不到 Crossroad_01 Prefab。");
            }
            else
            {
                DreamRoomTemplate template = prefab.GetComponent<DreamRoomTemplate>();
                errors.AddRange(ValidateLoadedPrefab(prefab, template));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "P10.4 校验失败：\n- " + string.Join("\n- ", errors));
            }

            string archivedState =
                AssetDatabase.LoadAssetAtPath<Texture2D>(WorkingDraftPath) != null
                    ? "Working/CompositeDraft=Archived"
                    : "Working/CompositeDraft=NotFoundButRuntimeIndependent";

            Debug.Log(
                "[P10.4] Crossroad_01 正式三层美术校验通过。\n" +
                "Floor=1024x1024 RGBA | PPU64 | Point | Uncompressed | MipMapOff\n" +
                "Objects=1024x1024 RGBA | PPU64 | Point | Uncompressed | MipMapOff\n" +
                "Effects=1024x1024 RGBA | PPU64 | Point | Uncompressed | MipMapOff\n" +
                "Hierarchy=Floor_Runtime / Objects_Runtime / Effects_Runtime\n" +
                "Transforms=Identity | RuntimeArtSpriteColliders=0\n" +
                "Geometry=Blocked108 | Walkable148 | P10.2.5Preserved\n" +
                archivedState + "\n" +
                "RuntimeImageEditMethod=OverwriteSameNamePNGOnly\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.4 Passed",
                "Crossroad_01 已切换到正式三层 Runtime 资产契约。\n\n" +
                "以后普通美术修改只覆盖 Runtime 三张同名 PNG；只有可行走区域改变时才同步修改 BlockedCells / Collider。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog(
                "P10.4 校验失败。请把 Console 第一条红色错误发给我。");
        }
    }

    private static void ValidateGeometryPrerequisites(
        GameObject root,
        DreamRoomTemplate template)
    {
        if (template.SizeInCells != ExpectedSize)
        {
            throw new InvalidOperationException(
                "SizeInCells 应为 16x16。");
        }

        List<Vector2Int> blocked = new List<Vector2Int>();
        List<Vector2Int> walkable = new List<Vector2Int>();
        template.GetBlockedCells(blocked);
        template.GetWalkableCells(walkable);

        if (blocked.Count != ExpectedBlocked || walkable.Count != ExpectedWalkable)
        {
            throw new InvalidOperationException(
                "P10.2.5 Geometry 不符合当前权威值。Blocked=" +
                blocked.Count + " Walkable=" + walkable.Count +
                "，预期 108 / 148。");
        }

        if (root.transform.Find("Navigation/Colliders/P10_1_Geometry") == null)
        {
            throw new InvalidOperationException(
                "找不到 P10.2.5 Geometry 根节点。");
        }
    }

    private static void RebuildSingleRuntimeSprite(
        Transform layerRoot,
        string runtimeName,
        Sprite sprite,
        int sortingOrder,
        bool preserveChildrenExceptRuntime = false)
    {
        List<GameObject> remove = new List<GameObject>();

        for (int i = 0; i < layerRoot.childCount; i++)
        {
            Transform child = layerRoot.GetChild(i);

            bool isKnownRuntime =
                child.name == runtimeName ||
                child.name == "CompositeDraft_Runtime" ||
                child.name == "Floor_Placeholder";

            if (!preserveChildrenExceptRuntime || isKnownRuntime)
            {
                if (isKnownRuntime || !preserveChildrenExceptRuntime)
                {
                    remove.Add(child.gameObject);
                }
            }
        }

        for (int i = 0; i < remove.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(remove[i]);
        }

        GameObject runtime = new GameObject(runtimeName);
        runtime.transform.SetParent(layerRoot, false);
        runtime.transform.localPosition = Vector3.zero;
        runtime.transform.localRotation = Quaternion.identity;
        runtime.transform.localScale = Vector3.one;

        SpriteRenderer renderer = runtime.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
    }

    private static List<string> ValidateLoadedPrefab(
        GameObject root,
        DreamRoomTemplate template)
    {
        List<string> errors = new List<string>();

        if (root == null)
        {
            errors.Add("Prefab root 为 null。");
            return errors;
        }

        if (template == null)
        {
            errors.Add("缺少 DreamRoomTemplate。");
            return errors;
        }

        if (template.SizeInCells != ExpectedSize)
        {
            errors.Add("SizeInCells 不是 16x16。");
        }

        List<Vector2Int> blocked = new List<Vector2Int>();
        List<Vector2Int> walkable = new List<Vector2Int>();
        template.GetBlockedCells(blocked);
        template.GetWalkableCells(walkable);

        if (blocked.Count != ExpectedBlocked)
        {
            errors.Add("Blocked Cells 应为 108，实际=" + blocked.Count + "。");
        }

        if (walkable.Count != ExpectedWalkable)
        {
            errors.Add("Walkable Cells 应为 148，实际=" + walkable.Count + "。");
        }

        ValidateRuntimeNode(
            root.transform.Find("Visual/Floor/" + FloorRuntimeName),
            FloorPath,
            "Floor_Runtime",
            errors);

        ValidateRuntimeNode(
            root.transform.Find("Visual/Objects/" + ObjectsRuntimeName),
            ObjectsPath,
            "Objects_Runtime",
            errors);

        ValidateRuntimeNode(
            root.transform.Find("Visual/Effects/" + EffectsRuntimeName),
            EffectsPath,
            "Effects_Runtime",
            errors);

        Transform geometry =
            root.transform.Find("Navigation/Colliders/P10_1_Geometry");

        if (geometry == null)
        {
            errors.Add("P10.2.5 Geometry 不存在。");
        }

        ValidateNoColliderOnRuntimeSprite(
            root.transform.Find("Visual/Floor/" + FloorRuntimeName),
            "Floor_Runtime",
            errors);

        ValidateNoColliderOnRuntimeSprite(
            root.transform.Find("Visual/Objects/" + ObjectsRuntimeName),
            "Objects_Runtime",
            errors);

        ValidateNoColliderOnRuntimeSprite(
            root.transform.Find("Visual/Effects/" + EffectsRuntimeName),
            "Effects_Runtime",
            errors);

        // 注意：Visual/Objects/ClosedBlockers 是 DoorSocket 的游戏逻辑对象，
        // 按 R7.3 设计允许携带 Collider2D；P10.4 只禁止三张美术 Sprite 自带 Collider。
        return errors;
    }


    private static void ValidateNoColliderOnRuntimeSprite(
        Transform node,
        string label,
        List<string> errors)
    {
        if (node == null)
        {
            return;
        }

        Collider2D[] colliders = node.GetComponents<Collider2D>();
        if (colliders.Length != 0)
        {
            errors.Add(label + " 不应携带 Collider2D，实际=" + colliders.Length + "。");
        }
    }

    private static void ValidateRuntimeNode(
        Transform node,
        string expectedSpritePath,
        string label,
        List<string> errors)
    {
        if (node == null)
        {
            errors.Add("缺少 " + label + "。");
            return;
        }

        if (node.localPosition != Vector3.zero ||
            node.localRotation != Quaternion.identity ||
            node.localScale != Vector3.one)
        {
            errors.Add(label + " Transform 必须是 Identity。");
        }

        SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            errors.Add(label + " 缺少 SpriteRenderer。");
            return;
        }

        Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(expectedSpritePath);
        if (expected == null || renderer.sprite != expected)
        {
            errors.Add(label + " 没有引用预期 Runtime Sprite。");
        }
    }

    private static void ValidateTextureAsset(
        string path,
        string label,
        List<string> errors)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            errors.Add(label + " Sprite 不存在：" + path);
            return;
        }

        Texture2D texture = sprite.texture;
        if (texture == null || texture.width != ExpectedPixels || texture.height != ExpectedPixels)
        {
            errors.Add(label + " 必须是 1024x1024。");
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            errors.Add(label + " 无法读取 TextureImporter。");
            return;
        }

        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spritePixelsPerUnit != ExpectedPpu ||
            importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            errors.Add(label + " Import 设置不符合 P10.4 契约。");
        }
    }

    private static string ResolveDraftSource()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyDraftPath) != null)
        {
            return LegacyDraftPath;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(WorkingDraftPath) != null)
        {
            return WorkingDraftPath;
        }

        return null;
    }

    private static void ArchiveCompositeDraftIfNeeded()
    {
        Texture2D legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyDraftPath);
        Texture2D working = AssetDatabase.LoadAssetAtPath<Texture2D>(WorkingDraftPath);

        if (legacy == null)
        {
            return;
        }

        if (working != null)
        {
            // 不覆盖已有 Working 文件，避免损失用户后续修改。
            return;
        }

        string error = AssetDatabase.MoveAsset(LegacyDraftPath, WorkingDraftPath);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(
                "CompositeDraft 归档失败：" + error);
        }
    }

    private static void CreateTransparentPngIfMissing(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            ExpectedPixels,
            ExpectedPixels,
            TextureFormat.RGBA32,
            false);

        try
        {
            // Color32 默认值即 RGBA=(0,0,0,0)，是真透明 Alpha。
            texture.SetPixels32(new Color32[ExpectedPixels * ExpectedPixels]);
            texture.Apply(false, false);

            byte[] png = texture.EncodeToPNG();
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absolutePath, png);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceSynchronousImport);
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

    private static void ValidateSpriteTexture(Sprite sprite, string label)
    {
        if (sprite == null || sprite.texture == null ||
            sprite.texture.width != ExpectedPixels ||
            sprite.texture.height != ExpectedPixels)
        {
            throw new InvalidOperationException(
                label + " 必须是 1024x1024 Sprite。");
        }
    }

    private static void ConfigureTextureImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                "无法取得 TextureImporter：" + path);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ExpectedPpu;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(projectRoot, relative);
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)
            .Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static void FailDialog(string message)
    {
        EditorUtility.DisplayDialog("P10.4", message, "OK");
    }
}
