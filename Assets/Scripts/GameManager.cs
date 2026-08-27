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

    [Header("啟動")]
    [SerializeField] private bool generateOnStart = true;

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

    public int CurrentFloor { get; private set; }

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
        if (generateOnStart)
        {
            GenerateNextFloor();
        }
    }

    private void Update()
    {
        if (!GameFlowManager.AllowsGameplayInput)
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

        TryGenerateNextFloor();
    }

    /// <summary>
    /// R8.4 出口入口。失败时返回 false，让当前出口重新待命。
    /// 原有 PlayerReachedExit() 与 GenerateNextFloor() 均继续保留。
    /// </summary>
    public bool TryPlayerReachedExit()
    {
        if (!GameFlowManager.AllowsGameplayInput)
        {
            return false;
        }

        return TryGenerateNextFloor();
    }

    public void GenerateNextFloor()
    {
        TryGenerateNextFloor();
    }

    private bool TryGenerateNextFloor()
    {
        int targetFloorNumber =
            Mathf.Max(1, CurrentFloor + 1);

        return GenerateFloor(targetFloorNumber);
    }

    public void RegenerateCurrentFloor()
    {
        int targetFloorNumber =
            CurrentFloor > 0
                ? CurrentFloor
                : 1;

        GenerateFloor(targetFloorNumber);
    }

    private bool GenerateFloor(int targetFloorNumber)
    {
        if (isGenerating)
        {
            return false;
        }

        CacheComponents();

        if (playerManager == null ||
            enemyManager == null ||
            itemManager == null)
        {
            Debug.LogError(
                "GameManager：PlayerManager、EnemyManager 或 ItemManager 未設定。");

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

            enemyManager.SetupFloor(
                CurrentFloor,
                currentLayout,
                currentDungeonRoot,
                dungeonRenderer,
                player,
                runtimeSpawnReservations);

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
            itemManager != null
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
            "WASD / 方向鍵：移動    R：重生成本層");

        GUI.Label(
            new Rect(24f, 48f, 540f, 24f),
            "黃色方塊：下一層    核心道具：碰觸拾取");

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
            "    Survived Floors: " +
            combatSnapshot.SurvivedFloorCount);
    }
}
