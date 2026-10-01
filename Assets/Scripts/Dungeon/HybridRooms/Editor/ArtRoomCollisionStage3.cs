using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ArtRoom_01 Stage 3:
/// Rebuild collision for the 11x18 visual refit.
///
/// The old 13x21 art was scaled to 11x18 with:
/// X = 11/13, Y = 18/21.
/// Physical collision is transformed by the exact same affine scale.
/// Grid navigation is then re-derived on the 11x18 grid from the
/// transformed hard-collision geometry.
///
/// This does NOT copy the old blocked-cell list verbatim.
/// </summary>
public static class ArtRoomCollisionStage3
{
    private const string MenuPath =
        "Tools/Dream Dungeon/Production Rooms/ArtRoom Rebuild/" +
        "Stage 3 - Rebuild Collision for 11x18";

    private const string PrefabPath =
        "Assets/DreamDungeon/Production/Rooms/ArtRoom_01/" +
        "Room_ArtRoom_01.prefab";

    private static readonly Vector2Int RoomSize =
        new Vector2Int(11, 18);

    private const float ScaleX = 11f / 13f;
    private const float ScaleY = 18f / 21f;

    // 2026-10-01 visual review overrides.
    // Red markup = add hard collision.
    // Cyan markup = remove hard collision.
    private static readonly Vector2Int[] ManualAddCells =
    {
        // Left perimeter / furniture strip.
        new Vector2Int(0, 14),
        new Vector2Int(0, 13), new Vector2Int(1, 13),
        new Vector2Int(0, 12), new Vector2Int(1, 12),
        new Vector2Int(0, 11), new Vector2Int(1, 11),
        new Vector2Int(0, 10), new Vector2Int(1, 10),
        new Vector2Int(0, 9),
        new Vector2Int(0, 8),
        new Vector2Int(0, 7),
        new Vector2Int(0, 6),
        new Vector2Int(0, 5),
        new Vector2Int(0, 4),
        new Vector2Int(0, 3),
        new Vector2Int(0, 2),

        // Bottom-left block, leaving the South_0 doorway clear.
        new Vector2Int(0, 1), new Vector2Int(1, 1),
        new Vector2Int(2, 1), new Vector2Int(3, 1),
        new Vector2Int(0, 0), new Vector2Int(1, 0),
        new Vector2Int(2, 0), new Vector2Int(3, 0),

        // Bottom-right block.
        new Vector2Int(6, 1), new Vector2Int(7, 1),
        new Vector2Int(8, 1), new Vector2Int(9, 1),
        new Vector2Int(6, 0), new Vector2Int(7, 0),
        new Vector2Int(8, 0), new Vector2Int(9, 0),

        // Right perimeter / furniture strip.
        new Vector2Int(10, 12),
        new Vector2Int(10, 11),
        new Vector2Int(10, 10),
        new Vector2Int(10, 9),
        new Vector2Int(10, 8),
        new Vector2Int(10, 7),
        new Vector2Int(10, 6),
        new Vector2Int(10, 5),
        new Vector2Int(10, 4),
        new Vector2Int(10, 3),
        new Vector2Int(10, 2),

        // Individually circled red cells.
        new Vector2Int(6, 13),
        new Vector2Int(7, 12),
        new Vector2Int(7, 11),
        new Vector2Int(7, 10),
        new Vector2Int(3, 8),
        new Vector2Int(4, 8),
        new Vector2Int(4, 6),
        new Vector2Int(3, 2),
    };

    private static readonly Vector2Int[] ManualRemoveCells =
    {
        new Vector2Int(8, 4),
        new Vector2Int(8, 3),
        new Vector2Int(9, 3),
        new Vector2Int(3, 3),
    };

