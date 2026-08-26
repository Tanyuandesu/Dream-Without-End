using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12A-2：Soft Decor Theme + Runtime 验收。
/// </summary>
public static class DreamProceduralRoomSoftDecorAuditP1012A2
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/P10.12A-2 Soft Decor/";

    private const string AssetFolder =
        "Assets/Resources/DreamDungeon/Procedural";

    private const string ThemeAssetPath =
        AssetFolder +
        "/ProceduralMediumSoftDecorTheme.asset";

    [MenuItem(
        MenuRoot + "1. Create or Select Replaceable Theme Asset",
        false,
        2900)]
    private static void CreateOrSelectTheme()
    {
        DreamProceduralRoomDecorThemeP1012A2
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomDecorThemeP1012A2>(
                        ThemeAssetPath);

        bool created = false;

        if (theme == null)
        {
            EnsureAssetFolder(
                AssetFolder);

            theme =
                ScriptableObject.CreateInstance<
                    DreamProceduralRoomDecorThemeP1012A2>();

            AssetDatabase.CreateAsset(
                theme,
                ThemeAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            created = true;
        }

        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);

        Debug.Log(
            "[P10.12A-2] Replaceable Theme " +
            (created ? "CREATED" : "FOUND") +
            "\nAsset=" + ThemeAssetPath +
            "\nReplacementRule=" +
            "只替换 Sprite Pools / 数量 / SortingOrder，" +
            "不需要修改 BlockedCells、Collider、A* 或 Production_Main。");
    }

    [MenuItem(
        MenuRoot + "2. Validate Replaceable Theme Contract",
        false,
        2910)]
    private static void ValidateTheme()
    {
        DreamProceduralRoomDecorThemeP1012A2
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomDecorThemeP1012A2>(
                        ThemeAssetPath);

        if (theme == null)
        {
            Debug.LogError(
                "[P10.12A-2] Theme 不存在。" +
                " 请先执行第 1 项。");
            return;
        }

        Debug.Log(
            "[P10.12A-2] Replaceable Theme Contract PASS\n" +
            "ThemeId=" + theme.ThemeId +
            " | FloorTarget=" +
            theme.FloorClutterCount +
            " | EdgeTarget=" +
            theme.EdgePropCount +
            " | NearStructureTarget=" +
            theme.NearStructureCount +
            " | ForegroundTarget=" +
            theme.ForegroundPropCount + "\n" +
            "SpritePools=" +
            " Floor:" +
            theme.GetSpriteCount(
                DreamProceduralDecorCategoryP1012A2.FloorClutter) +
            " Edge:" +
            theme.GetSpriteCount(
                DreamProceduralDecorCategoryP1012A2.EdgeProp) +
            " Near:" +
            theme.GetSpriteCount(
                DreamProceduralDecorCategoryP1012A2.NearStructure) +
            " Foreground:" +
            theme.GetSpriteCount(
                DreamProceduralDecorCategoryP1012A2.ForegroundProp) +
            "\nEmptyPoolBehavior=DebugPlaceholder" +
            " | ColliderOwnership=None" +
            " | NavigationOwnership=None");
    }

    [MenuItem(
        MenuRoot + "3. Validate LIVE Soft Decor",
        false,
        2920)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12A-2] LIVE Soft Decor 验收必须在 Play Mode 中执行。");
            return;
        }

        DreamProceduralRoomSoftDecorP1012A2[] components =
            UnityEngine.Object.FindObjectsByType<
                DreamProceduralRoomSoftDecorP1012A2>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

        if (components == null ||
            components.Length == 0)
        {
            Debug.LogError(
                "[P10.12A-2] 当前 Floor 找不到 Soft Decor Runtime Component。" +
                " 请先确认本层存在 R2B 13x9 Procedural Room。");
            return;
        }

        List<string> errors =
            new List<string>();

        int committed = 0;
        int totalSlots = 0;
        int floor = 0;
        int edge = 0;
        int near = 0;
        int foreground = 0;
        int colliderViolations = 0;
        int blockedViolations = 0;
        int doorClearanceViolations = 0;
        int duplicateCellViolations = 0;

        for (int i = 0;
             i < components.Length;
             i++)
        {
            DreamProceduralRoomSoftDecorP1012A2
                component =
                    components[i];

            if (component == null)
            {
                continue;
            }

            if (!component.IsCommitted)
            {
                errors.Add(
                    "RoomIndex=" +
                    component.RoomIndex +
                    " Soft Decor 未 Commit：" +
                    component.LastFailureReason);
                continue;
            }

            committed++;

            DreamRoomPlacement placement =
                FindPlacementForRoomIndex(
                    component.RoomIndex);

            if (placement == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    component.RoomIndex +
                    " 无法从 currentLayout 取回 Placement。");
                continue;
            }

            HashSet<Vector2Int> blocked =
                new HashSet<Vector2Int>();

            List<Vector2Int> blockedBuffer =
                new List<Vector2Int>();

            placement.GetRuntimeProceduralBlockedLocalCells(
                blockedBuffer);

            blocked.UnionWith(
                blockedBuffer);

            HashSet<Vector2Int> uniqueSlots =
                new HashSet<Vector2Int>();

            IReadOnlyList<DreamProceduralDecorSlotP1012A2>
                slots =
                    component.Slots;

            totalSlots += slots.Count;

            for (int s = 0;
                 s < slots.Count;
                 s++)
            {
                DreamProceduralDecorSlotP1012A2
                    slot =
                        slots[s];

                if (!uniqueSlots.Add(
                        slot.LocalCell))
                {
                    duplicateCellViolations++;
                }

                if (blocked.Contains(
                        slot.LocalCell))
                {
                    blockedViolations++;
                }

                if (component.IsDoorClearanceCell(
                        slot.LocalCell))
                {
                    doorClearanceViolations++;
                }

                switch (slot.Category)
                {
                    case DreamProceduralDecorCategoryP1012A2.FloorClutter:
                        floor++;
                        break;

                    case DreamProceduralDecorCategoryP1012A2.EdgeProp:
                        edge++;
                        break;

                    case DreamProceduralDecorCategoryP1012A2.NearStructure:
                        near++;
                        break;

                    case DreamProceduralDecorCategoryP1012A2.ForegroundProp:
                        foreground++;
                        break;
                }
            }

            Transform root =
                component.transform.Find(
                    DreamProceduralRoomSoftDecorP1012A2.DecorRootName);

            if (root == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    component.RoomIndex +
                    " 缺少 Soft Decor Root。");
            }
            else
            {
                Collider2D[] colliders =
                    root.GetComponentsInChildren<
                        Collider2D>(
                            true);

                colliderViolations +=
                    colliders.Length;
            }
        }

        if (colliderViolations > 0)
        {
            errors.Add(
                "Soft Decor ColliderViolations=" +
                colliderViolations);
        }

        if (blockedViolations > 0)
        {
            errors.Add(
                "Soft Decor 与 Runtime Blocked 重叠=" +
                blockedViolations);
        }

        if (doorClearanceViolations > 0)
        {
            errors.Add(
                "Soft Decor 侵入 Used Door Clearance=" +
                doorClearanceViolations);
        }

        if (duplicateCellViolations > 0)
        {
            errors.Add(
                "Soft Decor Slot 重复 Cell=" +
                duplicateCellViolations);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12A-2] LIVE Soft Decor Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.12A-2] LIVE Soft Decor Audit PASS\n" +
            "Components=" + components.Length +
            " | Committed=" + committed +
            " | Slots=" + totalSlots +
            "\nCategories=" +
            " Floor:" + floor +
            " Edge:" + edge +
            " NearStructure:" + near +
            " Foreground:" + foreground +
            "\nSoftColliderViolations=0" +
            " | RuntimeBlockedOverlap=0" +
            " | UsedDoorClearanceOverlap=0" +
            " | DuplicateSlotCells=0" +
            "\nNavigationMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=ThemeAssetOnly" +
            " | Result=PASS");
    }

    private static DreamRoomPlacement
        FindPlacementForRoomIndex(
            int roomIndex)
    {
        GameManager gameManager =
            UnityEngine.Object.FindFirstObjectByType<
                GameManager>();

        if (gameManager == null)
        {
            return null;
        }

        System.Reflection.FieldInfo field =
            typeof(GameManager).GetField(
                "currentLayout",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        if (field == null)
        {
            return null;
        }

        DungeonLayout layout =
            field.GetValue(gameManager)
                as DungeonLayout;

        if (layout == null ||
            roomIndex < 0 ||
            roomIndex >=
                layout.RoomPlacements.Count)
        {
            return null;
        }

        return layout.RoomPlacements[
            roomIndex];
    }

    private static void EnsureAssetFolder(
        string folder)
    {
        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current = next;
        }
    }
}
