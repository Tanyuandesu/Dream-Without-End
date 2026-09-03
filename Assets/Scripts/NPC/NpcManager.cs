using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SYS14 authoritative NPC runtime manager.
///
/// Persistence boundary:
/// - Saves only FirstEncounterCompleted because it changes future spawn rules.
/// - Never saves NPC position, room, wander target or ordinary dialogue history.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcDialogueController))]
public sealed class NpcManager : MonoBehaviour
{
    private const string ResourcePath = "DreamDungeon/NPC";
    private const int RegularSelectionSalt = 140101;
    private const int FirstEncounterSelectionSalt = 140102;
    private const int FinalLegacySelectionSalt = 140103;
    private const int RecentConversationCapacity = 3;

    private readonly List<NpcDefinition> definitions =
        new List<NpcDefinition>();
    private readonly Queue<string> recentConversationIds =
        new Queue<string>();

    private NpcDialogueController dialogueController;
    private NpcRuntimeController activeNpc;
    private Transform player;
    private ItemManager itemManager;
    private GameManager gameManager;
    private int currentFloor;
    private int floorSeed;
    private int dialogueSerial;
    private bool definitionsLoaded;
    private bool usingDevelopmentFallback;
    private bool requireInteractRelease;

    public bool FirstEncounterCompleted { get; private set; }
    public NpcRuntimeController ActiveNpc => activeNpc;
    public bool UsingDevelopmentFallback => usingDevelopmentFallback;

    private void Awake()
    {
        dialogueController = GetComponent<NpcDialogueController>();
        LoadDefinitions();
    }

    private void Update()
    {
        if (activeNpc == null ||
            player == null ||
            dialogueController == null ||
            dialogueController.IsDialogueActive)
        {
            if (dialogueController != null &&
                !dialogueController.IsDialogueActive)
            {
                dialogueController.ShowInteractionPrompt(false);
            }
            return;
        }

        // One E press must never both close a dialogue and immediately
        // start the next one in the same frame.  Once a dialogue has
        // started, require a physical E-key release before interaction
        // can be armed again.  This is deliberately held across the
        // whole dialogue so script execution order cannot reintroduce
        // same-frame dialogue re-entry.
        if (requireInteractRelease)
        {
            dialogueController.ShowInteractionPrompt(false);

            if (!Input.GetKey(KeyCode.E))
            {
                requireInteractRelease = false;
            }

            return;
        }

        GameFlowManager flow = GameFlowManager.Instance;
        bool canInteract =
            flow == null || flow.State == GameFlowState.Playing;

        float radius = activeNpc.Definition != null
            ? activeNpc.Definition.InteractionRadius
            : 1.35f;

        float distanceSquared =
            ((Vector2)player.position -
             (Vector2)activeNpc.transform.position).sqrMagnitude;

        bool inRange =
            canInteract &&
            distanceSquared <= radius * radius;

        dialogueController.ShowInteractionPrompt(inRange);

        if (inRange && Input.GetKeyDown(KeyCode.E))
        {
            TryBeginActiveNpcDialogue();
        }
    }

    public void BindGameManager(GameManager owner)
    {
        gameManager = owner;
    }

    public void ResetRunState()
    {
        FirstEncounterCompleted = false;
        dialogueSerial = 0;
        recentConversationIds.Clear();
        ClearFloor();
    }

    public void RestoreRunState(bool firstEncounterCompleted)
    {
        FirstEncounterCompleted = firstEncounterCompleted;
        dialogueSerial = 0;
        recentConversationIds.Clear();
        ClearFloor();
    }

    public void SetupFloor(
        int floorNumber,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform,
        ItemManager items,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        ClearFloor();
        LoadDefinitions();

        currentFloor = floorNumber;
        floorSeed = layout != null ? layout.Seed : 0;
        player = playerTransform;
        itemManager = items;

        if (layout == null ||
            floorRoot == null ||
            renderer == null ||
            playerTransform == null ||
            definitions.Count == 0)
        {
            return;
        }

        int firstGuaranteedFloor =
            itemManager != null
                ? itemManager.FirstGuaranteedFloor
                : 2;

        if (!FirstEncounterCompleted &&
            floorNumber >= firstGuaranteedFloor &&
            TrySpawnFirstEncounter(
                layout,
                floorRoot,
                renderer,
                runtimeSpawnReservations))
        {
            return;
        }

        if (!FirstEncounterCompleted)
        {
            return;
        }

        TrySpawnRegularNpc(
            layout,
            floorRoot,
            renderer,
            runtimeSpawnReservations);
    }

