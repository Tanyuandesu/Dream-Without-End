using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcCollisionMode
{
    Solid = 0,
    Ghost = 1
}

[Serializable]
public sealed class NpcDialogueEntry
{
    [SerializeField] private string conversationId = "npc_dialogue";
    [Min(1)]
    [SerializeField] private int randomWeight = 1;

    [Header("条件")]
    [Min(1)]
    [SerializeField] private int minimumFloor = 1;
    [Tooltip("0 表示不限制最高楼层。")]
    [Min(0)]
    [SerializeField] private int maximumFloor;
    [Min(0)]
    [SerializeField] private int minimumCollectedItemCount;
    [Tooltip("-1 表示不限制最多道具数量。")]
    [SerializeField] private int maximumCollectedItemCount = -1;
    [SerializeField] private List<string> requiredItemIds = new List<string>();
    [SerializeField] private List<string> excludedItemIds = new List<string>();

    [Header("内容")]
    [Tooltip("按顺序播放的 Localization Key。")]
    [SerializeField] private List<string> lineKeys = new List<string>();

    [Header("可选：终局事件")]
    [Tooltip(
        "留空表示普通对话。非空时，对话完成后把该 Event Flag 交给 EndingResolver。" +
        "只有 Final Legacy 中的 NPC 对话会消费该字段。")]
    [SerializeField] private string endingEventFlag = string.Empty;

    public string ConversationId => conversationId;
    public int RandomWeight => Mathf.Max(1, randomWeight);
    public IReadOnlyList<string> LineKeys => lineKeys;
    public string EndingEventFlag => endingEventFlag ?? string.Empty;

    public bool IsEligible(int floorNumber, ItemManager itemManager)
    {
        if (floorNumber < Mathf.Max(1, minimumFloor))
        {
            return false;
        }

        if (maximumFloor > 0 && floorNumber > maximumFloor)
        {
            return false;
        }

        int itemCount = itemManager != null
            ? itemManager.CollectedItemCount
            : 0;

        if (itemCount < Mathf.Max(0, minimumCollectedItemCount))
        {
            return false;
        }

        if (maximumCollectedItemCount >= 0 &&
            itemCount > maximumCollectedItemCount)
        {
            return false;
        }

        if (itemManager != null)
        {
            for (int i = 0; i < requiredItemIds.Count; i++)
            {
                string itemId = requiredItemIds[i];
                if (!string.IsNullOrWhiteSpace(itemId) &&
                    !itemManager.HasCollected(itemId))
                {
                    return false;
                }
            }

            for (int i = 0; i < excludedItemIds.Count; i++)
            {
                string itemId = excludedItemIds[i];
                if (!string.IsNullOrWhiteSpace(itemId) &&
                    itemManager.HasCollected(itemId))
                {
                    return false;
                }
            }
        }

        return lineKeys != null && lineKeys.Count > 0;
    }

    internal static NpcDialogueEntry CreateRuntime(
        string id,
        params string[] localizationKeys)
    {
        NpcDialogueEntry entry = new NpcDialogueEntry
        {
            conversationId = id,
            randomWeight = 1,
            minimumFloor = 1,
            maximumFloor = 0,
            minimumCollectedItemCount = 0,
            maximumCollectedItemCount = -1,
            requiredItemIds = new List<string>(),
            excludedItemIds = new List<string>(),
            lineKeys = new List<string>(localizationKeys ?? Array.Empty<string>()),
            endingEventFlag = string.Empty
        };

        return entry;
    }

    internal static NpcDialogueEntry CreateRuntimeEnding(
        string id,
        string eventFlag,
        params string[] localizationKeys)
    {
        NpcDialogueEntry entry =
            CreateRuntime(id, localizationKeys);
        entry.endingEventFlag = eventFlag ?? string.Empty;
        return entry;
    }
}

[Serializable]
public sealed class NpcRoomDialoguePool
{
    [Tooltip("必须与 DreamRoomTemplate.TemplateId 完全一致。")]
    [SerializeField] private string roomTemplateId = string.Empty;
    [SerializeField] private List<NpcDialogueEntry> entries =
        new List<NpcDialogueEntry>();

