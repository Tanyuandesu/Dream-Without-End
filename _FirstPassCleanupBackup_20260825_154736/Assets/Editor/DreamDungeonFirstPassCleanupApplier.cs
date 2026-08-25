#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the approved first-pass cleanup to the user's complete local project.
///
/// The tool deletes only the exact historical/test paths listed below. It never
/// replaces the Assets folder, so local artwork that was omitted from the audit
/// archive remains untouched. Before mutation, every affected asset and its meta
/// file are copied to a timestamped backup folder beside Assets.
/// </summary>
public static class DreamDungeonFirstPassCleanupApplier
{
    private const string ExpectedUnityVersion = "6000.0.26f1";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string MenuPath =
        "Tools/Dream Dungeon/Maintenance/Apply First-Pass No-Loss Cleanup";

    private static readonly string[] RequiredRetainedPaths =
    {
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionPipelineP107.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomSelectedProductionRuntimeProbeP109.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomMusicRoomGeometryP1010.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomMusicRoomArtReferenceRepairP1011.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomClassroomGeometryP108.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamCorridorPassAuditC2.cs",
        "Assets/Scripts/Combat/Editor/CombatCB9FullSystemAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB95TemporaryHealthBarAudit.cs",
        "Assets/Scripts/Enemy/Editor/EnemyEA3AlgorithmAudit.cs",
        "Assets/Scripts/Enemy/Editor/EnemyEA3NavigationAudit.cs",
        "Assets/Scripts/Animation/Editor/TemporaryCharacterAnimationInstaller.cs",
        "Assets/Scripts/Animation/Editor/CombatCB10AAnimationAudit.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonGenerator.SocketCorridors.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonRenderer.cs",
        "Assets/Scripts/Enemy/EnemyPathfinder.cs",
        "Assets/DreamDungeon/Production/Catalog/RoomCatalog_Production.asset",
        "Assets/DreamDungeon/Production/Rooms/MusicRoom_01/Room_MusicRoom_01.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_08x06.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_09x16.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_13x09.prefab",
        "Assets/DreamDungeon/Generated/R3_Graybox/Rooms/Room_18x07.prefab",
        GameScenePath
    };

    private static readonly string[] CleanupAssetPaths =
    {
        "Assets/TextMesh Pro/Examples & Extras",
        "Assets/TutorialInfo",
        "Assets/Readme.asset",

        "Assets/DreamDungeon/Generated/R9_1_NonRectSample",
        "Assets/DreamDungeon/Generated/R9_4_1_RoleTags_Clean",
        "Assets/DreamDungeon/Generated/R9_4_2_Rare",
        "Assets/DreamDungeon/Generated/R9_4_3_CoreItem",
        "Assets/DreamDungeon/Generated/R9_4_4_Special",

        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamCorridorPassAuditC1.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomCoreItemRoomAuditR943.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomGeometryContractAuditR93.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomGrayboxLibraryGenerator.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomNonRectRuntimeAuditR92.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomNonRectSampleGeneratorR91.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionArtLayersP104.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionBootstrapP100.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionCatalogCommitP106.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionClosedBlockerP105.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionGeometryP101.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionGeometryRefineP1025.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionRuntimeProbeP103.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomProductionVisualP102.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomRareRuleAuditR942.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomRoleTagAuditR941.cs",
        "Assets/Scripts/Dungeon/HybridRooms/Editor/DreamRoomSpecialRoomAuditR944.cs",

        "Assets/Scripts/Combat/Editor/CombatCB0ContractAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB1RuntimeAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB2DecayAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB35FacingAttackAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB3ActionTimingAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB45MultiInputAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB4PostKnockbackRecoveryAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB5DirectAttackAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB6DeathLifecycleAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB7WeakHitReactionAudit.cs",
        "Assets/Scripts/Combat/Editor/CombatCB8ActionArbitrationAudit.cs",

        "Assets/Scripts/Enemy/Editor/EnemyEA1ConfigurationAudit.cs",
        "Assets/Scripts/Enemy/Editor/EnemyEA2RuntimeAudit.cs",
        "Assets/Scripts/Item/Editor/GenerateTestItemAssets.cs",
        "Assets/Scripts/Background/Editor/GenerateDreamBackgroundProgressionProfile.cs",

        "Assets/Scripts/Dungeon/HybridRooms/DreamRoomDataPreview.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonLayoutR2Preview.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonTemplatePlacementR4Preview.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonRoomGraphR5Preview.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonGenerator.SpawnResolverR81.cs",
        "Assets/Scripts/Dungeon/HybridRooms/DungeonSocketCorridorR6Preview.cs",
        "Assets/Scripts/Combat/HealthDebugHUD.cs",
        "Assets/Scripts/Background/DreamBackgroundProgressionDebug.cs",

        "Assets/Scripts/Dungeon/HybridRooms/DungeonRenderer_Backup.cs.txt",
        "Assets/Scripts/Background/DreamFogDrift _Backup.cs.txt"
    };

