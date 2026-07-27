using System;
using UnityEngine;

/// <summary>
/// 根据角色根物件的实际位移播放方向 Sprite 动画。
///
/// CB3.5 additionally accepts an optional ICharacterFacingSource on the same
/// root. The player supplies one authoritative eight-direction facing shared
/// by movement, animation and combat. Enemies have no such source and retain
/// the original displacement-derived facing behaviour. This component never
/// reads input or moves a Rigidbody2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionalSpriteAnimator : MonoBehaviour
{
    [Header("动画资源")]
    [SerializeField] private CharacterAnimationProfile profile;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("显示尺寸")]
    [Min(0.05f)]
    [SerializeField] private float visualWorldHeight = 1f;

    [Header("移动判定")]
    [Min(0.000001f)]
    [SerializeField] private float movementEpsilon = 0.0005f;

    [Min(0f)]
    [SerializeField] private float idleDelay = 0.06f;

    [Tooltip("单帧位移超过此值时视为传送，不改变朝向。")]
    [Min(0.1f)]
    [SerializeField] private float teleportDistance = 1.5f;

    private Vector3 previousWorldPosition;
    private float timeWithoutMovement;
    private float frameTimer;
    private int frameIndex;

    private CharacterFacingDirection facing;
    private CharacterAnimationState locomotionState =
        CharacterAnimationState.Idle;

    private bool actionActive;
    private bool returnAfterAction = true;
    private CharacterAnimationState actionState;

    private SpriteAnimationSequence activeSequence;
    private bool activeFlipX;
    private ICharacterFacingSource facingSource;
    private bool actionReachedEnd;

    [Header("CB10A action diagnostics")]
    [SerializeField] private int actionStartCount;
    [SerializeField] private int actionCompleteCount;
    [SerializeField] private CharacterAnimationState lastStartedAction;
    [SerializeField] private CharacterAnimationState lastCompletedAction;

    public event Action<DirectionalSpriteAnimator, CharacterAnimationState> ActionStarted;
    public event Action<DirectionalSpriteAnimator, CharacterAnimationState> ActionCompleted;

    public CharacterAnimationProfile Profile => profile;
    public CharacterFacingDirection Facing => facing;
    public bool HasAuthoritativeFacingSource => facingSource != null;

    public CharacterAnimationState CurrentState =>
        actionActive ? actionState : locomotionState;

    public bool IsMoving =>
        locomotionState == CharacterAnimationState.Walk;

    public bool IsActionActive => actionActive;
    public CharacterAnimationState ActiveActionState => actionState;
    public int ActionStartCount => actionStartCount;
    public int ActionCompleteCount => actionCompleteCount;

    public float GetActionDuration(
        CharacterAnimationState state,
        CharacterFacingDirection direction)
    {
        if (profile == null)
        {
            return 0f;
        }

        bool unusedFlip;
        SpriteAnimationSequence sequence =
            profile.GetSequence(state, direction, out unusedFlip);

        if (sequence == null || !sequence.HasFrames)
        {
            return 0f;
        }

        return sequence.FrameCount / sequence.FramesPerSecond;
    }

    private void Awake()
    {
        CacheRenderer();
        CacheFacingSource();

        facing = profile != null
            ? profile.DefaultFacing
            : CharacterFacingDirection.South;

        previousWorldPosition = transform.position;
    }

    private void OnEnable()
    {
        CacheFacingSource();
        ResetTracking();
        RefreshSequence(true);
    }

    private void LateUpdate()
    {
        if (profile == null || spriteRenderer == null)
        {
            previousWorldPosition = transform.position;
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector2 displacement =
            currentPosition - previousWorldPosition;

        previousWorldPosition = currentPosition;

        float teleportDistanceSquared =
            teleportDistance * teleportDistance;

        if (displacement.sqrMagnitude > teleportDistanceSquared)
        {
            TryApplyAuthoritativeFacing();
            locomotionState = CharacterAnimationState.Idle;
            timeWithoutMovement = idleDelay;
            RefreshSequence(false);
            AdvanceFrame();
            return;
        }

        float epsilonSquared =
            movementEpsilon * movementEpsilon;

        bool hasAuthoritativeFacing =
            TryApplyAuthoritativeFacing();

        if (displacement.sqrMagnitude > epsilonSquared)
        {
            timeWithoutMovement = 0f;
            locomotionState = CharacterAnimationState.Walk;

            if (!hasAuthoritativeFacing)
            {
                facing = QuantizeDirection(displacement);
            }
        }
        else
        {
            timeWithoutMovement += Time.deltaTime;

            if (timeWithoutMovement >= idleDelay)
            {
                locomotionState = CharacterAnimationState.Idle;
            }
        }

        RefreshSequence(false);
        AdvanceFrame();
    }

    public void Initialize(
        CharacterAnimationProfile newProfile,
        SpriteRenderer targetRenderer,
        float targetWorldHeight)
    {
        profile = newProfile;
        spriteRenderer = targetRenderer;
        visualWorldHeight = Mathf.Max(0.05f, targetWorldHeight);
        CacheFacingSource();

        facing = profile != null
            ? profile.DefaultFacing
            : CharacterFacingDirection.South;

        ResetTracking();
        RefreshSequence(true);
    }

    public void SetProfile(
        CharacterAnimationProfile newProfile)
    {
        if (profile == newProfile)
        {
            return;
        }

        profile = newProfile;

        facing = profile != null
            ? profile.DefaultFacing
            : CharacterFacingDirection.South;

        ClearAction();
        RefreshSequence(true);
    }

    public void SetVisualWorldHeight(float newWorldHeight)
    {
        visualWorldHeight = Mathf.Max(0.05f, newWorldHeight);
        ApplyCurrentFrame();
    }

    public void SetFacingDirection(
        CharacterFacingDirection newFacing)
    {
        facing = newFacing;
        RefreshSequence(true);
    }

    /// <summary>
    /// 为未来 Attack、Hurt、Death、Special 提供统一入口。
    /// CA1 暂时不主动调用这些状态。
    /// </summary>
    public bool PlayAction(
        CharacterAnimationState state,
        bool restart = true,
        bool returnToLocomotion = true)
    {
        if (state == CharacterAnimationState.Idle ||
            state == CharacterAnimationState.Walk ||
            profile == null)
        {
            return false;
        }

        if (!profile.HasSequence(state, facing))
        {
            return false;
        }

        if (actionActive &&
            actionState == state &&
            !restart)
        {
            return true;
        }

        actionActive = true;
        actionState = state;
        returnAfterAction = returnToLocomotion;
        actionReachedEnd = false;

        RefreshSequence(true);
        actionStartCount++;
        lastStartedAction = state;
        ActionStarted?.Invoke(this, state);
        return true;
    }

    public void ClearAction()
    {
        actionActive = false;
        actionReachedEnd = false;
        RefreshSequence(true);
    }

    private void ResetTracking()
    {
        previousWorldPosition = transform.position;
        timeWithoutMovement = idleDelay;
        locomotionState = CharacterAnimationState.Idle;
        frameTimer = 0f;
        frameIndex = 0;
        activeSequence = null;
        actionReachedEnd = false;
    }

    private bool TryApplyAuthoritativeFacing()
    {
        if (facingSource == null)
        {
            CacheFacingSource();
        }

        if (facingSource == null)
        {
            return false;
        }

        facing = facingSource.CurrentFacing;
        return true;
    }

    private void CacheFacingSource()
    {
        if (facingSource != null)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null || object.ReferenceEquals(behaviour, this))
            {
                continue;
            }

            ICharacterFacingSource candidate =
                behaviour as ICharacterFacingSource;

            if (candidate != null)
            {
                facingSource = candidate;
                return;
            }
        }
    }

    private void CacheRenderer()
    {
        if (spriteRenderer != null)
        {
            return;
        }

        spriteRenderer =
            GetComponentInChildren<SpriteRenderer>(true);
    }

    private CharacterFacingDirection QuantizeDirection(
        Vector2 direction)
    {
        if (profile.DirectionMode ==
            CharacterAnimationDirectionMode.NoDirection)
        {
            return profile.DefaultFacing;
        }

        if (profile.DirectionMode ==
            CharacterAnimationDirectionMode.FourDirections)
        {
            if (Mathf.Abs(direction.x) >
                Mathf.Abs(direction.y))
            {
                return direction.x >= 0f
                    ? CharacterFacingDirection.East
                    : CharacterFacingDirection.West;
            }

            return direction.y >= 0f
                ? CharacterFacingDirection.North
                : CharacterFacingDirection.South;
        }

        float angle = Mathf.Atan2(
            direction.y,
            direction.x) * Mathf.Rad2Deg;

        if (angle < 0f)
        {
            angle += 360f;
        }

        int sector =
            Mathf.RoundToInt(angle / 45f) % 8;

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

    private void RefreshSequence(bool restart)
    {
        if (profile == null)
        {
            activeSequence = null;
            return;
        }

        CharacterAnimationState desiredState =
            actionActive
                ? actionState
                : locomotionState;

        bool flipX;

        SpriteAnimationSequence desiredSequence =
            profile.GetSequence(
                desiredState,
                facing,
                out flipX);

        if (desiredSequence == activeSequence &&
            flipX == activeFlipX &&
            !restart)
        {
            return;
        }

        activeSequence = desiredSequence;
        activeFlipX = flipX;
        frameIndex = 0;
        frameTimer = 0f;

        ApplyCurrentFrame();
    }

    private void AdvanceFrame()
    {
        if (activeSequence == null ||
            !activeSequence.HasFrames)
        {
            return;
        }

        if (activeSequence.FrameCount <= 1)
        {
            ApplyCurrentFrame();
            return;
        }

        frameTimer += Time.deltaTime;

        float frameDuration =
            1f / activeSequence.FramesPerSecond;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;

            if (frameIndex < activeSequence.FrameCount)
            {
                continue;
            }

            if (activeSequence.Loop)
            {
                frameIndex = 0;
                continue;
            }

            frameIndex =
                activeSequence.FrameCount - 1;

            if (actionActive && !actionReachedEnd)
            {
                actionReachedEnd = true;
                actionCompleteCount++;
                lastCompletedAction = actionState;
                ActionCompleted?.Invoke(this, actionState);
            }

            if (actionActive && returnAfterAction)
            {
                actionActive = false;
                actionReachedEnd = false;
                RefreshSequence(true);
            }

            break;
        }

        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (spriteRenderer == null ||
            activeSequence == null ||
            !activeSequence.HasFrames)
        {
            return;
        }

        Sprite frame = activeSequence.GetFrame(frameIndex);

        if (frame == null)
        {
            return;
        }

        spriteRenderer.sprite = frame;
        spriteRenderer.flipX = activeFlipX;

        float spriteHeight = frame.bounds.size.y;

        if (spriteHeight <= 0.0001f)
        {
            return;
        }

        float uniformScale =
            visualWorldHeight / spriteHeight;

        spriteRenderer.transform.localScale =
            new Vector3(
                uniformScale,
                uniformScale,
                1f);
    }
}
