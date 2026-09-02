#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SYS13ProjectAuditV2
{
    private const string MenuPath = "Tools/Dream Dungeon/SYS13/Export Cleanup Audit V2";

    private static readonly string[] SerializedExtensions =
    {
        ".unity", ".prefab", ".asset", ".controller",
        ".overrideController", ".playable"
    };

    private static readonly string[] DebugMarkers =
    {
        "[ContextMenu(",
        "[MenuItem(",
        "SYS1 Debug", "SYS2 Debug", "SYS3 Debug", "SYS4 Debug",
        "SYS5 Debug", "SYS6 Debug", "SYS7 Debug", "SYS8 Debug",
        "SYS9 Debug", "SYS10 Debug", "SYS11 Debug", "SYS12 Debug",
        "ControlledFailure", "controlled failure", "受控失败"
    };

    private static readonly string[] ObsoletePatterns =
    {
        "FindObjectOfType<",
        "FindObjectsOfType<",
        ".enableWordWrapping",
        "Physics2D.CircleCastNonAlloc"
    };

    [MenuItem(MenuPath)]
    public static void Export()
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportPath = Path.Combine(projectRoot, "SYS13_Audit_Report_V2.txt");

            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            string[] allFiles = Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories);

            Dictionary<string, ScriptInfo> scripts = BuildScriptIndex(csFiles, projectRoot);
            List<SerializedFileInfo> serializedFiles = ReadSerializedFiles(allFiles, projectRoot);

            Dictionary<string, int> serializedRefCounts = CountSerializedReferences(scripts, serializedFiles);
            Dictionary<string, List<string>> serializedRefLocations =
                FindSerializedReferenceLocations(scripts, serializedFiles);
            Dictionary<string, int> codeRefCounts = CountCodeReferences(scripts);

            List<MissingGuidInfo> trulyMissing = FindTrulyMissingScriptGuids(serializedFiles);
            List<PackageGuidInfo> externalRefs = FindExternalScriptGuidRefs(serializedFiles);
            List<string> duplicateNonPartialTypes = FindDuplicateNonPartialTypeNames(scripts);
            List<string> partialGroups = FindPartialTypeGroups(scripts);

            StringBuilder report = new StringBuilder(256 * 1024);

            report.AppendLine("Dream Dungeon SYS13 Cleanup Audit V2");
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("Project: " + projectRoot);
            report.AppendLine();

            report.AppendLine("=== SUMMARY ===");
            report.AppendLine("CSharpFiles=" + scripts.Count);
            report.AppendLine("SerializedFiles=" + serializedFiles.Count);
            report.AppendLine("TrulyMissingScriptGUIDs=" + trulyMissing.Count);
            report.AppendLine("ExternalPackageOrBuiltInScriptRefs=" + externalRefs.Count);
            report.AppendLine("DuplicateNonPartialTypes=" + duplicateNonPartialTypes.Count);
            report.AppendLine("PartialTypeGroups=" + partialGroups.Count);

            int zeroBoth = scripts.Values.Count(
                s => serializedRefCounts[s.Path] == 0 && codeRefCounts[s.Path] == 0);
            report.AppendLine("ScriptsWithZeroSerializedAndCodeRefs=" + zeroBoth);
            report.AppendLine();

            report.AppendLine("=== HIGH-CONFIDENCE CLEANUP CANDIDATES ===");
            report.AppendLine(
                "Zero serialized refs + zero detected code refs. " +
                "Review MenuItem/RuntimeInitialize scripts manually before deletion.");
            report.AppendLine();

            foreach (ScriptInfo script in scripts.Values
                .Where(s => serializedRefCounts[s.Path] == 0 && codeRefCounts[s.Path] == 0)
                .OrderBy(s => s.Path))
            {
                report.AppendLine("[CANDIDATE] " + script.Path);
                report.AppendLine("  GUID=" + Safe(script.Guid));
                report.AppendLine("  Types=" +
                    (script.Types.Count == 0 ? "<none>" : string.Join(", ", script.Types)));
                report.AppendLine("  EditorOnlyPath=" + script.IsEditorPath);
                report.AppendLine("  MenuOrContext=" + script.HasMenuOrContext);
                report.AppendLine("  RuntimeInitialize=" + script.HasRuntimeInitialize);
            }

            report.AppendLine();
            report.AppendLine("=== TRULY MISSING SCRIPT GUIDS ===");
            if (trulyMissing.Count == 0)
            {
                report.AppendLine("None.");
            }
            else
            {
                foreach (MissingGuidInfo info in trulyMissing)
                    report.AppendLine(info.SerializedPath + " -> " + info.Guid);
            }

            report.AppendLine();
            report.AppendLine("=== EXTERNAL / PACKAGE / BUILT-IN SCRIPT REFERENCES ===");
            foreach (PackageGuidInfo info in externalRefs
                .OrderBy(x => x.AssetPath).ThenBy(x => x.SerializedPath))
            {
                report.AppendLine(info.SerializedPath + " -> " + info.Guid + " -> " + info.AssetPath);
            }

            report.AppendLine();
            report.AppendLine("=== DUPLICATE NON-PARTIAL TYPES ===");
            if (duplicateNonPartialTypes.Count == 0)
                report.AppendLine("None.");
            else
                foreach (string value in duplicateNonPartialTypes)
                    report.AppendLine(value);

            report.AppendLine();
            report.AppendLine("=== PARTIAL TYPE GROUPS ===");
            if (partialGroups.Count == 0)
                report.AppendLine("None.");
            else
                foreach (string value in partialGroups)
                    report.AppendLine(value);

            report.AppendLine();
            report.AppendLine("=== DEBUG / AUDIT / TEST SCAFFOLD ===");
            AppendHits(report, scripts, DebugMarkers, "[DEBUG]");

            report.AppendLine();
            report.AppendLine("=== OBSOLETE / MIGRATION API HITS ===");
            AppendHits(report, scripts, ObsoletePatterns, "[API]");

            report.AppendLine();
            report.AppendLine("=== ALL SCRIPT REFERENCE MATRIX ===");
            foreach (ScriptInfo script in scripts.Values.OrderBy(s => s.Path))
            {
                report.AppendLine("[SCRIPT] " + script.Path);
                report.AppendLine("  GUID=" + Safe(script.Guid));
                report.AppendLine("  Types=" +
                    (script.Types.Count == 0 ? "<none>" : string.Join(", ", script.Types)));
                report.AppendLine("  SerializedRefs=" + serializedRefCounts[script.Path]);
                report.AppendLine("  CodeRefs=" + codeRefCounts[script.Path]);
                report.AppendLine("  EditorOnlyPath=" + script.IsEditorPath);
                report.AppendLine("  MenuOrContext=" + script.HasMenuOrContext);
                report.AppendLine("  RuntimeInitialize=" + script.HasRuntimeInitialize);

                foreach (string location in serializedRefLocations[script.Path].Take(20))
                    report.AppendLine("    REF " + location);
            }

            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));

            Debug.Log("[SYS13] V2 cleanup audit exported:\n" + reportPath);
            EditorUtility.RevealInFinder(reportPath);
            EditorUtility.DisplayDialog(
                "SYS13 Audit V2 Complete",
                "已输出：\n" + reportPath + "\n\n请上传 SYS13_Audit_Report_V2.txt。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("SYS13 Audit V2 Failed", exception.ToString(), "OK");
        }
    }

    private static void AppendHits(
        StringBuilder report,
        Dictionary<string, ScriptInfo> scripts,
        string[] needles,
        string prefix)
    {
        foreach (ScriptInfo script in scripts.Values.OrderBy(s => s.Path))
        {
            List<string> hits = FindLineHits(script.FullPath, needles);
            if (hits.Count == 0)
                continue;

            report.AppendLine(prefix + " " + script.Path);
            foreach (string hit in hits)
                report.AppendLine("  " + hit);
        }
    }

    private static Dictionary<string, ScriptInfo>
        BuildScriptIndex(string[] csFiles, string projectRoot)
    {
        Dictionary<string, ScriptInfo> result =
            new Dictionary<string, ScriptInfo>(StringComparer.OrdinalIgnoreCase);

        Regex typeRegex = new Regex(
            @"\b(?:(partial)\s+)?(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        foreach (string fullPath in csFiles)
        {
            string path = ToProjectPath(fullPath, projectRoot);
            string text = SafeReadAllText(fullPath);
            string guid = ReadGuidFromMeta(fullPath + ".meta");

            List<TypeDecl> declarations = new List<TypeDecl>();
            foreach (Match match in typeRegex.Matches(text))
            {
                declarations.Add(new TypeDecl
                {
                    Name = match.Groups[2].Value,
                    IsPartial = !string.IsNullOrEmpty(match.Groups[1].Value)
                });
            }

            result[path] = new ScriptInfo
            {
                Path = path,
                FullPath = fullPath,
                Guid = guid,
                TypeDeclarations = declarations,
                Types = declarations.Select(d => d.Name).Distinct().ToList(),
                IsEditorPath = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0,
                HasMenuOrContext = text.Contains("[MenuItem(") || text.Contains("[ContextMenu("),
                HasRuntimeInitialize = text.Contains("RuntimeInitializeOnLoadMethod")
            };
        }

        return result;
    }

    private static List<SerializedFileInfo>
        ReadSerializedFiles(string[] allFiles, string projectRoot)
    {
        List<SerializedFileInfo> result = new List<SerializedFileInfo>();

        foreach (string fullPath in allFiles)
        {
            string ext = Path.GetExtension(fullPath);
            if (!SerializedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                continue;

            try
            {
                result.Add(new SerializedFileInfo
                {
                    Path = ToProjectPath(fullPath, projectRoot),
                    Text = File.ReadAllText(fullPath)
                });
            }
            catch { }
        }

        return result;
    }

    private static List<MissingGuidInfo>
        FindTrulyMissingScriptGuids(List<SerializedFileInfo> serializedFiles)
    {
        Regex regex = new Regex(
            @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        Dictionary<string, MissingGuidInfo> unique = new Dictionary<string, MissingGuidInfo>();

        foreach (SerializedFileInfo file in serializedFiles)
        {
            foreach (Match match in regex.Matches(file.Text))
            {
                string guid = match.Groups[1].Value;
                string resolved = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(resolved))
                    continue;

                string key = file.Path + "|" + guid;
                unique[key] = new MissingGuidInfo
                {
                    SerializedPath = file.Path,
                    Guid = guid
                };
            }
        }

        return unique.Values.OrderBy(x => x.SerializedPath).ThenBy(x => x.Guid).ToList();
    }

    private static List<PackageGuidInfo>
        FindExternalScriptGuidRefs(List<SerializedFileInfo> serializedFiles)
    {
        Regex regex = new Regex(
            @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        Dictionary<string, PackageGuidInfo> unique =
            new Dictionary<string, PackageGuidInfo>();

        foreach (SerializedFileInfo file in serializedFiles)
        {
            foreach (Match match in regex.Matches(file.Text))
            {
                string guid = match.Groups[1].Value;
                string resolved = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(resolved) ||
                    resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string key = file.Path + "|" + guid;
                unique[key] = new PackageGuidInfo
                {
                    SerializedPath = file.Path,
                    Guid = guid,
                    AssetPath = resolved
                };
            }
        }

        return unique.Values.ToList();
    }

    private static List<string>
        FindDuplicateNonPartialTypeNames(Dictionary<string, ScriptInfo> scripts)
    {
        var groups = scripts.Values
            .SelectMany(s => s.TypeDeclarations.Select(d => new
            {
                d.Name,
                d.IsPartial,
                s.Path
            }))
            .GroupBy(x => x.Name)
            .Where(g => g.Count() > 1);

        List<string> result = new List<string>();

        foreach (var group in groups)
        {
            if (group.All(x => x.IsPartial))
                continue;

            result.Add(
                group.Key + " -> " +
                string.Join(" | ",
                    group.Select(x => x.Path + (x.IsPartial ? " [partial]" : " [non-partial]"))));
        }

        return result.OrderBy(x => x).ToList();
    }

    private static List<string>
        FindPartialTypeGroups(Dictionary<string, ScriptInfo> scripts)
    {
        return scripts.Values
            .SelectMany(s => s.TypeDeclarations.Select(d => new { d.Name, d.IsPartial, s.Path }))
            .Where(x => x.IsPartial)
            .GroupBy(x => x.Name)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key)
            .Select(g => g.Key + " -> " + string.Join(" | ", g.Select(x => x.Path)))
            .ToList();
    }

    private static Dictionary<string, int>
        CountSerializedReferences(
            Dictionary<string, ScriptInfo> scripts,
            List<SerializedFileInfo> serializedFiles)
    {
        Dictionary<string, int> result =
            scripts.Keys.ToDictionary(k => k, k => 0, StringComparer.OrdinalIgnoreCase);

        foreach (ScriptInfo script in scripts.Values)
        {
            if (string.IsNullOrEmpty(script.Guid))
                continue;

            string needle = "guid: " + script.Guid;
            int count = 0;
            foreach (SerializedFileInfo file in serializedFiles)
                count += CountOccurrences(file.Text, needle);

            result[script.Path] = count;
        }

        return result;
    }

    private static Dictionary<string, List<string>>
        FindSerializedReferenceLocations(
            Dictionary<string, ScriptInfo> scripts,
            List<SerializedFileInfo> serializedFiles)
    {
        Dictionary<string, List<string>> result =
            scripts.Keys.ToDictionary(
                k => k,
                k => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

        foreach (ScriptInfo script in scripts.Values)
        {
            if (string.IsNullOrEmpty(script.Guid))
                continue;

            string needle = "guid: " + script.Guid;

            foreach (SerializedFileInfo file in serializedFiles)
            {
                int count = CountOccurrences(file.Text, needle);
                if (count > 0)
                    result[script.Path].Add(file.Path + " x" + count);
            }
        }

        return result;
    }

    private static Dictionary<string, int>
        CountCodeReferences(Dictionary<string, ScriptInfo> scripts)
    {
        Dictionary<string, string> texts =
            scripts.Values.ToDictionary(
                s => s.Path,
                s => SafeReadAllText(s.FullPath),
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> result =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (ScriptInfo script in scripts.Values)
        {
            int count = 0;

            foreach (string typeName in script.Types)
            {
                Regex tokenRegex = new Regex(@"\b" + Regex.Escape(typeName) + @"\b");

                foreach (var pair in texts)
                {
                    if (string.Equals(pair.Key, script.Path, StringComparison.OrdinalIgnoreCase))
                        continue;

                    count += tokenRegex.Matches(pair.Value).Count;
                }
            }

            result[script.Path] = count;
        }

        return result;
    }

    private static List<string> FindLineHits(string path, string[] needles)
    {
        List<string> hits = new List<string>();
        string[] lines;

        try { lines = File.ReadAllLines(path); }
        catch { return hits; }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (!needles.Any(needle =>
                line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                continue;

            hits.Add("L" + (i + 1) + ": " + line.Trim());

            if (hits.Count >= 120)
            {
                hits.Add("... hit limit reached");
                break;
            }
        }

        return hits;
    }

    private static string ReadGuidFromMeta(string metaPath)
    {
        if (!File.Exists(metaPath))
            return string.Empty;

        foreach (string line in File.ReadAllLines(metaPath))
        {
            if (line.StartsWith("guid:", StringComparison.Ordinal))
                return line.Substring("guid:".Length).Trim();
        }

        return string.Empty;
    }

    private static int CountOccurrences(string text, string needle)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
            return 0;

        int count = 0;
        int index = 0;

        while (true)
        {
            index = text.IndexOf(needle, index, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            index += needle.Length;
        }
    }

    private static string SafeReadAllText(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }

    private static string ToProjectPath(string fullPath, string projectRoot)
    {
        string relative = fullPath.Substring(projectRoot.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "<none>" : value;
    }

    private sealed class ScriptInfo
    {
        public string Path;
        public string FullPath;
        public string Guid;
        public List<string> Types;
        public List<TypeDecl> TypeDeclarations;
        public bool IsEditorPath;
        public bool HasMenuOrContext;
        public bool HasRuntimeInitialize;
    }

    private sealed class TypeDecl
    {
        public string Name;
        public bool IsPartial;
    }

    private sealed class SerializedFileInfo
    {
        public string Path;
        public string Text;
    }

    private sealed class MissingGuidInfo
    {
        public string SerializedPath;
        public string Guid;
    }

    private sealed class PackageGuidInfo
    {
        public string SerializedPath;
        public string Guid;
        public string AssetPath;
    }
}
#endif
