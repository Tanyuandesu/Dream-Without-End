#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 正式主角 PixelLab PNG 的一次性/可重复执行导入工具。
/// Unity 6 (6000.x) compatible.
/// Runtime 不依赖本脚本；它只存在于 Editor 中。
///
/// 特点：
/// - 优先使用标准目录。
/// - 如果你把 animations 放在了别的位置，会自动在 Assets 内寻找
///   同时包含 Breathing_Idle / Walking / Lead_Jab / 推击动画 的目录。
/// - Runtime 继续复用 CharacterAnimationProfile.SetSequence()。
/// </summary>
public static class MainCharacterAnimationImporter
{
    private const string PreferredRoot =
        "Assets/Art/Characters/Player/MainCharacter/animations";

    private const string ProfileFolder =
        "Assets/GeneratedCharacterAnimation";

    private const string ProfilePath =
        ProfileFolder + "/MainCharacter_8Direction.asset";

    private const string IdleFolder = "Breathing_Idle";
    private const string WalkFolder = "Walking";
    private const string AttackFolder = "Lead_Jab";
    private const string SpecialFolder =
        "A_short_restrained_two-handed_shove_forward._Keep";

    private const float IdleFps = 4f;
    private const float WalkFps = 8f;
    private const float AttackFps = 12f;
    private const float SpecialFps = 12f;

    private static readonly (string folder, CharacterFacingDirection direction)[]
        Directions =
        {
            ("south", CharacterFacingDirection.South),
            ("south-east", CharacterFacingDirection.SouthEast),
            ("east", CharacterFacingDirection.East),
            ("north-east", CharacterFacingDirection.NorthEast),
            ("north", CharacterFacingDirection.North),
            ("north-west", CharacterFacingDirection.NorthWest),
            ("west", CharacterFacingDirection.West),
            ("south-west", CharacterFacingDirection.SouthWest),
        };

    [MenuItem(
        "Dream Dungeon/Character Animation/1. Prepare Main Character Textures")]
    public static void PrepareTextures()
    {
        string root = ResolveRootOrShowDialog();
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { root });

