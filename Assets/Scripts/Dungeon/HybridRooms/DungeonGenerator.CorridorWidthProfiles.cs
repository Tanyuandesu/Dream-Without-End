using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在 R6 已求得的双格安全中心线上应用可切换的宽度表现。
///
/// Mixed1And2 不改写房间图、Socket 选择或 A* 中心线：
/// 1. 主路径保持双格宽，避免主要战斗通路形成结构性堵塞。
/// 2. 支路的门口、转角、交叉口保持双格宽。
/// 3. 只有足够长的支路直线中段收窄为一格。
/// 4. 混合结果必须是原双格安全包络的子集。
///
/// 因此 EnemyPathService 仍读取最终 FloorCells 与合法门边；
/// 本类不会复制或替换敌人 A*。
/// </summary>
public sealed partial class DungeonGenerator
{
    [Header("Corridor Pass C1：一／两格混合宽度")]
    [Tooltip(
        "Uniform2 保留 R6～R9.4 已封板基线；" +
        "Mixed1And2 只收窄支路直线中段。")]
    [SerializeField]
    private DungeonCorridorWidthMode socketCorridorWidthMode =
        DungeonCorridorWidthMode.Uniform2;

    [Tooltip(
        "Mixed1And2 下，门外从两端各保留多少个双格中心线节点。")]
    [Min(1)]
    [SerializeField]
    private int mixedCorridorDoorApronLength = 2;

    [Tooltip(
        "Mixed1And2 下，转角前后各保留多少个双格中心线节点。")]
    [Min(0)]
    [SerializeField]
    private int mixedCorridorCornerRadius = 1;

    [Tooltip(
        "Mixed1And2 下，交叉／汇合点前后各保留多少个双格中心线节点。")]
    [Min(0)]
    [SerializeField]
    private int mixedCorridorJunctionRadius = 1;

    [Tooltip(
        "连续一格宽中心线少于此长度时，自动恢复为双格，" +
        "避免产生短促的视觉锯齿和单格陷阱。")]
    [Min(1)]
    [SerializeField]
    private int mixedCorridorMinimumNarrowRunLength = 3;

    [Tooltip(
        "保持 Start 到 Exit 的最短房间图路径为双格宽。" +
        "建议开启，避免主干道成为长期单列堵点。")]
    [SerializeField]
    private bool mixedCorridorKeepPrimaryRouteWide = true;

    [Header("Corridor Pass C2：一／二／三格层次宽度")]
    [Tooltip(
        "C2 只在通过房间占格与地图边界校验的节点加入三格开阔段；" +
        "门口仍保持两格，失败节点会局部回退为两格。")]
    [Range(0.05f, 0.4f)]
    [SerializeField]
    private float layeredCorridorOpenFraction = 0.20f;

    [Tooltip("一段直线至少有多少个候选节点时，才加入三格开阔段。")]
    [Min(2)]
    [SerializeField]
    private int layeredCorridorMinimumOpenRunLength = 3;

    [Tooltip("单个三格开阔段最多使用多少个中心线节点。")]
    [Min(2)]
    [SerializeField]
    private int layeredCorridorMaximumOpenRunLength = 4;

    [Tooltip(
        "在既有两格门前区与三格开阔段之间额外保留多少个两格过渡节点。")]
    [Min(1)]
    [SerializeField]
    private int layeredCorridorDoorTransitionLength = 1;

    [Tooltip("允许 Start 到 Exit 主路径的长直段出现短三格开阔区。")]
    [SerializeField]
    private bool layeredCorridorOpenPrimaryRoute = true;

    [Tooltip(
        "允许连接中心线的交汇节点尝试扩为三格；空间不足时自动保持两格。")]
    [SerializeField]
    private bool layeredCorridorOpenJunctions = true;

    public DungeonCorridorWidthMode SocketCorridorWidthMode =>
        socketCorridorWidthMode;

    public int MixedCorridorDoorApronLength =>
        mixedCorridorDoorApronLength;

    public int MixedCorridorCornerRadius =>
        mixedCorridorCornerRadius;

    public int MixedCorridorJunctionRadius =>
        mixedCorridorJunctionRadius;

    public int MixedCorridorMinimumNarrowRunLength =>
        mixedCorridorMinimumNarrowRunLength;

    public bool MixedCorridorKeepPrimaryRouteWide =>
        mixedCorridorKeepPrimaryRouteWide;

    public float LayeredCorridorOpenFraction =>
        layeredCorridorOpenFraction;

