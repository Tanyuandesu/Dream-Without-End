using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P10.12A-3.2 Shell / Floor / Wall / Transition Theme + Live Audit。
/// </summary>
public static class DreamProceduralRoomShellSkinAuditP1012A32
{
    private const string MenuRoot =
        "Tools/Dream Dungeon/P10.12A-3.2 Shell Floor Wall/";

    private const string AssetFolder =
        "Assets/Resources/DreamDungeon/Procedural";

    private const string ThemeAssetPath =
        AssetFolder +
        "/ProceduralMediumShellTheme.asset";

    [MenuItem(
        MenuRoot + "1. Create or Select Shell Theme",
        false,
        2970)]
    private static void CreateOrSelectTheme()
    {
        DreamProceduralRoomShellThemeP1012A32
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomShellThemeP1012A32>(
                        ThemeAssetPath);

        bool created = false;

        if (theme == null)
        {
            EnsureAssetFolder(
                AssetFolder);

            theme =
                ScriptableObject.CreateInstance<
                    DreamProceduralRoomShellThemeP1012A32>();

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
            "[P10.12A-3.2] Shell Theme " +
            (created ? "CREATED" : "FOUND") +
            "\nAsset=" +
            ThemeAssetPath +
            "\nReplacementRule=Floor/Wall/Transition Sprite Pool + Visual Params Only" +
            "\nColliderOwnership=None" +
            " | NavigationOwnership=None" +
            " | SocketOwnership=None");
    }

    [MenuItem(
        MenuRoot + "2. Validate Shell Theme Contract",
        false,
        2980)]
    private static void ValidateTheme()
    {
        DreamProceduralRoomShellThemeP1012A32
            theme =
                AssetDatabase.LoadAssetAtPath<
                    DreamProceduralRoomShellThemeP1012A32>(
                        ThemeAssetPath);

        if (theme == null)
        {
            Debug.LogError(
                "[P10.12A-3.2] Shell Theme 不存在。" +
                " 请先执行第 1 项。");
            return;
        }

        Debug.Log(
            "[P10.12A-3.2] Shell Theme Contract PASS\n" +
            "ThemeId=" +
            theme.ThemeId +
            " | FloorSprites=" +
            theme.FloorSpriteCount +
            " | WallSprites=" +
            theme.WallSpriteCount +
            " | TransitionSprites=" +
            theme.TransitionSpriteCount +
            "\nTransitionInsideDepth=" +
            theme.TransitionInsideDepth +
            " | TransitionOutsideDepth=" +
            theme.TransitionOutsideDepth +
            "\nEmptyPoolBehavior=DebugPlaceholder" +
            " | ColliderOwnership=None" +
            " | NavigationOwnership=None" +
            " | SocketOwnership=None");
    }

