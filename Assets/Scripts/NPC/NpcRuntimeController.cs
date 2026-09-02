using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime movement/physics shell for one NPC instance.
/// Movement is intentionally room-local and uses only legal walkable cells.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcRuntimeController : MonoBehaviour
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private readonly HashSet<Vector2Int> wanderCells =
        new HashSet<Vector2Int>();
    private readonly List<Vector2Int> neighborBuffer =
        new List<Vector2Int>(4);

    private NpcDefinition definition;
    private DungeonRenderer dungeonRenderer;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private System.Random random;

    private Vector2Int currentCell;
    private Vector2Int targetCell;
    private Vector2Int previousCell;
    private bool hasPreviousCell;
    private bool moving;
    private bool wanderEnabled;
    private float idleRemaining;

    public NpcDefinition Definition => definition;
    public Vector2Int CurrentCell => currentCell;
    public bool WanderEnabled => wanderEnabled;
    public bool IsFirstEncounterInstance { get; private set; }
    public bool IsFinalLegacyInstance { get; private set; }
    public string RoomTemplateId { get; private set; } = string.Empty;
    public Collider2D BodyCollider => bodyCollider;

    public void Initialize(
        NpcDefinition npcDefinition,
        DungeonRenderer renderer,
        Vector2Int spawnCell,
        IEnumerable<Vector2Int> allowedWanderCells,
        bool enableWander,
        bool isFirstEncounter,
        bool isFinalLegacy,
        string roomTemplateId,
        int randomSeed)
    {
        definition = npcDefinition;
        dungeonRenderer = renderer;
        currentCell = spawnCell;
        targetCell = spawnCell;
        previousCell = spawnCell;
        hasPreviousCell = false;
        moving = false;
        wanderEnabled = enableWander;
        IsFirstEncounterInstance = isFirstEncounter;
        IsFinalLegacyInstance = isFinalLegacy;
        RoomTemplateId = roomTemplateId ?? string.Empty;
        random = new System.Random(randomSeed);

        wanderCells.Clear();
        if (allowedWanderCells != null)
        {
            foreach (Vector2Int cell in allowedWanderCells)
            {
                wanderCells.Add(cell);
            }
        }

        wanderCells.Add(spawnCell);
        EnsurePhysics();
        ResetIdleTimer();
    }

    public void SetWanderEnabled(bool enabled)
    {
        wanderEnabled = enabled;
        moving = false;
        currentCell = targetCell;
        ResetIdleTimer();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        if (!wanderEnabled ||
            definition == null ||
            dungeonRenderer == null ||
            body == null)
        {
            return;
        }

        if (moving)
        {
            Vector2 targetWorld =
                dungeonRenderer.CellToWorld(targetCell);

            Vector2 next = Vector2.MoveTowards(
                body.position,
                targetWorld,
                definition.MoveSpeed * Time.fixedDeltaTime);

            body.MovePosition(next);

            if ((next - targetWorld).sqrMagnitude <= 0.0004f)
            {
                body.MovePosition(targetWorld);
                previousCell = currentCell;
                hasPreviousCell = true;
                currentCell = targetCell;
                moving = false;
                ResetIdleTimer();
            }

            return;
        }

        idleRemaining -= Time.fixedDeltaTime;
        if (idleRemaining > 0f)
        {
            return;
        }

        TryChooseNextCell();
    }

    private void TryChooseNextCell()
    {
        neighborBuffer.Clear();

        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int candidate =
                currentCell + CardinalDirections[i];

            if (wanderCells.Contains(candidate))
            {
                neighborBuffer.Add(candidate);
            }
        }

        if (neighborBuffer.Count == 0)
        {
            ResetIdleTimer();
            return;
        }

        if (neighborBuffer.Count > 1 && hasPreviousCell)
        {
            neighborBuffer.Remove(previousCell);
        }

        int index = random != null
            ? random.Next(0, neighborBuffer.Count)
            : Random.Range(0, neighborBuffer.Count);

        targetCell = neighborBuffer[index];
        moving = true;
    }

    private void ResetIdleTimer()
    {
        if (definition == null)
        {
            idleRemaining = 0f;
            return;
        }

        float min = definition.IdleTimeMin;
        float max = definition.IdleTimeMax;
        double sample = random != null
            ? random.NextDouble()
            : Random.value;

        idleRemaining = Mathf.Lerp(
            min,
            max,
            (float)sample);
    }

    private void EnsurePhysics()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider == null)
        {
            bodyCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        bodyCollider.size = definition != null
            ? definition.BodyColliderSize
            : new Vector2(0.52f, 0.58f);
        bodyCollider.offset = definition != null
            ? definition.BodyColliderOffset
            : new Vector2(0f, -0.08f);
        bodyCollider.isTrigger = false;
        bodyCollider.enabled =
            definition == null ||
            definition.CollisionMode == NpcCollisionMode.Solid;
    }
}
