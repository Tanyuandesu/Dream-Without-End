#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 主角手感快速调参窗口。
/// 只改现有配置：
/// - PlayerSpawner.moveSpeed
/// - MainCharacter_8Direction 的 Walk FPS（一次同步 8 个方向）
/// - CameraManager.orthographicSize
///
/// 不新增 Runtime 逻辑。
/// </summary>
public sealed class MainCharacterTuningWindow : EditorWindow
{
    private const string ProfilePath =
        "Assets/GeneratedCharacterAnimation/MainCharacter_8Direction.asset";

    private static readonly CharacterFacingDirection[] Directions =
    {
        CharacterFacingDirection.South,
        CharacterFacingDirection.SouthEast,
        CharacterFacingDirection.East,
        CharacterFacingDirection.NorthEast,
        CharacterFacingDirection.North,
        CharacterFacingDirection.NorthWest,
        CharacterFacingDirection.West,
        CharacterFacingDirection.SouthWest,
    };

    private CharacterAnimationProfile profile;
    private PlayerSpawner playerSpawner;
    private CameraManager cameraManager;

    private float moveSpeed = 4.3f;
    private float walkFps = 12f;
    private float cameraSize = 6.5f;

    [MenuItem("Dream Dungeon/Character Animation/Open Main Character Tuner")]
    public static void Open()
    {
        MainCharacterTuningWindow window =
            GetWindow<MainCharacterTuningWindow>(
                "Main Character Tuner");

        window.minSize = new Vector2(420f, 310f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshReferences();
        ReadCurrentValues();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "主角手感快速调参",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "这里不改动画架构，只批量修改现有配置。" +
            "Walk FPS 会一次同步 8 个方向。",
            MessageType.Info);

        EditorGUILayout.Space(6);

        DrawReferenceStatus();

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "移动",
            EditorStyles.boldLabel);

        moveSpeed = EditorGUILayout.FloatField(
            "Move Speed",
            moveSpeed);

        using (new EditorGUI.DisabledScope(playerSpawner == null))
        {
            if (GUILayout.Button("应用 Move Speed"))
            {
                ApplyMoveSpeed();
            }
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "走路动画",
            EditorStyles.boldLabel);

        walkFps = EditorGUILayout.FloatField(
            "Walk FPS（8方向统一）",
            walkFps);

        using (new EditorGUI.DisabledScope(profile == null))
        {
            if (GUILayout.Button("一键应用 Walk FPS 到 8 个方向"))
            {
                ApplyWalkFps();
            }
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "视角",
            EditorStyles.boldLabel);

        cameraSize = EditorGUILayout.FloatField(
            "Orthographic Size",
            cameraSize);

