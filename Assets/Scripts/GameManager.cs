using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 遊戲與樓層流程協調者。
///
/// 玩家跨樓層保留；
/// 敵人與道具由各自系統管理；
/// 建立下一層前會廣播 RunProgressionContext。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DungeonGenerator))]
[RequireComponent(typeof(DungeonRenderer))]
[RequireComponent(typeof(ExitSpawner))]
[RequireComponent(typeof(CameraManager))]
public sealed class GameManager : MonoBehaviour
{
    [Header("地圖與流程組件")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private DungeonRenderer dungeonRenderer;
    [SerializeField] private ExitSpawner exitSpawner;
    [SerializeField] private CameraManager cameraManager;

    [Header("獨立角色系統")]
    [SerializeField] private PlayerManager playerManager;

    [Header("獨立敵人系統")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("獨立道具系統")]
    [SerializeField] private ItemManager itemManager;

    [Header("獨立 NPC 系統")]
    [SerializeField] private NpcManager npcManager;

    [Header("啟動")]
    [SerializeField] private bool generateOnStart = true;

    [Header("SYS12 Final Legacy")]
    [Tooltip(
        "收集全部核心道具后使用旧 ProceduralCells 生成器建立终局迷宫。" +
        "这个数字只作为旧生成器的 floorNumber 输入，不代表普通楼层进度。")]
    [Min(1)]
    [SerializeField] private int finalLegacyGenerationFloor = 1;

    [Tooltip(
        "收集到这个数量后，普通迷宫的下一次出口不再进入下一层，" +
        "而是切换到 Final Legacy 1.0 迷宫。")]
    [Min(1)]
    [SerializeField] private int finalLegacyRequiredCoreItemCount = 7;

    [Header("R8.4 受控失败测试")]
    [Tooltip(
        "只用于 R8.4：已有楼层时，拒绝下一层的事务提交，" +
        "验证旧层、玩家、进度与出口重试能力。" +
        "首次生成与 R 重生成不受影响；正常运行必须关闭。")]
    [SerializeField]
    private bool r84RejectNextFloorCommitForControlledFailure;

    [Header("除錯")]
    [SerializeField] private bool showDebugOverlay = true;

    private Transform currentDungeonRoot;
    private DungeonLayout currentLayout;
    private bool isGenerating;
    private DungeonRenderMode requestedGenerationMode =
        DungeonRenderMode.ProceduralCells;
    private DungeonRenderMode effectiveGenerationMode =
        DungeonRenderMode.ProceduralCells;
    private string generationModeStatus = "尚未生成";
    private int r84TransactionSerial;
    private bool isFinalLegacyMode;

    public int CurrentFloor { get; private set; }

    /// <summary>
    /// SYS12-B1: true only after all core items have been collected and the
    /// current GameScene has been replaced by the original ProceduralCells
    /// labyrinth. GameFlow remains Playing so the player can still walk.
    /// </summary>
    public bool IsFinalLegacyMode => isFinalLegacyMode;

    public int CurrentSeed =>
        currentLayout != null
            ? currentLayout.Seed
            : 0;

    public DungeonRenderMode RequestedGenerationMode =>
        requestedGenerationMode;

    public DungeonRenderMode EffectiveGenerationMode =>
        effectiveGenerationMode;

    public ItemProgressSnapshot CurrentItemProgress =>
        itemManager != null
            ? itemManager.CreateProgressSnapshot()
            : new ItemProgressSnapshot(null, 0, -1);

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        if (RunLaunchContext.TryConsume(
                out RunLaunchRequest launchRequest))
        {
            if (launchRequest.Mode == RunLaunchMode.Continue)
            {
                TryStartFromSave(launchRequest.SaveData);
                return;
            }

            if (launchRequest.Mode == RunLaunchMode.NewGame)
            {
                PrepareNewRunState();
                GenerateFloor(1);
                return;
            }
        }

        // Direct GameScene launches used by development/testing keep the
        // historical behaviour: start a fresh floor 1 run.
        PrepareNewRunState();
        GenerateFloor(1);
    }