    private static readonly HardRect[] LegacyHardRects =
    {
        new HardRect("Hard_09_12_Y17", 4.5f, 7f, 4f, 1f),
        new HardRect("Hard_02_03_Y11", -3.5f, 1f, 2f, 1f),
        new HardRect("Hard_10_11_Y09", 4.5f, -1f, 2f, 1f),
        new HardRect("Hard_02_04_Y04", -3f, -6f, 3f, 1f),
        new HardRect("Hard_10_12_Y16", 5f, 6f, 3f, 1f),
        new HardRect("Hard_09_11_Y05", 4f, -5f, 3f, 1f),
        new HardRect("Hard_02_02_Y15", -4f, 5f, 1f, 1f),
        new HardRect("Hard_02_04_Y07", -3f, -3f, 3f, 1f),
        new HardRect("Hard_09_11_Y14", 4f, 4f, 3f, 1f),
        new HardRect("Hard_05_06_Y15", -0.5f, 5f, 2f, 1f),
        new HardRect("Hard_09_11_Y13", 4f, 3f, 3f, 1f),
        new HardRect("Hard_04_09_Y19", 0.5f, 9f, 6f, 1f),
        new HardRect("Hard_10_11_Y04", 4.5f, -6f, 2f, 1f),
        new HardRect("Hard_02_04_Y05", -3f, -5f, 3f, 1f),
        new HardRect("Hard_10_11_Y10", 4.5f, 0f, 2f, 1f),
        new HardRect("Hard_02_04_Y06", -3f, -4f, 3f, 1f),
        new HardRect("Hard_10_11_Y08", 4.5f, -2f, 2f, 1f),
        new HardRect("Hard_00_00_Y19", -6f, 9f, 1f, 1f),
        new HardRect("Hard_09_09_Y18", 3f, 8f, 1f, 1f),
        new HardRect("Hard_02_04_Y08", -3f, -2f, 3f, 1f),
        new HardRect("Hard_02_04_Y10", -3f, 0f, 3f, 1f),
        new HardRect("Hard_00_04_Y20", -4f, 10f, 5f, 1f),
        new HardRect("Hard_02_04_Y03", -3f, -7f, 3f, 1f),
        new HardRect("Hard_01_08_Y16", -1.5f, 6f, 8f, 1f),
        new HardRect("Hard_09_11_Y07", 4f, -3f, 3f, 1f),
        new HardRect("Hard_02_03_Y09", -3.5f, -1f, 2f, 1f),
        new HardRect("Hard_00_00_Y18", -6f, 8f, 1f, 1f),
        new HardRect("Hard_04_04_Y18", -2f, 8f, 1f, 1f),
        new HardRect("Hard_10_11_Y11", 4.5f, 1f, 2f, 1f),
        new HardRect("Hard_09_11_Y12", 4f, 2f, 3f, 1f),
        new HardRect("Hard_02_03_Y02", -3.5f, -8f, 2f, 1f),
        new HardRect("Hard_01_03_Y17", -4f, 7f, 3f, 1f),
        new HardRect("Hard_09_11_Y06", 4f, -4f, 3f, 1f),
        new HardRect("Hard_08_12_Y15", 4f, 5f, 5f, 1f),
    };

    private static readonly PartialPolygon[] LegacyPartialPolygons =
    {
        new PartialPolygon(
            "Partial_04_09",
            new[]
            {
                new Vector2(-1.828125f, -0.578125f),
                new Vector2(-1.5f, -0.609375f),
                new Vector2(-1.5f, -1.3125f),
                new Vector2(-1.890625f, -1.21875f),
            }),
        new PartialPolygon(
            "Partial_05_09",
            new[]
            {
                new Vector2(-1.5f, -0.609375f),
                new Vector2(-0.953125f, -0.59375f),
                new Vector2(-0.8125f, -1.078125f),
                new Vector2(-0.921875f, -1.421875f),
                new Vector2(-1.5f, -1.3125f),
            }),
        new PartialPolygon(
            "Partial_07_15",
            new[]
            {
                new Vector2(0.5f, 5.359375f),
                new Vector2(0.796875f, 5.28125f),
                new Vector2(0.90625f, 4.96875f),
                new Vector2(0.75f, 4.640625f),
                new Vector2(0.5f, 4.71875f),
            }),
    };

