#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CA1 临时角色动画安装器。
///
/// 负责：
/// 1. 设置临时 PNG 的像素导入参数。
/// 2. 生成玩家火柴人与敌人火柴猫的动画 Profile。
/// 3. 把 Profile 写入当前 GameScene 的 PlayerSpawner 与 EnemySpawner。
///
/// 不修改玩家移动、敌人 AI、A*、碰撞或房间系统。
/// </summary>
public static class TemporaryCharacterAnimationInstaller
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Character Animation/";

    private const string TemporaryRoot =
        "Assets/Art/Characters/Temporary";

    private const string GeneratedRoot =
        "Assets/GeneratedCharacterAnimation";

    private const string PlayerProfilePath =
        GeneratedRoot +
        "/CA1_TemporaryStickHuman_8Direction.asset";

    private const string EnemyProfilePath =
        GeneratedRoot +
        "/CA1_TemporaryStickCat_8Direction.asset";

    private static readonly CharacterFacingDirection[]
        AuthoredDirections =
        {
            CharacterFacingDirection.South,
            CharacterFacingDirection.SouthEast,
            CharacterFacingDirection.East,
            CharacterFacingDirection.NorthEast,
            CharacterFacingDirection.North
        };

    private static readonly string[] DirectionTokens =
    {
        "S",
        "SE",
        "E",
        "NE",
        "N"
    };

    [MenuItem(
        MenuRoot +
        "CA1 Install Temporary Stick Characters")]
    public static void InstallTemporaryCharacters()
    {
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);

        EnsureGeneratedFolder();
        ConfigureAllTemporaryTextures();

        CharacterAnimationProfile playerProfile =
            CreateOrUpdateProfile(
                PlayerProfilePath,
                "StickHuman",
                "TempStickHuman",
                8f);

        CharacterAnimationProfile enemyProfile =
            CreateOrUpdateProfile(
                EnemyProfilePath,
                "StickCat",
                "TempStickCat",
                7f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool assigned = AssignProfilesToOpenScene(
            playerProfile,
            enemyProfile);

        if (!assigned)
        {
            Debug.LogError(
                "[CA1] Profile 已生成，但当前场景找不到 " +
                "PlayerSpawner 或 EnemySpawner。" +
                "请打开 Assets/Scenes/GameScene.unity 后再次执行。" );

            return;
        }

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene());

        Selection.activeObject = playerProfile;

        Debug.Log(
            "[CA1] 临时像素火柴人与火柴猫已安装。\n" +
            "玩家：八方向，左侧使用水平镜像。\n" +
            "敌人：根据实际位移判断方向，当前使用同一火柴猫 Profile。\n" +
            "请保存 GameScene，然后进入 Play Mode 测试。" );
    }

    [MenuItem(
        MenuRoot +
        "CA1 Validate Temporary Setup")]
    public static void ValidateTemporarySetup()
    {
        List<string> errors = new List<string>();

        CharacterAnimationProfile playerProfile =
            AssetDatabase.LoadAssetAtPath<
                CharacterAnimationProfile>(
                PlayerProfilePath);

        CharacterAnimationProfile enemyProfile =
            AssetDatabase.LoadAssetAtPath<
                CharacterAnimationProfile>(
                EnemyProfilePath);

        if (playerProfile == null)
        {
            errors.Add("玩家动画 Profile 不存在。");
        }

        if (enemyProfile == null)
        {
            errors.Add("敌人动画 Profile 不存在。");
        }

        PlayerSpawner playerSpawner =
            Object.FindAnyObjectByType<PlayerSpawner>();

        EnemySpawner enemySpawner =
            Object.FindAnyObjectByType<EnemySpawner>();

        if (playerSpawner == null)
        {
            errors.Add("当前场景找不到 PlayerSpawner。");
        }
        else
        {
            SerializedObject serializedPlayer =
                new SerializedObject(playerSpawner);

            SerializedProperty playerProperty =
                serializedPlayer.FindProperty(
                    "animationProfile");

            if (playerProperty == null ||
                playerProperty.objectReferenceValue == null)
            {
                errors.Add(
                    "PlayerSpawner 尚未绑定动画 Profile。");
            }
        }

        if (enemySpawner == null)
        {
            errors.Add("当前场景找不到 EnemySpawner。");
        }
        else
        {
            SerializedObject serializedEnemy =
                new SerializedObject(enemySpawner);

            SerializedProperty enemyProperty =
                serializedEnemy.FindProperty(
                    "defaultAnimationProfile");

            if (enemyProperty == null ||
                enemyProperty.objectReferenceValue == null)
            {
                errors.Add(
                    "EnemySpawner 尚未绑定默认动画 Profile。");
            }
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[CA1] 验证失败：\n- " +
                string.Join("\n- ", errors));

            return;
        }

        Debug.Log(
            "[CA1] 验证通过。" +
            " Profile、场景引用和临时 Sprite 均已建立。" );
    }

    private static CharacterAnimationProfile
        CreateOrUpdateProfile(
            string profilePath,
            string characterFolder,
            string filePrefix,
            float walkFramesPerSecond)
    {
        CharacterAnimationProfile profile =
            AssetDatabase.LoadAssetAtPath<
                CharacterAnimationProfile>(
                profilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<
                CharacterAnimationProfile>();

            AssetDatabase.CreateAsset(
                profile,
                profilePath);
        }

        profile.ConfigureDirectionRules(
            CharacterAnimationDirectionMode.EightDirections,
            true,
            CharacterFacingDirection.South);

        profile.ClearAllSequences();

        for (int i = 0;
             i < AuthoredDirections.Length;
             i++)
        {
            CharacterFacingDirection direction =
                AuthoredDirections[i];

            string token = DirectionTokens[i];

            Sprite idleSprite = LoadSprite(
                TemporaryRoot +
                "/" + characterFolder +
                "/" + filePrefix +
                "_Idle_" + token + ".png");

            profile.SetSequence(
                CharacterAnimationState.Idle,
                direction,
                new Sprite[] { idleSprite },
                1f,
                true);

            Sprite[] walkFrames = new Sprite[4];

            for (int frame = 0;
                 frame < walkFrames.Length;
                 frame++)
            {
                walkFrames[frame] = LoadSprite(
                    TemporaryRoot +
                    "/" + characterFolder +
                    "/" + filePrefix +
                    "_Walk_" + token +
                    "_" + frame + ".png");
            }

            profile.SetSequence(
                CharacterAnimationState.Walk,
                direction,
                walkFrames,
                walkFramesPerSecond,
                true);
        }

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                assetPath);

        if (sprite == null)
        {
            throw new System.InvalidOperationException(
                "[CA1] 无法读取 Sprite：" + assetPath);
        }

        return sprite;
    }

    private static void ConfigureAllTemporaryTextures()
    {
        string[] textureGuids =
            AssetDatabase.FindAssets(
                "t:Texture2D",
                new string[] { TemporaryRoot });

        for (int i = 0;
             i < textureGuids.Length;
             i++)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    textureGuids[i]);

            ConfigureTexture(assetPath);
        }
    }

    private static void ConfigureTexture(string assetPath)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath)
            as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings =
            new TextureImporterSettings();

        importer.ReadTextureSettings(settings);

        settings.spriteMeshType =
            SpriteMeshType.FullRect;

        settings.spriteGenerateFallbackPhysicsShape = false;

        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static bool AssignProfilesToOpenScene(
        CharacterAnimationProfile playerProfile,
        CharacterAnimationProfile enemyProfile)
    {
        PlayerSpawner playerSpawner =
            Object.FindAnyObjectByType<PlayerSpawner>();

        EnemySpawner enemySpawner =
            Object.FindAnyObjectByType<EnemySpawner>();

        if (playerSpawner == null || enemySpawner == null)
        {
            return false;
        }

        SerializedObject serializedPlayer =
            new SerializedObject(playerSpawner);

        SerializedProperty playerProperty =
            serializedPlayer.FindProperty(
                "animationProfile");

        playerProperty.objectReferenceValue =
            playerProfile;

        serializedPlayer.ApplyModifiedProperties();

        SerializedObject serializedEnemy =
            new SerializedObject(enemySpawner);

        SerializedProperty defaultProperty =
            serializedEnemy.FindProperty(
                "defaultAnimationProfile");

        defaultProperty.objectReferenceValue =
            enemyProfile;

        SerializedProperty profilesProperty =
            serializedEnemy.FindProperty(
                "animationProfiles");

        profilesProperty.arraySize = 1;
        profilesProperty
            .GetArrayElementAtIndex(0)
            .objectReferenceValue = enemyProfile;

        serializedEnemy.ApplyModifiedProperties();

        return true;
    }

    private static void EnsureGeneratedFolder()
    {
        if (AssetDatabase.IsValidFolder(GeneratedRoot))
        {
            return;
        }

        AssetDatabase.CreateFolder(
            "Assets",
            "GeneratedCharacterAnimation");
    }
}
#endif
