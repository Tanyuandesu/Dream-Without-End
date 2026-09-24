#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CB11CombatTuningTool
{
    private const float RecommendedPostKnockbackPause = 0.55f;

    [MenuItem("Dream Dungeon/Combat/CB11 Apply Recommended Baseline")]
    public static void ApplyRecommendedBaseline()
    {
        int enemyCount = ApplyEnemyBaselines();
        bool playerUpdated = ApplyPlayerBloodShotBaseline();

        AssetDatabase.SaveAssets();

        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());
        }

        EditorUtility.DisplayDialog(
            "CB11 Combat Baseline",
            $"完成。\n\n" +
            $"EnemyDefinition 更新：{enemyCount}\n" +
            $"PlayerSpawner 血弹参数：{(playerUpdated ? "已更新" : "未找到 PlayerSpawner")}\n\n" +
            "A击退落地后停顿 = 0.55s\n" +
            "Blood Shot = 10 HP / 6伤害 / 0.60击退 / 0.30s眩晕",
            "OK");
    }

    private static int ApplyEnemyBaselines()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemyDefinition",
            new[] { "Assets/GeneratedEnemySettings" });

        int updated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);

            if (definition == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(definition);

            SetFloat(so, "postKnockbackPauseDuration", RecommendedPostKnockbackPause);
            SetFloat(so, "bloodShotDisplacementMultiplier", 1f);
            SetFloat(so, "bloodShotStunMultiplier", 1f);
            SetBool(so, "cb11BloodShotResponseInitialized", true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            updated++;
        }

        return updated;
    }

    private static bool ApplyPlayerBloodShotBaseline()
    {
        PlayerSpawner spawner = Object.FindObjectsByType<PlayerSpawner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();

        if (spawner == null)
        {
            return false;
        }

        Undo.RecordObject(spawner, "Apply CB11 Combat Baseline");
        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty blood = so.FindProperty("bloodShotSettings");

        if (blood == null)
        {
            return false;
        }

        SetFloat(blood, "holdThreshold", 0.32f);
        SetFloat(blood, "healthCost", 10f);
        SetFloat(blood, "minimumRemainingHealth", 1f);
        SetFloat(blood, "projectileSpeed", 9f);
        SetFloat(blood, "projectileLifetime", 1.4f);
        SetFloat(blood, "projectileRadius", 0.12f);
        SetFloat(blood, "spawnOffset", 0.45f);
        SetFloat(blood, "projectileVisualSize", 0.24f);
        SetFloat(blood, "damage", 6f);
        SetFloat(blood, "displacementDistance", 0.60f);
        SetFloat(blood, "displacementDuration", 0.12f);
        SetFloat(blood, "stunDuration", 0.30f);
        SetFloat(blood, "cooldownDuration", 0.75f);
        SetFloat(blood, "afterlagDuration", 0.25f);
        SetFloat(blood, "afterlagMovementMultiplier", 0.35f);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);
        return true;
    }

    private static void SetFloat(
        SerializedObject so,
        string name,
        float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetBool(
        SerializedObject so,
        string name,
        bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetFloat(
        SerializedProperty parent,
        string name,
        float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }
}
#endif