    public string RoomTemplateId => roomTemplateId ?? string.Empty;
    public IReadOnlyList<NpcDialogueEntry> Entries => entries;

    internal static NpcRoomDialoguePool CreateRuntime(
        string templateId,
        params NpcDialogueEntry[] dialogueEntries)
    {
        return new NpcRoomDialoguePool
        {
            roomTemplateId = templateId,
            entries = new List<NpcDialogueEntry>(
                dialogueEntries ?? Array.Empty<NpcDialogueEntry>())
        };
    }
}

/// <summary>
/// SYS14 NPC content definition.
///
/// NPC is intentionally not an Enemy. This asset only describes appearance,
/// collision, wandering and dialogue eligibility. Generated map state and
/// ordinary dialogue history are never persisted.
/// </summary>
[CreateAssetMenu(
    fileName = "NpcDefinition",
    menuName = "Dream Dungeon/NPC/NPC Definition")]
public sealed class NpcDefinition : ScriptableObject
{
    [Header("身份")]
    [SerializeField] private string npcId = "npc_unknown";
    [SerializeField] private string displayNameKey = "NPC_UNKNOWN_NAME";
    [SerializeField] private bool firstEncounterNpc;
    [Min(1)]
    [SerializeField] private int regularSpawnWeight = 1;

    [Header("外观")]
    [Tooltip("可选。这里只作为视觉子物体实例化，不接管 Runtime 物理。")]
    [SerializeField] private GameObject visualPrefab;
    [SerializeField] private Color fallbackColor =
        new Color(0.72f, 0.82f, 0.95f, 0.92f);
    [SerializeField] private Vector2 fallbackVisualSize =
        new Vector2(0.68f, 0.92f);
    [SerializeField] private int sortingOrder = 18;

    [Header("交互与碰撞")]
    [SerializeField] private NpcCollisionMode collisionMode =
        NpcCollisionMode.Solid;
    [Min(0.2f)]
    [SerializeField] private float interactionRadius = 1.35f;
    [SerializeField] private Vector2 bodyColliderSize =
        new Vector2(0.52f, 0.58f);
    [SerializeField] private Vector2 bodyColliderOffset =
        new Vector2(0f, -0.08f);

    [Header("随机巡游")]
    [Min(0.05f)]
    [SerializeField] private float moveSpeed = 1.4f;
    [Min(0f)]
    [SerializeField] private float idleTimeMin = 0.7f;
    [Min(0f)]
    [SerializeField] private float idleTimeMax = 2.2f;

    [Header("第一次遭遇")]
    [SerializeField] private List<NpcDialogueEntry> firstEncounterDialogues =
        new List<NpcDialogueEntry>();

    [Header("普通高精度房间对话池")]
    [SerializeField] private List<NpcRoomDialoguePool> roomDialoguePools =
        new List<NpcRoomDialoguePool>();

    [Header("Final Legacy")]
    [SerializeField] private bool appearsInFinalLegacy;
    [Min(1)]
    [SerializeField] private int finalLegacyWanderRadiusInCells = 4;
    [SerializeField] private List<NpcDialogueEntry> finalLegacyDialogues =
        new List<NpcDialogueEntry>();

    public string NpcId => npcId ?? string.Empty;
    public string DisplayNameKey => displayNameKey ?? string.Empty;
    public bool IsFirstEncounterNpc => firstEncounterNpc;
    public int RegularSpawnWeight => Mathf.Max(1, regularSpawnWeight);
    public GameObject VisualPrefab => visualPrefab;
    public Color FallbackColor => fallbackColor;
    public Vector2 FallbackVisualSize => fallbackVisualSize;
    public int SortingOrder => sortingOrder;
    public NpcCollisionMode CollisionMode => collisionMode;
    public float InteractionRadius => Mathf.Max(0.2f, interactionRadius);
    public Vector2 BodyColliderSize => bodyColliderSize;
    public Vector2 BodyColliderOffset => bodyColliderOffset;
    public float MoveSpeed => Mathf.Max(0.05f, moveSpeed);
    public float IdleTimeMin => Mathf.Max(0f, idleTimeMin);
    public float IdleTimeMax => Mathf.Max(IdleTimeMin, idleTimeMax);
    public IReadOnlyList<NpcDialogueEntry> FirstEncounterDialogues =>
        firstEncounterDialogues;
    public IReadOnlyList<NpcRoomDialoguePool> RoomDialoguePools =>
        roomDialoguePools;
    public bool AppearsInFinalLegacy => appearsInFinalLegacy;
    public int FinalLegacyWanderRadiusInCells =>
        Mathf.Max(1, finalLegacyWanderRadiusInCells);
    public IReadOnlyList<NpcDialogueEntry> FinalLegacyDialogues =>
        finalLegacyDialogues;

