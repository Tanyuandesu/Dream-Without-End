
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DreamRoomMusicRoomArtReferenceRepairP1011
{
    private const string PrefabPath =
        "Assets/DreamDungeon/Production/Rooms/MusicRoom_01/Room_MusicRoom_01.prefab";

    private const string ArtRoot =
        "Assets/DreamDungeon/Production/Rooms/MusicRoom_01/Art/Runtime/";

    private sealed class LayerSpec
    {
        public string Name;
        public string TexturePath;
        public string TransformPath;

        public LayerSpec(string name, string texturePath, string transformPath)
        {
            Name = name;
            TexturePath = texturePath;
            TransformPath = transformPath;
        }
    }

    private static readonly LayerSpec[] Layers =
    {
        new LayerSpec("Floor", ArtRoot + "Room_MusicRoom_01_Floor.png", "Visual/Floor/Floor_Runtime"),
        new LayerSpec("Objects", ArtRoot + "Room_MusicRoom_01_Objects.png", "Visual/Objects/Objects_Runtime"),
        new LayerSpec("Foreground", ArtRoot + "Room_MusicRoom_01_Foreground.png", "Visual/Foreground/Foreground_Runtime"),
        new LayerSpec("Effects", ArtRoot + "Room_MusicRoom_01_Effects.png", "Visual/Effects/Effects_Runtime"),
    };

    [MenuItem("Tools/Dream Dungeon/Production Rooms/P10.11 MusicRoom Art Repair/1. Repair Import Settings + Sprite References")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[P10.11] 请先退出 Play Mode。");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError("[P10.11] 找不到 MusicRoom Prefab：\n" + PrefabPath);
            return;
        }

        foreach (var layer in Layers)
        {
            var importer = AssetImporter.GetAtPath(layer.TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[P10.11] 找不到 PNG 或 TextureImporter：\n" + layer.TexturePath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            // Unity 6: spriteMeshType lives on TextureImporterSettings, not TextureImporter.
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);

            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var layer in Layers)
            {
                var t = root.transform.Find(layer.TransformPath);
                if (t == null)
                    throw new InvalidOperationException("找不到 Prefab 节点：" + layer.TransformPath);

                var sr = t.GetComponent<SpriteRenderer>();
                if (sr == null)
                    throw new InvalidOperationException("节点缺少 SpriteRenderer：" + layer.TransformPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(layer.TexturePath);
                if (sprite == null)
                    throw new InvalidOperationException("PNG 已导入但没有可用的 Single Sprite：" + layer.TexturePath);

                sr.sprite = sprite;
                sr.enabled = true;

                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;

                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(t);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[P10.11] MusicRoom_01 美术引用修复完成。\n" +
            "Import=Sprite Single | PPU64 | Point | FullRect | Uncompressed | MipMapOff | Clamp\n" +
            "Rebound=Floor/Objects/Foreground/Effects\n" +
            "GeometryChanged=False | SocketsChanged=False | ProductionMainChanged=False | RuntimeCoreChanged=False");
    }

    [MenuItem("Tools/Dream Dungeon/Production Rooms/P10.11 MusicRoom Art Repair/2. Validate MusicRoom Art References")]
    public static void Validate()
    {
        var errors = new List<string>();

        foreach (var layer in Layers)
        {
            var importer = AssetImporter.GetAtPath(layer.TexturePath) as TextureImporter;
            if (importer == null)
            {
                errors.Add(layer.Name + ": TextureImporter missing");
                continue;
            }

            if (importer.textureType != TextureImporterType.Sprite)
                errors.Add(layer.Name + ": TextureType != Sprite");

            if (importer.spriteImportMode != SpriteImportMode.Single)
                errors.Add(layer.Name + ": SpriteMode != Single");

            if (Mathf.Abs(importer.spritePixelsPerUnit - 64f) > 0.01f)
                errors.Add(layer.Name + ": PPU != 64");

            if (importer.filterMode != FilterMode.Point)
                errors.Add(layer.Name + ": Filter != Point");

            if (importer.mipmapEnabled)
                errors.Add(layer.Name + ": MipMap must be Off");

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                errors.Add(layer.Name + ": Compression != Uncompressed");

            if (importer.wrapMode != TextureWrapMode.Clamp)
                errors.Add(layer.Name + ": Wrap != Clamp");

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
                errors.Add(layer.Name + ": MeshType != FullRect");

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(layer.TexturePath);
            if (sprite == null)
                errors.Add(layer.Name + ": Single Sprite missing after import");
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            errors.Add("MusicRoom Prefab missing");
        }
        else
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var layer in Layers)
                {
                    var t = root.transform.Find(layer.TransformPath);
                    if (t == null)
                    {
                        errors.Add(layer.Name + ": node missing (" + layer.TransformPath + ")");
                        continue;
                    }

                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr == null)
                    {
                        errors.Add(layer.Name + ": SpriteRenderer missing");
                        continue;
                    }

                    if (sr.sprite == null)
                        errors.Add(layer.Name + ": SpriteRenderer.sprite is Missing/None");

                    if (t.localPosition != Vector3.zero ||
                        t.localRotation != Quaternion.identity ||
                        t.localScale != Vector3.one)
                    {
                        errors.Add(layer.Name + ": Runtime transform is not Identity");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "[P10.11] MusicRoom_01 美术引用校验失败：\n- " +
                string.Join("\n- ", errors));
        }

        Debug.Log(
            "[P10.11] MusicRoom_01 美术引用校验通过。\n" +
            "Layers=Floor/Objects/Foreground/Effects\n" +
            "Import=Sprite Single | PPU64 | Point | FullRect | Uncompressed | MipMapOff | Clamp\n" +
            "SpriteReferences=Valid\n" +
            "Transforms=Identity\n" +
            "GeometryChanged=False | SocketsChanged=False | ProductionMainChanged=False | RuntimeCoreChanged=False");
    }
}
#endif