    public void SetupFinalLegacy(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        Transform playerTransform,
        ItemManager items,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        ClearFloor();
        LoadDefinitions();

        player = playerTransform;
        itemManager = items;
        currentFloor = gameManager != null
            ? Mathf.Max(1, gameManager.CurrentFloor)
            : 1;
        floorSeed = layout != null ? layout.Seed : 0;

        if (layout == null ||
            floorRoot == null ||
            renderer == null ||
            playerTransform == null)
        {
            return;
        }

        NpcDefinition definition =
            SelectFinalLegacyDefinition(layout.Seed);

        if (definition == null ||
            definition.FinalLegacyDialogues == null ||
            definition.FinalLegacyDialogues.Count == 0)
        {
            return;
        }

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Npc,
                allowedRoomIndices: null,
                selectionSalt: CombineSeed(
                    FinalLegacySelectionSalt,
                    layout.Seed,
                    StableHash(definition.NpcId)),
                reservedCells: runtimeSpawnReservations,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 2,
                minimumDistanceFromExit: 2,
                preferredCell: layout.ExitCell,
                allowWalkableFallback: true,
                allowLayoutWideFallback: true);

        if (!DungeonSpawnCellResolver.TryResolve(
                request,
                out DungeonSpawnCellResult result,
                out string failureReason))
        {
            Debug.LogWarning(
                "[SYS14] Final Legacy NPC spawn rejected | " + failureReason,
                this);
            return;
        }

        HashSet<Vector2Int> wanderCells =
            CollectFinalLegacyWanderCells(
                layout,
                result.Cell,
                definition.FinalLegacyWanderRadiusInCells);

