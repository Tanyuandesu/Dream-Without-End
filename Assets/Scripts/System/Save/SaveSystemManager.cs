using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Authoritative lightweight run-save file service.
///
/// This is deliberately separate from SystemSettingsManager. Settings use
/// PlayerPrefs; run progress uses a JSON file under persistentDataPath.
/// The service knows only SaveGameData and never reads gameplay managers.
/// SYS9 owns the mapping between live gameplay state and this contract.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveSystemManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 1;

    private const string SaveDirectoryName = "DreamDungeon";
    private const string SaveFileName = "run_save.json";
    private const string TempFileName = "run_save.tmp";

    public static SaveSystemManager Instance { get; private set; }

    public string SaveDirectoryPath =>
        Path.Combine(
            Application.persistentDataPath,
            SaveDirectoryName);

    public string SaveFilePath =>
        Path.Combine(
            SaveDirectoryPath,
            SaveFileName);

    private string TempFilePath =>
        Path.Combine(
            SaveDirectoryPath,
            TempFileName);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static SaveSystemManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SaveSystemManager existing =
            FindObjectOfType<SaveSystemManager>();

        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject saveObject =
            new GameObject("SaveSystem_Runtime");

        return saveObject.AddComponent<SaveSystemManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool HasValidSave()
    {
        return TryLoadSave(
            out _,
            out _,
            false);
    }

    public bool TryWriteSave(
        SaveGameData source,
        out string error)
    {
        error = string.Empty;

        if (!TryCreateValidatedCopy(
                source,
                out SaveGameData validated,
                out error))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(
                SaveDirectoryPath);

            string json =
                JsonUtility.ToJson(
                    validated,
                    true);

            File.WriteAllText(
                TempFilePath,
                json,
                new UTF8Encoding(false));

            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }

            File.Move(
                TempFilePath,
                SaveFilePath);

            return true;
        }
        catch (Exception exception)
        {
            error =
                "Save write failed: " +
                exception.Message;

            TryDeleteTempFile();
            Debug.LogError(
                "[SYS8] " + error,
                this);
            return false;
        }
    }

    public bool TryLoadSave(
        out SaveGameData data,
        out string error)
    {
        return TryLoadSave(
            out data,
            out error,
            true);
    }

    public bool DeleteSave(out string error)
    {
        error = string.Empty;

        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }

            TryDeleteTempFile();
            return true;
        }
        catch (Exception exception)
        {
            error =
                "Save delete failed: " +
                exception.Message;

            Debug.LogError(
                "[SYS8] " + error,
                this);
            return false;
        }
    }

    private bool TryLoadSave(
        out SaveGameData data,
        out string error,
        bool logFailures)
    {
        data = null;
        error = string.Empty;

        if (!File.Exists(SaveFilePath))
        {
            error = "No save file exists.";
            return false;
        }

        try
        {
            string json =
                File.ReadAllText(
                    SaveFilePath,
                    Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save file is empty.";
                LogLoadFailure(error, logFailures);
                return false;
            }

            SaveGameData parsed =
                JsonUtility.FromJson<SaveGameData>(json);

            if (!TryCreateValidatedCopy(
                    parsed,
                    out SaveGameData validated,
                    out error))
            {
                LogLoadFailure(error, logFailures);
                return false;
            }

            data = validated;
            return true;
        }
        catch (Exception exception)
        {
            error =
                "Save read failed or file is corrupt: " +
                exception.Message;

            LogLoadFailure(error, logFailures);
            return false;
        }
    }

    private static bool TryCreateValidatedCopy(
        SaveGameData source,
        out SaveGameData validated,
        out string error)
    {
        validated = null;
        error = string.Empty;

        if (source == null)
        {
            error = "SaveGameData is null.";
            return false;
        }

        if (source.saveVersion != CurrentSaveVersion)
        {
            error =
                "Unsupported save version " +
                source.saveVersion +
                ". Expected " +
                CurrentSaveVersion + ".";
            return false;
        }

        if (source.floorIndex < 1)
        {
            error = "Floor index must be at least 1.";
            return false;
        }

        if (float.IsNaN(source.currentHP) ||
            float.IsInfinity(source.currentHP) ||
            source.currentHP < 0f)
        {
            error = "Current HP is invalid.";
            return false;
        }

        if (source.killCount < 0)
        {
            error = "Kill count cannot be negative.";
            return false;
        }

        List<string> itemIds =
            NormalizeItemIds(
                source.collectedItemIds);

        validated =
            new SaveGameData(
                source.floorIndex,
                source.currentHP,
                itemIds,
                source.killCount)
            {
                saveVersion = CurrentSaveVersion
            };

        return true;
    }

    private static List<string> NormalizeItemIds(
        IEnumerable<string> source)
    {
        List<string> result =
            new List<string>();
        HashSet<string> seen =
            new HashSet<string>(
                StringComparer.Ordinal);

        if (source == null)
        {
            return result;
        }

        foreach (string rawId in source)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                continue;
            }

            string id = rawId.Trim();

            if (seen.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private void LogLoadFailure(
        string error,
        bool logFailures)
    {
        if (!logFailures)
        {
            return;
        }

        Debug.LogWarning(
            "[SYS8] Save load rejected | " +
            error,
            this);
    }

    private void TryDeleteTempFile()
    {
        try
        {
            if (File.Exists(TempFilePath))
            {
                File.Delete(TempFilePath);
            }
        }
        catch
        {
            // A stale temp file is non-authoritative. The real save remains
            // untouched, so cleanup failure should not mask the main result.
        }
    }

#if UNITY_EDITOR
    [ContextMenu("SYS8 Debug/Write Probe Save")]
    private void DebugWriteProbeSave()
    {
        SaveGameData probe =
            new SaveGameData(
                4,
                63f,
                new[]
                {
                    "first_memory",
                    "third_memory"
                },
                27);

        bool success =
            TryWriteSave(
                probe,
                out string error);

        Debug.Log(
            "[SYS8] Probe save write=" + success +
            " | Floor=4" +
            " | HP=63" +
            " | Items=first_memory,third_memory" +
            " | Kills=27" +
            " | Path=" + SaveFilePath +
            (success ? string.Empty : " | Error=" + error),
            this);
    }

    [ContextMenu("SYS8 Debug/Read Save")]
    private void DebugReadSave()
    {
        bool success =
            TryLoadSave(
                out SaveGameData data,
                out string error);

        if (!success)
        {
            Debug.LogWarning(
                "[SYS8] Read save=False | " + error,
                this);
            return;
        }

        Debug.Log(
            "[SYS8] Read save=True" +
            " | Version=" + data.saveVersion +
            " | Floor=" + data.floorIndex +
            " | HP=" + data.currentHP.ToString("0.##") +
            " | Items=" +
            string.Join(",", data.collectedItemIds) +
            " | Kills=" + data.killCount,
            this);
    }

    [ContextMenu("SYS8 Debug/Print Save Status")]
    private void DebugPrintSaveStatus()
    {
        Debug.Log(
            "[SYS8] Save status" +
            " | Exists=" + File.Exists(SaveFilePath) +
            " | Valid=" + HasValidSave() +
            " | Path=" + SaveFilePath,
            this);
    }

    [ContextMenu("SYS8 Debug/Test Invalid Data Rejection")]
    private void DebugTestInvalidDataRejection()
    {
        SaveGameData invalid =
            new SaveGameData(
                0,
                63f,
                new[] { "first_memory" },
                27);

        bool accepted =
            TryWriteSave(
                invalid,
                out string error);

        Debug.Log(
            "[SYS8] Invalid save accepted=" + accepted +
            " | Expected=False" +
            " | Error=" + error,
            this);
    }

    [ContextMenu("SYS8 Debug/Delete Save")]
    private void DebugDeleteSave()
    {
        bool success =
            DeleteSave(out string error);

        Debug.Log(
            "[SYS8] Delete save=" + success +
            " | ExistsAfter=" + File.Exists(SaveFilePath) +
            (success ? string.Empty : " | Error=" + error),
            this);
    }
#endif
}