    private void Update()
    {
        if (!GameFlowManager.AllowsGameplayInput)
        {
            return;
        }

        // The final legacy maze is a destination, not another rerollable floor.
        if (isFinalLegacyMode)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateCurrentFloor();
        }
    }

    public void PlayerReachedExit()
    {
        if (!GameFlowManager.AllowsGameplayInput)
        {
            return;
        }

        if (isFinalLegacyMode)
        {
            TryFinishFinalLegacyRun();
            return;
        }

        // SYS12-B1.1: completing the collection does not tear the current
        // floor away immediately. The player's next deliberate exit is the
        // portal into the original 1.0 maze.
        if (IsFinalLegacyReady())
        {
            TryEnterFinalLegacyMode();
            return;
        }

        TryGenerateNextFloor();
    }

    /// <summary>
    /// R8.4 出口入口。失败时返回 false，让当前出口重新待命。
    /// SYS12-B1.1：普通迷宫集齐核心道具后，下一次出口进入
    /// Final Legacy；Final Legacy 自己的出口才结束本局。
    /// </summary>
    public bool TryPlayerReachedExit()
    {
        if (!GameFlowManager.AllowsGameplayInput)
        {
            return false;
        }

        if (isFinalLegacyMode)
        {
            return TryFinishFinalLegacyRun();
        }

        if (IsFinalLegacyReady())
        {
            return TryEnterFinalLegacyMode();
        }

        return TryGenerateNextFloor();
    }

    public void GenerateNextFloor()
    {
        TryGenerateNextFloor();
    }

    private bool TryGenerateNextFloor()
    {
        if (isFinalLegacyMode)
        {
            return false;
        }

        // Protect every legacy/secondary caller of GenerateNextFloor as well,
        // so no path can skip the final 1.0 maze after collection is complete.
        if (IsFinalLegacyReady())
        {
            return TryEnterFinalLegacyMode();
        }

        int targetFloorNumber =
            Mathf.Max(1, CurrentFloor + 1);

        return GenerateFloor(targetFloorNumber);
    }

    private bool IsFinalLegacyReady()
    {
        CacheComponents();

        return !isFinalLegacyMode &&
               itemManager != null &&
               itemManager.CollectedItemCount >=
                   Mathf.Max(1, finalLegacyRequiredCoreItemCount);
    }

    public void RegenerateCurrentFloor()
    {
        if (isFinalLegacyMode)
        {
            return;
        }

        int targetFloorNumber =
            CurrentFloor > 0
                ? CurrentFloor
                : 1;

        GenerateFloor(targetFloorNumber);
    }

    /// <summary>
    /// SYS12-B1 authoritative transition into the "return to origin" maze.
    /// It deliberately reuses DungeonGenerator.Generate(), the old
    /// ProceduralCells path. The current player is preserved, while enemies,
    /// item spawning and normal floor progression are removed.
    /// </summary>
    public bool TryEnterFinalLegacyMode()
    {
        if (isFinalLegacyMode || isGenerating)
        {
            return false;
        }

        if (!GameFlowManager.AllowsGameplayInput)
        {
            return false;
        }

        CacheComponents();

        if (itemManager != null)
        {
            itemManager.ValidateProgressionConfiguration(
                Mathf.Max(1, finalLegacyRequiredCoreItemCount),
                false);
        }

        if (dungeonGenerator == null ||
            dungeonRenderer == null ||
            exitSpawner == null ||
            cameraManager == null ||
            playerManager == null ||
            enemyManager == null ||
            itemManager == null ||
            npcManager == null)
        {
            Debug.LogError(
                "[SYS12-B1] Final Legacy transition failed: required " +
                "GameManager components are missing.",
                this);
            return false;
        }

        DungeonLayout legacyLayout;
        string failureReason;

        if (!TryGenerateProceduralLayout(
                Mathf.Max(1, finalLegacyGenerationFloor),
                out legacyLayout,
                out failureReason))
        {
            Debug.LogError(
                "[SYS12-B1] Final Legacy generation failed.\n" +
                failureReason,
                this);
            return false;
        }

        isGenerating = true;

        try
        {
            // RemoveCurrentFloor is also the single existing authority for
            // clearing current enemies and the active pickup.
            RemoveCurrentFloor();

            isFinalLegacyMode = true;
            currentLayout = legacyLayout;
            requestedGenerationMode =
                DungeonRenderMode.ProceduralCells;
            effectiveGenerationMode =
                DungeonRenderMode.ProceduralCells;
            generationModeStatus = "Final Legacy / 1.0";

            currentDungeonRoot =
                new GameObject("GeneratedDungeon_FinalLegacy")
                    .transform;

            currentDungeonRoot.SetParent(transform);
            currentDungeonRoot.localPosition = Vector3.zero;

            // Even if DungeonRenderer is configured for Hybrid at scene level,
            // a non-hybrid DungeonLayout explicitly falls back to the old
            // ProceduralCells renderer.
            dungeonRenderer.Render(
                currentLayout,
                currentDungeonRoot);

            Transform player =
                playerManager.PlacePlayer(
                    currentLayout.StartCell,
                    dungeonRenderer);

            if (player == null)
            {
                Debug.LogError(
                    "[SYS12-B1] Final Legacy player placement failed.",
                    this);
                return false;
            }

            // The existing RuntimeDungeonExit calls back into this GameManager.
            // PlayerReachedExit() now routes this particular mode to Ending.
            exitSpawner.Spawn(
                currentLayout.ExitCell,
                currentDungeonRoot,
                dungeonRenderer,
                this);

            HashSet<Vector2Int> runtimeSpawnReservations =
                new HashSet<Vector2Int>
                {
                    currentLayout.StartCell,
                    currentLayout.ExitCell
                };

            npcManager.SetupFinalLegacy(
                currentLayout,
                currentDungeonRoot,
                dungeonRenderer,
                player,
                itemManager,
                runtimeSpawnReservations);

            cameraManager.SetTarget(player);

            Debug.Log(
                "[SYS12-B1] Final Legacy Mode entered" +
                " | SourceFloor=" + CurrentFloor +
                " | LegacySeed=" + currentLayout.Seed +
                " | Rooms=" + currentLayout.Rooms.Count +
                " | Enemies=0" +
                " | ItemSpawn=Off" +
                " | Reroll=Off" +
                " | Exit=Ending",
                this);

            return true;
        }
        finally
        {
            isGenerating = false;
        }
    }

    /// <summary>
    /// SYS9 gameplay-to-save mapping. Saves only the lightweight run contract:
    /// floor, current HP, collected stable Item IDs and cumulative EnemyRunRecord facts.
    /// Generated world state is deliberately excluded.
    /// </summary>
    public bool TrySaveCurrentRun(out string error)
    {
        error = string.Empty;
        CacheComponents();

        if (isFinalLegacyMode)
        {
            error =
                "Final Legacy Mode is an ending area and cannot be saved.";
            return false;
        }

        if (CurrentFloor < 1)
        {
            error = "No generated floor is available to save.";
            return false;
        }

        if (playerManager == null ||
            playerManager.CurrentHealth == null)
        {
            error = "Player Health is not available.";
            return false;
        }

        float currentHp =
            playerManager.CurrentHealth.CurrentHealth;

        if (currentHp <= 0f)
        {
            error = "A defeated player cannot create a run save.";
            return false;
        }

        if (itemManager == null ||
            enemyManager == null ||
            npcManager == null)
        {
            error = "ItemManager, EnemyManager or NpcManager is not available.";
            return false;
        }

        ItemProgressSnapshot itemSnapshot =
            itemManager.CreateProgressSnapshot();
        List<string> itemIds = new List<string>();

        IReadOnlyList<ItemDefinition> collected =
            itemSnapshot.CollectedItems;

        for (int i = 0; i < collected.Count; i++)
        {
            ItemDefinition definition = collected[i];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId))
            {
                error =
                    "Collected item progress contains an invalid Item ID.";
                return false;
            }

            itemIds.Add(definition.ItemId);
        }

        EnemyRunRecordSnapshot enemySnapshot =
            enemyManager.CurrentRunSnapshot;

        SaveGameData data = new SaveGameData(
            CurrentFloor,
            currentHp,
            itemIds,
            enemySnapshot,
            npcManager.FirstEncounterCompleted);

        bool success =
            SaveSystemManager.GetOrCreate().TryWriteSave(
                data,
                out error);

        if (success)
        {
            Debug.Log(
                "[SYS9] Current run saved" +
                " | Floor=" + CurrentFloor +
                " | HP=" + currentHp.ToString("0.##") +
                " | Items=" + itemIds.Count +
                " | RunEnemies=" +
                enemySnapshot.EligibleSpawnedCount +
                " | Kills=" + enemySnapshot.PlayerKillCount +
                " | NpcFirstEncounter=" +
                npcManager.FirstEncounterCompleted,
                this);
        }

        return success;
    }

    private void PrepareNewRunState()
    {
        CacheComponents();

        isFinalLegacyMode = false;
        EndingRunContext.Clear();

        if (enemyManager != null)
        {
            enemyManager.ResetRunRecord();
        }

        if (npcManager != null)
        {
            npcManager.ResetRunState();
        }
    }

    private bool TryStartFromSave(SaveGameData data)
    {
        CacheComponents();

        isFinalLegacyMode = false;
        EndingRunContext.Clear();

        if (data == null)
        {
            return FailContinueStartup(
                "Continue launch data is null.");
        }

        if (playerManager == null ||
            enemyManager == null ||
            itemManager == null ||
            npcManager == null)
        {
            return FailContinueStartup(
                "PlayerManager, EnemyManager, ItemManager or NpcManager is missing.");
        }

        if (data.enemyRun == null)
        {
            return FailContinueStartup(
                "Enemy run save data is missing.");
        }

        Debug.Log(
            "[SYS9] Continue startup" +
            " | TargetFloor=" + data.floorIndex +
            " | SavedHP=" + data.currentHP.ToString("0.##") +
            " | SavedItems=" + data.collectedItemIds.Count +
            " | SavedRunEnemies=" +
            data.enemyRun.eligibleSpawnedCount +
            " | SavedKills=" + data.killCount +
            " | NpcFirstEncounter=" +
            data.npcFirstEncounterCompleted +
            " | WorldSnapshot=Regenerate" +
            " | RunHistory=Restore",
            this);

        if (!itemManager.TryRestoreRunProgress(
                data.collectedItemIds,
                data.floorIndex,
                out string itemError))
        {
            return FailContinueStartup(
                "Item restore failed: " + itemError);
        }

        if (!enemyManager.TryRestoreSavedRunRecord(
                data.enemyRun.CreateSnapshot(),
                out string enemyError))
        {
            return FailContinueStartup(
                "Enemy run history restore failed: " + enemyError);
        }

        npcManager.RestoreRunState(
            data.npcFirstEncounterCompleted);

        if (!GenerateFloor(data.floorIndex))
        {
            return FailContinueStartup(
                "Requested floor generation failed.");
        }

        if (!playerManager.TryRestoreCurrentHealth(
                data.currentHP,
                out string healthError))
        {
            return FailContinueStartup(
                "Player HP restore failed: " + healthError);
        }

        EnemyRunRecordSnapshot restoredEnemySnapshot =
            enemyManager.CurrentRunSnapshot;

        Debug.Log(
            "[SYS13-Bugfix1] Continue restore complete" +
            " | Floor=" + CurrentFloor +
            " | HP=" +
            playerManager.CurrentHealth.CurrentHealth.ToString("0.##") +
            " | Items=" + itemManager.CollectedItemCount +
            " | RunEnemies=" +
            restoredEnemySnapshot.EligibleSpawnedCount +
            " | Kills=" + restoredEnemySnapshot.PlayerKillCount +
            " | NpcFirstEncounter=" +
            npcManager.FirstEncounterCompleted +
            " | GeneratedOnce=True",
            this);

        return true;
    }

    private bool TryFinishFinalLegacyRun()
    {
        return TryFinishFinalLegacyRun(string.Empty);
    }

    public bool TryFinishFinalLegacyRunFromNpc(
        string endingEventFlag)
    {
        if (string.IsNullOrWhiteSpace(endingEventFlag))
        {
            return false;
        }

        return TryFinishFinalLegacyRun(
            endingEventFlag.Trim());
    }

    private bool TryFinishFinalLegacyRun(
        string endingEventFlag)
    {
        if (!isFinalLegacyMode || isGenerating)
        {
            return false;
        }

        CacheComponents();

        if (playerManager == null ||
            playerManager.CurrentHealth == null ||
            itemManager == null ||
            enemyManager == null)
        {
            Debug.LogError(
                "[SYS12-B2] Cannot resolve ending: run state is incomplete.",
                this);
            return false;
        }

        List<string> itemIds = new List<string>();
        IReadOnlyList<ItemDefinition> collected =
            itemManager.CreateProgressSnapshot().CollectedItems;

        for (int i = 0; i < collected.Count; i++)
        {
            ItemDefinition definition = collected[i];

            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.ItemId))
            {
                itemIds.Add(definition.ItemId);
            }
        }

        EnemyRunRecordSnapshot endingEnemySnapshot =
            enemyManager.CurrentRunSnapshot;

        EndingRunData endingData =
            new EndingRunData(
                CurrentFloor,
                playerManager.CurrentHealth.CurrentHealth,
                itemIds,
                endingEnemySnapshot);

        endingData.AddEventFlag(endingEventFlag);

        endingData.endingId =
            EndingResolver.Resolve(endingData);

        EndingRunContext.Queue(endingData);

        GameFlowManager flow =
            GameFlowManager.GetOrCreate();

        if (!flow.TryBeginVictory())
        {
            EndingRunContext.Clear();

            Debug.LogWarning(
                "[SYS12-B2] Final exit reached but Victory state " +
                "could not begin.",
                this);
            return false;
        }

        Debug.Log(
            "[SYS12-B2] Final exit reached" +
            " | EndingId=" + endingData.endingId +
            " | HP=" + endingData.finalHP.ToString("0.##") +
            " | Items=" + endingData.collectedItemIds.Count +
            " | RunEnemies=" +
            endingData.eligibleEnemySpawnCount +
            " | Kills=" + endingData.killCount +
            " | EventFlag=" +
            (string.IsNullOrWhiteSpace(endingEventFlag)
                ? "None"
                : endingEventFlag),
            this);

        flow.LoadEnding();
        return true;
    }

    private bool FailContinueStartup(string reason)
    {
        Debug.LogError(
            "[SYS9] Continue restore failed | " + reason,
            this);

        GameFlowManager flow = GameFlowManager.Instance;

        if (flow != null)
        {
            flow.ReturnToTitle();
        }

        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("SYS9 Debug/Save Current Run")]
    private void DebugSaveCurrentRun()
    {
        bool success = TrySaveCurrentRun(out string error);

        Debug.Log(
            "[SYS9] Debug save current run=" + success +
            (success ? string.Empty : " | Error=" + error),
            this);
    }

    [ContextMenu("SYS9 Debug/Print Live Save State")]
    private void DebugPrintLiveSaveState()
    {
        CacheComponents();

        EnemyRunRecordSnapshot enemySnapshot =
            enemyManager != null
                ? enemyManager.CurrentRunSnapshot
                : new EnemyRunRecordSnapshot();

        Debug.Log(
            "[SYS9] Live state" +
            " | Floor=" + CurrentFloor +
            " | HP=" +
            (playerManager != null && playerManager.CurrentHealth != null
                ? playerManager.CurrentHealth.CurrentHealth.ToString("0.##")
                : "<none>") +
            " | Items=" +
            (itemManager != null ? itemManager.CollectedItemCount : -1) +
            " | RunEnemies=" + enemySnapshot.EligibleSpawnedCount +
            " | Kills=" + enemySnapshot.PlayerKillCount +
            " | FinalLegacy=" + isFinalLegacyMode,
            this);
    }

    [ContextMenu("SYS12 Debug/Enter Final Legacy Mode")]
    private void DebugEnterFinalLegacyMode()
    {
        bool success = TryEnterFinalLegacyMode();

        Debug.Log(
            "[SYS12] Debug enter Final Legacy=" + success +
            " | CurrentFloor=" + CurrentFloor +
            " | IsFinalLegacy=" + isFinalLegacyMode,
            this);
    }
