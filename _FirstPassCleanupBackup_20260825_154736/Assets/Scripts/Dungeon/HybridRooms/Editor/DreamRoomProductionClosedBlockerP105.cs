using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P10.5：Crossroad_01 ClosedBlocker 正式化。
///
/// 目标：
/// 1. 移除 P10.0 阶段遗留的红色 debug SpriteRenderer。
/// 2. 保留 DreamRoomDoorSocket.closedBlocker 的同一个 GameObject 引用。
/// 3. ClosedBlocker 根节点统一为 Scale=1，BoxCollider2D 用真实 Size 表达 2 Cell 门宽。
/// 4. 未连接 Socket 时仍由 ClosedBlocker 负责物理封口；连接成立时 SetOpen(true) 继续整物件关闭。
/// 5. 暂不伪造正式门体美术。以后需要门帘/障碍物时可直接给 ClosedBlocker 添加正式 SpriteRenderer。
///
/// 本阶段不修改：Floor/Objects/Effects PNG、BlockedCells、P10.2.5 Geometry、Catalog、GameScene、
/// DungeonGenerator、DungeonRenderer、A*、Enemy AI、Corridor 算法。
/// </summary>
public static class DreamRoomProductionClosedBlockerP105
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/Production Rooms/P10.5/";

    private const string CrossroadPrefabPath =
        "Assets/DreamDungeon/Production/Rooms/Crossroad_01/Room_Crossroad_01.prefab";

    private const float HalfRoom = 8f;
    private const float DoorWidth = 2f;
    private const float BlockerThickness = 0.35f;

    [MenuItem(MenuRoot + "1. Apply Production Closed Blocker Contract", false, 2750)]
    private static void ApplyProductionClosedBlockerContract()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            FailDialog("请先退出 Play Mode。");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            FailDialog("请先退出 Prefab Mode。");
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(CrossroadPrefabPath);

            if (root == null)
            {
                throw new InvalidOperationException(
                    "找不到 Crossroad_01 Prefab：\n" + CrossroadPrefabPath);
            }

            DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
            if (template == null)
            {
                throw new InvalidOperationException(
                    "Crossroad_01 根节点缺少 DreamRoomTemplate。");
            }

            Transform blockersRoot =
                root.transform.Find("Visual/Objects/ClosedBlockers");

            if (blockersRoot == null)
            {
                throw new InvalidOperationException(
                    "找不到 Visual/Objects/ClosedBlockers。请先完成 P10.0 / P10.4。");
            }

            template.RefreshDoorSockets();

            DreamRoomDoorSocket[] sockets =
                root.GetComponentsInChildren<DreamRoomDoorSocket>(true);

            if (sockets.Length != 4)
            {
                throw new InvalidOperationException(
                    "Crossroad_01 应有 4 个 DoorSocket，实际=" + sockets.Length + "。");
            }

            for (int i = 0; i < sockets.Length; i++)
            {
                DreamRoomDoorSocket socket = sockets[i];
                GameObject blocker = socket.ClosedBlocker;

                if (blocker == null)
                {
                    throw new InvalidOperationException(
                        socket.SocketId + " 缺少 ClosedBlocker。");
                }

                if (blocker.transform.parent != blockersRoot)
                {
                    blocker.transform.SetParent(blockersRoot, false);
                }

                ConfigureBlockerTransformAndCollider(
                    blocker,
                    socket.Direction);

                // P10.0 的红色方块只是 debug 视觉。
                // 正式化后不再让 ClosedBlocker 自带占位 Sprite。
                SpriteRenderer renderer = blocker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    UnityEngine.Object.DestroyImmediate(renderer);
                }

                blocker.SetActive(true);
                EditorUtility.SetDirty(blocker);
                EditorUtility.SetDirty(socket);
            }

            List<string> errors = ValidateLoadedPrefab(root);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "P10.5 保存前校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            PrefabUtility.SaveAsPrefabAsset(root, CrossroadPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[P10.5] Crossroad_01 ClosedBlocker 正式化完成。\n" +
                "ClosedBlockers=4 | DebugSpriteRenderers=Removed\n" +
                "RootScale=Identity | ColliderSize=2x0.35 / 0.35x2\n" +
                "SocketReferences=Preserved | DefaultClosed=True\n" +
                "OpenBehavior=DreamRoomDoorSocket.SetOpen unchanged\n" +
                "FormalDoorArt=NoneByDesign\n" +
                "P10.4ArtLayersPreserved=True | P10.2.5GeometryPreserved=True\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.5 Closed Blockers Ready",
                "四个红色 Debug 封口已经从正式 Prefab 中移除。\n\n" +
                "ClosedBlocker 仍保留 BoxCollider2D，因此未连接出口仍然会封闭；" +
                "连接成立时现有 DoorSocket.SetOpen 会照常关闭整个 Blocker。\n\n" +
                "现在执行 P10.5 第 2 项 Validate。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog("P10.5 已中止。请把 Console 第一条红色错误发给我。");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    [MenuItem(MenuRoot + "2. Validate Production Closed Blocker Contract", false, 2751)]
    private static void ValidateProductionClosedBlockerContract()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(CrossroadPrefabPath);

            if (root == null)
            {
                throw new InvalidOperationException(
                    "找不到 Crossroad_01 Prefab：\n" + CrossroadPrefabPath);
            }

            List<string> errors = ValidateLoadedPrefab(root);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "P10.5 校验失败：\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log(
                "[P10.5] Crossroad_01 ClosedBlocker 校验通过。\n" +
                "Sockets=4 | ClosedBlockers=4 | ActiveByDefault=4\n" +
                "DebugSpriteRenderers=0\n" +
                "North/South Collider=2x0.35 | East/West Collider=0.35x2\n" +
                "BlockerTransforms=IdentityScale\n" +
                "DoorSocketReferences=Valid\n" +
                "RuntimeRule=UnusedSocket blocked / UsedSocket SetOpen(true)\n" +
                "P10.4ArtLayers=Preserved | P10.2.5Geometry=Preserved\n" +
                "CatalogChanged=False | GameSceneChanged=False | CoreCodeChanged=False");

            EditorUtility.DisplayDialog(
                "P10.5 Passed",
                "ClosedBlocker 已从红色 Debug 视觉变成正式的逻辑封口。\n\n" +
                "当前没有伪造门体美术；以后若需要正式封口图，只需添加 SpriteRenderer，" +
                "不需要修改 DoorSocket 逻辑。",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FailDialog("P10.5 校验失败。请把 Console 第一条红色错误发给我。");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ConfigureBlockerTransformAndCollider(
        GameObject blocker,
        DreamRoomDoorDirection direction)
    {
        Vector3 position;
        Vector2 colliderSize;

        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position = new Vector3(0f, HalfRoom, 0f);
                colliderSize = new Vector2(DoorWidth, BlockerThickness);
                break;

            case DreamRoomDoorDirection.East:
                position = new Vector3(HalfRoom, 0f, 0f);
                colliderSize = new Vector2(BlockerThickness, DoorWidth);
                break;

            case DreamRoomDoorDirection.South:
                position = new Vector3(0f, -HalfRoom, 0f);
                colliderSize = new Vector2(DoorWidth, BlockerThickness);
                break;

            case DreamRoomDoorDirection.West:
                position = new Vector3(-HalfRoom, 0f, 0f);
                colliderSize = new Vector2(BlockerThickness, DoorWidth);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    "Unsupported door direction.");
        }

        Transform transform = blocker.transform;
        transform.localPosition = position;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = blocker.AddComponent<BoxCollider2D>();
        }

        collider.offset = Vector2.zero;
        collider.size = colliderSize;
        collider.isTrigger = false;
        collider.enabled = true;
    }

    private static List<string> ValidateLoadedPrefab(GameObject root)
    {
        List<string> errors = new List<string>();

        DreamRoomTemplate template = root.GetComponent<DreamRoomTemplate>();
        if (template == null)
        {
            errors.Add("缺少 DreamRoomTemplate。");
            return errors;
        }

        if (template.SizeInCells != new Vector2Int(16, 16))
        {
            errors.Add("SizeInCells 应为 16x16。");
        }

        Transform blockersRoot =
            root.transform.Find("Visual/Objects/ClosedBlockers");

        if (blockersRoot == null)
        {
            errors.Add("缺少 Visual/Objects/ClosedBlockers。");
            return errors;
        }

        DreamRoomDoorSocket[] sockets =
            root.GetComponentsInChildren<DreamRoomDoorSocket>(true);

        if (sockets.Length != 4)
        {
            errors.Add("DoorSocket 数量应为 4，实际=" + sockets.Length + "。");
        }

        HashSet<DreamRoomDoorDirection> directions =
            new HashSet<DreamRoomDoorDirection>();

        for (int i = 0; i < sockets.Length; i++)
        {
            DreamRoomDoorSocket socket = sockets[i];

            if (socket == null)
            {
                errors.Add("DoorSocket 中存在 null。");
                continue;
            }

            directions.Add(socket.Direction);

            if (socket.DoorWidthInCells != 2)
            {
                errors.Add(socket.SocketId + " 门宽应为 2 Cell。");
            }

            GameObject blocker = socket.ClosedBlocker;
            if (blocker == null)
            {
                errors.Add(socket.SocketId + " 缺少 ClosedBlocker。");
                continue;
            }

            if (blocker.transform.parent != blockersRoot)
            {
                errors.Add(socket.SocketId + " ClosedBlocker 不在 ClosedBlockers 根下。");
            }

            if (!blocker.activeSelf)
            {
                errors.Add(socket.SocketId + " ClosedBlocker 默认必须启用。");
            }

            if (!Approximately(blocker.transform.localScale, Vector3.one))
            {
                errors.Add(socket.SocketId + " ClosedBlocker Scale 应为 1,1,1。");
            }

            if (!Approximately(blocker.transform.localRotation, Quaternion.identity))
            {
                errors.Add(socket.SocketId + " ClosedBlocker Rotation 应为 Identity。");
            }

            Vector3 expectedPosition;
            Vector2 expectedSize;
            GetExpectedGeometry(socket.Direction, out expectedPosition, out expectedSize);

            if (!Approximately(blocker.transform.localPosition, expectedPosition))
            {
                errors.Add(
                    socket.SocketId + " ClosedBlocker Position 不匹配。实际=" +
                    blocker.transform.localPosition + " 预期=" + expectedPosition);
            }

            BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                errors.Add(socket.SocketId + " ClosedBlocker 缺少 BoxCollider2D。");
            }
            else
            {
                if (!Approximately(collider.offset, Vector2.zero))
                {
                    errors.Add(socket.SocketId + " Collider Offset 应为 0,0。");
                }

                if (!Approximately(collider.size, expectedSize))
                {
                    errors.Add(
                        socket.SocketId + " Collider Size 不匹配。实际=" +
                        collider.size + " 预期=" + expectedSize);
                }

                if (collider.isTrigger)
                {
                    errors.Add(socket.SocketId + " Collider 不应是 Trigger。");
                }
            }

            if (blocker.GetComponent<SpriteRenderer>() != null)
            {
                errors.Add(socket.SocketId + " 仍带有 P10.0 Debug SpriteRenderer。");
            }
        }

        if (directions.Count != 4)
        {
            errors.Add("必须包含 North/East/South/West 四个方向。");
        }

        if (root.transform.Find("Visual/Floor/Floor_Runtime") == null)
        {
            errors.Add("P10.4 Floor_Runtime 不存在。");
        }

        if (root.transform.Find("Visual/Objects/Objects_Runtime") == null)
        {
            errors.Add("P10.4 Objects_Runtime 不存在。");
        }

        if (root.transform.Find("Visual/Effects/Effects_Runtime") == null)
        {
            errors.Add("P10.4 Effects_Runtime 不存在。");
        }

        if (root.transform.Find("Navigation/Colliders/P10_1_Geometry") == null)
        {
            errors.Add("P10.2.5 Geometry 不存在。");
        }

        return errors;
    }

    private static void GetExpectedGeometry(
        DreamRoomDoorDirection direction,
        out Vector3 position,
        out Vector2 colliderSize)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North:
                position = new Vector3(0f, HalfRoom, 0f);
                colliderSize = new Vector2(DoorWidth, BlockerThickness);
                return;

            case DreamRoomDoorDirection.East:
                position = new Vector3(HalfRoom, 0f, 0f);
                colliderSize = new Vector2(BlockerThickness, DoorWidth);
                return;

            case DreamRoomDoorDirection.South:
                position = new Vector3(0f, -HalfRoom, 0f);
                colliderSize = new Vector2(DoorWidth, BlockerThickness);
                return;

            case DreamRoomDoorDirection.West:
                position = new Vector3(-HalfRoom, 0f, 0f);
                colliderSize = new Vector2(BlockerThickness, DoorWidth);
                return;

            default:
                position = Vector3.zero;
                colliderSize = Vector2.zero;
                return;
        }
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 0.000001f;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Vector2.SqrMagnitude(a - b) < 0.000001f;
    }

    private static bool Approximately(Quaternion a, Quaternion b)
    {
        return Mathf.Abs(Quaternion.Dot(a, b)) > 0.999999f;
    }

    private static void FailDialog(string message)
    {
        EditorUtility.DisplayDialog("Dream Dungeon P10.5", message, "OK");
    }
}
