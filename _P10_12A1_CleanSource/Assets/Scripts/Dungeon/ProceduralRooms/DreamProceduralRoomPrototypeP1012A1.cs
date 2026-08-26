using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-1.1
/// 仅用于 Scene 中预览 A-1 的随机结构；不在 Awake/Start 修改 Runtime。
///
/// 注意：这个 MonoBehaviour 必须放在同名 .cs 文件中，
/// 否则 Unity 虽然能够编译类型，但 Prefab 保存/重载时可能无法稳定序列化该组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamProceduralRoomPrototypeP1012A1 : MonoBehaviour
{
    [SerializeField] private int previewSeed = 12345;
    [SerializeField] private bool useNorth = true;
    [SerializeField] private bool useEast = true;
    [SerializeField] private bool useSouth = true;
    [SerializeField] private bool useWest = true;
    [SerializeField] private bool drawBlockedCells = true;
    [SerializeField] private bool drawReservedMainRoute = true;

    public int PreviewSeed => previewSeed;

    public void ConfigurePreviewSeed(int value)
    {
        previewSeed = value;
    }

    public bool TryBuildPreview(
        out DreamProceduralRoomLayout layout,
        out string failureReason)
    {
        DreamRoomTemplate template = GetComponent<DreamRoomTemplate>();
        if (template == null)
        {
            layout = null;
            failureReason = "根节点缺少 DreamRoomTemplate。";
            return false;
        }

        if (template.SizeInCells != DreamProceduralRoomKernelP1012A1.MediumSize)
        {
            layout = null;
            failureReason = "Prototype 必须为 13x9。";
            return false;
        }

        List<DreamProceduralDoorLane> lanes = new List<DreamProceduralDoorLane>();
        IReadOnlyList<DreamRoomDoorSocket> sockets = template.DoorSockets;

        if (sockets != null)
        {
            for (int i = 0; i < sockets.Count; i++)
            {
                DreamRoomDoorSocket socket = sockets[i];
                if (socket == null || !ShouldUse(socket.Direction))
                    continue;

                List<Vector2Int> cells = socket.GetLocalInsideCells();
                if (cells.Count == 2)
                {
                    lanes.Add(new DreamProceduralDoorLane(socket.Direction, cells));
                }
            }
        }

        return DreamProceduralRoomKernelP1012A1.TryGenerate(
            previewSeed,
            lanes,
            out layout,
            out failureReason);
    }

    private bool ShouldUse(DreamRoomDoorDirection direction)
    {
        switch (direction)
        {
            case DreamRoomDoorDirection.North: return useNorth;
            case DreamRoomDoorDirection.East: return useEast;
            case DreamRoomDoorDirection.South: return useSouth;
            case DreamRoomDoorDirection.West: return useWest;
            default: return false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        DreamProceduralRoomLayout layout;
        string failure;

        if (!TryBuildPreview(out layout, out failure) || layout == null)
            return;

        DreamRoomTemplate template = GetComponent<DreamRoomTemplate>();
        if (template == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (drawReservedMainRoute)
        {
            Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.20f);
            foreach (Vector2Int cell in layout.ReservedMainRouteCells)
            {
                Gizmos.DrawCube(
                    template.GetLocalCellCenter(cell),
                    new Vector3(0.88f, 0.88f, 0.02f));
            }
        }

        if (drawBlockedCells)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.42f);
            foreach (Vector2Int cell in layout.BlockedCells)
            {
                Gizmos.DrawCube(
                    template.GetLocalCellCenter(cell),
                    new Vector3(0.86f, 0.86f, 0.04f));
            }
        }

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}