        SpawnNpc(
            definition,
            layout,
            floorRoot,
            renderer,
            result,
            wanderCells,
            true,
            false,
            true,
            string.Empty,
            runtimeSpawnReservations);
    }

    public void IgnoreEnemyCollisions(
        IReadOnlyList<GameObject> enemies)
    {
        if (activeNpc == null ||
            activeNpc.BodyCollider == null ||
            !activeNpc.BodyCollider.enabled ||
            enemies == null)
        {
            return;
        }

        Collider2D npcCollider = activeNpc.BodyCollider;

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            Collider2D[] enemyColliders =
                enemy.GetComponentsInChildren<Collider2D>(true);

            for (int j = 0; j < enemyColliders.Length; j++)
            {
                Collider2D enemyCollider = enemyColliders[j];
                if (enemyCollider != null)
                {
                    Physics2D.IgnoreCollision(
                        npcCollider,
                        enemyCollider,
                        true);
                }
            }
        }
    }

    public void ClearFloor()
    {
        if (dialogueController != null)
        {
            dialogueController.CancelDialogue();
            dialogueController.ShowInteractionPrompt(false);
        }

        activeNpc = null;
        player = null;
        itemManager = null;
        recentConversationIds.Clear();
    }

    private bool TrySpawnFirstEncounter(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        NpcDefinition definition = SelectFirstEncounterDefinition();
        if (definition == null)
        {
            return false;
        }

        DungeonSpawnCellResult itemResult =
            itemManager != null
                ? itemManager.ActiveSpawnResult
                : null;

        List<int> roomScope = new List<int>();
        Vector2Int? preferredCell = null;

        if (itemResult != null && itemResult.RoomIndex >= 0)
        {
            roomScope.Add(itemResult.RoomIndex);
            preferredCell = itemResult.Cell;
        }
        else
        {
            DungeonCoreItemRoomScopeR943
                .CollectCandidateRoomIndices(
                    layout,
                    roomScope);
        }

        if (roomScope.Count == 0)
        {
            return false;
        }

        int roomIndex = roomScope[0];

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Npc,
                allowedRoomIndices: new[] { roomIndex },
                selectionSalt: CombineSeed(
                    FirstEncounterSelectionSalt,
                    layout.Seed,
                    StableHash(definition.NpcId)),
                reservedCells: runtimeSpawnReservations,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 1,
                minimumDistanceFromExit: 1,
                preferredCell: preferredCell,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        if (!DungeonSpawnCellResolver.TryResolve(
                request,
                out DungeonSpawnCellResult result,
                out string failureReason))
        {
            Debug.LogWarning(
                "[SYS14] First Encounter NPC spawn rejected | " + failureReason,
                this);
            return false;
        }

        HashSet<Vector2Int> wanderCells =
            CollectRoomWanderCells(layout, result.RoomIndex);

        string roomTemplateId =
            TryGetRoomTemplateId(layout, result.RoomIndex);

        SpawnNpc(
            definition,
            layout,
            floorRoot,
            renderer,
            result,
            wanderCells,
            false,
            true,
            false,
            roomTemplateId,
            runtimeSpawnReservations);

        return activeNpc != null;
    }

    private bool TrySpawnRegularNpc(
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        List<RegularSpawnCandidate> candidates =
            BuildRegularSpawnCandidates(layout);

        if (candidates.Count == 0)
        {
            return false;
        }

        RegularSpawnCandidate selected =
            SelectWeightedRegularCandidate(
                candidates,
                CombineSeed(
                    RegularSelectionSalt,
                    layout.Seed,
                    currentFloor));

        if (selected == null)
        {
            return false;
        }

        DungeonSpawnCellRequest request =
            new DungeonSpawnCellRequest(
                layout,
                DreamRoomSpawnPointKind.Npc,
                allowedRoomIndices: new[] { selected.RoomIndex },
                selectionSalt: CombineSeed(
                    RegularSelectionSalt,
                    layout.Seed,
                    StableHash(selected.Definition.NpcId)),
                reservedCells: runtimeSpawnReservations,
                excludeStartCell: true,
                excludeExitCell: true,
                minimumDistanceFromStart: 1,
                minimumDistanceFromExit: 1,
                allowWalkableFallback: true,
                allowLayoutWideFallback: false);

        if (!DungeonSpawnCellResolver.TryResolve(
                request,
                out DungeonSpawnCellResult result,
                out string failureReason))
        {
            Debug.LogWarning(
                "[SYS14] Regular NPC spawn rejected | " + failureReason,
                this);
            return false;
        }

        HashSet<Vector2Int> wanderCells =
            CollectRoomWanderCells(layout, result.RoomIndex);

        SpawnNpc(
            selected.Definition,
            layout,
            floorRoot,
            renderer,
            result,
            wanderCells,
            true,
            false,
            false,
            selected.RoomTemplateId,
            runtimeSpawnReservations);

        return activeNpc != null;
    }

    private void SpawnNpc(
        NpcDefinition definition,
        DungeonLayout layout,
        Transform floorRoot,
        DungeonRenderer renderer,
        DungeonSpawnCellResult result,
        IEnumerable<Vector2Int> wanderCells,
        bool enableWander,
        bool isFirstEncounter,
        bool isFinalLegacy,
        string roomTemplateId,
        ISet<Vector2Int> runtimeSpawnReservations)
    {
        if (definition == null || result == null)
        {
            return;
        }

        GameObject root = new GameObject(
            "NPC_" + definition.NpcId);
        root.transform.SetParent(floorRoot, false);
        root.transform.position = renderer.CellToWorld(result.Cell);

        CreateVisual(definition, root.transform);

        activeNpc = root.AddComponent<NpcRuntimeController>();
        activeNpc.Initialize(
            definition,
            renderer,
            result.Cell,
            wanderCells,
            enableWander,
            isFirstEncounter,
            isFinalLegacy,
            roomTemplateId,
            CombineSeed(
                layout != null ? layout.Seed : 0,
                StableHash(definition.NpcId),
                result.RoomIndex));

        runtimeSpawnReservations?.Add(result.Cell);

        Debug.Log(
            "[SYS14] NPC spawned" +
            " | NpcId=" + definition.NpcId +
            " | Floor=" + currentFloor +
            " | First=" + isFirstEncounter +
            " | FinalLegacy=" + isFinalLegacy +
            " | RoomIndex=" + result.RoomIndex +
            " | RoomTemplate=" +
            (string.IsNullOrEmpty(roomTemplateId)
                ? "None"
                : roomTemplateId) +
            " | Cell=" + result.Cell +
            " | Wander=" + enableWander +
            " | Collision=" + definition.CollisionMode,
            this);
    }

    private void CreateVisual(
        NpcDefinition definition,
        Transform root)
    {
        if (definition.VisualPrefab != null)
        {
            GameObject visual = Instantiate(
                definition.VisualPrefab,
                root);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Collider2D[] visualColliders =
                visual.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < visualColliders.Length; i++)
            {
                visualColliders[i].enabled = false;
            }

            Rigidbody2D[] visualBodies =
                visual.GetComponentsInChildren<Rigidbody2D>(true);

            for (int i = 0; i < visualBodies.Length; i++)
            {
                visualBodies[i].simulated = false;
            }
            return;
        }

        ProceduralSpriteUtility.CreateRect(
            "Visual_Fallback",
            root,
            Vector3.zero,
            definition.FallbackVisualSize,
            definition.FallbackColor,
            definition.SortingOrder);
    }

    private void TryBeginActiveNpcDialogue()
    {
        if (activeNpc == null || dialogueController == null)
        {
            return;
        }

        NpcDialogueEntry entry =
            SelectDialogueForActiveNpc();

        if (entry == null)
        {
            Debug.LogWarning(
                "[SYS14] NPC has no eligible dialogue" +
                " | NpcId=" + activeNpc.Definition.NpcId +
                " | Room=" + activeNpc.RoomTemplateId +
                " | Floor=" + currentFloor,
                this);
            return;
        }

        NpcRuntimeController npcAtStart = activeNpc;

        if (!dialogueController.TryBeginDialogue(
                activeNpc.Definition,
                entry,
                () => HandleDialogueCompleted(
                    npcAtStart,
                    entry)))
        {
            return;
        }

        requireInteractRelease = true;
        RememberConversation(entry.ConversationId);
        dialogueSerial++;
    }

    private void HandleDialogueCompleted(
        NpcRuntimeController npc,
        NpcDialogueEntry entry)
    {
        if (npc == null || entry == null)
        {
            return;
        }

        if (npc.IsFirstEncounterInstance &&
            !FirstEncounterCompleted)
        {
            FirstEncounterCompleted = true;
            npc.SetWanderEnabled(true);

            Debug.Log(
                "[SYS14] First Encounter completed" +
                " | NpcId=" + npc.Definition.NpcId +
                " | PersistentFlag=True",
                this);
        }

        if (npc.IsFinalLegacyInstance &&
            !string.IsNullOrWhiteSpace(entry.EndingEventFlag) &&
            gameManager != null)
        {
            gameManager.TryFinishFinalLegacyRunFromNpc(
                entry.EndingEventFlag.Trim());
        }
    }

    private NpcDialogueEntry SelectDialogueForActiveNpc()
    {
        if (activeNpc == null || activeNpc.Definition == null)
        {
            return null;
        }

        IReadOnlyList<NpcDialogueEntry> source = null;

        if (activeNpc.IsFirstEncounterInstance)
        {
            // The first-encounter instance is a special lifetime state, not
            // a normal room-bound NPC. The guaranteed item can legally spawn
            // in a non-HighPrecision room, so after the initial conversation
            // this same instance must keep using a room-independent follow-up
            // pool for the rest of the floor.
            source = !FirstEncounterCompleted
                ? activeNpc.Definition.FirstEncounterDialogues
                : activeNpc.Definition.FirstEncounterFollowupDialogues;
        }
        else if (activeNpc.IsFinalLegacyInstance)
        {
            source = activeNpc.Definition.FinalLegacyDialogues;
        }
        else if (activeNpc.Definition.TryGetRoomDialoguePool(
                     activeNpc.RoomTemplateId,
                     out NpcRoomDialoguePool roomPool))
        {
            source = roomPool.Entries;
        }

        if (source == null || source.Count == 0)
        {
            return null;
        }

        List<NpcDialogueEntry> eligible =
            new List<NpcDialogueEntry>();
        List<NpcDialogueEntry> nonRecent =
            new List<NpcDialogueEntry>();

        for (int i = 0; i < source.Count; i++)
        {
            NpcDialogueEntry entry = source[i];
            if (entry == null ||
                !entry.IsEligible(currentFloor, itemManager))
            {
                continue;
            }

            eligible.Add(entry);

            if (!IsRecentConversation(entry.ConversationId))
            {
                nonRecent.Add(entry);
            }
        }

        List<NpcDialogueEntry> pool =
            nonRecent.Count > 0
                ? nonRecent
                : eligible;

        if (pool.Count == 0)
        {
            return null;
        }

        int seed = CombineSeed(
            floorSeed,
            dialogueSerial,
            StableHash(activeNpc.Definition.NpcId));

        return SelectWeightedDialogue(pool, seed);
    }

    private List<RegularSpawnCandidate>
        BuildRegularSpawnCandidates(DungeonLayout layout)
    {
        List<RegularSpawnCandidate> candidates =
            new List<RegularSpawnCandidate>();

        if (layout == null || layout.RoomPlacements == null)
        {
            return candidates;
        }

        for (int roomIndex = 0;
             roomIndex < layout.RoomPlacements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];
            DreamRoomTemplate template =
                placement != null ? placement.Template : null;

            // HighPrecision is authoritative. The runtime procedural override
            // guard also protects old/stale prefab serialization from being
            // mistaken for a handcrafted room.
            if (template == null ||
                template.RoomFidelityTier !=
                    DreamRoomFidelityTier.HighPrecision ||
                placement.HasRuntimeProceduralOverride)
            {
                continue;
            }

            for (int definitionIndex = 0;
                 definitionIndex < definitions.Count;
                 definitionIndex++)
            {
                NpcDefinition definition = definitions[definitionIndex];
                if (definition == null)
                {
                    continue;
                }

                if (definition.TryGetRoomDialoguePool(
                        template.TemplateId,
                        out NpcRoomDialoguePool pool) &&
                    HasAnyEligibleDialogue(
                        pool.Entries,
                        currentFloor,
                        itemManager))
                {
                    candidates.Add(
                        new RegularSpawnCandidate(
                            definition,
                            roomIndex,
                            template.TemplateId));
                }
            }
        }

        return candidates;
    }

    private HashSet<Vector2Int> CollectRoomWanderCells(
        DungeonLayout layout,
        int roomIndex)
    {
        HashSet<Vector2Int> result =
            new HashSet<Vector2Int>();

        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count)
        {
            return result;
        }

        DreamRoomPlacement placement =
            layout.RoomPlacements[roomIndex];
        if (placement == null || placement.Template == null)
        {
            return result;
        }

        List<Vector2Int> walkable = new List<Vector2Int>();
        placement.GetWalkableGlobalCells(walkable);
        result.UnionWith(walkable);

        IReadOnlyList<DreamRoomDoorSocket> sockets =
            placement.Template.DoorSockets;
        List<Vector2Int> socketCells = new List<Vector2Int>();

        for (int i = 0; i < sockets.Count; i++)
        {
            DreamRoomDoorSocket socket = sockets[i];
            if (socket == null)
            {
                continue;
            }

            placement.GetSocketInsideCells(
                socket,
                socketCells);

            for (int j = 0; j < socketCells.Count; j++)
            {
                result.Remove(socketCells[j]);
            }
        }

        return result;
    }

    private static HashSet<Vector2Int>
        CollectFinalLegacyWanderCells(
            DungeonLayout layout,
            Vector2Int center,
            int radius)
    {
        HashSet<Vector2Int> result =
            new HashSet<Vector2Int>();

        if (layout == null || layout.FloorCells == null)
        {
            return result;
        }

        int clampedRadius = Mathf.Max(1, radius);

        foreach (Vector2Int cell in layout.FloorCells)
        {
            int distance =
                Mathf.Abs(cell.x - center.x) +
                Mathf.Abs(cell.y - center.y);

            if (distance <= clampedRadius)
            {
                result.Add(cell);
            }
        }

        return result;
    }

    private NpcDefinition SelectFirstEncounterDefinition()
    {
        NpcDefinition fallback = null;

        for (int i = 0; i < definitions.Count; i++)
        {
            NpcDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = definition;
            }

            if (definition.IsFirstEncounterNpc)
            {
                return definition;
            }
        }

        return fallback;
    }

    private NpcDefinition SelectFinalLegacyDefinition(int seed)
    {
        List<NpcDefinition> candidates = new List<NpcDefinition>();

        for (int i = 0; i < definitions.Count; i++)
        {
            NpcDefinition definition = definitions[i];
            if (definition != null &&
                definition.AppearsInFinalLegacy)
            {
                candidates.Add(definition);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        System.Random random = new System.Random(
            CombineSeed(FinalLegacySelectionSalt, seed, candidates.Count));

        return candidates[random.Next(0, candidates.Count)];
    }

    private void LoadDefinitions()
    {
        if (definitionsLoaded)
        {
            return;
        }

        definitionsLoaded = true;
        definitions.Clear();

        NpcDefinition[] loaded =
            Resources.LoadAll<NpcDefinition>(ResourcePath);

        if (loaded != null)
        {
            definitions.AddRange(loaded);
        }

        definitions.Sort(
            (a, b) => string.CompareOrdinal(
                a != null ? a.NpcId : string.Empty,
                b != null ? b.NpcId : string.Empty));

        if (definitions.Count == 0)
        {
            usingDevelopmentFallback = true;
            definitions.Add(
                NpcDefinition.CreateDevelopmentFallback());

            Debug.LogWarning(
                "[SYS14] No NpcDefinition assets found under Resources/" +
                ResourcePath +
                ". A development-only fallback NPC is active.",
                this);
        }
        else
        {
            usingDevelopmentFallback = false;
        }
    }

    private static bool HasAnyEligibleDialogue(
        IReadOnlyList<NpcDialogueEntry> entries,
        int floorNumber,
        ItemManager items)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            NpcDialogueEntry entry = entries[i];
            if (entry != null &&
                entry.IsEligible(floorNumber, items))
            {
                return true;
            }
        }

        return false;
    }

    private static NpcDialogueEntry SelectWeightedDialogue(
        List<NpcDialogueEntry> entries,
        int seed)
    {
        int totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            totalWeight += entries[i].RandomWeight;
        }

        System.Random random = new System.Random(seed);
        int roll = random.Next(0, Mathf.Max(1, totalWeight));

        for (int i = 0; i < entries.Count; i++)
        {
            roll -= entries[i].RandomWeight;
            if (roll < 0)
            {
                return entries[i];
            }
        }

        return entries[entries.Count - 1];
    }

    private static RegularSpawnCandidate SelectWeightedRegularCandidate(
        List<RegularSpawnCandidate> candidates,
        int seed)
    {
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Definition.RegularSpawnWeight;
        }

        System.Random random = new System.Random(seed);
        int roll = random.Next(0, Mathf.Max(1, totalWeight));

        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Definition.RegularSpawnWeight;
            if (roll < 0)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private void RememberConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        recentConversationIds.Enqueue(conversationId.Trim());

        while (recentConversationIds.Count >
               RecentConversationCapacity)
        {
            recentConversationIds.Dequeue();
        }
    }

    private bool IsRecentConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        foreach (string recent in recentConversationIds)
        {
            if (string.Equals(
                    recent,
                    conversationId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string TryGetRoomTemplateId(
        DungeonLayout layout,
        int roomIndex)
    {
        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >= layout.RoomPlacements.Count)
        {
            return string.Empty;
        }

        DreamRoomPlacement placement =
            layout.RoomPlacements[roomIndex];

        return placement != null && placement.Template != null
            ? placement.Template.TemplateId
            : string.Empty;
    }

    private static int CombineSeed(int a, int b, int c)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + a;
            hash = hash * 31 + b;
            hash = hash * 31 + c;
            return hash;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (value == null)
            {
                return hash;
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private sealed class RegularSpawnCandidate
    {
        public NpcDefinition Definition { get; }
        public int RoomIndex { get; }
        public string RoomTemplateId { get; }

        public RegularSpawnCandidate(
            NpcDefinition definition,
            int roomIndex,
            string roomTemplateId)
        {
            Definition = definition;
            RoomIndex = roomIndex;
            RoomTemplateId = roomTemplateId ?? string.Empty;
        }
    }
}