        List<string> paths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.StartsWith(root + "/", StringComparison.Ordinal) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Main Character Importer",
                "已经找到动画目录，但里面没有找到 PNG：\n\n" + root,
                "OK");
            return;
        }

        int changed = 0;

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];

                EditorUtility.DisplayProgressBar(
                    "Preparing Main Character Textures",
                    $"{i + 1}/{paths.Count}\n{path}",
                    (float)(i + 1) / paths.Count);

                TextureImporter importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                {
                    Debug.LogWarning(
                        "[MainCharacterAnimationImporter] " +
                        "TextureImporter not found: " + path);
                    continue;
                }

                bool needsReimport = false;

                needsReimport |= SetIfDifferent(
                    importer.textureType,
                    TextureImporterType.Sprite,
                    value => importer.textureType = value);

                needsReimport |= SetIfDifferent(
                    importer.spriteImportMode,
                    SpriteImportMode.Single,
                    value => importer.spriteImportMode = value);

                needsReimport |= SetIfDifferent(
                    importer.spritePixelsPerUnit,
                    64f,
                    value => importer.spritePixelsPerUnit = value);

                TextureImporterSettings spriteSettings =
                    new TextureImporterSettings();

                importer.ReadTextureSettings(spriteSettings);

                if (spriteSettings.spriteMeshType !=
                    SpriteMeshType.FullRect)
                {
                    spriteSettings.spriteMeshType =
                        SpriteMeshType.FullRect;

                    importer.SetTextureSettings(spriteSettings);
                    needsReimport = true;
                }

                needsReimport |= SetIfDifferent(
                    importer.filterMode,
                    FilterMode.Point,
                    value => importer.filterMode = value);

                needsReimport |= SetIfDifferent(
                    importer.wrapMode,
                    TextureWrapMode.Clamp,
                    value => importer.wrapMode = value);

                needsReimport |= SetIfDifferent(
                    importer.textureCompression,
                    TextureImporterCompression.Uncompressed,
                    value => importer.textureCompression = value);

                needsReimport |= SetIfDifferent(
                    importer.maxTextureSize,
                    256,
                    value => importer.maxTextureSize = value);

                needsReimport |= SetIfDifferent(
                    importer.alphaSource,
                    TextureImporterAlphaSource.FromInput,
                    value => importer.alphaSource = value);

                needsReimport |= SetIfDifferent(
                    importer.alphaIsTransparency,
                    true,
                    value => importer.alphaIsTransparency = value);

                needsReimport |= SetIfDifferent(
                    importer.mipmapEnabled,
                    false,
                    value => importer.mipmapEnabled = value);

                if (needsReimport)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Main Character Importer",
            $"完成。\n\n" +
            $"自动识别目录：\n{root}\n\n" +
            $"扫描 PNG：{paths.Count} 张\n" +
            $"实际重新导入：{changed} 张\n\n" +
            "没有处理该目录以外的图片。",
            "OK");

        Debug.Log(
            $"[MainCharacterAnimationImporter] Texture preparation complete. " +
            $"Scanned={paths.Count}, Reimported={changed}, Root={root}");
    }

    [MenuItem(
        "Dream Dungeon/Character Animation/2. Build or Update Main Character Profile")]
    public static void BuildOrUpdateProfile()
    {
        string root = ResolveRootOrShowDialog();
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        EnsureFolder(ProfileFolder);

        CharacterAnimationProfile profile =
            AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(
                ProfilePath);

        if (profile == null)
        {
            profile =
                ScriptableObject.CreateInstance<CharacterAnimationProfile>();

            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        Undo.RecordObject(
            profile,
            "Build Main Character Animation Profile");

        profile.ClearAllSequences();

        profile.ConfigureDirectionRules(
            CharacterAnimationDirectionMode.EightDirections,
            false,
            CharacterFacingDirection.South);

        ImportAnimation(
            root,
            profile,
            CharacterAnimationState.Idle,
            IdleFolder,
            IdleFps,
            true);

        ImportAnimation(
            root,
            profile,
            CharacterAnimationState.Walk,
            WalkFolder,
            WalkFps,
            true);

        ImportAnimation(
            root,
            profile,
            CharacterAnimationState.Attack,
            AttackFolder,
            AttackFps,
            false);

        ImportAnimation(
            root,
            profile,
            CharacterAnimationState.Special,
            SpecialFolder,
            SpecialFps,
            false);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);

        EditorUtility.DisplayDialog(
            "Main Character Importer",
            "正式主角 Profile 已建立/更新：\n\n" +
            ProfilePath +
            "\n\n资源目录：\n" + root +
            "\n\nIdle = Breathing_Idle\n" +
            "Walk = Walking\n" +
            "Attack = Lead_Jab\n" +
            "Special = two-handed shove",
            "OK");
    }

    [MenuItem(
        "Dream Dungeon/Character Animation/3. Validate Main Character Source")]
    public static void ValidateSource()
    {
        string root = ResolveRootOrShowDialog();
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        List<string> problems = new List<string>();
        int totalFrames = 0;

        ValidateAnimation(
            root,
            IdleFolder,
            expectedPerDirection: 4,
            problems,
            ref totalFrames);

        ValidateAnimation(
            root,
            WalkFolder,
            expectedPerDirection: 8,
            problems,
            ref totalFrames);

        ValidateAnimation(
            root,
            AttackFolder,
            expectedPerDirection: 3,
            problems,
            ref totalFrames);

        ValidateAnimation(
            root,
            SpecialFolder,
            expectedPerDirection: 9,
            problems,
            ref totalFrames);

        if (problems.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Main Character Importer",
                $"验证通过。\n\n" +
                $"自动识别目录：\n{root}\n\n" +
                $"共找到 {totalFrames} 张动画帧。\n" +
                "4 套动画 × 8 方向结构完整。",
                "OK");
            return;
        }

        Debug.LogError(
            "[MainCharacterAnimationImporter] Validation failed:\n" +
            string.Join("\n", problems));

        EditorUtility.DisplayDialog(
            "Main Character Importer",
            "发现资源结构问题。\n\n" +
            "识别目录：\n" + root + "\n\n" +
            string.Join("\n", problems.Take(12)) +
            (problems.Count > 12
                ? $"\n\n……另有 {problems.Count - 12} 项，请看 Console。"
                : ""),
            "OK");
    }

    private static string ResolveRootOrShowDialog()
    {
        string root = ResolveRoot();

        if (!string.IsNullOrEmpty(root))
        {
            return root;
        }

        EditorUtility.DisplayDialog(
            "Main Character Importer",
            "找不到正式主角 animations 目录。\n\n" +
            "工具已经自动搜索整个 Assets，但没有找到一个同时包含：\n\n" +
            "• Breathing_Idle\n" +
            "• Walking\n" +
            "• Lead_Jab\n" +
            "• A_short_restrained_two-handed_shove_forward._Keep\n\n" +
            "的目录。\n\n" +
            "请确认这 4 个动画文件夹已经拖进 Unity Project。",
            "OK");

        return null;
    }

    private static string ResolveRoot()
    {
        if (LooksLikeAnimationRoot(PreferredRoot))
        {
            return PreferredRoot;
        }

        // 不要求用户把资源放在固定位置。
        // 找 Breathing_Idle 文件夹，再检查它的兄弟目录是否齐全。
        string[] candidates =
            AssetDatabase.FindAssets(IdleFolder, new[] { "Assets" });

        foreach (string guid in candidates)
        {
            string idlePath =
                AssetDatabase.GUIDToAssetPath(guid);

            if (!AssetDatabase.IsValidFolder(idlePath))
            {
                continue;
            }

            if (!string.Equals(
                    Path.GetFileName(idlePath),
                    IdleFolder,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string parent =
                Path.GetDirectoryName(idlePath)?
                    .Replace("\\", "/");

            if (!string.IsNullOrEmpty(parent) &&
                LooksLikeAnimationRoot(parent))
            {
                return parent;
            }
        }

        return null;
    }

    private static bool LooksLikeAnimationRoot(string root)
    {
        if (string.IsNullOrEmpty(root) ||
            !AssetDatabase.IsValidFolder(root))
        {
            return false;
        }

        return AssetDatabase.IsValidFolder($"{root}/{IdleFolder}") &&
               AssetDatabase.IsValidFolder($"{root}/{WalkFolder}") &&
               AssetDatabase.IsValidFolder($"{root}/{AttackFolder}") &&
               AssetDatabase.IsValidFolder($"{root}/{SpecialFolder}");
    }

    private static void ImportAnimation(
        string root,
        CharacterAnimationProfile profile,
        CharacterAnimationState state,
        string animationFolder,
        float fps,
        bool loop)
    {
        foreach (var entry in Directions)
        {
            string folder =
                $"{root}/{animationFolder}/{entry.folder}";

            Sprite[] frames = LoadFrames(folder);

            if (frames.Length == 0)
            {
                throw new InvalidOperationException(
                    $"没有找到动画帧：{folder}");
            }

            profile.SetSequence(
                state,
                entry.direction,
                frames,
                fps,
                loop);
        }
    }

    private static Sprite[] LoadFrames(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return Array.Empty<Sprite>();
        }

        string[] guids =
            AssetDatabase.FindAssets("t:Sprite", new[] { folder });

        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                Path.GetDirectoryName(path)?
                    .Replace("\\", "/") == folder &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
                AssetDatabase.LoadAssetAtPath<Sprite>(path))
            .Where(sprite => sprite != null)
            .ToArray();
    }

    private static void ValidateAnimation(
        string root,
        string animationFolder,
        int expectedPerDirection,
        List<string> problems,
        ref int totalFrames)
    {
        foreach (var entry in Directions)
        {
            string folder =
                $"{root}/{animationFolder}/{entry.folder}";

            Sprite[] frames = LoadFrames(folder);
            totalFrames += frames.Length;

            if (frames.Length != expectedPerDirection)
            {
                problems.Add(
                    $"{animationFolder}/{entry.folder}: " +
                    $"预期 {expectedPerDirection} 帧，实际 {frames.Length} 帧");
            }
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?
            .Replace("\\", "/");

        string name = Path.GetFileName(folder);

        if (string.IsNullOrEmpty(parent) ||
            string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException(
                "Invalid folder path: " + folder);
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static bool SetIfDifferent<T>(
        T current,
        T desired,
        Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired))
        {
            return false;
        }

        setter(desired);
        return true;
    }

    private static bool SetIfDifferent(
        float current,
        float desired,
        Action<float> setter)
    {
        if (Mathf.Approximately(current, desired))
        {
            return false;
        }

        setter(desired);
        return true;
    }
}
#endif