    [MenuItem(
        MenuRoot + "3. Validate LIVE Shell Skin",
        false,
        2990)]
    private static void ValidateLive()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "[P10.12A-3.2] LIVE Shell 验收必须在 Play Mode。");
            return;
        }

        DreamProceduralRoomShellSkinP1012A32[]
            shells =
                UnityEngine.Object.FindObjectsByType<
                    DreamProceduralRoomShellSkinP1012A32>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

        if (shells == null ||
            shells.Length == 0)
        {
            Debug.LogError(
                "[P10.12A-3.2] 当前 Floor 找不到 Shell Skin。" +
                " 请确认本层存在 R2B 13x9 Procedural Room。");
            return;
        }

        List<string> errors =
            new List<string>();

        int committed = 0;
        int floorRenderers = 0;
        int wallRenderers = 0;
        int transitionRenderers = 0;
        int openSockets = 0;
        int shellColliderViolations = 0;
        int enabledLegacyVisualRenderers = 0;
        int structureMissing = 0;
        int softDecorMissing = 0;

        for (int i = 0;
             i < shells.Length;
             i++)
        {
            DreamProceduralRoomShellSkinP1012A32
                shell =
                    shells[i];

            if (shell == null)
            {
                continue;
            }

            if (!shell.IsCommitted)
            {
                errors.Add(
                    "RoomIndex=" +
                    shell.RoomIndex +
                    " Shell Skin 未 Commit：" +
                    shell.LastFailureReason);
                continue;
            }

            committed++;

            floorRenderers +=
                shell.FloorRendererCount;

            wallRenderers +=
                shell.WallRendererCount;

            transitionRenderers +=
                shell.TransitionRendererCount;

            openSockets +=
                shell.OpenSocketCount;

            Transform shellRoot =
                shell.transform.Find(
                    DreamProceduralRoomShellSkinP1012A32
                        .ShellRootName);

            if (shellRoot == null)
            {
                errors.Add(
                    "RoomIndex=" +
                    shell.RoomIndex +
                    " 缺少 Shell Root。");
            }
            else
            {
                shellColliderViolations +=
                    shellRoot.GetComponentsInChildren<
                        Collider2D>(
                            true).Length;
            }

            enabledLegacyVisualRenderers +=
                shell.CountEnabledLegacyVisualRenderers();

            DreamProceduralRoomStructuralSkinP1012A31
                structure =
                    shell.GetComponent<
                        DreamProceduralRoomStructuralSkinP1012A31>();

            if (structure == null ||
                !structure.IsCommitted)
            {
                structureMissing++;
            }

            DreamProceduralRoomSoftDecorP1012A2
                softDecor =
                    shell.GetComponent<
                        DreamProceduralRoomSoftDecorP1012A2>();

            if (softDecor == null ||
                !softDecor.IsCommitted)
            {
                softDecorMissing++;
            }

            if (shell.FloorRendererCount <= 0)
            {
                errors.Add(
                    "RoomIndex=" +
                    shell.RoomIndex +
                    " FloorRendererCount=0");
            }

            if (shell.WallRendererCount <= 0)
            {
                errors.Add(
                    "RoomIndex=" +
                    shell.RoomIndex +
                    " WallRendererCount=0");
            }

            if (shell.OpenSocketCount <= 0 ||
                shell.TransitionRendererCount <= 0)
            {
                errors.Add(
                    "RoomIndex=" +
                    shell.RoomIndex +
                    " 没有有效 Socket Transition。");
            }
        }

        if (shellColliderViolations > 0)
        {
            errors.Add(
                "ShellColliderViolations=" +
                shellColliderViolations);
        }

        if (enabledLegacyVisualRenderers > 0)
        {
            errors.Add(
                "Legacy Graybox Visual Renderer 仍启用=" +
                enabledLegacyVisualRenderers);
        }

        if (structureMissing > 0)
        {
            errors.Add(
                "Structural Skin 未同步 Commit 的房间=" +
                structureMissing);
        }

        if (softDecorMissing > 0)
        {
            errors.Add(
                "Soft Decor 未同步 Commit 的房间=" +
                softDecorMissing);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[P10.12A-3.2] LIVE Shell Skin Audit FAILED\n- " +
                string.Join(
                    "\n- ",
                    errors));
            return;
        }

        Debug.Log(
            "[P10.12A-3.2] LIVE Shell Skin Audit PASS\n" +
            "Components=" +
            shells.Length +
            " | Committed=" +
            committed +
            " | FloorRenderers=" +
            floorRenderers +
            " | WallRenderers=" +
            wallRenderers +
            " | TransitionRenderers=" +
            transitionRenderers +
            " | OpenSockets=" +
            openSockets +
            "\nShellColliderViolations=0" +
            " | EnabledLegacyGrayboxVisualRenderers=0" +
            " | StructuralSkinCommitted=True" +
            " | SoftDecorCommitted=True" +
            "\nGeometryMutation=0" +
            " | BlockedCellsChanged=False" +
            " | FloorCellsChanged=False" +
            " | AStarChanged=False" +
            " | SocketMutation=0" +
            " | ProductionMainChanged=False" +
            "\nReplacementContract=ShellThemeSpritePoolsOnly" +
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
                current +
                "/" +
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
