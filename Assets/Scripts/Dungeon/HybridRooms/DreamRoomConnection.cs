using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 两个已放置房间之间的一条逻辑连接。
///
/// 阶段5会先建立 Room A/B 的连接图；
/// 阶段6再填入 Socket Id 与 Corridor Cells。
/// 使用索引和字符串而不是场景 Transform，保证 DungeonLayout 保持纯数据。
/// </summary>
[Serializable]
public sealed class DreamRoomConnection
{
    [SerializeField]
    private int roomAIndex;

    [SerializeField]
    private int roomBIndex;

    [SerializeField]
    private string socketAId = string.Empty;

    [SerializeField]
    private string socketBId = string.Empty;

    [SerializeField]
    private List<Vector2Int> corridorCells =
        new List<Vector2Int>();

    public int RoomAIndex => roomAIndex;
    public int RoomBIndex => roomBIndex;
    public string SocketAId => socketAId;
    public string SocketBId => socketBId;

    public IReadOnlyList<Vector2Int> CorridorCells =>
        corridorCells;

    public bool HasAssignedSockets =>
        !string.IsNullOrWhiteSpace(socketAId) &&
        !string.IsNullOrWhiteSpace(socketBId);

    public bool HasCorridor =>
        corridorCells != null &&
        corridorCells.Count > 0;

    public DreamRoomConnection(
        int firstRoomIndex,
        int secondRoomIndex)
    {
        roomAIndex = firstRoomIndex;
        roomBIndex = secondRoomIndex;
    }

    public bool ConnectsRoom(int roomIndex)
    {
        return roomAIndex == roomIndex ||
               roomBIndex == roomIndex;
    }

    public bool TryGetOtherRoomIndex(
        int roomIndex,
        out int otherRoomIndex)
    {
        if (roomAIndex == roomIndex)
        {
            otherRoomIndex = roomBIndex;
            return true;
        }

        if (roomBIndex == roomIndex)
        {
            otherRoomIndex = roomAIndex;
            return true;
        }

        otherRoomIndex = -1;
        return false;
    }

    public void AssignSockets(
        string firstSocketId,
        string secondSocketId)
    {
        socketAId = NormalizeId(firstSocketId);
        socketBId = NormalizeId(secondSocketId);
    }

    public void ClearAssignedSockets()
    {
        socketAId = string.Empty;
        socketBId = string.Empty;
    }

    public void SetCorridorCells(
        IEnumerable<Vector2Int> newCorridorCells)
    {
        if (corridorCells == null)
        {
            corridorCells = new List<Vector2Int>();
        }

        corridorCells.Clear();

        if (newCorridorCells == null)
        {
            return;
        }

        HashSet<Vector2Int> usedCells =
            new HashSet<Vector2Int>();

        foreach (Vector2Int cell in newCorridorCells)
        {
            if (usedCells.Add(cell))
            {
                corridorCells.Add(cell);
            }
        }
    }

    public List<string> GetValidationErrors(
        int roomCount,
        bool requireAssignedSockets,
        bool requireCorridor)
    {
        List<string> errors = new List<string>();

        if (roomAIndex < 0 || roomBIndex < 0)
        {
            errors.Add("连接两端的 Room Index 不能为负数。");
        }

        if (roomAIndex == roomBIndex)
        {
            errors.Add("一条连接不能把房间连接到自身。");
        }

        if (roomCount >= 0 &&
            (roomAIndex >= roomCount ||
             roomBIndex >= roomCount))
        {
            errors.Add(
                "连接引用了超出 RoomPlacements 范围的房间索引。");
        }

        bool hasSocketA =
            !string.IsNullOrWhiteSpace(socketAId);

        bool hasSocketB =
            !string.IsNullOrWhiteSpace(socketBId);

        if (hasSocketA != hasSocketB)
        {
            errors.Add(
                "Socket A/B 必须同时填写或同时留空。");
        }

        if (requireAssignedSockets &&
            (!hasSocketA || !hasSocketB))
        {
            errors.Add("当前阶段要求连接已经分配两个 Socket。");
        }

        if (requireCorridor && !HasCorridor)
        {
            errors.Add("当前阶段要求连接已经保存 Corridor Cells。");
        }

        return errors;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
