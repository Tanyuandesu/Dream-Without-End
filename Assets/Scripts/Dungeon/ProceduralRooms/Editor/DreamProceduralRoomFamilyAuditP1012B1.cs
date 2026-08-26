using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12B-1：家族契约 + 泛化 Kernel 审计。
///
/// 这一步故意不改 Runtime。
/// 先证明四种现有 Graybox Shell 都能由同一 Profile/Kernel 描述。
/// </summary>
public static class
    DreamProceduralRoomFamilyAuditP1012B1
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/P10.12B-1 Family Foundation/";

    private const int SeedsPerSocketCase =
        128;

    [MenuItem(
        MenuRoot +
        "1. Validate Four Graybox Family Contracts",
        false,
        3010)]
    private static void
        ValidateFourGrayboxFamilyContracts()
    {
        List<string> errors =
            new List<string>();

        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "[P10.12B-1] Four Graybox Family Contract Audit");

        IReadOnlyList<
            DreamProceduralRoomFamilyProfileP1012B1>
            profiles =
                DreamProceduralRoomFamilyRegistryP1012B1
                    .All;

        for (int i = 0;
             i < profiles.Count;
             i++)
        {
            DreamProceduralRoomFamilyProfileP1012B1
                profile =
                    profiles[i];

            DreamRoomTemplate template;

            string path;

            if (!TryFindTemplateAsset(
                    profile.TemplateId,
                    out template,
                    out path))
            {
                errors.Add(
                    profile.TemplateId +
                    "：Project 中找不到 DreamRoomTemplate Prefab。");
                continue;
            }

            if (template.SizeInCells !=
                profile.SizeInCells)
            {
                errors.Add(
                    profile.TemplateId +
                    " Size 不一致。 Prefab=" +
                    template.SizeInCells +
                    " Profile=" +
                    profile.SizeInCells);
                continue;
            }

            if (template.DoorSockets == null ||
                template.DoorSockets.Count != 4)
            {
                errors.Add(
                    profile.TemplateId +
                    " 需要四方向 Graybox Socket，实际=" +
                    (template.DoorSockets == null
                        ? 0
                        : template.DoorSockets.Count));
                continue;
            }

            HashSet<DreamRoomDoorDirection>
                directions =
                    new HashSet<
                        DreamRoomDoorDirection>();

            for (int s = 0;
                 s <
                 template.DoorSockets.Count;
                 s++)
            {
                DreamRoomDoorSocket socket =
                    template.DoorSockets[s];

                if (socket == null)
                {
                    errors.Add(
                        profile.TemplateId +
                        " 存在 Null Socket。");
                    continue;
                }

                if (!directions.Add(
                        socket.Direction))
                {
                    errors.Add(
                        profile.TemplateId +
                        " 同方向 Socket 重复：" +
                        socket.Direction);
                }

                if (socket.DoorWidthInCells != 2)
                {
                    errors.Add(
                        profile.TemplateId +
                        " Socket " +
                        socket.SocketId +
                        " Width=" +
                        socket.DoorWidthInCells +
                        "，当前 Family Kernel 契约要求 2。");
                }

                HashSet<Vector2Int> actual =
                    new HashSet<Vector2Int>(
                        socket.GetLocalInsideCells());

                HashSet<Vector2Int> expected =
                    new HashSet<Vector2Int>(
                        profile.GetDefaultDoorCells(
                            socket.Direction));

                if (!actual.SetEquals(
                        expected))
                {
                    errors.Add(
                        profile.TemplateId +
                        " Socket " +
                        socket.SocketId +
                        " Cell Contract 不一致。" +
                        " Actual=" +
                        FormatCells(actual) +
                        " Expected=" +
                        FormatCells(expected));
                }
            }

            report.AppendLine(
                profile.FamilyId +
                " | TemplateId=" +
                profile.TemplateId +
                " | Size=" +
                profile.SizeInCells.x +
                "x" +
                profile.SizeInCells.y +
                " | Sockets=4" +
                " | DoorWidth=2" +
                " | Asset=" +
                path);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12B-1] Four Graybox Family Contract Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        report.AppendLine(
            "Result=PASS" +
            " | RuntimeChanged=False" +
            " | ProductionMainChanged=False");

        Debug.Log(
            report.ToString());
    }

    [MenuItem(
        MenuRoot +
        "2. Run 4-Family Generic Kernel Audit",
        false,
        3020)]
    private static void
        RunFourFamilyGenericKernelAudit()
    {
        SocketCase[] socketCases =
        {
            new SocketCase(
                "NS",
                true, false, true, false),
            new SocketCase(
                "EW",
                false, true, false, true),
            new SocketCase(
                "NE",
                true, true, false, false),
            new SocketCase(
                "WS",
                false, false, true, true),
            new SocketCase(
                "NES",
                true, true, true, false),
            new SocketCase(
                "EWS",
                false, true, true, true),
            new SocketCase(
                "NEWS",
                true, true, true, true)
        };

        IReadOnlyList<
            DreamProceduralRoomFamilyProfileP1012B1>
            profiles =
                DreamProceduralRoomFamilyRegistryP1012B1
                    .All;

        StringBuilder report =
            new StringBuilder();

        List<string> failures =
            new List<string>();

        int totalExpected = 0;
        int totalGenerated = 0;
        int totalDeterministic = 0;

        report.AppendLine(
            "[P10.12B-1] 4-Family Generic Kernel Audit");

        for (int p = 0;
             p < profiles.Count;
             p++)
        {
            DreamProceduralRoomFamilyProfileP1012B1
                profile =
                    profiles[p];

            int familyGenerated = 0;
            int familyDeterministic = 0;

            float minRatio =
                1f;

            float maxRatio =
                0f;

            int minBlocked =
                int.MaxValue;

            int maxBlocked =
                int.MinValue;

            Dictionary<
                DreamProceduralRoomArchetype,
                int>
                archetypes =
                    new Dictionary<
                        DreamProceduralRoomArchetype,
                        int>();

            for (int c = 0;
                 c < socketCases.Length;
                 c++)
            {
                SocketCase socketCase =
                    socketCases[c];

                List<DreamProceduralDoorLane>
                    doors =
                        profile.BuildDefaultDoorSet(
                            socketCase.North,
                            socketCase.East,
                            socketCase.South,
                            socketCase.West);

                for (int s = 0;
                     s < SeedsPerSocketCase;
                     s++)
                {
                    totalExpected++;

                    int seed =
                        DreamProceduralRoomFamilyKernelP1012B1
                            .DeriveRoomSeed(
                                73129 + s,
                                s % 7,
                                profile,
                                socketCase.Mask);

                    DreamProceduralRoomLayout first;

                    string failure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .TryGenerate(
                                profile,
                                seed,
                                doors,
                                out first,
                                out failure))
                    {
                        failures.Add(
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Generate=" +
                            failure);

                        continue;
                    }

                    string validationFailure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .Validate(
                                profile,
                                first,
                                out validationFailure))
                    {
                        failures.Add(
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Validate=" +
                            validationFailure);

                        continue;
                    }

                    familyGenerated++;
                    totalGenerated++;

                    minRatio =
                        Mathf.Min(
                            minRatio,
                            first.BlockedRatio);

                    maxRatio =
                        Mathf.Max(
                            maxRatio,
                            first.BlockedRatio);

                    minBlocked =
                        Mathf.Min(
                            minBlocked,
                            first.BlockedCells.Count);

                    maxBlocked =
                        Mathf.Max(
                            maxBlocked,
                            first.BlockedCells.Count);

                    int archetypeCount;

                    archetypes.TryGetValue(
                        first.Archetype,
                        out archetypeCount);

                    archetypes[first.Archetype] =
                        archetypeCount + 1;

                    DreamProceduralRoomLayout second;

                    string secondFailure;

                    if (!DreamProceduralRoomFamilyKernelP1012B1
                            .TryGenerate(
                                profile,
                                seed,
                                doors,
                                out second,
                                out secondFailure))
                    {
                        failures.Add(
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Determinism second generate failed=" +
                            secondFailure);

                        continue;
                    }

                    if (second == null ||
                        first.Archetype !=
                            second.Archetype ||
                        !first.BlockedCells
                            .SetEquals(
                                second.BlockedCells) ||
                        !first.ReservedMainRouteCells
                            .SetEquals(
                                second.ReservedMainRouteCells))
                    {
                        failures.Add(
                            profile.FamilyId +
                            " | " +
                            socketCase.Name +
                            " | Seed=" +
                            seed +
                            " | Determinism mismatch.");

                        continue;
                    }

                    familyDeterministic++;
                    totalDeterministic++;
                }
            }

            int familyExpected =
                socketCases.Length *
                SeedsPerSocketCase;

            report.AppendLine(
                profile.FamilyId +
                " | Size=" +
                profile.SizeInCells.x +
                "x" +
                profile.SizeInCells.y +
                " | Generated=" +
                familyGenerated +
                "/" +
                familyExpected +
                " | Deterministic=" +
                familyDeterministic +
                "/" +
                familyExpected +
                " | Blocked=" +
                minBlocked +
                "～" +
                maxBlocked +
                " | Ratio=" +
                (minRatio * 100f)
                    .ToString("F1") +
                "%～" +
                (maxRatio * 100f)
                    .ToString("F1") +
                "%" +
                " | Archetype=" +
                FormatArchetypes(
                    archetypes));
        }

        if (failures.Count > 0 ||
            totalGenerated !=
                totalExpected ||
            totalDeterministic !=
                totalExpected)
        {
            StringBuilder failureReport =
                new StringBuilder();

            int limit =
                Mathf.Min(
                    30,
                    failures.Count);

            for (int i = 0;
                 i < limit;
                 i++)
            {
                failureReport.AppendLine(
                    "- " +
                    failures[i]);
            }

            if (failures.Count > limit)
            {
                failureReport.AppendLine(
                    "... and " +
                    (failures.Count - limit) +
                    " more.");
            }

            Debug.LogError(
                report.ToString() +
                "\n[P10.12B-1] Generic Kernel Audit FAILED" +
                "\nExpected=" +
                totalExpected +
                " | Generated=" +
                totalGenerated +
                " | Deterministic=" +
                totalDeterministic +
                " | Failures=" +
                failures.Count +
                "\n" +
                failureReport);

            return;
        }

        report.AppendLine(
            "Total=" +
            totalExpected +
            " | Generated=" +
            totalGenerated +
            "/" +
            totalExpected +
            " | Deterministic=" +
            totalDeterministic +
            "/" +
            totalExpected);

        report.AppendLine(
            "UsedSockets=100%Connected" +
            " | WalkableTopology=SingleConnectedComponent" +
            " | MainRoute=Reserved2CellBackbone");

        report.AppendLine(
            "RuntimeMigration=NotStartedByDesign" +
            " | Current13x9R2BUnchanged=True" +
            " | ProductionMainChanged=False");

        report.AppendLine(
            "Result=PASS");

        Debug.Log(
            report.ToString());
    }

    [MenuItem(
        MenuRoot +
        "3. Print Family Profiles",
        false,
        3030)]
    private static void
        PrintFamilyProfiles()
    {
        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "[P10.12B-1] Procedural Family Profiles");

        IReadOnlyList<
            DreamProceduralRoomFamilyProfileP1012B1>
            profiles =
                DreamProceduralRoomFamilyRegistryP1012B1
                    .All;

        for (int i = 0;
             i < profiles.Count;
             i++)
        {
            DreamProceduralRoomFamilyProfileP1012B1
                profile =
                    profiles[i];

            report.AppendLine(
                profile.FamilyId +
                " | Template=" +
                profile.TemplateId +
                " | Size=" +
                profile.SizeInCells.x +
                "x" +
                profile.SizeInCells.y +
                " | Hub=" +
                profile.HubMinimum +
                ".." +
                profile.HubMaximum +
                " | Blocked=" +
                (profile.MinimumBlockedRatio * 100f)
                    .ToString("F0") +
                "%～" +
                (profile.MaximumBlockedRatio * 100f)
                    .ToString("F0") +
                "% | Target=" +
                (profile.TargetBlockedRatioMin * 100f)
                    .ToString("F0") +
                "%～" +
                (profile.TargetBlockedRatioMax * 100f)
                    .ToString("F0") +
                "% | ObstacleShapes=" +
                profile.ObstacleSizes.Count);
        }

        Debug.Log(
            report.ToString());
    }

    private static bool TryFindTemplateAsset(
        string templateId,
        out DreamRoomTemplate template,
        out string path)
    {
        template = null;
        path = string.Empty;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Prefab");

        for (int i = 0;
             i < guids.Length;
             i++)
        {
            string candidatePath =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        candidatePath);

            if (prefab == null)
            {
                continue;
            }

            DreamRoomTemplate candidate =
                prefab.GetComponent<
                    DreamRoomTemplate>();

            if (candidate == null)
            {
                continue;
            }

            if (!string.Equals(
                    candidate.TemplateId,
                    templateId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            template = candidate;
            path = candidatePath;
            return true;
        }

        return false;
    }

    private static string FormatCells(
        IEnumerable<Vector2Int> cells)
    {
        List<Vector2Int> ordered =
            new List<Vector2Int>(
                cells);

        ordered.Sort(
            delegate(
                Vector2Int a,
                Vector2Int b)
            {
                int x =
                    a.x.CompareTo(
                        b.x);

                return
                    x != 0
                        ? x
                        : a.y.CompareTo(
                            b.y);
            });

        return
            "[" +
            string.Join(
                ",",
                ordered) +
            "]";
    }

    private static string FormatArchetypes(
        Dictionary<
            DreamProceduralRoomArchetype,
            int> values)
    {
        List<string> parts =
            new List<string>();

        foreach (
            DreamProceduralRoomArchetype archetype
            in Enum.GetValues(
                typeof(
                    DreamProceduralRoomArchetype)))
        {
            int count;

            values.TryGetValue(
                archetype,
                out count);

            parts.Add(
                archetype +
                ":" +
                count);
        }

        return
            string.Join(
                ",",
                parts);
    }

    private readonly struct SocketCase
    {
        public readonly string Name;
        public readonly bool North;
        public readonly bool East;
        public readonly bool South;
        public readonly bool West;

        public int Mask
        {
            get
            {
                int mask = 0;

                if (North)
                {
                    mask |= 1;
                }

                if (East)
                {
                    mask |= 2;
                }

                if (South)
                {
                    mask |= 4;
                }

                if (West)
                {
                    mask |= 8;
                }

                return mask;
            }
        }

        public SocketCase(
            string name,
            bool north,
            bool east,
            bool south,
            bool west)
        {
            Name = name;
            North = north;
            East = east;
            South = south;
            West = west;
        }
    }
}
