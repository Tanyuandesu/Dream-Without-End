using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 建立三個測試 ItemDefinition、Catalog 與 SpawnPolicy。
/// </summary>
public static class GenerateTestItemAssets
{
    private const string Folder =
        "Assets/GeneratedTestItems";

    [MenuItem(
        "Tools/Dream Dungeon/Generate Test Item Assets")]
    public static void Generate()
    {
        EnsureFolder();

        ItemDefinition first =
            GetOrCreate<ItemDefinition>(
                Folder + "/Item_FirstMemory.asset");

        first.ConfigureForEditor(
            "first_memory",
            "First Memory",
            "The first guaranteed progression item.",
            new Color(0.15f, 0.95f, 1f, 1f),
            1,
            true,
            1,
            new[] { "memory", "progression", "tier_1" });

        EditorUtility.SetDirty(first);

        ItemDefinition second =
            GetOrCreate<ItemDefinition>(
                Folder + "/Item_SecondMemory.asset");

        second.ConfigureForEditor(
            "second_memory",
            "Second Memory",
            "A later progression item.",
            new Color(0.75f, 0.35f, 1f, 1f),
            1,
            true,
            1,
            new[] { "memory", "progression", "tier_2" });

        EditorUtility.SetDirty(second);

        ItemDefinition third =
            GetOrCreate<ItemDefinition>(
                Folder + "/Item_ThirdMemory.asset");

        third.ConfigureForEditor(
            "third_memory",
            "Third Memory",
            "A later progression item.",
            new Color(1f, 0.55f, 0.15f, 1f),
            1,
            true,
            1,
            new[] { "memory", "progression", "tier_3" });

        EditorUtility.SetDirty(third);

        ItemCatalog catalog =
            GetOrCreate<ItemCatalog>(
                Folder + "/ItemCatalog.asset");

        catalog.ConfigureForEditor(
            first,
            new List<ItemDefinition>
            {
                second,
                third
            });

        EditorUtility.SetDirty(catalog);

        ItemSpawnPolicy policy =
            GetOrCreate<ItemSpawnPolicy>(
                Folder + "/ItemSpawnPolicy.asset");

        policy.ConfigureForEditor(
            2,
            false,
            0.20f,
            0.12f,
            0.85f,
            1);

        EditorUtility.SetDirty(policy);

        AssignToSceneItemManager(
            catalog,
            policy);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);

        Debug.Log(
            "測試道具資料已生成到 " + Folder);
    }

    private static T GetOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset =
            AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null)
        {
            return asset;
        }

        asset =
            ScriptableObject.CreateInstance<T>();

        AssetDatabase.CreateAsset(asset, path);

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

        serializedManager.FindProperty(
            "itemCatalog").objectReferenceValue = catalog;

        serializedManager.FindProperty(
            "spawnPolicy").objectReferenceValue = policy;

        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
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
}