    public int LayeredCorridorMinimumOpenRunLength =>
        layeredCorridorMinimumOpenRunLength;

    public int LayeredCorridorMaximumOpenRunLength =>
        layeredCorridorMaximumOpenRunLength;

    public int LayeredCorridorDoorTransitionLength =>
        layeredCorridorDoorTransitionLength;

    public bool LayeredCorridorOpenPrimaryRoute =>
        layeredCorridorOpenPrimaryRoute;

    public bool LayeredCorridorOpenJunctions =>
        layeredCorridorOpenJunctions;

    private bool R6TryApplyCorridorWidthProfile(
        DungeonLayout graphLayout,
        Dictionary<int, List<Vector2Int>> routedCenterlines,
        HashSet<Vector2Int> occupiedRoomCells,
        HashSet<Vector2Int> allCorridorCells,
        R6RoutingStatistics statistics,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (socketCorridorWidthMode ==
            DungeonCorridorWidthMode.Uniform2)
        {
            statistics.WidthMode =
                DungeonCorridorWidthMode.Uniform2;
            return true;
        }

        if (socketCorridorWidthMode ==
            DungeonCorridorWidthMode.Mixed1To3)
        {
            return R6TryApplyLayeredCorridorWidthProfile(
                graphLayout,
                routedCenterlines,
                occupiedRoomCells,
                allCorridorCells,
                statistics,
                out failureReason);
        }

        if (socketCorridorWidthMode !=
            DungeonCorridorWidthMode.Mixed1And2)
        {
            failureReason =
                "未知 Corridor Width Mode：" +
                socketCorridorWidthMode + "。";
            return false;
        }

        if (graphLayout == null ||
            routedCenterlines == null ||
            routedCenterlines.Count !=
                graphLayout.Connections.Count)
        {
            failureReason =
                "Mixed1And2 缺少完整的 Connection 中心线。";
            return false;
        }

        HashSet<int> primaryConnections;

        if (!R6TryFindPrimaryRouteConnections(
                graphLayout,
                out primaryConnections,
                out failureReason))
        {
            return false;
        }

        HashSet<Vector2Int> junctionCells =
            R6CollectCenterlineJunctionCells(
                routedCenterlines);

        Dictionary<int, List<Vector2Int>>
            mixedCellsByConnection =
                new Dictionary<int, List<Vector2Int>>();

        Dictionary<int, int> wideCenterlineCounts =
            new Dictionary<int, int>();

        Dictionary<int, int> narrowCenterlineCounts =
            new Dictionary<int, int>();

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            List<Vector2Int> centerline;

            if (!routedCenterlines.TryGetValue(
                    connectionIndex,
                    out centerline) ||
                centerline == null ||
                centerline.Count == 0)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 没有可应用宽度 Profile 的中心线。";
                return false;
            }

            bool keepWholeConnectionWide =
                mixedCorridorKeepPrimaryRouteWide &&
                primaryConnections.Contains(
                    connectionIndex);

            bool[] wideMask =
                R6BuildMixedWideMask(
                    centerline,
                    junctionCells,
                    keepWholeConnectionWide);

            List<Vector2Int> mixedCells =
                new List<Vector2Int>();

            R6ExpandCenterlineWithWidthMask(
                centerline,
                wideMask,
                mixedCells);

            DreamRoomConnection connection =
                graphLayout.Connections[connectionIndex];

            HashSet<Vector2Int> uniformEnvelope =
                new HashSet<Vector2Int>(
                    connection.CorridorCells);

            for (int cellIndex = 0;
                 cellIndex < mixedCells.Count;
                 cellIndex++)
            {
                Vector2Int cell = mixedCells[cellIndex];

                if (!uniformEnvelope.Contains(cell))
                {
                    failureReason =
                        "Connection " + connectionIndex +
                        " 的 Mixed Cell " + cell +
                        " 超出原双格安全包络。";
                    return false;
                }

                if (!R6IsInsideMap(cell) ||
                    occupiedRoomCells.Contains(cell))
                {
                    failureReason =
                        "Connection " + connectionIndex +
                        " 的 Mixed Cell " + cell +
                        " 越界或穿入房间。";
                    return false;
                }
            }

            HashSet<Vector2Int> uniqueMixed =
                new HashSet<Vector2Int>(mixedCells);

