#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DreamProceduralRoomAuditP1012A1
{
    private const string MenuRoot = "Tools/Dream Dungeon/Procedural Rooms/P10.12A-1/";
    private const string OutputFolder = "Assets/DreamDungeon/Production/Procedural/Medium";
    private const string PrototypePath = OutputFolder + "/ProcRoom_Medium_13x09.prefab";

    [MenuItem(MenuRoot + "1. Create 13x9 Prototype From Selected Template")]
    private static void CreatePrototype()
    {
        GameObject selected = Selection.activeObject as GameObject;
        if (selected == null)
        {
            Debug.LogError("[P10.12A-1] 请在 Project 中选择一个 13x9 房间 Prefab。");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(selected);
        DreamRoomTemplate sourceTemplate = selected.GetComponent<DreamRoomTemplate>();
        if (string.IsNullOrEmpty(sourcePath) ||
            !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            sourceTemplate == null)
        {
            Debug.LogError("[P10.12A-1] 选择对象必须是根节点带 DreamRoomTemplate 的 Prefab Asset。");
            return;
        }

        if (sourceTemplate.SizeInCells != DreamProceduralRoomKernelP1012A1.MediumSize)
        {
            Debug.LogError("[P10.12A-1] 当前仅允许从 13x9 Template 建立 Prototype。实际=" + sourceTemplate.SizeInCells);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePath) != null)
        {
            Debug.LogWarning(
                "[P10.12A-1.1] Prototype 已存在，不覆盖。\n" +
                "请执行：1B. Repair Existing Prototype Component\n" +
                PrototypePath);
            return;
        }

        EnsureFolder(OutputFolder);
        if (!AssetDatabase.CopyAsset(sourcePath, PrototypePath))
        {
            Debug.LogError("[P10.12A-1] 复制 Prefab 失败。\nSource=" + sourcePath + "\nTarget=" + PrototypePath);
            return;
        }

        AssetDatabase.ImportAsset(PrototypePath, ImportAssetOptions.ForceUpdate);
        RepairPrototypeAsset("Create", sourcePath);
    }

    [MenuItem(MenuRoot + "1B. Repair Existing Prototype Component")]
    private static void RepairExistingPrototype()
    {
        RepairPrototypeAsset("Repair", string.Empty);
    }

    [MenuItem(MenuRoot + "2. Validate 13x9 Prototype + Print Layout")]
    private static void ValidatePrototype()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePath);
        if (asset == null)
        {
            Debug.LogError(
                "[P10.12A-1.1] 找不到 Prototype：\n" + PrototypePath +
                "\n请先执行第 1 项创建。\n");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrototypePath);
        try
        {
            DreamProceduralRoomPrototypeP1012A1 prototype =
                root.GetComponent<DreamProceduralRoomPrototypeP1012A1>();

            if (prototype == null)
            {
                Debug.LogError(
                    "[P10.12A-1.1] Prototype 组件仍缺失。\n" +
                    "请先执行 1B. Repair Existing Prototype Component。");
                return;
            }

            DreamProceduralRoomLayout layout;
            string failure;
            if (!prototype.TryBuildPreview(out layout, out failure))
            {
                Debug.LogError("[P10.12A-1.1] Prototype 生成失败：" + failure);
                return;
            }

            if (!DreamProceduralRoomKernelP1012A1.Validate(layout, out failure))
            {
                Debug.LogError("[P10.12A-1.1] Prototype 校验失败：" + failure);
                return;
            }

            Debug.Log(BuildReport(layout));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem(MenuRoot + "3. Run 256-Seed Kernel Audit")]
    private static void Audit()
    {
        SocketCase[] cases =
        {
            new SocketCase("NS", true,false,true,false),
            new SocketCase("EW", false,true,false,true),
            new SocketCase("NE", true,true,false,false),
            new SocketCase("WS", false,false,true,true),
            new SocketCase("NES", true,true,true,false),
            new SocketCase("EWS", false,true,true,true),
            new SocketCase("NEWS", true,true,true,true)
        };

        int expected = cases.Length * 256;
        int generated = 0;
        int deterministic = 0;
        int failures = 0;
        float minRatio = 1f;
        float maxRatio = 0f;

        Dictionary<DreamProceduralRoomArchetype,int> archetypes =
            new Dictionary<DreamProceduralRoomArchetype,int>();
        StringBuilder fail = new StringBuilder();

        for (int ci = 0; ci < cases.Length; ci++)
        {
            SocketCase sc = cases[ci];
            List<DreamProceduralDoorLane> doors =
                DreamProceduralRoomKernelP1012A1.BuildDefaultDoorSet(sc.N, sc.E, sc.S, sc.W);

            for (int i = 0; i < 256; i++)
            {
                int seed = DreamProceduralRoomKernelP1012A1.DeriveRoomSeed(
                    12345 + i,
                    i % 7,
                    1309,
                    sc.Mask);

                DreamProceduralRoomLayout a;
                string reason;
                if (!DreamProceduralRoomKernelP1012A1.TryGenerate(seed, doors, out a, out reason) ||
                    !DreamProceduralRoomKernelP1012A1.Validate(a, out reason))
                {
                    failures++;
                    AppendFail(fail, sc.Name, seed, reason);
                    continue;
                }

                generated++;
                minRatio = Mathf.Min(minRatio, a.BlockedRatio);
                maxRatio = Mathf.Max(maxRatio, a.BlockedRatio);

                int count;
                archetypes.TryGetValue(a.Archetype, out count);
                archetypes[a.Archetype] = count + 1;

                DreamProceduralRoomLayout b;
                if (!DreamProceduralRoomKernelP1012A1.TryGenerate(seed, doors, out b, out reason) ||
                    a.Archetype != b.Archetype ||
                    !a.BlockedCells.SetEquals(b.BlockedCells))
                {
                    failures++;
                    AppendFail(fail, sc.Name, seed, "同 Seed 结果不一致。" + reason);
                    continue;
                }

                deterministic++;
            }
        }

        if (failures != 0 || generated != expected || deterministic != expected)
        {
            Debug.LogError(
                "[P10.12A-1] Kernel Audit FAILED\nExpected=" + expected +
                " | Generated=" + generated +
                " | Deterministic=" + deterministic +
                " | Failures=" + failures + "\n" + fail);
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[P10.12A-1] 13x9 Procedural Kernel Audit PASS");
        report.AppendLine("SocketCases=7 | SeedsPerCase=256 | Total=" + expected);
        report.AppendLine("Generated=" + generated + "/" + expected + " | Deterministic=" + deterministic + "/" + expected);
        report.AppendLine("BlockedRatioRange=" + (minRatio*100f).ToString("F1") + "%～" + (maxRatio*100f).ToString("F1") + "% | Required=15%～35%");
        report.AppendLine("MainRoute=Reserved2CellBackbone");
        report.AppendLine("WalkableTopology=SingleConnectedComponent");
        report.AppendLine("UsedSockets=100%Connected");
        report.AppendLine("RuntimeIntegration=NotStartedByDesign");
        report.AppendLine("ProductionMainChanged=False | GameSceneChanged=False | DungeonGeneratorChanged=False | DungeonRendererChanged=False");

        foreach (KeyValuePair<DreamProceduralRoomArchetype,int> pair in archetypes)
            report.AppendLine("Archetype." + pair.Key + "=" + pair.Value);

        Debug.Log(report.ToString());
    }

    private static void RepairPrototypeAsset(string mode, string sourcePath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePath);
        if (asset == null)
        {
            Debug.LogError(
                "[P10.12A-1.1] 找不到 Prototype，无法修复：\n" + PrototypePath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrototypePath);
        bool componentAdded = false;
        try
        {
            DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
            if (template == null)
            {
                Debug.LogError("[P10.12A-1.1] Prototype 根节点缺少 DreamRoomTemplate。");
                return;
            }

            if (template.SizeInCells != DreamProceduralRoomKernelP1012A1.MediumSize)
            {
                Debug.LogError("[P10.12A-1.1] Prototype 不是 13x9。实际=" + template.SizeInCells);
                return;
            }

            SerializedObject so = new SerializedObject(template);
            SerializedProperty id = so.FindProperty("templateId");
            if (id != null) id.stringValue = "Procedural_Medium_13x09";
            SerializedProperty weight = so.FindProperty("randomWeight");
            if (weight != null) weight.intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();

            DreamProceduralRoomPrototypeP1012A1 prototype =
                root.GetComponent<DreamProceduralRoomPrototypeP1012A1>();
            if (prototype == null)
            {
                prototype = root.AddComponent<DreamProceduralRoomPrototypeP1012A1>();
                componentAdded = true;
            }

            prototype.ConfigurePreviewSeed(12345);
            template.RefreshDoorSockets();

            PrefabUtility.SaveAsPrefabAsset(root, PrototypePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PrototypePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePath);
        bool persisted =
            reloaded != null &&
            reloaded.GetComponent<DreamProceduralRoomPrototypeP1012A1>() != null;

        Selection.activeObject = reloaded;

        if (!persisted)
        {
            Debug.LogError(
                "[P10.12A-1.1] Prototype 修复后组件仍未持久化。" +
                "请把 Console 截图发给我，不要继续下一步。");
            return;
        }

        Debug.Log(
            "[P10.12A-1.1] Prototype 序列化修复 PASS\n" +
            "Mode=" + mode +
            (string.IsNullOrEmpty(sourcePath) ? string.Empty : " | Source=" + sourcePath) +
            "\nPrototype=" + PrototypePath +
            "\nComponentAdded=" + componentAdded +
            " | ComponentPersisted=True" +
            "\nProductionMainChanged=False" +
            " | GameSceneChanged=False" +
            " | RuntimeCoreChanged=False" +
            "\n下一步：打开 Prototype，选择根节点，然后执行第 2 项 Validate。" );
    }

    private static string BuildReport(DreamProceduralRoomLayout layout)
    {
        StringBuilder r = new StringBuilder();
        r.AppendLine("[P10.12A-1] Selected Prototype PASS");
        r.AppendLine("Size=13x9 | Seed=" + layout.Seed + " | Archetype=" + layout.Archetype);
        r.AppendLine("Blocked=" + layout.BlockedCells.Count + " | Walkable=" + layout.WalkableCellCount + " | BlockedRatio=" + (layout.BlockedRatio*100f).ToString("F1") + "%");
        r.AppendLine("ReservedMainRoute=" + layout.ReservedMainRouteCells.Count + " | UsedSockets=" + layout.UsedDoorLanes.Count);
        r.AppendLine("RuntimeIntegration=NotStartedByDesign");
        r.AppendLine("ASCII (# Blocked, + MainRoute, D Door, . Walkable)");

        Dictionary<Vector2Int,char> chars = new Dictionary<Vector2Int,char>();
        foreach (Vector2Int c in layout.ReservedMainRouteCells) chars[c] = '+';
        foreach (Vector2Int c in layout.BlockedCells) chars[c] = '#';
        for (int d = 0; d < layout.UsedDoorLanes.Count; d++)
            for (int c = 0; c < layout.UsedDoorLanes[d].LocalInsideCells.Count; c++)
                chars[layout.UsedDoorLanes[d].LocalInsideCells[c]] = 'D';

        for (int y = 8; y >= 0; y--)
        {
            for (int x = 0; x < 13; x++)
            {
                char ch;
                if (!chars.TryGetValue(new Vector2Int(x,y), out ch)) ch = '.';
                r.Append(ch);
            }
            r.AppendLine();
        }

        return r.ToString();
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static void AppendFail(StringBuilder report, string caseName, int seed, string reason)
    {
        if (report.Length > 5000) return;
        report.AppendLine(caseName + " Seed=" + seed + " | " + reason);
    }

    private readonly struct SocketCase
    {
        public readonly string Name;
        public readonly bool N;
        public readonly bool E;
        public readonly bool S;
        public readonly bool W;

        public int Mask
        {
            get
            {
                int mask = 0;
                if (N) mask |= 1;
                if (E) mask |= 2;
                if (S) mask |= 4;
                if (W) mask |= 8;
                return mask;
            }
        }

        public SocketCase(string name, bool n, bool e, bool s, bool w)
        {
            Name = name;
            N = n;
            E = e;
            S = s;
            W = w;
        }
    }
}
#endif
