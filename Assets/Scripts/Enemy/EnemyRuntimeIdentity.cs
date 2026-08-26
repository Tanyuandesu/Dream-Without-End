using System;
using UnityEngine;

/// <summary>
/// Immutable identity and spawn provenance for one runtime enemy instance.
/// AI state is deliberately not stored here.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyRuntimeIdentity : MonoBehaviour
{
    [SerializeField] private string instanceId = string.Empty;
    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private int floorNumber;
    [SerializeField] private int floorSessionId;
    [SerializeField] private int roomIndex = -1;
    [SerializeField] private Vector2Int spawnCell;
    [SerializeField] private bool countsForEnding = true;

    public string InstanceId => instanceId;
    public EnemyDefinition Definition => definition;
    public EnemyId EnemyId => definition != null
        ? definition.Id
        : default(EnemyId);

    public int FloorNumber => floorNumber;
    public int FloorSessionId => floorSessionId;
    public int RoomIndex => roomIndex;
    public Vector2Int SpawnCell => spawnCell;
    public bool CountsForEnding => countsForEnding;

    public event Action<EnemyRuntimeIdentity> DestroyedUnexpectedly;

    public void Initialize(
        string newInstanceId,
        EnemyDefinition newDefinition,
        int newFloorNumber,
        int newFloorSessionId,
        int newRoomIndex,
        Vector2Int newSpawnCell,
        bool newCountsForEnding)
    {
        instanceId = string.IsNullOrWhiteSpace(newInstanceId)
            ? Guid.NewGuid().ToString("N")
            : newInstanceId.Trim();

        definition = newDefinition;
        floorNumber = Mathf.Max(0, newFloorNumber);
        floorSessionId = Mathf.Max(0, newFloorSessionId);
        roomIndex = newRoomIndex;
        spawnCell = newSpawnCell;
        countsForEnding = newCountsForEnding;
    }

    private void OnDestroy()
    {
        DestroyedUnexpectedly?.Invoke(this);
    }
}