            if (uniqueMixed.Count == 0 ||
                R6CountReachableCells(
                    uniqueMixed,
                    R6GetFirstCell(uniqueMixed)) !=
                uniqueMixed.Count)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 的 Mixed Cells 不是四方向连续区域。";
                return false;
            }

            int wideCount = 0;

            for (int maskIndex = 0;
                 maskIndex < wideMask.Length;
                 maskIndex++)
            {
                if (wideMask[maskIndex])
                {
                    wideCount++;
                }
            }

            mixedCellsByConnection.Add(
                connectionIndex,
                mixedCells);

            wideCenterlineCounts.Add(
                connectionIndex,
                wideCount);

            narrowCenterlineCounts.Add(
                connectionIndex,
                centerline.Count - wideCount);
        }

        allCorridorCells.Clear();
        statistics.ReusedCorridorCells = 0;
        statistics.WideCenterlineCells = 0;
        statistics.NarrowCenterlineCells = 0;
        statistics.PrimaryWideConnections = 0;
        statistics.MixedConnections = 0;

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                graphLayout.Connections[connectionIndex];

            List<Vector2Int> mixedCells =
                mixedCellsByConnection[connectionIndex];

            connection.SetCorridorCells(mixedCells);

            int reused = 0;

            for (int cellIndex = 0;
                 cellIndex < mixedCells.Count;
                 cellIndex++)
            {
                if (!allCorridorCells.Add(
                        mixedCells[cellIndex]))
                {
                    reused++;
                }
            }

            statistics.ReusedCorridorCells += reused;
            statistics.WideCenterlineCells +=
                wideCenterlineCounts[connectionIndex];
            statistics.NarrowCenterlineCells +=
                narrowCenterlineCounts[connectionIndex];

            if (primaryConnections.Contains(
                    connectionIndex) &&
                mixedCorridorKeepPrimaryRouteWide)
            {
                statistics.PrimaryWideConnections++;
            }

            if (narrowCenterlineCounts[connectionIndex] > 0)
            {
                statistics.MixedConnections++;
            }

            R6ReplaceExpandedCellCount(
                statistics,
                connectionIndex,
                mixedCells.Count);
        }

        statistics.WidthMode =
            DungeonCorridorWidthMode.Mixed1And2;
        statistics.JunctionCellCount =
            junctionCells.Count;

        return true;
    }

    /// <summary>
    /// C2 在 C1 的 1／2 格结果上加入经过逐节点空间验证的三格开阔段。
    /// A* 仍以两格安全包络求中心线；这里只进行确定性的后处理。
    /// </summary>
    private bool R6TryApplyLayeredCorridorWidthProfile(
        DungeonLayout graphLayout,
        Dictionary<int, List<Vector2Int>> routedCenterlines,
        HashSet<Vector2Int> occupiedRoomCells,
        HashSet<Vector2Int> allCorridorCells,
        R6RoutingStatistics statistics,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (socketCorridorWidth != 2)
        {
            failureReason =
                "Mixed1To3 要求 A* 安全包络与门口基准宽度为 2；" +
                "当前 socketCorridorWidth=" +
                socketCorridorWidth + "。";
            return false;
        }

        if (graphLayout == null ||
            routedCenterlines == null ||
            routedCenterlines.Count !=
                graphLayout.Connections.Count)
        {
            failureReason =
                "Mixed1To3 缺少完整的 Connection 中心线。";
            return false;
        }

        HashSet<int> primaryConnections;

        if (!R6TryFindPrimaryRouteConnections(
                graphLayout,
                out primaryConnections,
                out failureReason))
        {
            return false;
        }

        HashSet<Vector2Int> junctionCells =
            R6CollectCenterlineJunctionCells(
                routedCenterlines);

        Dictionary<int, List<Vector2Int>>
            layeredCellsByConnection =
                new Dictionary<int, List<Vector2Int>>();

        Dictionary<int, int[]> widthsByConnection =
            new Dictionary<int, int[]>();

        int openCandidateCount = 0;
        int acceptedOpenCount = 0;
        int fallbackOpenCount = 0;

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            List<Vector2Int> centerline;

            if (!routedCenterlines.TryGetValue(
                    connectionIndex,
                    out centerline) ||
                centerline == null ||
                centerline.Count == 0)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 没有可应用 Mixed1To3 的中心线。";
                return false;
            }

            bool isPrimaryConnection =
                primaryConnections.Contains(
                    connectionIndex);

            bool keepWholeConnectionWide =
                mixedCorridorKeepPrimaryRouteWide &&
                isPrimaryConnection;

            bool[] c1WideMask =
                R6BuildMixedWideMask(
                    centerline,
                    junctionCells,
                    keepWholeConnectionWide);

            int[] widths = new int[centerline.Count];

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = c1WideMask[i] ? 2 : 1;
            }

            bool[] doorProtected =
                R6BuildLayeredDoorProtectedMask(
                    centerline.Count);

            List<int> openCandidates =
                R6CollectLayeredOpenCandidates(
                    centerline,
                    widths,
                    doorProtected,
                    junctionCells,
                    isPrimaryConnection);

            openCandidateCount += openCandidates.Count;

            List<Vector2Int> acceptedCells =
                new List<Vector2Int>();

            R6ExpandCenterlineWithWidthValues(
                centerline,
                widths,
                acceptedCells);

            if (!R6AreLayeredCellsUsable(
                    acceptedCells,
                    occupiedRoomCells))
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 的 C1 基础宽度已经越界或穿入房间。";
                return false;
            }

            HashSet<Vector2Int> acceptedSet =
                new HashSet<Vector2Int>(acceptedCells);

            for (int candidateIndex = 0;
                 candidateIndex < openCandidates.Count;
                 candidateIndex++)
            {
                int centerlineIndex =
                    openCandidates[candidateIndex];

                if (!R6CanPromoteLayeredNode(
                        widths,
                        centerlineIndex))
                {
                    fallbackOpenCount++;
                    continue;
                }

                widths[centerlineIndex] = 3;

                List<Vector2Int> trialCells =
                    new List<Vector2Int>();

                R6ExpandCenterlineWithWidthValues(
                    centerline,
                    widths,
                    trialCells);

                HashSet<Vector2Int> trialSet =
                    new HashSet<Vector2Int>(trialCells);

                bool addsVisibleArea =
                    R6ContainsCellOutside(
                        trialSet,
                        acceptedSet);

                bool usable =
                    addsVisibleArea &&
                    R6AreLayeredCellsUsable(
                        trialCells,
                        occupiedRoomCells) &&
                    trialSet.Count > 0 &&
                    R6CountReachableCells(
                        trialSet,
                        R6GetFirstCell(trialSet)) ==
                    trialSet.Count;

                if (!usable)
                {
                    widths[centerlineIndex] = 2;
                    fallbackOpenCount++;
                    continue;
                }

                acceptedCells = trialCells;
                acceptedSet = trialSet;
                acceptedOpenCount++;
            }

            if (acceptedSet.Count == 0 ||
                R6CountReachableCells(
                    acceptedSet,
                    R6GetFirstCell(acceptedSet)) !=
                acceptedSet.Count)
            {
                failureReason =
                    "Connection " + connectionIndex +
                    " 的 Mixed1To3 Cells 不是四方向连续区域。";
                return false;
            }

            layeredCellsByConnection.Add(
                connectionIndex,
                acceptedCells);

            widthsByConnection.Add(
                connectionIndex,
                widths);
        }

        allCorridorCells.Clear();
        statistics.ReusedCorridorCells = 0;
        statistics.PrimaryWideConnections = 0;
        statistics.MixedConnections = 0;
        statistics.WideCenterlineCells = 0;
        statistics.NarrowCenterlineCells = 0;
        statistics.OpenCenterlineCells = 0;
        statistics.OpenConnections = 0;

        for (int connectionIndex = 0;
             connectionIndex < graphLayout.Connections.Count;
             connectionIndex++)
        {
            DreamRoomConnection connection =
                graphLayout.Connections[connectionIndex];

            List<Vector2Int> layeredCells =
                layeredCellsByConnection[
                    connectionIndex];

            int[] widths =
                widthsByConnection[connectionIndex];

            connection.SetCorridorCells(layeredCells);

            int reused = 0;
            int narrowCount = 0;
            int regularCount = 0;
            int openCount = 0;

            for (int cellIndex = 0;
                 cellIndex < layeredCells.Count;
                 cellIndex++)
            {
                if (!allCorridorCells.Add(
                        layeredCells[cellIndex]))
                {
                    reused++;
                }
            }

            for (int widthIndex = 0;
                 widthIndex < widths.Length;
                 widthIndex++)
            {
                switch (widths[widthIndex])
                {
                    case 1:
                        narrowCount++;
                        break;
                    case 3:
                        openCount++;
                        break;
                    default:
                        regularCount++;
                        break;
                }
            }

            statistics.ReusedCorridorCells += reused;
            statistics.NarrowCenterlineCells +=
                narrowCount;
            statistics.WideCenterlineCells +=
                regularCount;
            statistics.OpenCenterlineCells +=
                openCount;

            if (primaryConnections.Contains(
                    connectionIndex))
            {
                statistics.PrimaryWideConnections++;
            }

            if (narrowCount > 0 || openCount > 0)
            {
                statistics.MixedConnections++;
            }

            if (openCount > 0)
            {
                statistics.OpenConnections++;
            }

            R6ReplaceExpandedCellCount(
                statistics,
                connectionIndex,
                layeredCells.Count);
        }

        statistics.WidthMode =
            DungeonCorridorWidthMode.Mixed1To3;
        statistics.JunctionCellCount =
            junctionCells.Count;
        statistics.OpenCandidateCount =
            openCandidateCount;
        statistics.AcceptedOpenCount =
            acceptedOpenCount;
        statistics.FallbackOpenCount =
            fallbackOpenCount;

        return true;
    }

    private bool[] R6BuildLayeredDoorProtectedMask(
        int centerlineCount)
    {
        bool[] protectedMask =
            new bool[centerlineCount];

        int protectedLength = Mathf.Clamp(
            mixedCorridorDoorApronLength +
            layeredCorridorDoorTransitionLength,
            1,
            centerlineCount);

        for (int i = 0; i < protectedLength; i++)
        {
            protectedMask[i] = true;
            protectedMask[centerlineCount - 1 - i] = true;
        }

        return protectedMask;
    }

    private List<int> R6CollectLayeredOpenCandidates(
        List<Vector2Int> centerline,
        int[] widths,
        bool[] doorProtected,
        HashSet<Vector2Int> junctionCells,
        bool isPrimaryConnection)
    {
        HashSet<int> candidates = new HashSet<int>();

        if (layeredCorridorOpenJunctions)
        {
            for (int i = 1;
                 i < centerline.Count - 1;
                 i++)
            {
                if (!doorProtected[i] &&
                    widths[i] == 2 &&
                    junctionCells.Contains(
                        centerline[i]) &&
                    R6CanPromoteLayeredNode(
                        widths,
                        i))
                {
                    candidates.Add(i);
                }
            }
        }

        if (layeredCorridorOpenPrimaryRoute &&
            isPrimaryConnection)
        {
            int index = 1;

            while (index < centerline.Count - 1)
            {
                if (!R6IsLayeredStraightCandidate(
                        centerline,
                        widths,
                        doorProtected,
                        junctionCells,
                        index))
                {
                    index++;
                    continue;
                }

                int first = index;

                while (index < centerline.Count - 1 &&
                       R6IsLayeredStraightCandidate(
                           centerline,
                           widths,
                           doorProtected,
                           junctionCells,
                           index))
                {
                    index++;
                }

                int runLength = index - first;

                if (runLength <
                    layeredCorridorMinimumOpenRunLength)
                {
                    continue;
                }

                int desiredLength = Mathf.Clamp(
                    Mathf.RoundToInt(
                        runLength *
                        layeredCorridorOpenFraction),
                    layeredCorridorMinimumOpenRunLength,
                    layeredCorridorMaximumOpenRunLength);

                desiredLength = Mathf.Min(
                    desiredLength,
                    runLength);

                int openFirst =
                    first +
                    (runLength - desiredLength) / 2;

                for (int openIndex = openFirst;
                     openIndex <
                        openFirst + desiredLength;
                     openIndex++)
                {
                    candidates.Add(openIndex);
                }
            }
        }

        List<int> ordered = new List<int>(candidates);
        ordered.Sort();
        return ordered;
    }

    private static bool R6IsLayeredStraightCandidate(
        List<Vector2Int> centerline,
        int[] widths,
        bool[] doorProtected,
        HashSet<Vector2Int> junctionCells,
        int index)
    {
        if (index < 2 ||
            index >= centerline.Count - 2 ||
            doorProtected[index] ||
            widths[index] != 2 ||
            junctionCells.Contains(centerline[index]))
        {
            return false;
        }

        Vector2Int direction =
            centerline[index] -
            centerline[index - 1];

        return
            centerline[index - 1] -
                centerline[index - 2] == direction &&
            centerline[index + 1] -
                centerline[index] == direction &&
            centerline[index + 2] -
                centerline[index + 1] == direction;
    }

    private static bool R6CanPromoteLayeredNode(
        int[] widths,
        int index)
    {
        return index > 0 &&
               index < widths.Length - 1 &&
               widths[index] == 2 &&
               widths[index - 1] >= 2 &&
               widths[index + 1] >= 2;
    }

    private void R6ExpandCenterlineWithWidthValues(
        List<Vector2Int> centerline,
        int[] widths,
        List<Vector2Int> results)
    {
        results.Clear();

        HashSet<Vector2Int> used =
            new HashSet<Vector2Int>();

        if (centerline.Count == 1)
        {
            R6CollectWidthCells(
                centerline[0],
                Vector2Int.right,
                Mathf.Clamp(widths[0], 1, 3),
                results,
                used);
            return;
        }

        for (int segmentIndex = 0;
             segmentIndex < centerline.Count - 1;
             segmentIndex++)
        {
            Vector2Int direction =
                centerline[segmentIndex + 1] -
                centerline[segmentIndex];

            int width = Mathf.Clamp(
                Mathf.Max(
                    widths[segmentIndex],
                    widths[segmentIndex + 1]),
                1,
                3);

            R6CollectWidthCells(
                centerline[segmentIndex],
                direction,
                width,
                results,
                used);

            R6CollectWidthCells(
                centerline[segmentIndex + 1],
                direction,
                width,
                results,
                used);
        }

        for (int cellIndex = 1;
             cellIndex < centerline.Count - 1;
             cellIndex++)
        {
            Vector2Int incoming =
                centerline[cellIndex] -
                centerline[cellIndex - 1];

            Vector2Int outgoing =
                centerline[cellIndex + 1] -
                centerline[cellIndex];

            if (incoming == outgoing)
            {
                continue;
            }

            R6CollectCornerCellsWithWidth(
                centerline[cellIndex],
                incoming,
                outgoing,
                Mathf.Clamp(widths[cellIndex], 1, 3),
                results,
                used);
        }
    }

    private bool R6AreLayeredCellsUsable(
        List<Vector2Int> cells,
        HashSet<Vector2Int> occupiedRoomCells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (!R6IsInsideMap(cells[i]) ||
                occupiedRoomCells.Contains(cells[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool R6ContainsCellOutside(
        HashSet<Vector2Int> candidate,
        HashSet<Vector2Int> baseline)
    {
        foreach (Vector2Int cell in candidate)
        {
            if (!baseline.Contains(cell))
            {
                return true;
            }
        }

        return false;
    }

    private bool[] R6BuildMixedWideMask(
        List<Vector2Int> centerline,
        HashSet<Vector2Int> junctionCells,
        bool keepWholeConnectionWide)
    {
        bool[] wide = new bool[centerline.Count];

        if (keepWholeConnectionWide)
        {
            for (int i = 0; i < wide.Length; i++)
            {
                wide[i] = true;
            }

            return wide;
        }

        int apronLength = Mathf.Clamp(
            mixedCorridorDoorApronLength,
            1,
            centerline.Count);

        for (int i = 0; i < apronLength; i++)
        {
            wide[i] = true;
            wide[centerline.Count - 1 - i] = true;
        }

        for (int i = 1; i < centerline.Count - 1; i++)
        {
            Vector2Int incoming =
                centerline[i] - centerline[i - 1];

            Vector2Int outgoing =
                centerline[i + 1] - centerline[i];

            if (incoming != outgoing)
            {
                R6MarkWideRadius(
                    wide,
                    i,
                    mixedCorridorCornerRadius);
            }

            if (junctionCells.Contains(centerline[i]))
            {
                R6MarkWideRadius(
                    wide,
                    i,
                    mixedCorridorJunctionRadius);
            }
        }

        if (centerline.Count > 0 &&
            junctionCells.Contains(centerline[0]))
        {
            R6MarkWideRadius(
                wide,
                0,
                mixedCorridorJunctionRadius);
        }

        if (centerline.Count > 1 &&
            junctionCells.Contains(
                centerline[centerline.Count - 1]))
        {
            R6MarkWideRadius(
                wide,
                centerline.Count - 1,
                mixedCorridorJunctionRadius);
        }

        R6PromoteShortNarrowRuns(wide);
        return wide;
    }

    private static void R6MarkWideRadius(
        bool[] wide,
        int centerIndex,
        int radius)
    {
        int first = Mathf.Max(0, centerIndex - radius);
        int last = Mathf.Min(
            wide.Length - 1,
            centerIndex + radius);

        for (int i = first; i <= last; i++)
        {
            wide[i] = true;
        }
    }

    private void R6PromoteShortNarrowRuns(bool[] wide)
    {
        int index = 0;

        while (index < wide.Length)
        {
            if (wide[index])
            {
                index++;
                continue;
            }

            int first = index;

            while (index < wide.Length && !wide[index])
            {
                index++;
            }

            int length = index - first;

            if (length >=
                mixedCorridorMinimumNarrowRunLength)
            {
                continue;
            }

            for (int promoteIndex = first;
                 promoteIndex < index;
                 promoteIndex++)
            {
                wide[promoteIndex] = true;
            }
        }
    }

    private void R6ExpandCenterlineWithWidthMask(
        List<Vector2Int> centerline,
        bool[] wideMask,
        List<Vector2Int> results)
    {
        results.Clear();

        HashSet<Vector2Int> used =
            new HashSet<Vector2Int>();

        if (centerline.Count == 1)
        {
            R6CollectWidthCells(
                centerline[0],
                Vector2Int.right,
                socketCorridorWidth,
                results,
                used);
            return;
        }

        for (int segmentIndex = 0;
             segmentIndex < centerline.Count - 1;
             segmentIndex++)
        {
            Vector2Int direction =
                centerline[segmentIndex + 1] -
                centerline[segmentIndex];

            int width =
                wideMask[segmentIndex] ||
                wideMask[segmentIndex + 1]
                    ? socketCorridorWidth
                    : 1;

            R6CollectWidthCells(
                centerline[segmentIndex],
                direction,
                width,
                results,
                used);

            R6CollectWidthCells(
                centerline[segmentIndex + 1],
                direction,
                width,
                results,
                used);
        }

        for (int cellIndex = 1;
             cellIndex < centerline.Count - 1;
             cellIndex++)
        {
            Vector2Int incoming =
                centerline[cellIndex] -
                centerline[cellIndex - 1];

            Vector2Int outgoing =
                centerline[cellIndex + 1] -
                centerline[cellIndex];

            if (incoming == outgoing)
            {
                continue;
            }

            int width = wideMask[cellIndex]
                ? socketCorridorWidth
                : 1;

            R6CollectCornerCellsWithWidth(
                centerline[cellIndex],
                incoming,
                outgoing,
                width,
                results,
                used);
        }
    }

    private static void R6CollectCornerCellsWithWidth(
        Vector2Int anchor,
        Vector2Int incomingDirection,
        Vector2Int outgoingDirection,
        int width,
        List<Vector2Int> results,
        HashSet<Vector2Int> used)
    {
        Vector2Int incomingSideways =
            incomingDirection.x != 0
                ? Vector2Int.up
                : Vector2Int.right;

        Vector2Int outgoingSideways =
            outgoingDirection.x != 0
                ? Vector2Int.up
                : Vector2Int.right;

        int startOffset = -(width / 2);

        for (int first = 0; first < width; first++)
        {
            for (int second = 0;
                 second < width;
                 second++)
            {
                R6AddUniqueCell(
                    anchor +
                    incomingSideways *
                        (startOffset + first) +
                    outgoingSideways *
                        (startOffset + second),
                    results,
                    used);
            }
        }
    }

    private static HashSet<Vector2Int>
        R6CollectCenterlineJunctionCells(
            Dictionary<int, List<Vector2Int>>
                routedCenterlines)
    {
        Dictionary<Vector2Int, int> useCount =
            new Dictionary<Vector2Int, int>();

        HashSet<Vector2Int> allCenterlineCells =
            new HashSet<Vector2Int>();

        foreach (KeyValuePair<int, List<Vector2Int>> pair
                 in routedCenterlines)
        {
            HashSet<Vector2Int> usedByConnection =
                new HashSet<Vector2Int>();

            List<Vector2Int> centerline = pair.Value;

            for (int i = 0; i < centerline.Count; i++)
            {
                Vector2Int cell = centerline[i];
                allCenterlineCells.Add(cell);

                if (!usedByConnection.Add(cell))
                {
                    continue;
                }

                int existing;
                useCount.TryGetValue(cell, out existing);
                useCount[cell] = existing + 1;
            }
        }

        HashSet<Vector2Int> junctionCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int cell in allCenterlineCells)
        {
            int owners;

            if (useCount.TryGetValue(cell, out owners) &&
                owners > 1)
            {
                junctionCells.Add(cell);
                continue;
            }

            int neighbourCount = 0;

            for (int directionIndex = 0;
                 directionIndex <
                    R6CardinalDirections.Length;
                 directionIndex++)
            {
                if (allCenterlineCells.Contains(
                        cell +
                        R6CardinalDirections[
                            directionIndex]))
                {
                    neighbourCount++;
                }
            }

            if (neighbourCount >= 3)
            {
                junctionCells.Add(cell);
            }
        }

        return junctionCells;
    }

    private static bool R6TryFindPrimaryRouteConnections(
        DungeonLayout layout,
        out HashSet<int> primaryConnections,
        out string failureReason)
    {
        primaryConnections = new HashSet<int>();
        failureReason = string.Empty;

        int startRoom = R6FindRoomContainingCell(
            layout.RoomPlacements,
            layout.StartCell);

        int exitRoom = R6FindRoomContainingCell(
            layout.RoomPlacements,
            layout.ExitCell);

        if (startRoom < 0 || exitRoom < 0)
        {
            failureReason =
                "Mixed1And2 无法解析 Start／Exit 所属房间。";
            return false;
        }

        if (startRoom == exitRoom)
        {
            return true;
        }

        int roomCount = layout.RoomPlacements.Count;
        bool[] visited = new bool[roomCount];
        int[] parentRoom = new int[roomCount];
        int[] parentConnection = new int[roomCount];

        for (int i = 0; i < roomCount; i++)
        {
            parentRoom[i] = -1;
            parentConnection[i] = -1;
        }

        Queue<int> queue = new Queue<int>();
        visited[startRoom] = true;
        queue.Enqueue(startRoom);

        while (queue.Count > 0 && !visited[exitRoom])
        {
            int currentRoom = queue.Dequeue();

            for (int connectionIndex = 0;
                 connectionIndex < layout.Connections.Count;
                 connectionIndex++)
            {
                DreamRoomConnection connection =
                    layout.Connections[connectionIndex];

                int otherRoom;

                if (connection == null ||
                    !connection.TryGetOtherRoomIndex(
                        currentRoom,
                        out otherRoom) ||
                    otherRoom < 0 ||
                    otherRoom >= roomCount ||
                    visited[otherRoom])
                {
                    continue;
                }

                visited[otherRoom] = true;
                parentRoom[otherRoom] = currentRoom;
                parentConnection[otherRoom] =
                    connectionIndex;
                queue.Enqueue(otherRoom);
            }
        }

        if (!visited[exitRoom])
        {
            failureReason =
                "Mixed1And2 的房间图中 Start 无法到达 Exit。";
            return false;
        }

        int room = exitRoom;

        while (room != startRoom)
        {
            int connectionIndex =
                parentConnection[room];

            if (connectionIndex < 0)
            {
                failureReason =
                    "Mixed1And2 重建主路径时缺少父 Connection。";
                return false;
            }

            primaryConnections.Add(connectionIndex);
            room = parentRoom[room];
        }

        return true;
    }

    private static int R6FindRoomContainingCell(
        IReadOnlyList<DreamRoomPlacement> placements,
        Vector2Int targetCell)
    {
        List<Vector2Int> walkableCells =
            new List<Vector2Int>();

        for (int roomIndex = 0;
             roomIndex < placements.Count;
             roomIndex++)
        {
            DreamRoomPlacement placement =
                placements[roomIndex];

            if (placement == null)
            {
                continue;
            }

            placement.GetWalkableGlobalCells(
                walkableCells);

            if (walkableCells.Contains(targetCell))
            {
                return roomIndex;
            }
        }

        return -1;
    }

    private static void R6ReplaceExpandedCellCount(
        R6RoutingStatistics statistics,
        int connectionIndex,
        int expandedCellCount)
    {
        for (int summaryIndex = 0;
             summaryIndex <
                statistics.ConnectionSummaries.Count;
             summaryIndex++)
        {
            R6ConnectionRoutingSummary summary =
                statistics.ConnectionSummaries[
                    summaryIndex];

            if (summary.ConnectionIndex !=
                connectionIndex)
            {
                continue;
            }

            statistics.ConnectionSummaries[
                summaryIndex] =
                new R6ConnectionRoutingSummary(
                    summary.ConnectionIndex,
                    summary.RoomAIndex,
                    summary.RoomBIndex,
                    summary.SocketAId,
                    summary.SocketBId,
                    summary.DirectionA,
                    summary.DirectionB,
                    summary.CenterlineCellCount,
                    expandedCellCount,
                    summary.SocketPairAttempt);

            return;
        }
    }
}
