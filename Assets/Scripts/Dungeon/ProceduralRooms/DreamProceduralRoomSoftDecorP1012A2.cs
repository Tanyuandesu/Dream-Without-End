using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-2：中精度房间的“软装饰层”。
///
/// 硬边界：
/// 1. 不创建任何 Collider2D。
/// 2. 不修改 DreamRoomPlacement / DungeonLayout / FloorCells / A*。
/// 3. 只从已经提交的 R2B BlockedCells 与实例 Socket 状态读取数据。
/// 4. 具体 Sprite 来自可替换 Theme Asset；换图不需要重做导航。
///
/// Renderer 在房间根节点尚未激活时 Prepare()。
/// 当 R7.4 已经真正打开 Used Socket 并激活 Rooms Root 后，Start() 才提交装饰。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamProceduralRoomSoftDecorP1012A2 :
    MonoBehaviour
{
    public const string ResourceThemePath =
        "DreamDungeon/Procedural/ProceduralMediumSoftDecorTheme";

    public const string DecorRootName =
        "RuntimeSoftDecor_P10_12A2";

    public const string NormalRootName =
        "DecorativeProps";

    public const string ForegroundRootName =
        "ForegroundProps";

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private static Sprite fallbackSprite;

    private DreamRoomPlacement placement;
    private int roomIndex = -1;

    private DreamRoomTemplate roomTemplate;
    private DreamProceduralRoomDecorThemeP1012A2 theme;

    private bool prepared;
    private bool committed;
    private string lastFailureReason = string.Empty;

    private readonly List<DreamProceduralDecorSlotP1012A2>
        slots =
            new List<DreamProceduralDecorSlotP1012A2>();

    private readonly HashSet<Vector2Int>
        doorClearanceCells =
            new HashSet<Vector2Int>();

    public bool IsPrepared => prepared;
    public bool IsCommitted => committed;
    public string LastFailureReason => lastFailureReason;
    public int RoomIndex => roomIndex;
    public int SlotCount => slots.Count;
    public string ThemeId =>
        theme == null
            ? "FallbackDebugTheme"
            : theme.ThemeId;

    public IReadOnlyList<DreamProceduralDecorSlotP1012A2>
        Slots => slots;

    public void Prepare(
        DreamRoomPlacement sourcePlacement,
        int sourceRoomIndex)
    {
        if (sourcePlacement == null ||
            !sourcePlacement.HasRuntimeProceduralOverride)
        {
            throw new InvalidOperationException(
                "Soft Decor 需要已经提交 R2B Runtime Procedural Override 的 Placement。");
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
        lastFailureReason = string.Empty;

        if (!Application.isPlaying)
        {
            return Fail(
                "Soft Decor 只允许在 Play Mode 提交。");
        }

        if (!prepared ||
            placement == null ||
            !placement.HasRuntimeProceduralOverride)
        {
            return Fail(
                "Soft Decor 尚未 Prepare 或 Placement Override 已失效。");
        }

        roomTemplate =
            GetComponent<DreamRoomTemplate>();

        if (roomTemplate == null)
        {
            return Fail(
                "房间实例根节点缺少 DreamRoomTemplate。");
        }

        theme =
            Resources.Load<
                DreamProceduralRoomDecorThemeP1012A2>(
                    ResourceThemePath);

        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>();

        List<Vector2Int> blockedBuffer =
            new List<Vector2Int>();

        placement.GetRuntimeProceduralBlockedLocalCells(
            blockedBuffer);

        blocked.UnionWith(blockedBuffer);

        if (blocked.Count == 0)
        {
            return Fail(
                "R2B Runtime Procedural BlockedCells 为空。");
        }

        if (!BuildDoorClearance())
        {
            return false;
        }

        HashSet<Vector2Int> occupiedSlots =
            new HashSet<Vector2Int>();

        List<Vector2Int> nearStructureCandidates =
            new List<Vector2Int>();

        List<Vector2Int> edgeCandidates =
            new List<Vector2Int>();

        List<Vector2Int> floorCandidates =
            new List<Vector2Int>();

        Vector2Int size =
            roomTemplate.SizeInCells;

        for (int x = 1;
             x < size.x - 1;
             x++)
        {
            for (int y = 1;
                 y < size.y - 1;
                 y++)
            {
                Vector2Int cell =
                    new Vector2Int(x, y);

                if (blocked.Contains(cell) ||
                    doorClearanceCells.Contains(cell) ||
                    !roomTemplate.IsWalkableCell(cell))
                {
                    continue;
                }

                bool nearStructure =
                    IsAdjacentToBlocked(
                        cell,
                        blocked);

                bool nearEdge =
                    x == 1 ||
                    y == 1 ||
                    x == size.x - 2 ||
                    y == size.y - 2;

                if (nearStructure)
                {
                    nearStructureCandidates.Add(cell);
                }
                else if (nearEdge)
                {
                    edgeCandidates.Add(cell);
                }
                else
                {
                    floorCandidates.Add(cell);
                }
            }
        }

        System.Random random =
            new System.Random(
                unchecked(
                    placement.RuntimeProceduralSeed ^
                    0x51ED270B));

        DeterministicShuffle(
            nearStructureCandidates,
            random);

        DeterministicShuffle(
            edgeCandidates,
            random);

        DeterministicShuffle(
            floorCandidates,
            random);

        slots.Clear();

        int targetNear =
            theme == null
                ? 3
                : theme.NearStructureCount;

        int targetEdge =
            theme == null
                ? 2
                : theme.EdgePropCount;

        int targetFloor =
            theme == null
                ? 4
                : theme.FloorClutterCount;

        int targetForeground =
            theme == null
                ? 2
                : theme.ForegroundPropCount;

        AddSlots(
            nearStructureCandidates,
            targetNear,
            DreamProceduralDecorCategoryP1012A2.NearStructure,
            occupiedSlots);

        AddSlots(
            edgeCandidates,
            targetEdge,
            DreamProceduralDecorCategoryP1012A2.EdgeProp,
            occupiedSlots);

        AddSlots(
            floorCandidates,
            targetFloor,
            DreamProceduralDecorCategoryP1012A2.FloorClutter,
            occupiedSlots);

        // Foreground 优先取剩余 NearStructure，若不足再从剩余 interior 补。
        List<Vector2Int> foregroundCandidates =
            new List<Vector2Int>();

        AppendUnused(
            nearStructureCandidates,
            occupiedSlots,
            foregroundCandidates);

        AppendUnused(
            edgeCandidates,
            occupiedSlots,
            foregroundCandidates);

        AppendUnused(
            floorCandidates,
            occupiedSlots,
            foregroundCandidates);

        DeterministicShuffle(
            foregroundCandidates,
            random);

        AddSlots(
            foregroundCandidates,
            targetForeground,
            DreamProceduralDecorCategoryP1012A2.ForegroundProp,
            occupiedSlots);

        // P10.12B-2.1：
        // Small 08x06 + NEWS 四门全开时，门内 2 格安全带 + R2B Hard Structure
        // 可能合法地吃掉全部 Soft Decor 候选。
        //
        // Soft Decor 是纯视觉层，不拥有 Collider / FloorCells / A*。
        // 因此“0 个合法 Slot”应该视为 Graceful Degrade，而不是房间失败。
        // 仍然创建空的 RuntimeSoftDecor Root 并 Commit，
        // 让权威玩法保持完整，同时明确记录本局因空间不足抑制软装饰。
        bool suppressedBySpace =
            slots.Count == 0;

        Transform oldRoot =
            transform.Find(DecorRootName);

        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        GameObject rootObject =
            new GameObject(DecorRootName);

        Transform root =
            rootObject.transform;

        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Transform normalRoot =
            CreateChildRoot(
                NormalRootName,
                root);

        Transform foregroundRoot =
            CreateChildRoot(
                ForegroundRootName,
                root);

        int rendererCount = 0;

        for (int i = 0;
             i < slots.Count;
             i++)
        {
            DreamProceduralDecorSlotP1012A2 slot =
                slots[i];

            Transform parent =
                slot.Category ==
                    DreamProceduralDecorCategoryP1012A2.ForegroundProp
                    ? foregroundRoot
                    : normalRoot;

            GameObject prop =
                new GameObject(
                    slot.Category +
                    "_" +
                    slot.LocalCell.x +
                    "_" +
                    slot.LocalCell.y);

            prop.transform.SetParent(
                parent,
                false);

            Vector3 localPosition =
                roomTemplate.GetLocalCellCenter(
                    slot.LocalCell);

            float jitter =
                theme == null
                    ? 0.12f
                    : theme.PositionJitter;

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

            prop.transform.localPosition =
                localPosition;

            prop.transform.localRotation =
                Quaternion.identity;

            float scale =
                theme == null
                    ? GetFallbackScale(slot.Category)
                    : theme.GetScale(slot.Category);

            prop.transform.localScale =
                new Vector3(
                    scale,
                    scale,
                    1f);

            SpriteRenderer renderer =
                prop.AddComponent<SpriteRenderer>();

            Sprite selectedSprite =
                theme == null
                    ? null
                    : theme.PickSprite(
                        slot.Category,
                        random);

            renderer.sprite =
                selectedSprite != null
                    ? selectedSprite
                    : GetFallbackSprite();

            renderer.color =
                selectedSprite != null
                    ? Color.white
                    : GetFallbackColor(
                        slot.Category);

            renderer.sortingOrder =
                theme == null
                    ? GetFallbackSortingOrder(
                        slot.Category)
                    : theme.GetSortingOrder(
                        slot.Category);

            rendererCount++;
        }

        if (GetComponentsInChildren<Collider2D>(
                true).Length <
            placement.RuntimeProceduralBlockedCellCount)
        {
            return Fail(
                "房间现有 Collider 状态异常，Soft Decor 拒绝继续。");
        }

        // 只检查 Soft Decor Root 自身绝对没有 Collider。
        Collider2D[] softColliders =
            root.GetComponentsInChildren<
                Collider2D>(
                    true);

        if (softColliders.Length != 0)
        {
            return Fail(
                "Soft Decor Root 意外产生 Collider2D：" +
                softColliders.Length);
        }

        committed = true;

        Debug.Log(
            "[P10.12A-2] Soft Decor COMMIT" +
            "\nRoomIndex=" + roomIndex +
            " | TemplateId=" +
            roomTemplate.TemplateId +
            " | ProceduralSeed=" +
            placement.RuntimeProceduralSeed +
            " | Theme=" + ThemeId +
            "\nSlots=" + slots.Count +
            " | Renderers=" + rendererCount +
            " | SuppressedBySpace=" +
            suppressedBySpace +
            " | Floor=" +
            CountCategory(
                DreamProceduralDecorCategoryP1012A2.FloorClutter) +
            " | Edge=" +
            CountCategory(
                DreamProceduralDecorCategoryP1012A2.EdgeProp) +
            " | NearStructure=" +
            CountCategory(
                DreamProceduralDecorCategoryP1012A2.NearStructure) +
            " | Foreground=" +
            CountCategory(
                DreamProceduralDecorCategoryP1012A2.ForegroundProp) +
            "\nSoftColliderCount=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=ThemeSpritePoolsOnly",
            this);

        return true;
    }

    public bool IsDoorClearanceCell(
        Vector2Int cell)
    {
        return doorClearanceCells.Contains(cell);
    }

    public int CountCategory(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        int count = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Category == category)
            {
                count++;
            }
        }

        return count;
    }

    private bool BuildDoorClearance()
    {
        doorClearanceCells.Clear();

        IReadOnlyList<DreamRoomDoorSocket> sockets =
            roomTemplate.DoorSockets;

        if (sockets == null)
        {
            return Fail(
                "DoorSockets 为空。");
        }

        int openCount = 0;

        for (int i = 0; i < sockets.Count; i++)
        {
            DreamRoomDoorSocket socket =
                sockets[i];

            if (socket == null ||
                !socket.IsOpen)
            {
                continue;
            }

            openCount++;

            Vector2Int inward =
                GetInwardOffset(
                    socket.Direction);

            List<Vector2Int> cells =
                socket.GetLocalInsideCells();

            for (int c = 0;
                 c < cells.Count;
                 c++)
            {
                Vector2Int cell =
                    cells[c];

                doorClearanceCells.Add(cell);
                doorClearanceCells.Add(
                    cell + inward);
                doorClearanceCells.Add(
                    cell + inward * 2);
            }
        }

        if (openCount == 0)
        {
            return Fail(
                "Soft Decor Start 时没有任何 Open Socket。" +
                " 说明 Renderer 门提交时序与 A2 契约不一致。");
        }

        return true;
    }

    private static bool IsAdjacentToBlocked(
        Vector2Int cell,
        HashSet<Vector2Int> blocked)
    {
        for (int i = 0;
             i < CardinalDirections.Length;
             i++)
        {
            if (blocked.Contains(
                    cell +
                    CardinalDirections[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void AddSlots(
        List<Vector2Int> candidates,
        int targetCount,
        DreamProceduralDecorCategoryP1012A2 category,
        HashSet<Vector2Int> occupied)
    {
        int added = 0;

        for (int i = 0;
             i < candidates.Count &&
             added < targetCount;
             i++)
        {
            Vector2Int cell =
                candidates[i];

            if (!occupied.Add(cell))
            {
                continue;
            }

            slots.Add(
                new DreamProceduralDecorSlotP1012A2(
                    cell,
                    category));

            added++;
        }
    }

    private static void AppendUnused(
        List<Vector2Int> source,
        HashSet<Vector2Int> occupied,
        List<Vector2Int> target)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (!occupied.Contains(source[i]))
            {
                target.Add(source[i]);
            }
        }
    }

    private static void DeterministicShuffle(
        List<Vector2Int> list,
        System.Random random)
    {
        for (int i = list.Count - 1;
             i > 0;
             i--)
        {
            int swap =
                random.Next(0, i + 1);

            Vector2Int temp =
                list[i];

            list[i] =
                list[swap];

            list[swap] =
                temp;
        }
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

    private static Vector2Int GetInwardOffset(
        DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                return Vector2Int.down;

            case DreamRoomDoorDirection.East:
                return Vector2Int.left;

            case DreamRoomDoorDirection.South:
                return Vector2Int.up;

            case DreamRoomDoorDirection.West:
                return Vector2Int.right;

            default:
                return Vector2Int.zero;
        }
    }

    private static Transform CreateChildRoot(
        string name,
        Transform parent)
    {
        GameObject child =
            new GameObject(name);

        Transform childTransform =
            child.transform;

        childTransform.SetParent(
            parent,
            false);

        childTransform.localPosition =
            Vector3.zero;

        childTransform.localRotation =
            Quaternion.identity;

        childTransform.localScale =
            Vector3.one;

        return childTransform;
    }

    private bool Fail(
        string reason)
    {
        lastFailureReason =
            string.IsNullOrWhiteSpace(reason)
                ? "Unknown failure."
                : reason;

        Debug.LogError(
            "[P10.12A-2] Soft Decor FAILED" +
            "\nRoomIndex=" + roomIndex +
            "\n" + lastFailureReason +
            "\nBlockedCellsChanged=False" +
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
            "P10_12A2_SoftDecorDebugTexture";

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
            "P10_12A2_SoftDecorDebugSprite";

        fallbackSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return fallbackSprite;
    }

    private static Color GetFallbackColor(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return new Color(
                    0.40f,
                    0.82f,
                    1f,
                    0.72f);

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return new Color(
                    1f,
                    0.82f,
                    0.25f,
                    0.72f);

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return new Color(
                    0.50f,
                    1f,
                    0.45f,
                    0.72f);

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return new Color(
                    0.95f,
                    0.45f,
                    1f,
                    0.78f);

            default:
                return Color.white;
        }
    }

    private static float GetFallbackScale(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        switch (category)
        {
            case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                return 0.34f;

            case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                return 0.48f;

            case DreamProceduralDecorCategoryP1012A2.NearStructure:
                return 0.42f;

            case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                return 0.58f;

            default:
                return 0.4f;
        }
    }

    private static int GetFallbackSortingOrder(
        DreamProceduralDecorCategoryP1012A2 category)
    {
        return
            category ==
                DreamProceduralDecorCategoryP1012A2.ForegroundProp
                ? 30
                : 2;
    }
}

[Serializable]
public readonly struct DreamProceduralDecorSlotP1012A2
{
    public Vector2Int LocalCell { get; }
    public DreamProceduralDecorCategoryP1012A2 Category { get; }

    public DreamProceduralDecorSlotP1012A2(
        Vector2Int localCell,
        DreamProceduralDecorCategoryP1012A2 category)
    {
        LocalCell = localCell;
        Category = category;
    }
}
