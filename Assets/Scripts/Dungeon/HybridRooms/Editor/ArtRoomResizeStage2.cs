using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ArtRoom_01 Stage 2:
/// Preserve the existing 13x21 visual design and re-fit it to 11x18.
///
/// This is intentionally NOT a redesign.
/// The existing four art layers are scaled together from:
/// - source 832x1344 (13x21 @ 64 PPU)
/// - target 704x1152 (11x18 @ 64 PPU)
///
/// X scale = 704 / 832 = 0.8461538.
/// Y scale = 1152 / 1344 = 0.8571429.
/// Relative anisotropy is only about 1.3%, so the old design is preserved
/// while filling the exact 11x18 canvas.
/// Nearest-neighbour resampling is used to avoid introducing new blended
/// colours / alpha values into the pixel-art layers.
///
/// Collision is NOT copied; it is re-authored after visual review.
/// </summary>
public static class ArtRoomResizeStage2
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Production Rooms/ArtRoom Rebuild/" +
        "Stage 2 - Fit Existing Art to 11x18";

    private const int Ppu = 64;

    private const int SourceWidth = 13 * Ppu;
    private const int SourceHeight = 21 * Ppu;

    private const int TargetWidth = 11 * Ppu;
    private const int TargetHeight = 18 * Ppu;

    private const string SourceRoot =
        "Documentation/ArtRoom_01_Legacy13x21";

    private const string RuntimeRoot =
        "Assets/DreamDungeon/Production/Rooms/ArtRoom_01/Art/Runtime";

    private static readonly LayerSpec[] Layers =
    {
        new LayerSpec("Floor"),
        new LayerSpec("Objects"),
        new LayerSpec("Foreground"),
        new LayerSpec("Effects"),
    };

    [MenuItem(MenuPath, false, 2781)]
    private static void FitExistingArt()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Stage 2",
                "请先退出 Play Mode。",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "ArtRoom_01 Stage 2",
            "这一步不会重新设计画室。\n\n" +
            "将旧 13x21 四层整体缩小并做极轻微比例适配到 11x18：\n" +
            "• X：84.615%\n" +
            "• Y：85.714%\n" +
            "• 相对拉伸差约 1.3%\n" +
            "• 四层使用完全相同的变换\n" +
            "• 最近邻采样，不新增模糊颜色\n" +
            "• PPU 最终仍为 64\n\n" +
            "碰撞不会继承，后续重新审图制作。\n\n" +
            "继续？",
            "Fit Existing Art",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        try
        {
            for (int i = 0; i < Layers.Length; i++)
            {
                ProcessLayer(Layers[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < Layers.Length; i++)
            {
                ValidateRuntimeLayer(Layers[i]);
            }

            Debug.Log(
                "[ArtRoom Stage 2] Existing design fitted to 11x18.\n" +
                "Source=13x21 / 832x1344\n" +
                "Target=11x18 / 704x1152\n" +
                "ScaleX=0.8461538 | ScaleY=0.8571429 | RelativeStretch=~1.3%\n" +
                "Resample=NearestNeighbour | PPU=64\n" +
                "CollisionCopied=False | Redesign=False");

            EditorUtility.DisplayDialog(
                "ArtRoom Stage 2 Passed",
                "旧 ArtRoom_01 四层已整体缩小并轻微拉伸适配到 11x18。\n\n" +
                "没有重新设计，没有裁切主要内容。\n" +
                "下一步先审图，再重新制作 blockedCells / Collider。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "ArtRoom Stage 2 Failed",
                "适配失败。请不要继续做碰撞。\n" +
                "把 Console 第一条红色错误发给我。",
                "OK");
        }
    }

    private static void ProcessLayer(
        LayerSpec layer)
    {
        string sourceProjectPath =
            SourceRoot +
            "/Room_ArtRoom_01_" +
            layer.Name +
            "_13x21.png";

        string targetAssetPath =
            RuntimeRoot +
            "/Room_ArtRoom_01_" +
            layer.Name +
            ".png";

        string sourceAbsolute =
            ToAbsolutePath(sourceProjectPath);

        string targetAbsolute =
            ToAbsolutePath(targetAssetPath);

        if (!File.Exists(sourceAbsolute))
        {
            throw new FileNotFoundException(
                "找不到旧 13x21 源图。",
                sourceAbsolute);
        }

        byte[] sourceBytes =
            File.ReadAllBytes(sourceAbsolute);

        Texture2D sourceTexture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false,
                false);

        Texture2D targetTexture = null;

        try
        {
            if (!ImageConversion.LoadImage(
                    sourceTexture,
                    sourceBytes,
                    markNonReadable: false))
            {
                throw new InvalidOperationException(
                    "PNG 解码失败：" +
                    sourceProjectPath);
            }

            if (sourceTexture.width != SourceWidth ||
                sourceTexture.height != SourceHeight)
            {
                throw new InvalidOperationException(
                    layer.Name +
                    " 源图尺寸不是 832x1344，实际=" +
                    sourceTexture.width +
                    "x" +
                    sourceTexture.height);
            }

            Color32[] sourcePixels =
                sourceTexture.GetPixels32();

            Color32[] scaledPixels =
                new Color32[
                    TargetWidth *
                    TargetHeight];

            // 最近邻缩放：
            // 四层使用完全相同的采样规则，保证坐标关系不会漂移。
            // 用目标像素中心映射回源图，避免简单 floor 造成单侧偏移。
            for (int y = 0;
                 y < TargetHeight;
                 y++)
            {
                float sourceY =
                    ((y + 0.5f) *
                     SourceHeight /
                     TargetHeight) -
                    0.5f;

                int nearestY =
                    Mathf.Clamp(
                        Mathf.RoundToInt(sourceY),
                        0,
                        SourceHeight - 1);

                int targetRow =
                    y * TargetWidth;

                int sourceRow =
                    nearestY * SourceWidth;

                for (int x = 0;
                     x < TargetWidth;
                     x++)
                {
                    float sourceX =
                        ((x + 0.5f) *
                         SourceWidth /
                         TargetWidth) -
                        0.5f;

                    int nearestX =
                        Mathf.Clamp(
                            Mathf.RoundToInt(sourceX),
                            0,
                            SourceWidth - 1);

                    scaledPixels[
                        targetRow + x] =
                        sourcePixels[
                            sourceRow + nearestX];
                }
            }

            targetTexture =
                new Texture2D(
                    TargetWidth,
                    TargetHeight,
                    TextureFormat.RGBA32,
                    false,
                    false);

            targetTexture.name =
                "Room_ArtRoom_01_" +
                layer.Name;

            targetTexture.SetPixels32(
                scaledPixels);
            targetTexture.Apply(
                false,
                false);

            File.WriteAllBytes(
                targetAbsolute,
                targetTexture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                sourceTexture);

            if (targetTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    targetTexture);
            }
        }

        AssetDatabase.ImportAsset(
            targetAssetPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        ConfigureRuntimeImporter(
            targetAssetPath);
    }

    private static void ConfigureRuntimeImporter(
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
            Ppu;

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

    private static void ValidateRuntimeLayer(
        LayerSpec layer)
    {
        string path =
            RuntimeRoot +
            "/Room_ArtRoom_01_" +
            layer.Name +
            ".png";

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                path);

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                path);

        if (texture == null ||
            sprite == null)
        {
            throw new InvalidOperationException(
                "适配后无法读取 " +
                layer.Name +
                " Runtime Sprite。");
        }

        if (texture.width != TargetWidth ||
            texture.height != TargetHeight)
        {
            throw new InvalidOperationException(
                layer.Name +
                " 目标尺寸异常：" +
                texture.width +
                "x" +
                texture.height);
        }

        if (Mathf.Abs(
                sprite.pixelsPerUnit -
                Ppu) > 0.001f)
        {
            throw new InvalidOperationException(
                layer.Name +
                " PPU 不是 64。");
        }
    }

    private static string ToAbsolutePath(
        string projectPath)
    {
        string projectRoot =
            Directory.GetParent(
                Application.dataPath)
                .FullName;

        return Path.Combine(
            projectRoot,
            projectPath.Replace(
                '/',
                Path.DirectorySeparatorChar));
    }

    private readonly struct LayerSpec
    {
        public readonly string Name;

        public LayerSpec(
            string name)
        {
            Name = name;
        }
    }
}
