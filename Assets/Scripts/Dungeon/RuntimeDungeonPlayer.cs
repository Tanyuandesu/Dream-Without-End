using UnityEngine;

/// <summary>
/// Demonstration player movement used by the runtime dungeon.
/// CB3.5 makes this component the authoritative owner of the player's
/// eight-direction facing as well as Rigidbody2D movement. Combat may request
/// one immediate input/facing refresh before an action, but it never moves the
/// player directly. The last non-zero movement direction remains the facing
/// direction while the player is idle.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class RuntimeDungeonPlayer : MonoBehaviour, ICharacterFacingSource
{
    private const float MinimumModifierDuration = 0.01f;
    private const float MinimumDirectionMagnitude = 0.0001f;

    [Header("Runtime movement")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Vector2 input;
    [SerializeField] private float moveSpeed = 5f;

    [Header("CB3.5 authoritative eight-direction facing")]
    [SerializeField] private CharacterFacingDirection currentFacing =
        CharacterFacingDirection.South;
    [SerializeField] private Vector2 facingVector = Vector2.down;
    [SerializeField] private int lastInputSampleFrame = -1;
    [SerializeField] private int lastFacingChangeFrame = -1;

    [Header("Timed external movement scale")]
    [SerializeField] private float timedMovementScale = 1f;
    [SerializeField] private float timedMovementScaleEndsAt = -1f;

    [Header("Runtime diagnostics")]
    [SerializeField] private int timedMovementScaleStartCount;
    [SerializeField] private int timedMovementScaleCompleteCount;
    [SerializeField] private int timedMovementScaleRejectCount;
    [SerializeField] private float lowestRequestedTimedMovementScale = 1f;
    [SerializeField] private int facingUpdateCount;
    [SerializeField] private int diagonalFacingUpdateCount;
    [SerializeField] private int combatFacingRefreshCount;
    [SerializeField] private int combatFacingChangeCount;

    public Vector2 CurrentInput => input;
    public float BaseMoveSpeed => moveSpeed;
    public CharacterFacingDirection CurrentFacing => currentFacing;
    public Vector2 FacingVector => facingVector;
    public int LastInputSampleFrame => lastInputSampleFrame;
    public int LastFacingChangeFrame => lastFacingChangeFrame;
    public int FacingUpdateCount => facingUpdateCount;
    public int DiagonalFacingUpdateCount => diagonalFacingUpdateCount;
    public int CombatFacingRefreshCount => combatFacingRefreshCount;
    public int CombatFacingChangeCount => combatFacingChangeCount;

    public bool IsTimedMovementScaleActive =>
        timedMovementScaleEndsAt >= 0f &&
        Time.time < timedMovementScaleEndsAt;

    public float CurrentTimedMovementScale =>
        IsTimedMovementScaleActive
            ? timedMovementScale
            : 1f;

    public float EffectiveMoveSpeed =>
        moveSpeed * CurrentTimedMovementScale;

    public float TimedMovementScaleEndsAt =>
        timedMovementScaleEndsAt;

    public int TimedMovementScaleStartCount =>
        timedMovementScaleStartCount;

    public int TimedMovementScaleCompleteCount =>
        timedMovementScaleCompleteCount;

    public int TimedMovementScaleRejectCount =>
        timedMovementScaleRejectCount;

    public float LowestRequestedTimedMovementScale =>
        lowestRequestedTimedMovementScale;

    public void Initialize(float speed)
    {
        moveSpeed = Mathf.Max(0.5f, speed);
        input = Vector2.zero;
        currentFacing = CharacterFacingDirection.South;
        facingVector = Vector2.down;
        lastInputSampleFrame = -1;
        lastFacingChangeFrame = -1;
        facingUpdateCount = 0;
        diagonalFacingUpdateCount = 0;
        combatFacingRefreshCount = 0;
        combatFacingChangeCount = 0;

        ClearTimedMovementScale(countCompletion: false);
        timedMovementScaleStartCount = 0;
        timedMovementScaleCompleteCount = 0;
        timedMovementScaleRejectCount = 0;
        lowestRequestedTimedMovementScale = 1f;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        facingVector = FacingToVector(currentFacing);
    }

    private void OnDisable()
    {
        ClearTimedMovementScale(countCompletion: false);
        input = Vector2.zero;
    }

    private void Update()
    {
        TickTimedMovementScale(Time.time);

        if (!GameFlowManager.AllowsGameplayInput)
        {
            input = Vector2.zero;
            return;
        }

        SampleMovementInputAndFacing();
    }

    private void FixedUpdate()
    {
        TickTimedMovementScale(Time.time);

        if (!GameFlowManager.AllowsGameplayInput)
        {
            input = Vector2.zero;
            return;
        }

        if (body == null)
        {
            return;
        }

        Vector2 nextPosition =
            body.position +
            input * EffectiveMoveSpeed * Time.fixedDeltaTime;

        body.MovePosition(nextPosition);
    }

    /// <summary>
    /// Re-reads the current keyboard state immediately before combat resolves
    /// its facing snapshot. This removes Script Execution Order ambiguity:
    /// whether movement Update ran before or after combat Update, a turn and
    /// click in the same rendered frame uses the newest held direction.
    /// Returns true when the authoritative facing changed during this frame.
    /// </summary>
    public bool RefreshInputAndFacingForCombat()
    {
        combatFacingRefreshCount++;

        if (!GameFlowManager.AllowsGameplayInput)
        {
            input = Vector2.zero;
            return false;
        }

        SampleMovementInputAndFacing();

        bool changedThisFrame =
            lastFacingChangeFrame == Time.frameCount;

        if (changedThisFrame)
        {
            combatFacingChangeCount++;
        }

        return changedThisFrame;
    }

    /// <summary>
    /// Requests one temporary movement multiplier. Input remains live while
    /// the modifier is active, so the player may steer through the recovery.
    /// A caller can explicitly replace an existing modifier when its action
    /// owns that boundary.
    /// </summary>
    public bool TryBeginTimedMovementScale(
        float scale,
        float duration,
        bool replaceExisting)
    {
        float now = Time.time;
        TickTimedMovementScale(now);

        if (duration < MinimumModifierDuration)
        {
            timedMovementScaleRejectCount++;
            return false;
        }

        if (IsTimedMovementScaleActive && !replaceExisting)
        {
            timedMovementScaleRejectCount++;
            return false;
        }

        timedMovementScale = Mathf.Clamp(scale, 0f, 2f);
        timedMovementScaleEndsAt =
            now + Mathf.Max(MinimumModifierDuration, duration);

        timedMovementScaleStartCount++;
        lowestRequestedTimedMovementScale = Mathf.Min(
            lowestRequestedTimedMovementScale,
            timedMovementScale);

        return true;
    }

    public void TickTimedMovementScale(float now)
    {
        if (timedMovementScaleEndsAt < 0f ||
            now < timedMovementScaleEndsAt)
        {
            return;
        }

        ClearTimedMovementScale(countCompletion: true);
    }

    public void ClearTimedMovementScale(bool countCompletion)
    {
        bool wasActive = timedMovementScaleEndsAt >= 0f;

        timedMovementScale = 1f;
        timedMovementScaleEndsAt = -1f;

        if (wasActive && countCompletion)
        {
            timedMovementScaleCompleteCount++;
        }
    }

    public static CharacterFacingDirection QuantizeFacingDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude < MinimumDirectionMagnitude)
        {
            return CharacterFacingDirection.South;
        }

        float angle = Mathf.Atan2(
            direction.y,
            direction.x) * Mathf.Rad2Deg;

        if (angle < 0f)
        {
            angle += 360f;
        }

        int sector = Mathf.RoundToInt(angle / 45f) % 8;

        switch (sector)
        {
            case 0:
                return CharacterFacingDirection.East;

            case 1:
                return CharacterFacingDirection.NorthEast;

            case 2:
                return CharacterFacingDirection.North;

            case 3:
                return CharacterFacingDirection.NorthWest;

            case 4:
                return CharacterFacingDirection.West;

            case 5:
                return CharacterFacingDirection.SouthWest;

            case 6:
                return CharacterFacingDirection.South;

            default:
                return CharacterFacingDirection.SouthEast;
        }
    }

    public static Vector2 FacingToVector(
        CharacterFacingDirection facing)
    {
        const float diagonal = 0.70710678f;

        switch (facing)
        {
            case CharacterFacingDirection.SouthEast:
                return new Vector2(diagonal, -diagonal);

            case CharacterFacingDirection.East:
                return Vector2.right;

            case CharacterFacingDirection.NorthEast:
                return new Vector2(diagonal, diagonal);

            case CharacterFacingDirection.North:
                return Vector2.up;

            case CharacterFacingDirection.NorthWest:
                return new Vector2(-diagonal, diagonal);

            case CharacterFacingDirection.West:
                return Vector2.left;

            case CharacterFacingDirection.SouthWest:
                return new Vector2(-diagonal, -diagonal);

            default:
                return Vector2.down;
        }
    }

    public static bool IsDiagonalFacing(
        CharacterFacingDirection facing)
    {
        return facing == CharacterFacingDirection.SouthEast ||
               facing == CharacterFacingDirection.NorthEast ||
               facing == CharacterFacingDirection.NorthWest ||
               facing == CharacterFacingDirection.SouthWest;
    }

    private void SampleMovementInputAndFacing()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        input = new Vector2(horizontal, vertical).normalized;
        lastInputSampleFrame = Time.frameCount;

        if (input.sqrMagnitude < MinimumDirectionMagnitude)
        {
            return;
        }

        CharacterFacingDirection newFacing =
            QuantizeFacingDirection(input);

        if (newFacing == currentFacing)
        {
            facingVector = FacingToVector(currentFacing);
            return;
        }

        currentFacing = newFacing;
        facingVector = FacingToVector(currentFacing);
        lastFacingChangeFrame = Time.frameCount;
        facingUpdateCount++;

        if (IsDiagonalFacing(currentFacing))
        {
            diagonalFacingUpdateCount++;
        }
    }
}
