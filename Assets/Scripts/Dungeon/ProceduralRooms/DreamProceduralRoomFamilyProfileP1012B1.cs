using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12B-1：中精度房间家族 Profile。
///
/// 这一层只描述“尺寸与生成规则”，不保存具体房间实例状态。
/// 13x9 现行 R2B 暂时仍走旧 Kernel，本阶段先建立可验证的共用家族契约。
/// </summary>
public sealed class DreamProceduralRoomFamilyProfileP1012B1
{
    public string FamilyId { get; }
    public string TemplateId { get; }
    public Vector2Int SizeInCells { get; }
    public int ShellTypeHash { get; }

    public float MinimumBlockedRatio { get; }
    public float MaximumBlockedRatio { get; }
    public float TargetBlockedRatioMin { get; }
    public float TargetBlockedRatioMax { get; }

    public IReadOnlyList<Vector2Int> ObstacleSizes =>
        obstacleSizes;

    private readonly List<Vector2Int> obstacleSizes;

    public DreamProceduralRoomFamilyProfileP1012B1(
        string familyId,
        string templateId,
        Vector2Int sizeInCells,
        int shellTypeHash,
        float minimumBlockedRatio,
        float maximumBlockedRatio,
        float targetBlockedRatioMin,
        float targetBlockedRatioMax,
        IEnumerable<Vector2Int> allowedObstacleSizes)
    {
        FamilyId =
            string.IsNullOrWhiteSpace(familyId)
                ? "Unknown"
                : familyId.Trim();

        TemplateId =
            string.IsNullOrWhiteSpace(templateId)
                ? string.Empty
                : templateId.Trim();

        SizeInCells = sizeInCells;
        ShellTypeHash = shellTypeHash;

        MinimumBlockedRatio =
            Mathf.Clamp01(minimumBlockedRatio);

        MaximumBlockedRatio =
            Mathf.Clamp(
                maximumBlockedRatio,
                MinimumBlockedRatio,
                1f);

        TargetBlockedRatioMin =
            Mathf.Clamp(
                targetBlockedRatioMin,
                MinimumBlockedRatio,
                MaximumBlockedRatio);

        TargetBlockedRatioMax =
            Mathf.Clamp(
                targetBlockedRatioMax,
                TargetBlockedRatioMin,
                MaximumBlockedRatio);

        obstacleSizes =
            new List<Vector2Int>();

        if (allowedObstacleSizes != null)
        {
            foreach (Vector2Int size in
                     allowedObstacleSizes)
            {
                if (size.x <= 0 ||
                    size.y <= 0)
                {
                    continue;
                }

                if (!obstacleSizes.Contains(size))
                {
                    obstacleSizes.Add(size);
                }
            }
        }

        if (obstacleSizes.Count == 0)
        {
            obstacleSizes.Add(
                Vector2Int.one);
        }
    }

    public Vector2Int HubMinimum =>
        new Vector2Int(
            Mathf.Max(
                1,
                SizeInCells.x / 2 - 1),
            Mathf.Max(
                1,
                SizeInCells.y / 2 - 1));

    public Vector2Int HubMaximum =>
        new Vector2Int(
            Mathf.Min(
                SizeInCells.x - 2,
                SizeInCells.x / 2),
            Mathf.Min(
                SizeInCells.y - 2,
                SizeInCells.y / 2));