    public bool TryGetRoomDialoguePool(
        string templateId,
        out NpcRoomDialoguePool pool)
    {
        pool = null;

        if (string.IsNullOrWhiteSpace(templateId) ||
            roomDialoguePools == null)
        {
            return false;
        }

        string normalized = templateId.Trim();

        for (int i = 0; i < roomDialoguePools.Count; i++)
        {
            NpcRoomDialoguePool candidate = roomDialoguePools[i];
            if (candidate != null &&
                string.Equals(
                    candidate.RoomTemplateId,
                    normalized,
                    StringComparison.Ordinal))
            {
                pool = candidate;
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        npcId = string.IsNullOrWhiteSpace(npcId)
            ? name
            : npcId.Trim();
        displayNameKey = displayNameKey == null
            ? string.Empty
            : displayNameKey.Trim();
        regularSpawnWeight = Mathf.Max(1, regularSpawnWeight);
        interactionRadius = Mathf.Max(0.2f, interactionRadius);
        bodyColliderSize.x = Mathf.Max(0.05f, bodyColliderSize.x);
        bodyColliderSize.y = Mathf.Max(0.05f, bodyColliderSize.y);
        moveSpeed = Mathf.Max(0.05f, moveSpeed);
        idleTimeMin = Mathf.Max(0f, idleTimeMin);
        idleTimeMax = Mathf.Max(idleTimeMin, idleTimeMax);
        finalLegacyWanderRadiusInCells =
            Mathf.Max(1, finalLegacyWanderRadiusInCells);
    }

    /// <summary>
    /// Development-only fallback used until authored NPC assets are added.
    /// It is never saved as an asset and disappears automatically as soon as
    /// Resources/DreamDungeon/NPC contains at least one NpcDefinition.
    /// </summary>
    public static NpcDefinition CreateDevelopmentFallback()
    {
        NpcDefinition definition = CreateInstance<NpcDefinition>();
        definition.name = "SYS14_DevelopmentNpc";
        definition.npcId = "sys14_dev_npc";
        definition.displayNameKey = "NPC_SYS14_DEV_NAME";
        definition.firstEncounterNpc = true;
        definition.collisionMode = NpcCollisionMode.Ghost;
        definition.appearsInFinalLegacy = true;

        definition.firstEncounterDialogues =
            new List<NpcDialogueEntry>
            {
                NpcDialogueEntry.CreateRuntime(
                    "sys14_first",
                    "NPC_SYS14_FIRST_01",
                    "NPC_SYS14_FIRST_02")
            };

        definition.roomDialoguePools =
            new List<NpcRoomDialoguePool>
            {
                NpcRoomDialoguePool.CreateRuntime(
                    "Production_Classroom_01",
                    NpcDialogueEntry.CreateRuntime(
                        "sys14_classroom",
                        "NPC_SYS14_CLASSROOM_01")),
                NpcRoomDialoguePool.CreateRuntime(
                    "Production_Crossroad_01",
                    NpcDialogueEntry.CreateRuntime(
                        "sys14_crossroad",
                        "NPC_SYS14_CROSSROAD_01")),
                NpcRoomDialoguePool.CreateRuntime(
                    "Production_MusicRoom_01",
                    NpcDialogueEntry.CreateRuntime(
                        "sys14_musicroom",
                        "NPC_SYS14_MUSICROOM_01"))
            };

        definition.finalLegacyDialogues =
            new List<NpcDialogueEntry>
            {
                NpcDialogueEntry.CreateRuntimeEnding(
                    "sys14_final",
                    EndingResolver.NpcFinalDialogueEventFlag,
                    "NPC_SYS14_FINAL_01")
            };

        return definition;
    }
}
