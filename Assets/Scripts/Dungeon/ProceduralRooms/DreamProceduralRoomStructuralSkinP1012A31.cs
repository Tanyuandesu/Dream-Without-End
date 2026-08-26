using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-3.1：把 R2B Hard BlockedCells 转成“可换皮但不改玩法”的视觉层。
///
/// 每个 Blocked Cell 仍由 R2B 的 BoxCollider2D + FloorCells 权威负责。
/// 本组件只在另一棵 RuntimeStructuralSkin Root 下创建 SpriteRenderer。
///
/// 6 种拓扑素材 + 旋转可以覆盖所有 4-neighbour mask：
/// Isolated / End / Straight / Corner / T / Cross。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class DreamProceduralRoomStructuralSkinP1012A31 :
    MonoBehaviour
{
    public const string ResourceThemePath =
        "DreamDungeon/Procedural/ProceduralMediumStructuralSkinTheme";

    public const string SkinRootName =
        "RuntimeStructuralSkin_P10_12A3_1";

    private static readonly Vector2Int North =
        Vector2Int.up;

    private static readonly Vector2Int East =
        Vector2Int.right;

    private static readonly Vector2Int South =
        Vector2Int.down;

    private static readonly Vector2Int West =
        Vector2Int.left;

    private static Sprite fallbackSprite;

    private DreamRoomPlacement placement;
    private int roomIndex = -1;

    private DreamRoomTemplate roomTemplate;
    private DreamProceduralRoomStructuralSkinThemeP1012A31
        theme;

    private bool prepared;
    private bool committed;
    private string lastFailureReason =
        string.Empty;

    private int rendererCount;
    private int disabledR2BDebugRendererCount;

    private readonly List<
        DreamProceduralStructureVisualSlotP1012A31>
        slots =
            new List<
                DreamProceduralStructureVisualSlotP1012A31>();

    public bool IsPrepared => prepared;
    public bool IsCommitted => committed;
    public string LastFailureReason =>
        lastFailureReason;
    public int RoomIndex => roomIndex;
    public int RendererCount => rendererCount;
    public int DisabledR2BDebugRendererCount =>
        disabledR2BDebugRendererCount;

    public string ThemeId =>
        theme == null
            ? "FallbackStructuralTheme"
            : theme.ThemeId;

    public IReadOnlyList<
        DreamProceduralStructureVisualSlotP1012A31>
        Slots => slots;

    public void Prepare(
        DreamRoomPlacement sourcePlacement,
        int sourceRoomIndex)
    {
        if (sourcePlacement == null ||
            !sourcePlacement.HasRuntimeProceduralOverride)
        {
            throw new InvalidOperationException(
                "Structural Skin 需要已经提交 R2B Runtime Procedural Override 的 Placement。");
        }

        placement = sourcePlacement;
        roomIndex = sourceRoomIndex;
        prepared = true;
    }

    private void Start()
    {
        if (!prepared || committed)
        {
            return;
        }

        TryCommit();
    }

    public bool TryCommit()
    {
        lastFailureReason =
            string.Empty;

        if (!Application.isPlaying)
        {
            return Fail(
                "Structural Skin 只允许在 Play Mode 提交。");
        }

        if (!prepared ||
            placement == null ||
            !placement.HasRuntimeProceduralOverride)
        {
            return Fail(
                "Structural Skin 尚未 Prepare 或 Placement Override 已失效。");
        }

        roomTemplate =
            GetComponent<DreamRoomTemplate>();

        if (roomTemplate == null)
        {
            return Fail(
                "房间实例根节点缺少 DreamRoomTemplate。");
        }

        List<Vector2Int> blockedBuffer =
            new List<Vector2Int>();

        placement.GetRuntimeProceduralBlockedLocalCells(
            blockedBuffer);

        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>(
                blockedBuffer);

        if (blocked.Count == 0)
        {
            return Fail(
                "R2B Runtime Procedural BlockedCells 为空。");
        }

        DreamProceduralRoomRuntimeInstanceP1012R2B
            runtimeGeometry =
                GetComponent<
                    DreamProceduralRoomRuntimeInstanceP1012R2B>();

        if (runtimeGeometry == null)
        {
            return Fail(
                "房间实例缺少 R2B Runtime Geometry Component。");
        }

        if (runtimeGeometry.BlockedCellCount !=
            blocked.Count ||
            runtimeGeometry.ColliderCount !=
            blocked.Count)
        {
            return Fail(
                "R2B Geometry 与 Placement Blocked 数量不同步：" +
                " Placement=" + blocked.Count +
                " RuntimeBlocked=" +
                runtimeGeometry.BlockedCellCount +
                " Collider=" +
                runtimeGeometry.ColliderCount);
        }

        theme =
            Resources.Load<
                DreamProceduralRoomStructuralSkinThemeP1012A31>(
                    ResourceThemePath);

        slots.Clear();

        List<Vector2Int> ordered =
            new List<Vector2Int>(
                blocked);

        ordered.Sort(
            CompareCells);

        for (int i = 0;
             i < ordered.Count;
             i++)
        {
            Vector2Int cell =
                ordered[i];

            int mask =
                BuildNeighbourMask(
                    cell,
                    blocked);

            DreamProceduralStructureTopologyP1012A31
                topology;

            float rotationDegrees;

            ResolveTopology(
                mask,
                out topology,
                out rotationDegrees);

            slots.Add(
                new DreamProceduralStructureVisualSlotP1012A31(
                    cell,
                    mask,
                    topology,
                    rotationDegrees));
        }

        Transform oldRoot =
            transform.Find(
                SkinRootName);

        if (oldRoot != null)
        {
            Destroy(
                oldRoot.gameObject);
        }

        GameObject skinRootObject =
            new GameObject(
                SkinRootName);

        Transform skinRoot =
            skinRootObject.transform;

        skinRoot.SetParent(
            transform,
            false);

        skinRoot.localPosition =
            Vector3.zero;

        skinRoot.localRotation =
            Quaternion.identity;

        skinRoot.localScale =
            Vector3.one;

        System.Random random =
            new System.Random(
                unchecked(
                    placement.RuntimeProceduralSeed ^
                    0x2D31A6B7));

        rendererCount = 0;

        for (int i = 0;
             i < slots.Count;
             i++)
        {
            DreamProceduralStructureVisualSlotP1012A31
                slot =
                    slots[i];

            GameObject visual =
                new GameObject(
                    "StructureSkin_" +
                    slot.Topology +
                    "_" +
                    slot.LocalCell.x +
                    "_" +
                    slot.LocalCell.y);

            visual.transform.SetParent(
                skinRoot,
                false);

            Vector3 localPosition =
                roomTemplate.GetLocalCellCenter(
                    slot.LocalCell);

            float jitter =
                theme == null
                    ? 0f
                    : theme.PositionJitter;

            if (jitter > 0f)
            {
                localPosition.x +=
                    RandomRange(
                        random,
                        -jitter,
                        jitter);

                localPosition.y +=
                    RandomRange(
                        random,
                        -jitter,
                        jitter);
            }

            visual.transform.localPosition =
                localPosition;

            visual.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    slot.RotationDegrees);

            float scale =
                theme == null
                    ? 1f
                    : theme.Scale;

            visual.transform.localScale =
                new Vector3(
                    scale,
                    scale,
                    1f);

            SpriteRenderer renderer =
                visual.AddComponent<
                    SpriteRenderer>();

            Sprite selectedSprite =
                theme == null
                    ? null
                    : theme.PickSprite(
                        slot.Topology,
                        random);

            renderer.sprite =
                selectedSprite != null
                    ? selectedSprite
                    : GetFallbackSprite();

            renderer.color =
                selectedSprite != null
                    ? Color.white
                    : GetFallbackColor(
                        slot.Topology);

            renderer.sortingOrder =
                theme == null
                    ? 8
                    : theme.SortingOrder;

            rendererCount++;
        }

        Collider2D[] skinColliders =
            skinRoot.GetComponentsInChildren<
                Collider2D>(
                    true);

        if (skinColliders.Length != 0)
        {
            return Fail(
                "Structural Skin Root 意外产生 Collider2D：" +
                skinColliders.Length);
        }

        disabledR2BDebugRendererCount =
            DisableR2BDebugRenderers();

        if (rendererCount != blocked.Count)
        {
            return Fail(
                "Structural Skin Renderer 数量与 BlockedCells 不一致：" +
                rendererCount +
                "/" +
                blocked.Count);
        }

        committed = true;

        Debug.Log(
            "[P10.12A-3.1] Structural Skin COMMIT" +
            "\nRoomIndex=" + roomIndex +
            " | TemplateId=" +
            roomTemplate.TemplateId +
            " | ProceduralSeed=" +
            placement.RuntimeProceduralSeed +
            " | Archetype=" +
            placement.RuntimeProceduralArchetype +
            " | Theme=" +
            ThemeId +
            "\nBlockedCells=" +
            blocked.Count +
            " | StructuralRenderers=" +
            rendererCount +
            " | DisabledR2BDebugRenderers=" +
            disabledR2BDebugRendererCount +
            "\nTopology=" +
            BuildTopologySummary() +
            "\nSkinColliderCount=0" +
            " | GeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=StructuralThemeSpritePoolsOnly",
            this);

        return true;
    }

    public int CountTopology(
        DreamProceduralStructureTopologyP1012A31 topology)
    {
        int count = 0;

        for (int i = 0;
             i < slots.Count;
             i++)
        {
            if (slots[i].Topology ==
                topology)
            {
                count++;
            }
        }

        return count;
    }

    private int DisableR2BDebugRenderers()
    {
        Transform structureRoot =
            transform.Find(
                DreamProceduralRoomRuntimeInstanceP1012R2B
                    .StructureRootName);

        if (structureRoot == null)
        {
            return 0;
        }

        SpriteRenderer[] renderers =
            structureRoot.GetComponentsInChildren<
                SpriteRenderer>(
                    true);

        int disabled = 0;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                renderers[i];

            if (renderer == null ||
                !renderer.enabled)
            {
                continue;
            }

            renderer.enabled = false;
            disabled++;
        }

        return disabled;
    }

    private string BuildTopologySummary()
    {
        return
            "I:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.Isolated) +
            " E:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.End) +
            " S:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.Straight) +
            " C:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.Corner) +
            " T:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.TJunction) +
            " X:" +
            CountTopology(
                DreamProceduralStructureTopologyP1012A31.Cross);
    }

    private static int BuildNeighbourMask(
        Vector2Int cell,
        HashSet<Vector2Int> blocked)
    {
        int mask = 0;

        if (blocked.Contains(
                cell + North))
        {
            mask |= 1;
        }

        if (blocked.Contains(
                cell + East))
        {
            mask |= 2;
        }

        if (blocked.Contains(
                cell + South))
        {
            mask |= 4;
        }

        if (blocked.Contains(
                cell + West))
        {
            mask |= 8;
        }

        return mask;
    }

    private static void ResolveTopology(
        int mask,
        out DreamProceduralStructureTopologyP1012A31 topology,
        out float rotationDegrees)
    {
        rotationDegrees = 0f;

        int neighbourCount =
            CountBits(mask);

        if (neighbourCount <= 0)
        {
            topology =
                DreamProceduralStructureTopologyP1012A31.Isolated;
            return;
        }

        if (neighbourCount == 1)
        {
            topology =
                DreamProceduralStructureTopologyP1012A31.End;

            switch (mask)
            {
                case 1:
                    rotationDegrees = 0f;
                    break;

                case 2:
                    rotationDegrees = -90f;
                    break;

                case 4:
                    rotationDegrees = 180f;
                    break;

                case 8:
                    rotationDegrees = 90f;
                    break;
            }

            return;
        }

        if (neighbourCount == 2)
        {
            if (mask == 5 ||
                mask == 10)
            {
                topology =
                    DreamProceduralStructureTopologyP1012A31.Straight;

                rotationDegrees =
                    mask == 5
                        ? 0f
                        : 90f;

                return;
            }

            topology =
                DreamProceduralStructureTopologyP1012A31.Corner;

            switch (mask)
            {
                case 3:
                    rotationDegrees = 0f;
                    break;

                case 6:
                    rotationDegrees = -90f;
                    break;

                case 12:
                    rotationDegrees = 180f;
                    break;

                case 9:
                    rotationDegrees = 90f;
                    break;
            }

            return;
        }

        if (neighbourCount == 3)
        {
            topology =
                DreamProceduralStructureTopologyP1012A31.TJunction;

            // 标准 T = N+E+W，缺 South (mask 11)。
            switch (mask)
            {
                case 11:
                    rotationDegrees = 0f;
                    break;

                case 7:
                    rotationDegrees = -90f;
                    break;

                case 14:
                    rotationDegrees = 180f;
                    break;

                case 13:
                    rotationDegrees = 90f;
                    break;
            }

            return;
        }

        topology =
            DreamProceduralStructureTopologyP1012A31.Cross;

        rotationDegrees = 0f;
    }

    private static int CountBits(
        int mask)
    {
        int count = 0;

        while (mask != 0)
        {
            count +=
                mask & 1;

            mask >>= 1;
        }

        return count;
    }

    private static int CompareCells(
        Vector2Int a,
        Vector2Int b)
    {
        int y =
            a.y.CompareTo(
                b.y);

        return y != 0
            ? y
            : a.x.CompareTo(
                b.x);
    }

    private static float RandomRange(
        System.Random random,
        float min,
        float max)
    {
        return
            min +
            (float)random.NextDouble() *
            (max - min);
    }

    private bool Fail(
        string reason)
    {
        lastFailureReason =
            string.IsNullOrWhiteSpace(reason)
                ? "Unknown failure."
                : reason;

        Debug.LogError(
            "[P10.12A-3.1] Structural Skin FAILED" +
            "\nRoomIndex=" +
            roomIndex +
            "\n" +
            lastFailureReason +
            "\nGeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | ProductionMainChanged=False",
            this);

        return false;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "P10_12A3_1_StructuralSkinDebugTexture";

        texture.hideFlags =
            HideFlags.HideAndDontSave;

        texture.SetPixel(
            0,
            0,
            Color.white);

        texture.Apply(
            false,
            true);

        fallbackSprite =
            Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f),
                new Vector2(
                    0.5f,
                    0.5f),
                1f);

        fallbackSprite.name =
            "P10_12A3_1_StructuralSkinDebugSprite";

        fallbackSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return fallbackSprite;
    }

    private static Color GetFallbackColor(
        DreamProceduralStructureTopologyP1012A31 topology)
    {
        switch (topology)
        {
            case DreamProceduralStructureTopologyP1012A31.Isolated:
                return new Color(
                    0.80f,
                    0.58f,
                    0.32f,
                    0.92f);

            case DreamProceduralStructureTopologyP1012A31.End:
                return new Color(
                    0.70f,
                    0.50f,
                    0.28f,
                    0.92f);

            case DreamProceduralStructureTopologyP1012A31.Straight:
                return new Color(
                    0.62f,
                    0.45f,
                    0.26f,
                    0.92f);

            case DreamProceduralStructureTopologyP1012A31.Corner:
                return new Color(
                    0.68f,
                    0.48f,
                    0.26f,
                    0.92f);

            case DreamProceduralStructureTopologyP1012A31.TJunction:
                return new Color(
                    0.56f,
                    0.40f,
                    0.24f,
                    0.92f);

            case DreamProceduralStructureTopologyP1012A31.Cross:
                return new Color(
                    0.48f,
                    0.35f,
                    0.22f,
                    0.92f);

            default:
                return Color.white;
        }
    }
}

[Serializable]
public readonly struct DreamProceduralStructureVisualSlotP1012A31
{
    public Vector2Int LocalCell { get; }
    public int NeighbourMask { get; }
    public DreamProceduralStructureTopologyP1012A31
        Topology { get; }
    public float RotationDegrees { get; }

    public DreamProceduralStructureVisualSlotP1012A31(
        Vector2Int localCell,
        int neighbourMask,
        DreamProceduralStructureTopologyP1012A31 topology,
        float rotationDegrees)
    {
        LocalCell = localCell;
        NeighbourMask = neighbourMask;
        Topology = topology;
        RotationDegrees = rotationDegrees;
    }
}
