using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间 Prefab 上的一个独立门口。
///
/// 同一方向可以拥有多个 DreamRoomDoorSocket，
/// 因而不会受“每面墙只能有一扇门”的限制。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamRoomDoorSocket : MonoBehaviour
{
    [Header("身份")]
    [SerializeField]
    private string socketId = "Door_0";

    [SerializeField]
    private DreamRoomDoorDirection direction =
        DreamRoomDoorDirection.North;

    [Header("格子数据")]
    [Tooltip(
        "以房间左下角为 (0,0)，记录门内侧的基准可行走格。" +
        "门宽大于 1 时，会沿墙面方向扩展。")]
    [SerializeField]
    private Vector2Int localInsideCell = Vector2Int.zero;

    [Min(1)]
    [SerializeField]
    private int doorWidthInCells = 2;

    [Header("可选视觉对象")]
    [Tooltip(
        "未使用此门时负责封门的子物件。" +
        "没有制作封门物件时可以保持 None。")]
    [SerializeField]
    private GameObject closedBlocker;

    private bool isOpen;

    public string SocketId => socketId;
    public DreamRoomDoorDirection Direction => direction;
    public Vector2Int LocalInsideCell => localInsideCell;
    public int DoorWidthInCells => doorWidthInCells;
    public GameObject ClosedBlocker => closedBlocker;
    public bool IsOpen => isOpen;

    /// <summary>
    /// 门内基准格向外移动一格后的位置。
    /// 后续程序化走廊会从这里开始寻找路径。
    /// </summary>
    public Vector2Int LocalOutsideCell =>
        localInsideCell + direction.ToCellOffset();

    /// <summary>
    /// 返回此门在房间内部占用的全部格子。
    /// 偶数门宽采用与当前走廊算法相同的偏移方式。
    /// </summary>
    public List<Vector2Int> GetLocalInsideCells()
    {
        List<Vector2Int> cells =
            new List<Vector2Int>(doorWidthInCells);

        Vector2Int sideways =
            direction.PerpendicularCellOffset();

        int startOffset =
            -(doorWidthInCells / 2);

        for (int i = 0; i < doorWidthInCells; i++)
        {
            cells.Add(
                localInsideCell +
                sideways * (startOffset + i));
        }

        return cells;
    }

    /// <summary>
    /// 控制此门是否打开。
    /// 现在只负责开关可选的封门物件；
    /// 后续 Renderer 会在实例化房间后调用它。
    /// </summary>
    public void SetOpen(bool open)
    {
        isOpen = open;

        if (closedBlocker != null)
        {
            closedBlocker.SetActive(!open);
        }
    }

    /// <summary>
    /// 供后续灰盒房间生成工具和自定义 Inspector 使用。
    /// 普通使用者不需要在运行时调用。
    /// </summary>
    public void Configure(
        string newSocketId,
        DreamRoomDoorDirection newDirection,
        Vector2Int newLocalInsideCell,
        int newDoorWidthInCells,
        GameObject newClosedBlocker)
    {
        socketId = string.IsNullOrWhiteSpace(newSocketId)
            ? gameObject.name
            : newSocketId.Trim();

        direction = newDirection;
        localInsideCell = newLocalInsideCell;
        doorWidthInCells =
            Mathf.Max(1, newDoorWidthInCells);
        closedBlocker = newClosedBlocker;
    }

    [ContextMenu("Preview/Open Door")]
    private void PreviewOpenDoor()
    {
        SetOpen(true);
    }

    [ContextMenu("Preview/Close Door")]
    private void PreviewCloseDoor()
    {
        SetOpen(false);
    }

    private void OnValidate()
    {
        doorWidthInCells =
            Mathf.Max(1, doorWidthInCells);

        if (string.IsNullOrWhiteSpace(socketId))
        {
            socketId = gameObject.name;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = GetDirectionColor(direction);
        Gizmos.DrawSphere(transform.position, 0.16f);

        Vector2Int cellDirection =
            direction.ToCellOffset();

        Vector3 localDirection =
            new Vector3(
                cellDirection.x,
                cellDirection.y,
                0f);

        Vector3 worldDirection =
            transform.TransformDirection(localDirection);

        Gizmos.DrawLine(
            transform.position,
            transform.position + worldDirection * 0.8f);
    }

    private static Color GetDirectionColor(
        DreamRoomDoorDirection socketDirection)
    {
        switch (socketDirection)
        {
            case DreamRoomDoorDirection.North:
                return new Color(0.25f, 0.9f, 1f);

            case DreamRoomDoorDirection.East:
                return new Color(1f, 0.8f, 0.2f);

            case DreamRoomDoorDirection.South:
                return new Color(1f, 0.35f, 0.8f);

            case DreamRoomDoorDirection.West:
                return new Color(0.35f, 1f, 0.45f);

            default:
                return Color.white;
        }
    }
}
