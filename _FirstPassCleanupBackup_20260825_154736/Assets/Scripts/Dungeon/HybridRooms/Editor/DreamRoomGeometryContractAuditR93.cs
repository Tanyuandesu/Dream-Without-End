using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// R9.3：验证非矩形房间的数据、Visual 与 Collider 是否描述同一空间。
///
/// 本工具只读取 R9.1 样本 Prefab。受控失败测试只修改
/// PrefabUtility.LoadPrefabContents 返回的临时副本，绝不保存资产。
/// </summary>
public static class DreamRoomGeometryContractAuditR93
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/";

    private const string GameScenePath =
        "Assets/Scenes/GameScene.unity";

    private const string PrefabPath =
        "Assets/DreamDungeon/Generated/R9_1_NonRectSample/" +
        "Rooms/Room_L10x08_R91.prefab";

    private const string ExpectedTemplateId =
        "R91_LShape_10x08";

    private const string ExpectedGrayboxCatalogId =
        "Graybox_R3";

    private const string ExpectedTestCatalogId =
        "NonRect_R91_Test";

    private const int ExpectedRuntimeRooms = 7;

    private const int ExpectedBoundsCells = 80;
    private const int ExpectedOccupiedCells = 50;
    private const int ExpectedWalkableCells = 49;
    private const int ExpectedBlockedCells = 1;
    private const int ExpectedGapCells = 30;
    private const int ExpectedSockets = 4;
    private const int ExpectedSpawnPoints = 4;
    private const int ExpectedBoundaryEdges = 36;
    private const int ExpectedDoorEdges = 8;
    private const int ExpectedPermanentWalls = 28;

    private const float PositionTolerance = 0.002f;
    private const float PointTolerance = 0.0005f;

    private static readonly Vector2[] VisualProbeOffsets =
    {
        Vector2.zero,
        new Vector2(0.32f, 0f),
        new Vector2(-0.32f, 0f),
        new Vector2(0f, 0.32f),
        new Vector2(0f, -0.32f),
        new Vector2(0.32f, 0.32f),
        new Vector2(-0.32f, 0.32f),
        new Vector2(0.32f, -0.32f),
        new Vector2(-0.32f, -0.32f)
    };

    private static readonly Vector2[] ClearanceProbeOffsets =
    {
        Vector2.zero,
        new Vector2(0.22f, 0f),
        new Vector2(-0.22f, 0f),
        new Vector2(0f, 0.22f),
        new Vector2(0f, -0.22f)
    };

    [MenuItem(
        MenuRoot + "Validate Non-Rect Geometry Contract (R9.3)",
        false,
        2240)]
    private static void ValidateGeometryContract()
    {
        BaselineInfo baseline;
        List<string> setupErrors;

        if (!TryValidateCleanGrayboxBaseline(
                out baseline,
                out setupErrors))
        {
            ReportFailure(
                "R9.3 无法开始",
                setupErrors,
                null);
            return;
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        if (prefabAsset == null)
        {
            setupErrors.Add(
                "找不到 R9.1 样本 Prefab：" + PrefabPath);

            ReportFailure(
                "R9.3 无法开始",
                setupErrors,
                null);
            return;
        }

        if (PrefabUtility.GetPrefabAssetType(prefabAsset) !=
            PrefabAssetType.Regular)
        {
            setupErrors.Add(
                "R9.1 样本必须是独立 Regular Prefab。当前类型=" +
                PrefabUtility.GetPrefabAssetType(prefabAsset));

            ReportFailure(
                "R9.3 无法开始",
                setupErrors,
                prefabAsset);
            return;
        }

        GameObject loadedRoot = null;

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(PrefabPath);

            AuditMetrics metrics;
            List<string> errors =
                AuditLoadedRoom(loadedRoot, out metrics);

            if (errors.Count > 0)
            {
                ReportFailure(
                    "R9.3 数据／Visual／Collider 几何契约失败",
                    errors,
                    prefabAsset);
                return;
            }

            Debug.Log(
                BuildSuccessReport(metrics, baseline),
                prefabAsset);

            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);

            EditorUtility.DisplayDialog(
                "R9.3 Geometry Contract Passed",
                "数据、地板、缺角、内部障碍、轮廓墙、门封块、" +
                "SpawnPoint 与四向旋转全部一致。\n\n" +
                "本次校验没有修改 Prefab、Catalog 或 GameScene。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, prefabAsset);

            EditorUtility.DisplayDialog(
                "R9.3 validation failed",
                "校验过程抛出异常。请保留 Console 第一条红错。",
                "OK");
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(loadedRoot);
            }
        }
    }

    [MenuItem(
        MenuRoot + "Run Geometry Validator Self-Test (R9.3)",
        false,
        2241)]
    private static void RunControlledFailureSelfTest()
    {
        BaselineInfo baseline;
        List<string> setupErrors;

        if (!TryValidateCleanGrayboxBaseline(
                out baseline,
                out setupErrors))
        {
            ReportFailure(
                "R9.3 受控失败自检无法开始",
                setupErrors,
                null);
            return;
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        if (prefabAsset == null)
        {
            setupErrors.Add(
                "找不到 R9.1 样本 Prefab：" + PrefabPath);

            ReportFailure(
                "R9.3 受控失败自检无法开始",
                setupErrors,
                null);
            return;
        }

        Hash128 hashBefore =
            AssetDatabase.GetAssetDependencyHash(PrefabPath);

        GameObject loadedRoot = null;
        bool obstacleShiftDetected = false;
        bool gapFloorDetected = false;
        bool boundaryWallDetected = false;
        List<string> selfTestErrors = new List<string>();

        try
        {
            loadedRoot =
                PrefabUtility.LoadPrefabContents(PrefabPath);

            AuditMetrics baselineMetrics;
            List<string> positiveErrors =
                AuditLoadedRoom(
                    loadedRoot,
                    out baselineMetrics);

            if (positiveErrors.Count > 0)
            {
                selfTestErrors.Add(
                    "[PositiveControl] 原始 Prefab 本身未通过，" +
                    "不能执行受控失败自检。");

                AppendErrors(
                    selfTestErrors,
                    positiveErrors,
                    maximumCount: 12);
            }
            else
            {
                Transform visualRoot =
                    FindDirectChild(
                        loadedRoot.transform,
                        "Visual");

                Transform floorsRoot =
                    FindDirectChild(visualRoot, "Floors");

                Transform wallsRoot =
                    FindDirectChild(visualRoot, "Walls");

                Transform obstaclesRoot =
                    FindDirectChild(visualRoot, "Obstacles");

                Transform obstacle =
                    FindDirectChild(
                        obstaclesRoot,
                        "Blocked_02_01");

                Transform bottomFloor =
                    FindDirectChild(
                        floorsRoot,
                        "Floor_BottomArm");

                BoxCollider2D[] wallColliders =
                    wallsRoot == null
                        ? new BoxCollider2D[0]
                        : wallsRoot.GetComponentsInChildren<
                            BoxCollider2D>(true);

                if (obstacle == null ||
                    bottomFloor == null ||
                    wallColliders.Length == 0)
                {
                    selfTestErrors.Add(
                        "[SelfTestSetup] 找不到受控失败所需的" +
                        "障碍、地板或轮廓墙对象。");
                }
                else
                {
                    Vector3 originalObstaclePosition =
                        obstacle.localPosition;

                    obstacle.localPosition =
                        originalObstaclePosition + Vector3.right;

                    AuditMetrics ignoredMetrics;
                    List<string> obstacleErrors =
                        AuditLoadedRoom(
                            loadedRoot,
                            out ignoredMetrics);

                    obstacleShiftDetected =
                        ContainsErrorCode(
                            obstacleErrors,
                            "BlockedCollider") &&
                        ContainsErrorCode(
                            obstacleErrors,
                            "WalkableClearance");

                    obstacle.localPosition =
                        originalObstaclePosition;

                    Vector3 originalFloorScale =
                        bottomFloor.localScale;

                    bottomFloor.localScale =
                        new Vector3(
                            originalFloorScale.x,
                            5f,
                            originalFloorScale.z);

                    List<string> gapErrors =
                        AuditLoadedRoom(
                            loadedRoot,
                            out ignoredMetrics);

                    gapFloorDetected =
                        ContainsErrorCode(
                            gapErrors,
                            "GapVisual");

                    bottomFloor.localScale =
                        originalFloorScale;

                    Transform wallTransform =
                        wallColliders[0].transform;

                    Vector3 originalWallPosition =
                        wallTransform.localPosition;

                    wallTransform.localPosition =
                        originalWallPosition +
                        new Vector3(0.45f, 0.45f, 0f);

                    List<string> wallErrors =
                        AuditLoadedRoom(
                            loadedRoot,
                            out ignoredMetrics);

                    boundaryWallDetected =
                        ContainsErrorCode(
                            wallErrors,
                            "BoundaryWall");

                    wallTransform.localPosition =
                        originalWallPosition;
                }
            }
        }
        catch (Exception exception)
        {
            selfTestErrors.Add(
                "[Exception] 受控失败自检抛出异常：" +
                exception);
        }
        finally
        {
            if (loadedRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(loadedRoot);
            }
        }

        Hash128 hashAfter =
            AssetDatabase.GetAssetDependencyHash(PrefabPath);

        bool assetHashUnchanged =
            hashBefore.Equals(hashAfter);

        if (!obstacleShiftDetected)
        {
            selfTestErrors.Add(
                "[Detector] 未同时捕获“Blocked 失去碰撞”与" +
                "“Walkable 被障碍侵入”。");
        }

        if (!gapFloorDetected)
        {
            selfTestErrors.Add(
                "[Detector] 未捕获地板侵入 L 形缺角。");
        }

        if (!boundaryWallDetected)
        {
            selfTestErrors.Add(
                "[Detector] 未捕获轮廓墙偏移。");
        }

        if (!assetHashUnchanged)
        {
            selfTestErrors.Add(
                "[AssetSafety] 受控失败前后 Prefab 依赖哈希变化。" +
                "不要保存工程并立即提交此日志。");
        }

        if (selfTestErrors.Count > 0)
        {
            ReportFailure(
                "R9.3 受控失败自检失败",
                selfTestErrors,
                prefabAsset);
            return;
        }

        Debug.Log(
            "[DreamRoomGeometryContractAuditR93] " +
            "R9.3 受控失败自检通过\n" +
            "ObstacleShiftDetected=True" +
            " | GapFloorIntrusionDetected=True" +
            " | BoundaryWallShiftDetected=True\n" +
            "PrefabSaved=False" +
            " | AssetHashUnchanged=True" +
            " | GameSceneChanged=False" +
            " | Catalog=" + baseline.CatalogId,
            prefabAsset);

        EditorUtility.DisplayDialog(
            "R9.3 Self-Test Passed",
            "三种错误均被验证器捕获：\n" +
            "- Blocked 障碍移位\n" +
            "- 地板侵入 L 形缺角\n" +
            "- 轮廓墙移位\n\n" +
            "全部修改仅发生在临时副本，Prefab 未保存。",
            "OK");
    }

    [MenuItem(
        MenuRoot + "Validate Live Non-Rect Render (R9.3)",
        false,
        2242)]
    private static void ValidateLiveNonRectRender()
    {
        List<string> setupErrors = new List<string>();

        if (!EditorApplication.isPlaying)
        {
            setupErrors.Add(
                "此项必须在 NonRect_R91_Test 的 Play Mode 中执行。");

            ReportFailure(
                "R9.3 运行时渲染审计无法开始",
                setupErrors,
                null);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            !string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            setupErrors.Add(
                "Play Mode 的活动 Scene 必须是 " +
                GameScenePath + "。");
        }

        GameManager gameManager =
            FindSceneComponent<GameManager>(scene);

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

        if (gameManager == null)
        {
            setupErrors.Add("GameScene 中找不到 GameManager。");
        }

        if (generator == null)
        {
            setupErrors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            setupErrors.Add("GameScene 中找不到 DungeonRenderer。");
        }

        if (generator != null)
        {
            DreamRoomCatalog catalog =
                generator.TemplateFirstRoomCatalog;

            string catalogId =
                catalog == null
                    ? "<null>"
                    : catalog.CatalogId;

            if (!string.Equals(
                    catalogId,
                    ExpectedTestCatalogId,
                    StringComparison.Ordinal))
            {
                setupErrors.Add(
                    "当前 Catalog 必须是 NonRect_R91_Test，实际为 " +
                    catalogId + "。请先退出 Play Mode，执行 R9.2 Prepare，" +
                    "再重新进入 Play Mode。");
            }
        }

        if (renderer != null &&
            renderer.RenderMode !=
                DungeonRenderMode.HybridPrefabRooms)
        {
            setupErrors.Add(
                "Renderer 必须为 Hybrid Prefab Rooms。");
        }

        if (gameManager != null)
        {
            if (gameManager.CurrentFloor != 1 ||
                gameManager.CurrentSeed != 12345)
            {
                setupErrors.Add(
                    "运行时必须停在 Floor 1 / Seed 12345，实际为 " +
                    gameManager.CurrentFloor + " / " +
                    gameManager.CurrentSeed + "。");
            }

            if (gameManager.RequestedGenerationMode !=
                    DungeonRenderMode.HybridPrefabRooms ||
                gameManager.EffectiveGenerationMode !=
                    DungeonRenderMode.HybridPrefabRooms)
            {
                setupErrors.Add(
                    "Requested／Effective 必须均为 HybridPrefabRooms。");
            }
        }

        if (setupErrors.Count > 0)
        {
            ReportFailure(
                "R9.3 运行时渲染审计无法开始",
                setupErrors,
                renderer);
            return;
        }

        DungeonLayout currentLayout;

        if (!TryReadCurrentLayout(
                gameManager,
                out currentLayout))
        {
            setupErrors.Add(
                "无法读取 GameManager.currentLayout。" +
                "R9.0 基线字段可能被改名。");

            ReportFailure(
                "R9.3 运行时渲染审计无法开始",
                setupErrors,
                gameManager);
            return;
        }

        RuntimeMetrics metrics;
        List<string> errors =
            AuditLiveRender(
                gameManager,
                renderer,
                currentLayout,
                out metrics);

        if (errors.Count > 0)
        {
            ReportFailure(
                "R9.3 运行时 Renderer／Collider 审计失败",
                errors,
                renderer);
            return;
        }

        Debug.Log(
            BuildRuntimeSuccessReport(metrics),
            renderer);

        EditorUtility.DisplayDialog(
            "R9.3 Live Render Passed",
            "七间旋转实例、门状态、Blocked 碰撞／寻路排除、" +
            "走廊地板与走廊墙实例全部匹配当前 DungeonLayout。\n\n" +
            "退出 Play Mode 后仍须使用 R9.2 Restore 恢复 Graybox。",
            "OK");
    }

    private static List<string> AuditLiveRender(
        GameManager gameManager,
        DungeonRenderer renderer,
        DungeonLayout layout,
        out RuntimeMetrics metrics)
    {
        metrics = new RuntimeMetrics();
        List<string> errors = new List<string>();

        if (layout == null)
        {
            AddError(
                errors,
                "RuntimeLayout",
                "GameManager.currentLayout 为空。");
            return errors;
        }

        List<string> layoutErrors =
            layout.GetValidationErrors();

        for (int i = 0; i < layoutErrors.Count; i++)
        {
            AddError(
                errors,
                "RuntimeLayout",
                layoutErrors[i]);
        }

        metrics.Rooms = layout.RoomPlacements.Count;
        metrics.Connections = layout.Connections.Count;
        metrics.CorridorFloorExpected =
            layout.CorridorCells.Count;

        if (metrics.Rooms != ExpectedRuntimeRooms)
        {
            AddError(
                errors,
                "RuntimeRoomCount",
                "RoomPlacements 应为 7，实际为 " +
                metrics.Rooms + "。");
        }

        Transform dungeonRoot =
            FindDirectChild(
                gameManager.transform,
                "GeneratedDungeon_Floor_1");

        if (dungeonRoot == null)
        {
            AddError(
                errors,
                "RuntimeHierarchy",
                "GameManager 下找不到 GeneratedDungeon_Floor_1。");
            return errors;
        }

        Transform roomsRoot =
            FindDirectChild(dungeonRoot, "Rooms");

        Transform corridorsRoot =
            FindDirectChild(dungeonRoot, "Corridors");

        Transform corridorWallsRoot =
            FindDirectChild(dungeonRoot, "CorridorWalls");

        if (roomsRoot == null ||
            corridorsRoot == null ||
            corridorWallsRoot == null)
        {
            AddError(
                errors,
                "RuntimeHierarchy",
                "GeneratedDungeon 必须包含 Rooms、Corridors 与 " +
                "CorridorWalls 三个直接子根节点。");
            return errors;
        }

        HashSet<Vector2Int> occupiedGlobal =
            new HashSet<Vector2Int>();

        List<Vector2Int> occupiedBuffer =
            new List<Vector2Int>();

        List<Vector2Int> walkableBuffer =
            new List<Vector2Int>();

        List<Vector2Int> blockedBuffer =
            new List<Vector2Int>();

        Vector3 gridZero =
            renderer.CellToWorld(Vector2Int.zero);

        PlayerManager playerManager =
            FindSceneComponent<PlayerManager>(
                gameManager.gameObject.scene);

        GameObject playerObject =
            playerManager == null
                ? null
                : playerManager.CurrentPlayerObject;

        BoxCollider2D playerCollider =
            playerObject == null
                ? null
                : playerObject.GetComponent<BoxCollider2D>();

        Rigidbody2D playerBody =
            playerObject == null
                ? null
                : playerObject.GetComponent<Rigidbody2D>();

        if (playerObject == null ||
            playerCollider == null ||
            playerBody == null ||
            !playerCollider.enabled ||
            !playerBody.simulated)
        {
            AddError(
                errors,
                "RuntimePlayerPhysics",
                "当前 Player 必须具有已启用的 BoxCollider2D 与" +
                "正在模拟的 Rigidbody2D。");
        }

        if (roomsRoot.childCount !=
            layout.RoomPlacements.Count)
        {
            AddError(
                errors,
                "RuntimeRoomInstances",
                "Rooms 实例数应为 " +
                layout.RoomPlacements.Count +
                "，实际为 " + roomsRoot.childCount + "。");
        }

        int comparableRooms = Mathf.Min(
            roomsRoot.childCount,
            layout.RoomPlacements.Count);

        Dictionary<int, HashSet<string>> expectedOpenSockets =
            BuildExpectedOpenSocketMap(layout, errors);

        for (int roomIndex = 0;
             roomIndex < comparableRooms;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                layout.RoomPlacements[roomIndex];

            Transform instanceRoot =
                roomsRoot.GetChild(roomIndex);

            DreamRoomTemplate instanceTemplate =
                instanceRoot.GetComponent<DreamRoomTemplate>();

            if (placement == null ||
                placement.Template == null ||
                instanceTemplate == null)
            {
                AddError(
                    errors,
                    "RuntimeRoomInstance",
                    "Room " + roomIndex +
                    " 的 Placement、Asset Template 或实例 Template 为空。");
                continue;
            }

            if (!string.Equals(
                    placement.Template.TemplateId,
                    ExpectedTemplateId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    instanceTemplate.TemplateId,
                    ExpectedTemplateId,
                    StringComparison.Ordinal))
            {
                AddError(
                    errors,
                    "RuntimeTemplate",
                    "Room " + roomIndex +
                    " 未使用 R91_LShape_10x08。");
            }

            Vector3 expectedPosition =
                placement.GetRoomRootWorldPosition(
                    gridZero,
                    renderer.CellSize);

            if (Vector3.Distance(
                    instanceRoot.position,
                    expectedPosition) > PositionTolerance ||
                Quaternion.Angle(
                    instanceRoot.rotation,
                    placement.WorldRotation) > 0.02f ||
                instanceRoot.localScale != Vector3.one)
            {
                AddError(
                    errors,
                    "RuntimeRoomPose",
                    "Room " + roomIndex +
                    " 的实例 Pose 与 Placement 不一致。");
            }
            else
            {
                metrics.RoomPosesMatched++;
            }

            placement.GetOccupiedGlobalCells(
                occupiedBuffer);

            placement.GetWalkableGlobalCells(
                walkableBuffer);

            placement.GetBlockedGlobalCells(
                blockedBuffer);

            occupiedGlobal.UnionWith(occupiedBuffer);
            metrics.OccupiedCells += occupiedBuffer.Count;
            metrics.WalkableCells += walkableBuffer.Count;
            metrics.BlockedCells += blockedBuffer.Count;

            BoxCollider2D[] instanceColliders =
                instanceRoot.GetComponentsInChildren<
                    BoxCollider2D>(true);

            Transform obstacles =
                FindDirectChild(
                    instanceTemplate.VisualRoot,
                    "Obstacles");

            BoxCollider2D[] obstacleColliders =
                obstacles == null
                    ? new BoxCollider2D[0]
                    : obstacles.GetComponentsInChildren<
                        BoxCollider2D>(true);

            if (obstacleColliders.Length != 1)
            {
                AddError(
                    errors,
                    "RuntimeObstacleCount",
                    "Room " + roomIndex +
                    " 应有 1 个 Blocked 障碍 Collider，实际为 " +
                    obstacleColliders.Length + "。");
            }

            for (int obstacleIndex = 0;
                 obstacleIndex < obstacleColliders.Length;
                 obstacleIndex++)
            {
                BoxCollider2D obstacleCollider =
                    obstacleColliders[obstacleIndex];

                bool collisionIgnored =
                    playerCollider != null &&
                    (Physics2D.GetIgnoreLayerCollision(
                         playerObject.layer,
                         obstacleCollider.gameObject.layer) ||
                     Physics2D.GetIgnoreCollision(
                         playerCollider,
                         obstacleCollider));

                if (!IsBlockingCollider(obstacleCollider) ||
                    playerCollider == null ||
                    collisionIgnored)
                {
                    AddError(
                        errors,
                        "RuntimePhysicsPair",
                        "Room " + roomIndex +
                        " 的 Blocked 障碍不会与当前 Player 发生实体碰撞。");
                }
                else
                {
                    metrics.PlayerObstaclePairsEnabled++;
                }
            }

            Transform floors =
                FindDirectChild(
                    instanceTemplate.VisualRoot,
                    "Floors");

            SpriteRenderer[] floorRenderers =
                floors == null
                    ? new SpriteRenderer[0]
                    : floors.GetComponentsInChildren<
                        SpriteRenderer>(true);

            for (int cellIndex = 0;
                 cellIndex < walkableBuffer.Count;
                 cellIndex++)
            {
                Vector2Int globalCell =
                    walkableBuffer[cellIndex];

                Vector3 world =
                    renderer.CellToWorld(globalCell);

                if (CountBoxesAtWorldPoint(
                        instanceColliders,
                        world) != 0)
                {
                    AddError(
                        errors,
                        "RuntimeWalkableCollider",
                        "Room " + roomIndex +
                        " 的 Walkable Global Cell " +
                        CellLabel(globalCell) +
                        " 中心被实例 Collider 阻挡。");
                }
                else
                {
                    metrics.WalkableCentersClear++;
                }
            }

            for (int cellIndex = 0;
                 cellIndex < blockedBuffer.Count;
                 cellIndex++)
            {
                Vector2Int globalCell =
                    blockedBuffer[cellIndex];

                Vector3 world =
                    renderer.CellToWorld(globalCell);

                if (layout.RoomCells.Contains(globalCell) ||
                    layout.FloorCells.Contains(globalCell))
                {
                    AddError(
                        errors,
                        "RuntimeBlockedPath",
                        "Blocked Global Cell " +
                        CellLabel(globalCell) +
                        " 错误进入 RoomCells/FloorCells。");
                }
                else
                {
                    metrics.BlockedPathExcluded++;
                }

                if (CountBoxesAtWorldPoint(
                        instanceColliders,
                        world) != 1)
                {
                    AddError(
                        errors,
                        "RuntimeBlockedCollider",
                        "Blocked Global Cell " +
                        CellLabel(globalCell) +
                        " 应恰好有 1 个实例 Collider。");
                }
                else
                {
                    metrics.BlockedCentersSolid++;
                }
            }

            RectInt bounds = placement.CellBounds;

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int globalCell =
                        new Vector2Int(x, y);

                    if (occupiedGlobal.Contains(globalCell) ||
                        placement.ContainsOccupiedGlobalCell(
                            globalCell))
                    {
                        continue;
                    }

                    Vector3 world =
                        renderer.CellToWorld(globalCell);

                    bool hasFloor =
                        CountSpritesAtWorldPoint(
                            floorRenderers,
                            world) > 0;

                    bool hasCollider =
                        CountBoxesAtWorldPoint(
                            instanceColliders,
                            world) > 0;

                    if (hasFloor || hasCollider)
                    {
                        AddError(
                            errors,
                            "RuntimeGapGeometry",
                            "Room " + roomIndex +
                            " 的缺角 Global Cell " +
                            CellLabel(globalCell) +
                            " 中心出现 Floor 或 Collider。");
                    }
                    else
                    {
                        metrics.GapCentersClear++;
                    }
                }
            }

            ValidateRuntimeSocketStates(
                roomIndex,
                instanceTemplate,
                expectedOpenSockets,
                metrics,
                errors);
        }

        if (metrics.OccupiedCells != 350 ||
            metrics.WalkableCells != 343 ||
            metrics.BlockedCells != 7)
        {
            AddError(
                errors,
                "RuntimeCellCounts",
                "七房 Occupied/Walkable/Blocked 应为 350/343/7，" +
                "实际为 " +
                metrics.OccupiedCells + "/" +
                metrics.WalkableCells + "/" +
                metrics.BlockedCells + "。");
        }

        if (metrics.Connections != 8 ||
            metrics.OpenSockets != 16 ||
            metrics.ClosedSockets != 12)
        {
            AddError(
                errors,
                "RuntimeDoorCounts",
                "Connections/Open/Closed 应为 8/16/12，实际为 " +
                metrics.Connections + "/" +
                metrics.OpenSockets + "/" +
                metrics.ClosedSockets + "。");
        }

        foreach (Vector2Int corridorCell in
                 layout.CorridorCells)
        {
            if (occupiedGlobal.Contains(corridorCell))
            {
                metrics.CorridorInOccupied++;

                AddError(
                    errors,
                    "RuntimeCorridorOccupied",
                    "Corridor Cell " +
                    CellLabel(corridorCell) +
                    " 与房间 Occupied 重叠。");
            }
        }

        HashSet<Vector2Int> expectedWallCells =
            BuildExpectedCorridorWallCells(
                layout,
                occupiedGlobal);

        metrics.CorridorWallExpected =
            expectedWallCells.Count;

        ValidateRuntimeCellObjects(
            corridorsRoot,
            "CorridorFloor_",
            layout.CorridorCells,
            renderer,
            false,
            occupiedGlobal,
            out metrics.CorridorFloorActual,
            out metrics.CorridorFloorMismatches,
            errors);

        ValidateRuntimeCellObjects(
            corridorWallsRoot,
            "CorridorWall_",
            expectedWallCells,
            renderer,
            true,
            occupiedGlobal,
            out metrics.CorridorWallActual,
            out metrics.CorridorWallMismatches,
            errors);

        return errors;
    }

    private static void ValidateRuntimeSocketStates(
        int roomIndex,
        DreamRoomTemplate instanceTemplate,
        Dictionary<int, HashSet<string>> expectedOpenSockets,
        RuntimeMetrics metrics,
        List<string> errors)
    {
        HashSet<string> expectedForRoom;

        if (!expectedOpenSockets.TryGetValue(
                roomIndex,
                out expectedForRoom))
        {
            expectedForRoom =
                new HashSet<string>(StringComparer.Ordinal);
        }

        for (int socketIndex = 0;
             socketIndex < instanceTemplate.DoorSockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                instanceTemplate.DoorSockets[socketIndex];

            if (socket == null)
            {
                AddError(
                    errors,
                    "RuntimeSocket",
                    "Room " + roomIndex +
                    " 的 Door Sockets 含空引用。");
                continue;
            }

            bool shouldBeOpen =
                expectedForRoom.Contains(socket.SocketId);

            bool blockerActive =
                socket.ClosedBlocker != null &&
                socket.ClosedBlocker.activeSelf;

            if (socket.IsOpen != shouldBeOpen ||
                blockerActive == shouldBeOpen)
            {
                AddError(
                    errors,
                    "RuntimeDoorState",
                    "Room " + roomIndex +
                    " Socket '" + socket.SocketId +
                    "' 的 IsOpen／Blocker 状态与 Connection 不一致。");
            }
            else if (shouldBeOpen)
            {
                metrics.OpenSockets++;
            }
            else
            {
                metrics.ClosedSockets++;
            }
        }
    }

    private static Dictionary<int, HashSet<string>>
        BuildExpectedOpenSocketMap(
            DungeonLayout layout,
            List<string> errors)
    {
        Dictionary<int, HashSet<string>> result =
            new Dictionary<int, HashSet<string>>();

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                layout.Connections[connectionIndex];

            if (connection == null ||
                !connection.HasAssignedSockets)
            {
                AddError(
                    errors,
                    "RuntimeConnection",
                    "Connection " + connectionIndex +
                    " 为空或尚未分配 Socket。");
                continue;
            }

            AddExpectedSocket(
                result,
                connection.RoomAIndex,
                connection.SocketAId,
                errors);

            AddExpectedSocket(
                result,
                connection.RoomBIndex,
                connection.SocketBId,
                errors);
        }

        return result;
    }

    private static void AddExpectedSocket(
        Dictionary<int, HashSet<string>> map,
        int roomIndex,
        string socketId,
        List<string> errors)
    {
        HashSet<string> socketIds;

        if (!map.TryGetValue(roomIndex, out socketIds))
        {
            socketIds =
                new HashSet<string>(StringComparer.Ordinal);

            map.Add(roomIndex, socketIds);
        }

        if (!socketIds.Add(socketId))
        {
            AddError(
                errors,
                "RuntimeSocketReuse",
                "Room " + roomIndex +
                " 的 Socket '" + socketId +
                "' 被多个 Connection 重复使用。");
        }
    }

    private static HashSet<Vector2Int>
        BuildExpectedCorridorWallCells(
            DungeonLayout layout,
            HashSet<Vector2Int> occupiedGlobal)
    {
        Vector2Int[] eightDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        HashSet<Vector2Int> wallCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int corridorCell in
                 layout.CorridorCells)
        {
            for (int directionIndex = 0;
                 directionIndex < eightDirections.Length;
                 directionIndex++)
            {
                Vector2Int candidate =
                    corridorCell +
                    eightDirections[directionIndex];

                if (layout.FloorCells.Contains(candidate) ||
                    occupiedGlobal.Contains(candidate))
                {
                    continue;
                }

                wallCells.Add(candidate);
            }
        }

        return wallCells;
    }

    private static void ValidateRuntimeCellObjects(
        Transform root,
        string namePrefix,
        ICollection<Vector2Int> expectedCells,
        DungeonRenderer renderer,
        bool expectCollider,
        HashSet<Vector2Int> occupiedGlobal,
        out int actualCount,
        out int mismatchCount,
        List<string> errors)
    {
        actualCount = root.childCount;
        mismatchCount = 0;

        HashSet<Vector2Int> actualCells =
            new HashSet<Vector2Int>();

        for (int childIndex = 0;
             childIndex < root.childCount;
             childIndex++)
        {
            Transform child = root.GetChild(childIndex);
            Vector2Int cell;

            if (!TryParseCellObjectName(
                    child.name,
                    namePrefix,
                    out cell))
            {
                mismatchCount++;

                AddError(
                    errors,
                    "RuntimeCellName",
                    "对象 '" + child.name +
                    "' 不符合 " + namePrefix +
                    "x_y 命名契约。");
                continue;
            }

            if (!actualCells.Add(cell) ||
                !expectedCells.Contains(cell))
            {
                mismatchCount++;

                AddError(
                    errors,
                    "RuntimeCellSet",
                    "对象 '" + child.name +
                    "' 是重复或非预期格。");
            }

            Vector3 expectedWorld =
                renderer.CellToWorld(cell);

            if (Vector3.Distance(
                    child.position,
                    expectedWorld) > PositionTolerance ||
                child.localScale !=
                    new Vector3(
                        renderer.CellSize,
                        renderer.CellSize,
                        1f))
            {
                mismatchCount++;

                AddError(
                    errors,
                    "RuntimeCellPose",
                    "对象 '" + child.name +
                    "' 的位置或 Scale 与格子不一致。");
            }

            SpriteRenderer spriteRenderer =
                child.GetComponent<SpriteRenderer>();

            BoxCollider2D collider =
                child.GetComponent<BoxCollider2D>();

            if (spriteRenderer == null ||
                (expectCollider &&
                 !IsBlockingCollider(collider)) ||
                (!expectCollider && collider != null))
            {
                mismatchCount++;

                AddError(
                    errors,
                    "RuntimeCellComponents",
                    "对象 '" + child.name +
                    "' 的 Sprite／Collider 组合错误。");
            }

            if (occupiedGlobal.Contains(cell))
            {
                mismatchCount++;

                AddError(
                    errors,
                    "RuntimeWallOccupied",
                    "对象 '" + child.name +
                    "' 错误进入房间 Occupied Global Cell。");
            }
        }

        if (actualCells.Count != expectedCells.Count)
        {
            mismatchCount++;

            AddError(
                errors,
                "RuntimeCellCount",
                namePrefix + " 实例应为 " +
                expectedCells.Count +
                "，实际唯一格为 " +
                actualCells.Count + "。");
        }
    }

    private static bool TryParseCellObjectName(
        string objectName,
        string prefix,
        out Vector2Int cell)
    {
        cell = Vector2Int.zero;

        if (string.IsNullOrEmpty(objectName) ||
            !objectName.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string coordinates =
            objectName.Substring(prefix.Length);

        string[] parts = coordinates.Split('_');

        int x;
        int y;

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out x) ||
            !int.TryParse(parts[1], out y))
        {
            return false;
        }

        cell = new Vector2Int(x, y);
        return true;
    }

    private static bool TryReadCurrentLayout(
        GameManager gameManager,
        out DungeonLayout layout)
    {
        layout = null;

        FieldInfo field =
            typeof(GameManager).GetField(
                "currentLayout",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            return false;
        }

        layout = field.GetValue(gameManager) as DungeonLayout;
        return layout != null;
    }

    private static string BuildRuntimeSuccessReport(
        RuntimeMetrics metrics)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomGeometryContractAuditR93] " +
            "R9.3 运行时 Renderer／Collider 审计通过");

        report.AppendLine(
            "Catalog=NonRect_R91_Test" +
            " | Floor=1 | Seed=12345" +
            " | Hybrid→Hybrid");

        report.AppendLine(
            "Rooms=" + metrics.Rooms + "/7" +
            " | RoomPoses=" +
            metrics.RoomPosesMatched + "/7" +
            " | Connections=" + metrics.Connections);

        report.AppendLine(
            "Cells: Occupied=" + metrics.OccupiedCells +
            " | Walkable=" + metrics.WalkableCells +
            " | Blocked=" + metrics.BlockedCells +
            " | GapCentersClear=" +
            metrics.GapCentersClear + "/210");

        report.AppendLine(
            "Physics/Path: WalkableClear=" +
            metrics.WalkableCentersClear + "/343" +
            " | BlockedSolid=" +
            metrics.BlockedCentersSolid + "/7" +
            " | BlockedPathExcluded=" +
            metrics.BlockedPathExcluded + "/7" +
            " | PlayerCollisionPairs=" +
            metrics.PlayerObstaclePairsEnabled + "/7");

        report.AppendLine(
            "Doors: Open=" + metrics.OpenSockets + "/16" +
            " | Closed=" + metrics.ClosedSockets + "/12");

        report.AppendLine(
            "CorridorFloors=" +
            metrics.CorridorFloorActual + "/" +
            metrics.CorridorFloorExpected +
            " | CorridorWalls=" +
            metrics.CorridorWallActual + "/" +
            metrics.CorridorWallExpected);

        report.Append(
            "CorridorInOccupied=" +
            metrics.CorridorInOccupied +
            " | FloorMismatches=" +
            metrics.CorridorFloorMismatches +
            " | WallMismatches=" +
            metrics.CorridorWallMismatches +
            " | RuntimeObjectsModified=False");

        return report.ToString();
    }

    private static List<string> AuditLoadedRoom(
        GameObject loadedRoot,
        out AuditMetrics metrics)
    {
        metrics = new AuditMetrics();
        List<string> errors = new List<string>();

        if (loadedRoot == null)
        {
            AddError(
                errors,
                "Prefab",
                "加载后的 Prefab 根节点为空。");
            return errors;
        }

        Transform roomRoot = loadedRoot.transform;

        if (roomRoot.localPosition != Vector3.zero ||
            roomRoot.localRotation != Quaternion.identity ||
            roomRoot.localScale != Vector3.one)
        {
            AddError(
                errors,
                "RootTransform",
                "Prefab 根 Transform 必须为 Position 0、" +
                "Rotation 0、Scale 1。");
        }

        DreamRoomTemplate template =
            loadedRoot.GetComponent<DreamRoomTemplate>();

        if (template == null)
        {
            AddError(
                errors,
                "Template",
                "Prefab 根节点缺少 DreamRoomTemplate。");
            return errors;
        }

        if (!string.Equals(
                template.TemplateId,
                ExpectedTemplateId,
                StringComparison.Ordinal))
        {
            AddError(
                errors,
                "TemplateId",
                "Template Id 应为 '" +
                ExpectedTemplateId + "'，实际为 '" +
                template.TemplateId + "'。");
        }

        List<string> templateErrors =
            template.GetValidationErrors();

        for (int i = 0; i < templateErrors.Count; i++)
        {
            AddError(
                errors,
                "TemplateData",
                templateErrors[i]);
        }

        List<Vector2Int> occupied =
            new List<Vector2Int>();

        List<Vector2Int> walkable =
            new List<Vector2Int>();

        List<Vector2Int> blocked =
            new List<Vector2Int>();

        template.GetOccupiedCells(occupied);
        template.GetWalkableCells(walkable);
        template.GetBlockedCells(blocked);

        HashSet<Vector2Int> occupiedSet =
            new HashSet<Vector2Int>(occupied);

        HashSet<Vector2Int> walkableSet =
            new HashSet<Vector2Int>(walkable);

        HashSet<Vector2Int> blockedSet =
            new HashSet<Vector2Int>(blocked);

        metrics.BoundsCells =
            template.SizeInCells.x * template.SizeInCells.y;

        metrics.OccupiedCells = occupiedSet.Count;
        metrics.WalkableCells = walkableSet.Count;
        metrics.BlockedCells = blockedSet.Count;

        if (occupied.Count != occupiedSet.Count)
        {
            AddError(
                errors,
                "OccupiedData",
                "Occupied Cells 含重复格。");
        }

        if (walkable.Count != walkableSet.Count)
        {
            AddError(
                errors,
                "WalkableData",
                "Walkable Cells 含重复格。");
        }

        if (blocked.Count != blockedSet.Count)
        {
            AddError(
                errors,
                "BlockedData",
                "Blocked Cells 含重复格。");
        }

        if (metrics.BoundsCells != ExpectedBoundsCells ||
            metrics.OccupiedCells != ExpectedOccupiedCells ||
            metrics.WalkableCells != ExpectedWalkableCells ||
            metrics.BlockedCells != ExpectedBlockedCells)
        {
            AddError(
                errors,
                "CellCounts",
                "应为 Bounds/Occupied/Walkable/Blocked=" +
                "80/50/49/1，实际为 " +
                metrics.BoundsCells + "/" +
                metrics.OccupiedCells + "/" +
                metrics.WalkableCells + "/" +
                metrics.BlockedCells + "。");
        }

        foreach (Vector2Int cell in walkableSet)
        {
            if (!occupiedSet.Contains(cell) ||
                blockedSet.Contains(cell))
            {
                AddError(
                    errors,
                    "WalkableData",
                    "Walkable 格 " + CellLabel(cell) +
                    " 未满足 Occupied 且非 Blocked。");
            }
        }

        foreach (Vector2Int cell in blockedSet)
        {
            if (!occupiedSet.Contains(cell) ||
                walkableSet.Contains(cell))
            {
                AddError(
                    errors,
                    "BlockedData",
                    "Blocked 格 " + CellLabel(cell) +
                    " 未满足 Occupied 且非 Walkable。");
            }
        }

        List<Vector2Int> gaps =
            BuildGapCells(template, occupiedSet);

        metrics.GapCells = gaps.Count;

        if (metrics.GapCells != ExpectedGapCells)
        {
            AddError(
                errors,
                "GapData",
                "Bounding Gap 应为 30，实际为 " +
                metrics.GapCells + "。");
        }

        Transform visualRoot = template.VisualRoot;
        Transform socketsRoot = template.SocketsRoot;
        Transform navigationRoot = template.NavigationRoot;
        Transform spawnPointsRoot = template.SpawnPointsRoot;

        ValidateExpectedRoot(
            roomRoot,
            visualRoot,
            "Visual",
            errors);

        ValidateExpectedRoot(
            roomRoot,
            socketsRoot,
            "Sockets",
            errors);

        ValidateExpectedRoot(
            roomRoot,
            navigationRoot,
            "Navigation",
            errors);

        ValidateExpectedRoot(
            roomRoot,
            spawnPointsRoot,
            "SpawnPoints",
            errors);

        if (visualRoot == null)
        {
            return errors;
        }

        Transform floorsRoot =
            FindDirectChild(visualRoot, "Floors");

        Transform wallsRoot =
            FindDirectChild(visualRoot, "Walls");

        Transform blockersRoot =
            FindDirectChild(visualRoot, "DoorBlockers");

        Transform obstaclesRoot =
            FindDirectChild(visualRoot, "Obstacles");

        RequireVisualChild(
            floorsRoot,
            "Floors",
            errors);

        RequireVisualChild(
            wallsRoot,
            "Walls",
            errors);

        RequireVisualChild(
            blockersRoot,
            "DoorBlockers",
            errors);

        RequireVisualChild(
            obstaclesRoot,
            "Obstacles",
            errors);

        if (floorsRoot == null ||
            wallsRoot == null ||
            blockersRoot == null ||
            obstaclesRoot == null)
        {
            return errors;
        }

        SpriteRenderer[] floorRenderers =
            floorsRoot.GetComponentsInChildren<
                SpriteRenderer>(true);

        SpriteRenderer[] wallRenderers =
            wallsRoot.GetComponentsInChildren<
                SpriteRenderer>(true);

        SpriteRenderer[] blockerRenderers =
            blockersRoot.GetComponentsInChildren<
                SpriteRenderer>(true);

        SpriteRenderer[] obstacleRenderers =
            obstaclesRoot.GetComponentsInChildren<
                SpriteRenderer>(true);

        BoxCollider2D[] wallColliders =
            wallsRoot.GetComponentsInChildren<
                BoxCollider2D>(true);

        BoxCollider2D[] blockerColliders =
            blockersRoot.GetComponentsInChildren<
                BoxCollider2D>(true);

        BoxCollider2D[] obstacleColliders =
            obstaclesRoot.GetComponentsInChildren<
                BoxCollider2D>(true);

        metrics.FloorRenderers = floorRenderers.Length;
        metrics.WallColliders = wallColliders.Length;
        metrics.DoorBlockerColliders =
            blockerColliders.Length;
        metrics.ObstacleColliders =
            obstacleColliders.Length;

        ValidateColliderOwnership(
            loadedRoot,
            visualRoot,
            floorsRoot,
            wallsRoot,
            blockersRoot,
            obstaclesRoot,
            errors);

        ValidateRendererKinds(
            visualRoot,
            floorRenderers,
            wallRenderers,
            blockerRenderers,
            obstacleRenderers,
            errors);

        ValidateCellVisualsAndClearance(
            template,
            occupiedSet,
            walkableSet,
            blockedSet,
            gaps,
            floorRenderers,
            obstacleRenderers,
            wallColliders,
            blockerColliders,
            obstacleColliders,
            metrics,
            errors);

        HashSet<string> doorEdgeKeys;
        List<BoundaryEdge> boundaryEdges =
            BuildBoundaryEdges(
                template,
                occupiedSet,
                walkableSet,
                out doorEdgeKeys,
                errors);

        ValidateBoundaryGeometry(
            template,
            boundaryEdges,
            doorEdgeKeys,
            wallRenderers,
            blockerRenderers,
            wallColliders,
            blockerColliders,
            metrics,
            errors);

        ValidateSpawnPointTransforms(
            template,
            errors);

        ValidateFourRotations(
            template,
            occupiedSet,
            walkableSet,
            blockedSet,
            gaps,
            boundaryEdges,
            doorEdgeKeys,
            floorRenderers,
            wallColliders,
            blockerColliders,
            obstacleColliders,
            metrics,
            errors);

        return errors;
    }

    private static void ValidateCellVisualsAndClearance(
        DreamRoomTemplate template,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> walkable,
        HashSet<Vector2Int> blocked,
        List<Vector2Int> gaps,
        SpriteRenderer[] floorRenderers,
        SpriteRenderer[] obstacleRenderers,
        BoxCollider2D[] wallColliders,
        BoxCollider2D[] blockerColliders,
        BoxCollider2D[] obstacleColliders,
        AuditMetrics metrics,
        List<string> errors)
    {
        List<BoxCollider2D> allSolidColliders =
            CombineBlockingColliders(
                wallColliders,
                blockerColliders,
                obstacleColliders);

        foreach (Vector2Int cell in occupied)
        {
            bool fullyCovered =
                AreAllRoomLocalProbesCoveredBySprites(
                    template,
                    cell,
                    VisualProbeOffsets,
                    floorRenderers);

            if (fullyCovered)
            {
                metrics.OccupiedVisualCells++;
            }
            else
            {
                AddError(
                    errors,
                    "OccupiedVisual",
                    "Occupied 格 " + CellLabel(cell) +
                    " 没有被 Floor Visual 完整覆盖。");
            }
        }

        for (int i = 0; i < gaps.Count; i++)
        {
            Vector2Int gap = gaps[i];

            bool hasFloor =
                IsAnyRoomLocalProbeCoveredBySprites(
                    template,
                    gap,
                    VisualProbeOffsets,
                    floorRenderers);

            if (hasFloor)
            {
                metrics.GapVisualIntrusions++;

                AddError(
                    errors,
                    "GapVisual",
                    "非 Occupied 缺角格 " +
                    CellLabel(gap) +
                    " 被 Floor Visual 侵入。");
            }

            bool hasBlockingCollider =
                IsAnyRoomLocalProbeCoveredByBoxes(
                    template,
                    gap,
                    ClearanceProbeOffsets,
                    allSolidColliders);

            if (hasBlockingCollider)
            {
                metrics.GapColliderIntrusions++;

                AddError(
                    errors,
                    "GapCollider",
                    "非 Occupied 缺角格 " +
                    CellLabel(gap) +
                    " 的内部被实体 Collider 侵入。");
            }
        }

        foreach (Vector2Int cell in walkable)
        {
            bool obstructed =
                IsAnyRoomLocalProbeCoveredByBoxes(
                    template,
                    cell,
                    ClearanceProbeOffsets,
                    allSolidColliders);

            if (obstructed)
            {
                AddError(
                    errors,
                    "WalkableClearance",
                    "Walkable 格 " + CellLabel(cell) +
                    " 的安全内区被实体 Collider 阻挡。");
            }
            else
            {
                metrics.WalkableClearCells++;
            }
        }

        foreach (Vector2Int cell in blocked)
        {
            bool obstacleCoversCell =
                AreAllRoomLocalProbesCoveredByBoxes(
                    template,
                    cell,
                    ClearanceProbeOffsets,
                    obstacleColliders);

            if (!obstacleCoversCell)
            {
                AddError(
                    errors,
                    "BlockedCollider",
                    "Blocked 格 " + CellLabel(cell) +
                    " 没有被 Obstacles 实体 Collider 覆盖。");
            }
            else
            {
                metrics.BlockedColliderCells++;
            }

            bool obstacleVisual =
                AreAllRoomLocalProbesCoveredBySprites(
                    template,
                    cell,
                    ClearanceProbeOffsets,
                    obstacleRenderers);

            if (!obstacleVisual)
            {
                AddError(
                    errors,
                    "BlockedVisual",
                    "Blocked 格 " + CellLabel(cell) +
                    " 没有对应的 Obstacles Visual。");
            }
            else
            {
                metrics.BlockedVisualCells++;
            }
        }
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(
        DreamRoomTemplate template,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> walkable,
        out HashSet<string> doorEdgeKeys,
        List<string> errors)
    {
        List<BoundaryEdge> boundaryEdges =
            new List<BoundaryEdge>();

        HashSet<string> boundaryKeys =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (Vector2Int cell in occupied)
        {
            for (int directionIndex = 0;
                 directionIndex < 4;
                 directionIndex++)
            {
                DreamRoomDoorDirection direction =
                    (DreamRoomDoorDirection)directionIndex;

                if (occupied.Contains(
                        cell + direction.ToCellOffset()))
                {
                    continue;
                }

                BoundaryEdge edge =
                    new BoundaryEdge(cell, direction);

                boundaryEdges.Add(edge);
                boundaryKeys.Add(edge.Key);
            }
        }

        doorEdgeKeys =
            new HashSet<string>(StringComparer.Ordinal);

        if (template.DoorSockets.Count != ExpectedSockets)
        {
            AddError(
                errors,
                "SocketCount",
                "Door Socket 应为 4，实际为 " +
                template.DoorSockets.Count + "。");
        }

        for (int socketIndex = 0;
             socketIndex < template.DoorSockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[socketIndex];

            if (socket == null)
            {
                AddError(
                    errors,
                    "Socket",
                    "Door Sockets 含空引用。");
                continue;
            }

            List<Vector2Int> insideCells =
                socket.GetLocalInsideCells();

            for (int cellIndex = 0;
                 cellIndex < insideCells.Count;
                 cellIndex++)
            {
                Vector2Int inside = insideCells[cellIndex];
                BoundaryEdge edge =
                    new BoundaryEdge(
                        inside,
                        socket.Direction);

                if (!walkable.Contains(inside))
                {
                    AddError(
                        errors,
                        "SocketCell",
                        "Socket '" + socket.SocketId +
                        "' 的门内格 " + CellLabel(inside) +
                        " 不是 Walkable。");
                }

                if (!boundaryKeys.Contains(edge.Key))
                {
                    AddError(
                        errors,
                        "SocketBoundary",
                        "Socket '" + socket.SocketId +
                        "' 的门边不在 Occupied 轮廓上：" +
                        edge.Key + "。");
                }

                if (!doorEdgeKeys.Add(edge.Key))
                {
                    AddError(
                        errors,
                        "SocketDuplicate",
                        "多个 Socket 重复占用门边 " +
                        edge.Key + "。");
                }
            }
        }

        return boundaryEdges;
    }

    private static void ValidateBoundaryGeometry(
        DreamRoomTemplate template,
        List<BoundaryEdge> boundaryEdges,
        HashSet<string> doorEdgeKeys,
        SpriteRenderer[] wallRenderers,
        SpriteRenderer[] blockerRenderers,
        BoxCollider2D[] wallColliders,
        BoxCollider2D[] blockerColliders,
        AuditMetrics metrics,
        List<string> errors)
    {
        metrics.BoundaryEdges = boundaryEdges.Count;
        metrics.DoorEdges = doorEdgeKeys.Count;
        metrics.PermanentWallEdges =
            boundaryEdges.Count - doorEdgeKeys.Count;

        if (metrics.BoundaryEdges != ExpectedBoundaryEdges ||
            metrics.DoorEdges != ExpectedDoorEdges ||
            metrics.PermanentWallEdges != ExpectedPermanentWalls)
        {
            AddError(
                errors,
                "BoundaryCounts",
                "Boundary/Door/Permanent 应为 36/8/28，实际为 " +
                metrics.BoundaryEdges + "/" +
                metrics.DoorEdges + "/" +
                metrics.PermanentWallEdges + "。");
        }

        if (wallColliders.Length !=
            metrics.PermanentWallEdges)
        {
            AddError(
                errors,
                "BoundaryWall",
                "Walls 的 BoxCollider2D 应为 " +
                metrics.PermanentWallEdges +
                "，实际为 " + wallColliders.Length + "。");
        }

        if (wallRenderers.Length !=
            metrics.PermanentWallEdges)
        {
            AddError(
                errors,
                "BoundaryWallVisual",
                "Walls 的 SpriteRenderer 应为 " +
                metrics.PermanentWallEdges +
                "，实际为 " + wallRenderers.Length + "。");
        }

        if (blockerColliders.Length != ExpectedSockets ||
            blockerRenderers.Length != ExpectedSockets)
        {
            AddError(
                errors,
                "DoorBlocker",
                "DoorBlocker 的 Collider/Visual 应为 4/4，实际为 " +
                blockerColliders.Length + "/" +
                blockerRenderers.Length + "。");
        }

        for (int edgeIndex = 0;
             edgeIndex < boundaryEdges.Count;
             edgeIndex++)
        {
            BoundaryEdge edge = boundaryEdges[edgeIndex];
            Vector3 edgePoint =
                GetLocalEdgeMidpoint(template, edge);

            int wallColliderHits =
                CountBoxesAtRoomLocalPoint(
                    wallColliders,
                    template.transform,
                    edgePoint);

            int blockerColliderHits =
                CountBoxesAtRoomLocalPoint(
                    blockerColliders,
                    template.transform,
                    edgePoint);

            int wallVisualHits =
                CountSpritesAtRoomLocalPoint(
                    wallRenderers,
                    template.transform,
                    edgePoint);

            int blockerVisualHits =
                CountSpritesAtRoomLocalPoint(
                    blockerRenderers,
                    template.transform,
                    edgePoint);

            bool isDoorEdge =
                doorEdgeKeys.Contains(edge.Key);

            if (isDoorEdge)
            {
                if (wallColliderHits != 0 ||
                    wallVisualHits != 0)
                {
                    AddError(
                        errors,
                        "DoorOpening",
                        "门洞边 " + edge.Key +
                        " 被永久轮廓墙占用。");
                }

                if (blockerColliderHits != 1 ||
                    blockerVisualHits != 1)
                {
                    AddError(
                        errors,
                        "DoorBlocker",
                        "门洞边 " + edge.Key +
                        " 应恰好由 1 个门封块覆盖，实际 Collider/Visual=" +
                        blockerColliderHits + "/" +
                        blockerVisualHits + "。");
                }
            }
            else
            {
                if (wallColliderHits != 1 ||
                    wallVisualHits != 1)
                {
                    AddError(
                        errors,
                        "BoundaryWall",
                        "永久边界 " + edge.Key +
                        " 应恰好由 1 段墙覆盖，实际 Collider/Visual=" +
                        wallColliderHits + "/" +
                        wallVisualHits + "。");
                }

                if (blockerColliderHits != 0 ||
                    blockerVisualHits != 0)
                {
                    AddError(
                        errors,
                        "DoorBlocker",
                        "门封块错误侵入永久边界 " +
                        edge.Key + "。");
                }
            }
        }

        for (int colliderIndex = 0;
             colliderIndex < wallColliders.Length;
             colliderIndex++)
        {
            int coveredPermanentEdges = 0;

            for (int edgeIndex = 0;
                 edgeIndex < boundaryEdges.Count;
                 edgeIndex++)
            {
                BoundaryEdge edge = boundaryEdges[edgeIndex];

                if (doorEdgeKeys.Contains(edge.Key))
                {
                    continue;
                }

                if (BoxContainsRoomLocalPoint(
                        wallColliders[colliderIndex],
                        template.transform,
                        GetLocalEdgeMidpoint(template, edge)))
                {
                    coveredPermanentEdges++;
                }
            }

            if (coveredPermanentEdges != 1)
            {
                AddError(
                    errors,
                    "BoundaryWall",
                    "墙 Collider '" +
                    wallColliders[colliderIndex].name +
                    "' 应对应恰好 1 条永久边界，实际为 " +
                    coveredPermanentEdges + "。");
            }
        }

        for (int colliderIndex = 0;
             colliderIndex < blockerColliders.Length;
             colliderIndex++)
        {
            int coveredDoorEdges = 0;

            for (int edgeIndex = 0;
                 edgeIndex < boundaryEdges.Count;
                 edgeIndex++)
            {
                BoundaryEdge edge = boundaryEdges[edgeIndex];

                if (!doorEdgeKeys.Contains(edge.Key))
                {
                    continue;
                }

                if (BoxContainsRoomLocalPoint(
                        blockerColliders[colliderIndex],
                        template.transform,
                        GetLocalEdgeMidpoint(template, edge)))
                {
                    coveredDoorEdges++;
                }
            }

            if (coveredDoorEdges != 2)
            {
                AddError(
                    errors,
                    "DoorBlocker",
                    "门封块 Collider '" +
                    blockerColliders[colliderIndex].name +
                    "' 应覆盖 2 条门边，实际为 " +
                    coveredDoorEdges + "。");
            }
        }

        for (int socketIndex = 0;
             socketIndex < template.DoorSockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[socketIndex];

            if (socket == null)
            {
                continue;
            }

            if (socket.ClosedBlocker == null)
            {
                AddError(
                    errors,
                    "DoorBlocker",
                    "Socket '" + socket.SocketId +
                    "' 没有 Closed Blocker。");
                continue;
            }

            if (!socket.ClosedBlocker.activeSelf)
            {
                AddError(
                    errors,
                    "DoorBlocker",
                    "Socket '" + socket.SocketId +
                    "' 的 Closed Blocker 默认必须启用。");
            }

            if (socket.ClosedBlocker.GetComponent<
                    BoxCollider2D>() == null)
            {
                AddError(
                    errors,
                    "DoorBlocker",
                    "Socket '" + socket.SocketId +
                    "' 的 Closed Blocker 缺少 BoxCollider2D。");
            }

            Vector3 expectedSocketPosition =
                GetExpectedSocketLocalPosition(
                    template,
                    socket);

            Vector3 actualSocketPosition =
                template.transform.InverseTransformPoint(
                    socket.transform.position);

            if (Vector3.Distance(
                    expectedSocketPosition,
                    actualSocketPosition) >
                PositionTolerance)
            {
                AddError(
                    errors,
                    "SocketTransform",
                    "Socket '" + socket.SocketId +
                    "' Transform 与格子数据不一致。Expected=" +
                    VectorLabel(expectedSocketPosition) +
                    " Actual=" +
                    VectorLabel(actualSocketPosition) + "。");
            }
        }
    }

    private static void ValidateFourRotations(
        DreamRoomTemplate template,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> walkable,
        HashSet<Vector2Int> blocked,
        List<Vector2Int> gaps,
        List<BoundaryEdge> boundaryEdges,
        HashSet<string> doorEdgeKeys,
        SpriteRenderer[] floorRenderers,
        BoxCollider2D[] wallColliders,
        BoxCollider2D[] blockerColliders,
        BoxCollider2D[] obstacleColliders,
        AuditMetrics metrics,
        List<string> errors)
    {
        Transform roomRoot = template.transform;
        Vector3 originalPosition = roomRoot.position;
        Quaternion originalRotation = roomRoot.rotation;
        Vector3 originalScale = roomRoot.localScale;

        List<BoxCollider2D> allSolidColliders =
            CombineBlockingColliders(
                wallColliders,
                blockerColliders,
                obstacleColliders);

        try
        {
            for (int turns = 0; turns < 4; turns++)
            {
                int errorsBefore = errors.Count;

                DreamRoomPlacement placement =
                    new DreamRoomPlacement(
                        template,
                        Vector2Int.zero,
                        turns);

                placement.ApplyPose(
                    roomRoot,
                    Vector3.zero,
                    1f);

                roomRoot.localScale = Vector3.one;

                string rotationLabel =
                    (turns * 90) + "°";

                for (int y = 0;
                     y < template.SizeInCells.y;
                     y++)
                {
                    for (int x = 0;
                         x < template.SizeInCells.x;
                         x++)
                    {
                        Vector2Int originalCell =
                            new Vector2Int(x, y);

                        Vector2Int globalCell =
                            placement.OriginalToGlobalCell(
                                originalCell);

                        Vector3 expectedWorld =
                            placement.GetGlobalCellWorldCenter(
                                globalCell,
                                Vector3.zero,
                                1f);

                        Vector3 actualWorld =
                            roomRoot.TransformPoint(
                                template.GetLocalCellCenter(
                                    originalCell));

                        if (Vector3.Distance(
                                expectedWorld,
                                actualWorld) >
                            PositionTolerance)
                        {
                            AddError(
                                errors,
                                "RotationPose",
                                rotationLabel +
                                " 格 " + CellLabel(originalCell) +
                                " 的实体位置与 Placement 不一致。");
                        }

                        bool hasFloor =
                            CountSpritesAtWorldPoint(
                                floorRenderers,
                                expectedWorld) > 0;

                        bool hasCollider =
                            CountBoxesAtWorldPoint(
                                allSolidColliders,
                                expectedWorld) > 0;

                        if (occupied.Contains(originalCell) &&
                            !hasFloor)
                        {
                            AddError(
                                errors,
                                "RotationVisual",
                                rotationLabel +
                                " Occupied 格 " +
                                CellLabel(originalCell) +
                                " 失去 Floor Visual。");
                        }

                        if (!occupied.Contains(originalCell) &&
                            hasFloor)
                        {
                            AddError(
                                errors,
                                "RotationGap",
                                rotationLabel +
                                " 缺角格 " +
                                CellLabel(originalCell) +
                                " 被 Floor Visual 覆盖。");
                        }

                        if (walkable.Contains(originalCell) &&
                            hasCollider)
                        {
                            AddError(
                                errors,
                                "RotationWalkable",
                                rotationLabel +
                                " Walkable 格 " +
                                CellLabel(originalCell) +
                                " 中心被 Collider 阻挡。");
                        }

                        if (blocked.Contains(originalCell) &&
                            CountBoxesAtWorldPoint(
                                obstacleColliders,
                                expectedWorld) != 1)
                        {
                            AddError(
                                errors,
                                "RotationBlocked",
                                rotationLabel +
                                " Blocked 格 " +
                                CellLabel(originalCell) +
                                " 没有恰好 1 个障碍 Collider。");
                        }

                        if (!occupied.Contains(originalCell) &&
                            hasCollider)
                        {
                            AddError(
                                errors,
                                "RotationGapCollider",
                                rotationLabel +
                                " 缺角格 " +
                                CellLabel(originalCell) +
                                " 中心被 Collider 阻挡。");
                        }
                    }
                }

                for (int edgeIndex = 0;
                     edgeIndex < boundaryEdges.Count;
                     edgeIndex++)
                {
                    BoundaryEdge edge =
                        boundaryEdges[edgeIndex];

                    Vector2Int globalInside =
                        placement.OriginalToGlobalCell(
                            edge.Cell);

                    DreamRoomDoorDirection rotatedDirection =
                        edge.Direction.RotateClockwise(turns);

                    Vector2Int directionOffset =
                        rotatedDirection.ToCellOffset();

                    Vector3 expectedWorld =
                        new Vector3(
                            globalInside.x +
                                directionOffset.x * 0.5f,
                            globalInside.y +
                                directionOffset.y * 0.5f,
                            0f);

                    Vector3 physicalWorld =
                        roomRoot.TransformPoint(
                            GetLocalEdgeMidpoint(
                                template,
                                edge));

                    if (Vector3.Distance(
                            expectedWorld,
                            physicalWorld) >
                        PositionTolerance)
                    {
                        AddError(
                            errors,
                            "RotationBoundaryPose",
                            rotationLabel +
                            " 边界 " + edge.Key +
                            " 的实体旋转位置错误。");
                    }

                    if (doorEdgeKeys.Contains(edge.Key))
                    {
                        if (CountBoxesAtWorldPoint(
                                blockerColliders,
                                expectedWorld) != 1 ||
                            CountBoxesAtWorldPoint(
                                wallColliders,
                                expectedWorld) != 0)
                        {
                            AddError(
                                errors,
                                "RotationDoor",
                                rotationLabel +
                                " 门边 " + edge.Key +
                                " 的墙／门封块旋转错误。");
                        }
                    }
                    else if (CountBoxesAtWorldPoint(
                                 wallColliders,
                                 expectedWorld) != 1)
                    {
                        AddError(
                            errors,
                            "RotationBoundary",
                            rotationLabel +
                            " 永久边界 " + edge.Key +
                            " 的墙 Collider 旋转错误。");
                    }
                }

                ValidateRotatedSpawnPoints(
                    template,
                    placement,
                    rotationLabel,
                    errors);

                ValidateRotatedSockets(
                    template,
                    placement,
                    rotationLabel,
                    errors);

                if (errors.Count == errorsBefore)
                {
                    metrics.RotationsPassed++;
                }
            }
        }
        finally
        {
            roomRoot.position = originalPosition;
            roomRoot.rotation = originalRotation;
            roomRoot.localScale = originalScale;
        }
    }

    private static void ValidateRotatedSpawnPoints(
        DreamRoomTemplate template,
        DreamRoomPlacement placement,
        string rotationLabel,
        List<string> errors)
    {
        for (int pointIndex = 0;
             pointIndex < template.SpawnPoints.Count;
             pointIndex++)
        {
            DreamRoomSpawnPoint point =
                template.SpawnPoints[pointIndex];

            if (point == null)
            {
                continue;
            }

            Vector2Int globalCell =
                placement.GetSpawnPointGlobalCell(point);

            Vector3 expectedWorld =
                placement.GetGlobalCellWorldCenter(
                    globalCell,
                    Vector3.zero,
                    1f);

            if (Vector3.Distance(
                    point.transform.position,
                    expectedWorld) > PositionTolerance)
            {
                AddError(
                    errors,
                    "RotationSpawn",
                    rotationLabel + " SpawnPoint '" +
                    point.SpawnPointId +
                    "' 的 Transform 与旋转后 Local Cell 不一致。");
            }
        }
    }

    private static void ValidateRotatedSockets(
        DreamRoomTemplate template,
        DreamRoomPlacement placement,
        string rotationLabel,
        List<string> errors)
    {
        List<Vector2Int> insideCells =
            new List<Vector2Int>();

        for (int socketIndex = 0;
             socketIndex < template.DoorSockets.Count;
             socketIndex++)
        {
            DreamRoomDoorSocket socket =
                template.DoorSockets[socketIndex];

            if (socket == null)
            {
                continue;
            }

            placement.GetSocketInsideCells(
                socket,
                insideCells);

            if (insideCells.Count == 0)
            {
                continue;
            }

            Vector3 expectedWorld = Vector3.zero;

            for (int cellIndex = 0;
                 cellIndex < insideCells.Count;
                 cellIndex++)
            {
                expectedWorld +=
                    placement.GetGlobalCellWorldCenter(
                        insideCells[cellIndex],
                        Vector3.zero,
                        1f);
            }

            expectedWorld /= insideCells.Count;

            Vector2Int offset =
                placement.GetRotatedDirection(socket)
                    .ToCellOffset();

            expectedWorld +=
                new Vector3(
                    offset.x * 0.5f,
                    offset.y * 0.5f,
                    0f);

            if (Vector3.Distance(
                    socket.transform.position,
                    expectedWorld) > PositionTolerance)
            {
                AddError(
                    errors,
                    "RotationSocket",
                    rotationLabel + " Socket '" +
                    socket.SocketId +
                    "' 的 Transform 与旋转后门边不一致。");
            }
        }
    }

    private static void ValidateSpawnPointTransforms(
        DreamRoomTemplate template,
        List<string> errors)
    {
        if (template.SpawnPoints.Count != ExpectedSpawnPoints)
        {
            AddError(
                errors,
                "SpawnCount",
                "Spawn Points 应为 4，实际为 " +
                template.SpawnPoints.Count + "。");
        }

        for (int pointIndex = 0;
             pointIndex < template.SpawnPoints.Count;
             pointIndex++)
        {
            DreamRoomSpawnPoint point =
                template.SpawnPoints[pointIndex];

            if (point == null)
            {
                AddError(
                    errors,
                    "SpawnPoint",
                    "Spawn Points 含空引用。");
                continue;
            }

            Vector3 expected =
                template.GetLocalCellCenter(
                    point.LocalCell);

            Vector3 actual =
                template.transform.InverseTransformPoint(
                    point.transform.position);

            if (Vector3.Distance(expected, actual) >
                PositionTolerance)
            {
                AddError(
                    errors,
                    "SpawnTransform",
                    "SpawnPoint '" + point.SpawnPointId +
                    "' Transform 与 Local Cell 不一致。Expected=" +
                    VectorLabel(expected) +
                    " Actual=" + VectorLabel(actual) + "。");
            }
        }
    }

    private static void ValidateColliderOwnership(
        GameObject loadedRoot,
        Transform visualRoot,
        Transform floorsRoot,
        Transform wallsRoot,
        Transform blockersRoot,
        Transform obstaclesRoot,
        List<string> errors)
    {
        Collider2D[] allColliders =
            loadedRoot.GetComponentsInChildren<
                Collider2D>(true);

        for (int colliderIndex = 0;
             colliderIndex < allColliders.Length;
             colliderIndex++)
        {
            Collider2D collider =
                allColliders[colliderIndex];

            if (!IsDescendantOrSelf(
                    collider.transform,
                    visualRoot))
            {
                AddError(
                    errors,
                    "ColliderHierarchy",
                    "Collider '" + collider.name +
                    "' 必须位于 Visual 下。");
            }

            if (!(collider is BoxCollider2D))
            {
                AddError(
                    errors,
                    "ColliderType",
                    "R9.1 样本只允许 BoxCollider2D，发现 " +
                    collider.GetType().Name + "：" +
                    collider.name + "。");
            }

            if (collider.isTrigger)
            {
                AddError(
                    errors,
                    "ColliderTrigger",
                    "房间实体 Collider 不得为 Trigger：" +
                    collider.name + "。");
            }

            bool validOwner =
                IsDescendantOrSelf(
                    collider.transform,
                    wallsRoot) ||
                IsDescendantOrSelf(
                    collider.transform,
                    blockersRoot) ||
                IsDescendantOrSelf(
                    collider.transform,
                    obstaclesRoot);

            if (!validOwner ||
                IsDescendantOrSelf(
                    collider.transform,
                    floorsRoot))
            {
                AddError(
                    errors,
                    "ColliderOwnership",
                    "Collider '" + collider.name +
                    "' 必须只属于 Walls、DoorBlockers 或 Obstacles，" +
                    "不能属于 Floors。");
            }
        }
    }

    private static void ValidateRendererKinds(
        Transform visualRoot,
        SpriteRenderer[] floorRenderers,
        SpriteRenderer[] wallRenderers,
        SpriteRenderer[] blockerRenderers,
        SpriteRenderer[] obstacleRenderers,
        List<string> errors)
    {
        SpriteRenderer[] allRenderers =
            visualRoot.GetComponentsInChildren<
                SpriteRenderer>(true);

        int classifiedCount =
            floorRenderers.Length +
            wallRenderers.Length +
            blockerRenderers.Length +
            obstacleRenderers.Length;

        if (allRenderers.Length != classifiedCount)
        {
            AddError(
                errors,
                "VisualHierarchy",
                "Visual 下存在未归类到 Floors/Walls/" +
                "DoorBlockers/Obstacles 的 SpriteRenderer。");
        }

        if (floorRenderers.Length != 2)
        {
            AddError(
                errors,
                "FloorVisualCount",
                "R9.1 L 形样本应有 2 个 Floor Renderer，实际为 " +
                floorRenderers.Length + "。");
        }

        for (int rendererIndex = 0;
             rendererIndex < allRenderers.Length;
             rendererIndex++)
        {
            SpriteRenderer renderer =
                allRenderers[rendererIndex];

            if (renderer.sprite == null)
            {
                AddError(
                    errors,
                    "MissingSprite",
                    "SpriteRenderer '" + renderer.name +
                    "' 没有 Sprite。");
            }

            if (!renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                AddError(
                    errors,
                    "DisabledVisual",
                    "SpriteRenderer '" + renderer.name +
                    "' 必须默认启用。");
            }

            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                AddError(
                    errors,
                    "SpriteDrawMode",
                    "R9.1 样本只允许 Simple SpriteRenderer：" +
                    renderer.name + "。");
            }
        }
    }

    private static bool TryValidateCleanGrayboxBaseline(
        out BaselineInfo baseline,
        out List<string> errors)
    {
        baseline = new BaselineInfo();
        errors = new List<string>();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("必须先退出 Play Mode。");
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            errors.Add("必须先退出 Prefab Mode。");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("当前没有有效且已加载的 Scene。");
            return false;
        }

        if (!string.Equals(
                scene.path,
                GameScenePath,
                StringComparison.Ordinal))
        {
            errors.Add(
                "必须先打开 GameScene：" +
                GameScenePath + "。当前=" + scene.path);
        }

        if (scene.isDirty)
        {
            errors.Add(
                "GameScene 当前有未保存修改（标题带 *）。" +
                "R9.3 必须从已保存的 Graybox 基线开始。");
        }

        DungeonGenerator generator =
            FindSceneComponent<DungeonGenerator>(scene);

        DungeonRenderer renderer =
            FindSceneComponent<DungeonRenderer>(scene);

        if (generator == null)
        {
            errors.Add("GameScene 中找不到 DungeonGenerator。");
        }

        if (renderer == null)
        {
            errors.Add("GameScene 中找不到 DungeonRenderer。");
        }

        if (generator != null)
        {
            DreamRoomCatalog catalog =
                generator.TemplateFirstRoomCatalog;

            baseline.CatalogId =
                catalog == null
                    ? "<null>"
                    : catalog.CatalogId;

            if (catalog == null ||
                !string.Equals(
                    catalog.CatalogId,
                    ExpectedGrayboxCatalogId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "R4 Catalog 必须是 Graybox_R3，实际为 " +
                    baseline.CatalogId + "。");
            }

            baseline.FixedSeed =
                ReadSerializedInt(
                    generator,
                    "fixedSeed",
                    int.MinValue);

            bool useRandomSeed =
                ReadSerializedBool(
                    generator,
                    "useRandomSeed",
                    defaultValue: true);

            if (useRandomSeed || baseline.FixedSeed != 12345)
            {
                errors.Add(
                    "DungeonGenerator 必须为 Fixed Seed 12345。" +
                    " UseRandomSeed=" + useRandomSeed +
                    " FixedSeed=" + baseline.FixedSeed + "。");
            }
        }

        if (renderer != null)
        {
            baseline.RenderMode = renderer.RenderMode;

            if (renderer.RenderMode !=
                DungeonRenderMode.HybridPrefabRooms)
            {
                errors.Add(
                    "Render Mode 必须是 Hybrid Prefab Rooms，实际为 " +
                    renderer.RenderMode + "。");
            }
        }

        baseline.SceneSaved = !scene.isDirty;
        return errors.Count == 0;
    }

    private static string BuildSuccessReport(
        AuditMetrics metrics,
        BaselineInfo baseline)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomGeometryContractAuditR93] " +
            "R9.3 数据／Visual／Collider 几何契约通过");

        report.AppendLine(
            "Prefab=Room_L10x08_R91" +
            " | Template=" + ExpectedTemplateId);

        report.AppendLine(
            "Data: Bounds=" + metrics.BoundsCells +
            " | Occupied=" + metrics.OccupiedCells +
            " | Walkable=" + metrics.WalkableCells +
            " | Blocked=" + metrics.BlockedCells +
            " | Gaps=" + metrics.GapCells);

        report.AppendLine(
            "Visual: OccupiedCovered=" +
            metrics.OccupiedVisualCells + "/" +
            metrics.OccupiedCells +
            " | GapIntrusions=" +
            metrics.GapVisualIntrusions +
            " | BlockedVisual=" +
            metrics.BlockedVisualCells + "/" +
            metrics.BlockedCells);

        report.AppendLine(
            "Collider: WalkableClear=" +
            metrics.WalkableClearCells + "/" +
            metrics.WalkableCells +
            " | BlockedSolid=" +
            metrics.BlockedColliderCells + "/" +
            metrics.BlockedCells +
            " | GapIntrusions=" +
            metrics.GapColliderIntrusions);

        report.AppendLine(
            "Boundary: Edges=" + metrics.BoundaryEdges +
            " | PermanentWalls=" +
            metrics.PermanentWallEdges +
            " | DoorEdges=" + metrics.DoorEdges +
            " | WallColliders=" + metrics.WallColliders +
            " | DoorBlockers=" +
            metrics.DoorBlockerColliders);

        report.AppendLine(
            "Rotations=" + metrics.RotationsPassed + "/4" +
            " | SpawnPoints=" + ExpectedSpawnPoints +
            " | Sockets=" + ExpectedSockets);

        report.Append(
            "Baseline: Catalog=" + baseline.CatalogId +
            " | SceneSaved=" + baseline.SceneSaved +
            " | RenderMode=" + baseline.RenderMode +
            " | FixedSeed=" + baseline.FixedSeed +
            " | AssetsModified=False");

        return report.ToString();
    }

    private static void ReportFailure(
        string heading,
        List<string> errors,
        UnityEngine.Object context)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "[DreamRoomGeometryContractAuditR93] " +
            heading);

        int maximum = Mathf.Min(errors.Count, 40);

        for (int i = 0; i < maximum; i++)
        {
            report.AppendLine(
                (i + 1) + ". " + errors[i]);
        }

        if (errors.Count > maximum)
        {
            report.AppendLine(
                "...另有 " +
                (errors.Count - maximum) +
                " 项错误未展开。");
        }

        Debug.LogError(report.ToString(), context);

        EditorUtility.DisplayDialog(
            heading,
            "校验未通过。请保留 Console 第一条红错，" +
            "不要手工修改 R9.1 Prefab。",
            "OK");
    }

    private static void ValidateExpectedRoot(
        Transform roomRoot,
        Transform referencedRoot,
        string expectedName,
        List<string> errors)
    {
        if (referencedRoot == null)
        {
            AddError(
                errors,
                "Hierarchy",
                expectedName + " Root 引用为空。");
            return;
        }

        if (referencedRoot.parent != roomRoot ||
            referencedRoot.name != expectedName)
        {
            AddError(
                errors,
                "Hierarchy",
                expectedName +
                " 必须是房间根节点的同名直接子物件。");
        }

        if (referencedRoot.localPosition != Vector3.zero ||
            referencedRoot.localRotation !=
                Quaternion.identity ||
            referencedRoot.localScale != Vector3.one)
        {
            AddError(
                errors,
                "HierarchyTransform",
                expectedName +
                " Root Transform 必须归零且 Scale=1。");
        }
    }

    private static void RequireVisualChild(
        Transform target,
        string expectedName,
        List<string> errors)
    {
        if (target == null)
        {
            AddError(
                errors,
                "VisualHierarchy",
                "Visual 下缺少直接子物件 " +
                expectedName + "。");
        }
    }

    private static List<Vector2Int> BuildGapCells(
        DreamRoomTemplate template,
        HashSet<Vector2Int> occupied)
    {
        List<Vector2Int> gaps =
            new List<Vector2Int>();

        for (int y = 0; y < template.SizeInCells.y; y++)
        {
            for (int x = 0; x < template.SizeInCells.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (!occupied.Contains(cell))
                {
                    gaps.Add(cell);
                }
            }
        }

        return gaps;
    }

    private static bool AreAllRoomLocalProbesCoveredBySprites(
        DreamRoomTemplate template,
        Vector2Int cell,
        Vector2[] offsets,
        SpriteRenderer[] renderers)
    {
        Vector3 center = template.GetLocalCellCenter(cell);

        for (int offsetIndex = 0;
             offsetIndex < offsets.Length;
             offsetIndex++)
        {
            Vector3 point =
                center + new Vector3(
                    offsets[offsetIndex].x,
                    offsets[offsetIndex].y,
                    0f);

            if (CountSpritesAtRoomLocalPoint(
                    renderers,
                    template.transform,
                    point) == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAnyRoomLocalProbeCoveredBySprites(
        DreamRoomTemplate template,
        Vector2Int cell,
        Vector2[] offsets,
        SpriteRenderer[] renderers)
    {
        Vector3 center = template.GetLocalCellCenter(cell);

        for (int offsetIndex = 0;
             offsetIndex < offsets.Length;
             offsetIndex++)
        {
            Vector3 point =
                center + new Vector3(
                    offsets[offsetIndex].x,
                    offsets[offsetIndex].y,
                    0f);

            if (CountSpritesAtRoomLocalPoint(
                    renderers,
                    template.transform,
                    point) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreAllRoomLocalProbesCoveredByBoxes(
        DreamRoomTemplate template,
        Vector2Int cell,
        Vector2[] offsets,
        IList<BoxCollider2D> colliders)
    {
        Vector3 center = template.GetLocalCellCenter(cell);

        for (int offsetIndex = 0;
             offsetIndex < offsets.Length;
             offsetIndex++)
        {
            Vector3 point =
                center + new Vector3(
                    offsets[offsetIndex].x,
                    offsets[offsetIndex].y,
                    0f);

            if (CountBoxesAtRoomLocalPoint(
                    colliders,
                    template.transform,
                    point) == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAnyRoomLocalProbeCoveredByBoxes(
        DreamRoomTemplate template,
        Vector2Int cell,
        Vector2[] offsets,
        IList<BoxCollider2D> colliders)
    {
        Vector3 center = template.GetLocalCellCenter(cell);

        for (int offsetIndex = 0;
             offsetIndex < offsets.Length;
             offsetIndex++)
        {
            Vector3 point =
                center + new Vector3(
                    offsets[offsetIndex].x,
                    offsets[offsetIndex].y,
                    0f);

            if (CountBoxesAtRoomLocalPoint(
                    colliders,
                    template.transform,
                    point) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSpritesAtRoomLocalPoint(
        SpriteRenderer[] renderers,
        Transform roomRoot,
        Vector3 roomLocalPoint)
    {
        Vector3 worldPoint =
            roomRoot.TransformPoint(roomLocalPoint);

        return CountSpritesAtWorldPoint(
            renderers,
            worldPoint);
    }

    private static int CountSpritesAtWorldPoint(
        SpriteRenderer[] renderers,
        Vector3 worldPoint)
    {
        int count = 0;

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            if (SpriteContainsWorldPoint(
                    renderers[rendererIndex],
                    worldPoint))
            {
                count++;
            }
        }

        return count;
    }

    private static bool SpriteContainsWorldPoint(
        SpriteRenderer renderer,
        Vector3 worldPoint)
    {
        if (renderer == null ||
            renderer.sprite == null ||
            !renderer.enabled ||
            !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 localPoint =
            renderer.transform.InverseTransformPoint(
                worldPoint);

        Bounds spriteBounds = renderer.sprite.bounds;

        return localPoint.x >=
                   spriteBounds.min.x - PointTolerance &&
               localPoint.x <=
                   spriteBounds.max.x + PointTolerance &&
               localPoint.y >=
                   spriteBounds.min.y - PointTolerance &&
               localPoint.y <=
                   spriteBounds.max.y + PointTolerance;
    }

    private static int CountBoxesAtRoomLocalPoint(
        IList<BoxCollider2D> colliders,
        Transform roomRoot,
        Vector3 roomLocalPoint)
    {
        Vector3 worldPoint =
            roomRoot.TransformPoint(roomLocalPoint);

        return CountBoxesAtWorldPoint(
            colliders,
            worldPoint);
    }

    private static int CountBoxesAtWorldPoint(
        IList<BoxCollider2D> colliders,
        Vector3 worldPoint)
    {
        int count = 0;

        for (int colliderIndex = 0;
             colliderIndex < colliders.Count;
             colliderIndex++)
        {
            if (BoxContainsWorldPoint(
                    colliders[colliderIndex],
                    worldPoint))
            {
                count++;
            }
        }

        return count;
    }

    private static bool BoxContainsRoomLocalPoint(
        BoxCollider2D collider,
        Transform roomRoot,
        Vector3 roomLocalPoint)
    {
        return BoxContainsWorldPoint(
            collider,
            roomRoot.TransformPoint(roomLocalPoint));
    }

    private static bool BoxContainsWorldPoint(
        BoxCollider2D collider,
        Vector3 worldPoint)
    {
        if (!IsBlockingCollider(collider))
        {
            return false;
        }

        Vector3 localPoint =
            collider.transform.InverseTransformPoint(
                worldPoint);

        Vector2 delta =
            new Vector2(localPoint.x, localPoint.y) -
            collider.offset;

        Vector2 halfSize = collider.size * 0.5f;

        return Mathf.Abs(delta.x) <=
                   halfSize.x + PointTolerance &&
               Mathf.Abs(delta.y) <=
                   halfSize.y + PointTolerance;
    }

    private static bool IsBlockingCollider(
        BoxCollider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy &&
               !collider.isTrigger;
    }

    private static List<BoxCollider2D> CombineBlockingColliders(
        params BoxCollider2D[][] arrays)
    {
        List<BoxCollider2D> results =
            new List<BoxCollider2D>();

        for (int arrayIndex = 0;
             arrayIndex < arrays.Length;
             arrayIndex++)
        {
            BoxCollider2D[] array = arrays[arrayIndex];

            for (int itemIndex = 0;
                 itemIndex < array.Length;
                 itemIndex++)
            {
                if (IsBlockingCollider(array[itemIndex]))
                {
                    results.Add(array[itemIndex]);
                }
            }
        }

        return results;
    }

    private static Vector3 GetLocalEdgeMidpoint(
        DreamRoomTemplate template,
        BoundaryEdge edge)
    {
        Vector2Int offset =
            edge.Direction.ToCellOffset();

        return template.GetLocalCellCenter(edge.Cell) +
               new Vector3(
                   offset.x * 0.5f,
                   offset.y * 0.5f,
                   0f);
    }

    private static Vector3 GetExpectedSocketLocalPosition(
        DreamRoomTemplate template,
        DreamRoomDoorSocket socket)
    {
        List<Vector2Int> cells =
            socket.GetLocalInsideCells();

        Vector3 result = Vector3.zero;

        for (int i = 0; i < cells.Count; i++)
        {
            result += template.GetLocalCellCenter(cells[i]);
        }

        if (cells.Count > 0)
        {
            result /= cells.Count;
        }

        Vector2Int offset =
            socket.Direction.ToCellOffset();

        return result +
               new Vector3(
                   offset.x * 0.5f,
                   offset.y * 0.5f,
                   0f);
    }

    private static Transform FindDirectChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int childIndex = 0;
             childIndex < parent.childCount;
             childIndex++)
        {
            Transform child = parent.GetChild(childIndex);

            if (string.Equals(
                    child.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static bool IsDescendantOrSelf(
        Transform candidate,
        Transform expectedRoot)
    {
        if (candidate == null || expectedRoot == null)
        {
            return false;
        }

        return candidate == expectedRoot ||
               candidate.IsChildOf(expectedRoot);
    }

    private static T FindSceneComponent<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            T component =
                roots[rootIndex]
                    .GetComponentInChildren<T>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static int ReadSerializedInt(
        UnityEngine.Object target,
        string propertyName,
        int defaultValue)
    {
        SerializedObject serialized =
            new SerializedObject(target);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        return property == null
            ? defaultValue
            : property.intValue;
    }

    private static bool ReadSerializedBool(
        UnityEngine.Object target,
        string propertyName,
        bool defaultValue)
    {
        SerializedObject serialized =
            new SerializedObject(target);

        SerializedProperty property =
            serialized.FindProperty(propertyName);

        return property == null
            ? defaultValue
            : property.boolValue;
    }

    private static void AddError(
        List<string> errors,
        string code,
        string message)
    {
        string formatted =
            "[" + code + "] " + message;

        if (!errors.Contains(formatted))
        {
            errors.Add(formatted);
        }
    }

    private static bool ContainsErrorCode(
        List<string> errors,
        string code)
    {
        string marker = "[" + code + "]";

        for (int i = 0; i < errors.Count; i++)
        {
            if (errors[i].StartsWith(
                    marker,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendErrors(
        List<string> destination,
        List<string> source,
        int maximumCount)
    {
        int count = Mathf.Min(
            source.Count,
            Mathf.Max(0, maximumCount));

        for (int i = 0; i < count; i++)
        {
            destination.Add(source[i]);
        }
    }

    private static string CellLabel(Vector2Int cell)
    {
        return "(" + cell.x + "," + cell.y + ")";
    }

    private static string VectorLabel(Vector3 value)
    {
        return "(" +
               value.x.ToString("0.###") + "," +
               value.y.ToString("0.###") + "," +
               value.z.ToString("0.###") + ")";
    }

    private struct BoundaryEdge
    {
        public readonly Vector2Int Cell;
        public readonly DreamRoomDoorDirection Direction;

        public string Key =>
            Cell.x + ":" + Cell.y + ":" +
            (int)Direction;

        public BoundaryEdge(
            Vector2Int cell,
            DreamRoomDoorDirection direction)
        {
            Cell = cell;
            Direction = direction;
        }
    }

    private struct BaselineInfo
    {
        public string CatalogId;
        public bool SceneSaved;
        public DungeonRenderMode RenderMode;
        public int FixedSeed;
    }

    private sealed class AuditMetrics
    {
        public int BoundsCells;
        public int OccupiedCells;
        public int WalkableCells;
        public int BlockedCells;
        public int GapCells;
        public int FloorRenderers;
        public int OccupiedVisualCells;
        public int GapVisualIntrusions;
        public int BlockedVisualCells;
        public int WalkableClearCells;
        public int BlockedColliderCells;
        public int GapColliderIntrusions;
        public int BoundaryEdges;
        public int DoorEdges;
        public int PermanentWallEdges;
        public int WallColliders;
        public int DoorBlockerColliders;
        public int ObstacleColliders;
        public int RotationsPassed;
    }

    private sealed class RuntimeMetrics
    {
        public int Rooms;
        public int RoomPosesMatched;
        public int Connections;
        public int OccupiedCells;
        public int WalkableCells;
        public int BlockedCells;
        public int GapCentersClear;
        public int WalkableCentersClear;
        public int BlockedCentersSolid;
        public int BlockedPathExcluded;
        public int PlayerObstaclePairsEnabled;
        public int OpenSockets;
        public int ClosedSockets;
        public int CorridorInOccupied;
        public int CorridorFloorExpected;
        public int CorridorFloorActual;
        public int CorridorFloorMismatches;
        public int CorridorWallExpected;
        public int CorridorWallActual;
        public int CorridorWallMismatches;
    }
}