    private static readonly HashSet<string> RemovedComponentTypeNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "DungeonSocketCorridorR6Preview",
            "HealthDebugHUD",
            "DreamBackgroundProgressionDebug"
        };

    [MenuItem(MenuPath, false, 9000)]
    private static void ApplyCleanup()
    {
        if (!ValidatePreconditions(out string preconditionError))
        {
            EditorUtility.DisplayDialog(
                "First-Pass Cleanup：已中止",
                preconditionError,
                "OK");
            return;
        }

        List<string> existingTargets = new List<string>();
        List<string> alreadyAbsentTargets = new List<string>();

        for (int i = 0; i < CleanupAssetPaths.Length; i++)
        {
            string path = CleanupAssetPaths[i];
            if (AssetPathExists(path))
            {
                existingTargets.Add(path);
            }
            else
            {
                alreadyAbsentTargets.Add(path);
            }
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply First-Pass No-Loss Cleanup",
            "即将在当前完整项目中执行精确路径清理。\n\n" +
            "将处理：" + existingTargets.Count + " 个文件／目录\n" +
            "已经不存在：" + alreadyAbsentTargets.Count + " 个\n\n" +
            "执行前会自动备份所有目标和 GameScene。\n" +
            "不会覆盖 Assets，也不会接触列表之外的照片或美术素材。",
            "建立备份并执行",
            "取消");

        if (!confirmed)
        {
            return;
        }

        string projectRoot = GetProjectRoot();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupRoot = Path.Combine(
            projectRoot,
            "_FirstPassCleanupBackup_" + timestamp);

        string originalScenePath = SceneManager.GetActiveScene().path;
        string selfScriptPath = FindSelfScriptPath();
        bool mutationStarted = false;

        try
        {
            Directory.CreateDirectory(backupRoot);

            BackupAssetPath(GameScenePath, backupRoot);

            for (int i = 0; i < existingTargets.Count; i++)
            {
                BackupAssetPath(existingTargets[i], backupRoot);
            }

            if (!string.IsNullOrEmpty(selfScriptPath))
            {
                BackupAssetPath(selfScriptPath, backupRoot);
            }

            mutationStarted = true;

            Scene gameScene = EditorSceneManager.OpenScene(
                GameScenePath,
                OpenSceneMode.Single);

            Dictionary<string, int> removedComponents =
                RemoveObsoleteSceneComponents(gameScene);

            if (!EditorSceneManager.SaveScene(gameScene))
            {
                throw new InvalidOperationException(
                    "GameScene 保存失败，未继续删除资产。");
            }

            List<string> deletedTargets = new List<string>();
            List<string> deletionFailures = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < existingTargets.Count; i++)
                {
                    string path = existingTargets[i];
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        deletedTargets.Add(path);
                    }
                    else
                    {
                        deletionFailures.Add(path);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (deletionFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    "以下资产无法删除：\n- " +
                    string.Join("\n- ", deletionFailures));
            }

            List<string> remainingTargets = new List<string>();
            for (int i = 0; i < existingTargets.Count; i++)
            {
                if (AssetPathExists(existingTargets[i]))
                {
                    remainingTargets.Add(existingTargets[i]);
                }
            }

            if (remainingTargets.Count > 0)
            {
                throw new InvalidOperationException(
                    "删除后仍存在以下目标：\n- " +
                    string.Join("\n- ", remainingTargets));
            }

            if (!string.IsNullOrEmpty(originalScenePath) &&
                !string.Equals(
                    originalScenePath,
                    GameScenePath,
                    StringComparison.Ordinal) &&
                File.Exists(ToAbsolutePath(originalScenePath)))
            {
                EditorSceneManager.OpenScene(
                    originalScenePath,
                    OpenSceneMode.Single);
            }

            WriteCleanupReport(
                backupRoot,
                deletedTargets,
                alreadyAbsentTargets,
                removedComponents,
                selfScriptPath);

            AssetDatabase.SaveAssets();

            bool selfRemoved = false;
            if (!string.IsNullOrEmpty(selfScriptPath))
            {
                selfRemoved = AssetDatabase.DeleteAsset(selfScriptPath);
            }

            EditorUtility.DisplayDialog(
                "First-Pass Cleanup：完成",
                "第一轮增量瘦身已经执行。\n\n" +
                "删除目标：" + deletedTargets.Count + "\n" +
                "备份位置：\n" + backupRoot + "\n\n" +
                (selfRemoved
                    ? "清理工具已自我删除。请等待 Unity 重新编译。"
                    : "工具脚本未能自我删除，请在确认无红错后手动删除。"),
                "OK");
        }
        catch (Exception exception)
        {
            string rollbackResult = "尚未开始修改，无需回滚。";

            if (mutationStarted)
            {
                try
                {
                    RestoreAssetsFromBackup(backupRoot);
                    AssetDatabase.Refresh();
                    rollbackResult =
                        "已从自动备份恢复本轮涉及的Assets。";
                }
                catch (Exception rollbackException)
                {
                    rollbackResult =
                        "自动恢复失败，请手动从以下目录恢复：\n" +
                        backupRoot + "\n\n恢复错误：" +
                        rollbackException.Message;
                }
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "First-Pass Cleanup：失败",
                exception.Message + "\n\n" + rollbackResult,
                "OK");
        }
    }

    private static bool ValidatePreconditions(out string error)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            error = "请先退出 Play Mode。";
            return false;
        }

        if (!string.Equals(
                Application.unityVersion,
                ExpectedUnityVersion,
                StringComparison.Ordinal))
        {
            error =
                "Unity版本不匹配。\n" +
                "要求：" + ExpectedUnityVersion + "\n" +
                "当前：" + Application.unityVersion;
            return false;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isDirty)
            {
                error =
                    "存在未保存Scene：" + scene.name + "。\n" +
                    "请先保存或撤销修改，再执行清理。";
                return false;
            }
        }

        List<string> missingRequired = new List<string>();
        for (int i = 0; i < RequiredRetainedPaths.Length; i++)
        {
            if (!AssetPathExists(RequiredRetainedPaths[i]))
            {
                missingRequired.Add(RequiredRetainedPaths[i]);
            }
        }

        if (missingRequired.Count > 0)
        {
            error =
                "项目不像本次审计的权威基线，以下保留对象不存在：\n- " +
                string.Join("\n- ", missingRequired) +
                "\n\n为避免操作错误项目，本次清理已中止。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static Dictionary<string, int> RemoveObsoleteSceneComponents(
        Scene scene)
    {
        Dictionary<string, int> removed = new Dictionary<string, int>();

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            MonoBehaviour[] behaviours =
                roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = behaviours.Length - 1; i >= 0; i--)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (!RemovedComponentTypeNames.Contains(typeName))
                {
                    continue;
                }

                if (!removed.ContainsKey(typeName))
                {
                    removed.Add(typeName, 0);
                }

                removed[typeName]++;
                UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }

        return removed;
    }

    private static string FindSelfScriptPath()
    {
        string[] guids = AssetDatabase.FindAssets(
            "DreamDungeonFirstPassCleanupApplier t:MonoScript");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.Equals(
                    Path.GetFileName(path),
                    "DreamDungeonFirstPassCleanupApplier.cs",
                    StringComparison.Ordinal))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static bool AssetPathExists(string assetPath)
    {
        return AssetDatabase.IsValidFolder(assetPath) ||
               File.Exists(ToAbsolutePath(assetPath));
    }

    private static string GetProjectRoot()
    {
        DirectoryInfo parent = Directory.GetParent(Application.dataPath);
        if (parent == null)
        {
            throw new InvalidOperationException("无法取得Unity项目根目录。");
        }

        return parent.FullName;
    }

    private static string ToAbsolutePath(string projectRelativePath)
    {
        string normalized = projectRelativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);

        return Path.Combine(GetProjectRoot(), normalized);
    }

    private static void BackupAssetPath(
        string assetPath,
        string backupRoot)
    {
        string source = ToAbsolutePath(assetPath);
        string destination = Path.Combine(
            backupRoot,
            assetPath.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(source))
        {
            CopyDirectory(source, destination);
        }
        else if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
        else
        {
            return;
        }

        string sourceMeta = source + ".meta";
        if (File.Exists(sourceMeta))
        {
            string destinationMeta = destination + ".meta";
            Directory.CreateDirectory(Path.GetDirectoryName(destinationMeta));
            File.Copy(sourceMeta, destinationMeta, true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        string[] directories = Directory.GetDirectories(
            source,
            "*",
            SearchOption.AllDirectories);

        for (int i = 0; i < directories.Length; i++)
        {
            string relative = directories[i].Substring(source.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        string[] files = Directory.GetFiles(
            source,
            "*",
            SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            string relative = files[i].Substring(source.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string destinationFile = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
            File.Copy(files[i], destinationFile, true);
        }
    }

    private static void RestoreAssetsFromBackup(string backupRoot)
    {
        string backedUpAssets = Path.Combine(backupRoot, "Assets");
        if (!Directory.Exists(backedUpAssets))
        {
            throw new DirectoryNotFoundException(
                "备份中不存在Assets目录：" + backedUpAssets);
        }

        CopyDirectory(backedUpAssets, Application.dataPath);
    }

    private static void WriteCleanupReport(
        string backupRoot,
        List<string> deletedTargets,
        List<string> alreadyAbsentTargets,
        Dictionary<string, int> removedComponents,
        string selfScriptPath)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("Dream Dungeon First-Pass Cleanup");
        report.AppendLine("Unity=" + Application.unityVersion);
        report.AppendLine("Time=" + DateTime.Now.ToString("O"));
        report.AppendLine("Backup=" + backupRoot);
        report.AppendLine();

        report.AppendLine("Deleted assets:");
        for (int i = 0; i < deletedTargets.Count; i++)
        {
            report.AppendLine("- " + deletedTargets[i]);
        }

        report.AppendLine();
        report.AppendLine("Already absent:");
        for (int i = 0; i < alreadyAbsentTargets.Count; i++)
        {
            report.AppendLine("- " + alreadyAbsentTargets[i]);
        }

        report.AppendLine();
        report.AppendLine("Removed GameScene components:");
        foreach (KeyValuePair<string, int> entry in removedComponents)
        {
            report.AppendLine("- " + entry.Key + " x" + entry.Value);
        }

        report.AppendLine();
        report.AppendLine("Cleanup tool source=" + selfScriptPath);

        File.WriteAllText(
            Path.Combine(backupRoot, "CleanupReport.txt"),
            report.ToString(),
            new UTF8Encoding(false));
    }
}
#endif