    /// <summary>
    /// Graybox Factory 一直采用的中心 Socket 基准格。
    /// DreamRoomDoorSocket.GetLocalInsideCells() 会从该基准格向墙面方向扩 2 格。
    /// </summary>
    public Vector2Int GetDefaultSocketBaseCell(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return new Vector2Int(
                    SizeInCells.x / 2,
                    SizeInCells.y - 1);

            case DreamRoomDoorDirection.East:
                return new Vector2Int(
                    SizeInCells.x - 1,
                    SizeInCells.y / 2);

            case DreamRoomDoorDirection.South:
                return new Vector2Int(
                    SizeInCells.x / 2,
                    0);

            case DreamRoomDoorDirection.West:
                return new Vector2Int(
                    0,
                    SizeInCells.y / 2);

            default:
                return Vector2Int.zero;
        }
    }

    public List<Vector2Int> GetDefaultDoorCells(
        DreamRoomDoorDirection direction)
    {
        Vector2Int baseCell =
            GetDefaultSocketBaseCell(direction);

        Vector2Int sideways =
            direction.PerpendicularCellOffset();

        // doorWidthInCells = 2，与当前 Graybox 契约一致。
        return new List<Vector2Int>
        {
            baseCell - sideways,
            baseCell
        };
    }

    public List<DreamProceduralDoorLane>
        BuildDefaultDoorSet(
            bool north,
            bool east,
            bool south,
            bool west)
    {
        List<DreamProceduralDoorLane> result =
            new List<DreamProceduralDoorLane>();

        if (north)
        {
            result.Add(
                new DreamProceduralDoorLane(
                    DreamRoomDoorDirection.North,
                    GetDefaultDoorCells(
                        DreamRoomDoorDirection.North)));
        }

        if (east)
        {
            result.Add(
                new DreamProceduralDoorLane(
                    DreamRoomDoorDirection.East,
                    GetDefaultDoorCells(
                        DreamRoomDoorDirection.East)));
        }

        if (south)
        {
            result.Add(
                new DreamProceduralDoorLane(
                    DreamRoomDoorDirection.South,
                    GetDefaultDoorCells(
                        DreamRoomDoorDirection.South)));
        }

        if (west)
        {
            result.Add(
                new DreamProceduralDoorLane(
                    DreamRoomDoorDirection.West,
                    GetDefaultDoorCells(
                        DreamRoomDoorDirection.West)));
        }

        return result;
    }
}

/// <summary>
/// P10.12B-1 家族注册表。
/// 不使用 ScriptableObject，避免在结构迁移阶段增加额外 Asset 权威。
/// </summary>
public static class DreamProceduralRoomFamilyRegistryP1012B1
{
    public static readonly
        DreamProceduralRoomFamilyProfileP1012B1
        Small08x06 =
            new DreamProceduralRoomFamilyProfileP1012B1(
                "Small_08x06",
                "Graybox_08x06",
                new Vector2Int(8, 6),
                806,
                0.15f,
                0.35f,
                0.16f,
                0.24f,
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2)
                });

    public static readonly
        DreamProceduralRoomFamilyProfileP1012B1
        Medium13x09 =
            new DreamProceduralRoomFamilyProfileP1012B1(
                "Medium_13x09",
                "Graybox_13x09",
                new Vector2Int(13, 9),
                1309,
                0.15f,
                0.35f,
                0.20f,
                0.29f,
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2),
                    new Vector2Int(1, 3),
                    new Vector2Int(3, 1)
                });

    public static readonly
        DreamProceduralRoomFamilyProfileP1012B1
        Wide18x07 =
            new DreamProceduralRoomFamilyProfileP1012B1(
                "Wide_18x07",
                "Graybox_18x07",
                new Vector2Int(18, 7),
                1807,
                0.15f,
                0.35f,
                0.18f,
                0.28f,
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2),
                    new Vector2Int(1, 3),
                    new Vector2Int(3, 1),
                    new Vector2Int(1, 4),
                    new Vector2Int(4, 1)
                });

    public static readonly
        DreamProceduralRoomFamilyProfileP1012B1
        Tall09x16 =
            new DreamProceduralRoomFamilyProfileP1012B1(
                "Tall_09x16",
                "Graybox_09x16",
                new Vector2Int(9, 16),
                916,
                0.15f,
                0.35f,
                0.18f,
                0.28f,
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2),
                    new Vector2Int(1, 3),
                    new Vector2Int(3, 1),
                    new Vector2Int(1, 4),
                    new Vector2Int(4, 1)
                });

    private static readonly
        DreamProceduralRoomFamilyProfileP1012B1[]
        all =
        {
            Small08x06,
            Medium13x09,
            Wide18x07,
            Tall09x16
        };

    public static IReadOnlyList<
        DreamProceduralRoomFamilyProfileP1012B1>
        All => all;

    public static bool TryGetByTemplateId(
        string templateId,
        out DreamProceduralRoomFamilyProfileP1012B1
            profile)
    {
        profile = null;

        if (string.IsNullOrWhiteSpace(
                templateId))
        {
            return false;
        }

        for (int i = 0;
             i < all.Length;
             i++)
        {
            if (string.Equals(
                    all[i].TemplateId,
                    templateId,
                    StringComparison.Ordinal))
            {
                profile = all[i];
                return true;
            }
        }

        return false;
    }
}