#endif

    private bool GenerateFloor(int targetFloorNumber)
    {
        if (isGenerating)
        {
            return false;
        }

        CacheComponents();

        if (playerManager == null ||
            enemyManager == null ||
            itemManager == null ||
            npcManager == null)
        {
            Debug.LogError(
                "GameManager：PlayerManager、EnemyManager、ItemManager 或 NpcManager 未設定。");

            return false;
        }

        isGenerating = true;

        try
        {
            int transactionId =
                ++r84TransactionSerial;

            int previousFloor = CurrentFloor;
            GameObject previousRoot =
                currentDungeonRoot != null
                    ? currentDungeonRoot.gameObject
                    : null;
            int previousRootId =
                GetR84InstanceId(previousRoot);
            int previousPlayerId =
                GetR84InstanceId(
                    playerManager.CurrentPlayerObject);
            List<int> previousEnemyIds =
                GetR84ActiveEnemyIds();
            int previousPickupId =
                GetR84InstanceId(
                    itemManager.ActivePickup);
            ItemProgressSnapshot previousProgress =
                itemManager.CreateProgressSnapshot();

            DungeonRenderMode requestedMode =
                dungeonRenderer.RenderMode;

            requestedGenerationMode = requestedMode;
            generationModeStatus = "准备中";

            if (r84RejectNextFloorCommitForControlledFailure &&
                CurrentFloor > 0 &&
                targetFloorNumber > CurrentFloor)
            {
                generationModeStatus =
                    "受控失败，旧层保留";

                Debug.LogWarning(
                    "[GameManager/R8.4] 下一层事务被受控拒绝，" +
                    "旧层保持可用。" +
                    "\nTransaction=" + transactionId +
                    " | ControlledFailure=True" +
                    " | Commit=Rejected" +
                    " | Requested=" + requestedMode +
                    " | Effective=Unchanged(" +
                    effectiveGenerationMode + ")" +
                    "\nCurrentFloor=" + CurrentFloor +
                    " | TargetFloor=" + targetFloorNumber +
                    " | GeneratedRootId=" + previousRootId +
                    " | PlayerId=" + previousPlayerId +
                    " | ActiveEnemies=" +
                    previousEnemyIds.Count +
                    " | ActivePickupId=" +
                    previousPickupId +
                    "\nProgress=" +
                    FormatR84Progress(previousProgress) +
                    " | LifecycleUnchanged=True" +
                    " | SceneMutation=None" +
                    " | ProgressMutation=None",
                    this);

                return false;
            }

            DungeonLayout preparedLayout;
            DungeonRenderMode preparedEffectiveMode;
            bool usedFallback;

            if (!TryPrepareLayout(
                    targetFloorNumber,
                    requestedMode,
                    out preparedLayout,
                    out preparedEffectiveMode,
                    out usedFallback))
            {
                generationModeStatus =
                    "生成失败，旧层保留";

                Debug.LogError(
                    "[GameManager/R7.1] 楼层生成未提交。" +
                    " Requested=" + requestedMode +
                    ", Effective=Unchanged(" +
                    effectiveGenerationMode + ")" +
                    " | Floor " + targetFloorNumber +
                    " | 旧层、CurrentFloor 与进度广播均保持不变。",
                    this);

                return false;
            }

            // 到这里才提交楼层切换：R6 失败时，旧层仍然完整存在。
            RemoveCurrentFloor();

            CurrentFloor = targetFloorNumber;
            currentLayout = preparedLayout;
            effectiveGenerationMode = preparedEffectiveMode;
            generationModeStatus =
                usedFallback
                    ? "已明确回退"
                    : "直接成功";

            RunProgressionContext progressionContext =
                new RunProgressionContext(
                    CurrentFloor,
                    itemManager.CreateProgressSnapshot());

            BroadcastRunProgression(
                progressionContext);

            currentDungeonRoot =
                new GameObject(
                    "GeneratedDungeon_Floor_" + CurrentFloor)
                .transform;

            currentDungeonRoot.SetParent(transform);
            currentDungeonRoot.localPosition = Vector3.zero;

            dungeonRenderer.Render(
                currentLayout,
                currentDungeonRoot);

            Transform player =
                playerManager.PlacePlayer(
                    currentLayout.StartCell,
                    dungeonRenderer);

            if (player == null)
            {
                Debug.LogError(
                    "GameManager：玩家建立失敗。");

                return false;
            }

            exitSpawner.Spawn(
                currentLayout.ExitCell,
                currentDungeonRoot,
                dungeonRenderer,
                this);

            // R8.3：同一楼层的 Player／Exit／Item／Enemy 共用一份
            // 运行时出生格保留集合。它只协调消费者，不写回 Layout，
            // 也不改变 R4／R5／R6 的房间与走廊结果。
            HashSet<Vector2Int> runtimeSpawnReservations =
                new HashSet<Vector2Int>
                {
                    currentLayout.StartCell,
                    currentLayout.ExitCell
                };

            itemManager.SetupFloor(
                CurrentFloor,
                currentLayout,
                currentDungeonRoot,
                dungeonRenderer,
                runtimeSpawnReservations);

            // SYS14 order is Item -> NPC -> Enemy. The first encounter reads
            // the item's actual resolved room/cell, and the NPC then reserves
            // its own cell before enemy spawning.
            npcManager.SetupFloor(
                CurrentFloor,
                currentLayout,
                currentDungeonRoot,
                dungeonRenderer,
                player,
                itemManager,
                runtimeSpawnReservations);

            enemyManager.SetupFloor(
                CurrentFloor,
                currentLayout,
                currentDungeonRoot,
                dungeonRenderer,
                player,
                runtimeSpawnReservations);

            npcManager.IgnoreEnemyCollisions(
                enemyManager.ActiveEnemies);

            cameraManager.SetTarget(player);

            GameObject committedRoot =
                currentDungeonRoot != null
                    ? currentDungeonRoot.gameObject
                    : null;

            StartCoroutine(
                AuditR84CommitAfterFrame(
                    transactionId,
                    previousFloor,
                    targetFloorNumber,
                    requestedMode,
                    preparedEffectiveMode,
                    previousRoot,
                    previousRootId,
                    committedRoot,
                    GetR84InstanceId(committedRoot),
                    previousPlayerId,
                    previousEnemyIds,
                    previousPickupId,
                    previousProgress));

            return true;
        }
        finally
        {
            isGenerating = false;
        }
    }

    private IEnumerator AuditR84CommitAfterFrame(
        int transactionId,
        int previousFloor,
        int targetFloor,
        DungeonRenderMode requestedMode,
        DungeonRenderMode effectiveMode,
        GameObject previousRoot,
        int previousRootId,
        GameObject committedRoot,
        int committedRootId,
        int previousPlayerId,
        List<int> previousEnemyIds,
        int previousPickupId,
        ItemProgressSnapshot previousProgress)
    {
        // Destroy 在帧末真正执行；下一帧检查才能区分
        // “排队销毁”与“旧 GeneratedDungeon 已经消失”。
        yield return null;

        int generatedRootCount =
            CountR84GeneratedRoots();
        int playerObjectCount =
            CountR84PlayerObjects();
        int currentPlayerId =
            GetR84InstanceId(
                playerManager.CurrentPlayerObject);
        List<int> currentEnemyIds =
            GetR84ActiveEnemyIds();
        int currentPickupId =
            GetR84InstanceId(
                itemManager.ActivePickup);
        ItemProgressSnapshot currentProgress =
            itemManager.CreateProgressSnapshot();

        bool floorCommitted =
            CurrentFloor == targetFloor &&
            committedRoot != null &&
            GetR84InstanceId(committedRoot) ==
                committedRootId;
        bool oldRootDestroyed =
            previousRootId == 0 ||
            previousRoot == null;
        bool exactlyOneGeneratedRoot =
            generatedRootCount == 1;
        bool exactlyOnePlayer =
            playerObjectCount == 1 &&
            currentPlayerId != 0;
        bool playerReused =
            previousPlayerId == 0 ||
            currentPlayerId == previousPlayerId;
        bool cameraTargetMatches =
            cameraManager.CurrentTarget != null &&
            cameraManager.CurrentTarget ==
                playerManager.CurrentPlayer;
        bool oldEnemyReferencesCleared =
            !HasAnySharedR84InstanceId(
                previousEnemyIds,
                currentEnemyIds);
        bool oldItemReferenceCleared =
            previousPickupId == 0 ||
            currentPickupId != previousPickupId;
        bool progressPreserved =
            HasSameR84Progress(
                previousProgress,
                currentProgress);

        bool auditPassed =
            floorCommitted &&
            oldRootDestroyed &&
            exactlyOneGeneratedRoot &&
            exactlyOnePlayer &&
            playerReused &&
            cameraTargetMatches &&
            oldEnemyReferencesCleared &&
            oldItemReferenceCleared &&
            progressPreserved;

        string playerReuseText =
            previousPlayerId == 0
                ? "InitialCreation"
                : playerReused.ToString();

        string message =
            "[GameManager/R8.4] 楼层生命周期审计" +
            (auditPassed ? "通过。" : "失败。") +
            "\nTransaction=" + transactionId +
            " | PreviousFloor=" + previousFloor +
            " | CurrentFloor=" + CurrentFloor +
            " | Requested=" + requestedMode +
            " | Effective=" + effectiveMode +
            "\nGeneratedRoots=" +
            generatedRootCount +
            " | OldRootId=" + previousRootId +
            " | NewRootId=" + committedRootId +
            " | OldRootDestroyed=" +
            oldRootDestroyed +
            "\nPlayerObjects=" + playerObjectCount +
            " | PreviousPlayerId=" + previousPlayerId +
            " | CurrentPlayerId=" + currentPlayerId +
            " | PlayerReused=" + playerReuseText +
            " | CameraTargetMatches=" +
            cameraTargetMatches +
            "\nOldEnemyReferencesCleared=" +
            oldEnemyReferencesCleared +
            " | ActiveEnemies=" +
            currentEnemyIds.Count +
            " | OldItemReferenceCleared=" +
            oldItemReferenceCleared +
            " | ActivePickupId=" + currentPickupId +
            "\nProgressBefore=" +
            FormatR84Progress(previousProgress) +
            " | ProgressAfter=" +
            FormatR84Progress(currentProgress) +
            " | ProgressPreserved=" +
            progressPreserved;

        if (auditPassed)
        {
            Debug.Log(message, this);
        }
        else
        {
            Debug.LogError(message, this);
        }
    }

    private int CountR84GeneratedRoots()
    {
        int count = 0;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child != null &&
                child.name.StartsWith(
                    "GeneratedDungeon_Floor_",
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private int CountR84PlayerObjects()
    {
        RuntimeDungeonPlayer[] players =
            playerManager.GetComponentsInChildren<
                RuntimeDungeonPlayer>(true);

        return players.Length;
    }

    private List<int> GetR84ActiveEnemyIds()
    {
        List<int> instanceIds =
            new List<int>();
        IReadOnlyList<GameObject> enemies =
            enemyManager.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            int instanceId =
                GetR84InstanceId(enemies[i]);

            if (instanceId != 0)
            {
                instanceIds.Add(instanceId);
            }
        }

        return instanceIds;
    }

    private static bool HasAnySharedR84InstanceId(
        List<int> first,
        List<int> second)
    {
        for (int i = 0; i < first.Count; i++)
        {
            if (second.Contains(first[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSameR84Progress(
        ItemProgressSnapshot first,
        ItemProgressSnapshot second)
    {
        return
            first.CollectedCount == second.CollectedCount &&
            first.ProgressionScore ==
                second.ProgressionScore &&
            first.LastCollectedFloor ==
                second.LastCollectedFloor;
    }

    private static int GetR84InstanceId(
        UnityEngine.Object value)
    {
        return value != null
            ? value.GetInstanceID()
            : 0;
    }

    private static string FormatR84Progress(
        ItemProgressSnapshot progress)
    {
        return
            "(Items=" + progress.CollectedCount +
            ",Score=" + progress.ProgressionScore +
            ",LastFloor=" +
            progress.LastCollectedFloor + ")";
    }

    private bool TryPrepareLayout(
        int floorNumber,
        DungeonRenderMode requestedMode,
        out DungeonLayout layout,
        out DungeonRenderMode effectiveMode,
        out bool usedFallback)
    {
        layout = null;
        effectiveMode = DungeonRenderMode.ProceduralCells;
        usedFallback = false;

        if (requestedMode ==
            DungeonRenderMode.HybridPrefabRooms)
        {
            DungeonLayout hybridLayout;
            string hybridReport;
            bool hybridGenerated;

            try
            {
                hybridGenerated =
                    dungeonGenerator
                        .TryGenerateHybridRuntimeLayout(
                            floorNumber,
                            out hybridLayout,
                            out hybridReport);
            }
            catch (Exception exception)
            {
                hybridGenerated = false;
                hybridLayout = null;
                hybridReport =
                    "[DungeonGenerator/R7.1] Hybrid 运行时生成抛出异常：\n" +
                    exception;
            }

            List<string> hybridValidationErrors =
                hybridGenerated
                    ? GetHybridRuntimeValidationErrors(
                        hybridLayout)
                    : new List<string>();

            if (hybridGenerated &&
                hybridValidationErrors.Count == 0)
            {
                layout = hybridLayout;
                effectiveMode =
                    DungeonRenderMode.HybridPrefabRooms;

                Debug.Log(hybridReport, dungeonGenerator);
                Debug.Log(
                    BuildHybridSuccessSummary(
                        floorNumber,
                        hybridLayout),
                    this);

                return true;
            }

            usedFallback = true;

            Debug.LogWarning(
                BuildHybridFallbackReport(
                    floorNumber,
                    hybridReport,
                    hybridValidationErrors),
                this);

            string proceduralFailureReason;

            if (!TryGenerateProceduralLayout(
                    floorNumber,
                    out layout,
                    out proceduralFailureReason))
            {
                Debug.LogError(
                    "[GameManager/R7.1] Hybrid 与 Procedural fallback 均失败。\n" +
                    "Requested=HybridPrefabRooms, " +
                    "Effective=None\n" +
                    proceduralFailureReason,
                    this);

                return false;
            }

            effectiveMode =
                DungeonRenderMode.ProceduralCells;

            Debug.LogWarning(
                "[GameManager/R7.1] Procedural fallback 已建立。" +
                " Requested=HybridPrefabRooms," +
                " Effective=ProceduralCells" +
                " | Floor " + floorNumber +
                " | Seed " + layout.Seed +
                " | 旧层现在才会被替换。",
                this);

            return true;
        }

        if (requestedMode !=
            DungeonRenderMode.ProceduralCells)
        {
            usedFallback = true;

            Debug.LogWarning(
                "[GameManager/R7.1] 收到未知生成模式 " +
                (int)requestedMode +
                "。Requested=" + requestedMode +
                ", Effective=ProceduralCells。",
                this);
        }

        string failureReason;

        if (!TryGenerateProceduralLayout(
                floorNumber,
                out layout,
                out failureReason))
        {
            Debug.LogError(
                "[GameManager/R7.1] ProceduralCells 生成失败。\n" +
                "Requested=" + requestedMode +
                ", Effective=None\n" +
                failureReason,
                this);

            return false;
        }

        effectiveMode =
            DungeonRenderMode.ProceduralCells;

        Debug.Log(
            "[GameManager/R7.1] 运行时布局已建立。" +
            " Requested=" + requestedMode +
            ", Effective=ProceduralCells" +
            " | Floor " + floorNumber +
            " | Seed " + layout.Seed +
            " | HasHybridRoomData=" +
            layout.HasHybridRoomData,
            this);

        return true;
    }

    private bool TryGenerateProceduralLayout(
        int floorNumber,
        out DungeonLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = string.Empty;

        try
        {
            layout =
                dungeonGenerator.Generate(floorNumber);
        }
        catch (Exception exception)
        {
            failureReason = exception.ToString();
            return false;
        }

        if (layout == null)
        {
            failureReason =
                "DungeonGenerator.Generate(int) 返回了 null。";
            return false;
        }

        List<string> validationErrors =
            layout.GetValidationErrors();

        if (validationErrors.Count > 0)
        {
            failureReason =
                BuildValidationErrorList(
                    validationErrors);
            layout = null;
            return false;
        }

        return true;
    }

    private List<string> GetHybridRuntimeValidationErrors(
        DungeonLayout layout)
    {
        List<string> errors = new List<string>();

        if (layout == null)
        {
            errors.Add("Hybrid Layout 不能为空。");
            return errors;
        }

        if (!layout.HasHybridRoomData)
        {
            errors.Add(
                "HasHybridRoomData 必须为 True。");
        }

        errors.AddRange(
            layout.GetValidationErrors());

        errors.AddRange(
            dungeonGenerator
                .GetSocketCorridorValidationErrors(
                    layout));

        return errors;
    }

    private string BuildHybridSuccessSummary(
        int floorNumber,
        DungeonLayout layout)
    {
        return
            "[GameManager/R7.1] Hybrid 运行时布局已通过提交前校验。" +
            " Requested=HybridPrefabRooms," +
            " Effective=HybridPrefabRooms" +
            " | Floor " + floorNumber +
            " | Seed " + layout.Seed +
            " | HasHybridRoomData=" +
            layout.HasHybridRoomData +
            " | Placements=" +
            layout.RoomPlacements.Count +
            " | Connections=" +
            layout.Connections.Count +
            " | FloorCells=" +
            layout.FloorCells.Count +
            " | R7.1 仍由 ProceduralCells 过渡显示。";
    }

    private string BuildHybridFallbackReport(
        int floorNumber,
        string hybridReport,
        List<string> validationErrors)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(
            "[GameManager/R7.1] Hybrid 请求失败，准备明确回退。");
        builder.AppendLine(
            "Requested=HybridPrefabRooms, " +
            "Effective=ProceduralCells" +
            " | Floor " + floorNumber);

        if (!string.IsNullOrEmpty(hybridReport))
        {
            builder.AppendLine("R6 完整报告：");
            builder.AppendLine(hybridReport);
        }

        if (validationErrors.Count > 0)
        {
            builder.AppendLine("R7.1 提交前校验错误：");
            builder.Append(
                BuildValidationErrorList(
                    validationErrors));
        }

        return builder.ToString();
    }

    private static string BuildValidationErrorList(
        List<string> errors)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append("- ");
            builder.Append(errors[i]);

            if (i < errors.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private void BroadcastRunProgression(
        RunProgressionContext context)
    {
        MonoBehaviour[] behaviours =
            GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            IRunProgressionConsumer consumer =
                behaviours[i] as IRunProgressionConsumer;

            if (consumer != null)
            {
                consumer.ApplyRunProgression(context);
            }
        }
    }

    private void RemoveCurrentFloor()
    {
        if (npcManager != null)
        {
            npcManager.ClearFloor();
        }

        if (enemyManager != null)
        {
            enemyManager.ClearFloor();
        }

        if (itemManager != null)
        {
            itemManager.ClearFloor();
        }

        if (currentDungeonRoot == null)
        {
            return;
        }

        currentDungeonRoot.gameObject.SetActive(false);
        Destroy(currentDungeonRoot.gameObject);
        currentDungeonRoot = null;
    }

    private void CacheComponents()
    {
        dungeonGenerator =
            GetComponent<DungeonGenerator>();

        dungeonRenderer =
            GetComponent<DungeonRenderer>();

        exitSpawner =
            GetComponent<ExitSpawner>();

        cameraManager =
            GetComponent<CameraManager>();

        if (playerManager == null)
        {
            playerManager =
                GetComponentInChildren<PlayerManager>(true);
        }

        if (enemyManager == null)
        {
            enemyManager =
                GetComponentInChildren<EnemyManager>(true);
        }

        if (itemManager == null)
        {
            itemManager =
                GetComponentInChildren<ItemManager>(true);
        }

        if (npcManager == null)
        {
            npcManager =
                GetComponentInChildren<NpcManager>(true);
        }

        // No manual scene surgery is required for SYS14. In edit mode we do
        // not mutate the scene; at runtime GameManager owns this lightweight
        // persistent-across-floors manager just like Item/Enemy managers.
        if (npcManager == null && Application.isPlaying)
        {
            GameObject npcSystem =
                new GameObject("NpcSystem_Runtime");
            npcSystem.transform.SetParent(transform, false);
            npcManager = npcSystem.AddComponent<NpcManager>();
        }

        if (npcManager != null)
        {
            npcManager.BindGameManager(this);
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        int roomCount =
            currentLayout != null
                ? currentLayout.Rooms.Count
                : 0;

        int enemyCount =
            enemyManager != null
                ? enemyManager.ActiveEnemyCount
                : 0;

        int itemCount =
            itemManager != null
                ? itemManager.CollectedItemCount
                : 0;

        float nextItemChance =
            !isFinalLegacyMode && itemManager != null
                ? itemManager.GetSpawnChanceForFloor(
                    CurrentFloor + 1)
                : 0f;

        EnemyRunRecordSnapshot combatSnapshot =
            enemyManager != null
                ? enemyManager.CurrentRunSnapshot
                : new EnemyRunRecordSnapshot();

        GUI.Box(
            new Rect(12f, 12f, 650f, 210f),
            "");

        GUI.Label(
            new Rect(24f, 22f, 540f, 24f),
            isFinalLegacyMode
                ? "FINAL LEGACY：WASD / 方向鍵：移動    R：停用"
                : "WASD / 方向鍵：移動    R：重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 540f, 24f),
            isFinalLegacyMode
                ? "出口：進入結局    敵人/道具刷新：停用"
                : "黃色方塊：下一層    核心道具：碰觸拾取");

        GUI.Label(
            new Rect(24f, 74f, 540f, 24f),
            "Floor: " + CurrentFloor +
            "    Rooms: " + roomCount +
            "    Enemies: " + enemyCount +
            "    Seed: " + CurrentSeed);

        GUI.Label(
            new Rect(24f, 100f, 540f, 24f),
            "Requested Mode: " +
            requestedGenerationMode);

        GUI.Label(
            new Rect(24f, 126f, 620f, 24f),
            "Effective Mode: " +
            effectiveGenerationMode +
            "    Status: " + generationModeStatus);

        GUI.Label(
            new Rect(24f, 152f, 620f, 24f),
            "Items: " + itemCount +
            "    Progression Score: " +
            (itemManager != null
                ? itemManager.ProgressionScore
                : 0) +
            "    Next Chance: " +
            Mathf.RoundToInt(nextItemChance * 100f) +
            "%");

        GUI.Label(
            new Rect(24f, 178f, 620f, 24f),
            "Run Enemies: " +
            combatSnapshot.EligibleSpawnedCount +
            "    Player Kills: " +
            combatSnapshot.PlayerKillCount +
            "    Other Deaths: " +
            combatSnapshot.OtherDeathCount +
            "    Survived Enemies: " +
            combatSnapshot.SurvivedFloorCount);
    }
}