        using (new EditorGUI.DisabledScope(cameraManager == null))
        {
            if (GUILayout.Button("应用相机视角"))
            {
                ApplyCameraSize();
            }
        }

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(
            playerSpawner == null ||
            profile == null ||
            cameraManager == null))
        {
            if (GUILayout.Button(
                    "一次应用以上 3 项",
                    GUILayout.Height(32)))
            {
                ApplyMoveSpeed();
                ApplyWalkFps();
                ApplyCameraSize();
            }
        }

        EditorGUILayout.Space(8);

        if (GUILayout.Button("重新读取当前工程数值"))
        {
            RefreshReferences();
            ReadCurrentValues();
            Repaint();
        }
    }

    private void DrawReferenceStatus()
    {
        EditorGUILayout.LabelField(
            "检测状态",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "Main Character Profile",
            profile != null ? "找到" : "未找到");

        EditorGUILayout.LabelField(
            "PlayerSpawner",
            playerSpawner != null ? "找到" : "未找到");

        EditorGUILayout.LabelField(
            "CameraManager",
            cameraManager != null ? "找到" : "未找到");
    }

    private void RefreshReferences()
    {
        profile =
            AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(
                ProfilePath);

        playerSpawner =
            FindFirstObjectByType<PlayerSpawner>(
                FindObjectsInactive.Include);

        cameraManager =
            FindFirstObjectByType<CameraManager>(
                FindObjectsInactive.Include);
    }

    private void ReadCurrentValues()
    {
        if (playerSpawner != null)
        {
            SerializedObject so =
                new SerializedObject(playerSpawner);

            SerializedProperty p =
                so.FindProperty("moveSpeed");

            if (p != null)
            {
                moveSpeed = p.floatValue;
            }
        }

        if (profile != null)
        {
            bool unusedFlip;
            SpriteAnimationSequence sequence =
                profile.GetSequence(
                    CharacterAnimationState.Walk,
                    CharacterFacingDirection.South,
                    out unusedFlip);

            if (sequence != null)
            {
                walkFps = sequence.FramesPerSecond;
            }
        }

        if (cameraManager != null)
        {
            SerializedObject so =
                new SerializedObject(cameraManager);

            SerializedProperty p =
                so.FindProperty("orthographicSize");

            if (p != null)
            {
                cameraSize = p.floatValue;
            }
        }
    }

    private void ApplyMoveSpeed()
    {
        if (playerSpawner == null)
        {
            return;
        }

        moveSpeed = Mathf.Max(0.1f, moveSpeed);

        Undo.RecordObject(
            playerSpawner,
            "Tune Player Move Speed");

        SerializedObject so =
            new SerializedObject(playerSpawner);

        SerializedProperty p =
            so.FindProperty("moveSpeed");

        if (p == null)
        {
            Debug.LogError(
                "[MainCharacterTuningWindow] " +
                "PlayerSpawner.moveSpeed not found.");
            return;
        }

        p.floatValue = moveSpeed;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(playerSpawner);
        MarkSceneDirty();

        Debug.Log(
            $"[MainCharacterTuningWindow] Move Speed = {moveSpeed:0.##}");
    }

    private void ApplyWalkFps()
    {
        if (profile == null)
        {
            return;
        }

        walkFps = Mathf.Max(0.01f, walkFps);

        Undo.RecordObject(
            profile,
            "Tune Main Character Walk FPS");

        foreach (CharacterFacingDirection direction in Directions)
        {
            bool unusedFlip;

            SpriteAnimationSequence sequence =
                profile.GetSequence(
                    CharacterAnimationState.Walk,
                    direction,
                    out unusedFlip);

            if (sequence == null ||
                !sequence.HasFrames)
            {
                Debug.LogWarning(
                    "[MainCharacterTuningWindow] " +
                    $"Walk sequence missing: {direction}");
                continue;
            }

            Sprite[] frames =
                new Sprite[sequence.FrameCount];

            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = sequence.GetFrame(i);
            }

            profile.SetSequence(
                CharacterAnimationState.Walk,
                direction,
                frames,
                walkFps,
                sequence.Loop);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[MainCharacterTuningWindow] " +
            $"Walk FPS = {walkFps:0.##} for all 8 directions.");
    }

    private void ApplyCameraSize()
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraSize = Mathf.Max(1f, cameraSize);

        Undo.RecordObject(
            cameraManager,
            "Tune Camera Orthographic Size");

        SerializedObject so =
            new SerializedObject(cameraManager);

        SerializedProperty p =
            so.FindProperty("orthographicSize");

        if (p == null)
        {
            Debug.LogError(
                "[MainCharacterTuningWindow] " +
                "CameraManager.orthographicSize not found.");
            return;
        }

        p.floatValue = cameraSize;
        so.ApplyModifiedProperties();

        // 同步当前 Scene 中实际 Camera，便于不用 Play 就看到取景变化。
        Camera sceneCamera =
            cameraManager.GetComponent<Camera>();

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera != null)
        {
            Undo.RecordObject(
                sceneCamera,
                "Tune Camera Orthographic Size");

            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = cameraSize;
            EditorUtility.SetDirty(sceneCamera);
        }

        EditorUtility.SetDirty(cameraManager);
        MarkSceneDirty();

        Debug.Log(
            $"[MainCharacterTuningWindow] " +
            $"Camera Orthographic Size = {cameraSize:0.##}");
    }

    private static void MarkSceneDirty()
    {
        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());
        }
    }
}
#endif
