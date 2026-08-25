using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 建立七個測試 ItemDefinition、Catalog 與 SpawnPolicy。
/// 已存在的資產只補缺項，不覆蓋人工配置的文本、貼圖或 Prefab。
/// </summary>
public static class GenerateTestItemAssets
{
    private const string Folder =
        "Assets/GeneratedTestItems";

    private const int RequiredTestItemCount = 7;

    [MenuItem(
        "Tools/Dream Dungeon/Generate Test Item Assets")]
    public static void Generate()
    {
        EnsureFolder();

        ItemDefinition first =
            GetOrCreateItem(
                "Item_FirstMemory.asset",
                "first_memory",
                "First Memory",
                "The first guaranteed progression item.",
                new Color(0.15f, 0.95f, 1f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_1" });

        ItemDefinition second =
            GetOrCreateItem(
                "Item_SecondMemory.asset",
                "second_memory",
                "Second Memory",
                "The second progression memory.",
                new Color(0.75f, 0.35f, 1f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_2" });

        ItemDefinition third =
            GetOrCreateItem(
                "Item_ThirdMemory.asset",
                "third_memory",
                "Third Memory",
                "The third progression memory.",
                new Color(1f, 0.55f, 0.15f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_3" });

        ItemDefinition fourth =
            GetOrCreateItem(
                "Item_FourthMemory.asset",
                "fourth_memory",
                "Fourth Memory",
                "The fourth progression memory.",
                new Color(0.25f, 0.9f, 0.45f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_4" });

        ItemDefinition fifth =
            GetOrCreateItem(
                "Item_FifthMemory.asset",
                "fifth_memory",
                "Fifth Memory",
                "The fifth progression memory.",
                new Color(0.95f, 0.25f, 0.55f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_5" });

        ItemDefinition sixth =
            GetOrCreateItem(
                "Item_SixthMemory.asset",
                "sixth_memory",
                "Sixth Memory",
                "The sixth progression memory.",
                new Color(0.95f, 0.9f, 0.2f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_6" });

        ItemDefinition seventh =
            GetOrCreateItem(
                "Item_SeventhMemory.asset",
                "seventh_memory",
                "Seventh Memory",
                "The seventh progression memory.",
                new Color(0.95f, 0.95f, 1f, 1f),
                1,
                true,
                1,
                new[] { "memory", "progression", "tier_7" });

        ItemCatalog catalog =
            GetOrCreate<ItemCatalog>(
                Folder + "/ItemCatalog.asset",
                out bool catalogCreated);

        List<ItemDefinition> laterItems =
            new List<ItemDefinition>
            {
                second,
                third,
                fourth,
                fifth,
                sixth,
                seventh
            };

        if (catalogCreated)
        {
            catalog.ConfigureForEditor(
                first,
                laterItems);

            EditorUtility.SetDirty(catalog);
        }
        else if (catalog.EnsureContainsForEditor(
                     first,
                     laterItems))
        {
            EditorUtility.SetDirty(catalog);
        }

        ItemSpawnPolicy policy =
            GetOrCreate<ItemSpawnPolicy>(
                Folder + "/ItemSpawnPolicy.asset",
                out bool policyCreated);

        if (policyCreated)
        {
            policy.ConfigureForEditor(
                2,
                true,
                0.20f,
                0.12f,
                0.85f,
                1);

            EditorUtility.SetDirty(policy);
        }

        AssignToSceneItemManager(
            catalog,
            policy);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);

        LogValidationResult(catalog);
    }

    private static ItemDefinition GetOrCreateItem(
        string fileName,
        string itemId,
        string displayName,
        string description,
        Color fallbackColor,
        int progressionValue,
        bool uniqueInRun,
        int spawnWeight,
        string[] tags)
    {
        ItemDefinition definition =
            GetOrCreate<ItemDefinition>(
                Folder + "/" + fileName,
                out bool created);

        if (!created)
        {
            return definition;
        }

        definition.ConfigureForEditor(
            itemId,
            displayName,
            description,
            fallbackColor,
            progressionValue,
            uniqueInRun,
            spawnWeight,
            tags);

        EditorUtility.SetDirty(definition);

        return definition;
    }

    private static T GetOrCreate<T>(
        string path,
        out bool created)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null)
        {
            created = false;
            return asset;
        }

        asset =
            ScriptableObject.CreateInstance<T>();

        AssetDatabase.CreateAsset(asset, path);

        created = true;

        return asset;
    }

    private static void AssignToSceneItemManager(
        ItemCatalog catalog,
        ItemSpawnPolicy policy)
    {
        ItemManager manager =
            Object.FindObjectOfType<ItemManager>();

        if (manager == null)
        {
            return;
        }

        SerializedObject serializedManager =
            new SerializedObject(manager);

        SerializedProperty catalogProperty =
            serializedManager.FindProperty("itemCatalog");

        SerializedProperty policyProperty =
            serializedManager.FindProperty("spawnPolicy");

        bool changed = false;

        if (catalogProperty != null &&
            catalogProperty.objectReferenceValue == null)
        {
            catalogProperty.objectReferenceValue = catalog;
            changed = true;
        }

        if (policyProperty != null &&
            policyProperty.objectReferenceValue == null)
        {
            policyProperty.objectReferenceValue = policy;
            changed = true;
        }

        if (changed)
        {
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "GeneratedTestItems");
        }
    }

    private static void LogValidationResult(
        ItemCatalog catalog)
    {
        List<string> errors =
            catalog.GetProgressionValidationErrors(
                RequiredTestItemCount,
                false);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "測試道具資料生成完成，但配置驗證失敗：\n- " +
                string.Join("\n- ", errors),
                catalog);

            return;
        }

        Debug.Log(
            "七件測試道具資料已補齊到 " + Folder +
            "。既有文本、Icon、Pickup Prefab 與人工配置未被覆蓋。",
            catalog);
    }
}
