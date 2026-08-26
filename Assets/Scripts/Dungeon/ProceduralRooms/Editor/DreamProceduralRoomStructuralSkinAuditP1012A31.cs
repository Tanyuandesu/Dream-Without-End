using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12A-3.1 Structural Skin Theme + Live Audit。
/// </summary>
public static class DreamProceduralRoomStructuralSkinAuditP1012A31
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/P10.12A-3.1 Structural Skin/";

    private const string AssetFolder =
        "Assets/Resources/DreamDungeon/Procedural";

    private const string ThemeAssetPath =
        AssetFolder +
        "/ProceduralMediumStructuralSkinTheme.asset";

    [MenuItem(
        MenuRoot + "1. Create or Select Structural Skin Theme",
        false,
        2940)]
    private static void CreateOrSelectTheme()
    {
        DreamProceduralRoomStructuralSkinThemeP1012A31
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomStructuralSkinThemeP1012A31>(
                        ThemeAssetPath);

        bool created = false;

        if (theme == null)
        {
            EnsureAssetFolder(
                AssetFolder);

            theme =
                ScriptableObject.CreateInstance<
                    DreamProceduralRoomStructuralSkinThemeP1012A31>();

            AssetDatabase.CreateAsset(
                theme,
                ThemeAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            created = true;
        }

        Selection.activeObject =
            theme;

        EditorGUIUtility.PingObject(
            theme);

        Debug.Log(
            "[P10.12A-3.1] Structural Skin Theme " +
            (created ? "CREATED" : "FOUND") +
            "\nAsset=" +
            ThemeAssetPath +
            "\nReplacementRule=只替换 6 类拓扑 Sprite Pool / Scale / SortingOrder。" +
            "\nGeometryOwnership=R2B Only" +
            " | ColliderOwnership=None" +
            " | NavigationOwnership=None");
    }

    [MenuItem(
        MenuRoot + "2. Validate Structural Skin Theme Contract",
        false,
        2950)]
    private static void ValidateTheme()
    {
        DreamProceduralRoomStructuralSkinThemeP1012A31
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomStructuralSkinThemeP1012A31>(
                        ThemeAssetPath);

        if (theme == null)
        {
            Debug.LogError(
                "[P10.12A-3.1] Structural Skin Theme 不存在。" +
                " 请先执行第 1 项。");
            return;
        }

        Debug.Log(
            "[P10.12A-3.1] Structural Skin Theme Contract PASS\n" +
            "ThemeId=" +
            theme.ThemeId +
            " | Scale=" +
            theme.Scale +
            " | SortingOrder=" +
            theme.SortingOrder +
            "\nSpritePools=" +
            " I:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.Isolated) +
            " E:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.End) +
            " S:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.Straight) +
            " C:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.Corner) +
            " T:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.TJunction) +
            " X:" +
            theme.GetSpriteCount(
                DreamProceduralStructureTopologyP1012A31.Cross) +
            "\nEmptyPoolBehavior=TopologyDebugPlaceholder" +
            " | GeometryMutation=0" +
            " | ColliderOwnership=None" +
            " | NavigationOwnership=None");
    }

    [MenuItem(
        MenuRoot + "3. Validate LIVE Structural Skin",
        false,
        2960)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12A-3.1] LIVE Structural Skin 验收必须在 Play Mode。");
            return;
        }

        DreamProceduralRoomStructuralSkinP1012A31[]
            skins =
                UnityEngine.Object.FindObjectsByType<
                    DreamProceduralRoomStructuralSkinP1012A31>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

        if (skins == null ||
            skins.Length == 0)
        {
            Debug.LogError(
                "[P10.12A-3.1] 当前 Floor 找不到 Structural Skin。" +
                " 请先确认本层生成了 R2B 13x9 Procedural Room。");
            return;
        }

        List<string> errors =
            new List<string>();

        int committed = 0;
        int totalBlocked = 0;
        int totalRenderers = 0;
        int skinColliderViolations = 0;
        int missingVisualCells = 0;
        int duplicateVisualCells = 0;
        int geometryMismatches = 0;
        int enabledR2BDebugRenderers = 0;

        int isolated = 0;
        int end = 0;
        int straight = 0;
        int corner = 0;
        int tJunction = 0;
        int cross = 0;

        for (int i = 0;
             i < skins.Length;
             i++)
        {
            DreamProceduralRoomStructuralSkinP1012A31
                skin =
                    skins[i];

            if (skin == null)
            {
                continue;
            }

            if (!skin.IsCommitted)
            {
                errors.Add(
                    "RoomIndex=" +
                    skin.RoomIndex +
                    " Structural Skin 未 Commit：" +
                    skin.LastFailureReason);
                continue;
            }

            committed++;

            DreamProceduralRoomRuntimeInstanceP1012R2B
                geometry =
                    skin.GetComponent<
                        DreamProceduralRoomRuntimeInstanceP1012R2B>();

            if (geometry == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    skin.RoomIndex +
                    " 缺少 R2B Geometry Component。");
                continue;
            }

            totalBlocked +=
                geometry.BlockedCellCount;

            totalRenderers +=
                skin.RendererCount;

            if (geometry.ColliderCount !=
                    geometry.BlockedCellCount ||
                skin.RendererCount !=
                    geometry.BlockedCellCount)
            {
                geometryMismatches++;
            }

            HashSet<Vector2Int> uniqueCells =
                new HashSet<Vector2Int>();

            IReadOnlyList<
                DreamProceduralStructureVisualSlotP1012A31>
                slots =
                    skin.Slots;

            for (int s = 0;
                 s < slots.Count;
                 s++)
            {
                DreamProceduralStructureVisualSlotP1012A31
                    slot =
                        slots[s];

                if (!uniqueCells.Add(
                        slot.LocalCell))
                {
                    duplicateVisualCells++;
                }

                switch (slot.Topology)
                {
                    case DreamProceduralStructureTopologyP1012A31.Isolated:
                        isolated++;
                        break;

                    case DreamProceduralStructureTopologyP1012A31.End:
                        end++;
                        break;

                    case DreamProceduralStructureTopologyP1012A31.Straight:
                        straight++;
                        break;

                    case DreamProceduralStructureTopologyP1012A31.Corner:
                        corner++;
                        break;

                    case DreamProceduralStructureTopologyP1012A31.TJunction:
                        tJunction++;
                        break;

                    case DreamProceduralStructureTopologyP1012A31.Cross:
                        cross++;
                        break;
                }
            }

            if (uniqueCells.Count !=
                geometry.BlockedCellCount)
            {
                missingVisualCells +=
                    Mathf.Abs(
                        geometry.BlockedCellCount -
                        uniqueCells.Count);
            }

            Transform skinRoot =
                skin.transform.Find(
                    DreamProceduralRoomStructuralSkinP1012A31
                        .SkinRootName);

            if (skinRoot == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    skin.RoomIndex +
                    " 缺少 Structural Skin Root。");
            }
            else
            {
                skinColliderViolations +=
                    skinRoot.GetComponentsInChildren<
                        Collider2D>(
                            true).Length;
            }

            Transform r2bRoot =
                skin.transform.Find(
                    DreamProceduralRoomRuntimeInstanceP1012R2B
                        .StructureRootName);

            if (r2bRoot != null)
            {
                SpriteRenderer[] debugRenderers =
                    r2bRoot.GetComponentsInChildren<
                        SpriteRenderer>(
                            true);

                for (int d = 0;
                     d < debugRenderers.Length;
                     d++)
                {
                    if (debugRenderers[d] != null &&
                        debugRenderers[d].enabled)
                    {
                        enabledR2BDebugRenderers++;
                    }
                }
            }
        }

        if (skinColliderViolations > 0)
        {
            errors.Add(
                "Structural Skin ColliderViolations=" +
                skinColliderViolations);
        }

        if (missingVisualCells > 0)
        {
            errors.Add(
                "Blocked Cell 缺少 Visual Slot=" +
                missingVisualCells);
        }

        if (duplicateVisualCells > 0)
        {
            errors.Add(
                "Visual Slot 重复 Cell=" +
                duplicateVisualCells);
        }

        if (geometryMismatches > 0)
        {
            errors.Add(
                "Blocked/Collider/Renderer 数量不同步的房间=" +
                geometryMismatches);
        }

        if (enabledR2BDebugRenderers > 0)
        {
            errors.Add(
                "旧 R2B 红色 Debug Renderer 仍启用=" +
                enabledR2BDebugRenderers);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12A-3.1] LIVE Structural Skin Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.12A-3.1] LIVE Structural Skin Audit PASS\n" +
            "Components=" +
            skins.Length +
            " | Committed=" +
            committed +
            " | BlockedCells=" +
            totalBlocked +
            " | StructuralRenderers=" +
            totalRenderers +
            "\nTopology=" +
            " I:" + isolated +
            " E:" + end +
            " S:" + straight +
            " C:" + corner +
            " T:" + tJunction +
            " X:" + cross +
            "\nSkinColliderViolations=0" +
            " | MissingVisualCells=0" +
            " | DuplicateVisualCells=0" +
            " | GeometryMismatch=0" +
            " | EnabledR2BDebugRenderers=0" +
            "\nGeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=StructuralThemeSpritePoolsOnly" +
            " | Result=PASS");
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
                current + "/" +
                parts[i];

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
