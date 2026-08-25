using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GenerateDreamBackgroundProgressionProfile
{
    private const string Folder =
        "Assets/GeneratedBackgroundSettings";

    private const string AssetPath =
        Folder + "/DreamBackgroundProgressionProfile.asset";

    [MenuItem(
        "Tools/Dream Dungeon/Generate Background Progression Profile")]
    public static void Generate()
    {
        EnsureFolder();

        DreamBackgroundProgressionProfile profile =
            AssetDatabase.LoadAssetAtPath<
                DreamBackgroundProgressionProfile>(AssetPath);

        if (profile == null)
        {
            profile =
                ScriptableObject.CreateInstance<
                    DreamBackgroundProgressionProfile>();

            AssetDatabase.CreateAsset(profile, AssetPath);
        }

        profile.ConfigureForEditor(
            new List<DreamBackgroundStageSettings>
            {
                CreateStage(
                    0, 0.90f,
                    0.65f, 0.75f, 0.70f,
                    0.45f, 0.85f,
                    0.25f, 0.80f,
                    1.5f),

                CreateStage(
                    2, 0.95f,
                    0.85f, 0.90f, 0.90f,
                    0.70f, 0.95f,
                    0.50f, 0.90f,
                    2.0f),

                CreateStage(
                    4, 1.00f,
                    1.05f, 1.10f, 1.10f,
                    1.00f, 1.05f,
                    0.80f, 1.00f,
                    2.5f),

                CreateStage(
                    6, 1.05f,
                    1.25f, 1.25f, 1.25f,
                    1.25f, 1.15f,
                    1.20f, 1.10f,
                    3.0f)
            });

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);

        Debug.Log(
            "Background Progression Profile created: " +
            AssetPath);
    }

    private static DreamBackgroundStageSettings CreateStage(
        int minimumItems,
        float brightness,
        float fogAlpha,
        float fogSpeed,
        float fogAmplitude,
        float dustEmission,
        float dustSize,
        float glowEmission,
        float glowSize,
        float transitionDuration)
    {
        return new DreamBackgroundStageSettings
        {
            minimumCollectedItems = minimumItems,
            backgroundBrightness = brightness,
            fogAlphaMultiplier = fogAlpha,
            fogSpeedMultiplier = fogSpeed,
            fogAmplitudeMultiplier = fogAmplitude,
            dustEmissionMultiplier = dustEmission,
            dustSizeMultiplier = dustSize,
            glowEmissionMultiplier = glowEmission,
            glowSizeMultiplier = glowSize,
            transitionDuration = transitionDuration
        };
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "GeneratedBackgroundSettings");
        }
    }
}
