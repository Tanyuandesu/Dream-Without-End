using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.2：把 Crossroad_01 的 16x16 正式视觉契约接到 Prefab。
///
/// 本阶段使用一张 1024x1024 的 Composite Draft：
/// - 来源是当前 1440x1440 母图按工程契约缩放到 1024x1024。
/// - 只用于验证“正式图片 -> Prefab -> Hybrid Runtime”之前的视觉资产链。
/// - 后续 P10.3 拆分 Floor / Objects / Effects 时，会由分层正式资源替换它，
///   不保留第二套长期并行视觉。
///
/// 本阶段不做：
/// - 不修改 Catalog / GameScene。
/// - 不修改 DungeonGenerator / DungeonRenderer / A* / Enemy AI。
/// - 不从图片自动生成 Collider。
/// - 不修改 P10.1 的 Blocked Cells / Navigation Colliders。
/// </summary>
public static class DreamRoomProductionVisualP102
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.2/";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const string DraftTexturePath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Art/Room_Crossroad_01_CompositeDraft.png";

    private const string RuntimeVisualName =
        "CompositeDraft_Runtime";

    private const int ExpectedPixels = 1024;
    private const float ExpectedPpu = 64f;
    private static readonly Vector2Int ExpectedSize =
        new Vector2Int(16, 16);

    [MenuItem(
        MenuRoot + "Apply Composite Draft Visual",
        false,
        2720)]
    private static void ApplyCompositeDraftVisual()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Exit Prefab Mode first",
                "请先退出 Prefab Mode，再执行 P10.2。",
                "OK");
            return;
        }

        try
        {
            ConfigureTextureImporter();

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    DraftTexturePath);

            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "找不到 P10.2 Composite Draft Sprite：\n" +
                    DraftTexturePath);
            }

            Texture2D texture = sprite.texture;

            if (texture == null ||
                texture.width != ExpectedPixels ||
                texture.height != ExpectedPixels)
            {
                throw new InvalidOperationException(
                    "P10.2 图片必须是 1024x1024。当前=" +
                    (texture == null
                        ? "<null>"
                        : texture.width + "x" + texture.height) +
                    "。" );
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CrossroadPrefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "找不到 Crossroad_01 Prefab：\n" +
                    CrossroadPrefabPath +
                    "\n请先完成 P10.0 / P10.1。" );
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
                        "P10.2 只接受 16x16 Crossroad。当前=" +
                        template.SizeInCells.x + "x" +
                        template.SizeInCells.y + "。" );
                }

                Transform floorRoot =
                    root.transform.Find("Visual/Floor");

                if (floorRoot == null)
                {
                    throw new InvalidOperationException(
                        "找不到 Visual/Floor。" );
                }

                Transform geometryRoot =
                    root.transform.Find(
                        "Navigation/Colliders/P10_1_Geometry");

                if (geometryRoot == null)
                {
                    throw new InvalidOperationException(
                        "找不到 P10.1 Geometry。请先完成 P10.1。" );
                }

                RemoveKnownFloorVisuals(floorRoot);

                GameObject visual =
                    new GameObject(RuntimeVisualName);

                visual.transform.SetParent(floorRoot, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                SpriteRenderer renderer =
                    visual.AddComponent<SpriteRenderer>();

                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = -10;

                List<string> errors =
                    ValidateLoadedPrefab(root, template, sprite);

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "P10.2 保存前校验失败：\n- " +
                        string.Join("\n- ", errors));
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    CrossroadPrefabPath);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject savedPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CrossroadPrefabPath);

            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log(
                "[P10.2] Crossroad_01 Composite Draft 视觉已接入。\n" +
                "Texture=1024x1024 | PPU64 | Point | Uncompressed | MipMapOff\n" +
                "PrefabVisual=Visual/Floor/" + RuntimeVisualName + "\n" +
                "Transform=Position(0,0,0) Rotation(0,0,0) Scale(1,1,1)\n" +
                "ColliderFromImage=False | P10.1GeometryPreserved=True\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False" );

            EditorUtility.DisplayDialog(
                "P10.2 Visual Ready",
                "Crossroad_01 已接入 1024x1024 工程化视觉草稿。\n\n" +
                "现在双击 Prefab，应看到母图完整铺满 16x16 Cell。\n" +
                "P10.1 的 Collider / Blocked Cells 保持独立。\n\n" +
                "本阶段仍不会随机生成到正式游戏中。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "P10.2 failed",
                "P10.2 已中止。请把 Console 第一条红色错误发给我。",
                "OK");
        }
    }

    [MenuItem(
        MenuRoot + "Validate Composite Draft Visual",
        false,
        2721)]
    private static void ValidateCompositeDraftVisual()
    {
        List<string> errors = new List<string>();

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                DraftTexturePath);

        if (sprite == null)
        {
            errors.Add(
                "找不到 Composite Draft Sprite：" +
                DraftTexturePath);
        }
        else
        {
            Texture2D texture = sprite.texture;

            if (texture == null ||
                texture.width != ExpectedPixels ||
                texture.height != ExpectedPixels)
            {
                errors.Add(
                    "图片应为 1024x1024。" );
            }

            TextureImporter importer =
                AssetImporter.GetAtPath(DraftTexturePath)
                    as TextureImporter;

            if (importer == null)
            {
                errors.Add("无法读取 TextureImporter。" );
            }
            else
            {
                if (importer.textureType !=
                    TextureImporterType.Sprite)
                {
                    errors.Add("Texture Type 不是 Sprite。" );
                }

                if (Mathf.Abs(
                        importer.spritePixelsPerUnit -
                        ExpectedPpu) > 0.001f)
                {
                    errors.Add("PPU 不是 64。" );
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    errors.Add("Filter Mode 不是 Point。" );
                }

                if (importer.mipmapEnabled)
                {
                    errors.Add("Mip Maps 必须关闭。" );
                }

                if (importer.textureCompression !=
                    TextureImporterCompression.Uncompressed)
                {
                    errors.Add("Compression 必须为 None/Uncompressed。" );
                }
            }
        }

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                CrossroadPrefabPath);

        if (prefab == null)
        {
            errors.Add("找不到 Crossroad_01 Prefab。" );
        }
        else
        {
            GameObject root = null;

            try
            {
                root = PrefabUtility.LoadPrefabContents(
                    CrossroadPrefabPath);

                DreamRoomTemplate template =
                    root.GetComponent<DreamRoomTemplate>();

                if (sprite != null)
                {
                    errors.AddRange(
                        ValidateLoadedPrefab(
                            root,
                            template,
                            sprite));
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.2] Crossroad_01 视觉校验失败：\n- " +
                string.Join("\n- ", errors));
            return;
        }

        Debug.Log(
            "[P10.2] Crossroad_01 视觉校验通过。\n" +
            "Image=1024x1024 RGBA | PPU64 | Point | Uncompressed | MipMapOff\n" +
            "FloorTransform=Identity | FloorCollider=0\n" +
            "P10.1Geometry=Preserved | RuntimeIntegration=NotStartedByDesign" );
    }

    private static void ConfigureTextureImporter()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(DraftTexturePath)
                as TextureImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(
                DraftTexturePath,
                ImportAssetOptions.ForceSynchronousImport);

            importer =
                AssetImporter.GetAtPath(DraftTexturePath)
                    as TextureImporter;
        }

        if (importer == null)
        {
            throw new InvalidOperationException(
                "无法取得 TextureImporter：" +
                DraftTexturePath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ExpectedPpu;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;

        TextureImporterSettings settings =
            new TextureImporterSettings();

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment =
            (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static void RemoveKnownFloorVisuals(
        Transform floorRoot)
    {
        string[] knownNames =
        {
            "Floor_Placeholder",
            RuntimeVisualName
        };

        for (int i = 0; i < knownNames.Length; i++)
        {
            Transform child =
                floorRoot.Find(knownNames[i]);

            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    child.gameObject);
            }
        }
    }

    private static List<string> ValidateLoadedPrefab(
        GameObject root,
        DreamRoomTemplate template,
        Sprite expectedSprite)
    {
        List<string> errors = new List<string>();

        if (root == null)
        {
            errors.Add("Prefab root 为 null。" );
            return errors;
        }

        if (template == null)
        {
            errors.Add("缺少 DreamRoomTemplate。" );
            return errors;
        }

        if (template.SizeInCells != ExpectedSize)
        {
            errors.Add(
                "SizeInCells 应为 16x16。" );
        }

        Transform floorRoot =
            root.transform.Find("Visual/Floor");

        if (floorRoot == null)
        {
            errors.Add("找不到 Visual/Floor。" );
            return errors;
        }

        if (floorRoot.Find("Floor_Placeholder") != null)
        {
            errors.Add("Floor_Placeholder 尚未移除。" );
        }

        Transform runtimeVisual =
            floorRoot.Find(RuntimeVisualName);

        if (runtimeVisual == null)
        {
            errors.Add(
                "找不到 " + RuntimeVisualName + "。" );
        }
        else
        {
            if (runtimeVisual.localPosition != Vector3.zero)
            {
                errors.Add("Floor Position 必须为 0,0,0。" );
            }

            if (runtimeVisual.localRotation != Quaternion.identity)
            {
                errors.Add("Floor Rotation 必须为 Identity。" );
            }

            if (runtimeVisual.localScale != Vector3.one)
            {
                errors.Add("Floor Scale 必须为 1,1,1。" );
            }

            SpriteRenderer renderer =
                runtimeVisual.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                errors.Add("CompositeDraft_Runtime 缺少 SpriteRenderer。" );
            }
            else
            {
                if (renderer.sprite != expectedSprite)
                {
                    errors.Add("SpriteRenderer 没有引用预期图片。" );
                }

                if (renderer.sortingOrder != -10)
                {
                    errors.Add("Floor sortingOrder 应为 -10。" );
                }
            }
        }

        Collider2D[] floorColliders =
            floorRoot.GetComponentsInChildren<Collider2D>(true);

        if (floorColliders.Length != 0)
        {
            errors.Add(
                "Visual/Floor 下不应存在 Collider2D。当前=" +
                floorColliders.Length + "。" );
        }

        Transform p101 =
            root.transform.Find(
                "Navigation/Colliders/P10_1_Geometry");

        if (p101 == null)
        {
            errors.Add("P10.1 Geometry 不存在。" );
        }
        else
        {
            BoxCollider2D[] geometryColliders =
                p101.GetComponentsInChildren<BoxCollider2D>(true);

            if (geometryColliders.Length != 13)
            {
                errors.Add(
                    "P10.1 BoxCollider2D 应为 13 个，当前=" +
                    geometryColliders.Length + "。" );
            }
        }

        return errors;
    }
}
