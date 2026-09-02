using UnityEngine;

/// <summary>
/// 房间内候选出生点的用途。
/// 当前只建立共同协议，R1 不会调用任何 Manager 生成对象。
/// </summary>
public enum DreamRoomSpawnPointKind
{
    Generic = 0,
    Player = 1,
    Exit = 2,
    Enemy = 3,
    Item = 4,
    Npc = 5
}

/// <summary>
/// 挂在 DreamRoomTemplate/SpawnPoints 下的候选点标记。
/// Local Cell 是权威格子数据；Transform 只用于 Prefab 内可视化定位。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamRoomSpawnPoint : MonoBehaviour
{
    [Header("身份")]
    [SerializeField]
    private string spawnPointId = "Spawn_0";

    [SerializeField]
    private DreamRoomSpawnPointKind kind =
        DreamRoomSpawnPointKind.Generic;

    [Header("格子数据")]
    [SerializeField]
    private Vector2Int localCell = Vector2Int.zero;

    [Tooltip("同类候选点之间的相对权重，不是百分比。")]
    [Min(1)]
    [SerializeField]
    private int randomWeight = 1;

    public string SpawnPointId => spawnPointId;
    public DreamRoomSpawnPointKind Kind => kind;
    public Vector2Int LocalCell => localCell;
    public int RandomWeight => randomWeight;

    public void Configure(
        string newId,
        DreamRoomSpawnPointKind newKind,
        Vector2Int newLocalCell,
        int newRandomWeight)
    {
        spawnPointId = string.IsNullOrWhiteSpace(newId)
            ? gameObject.name
            : newId.Trim();

        kind = newKind;
        localCell = newLocalCell;
        randomWeight = Mathf.Max(1, newRandomWeight);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            spawnPointId = gameObject.name;
        }

        randomWeight = Mathf.Max(1, randomWeight);
    }

    private void OnDrawGizmosSelected()
    {
        DreamRoomTemplate owner =
            GetComponentInParent<DreamRoomTemplate>();

        Vector3 worldPosition =
            owner != null
                ? owner.GetWorldCellCenter(localCell)
                : transform.position;

        Gizmos.color = GetKindColor(kind);
        Gizmos.DrawWireSphere(worldPosition, 0.2f);
        Gizmos.DrawLine(
            worldPosition + Vector3.left * 0.24f,
            worldPosition + Vector3.right * 0.24f);
        Gizmos.DrawLine(
            worldPosition + Vector3.down * 0.24f,
            worldPosition + Vector3.up * 0.24f);
    }

    private static Color GetKindColor(
        DreamRoomSpawnPointKind pointKind)
    {
        switch (pointKind)
        {
            case DreamRoomSpawnPointKind.Player:
                return new Color(0.25f, 0.95f, 1f);

            case DreamRoomSpawnPointKind.Exit:
                return new Color(1f, 0.78f, 0.15f);

            case DreamRoomSpawnPointKind.Enemy:
                return new Color(1f, 0.3f, 0.3f);

            case DreamRoomSpawnPointKind.Item:
                return new Color(0.45f, 1f, 0.45f);

            case DreamRoomSpawnPointKind.Npc:
                return new Color(0.72f, 0.62f, 1f);

            default:
                return Color.white;
        }
    }
}