    [MenuItem(MenuPath, false, 2782)]
    private static void RebuildCollision()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Stage 3",
                "请先退出 Play Mode。",
                "OK");
            return;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "ArtRoom Stage 3",
                "请先退出 Prefab Mode。",
                "OK");
            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "ArtRoom_01 Stage 3",
                "将按 13x21 → 11x18 的同一缩放关系重建碰撞。\n\n" +
                "会执行：\n" +
                "• 以旧碰撞缩放结果作为基准\n" +
                "• 套用截图红色 52 格新增 / 青色 4 格取消\n" +
                "• 最终 blockedCells 重建为按行合并 BoxCollider2D\n" +
                "• 3 个局部 PolygonCollider 继续同比例缩放\n" +
                "• 只保留与 South_0 连通的 walkableCells\n\n" +
                "不会复制旧 96 格列表。\n\n" +
                "继续？",
                "Rebuild Collision",
                "Cancel");

        if (!confirmed)
        {
            return;
        }

        GameObject root = null;

        try
        {
            root =
                PrefabUtility.LoadPrefabContents(
                    PrefabPath);

            if (root == null)
            {
                throw new InvalidOperationException(
                    "无法加载 ArtRoom Prefab。");
            }

            DreamRoomTemplate template =
                root.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException(
                    "Prefab 根节点缺少 DreamRoomTemplate。");
            }

            if (template.SizeInCells != RoomSize)
            {
                throw new InvalidOperationException(
                    "Stage 3 只允许在 11x18 ArtRoom 上执行。");
            }

            Transform colliders =
                root.transform.Find(
                    "Navigation/Colliders");

            if (colliders == null)
            {
                throw new InvalidOperationException(
                    "缺少 Navigation/Colliders。");
            }

            Transform interior =
                colliders.Find("Interior");

            if (interior == null)
            {
                throw new InvalidOperationException(
                    "缺少 Navigation/Colliders/Interior。");
            }

            Transform hardBlocks =
                colliders.Find("HardBlocks");

            if (hardBlocks == null)
            {
                hardBlocks =
                    CreateEmptyChild(
                        colliders,
                        "HardBlocks");
            }

            DestroyAllChildren(hardBlocks);
            DestroyAllChildren(interior);

            List<Vector2Int> blockedCells =
                DeriveBlockedCells();

            ApplyManualReviewOverrides(
                blockedCells);

            BuildGridHardBlocks(
                hardBlocks,
                blockedCells);

            BuildScaledPartialColliders(
                interior);

            List<Vector2Int> walkableCells =
                DeriveDoorConnectedWalkable(
                    blockedCells);

            ApplyGridOverrides(
                template,
                blockedCells,
                walkableCells);

            template.RefreshDoorSockets();
            template.RefreshSpawnPoints();

            ValidateBeforeSave(
                template,
                hardBlocks,
                interior,
                blockedCells,
                walkableCells);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath);

            AssetDatabase.SaveAssets();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "ArtRoom Stage 3 Failed",
                "碰撞重建失败。\n" +
                "请把 Console 第一条红色错误发给我。",
                "OK");

            return;
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    root);
            }
        }

        ValidateSavedPrefab();

        Debug.Log(
            "[ArtRoom Stage 3] Collision rebuilt for 11x18.\n" +
            "HardBlocks=42 row-merged colliders\n" +
            "PartialPolygons=3 scaled colliders\n" +
            "BlockedCells=110\n" +
            "DoorConnectedWalkable=57\n" +
            "ExcludedDisconnectedCells=31\n" +
            "ManualAddCells=52 | ManualRemoveCells=4\n" +
            "DoorCells=(4,0),(5,0) walkable\n" +
            "ScaleX=11/13 | ScaleY=18/21\n" +
            "LegacyBlockedCellsCopied=False");

        EditorUtility.DisplayDialog(
            "ArtRoom Stage 3 Passed",
            "11x18 画室碰撞已重建。\n\n" +
            "HardBlocks：42\n" +
            "Partial Polygon：3\n" +
            "BlockedCells：110\n" +
            "South_0 连通 Walkable：57\n" +
            "人工加碰撞：52 格 / 取消碰撞：4 格\n\n" +
            "下一步：P10.7 Validate + Prefab 可视检查。",
            "OK");
    }

    private static void BuildGridHardBlocks(
        Transform parent,
        List<Vector2Int> blockedCells)
    {
        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>(
                blockedCells);

        int index = 0;

        for (int y = 0;
             y < RoomSize.y;
             y++)
        {
            int x = 0;

            while (x < RoomSize.x)
            {
                if (!blocked.Contains(
                        new Vector2Int(x, y)))
                {
                    x++;
                    continue;
                }

                int startX = x;

                while (x + 1 < RoomSize.x &&
                       blocked.Contains(
                           new Vector2Int(
                               x + 1,
                               y)))
                {
                    x++;
                }

                int endX = x;
                int width = endX - startX + 1;

                GameObject go =
                    new GameObject(
                        "Hard_" +
                        startX.ToString("00") +
                        "_" +
                        endX.ToString("00") +
                        "_Y" +
                        y.ToString("00") +
                        "_Review");

                go.transform.SetParent(
                    parent,
                    false);

                Vector2 first =
                    CellCenter(
                        startX,
                        y);

                Vector2 last =
                    CellCenter(
                        endX,
                        y);

                go.transform.localPosition =
                    new Vector3(
                        (first.x + last.x) * 0.5f,
                        first.y,
                        0f);

                BoxCollider2D collider =
                    go.AddComponent<BoxCollider2D>();

                collider.size =
                    new Vector2(
                        width,
                        1f);

                collider.offset = Vector2.zero;
                collider.isTrigger = false;

                index++;
                x++;
            }
        }
    }

    private static void ApplyManualReviewOverrides(
        List<Vector2Int> blockedCells)
    {
        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>(
                blockedCells);

        for (int i = 0;
             i < ManualRemoveCells.Length;
             i++)
        {
            blocked.Remove(
                ManualRemoveCells[i]);
        }

        for (int i = 0;
             i < ManualAddCells.Length;
             i++)
        {
            Vector2Int cell =
                ManualAddCells[i];

            if (cell.x < 0 ||
                cell.y < 0 ||
                cell.x >= RoomSize.x ||
                cell.y >= RoomSize.y)
            {
                throw new InvalidOperationException(
                    "ManualAddCells 越界：" +
                    cell);
            }

            blocked.Add(cell);
        }

        blockedCells.Clear();
        blockedCells.AddRange(blocked);
        blockedCells.Sort(CompareCells);
    }

    private static void BuildScaledPartialColliders(
        Transform parent)
    {
        for (int i = 0;
             i < LegacyPartialPolygons.Length;
             i++)
        {
            PartialPolygon source =
                LegacyPartialPolygons[i];

            GameObject go =
                new GameObject(
                    source.Name + "_11x18");

            go.transform.SetParent(
                parent,
                false);

            go.transform.localPosition =
                Vector3.zero;

            PolygonCollider2D collider =
                go.AddComponent<PolygonCollider2D>();

            Vector2[] scaled =
                new Vector2[
                    source.Points.Length];

            for (int p = 0;
                 p < source.Points.Length;
                 p++)
            {
                scaled[p] =
                    new Vector2(
                        source.Points[p].x * ScaleX,
                        source.Points[p].y * ScaleY);
            }

            collider.pathCount = 1;
            collider.SetPath(
                0,
                scaled);

            collider.isTrigger = false;
        }
    }

    private static List<Vector2Int>
        DeriveBlockedCells()
    {
        List<Vector2Int> result =
            new List<Vector2Int>();

        for (int y = 0;
             y < RoomSize.y;
             y++)
        {
            for (int x = 0;
                 x < RoomSize.x;
                 x++)
            {
                Vector2 center =
                    CellCenter(x, y);

                if (IsInsideScaledHardCollision(
                        center))
                {
                    result.Add(
                        new Vector2Int(
                            x,
                            y));
                }
            }
        }

        return result;
    }

    private static bool IsInsideScaledHardCollision(
        Vector2 point)
    {
        const float epsilon = 0.0001f;

        for (int i = 0;
             i < LegacyHardRects.Length;
             i++)
        {
            HardRect source =
                LegacyHardRects[i];

            float centerX =
                source.CenterX * ScaleX;

            float centerY =
                source.CenterY * ScaleY;

            float halfWidth =
                source.Width *
                ScaleX *
                0.5f;

            float halfHeight =
                source.Height *
                ScaleY *
                0.5f;

            if (Mathf.Abs(
                    point.x -
                    centerX) <=
                halfWidth + epsilon &&
                Mathf.Abs(
                    point.y -
                    centerY) <=
                halfHeight + epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static List<Vector2Int>
        DeriveDoorConnectedWalkable(
            List<Vector2Int> blockedCells)
    {
        HashSet<Vector2Int> blocked =
            new HashSet<Vector2Int>(
                blockedCells);

        HashSet<Vector2Int> visited =
            new HashSet<Vector2Int>();

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        Vector2Int[] doorCells =
        {
            new Vector2Int(4, 0),
            new Vector2Int(5, 0),
        };

        for (int i = 0;
             i < doorCells.Length;
             i++)
        {
            if (!blocked.Contains(
                    doorCells[i]))
            {
                visited.Add(
                    doorCells[i]);

                queue.Enqueue(
                    doorCells[i]);
            }
        }

        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down,
        };

        while (queue.Count > 0)
        {
            Vector2Int current =
                queue.Dequeue();

            for (int i = 0;
                 i < directions.Length;
                 i++)
            {
                Vector2Int next =
                    current +
                    directions[i];

                if (next.x < 0 ||
                    next.y < 0 ||
                    next.x >= RoomSize.x ||
                    next.y >= RoomSize.y ||
                    blocked.Contains(next) ||
                    !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        List<Vector2Int> result =
            new List<Vector2Int>(
                visited);

        result.Sort(
            CompareCells);

        return result;
    }

    private static void ApplyGridOverrides(
        DreamRoomTemplate template,
        List<Vector2Int> blockedCells,
        List<Vector2Int> walkableCells)
    {
        SerializedObject serialized =
            new SerializedObject(
                template);

        SerializedProperty occupied =
            RequireProperty(
                serialized,
                "occupiedCells");

        SerializedProperty blocked =
            RequireProperty(
                serialized,
                "blockedCells");

        SerializedProperty walkable =
            RequireProperty(
                serialized,
                "walkableCells");

        occupied.arraySize = 0;

        WriteCells(
            blocked,
            blockedCells);

        WriteCells(
            walkable,
            walkableCells);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(template);
    }

    private static void ValidateBeforeSave(
        DreamRoomTemplate template,
        Transform hardBlocks,
        Transform interior,
        List<Vector2Int> blockedCells,
        List<Vector2Int> walkableCells)
    {
        if (hardBlocks.childCount != 42)
        {
            throw new InvalidOperationException(
                "HardBlocks 数量异常，预期 42 个按行合并碰撞，实际=" +
                hardBlocks.childCount);
        }

        if (interior.childCount !=
            LegacyPartialPolygons.Length)
        {
            throw new InvalidOperationException(
                "Interior Polygon 数量异常：" +
                interior.childCount);
        }

        if (blockedCells.Count != 110)
        {
            throw new InvalidOperationException(
                "BlockedCells 预期 110，实际=" +
                blockedCells.Count);
        }

        if (walkableCells.Count != 57)
        {
            throw new InvalidOperationException(
                "DoorConnected Walkable 预期 57，实际=" +
                walkableCells.Count);
        }

        if (blockedCells.Contains(
                new Vector2Int(4, 0)) ||
            blockedCells.Contains(
                new Vector2Int(5, 0)))
        {
            throw new InvalidOperationException(
                "South_0 门格被 BlockedCells 封死。");
        }

        if (!walkableCells.Contains(
                new Vector2Int(4, 0)) ||
            !walkableCells.Contains(
                new Vector2Int(5, 0)))
        {
            throw new InvalidOperationException(
                "South_0 门格不在 WalkableCells。");
        }

        List<string> errors =
            template.GetValidationErrors();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "DreamRoomTemplate 校验失败：\n- " +
                string.Join(
                    "\n- ",
                    errors));
        }
    }

    private static void ValidateSavedPrefab()
    {
        GameObject loaded = null;

        try
        {
            loaded =
                PrefabUtility.LoadPrefabContents(
                    PrefabPath);

            if (loaded == null)
            {
                throw new InvalidOperationException(
                    "保存后无法加载 Prefab。");
            }

            DreamRoomTemplate template =
                loaded.GetComponent<DreamRoomTemplate>();

            if (template == null)
            {
                throw new InvalidOperationException(
                    "保存后缺少 DreamRoomTemplate。");
            }

            template.RefreshDoorSockets();
            template.RefreshSpawnPoints();

            List<Vector2Int> blocked =
                new List<Vector2Int>();

            List<Vector2Int> walkable =
                new List<Vector2Int>();

            template.GetBlockedCells(
                blocked);

            template.GetWalkableCells(
                walkable);

            Transform hardBlocks =
                loaded.transform.Find(
                    "Navigation/Colliders/HardBlocks");

            Transform interior =
                loaded.transform.Find(
                    "Navigation/Colliders/Interior");

            ValidateBeforeSave(
                template,
                hardBlocks,
                interior,
                blocked,
                walkable);
        }
        finally
        {
            if (loaded != null)
            {
                PrefabUtility.UnloadPrefabContents(
                    loaded);
            }
        }
    }

    private static Vector2 CellCenter(
        int x,
        int y)
    {
        return new Vector2(
            x -
                (RoomSize.x - 1) *
                0.5f,
            y -
                (RoomSize.y - 1) *
                0.5f);
    }

    private static void WriteCells(
        SerializedProperty property,
        List<Vector2Int> cells)
    {
        property.arraySize =
            cells.Count;

        for (int i = 0;
             i < cells.Count;
             i++)
        {
            property
                .GetArrayElementAtIndex(i)
                .vector2IntValue =
                cells[i];
        }
    }

    private static int CompareCells(
        Vector2Int a,
        Vector2Int b)
    {
        int y =
            a.y.CompareTo(
                b.y);

        if (y != 0)
        {
            return y;
        }

        return a.x.CompareTo(
            b.x);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "找不到序列化字段：" +
                propertyName);
        }

        return property;
    }

    private static Transform CreateEmptyChild(
        Transform parent,
        string name)
    {
        GameObject child =
            new GameObject(name);

        child.transform.SetParent(
            parent,
            false);

        child.transform.localPosition =
            Vector3.zero;

        child.transform.localRotation =
            Quaternion.identity;

        child.transform.localScale =
            Vector3.one;

        return child.transform;
    }

    private static void DestroyAllChildren(
        Transform parent)
    {
        for (int i =
                 parent.childCount - 1;
             i >= 0;
             i--)
        {
            UnityEngine.Object.DestroyImmediate(
                parent.GetChild(i).gameObject);
        }
    }

    private readonly struct HardRect
    {
        public readonly string Name;
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float Width;
        public readonly float Height;

        public HardRect(
            string name,
            float centerX,
            float centerY,
            float width,
            float height)
        {
            Name = name;
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }
    }

    private readonly struct PartialPolygon
    {
        public readonly string Name;
        public readonly Vector2[] Points;

        public PartialPolygon(
            string name,
            Vector2[] points)
        {
            Name = name;
            Points = points;
        }
    }
}
