using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-side combat boundary.
/// CB8 keeps the complete nonlethal-push and direct-damage pipelines while
/// adding one explicit action-arbitration boundary. Same-frame dual input is
/// resolved deterministically, each action keeps its own cooldown, and one
/// action may not silently overlap the other action's afterlag unless the
/// authored arbitration policy explicitly allows cancellation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public sealed class PlayerCombatController : MonoBehaviour
{
    private const int QueryResultCapacity = 64;
    private const float MinimumAimMagnitude = 0.0001f;

    [Header("Combat contract state")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool combatInputEnabled;

    [Header("CB4 facing-based nonlethal push")]
    [SerializeField] private NonlethalPushSettings nonlethalPushSettings =
        NonlethalPushSettings.CreateDefault();

    [Header("CB5 facing-based direct damage attack")]
    [SerializeField] private DirectAttackSettings directAttackSettings =
        DirectAttackSettings.CreateDefault();

    [Header("CB4.5 mouse and keyboard input bindings")]
    [SerializeField] private PlayerCombatInputBindings inputBindings =
        PlayerCombatInputBindings.CreateDefault();

    [Header("CB8 dual-action arbitration")]
    [SerializeField]
    private CombatActionArbitrationSettings actionArbitrationSettings =
        CombatActionArbitrationSettings.CreateDefault();

    [Header("Runtime references")]
    [SerializeField] private RuntimeDungeonPlayer movement;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Health health;
    [SerializeField] private DirectionalSpriteAnimator visualAnimator;

    [Header("Runtime diagnostics")]
    [SerializeField] private int issuedAttackCount;
    [SerializeField] private CombatAttackId lastIssuedAttackId;
    [SerializeField] private int leftPushInputCount;
    [SerializeField] private int successfulLeftPushActionCount;
    [SerializeField] private int acceptedLeftPushTargetCount;
    [SerializeField] private int lastAcceptedTargetCount;
    [SerializeField] private Vector2 lastAimDirection = Vector2.down;

    [Header("CB3.5 facing attack diagnostics")]
    [SerializeField] private CharacterFacingDirection lastActionFacing =
        CharacterFacingDirection.South;
    [SerializeField] private int facingBasedActionCount;
    [SerializeField] private int sameFrameTurnActionCount;
    [SerializeField] private int arcRejectedTargetCount;
    [SerializeField] private int lastArcRejectedTargetCount;
    [SerializeField] private int observedActionFacingMask;
    [SerializeField] private int visualFacingSyncCount;

    [Header("CB3 action timing diagnostics")]
    [SerializeField] private int startedLeftPushActionCount;
    [SerializeField] private int recoveryRejectedLeftPushCount;
    [SerializeField] private int cooldownRejectedLeftPushCount;
    [SerializeField] private int afterlagMovementStartCount;
    [SerializeField] private int afterlagMovementRejectCount;
    [SerializeField] private float lastLeftPushStartedAt = -1f;
    [SerializeField] private float actionRecoveryEndsAt = -1f;
    [SerializeField] private float nextLeftPushReadyAt = -1f;

    [Header("CB5 direct-attack diagnostics")]
    [SerializeField] private int issuedNonlethalPushAttackCount;
    [SerializeField] private int issuedDirectAttackCount;
    [SerializeField] private int startedDirectAttackActionCount;
    [SerializeField] private int successfulDirectAttackActionCount;
    [SerializeField] private int acceptedDirectAttackTargetCount;
    [SerializeField] private int lastAcceptedDirectAttackTargetCount;
    [SerializeField] private int directAttackRecoveryRejectCount;
    [SerializeField] private int directAttackCooldownRejectCount;
    [SerializeField] private int directAttackAfterlagMovementStartCount;
    [SerializeField] private int directAttackAfterlagMovementRejectCount;
    [SerializeField] private int directAttackArcRejectedTargetCount;
    [SerializeField] private int lastDirectAttackArcRejectedTargetCount;
    [SerializeField] private int directAttackFacingSnapshotCount;
    [SerializeField] private int directAttackSameFrameTurnCount;
    [SerializeField] private int directAttackObservedFacingMask;
    [SerializeField] private int directAttackVisualFacingSyncCount;
    [SerializeField] private float lastDirectAttackStartedAt = -1f;
    [SerializeField] private float directAttackRecoveryEndsAt = -1f;
    [SerializeField] private float nextDirectAttackReadyAt = -1f;
    [SerializeField] private Vector2 lastDirectAttackAimDirection = Vector2.down;
    [SerializeField] private CharacterFacingDirection lastDirectAttackFacing =
        CharacterFacingDirection.South;

    [Header("CB4.5 multi-input diagnostics")]
    [SerializeField] private int mousePushInputCount;
    [SerializeField] private int primaryKeyPushInputCount;
    [SerializeField] private int secondaryKeyPushInputCount;
    [SerializeField] private int mousePushStartedActionCount;
    [SerializeField] private int primaryKeyPushStartedActionCount;
    [SerializeField] private int secondaryKeyPushStartedActionCount;
    [SerializeField] private int mousePushSuccessfulActionCount;
    [SerializeField] private int primaryKeyPushSuccessfulActionCount;
    [SerializeField] private int secondaryKeyPushSuccessfulActionCount;
    [SerializeField] private int coalescedPushInputFrameCount;
    [SerializeField] private int directAttackInputFrameCount;
    [SerializeField] private int mouseDirectAttackInputCount;
    [SerializeField] private int primaryKeyDirectAttackInputCount;
    [SerializeField] private int secondaryKeyDirectAttackInputCount;
    [SerializeField] private int coalescedDirectAttackInputFrameCount;
    [SerializeField] private int reservedDirectAttackRequestCount;
    [SerializeField] private int executedDirectAttackActionCount;
    [SerializeField] private int mouseDirectAttackStartedActionCount;
    [SerializeField] private int primaryKeyDirectAttackStartedActionCount;
    [SerializeField] private int secondaryKeyDirectAttackStartedActionCount;
    [SerializeField] private int mouseDirectAttackSuccessfulActionCount;
    [SerializeField] private int primaryKeyDirectAttackSuccessfulActionCount;
    [SerializeField] private int secondaryKeyDirectAttackSuccessfulActionCount;

    [Header("CB8 action arbitration diagnostics")]
    [SerializeField] private int simultaneousActionInputFrameCount;
    [SerializeField] private int simultaneousPushPriorityResolutionCount;
    [SerializeField] private int simultaneousDirectPriorityResolutionCount;
    [SerializeField] private int simultaneousRejectBothCount;
    [SerializeField] private int suppressedPushInputFrameCount;
    [SerializeField] private int suppressedDirectAttackInputFrameCount;
    [SerializeField] private int pushBlockedByDirectRecoveryCount;
    [SerializeField] private int directBlockedByPushRecoveryCount;
    [SerializeField] private int pushCancelledDirectRecoveryCount;
    [SerializeField] private int directCancelledPushRecoveryCount;
    [SerializeField] private int pushStartedWhileDirectCooldownActiveCount;
    [SerializeField] private int directStartedWhilePushCooldownActiveCount;
    [SerializeField] private int dualActionStartFrameViolationCount;
    [SerializeField] private int lastPushActionStartFrame = -1;
    [SerializeField] private int lastDirectActionStartFrame = -1;
    [SerializeField] private CombatActionKind lastStartedActionKind =
        CombatActionKind.Unspecified;

    private ContactFilter2D combatQueryFilter;

    private readonly Collider2D[] overlapResults =
        new Collider2D[QueryResultCapacity];

    private readonly RaycastHit2D[] lineOfSightResults =
        new RaycastHit2D[QueryResultCapacity];

    private readonly HashSet<EnemyCombatReceiver> uniqueReceivers =
        new HashSet<EnemyCombatReceiver>();

    private readonly List<PushCandidate> pushCandidates =
        new List<PushCandidate>(16);

    private readonly List<PushCandidate> directAttackCandidates =
        new List<PushCandidate>(16);

    public event Action<
        PlayerCombatController,
        CombatActionKind,
        CharacterFacingDirection> CombatActionAnimationRequested;

    public bool IsInitialized => initialized;
    public bool CombatInputEnabled => combatInputEnabled;
    public RuntimeDungeonPlayer Movement => movement;
    public Rigidbody2D Body => body;
    public Health Health => health;
    public DirectionalSpriteAnimator VisualAnimator => visualAnimator;
    public NonlethalPushSettings PushSettings =>
        nonlethalPushSettings;
    public DirectAttackSettings DirectAttackSettings =>
        directAttackSettings;
    public PlayerCombatInputBindings InputBindings => inputBindings;
    public CombatActionArbitrationSettings ActionArbitrationSettings =>
        actionArbitrationSettings;

    public int IssuedAttackCount => issuedAttackCount;
    public CombatAttackId LastIssuedAttackId => lastIssuedAttackId;
    public int LeftPushInputCount => leftPushInputCount;
    public int SuccessfulLeftPushActionCount =>
        successfulLeftPushActionCount;

    public int AcceptedLeftPushTargetCount =>
        acceptedLeftPushTargetCount;

    public int LastAcceptedTargetCount => lastAcceptedTargetCount;
    public Vector2 LastAimDirection => lastAimDirection;
    public CharacterFacingDirection LastActionFacing => lastActionFacing;
    public int FacingBasedActionCount => facingBasedActionCount;
    public int SameFrameTurnActionCount => sameFrameTurnActionCount;
    public int ArcRejectedTargetCount => arcRejectedTargetCount;
    public int LastArcRejectedTargetCount => lastArcRejectedTargetCount;
    public int ObservedActionFacingMask => observedActionFacingMask;
    public int ObservedActionFacingCount =>
        CountObservedFacingDirections(observedActionFacingMask);
    public int VisualFacingSyncCount => visualFacingSyncCount;

    public int StartedLeftPushActionCount =>
        startedLeftPushActionCount;

    public int RecoveryRejectedLeftPushCount =>
        recoveryRejectedLeftPushCount;

    public int CooldownRejectedLeftPushCount =>
        cooldownRejectedLeftPushCount;

    public int AfterlagMovementStartCount =>
        afterlagMovementStartCount;

    public int AfterlagMovementRejectCount =>
        afterlagMovementRejectCount;

    public float LastLeftPushStartedAt =>
        lastLeftPushStartedAt;

    public float ActionRecoveryEndsAt =>
        actionRecoveryEndsAt;

    public float NextLeftPushReadyAt =>
        nextLeftPushReadyAt;

    public bool IsActionRecoveryActive =>
        actionRecoveryEndsAt >= 0f &&
        Time.time < actionRecoveryEndsAt;

    public bool IsLeftPushCooldownActive =>
        nextLeftPushReadyAt >= 0f &&
        Time.time < nextLeftPushReadyAt;

    public float RemainingActionRecovery =>
        IsActionRecoveryActive
            ? Mathf.Max(0f, actionRecoveryEndsAt - Time.time)
            : 0f;

    public float RemainingLeftPushCooldown =>
        IsLeftPushCooldownActive
            ? Mathf.Max(0f, nextLeftPushReadyAt - Time.time)
            : 0f;

    public int MousePushInputCount => mousePushInputCount;
    public int PrimaryKeyPushInputCount => primaryKeyPushInputCount;
    public int SecondaryKeyPushInputCount => secondaryKeyPushInputCount;
    public int MousePushStartedActionCount => mousePushStartedActionCount;
    public int PrimaryKeyPushStartedActionCount => primaryKeyPushStartedActionCount;
    public int SecondaryKeyPushStartedActionCount => secondaryKeyPushStartedActionCount;
    public int MousePushSuccessfulActionCount => mousePushSuccessfulActionCount;
    public int PrimaryKeyPushSuccessfulActionCount => primaryKeyPushSuccessfulActionCount;
    public int SecondaryKeyPushSuccessfulActionCount => secondaryKeyPushSuccessfulActionCount;
    public int CoalescedPushInputFrameCount => coalescedPushInputFrameCount;
    public int DirectAttackInputFrameCount => directAttackInputFrameCount;
    public int MouseDirectAttackInputCount => mouseDirectAttackInputCount;
    public int PrimaryKeyDirectAttackInputCount => primaryKeyDirectAttackInputCount;
    public int SecondaryKeyDirectAttackInputCount => secondaryKeyDirectAttackInputCount;
    public int CoalescedDirectAttackInputFrameCount =>
        coalescedDirectAttackInputFrameCount;
    public int ReservedDirectAttackRequestCount =>
        reservedDirectAttackRequestCount;
    public int ExecutedDirectAttackActionCount =>
        executedDirectAttackActionCount;
    public int IssuedNonlethalPushAttackCount =>
        issuedNonlethalPushAttackCount;
    public int IssuedDirectAttackCount => issuedDirectAttackCount;
    public int StartedDirectAttackActionCount =>
        startedDirectAttackActionCount;
    public int SuccessfulDirectAttackActionCount =>
        successfulDirectAttackActionCount;
    public int AcceptedDirectAttackTargetCount =>
        acceptedDirectAttackTargetCount;
    public int LastAcceptedDirectAttackTargetCount =>
        lastAcceptedDirectAttackTargetCount;
    public int DirectAttackRecoveryRejectCount =>
        directAttackRecoveryRejectCount;
    public int DirectAttackCooldownRejectCount =>
        directAttackCooldownRejectCount;
    public int DirectAttackAfterlagMovementStartCount =>
        directAttackAfterlagMovementStartCount;
    public int DirectAttackAfterlagMovementRejectCount =>
        directAttackAfterlagMovementRejectCount;
    public int DirectAttackArcRejectedTargetCount =>
        directAttackArcRejectedTargetCount;
    public int LastDirectAttackArcRejectedTargetCount =>
        lastDirectAttackArcRejectedTargetCount;
    public int DirectAttackFacingSnapshotCount =>
        directAttackFacingSnapshotCount;
    public int DirectAttackSameFrameTurnCount =>
        directAttackSameFrameTurnCount;
    public int DirectAttackObservedFacingMask =>
        directAttackObservedFacingMask;
    public int DirectAttackVisualFacingSyncCount =>
        directAttackVisualFacingSyncCount;
    public int DirectAttackObservedFacingCount =>
        CountObservedFacingDirections(directAttackObservedFacingMask);
    public float LastDirectAttackStartedAt => lastDirectAttackStartedAt;
    public float DirectAttackRecoveryEndsAt => directAttackRecoveryEndsAt;
    public float NextDirectAttackReadyAt => nextDirectAttackReadyAt;
    public Vector2 LastDirectAttackAimDirection =>
        lastDirectAttackAimDirection;
    public CharacterFacingDirection LastDirectAttackFacing =>
        lastDirectAttackFacing;
    public bool IsDirectAttackRecoveryActive =>
        directAttackRecoveryEndsAt >= 0f &&
        Time.time < directAttackRecoveryEndsAt;
    public bool IsDirectAttackCooldownActive =>
        nextDirectAttackReadyAt >= 0f &&
        Time.time < nextDirectAttackReadyAt;
    public int MouseDirectAttackStartedActionCount =>
        mouseDirectAttackStartedActionCount;
    public int PrimaryKeyDirectAttackStartedActionCount =>
        primaryKeyDirectAttackStartedActionCount;
    public int SecondaryKeyDirectAttackStartedActionCount =>
        secondaryKeyDirectAttackStartedActionCount;
    public int MouseDirectAttackSuccessfulActionCount =>
        mouseDirectAttackSuccessfulActionCount;
    public int PrimaryKeyDirectAttackSuccessfulActionCount =>
        primaryKeyDirectAttackSuccessfulActionCount;
    public int SecondaryKeyDirectAttackSuccessfulActionCount =>
        secondaryKeyDirectAttackSuccessfulActionCount;

    public int SimultaneousActionInputFrameCount =>
        simultaneousActionInputFrameCount;
    public int SimultaneousPushPriorityResolutionCount =>
        simultaneousPushPriorityResolutionCount;
    public int SimultaneousDirectPriorityResolutionCount =>
        simultaneousDirectPriorityResolutionCount;
    public int SimultaneousRejectBothCount => simultaneousRejectBothCount;
    public int SuppressedPushInputFrameCount => suppressedPushInputFrameCount;
    public int SuppressedDirectAttackInputFrameCount =>
        suppressedDirectAttackInputFrameCount;
    public int PushBlockedByDirectRecoveryCount =>
        pushBlockedByDirectRecoveryCount;
    public int DirectBlockedByPushRecoveryCount =>
        directBlockedByPushRecoveryCount;
    public int PushCancelledDirectRecoveryCount =>
        pushCancelledDirectRecoveryCount;
    public int DirectCancelledPushRecoveryCount =>
        directCancelledPushRecoveryCount;
    public int PushStartedWhileDirectCooldownActiveCount =>
        pushStartedWhileDirectCooldownActiveCount;
    public int DirectStartedWhilePushCooldownActiveCount =>
        directStartedWhilePushCooldownActiveCount;
    public int DualActionStartFrameViolationCount =>
        dualActionStartFrameViolationCount;
    public int LastPushActionStartFrame => lastPushActionStartFrame;
    public int LastDirectActionStartFrame => lastDirectActionStartFrame;
    public CombatActionKind LastStartedActionKind => lastStartedActionKind;
    public bool HasOverlappingActionRecovery =>
        IsActionRecoveryActive && IsDirectAttackRecoveryActive;

    private void Awake()
    {
        CacheComponents();
        ConfigureQueryFilter();
    }

    private void Update()
    {
        if (!GameFlowManager.AllowsGameplayInput ||
            !initialized ||
            !combatInputEnabled ||
            health == null ||
            health.IsDead ||
            inputBindings == null)
        {
            return;
        }

        CombatInputFrame pushInput =
            nonlethalPushSettings != null &&
            nonlethalPushSettings.Enabled
                ? ReadNonlethalPushInputs()
                : default(CombatInputFrame);

        CombatInputFrame directInput =
            directAttackSettings != null &&
            directAttackSettings.Enabled
                ? ReadDirectAttackInputs()
                : default(CombatInputFrame);

        ResolveCombatInputFrame(pushInput, directInput);
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator)
    {
        Initialize(
            newMovement,
            newBody,
            newHealth,
            newVisualAnimator,
            NonlethalPushSettings.CreateDefault(),
            DirectAttackSettings.CreateDefault(),
            PlayerCombatInputBindings.CreateDefault(),
            CombatActionArbitrationSettings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings)
    {
        Initialize(
            newMovement,
            newBody,
            newHealth,
            newVisualAnimator,
            newNonlethalPushSettings,
            DirectAttackSettings.CreateDefault(),
            PlayerCombatInputBindings.CreateDefault(),
            CombatActionArbitrationSettings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings,
        PlayerCombatInputBindings newInputBindings)
    {
        Initialize(
            newMovement,
            newBody,
            newHealth,
            newVisualAnimator,
            newNonlethalPushSettings,
            DirectAttackSettings.CreateDefault(),
            newInputBindings,
            CombatActionArbitrationSettings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings,
        DirectAttackSettings newDirectAttackSettings,
        PlayerCombatInputBindings newInputBindings)
    {
        Initialize(
            newMovement,
            newBody,
            newHealth,
            newVisualAnimator,
            newNonlethalPushSettings,
            newDirectAttackSettings,
            newInputBindings,
            CombatActionArbitrationSettings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings,
        DirectAttackSettings newDirectAttackSettings,
        PlayerCombatInputBindings newInputBindings,
        CombatActionArbitrationSettings newActionArbitrationSettings)
    {
        movement = newMovement;
        body = newBody;
        health = newHealth;
        visualAnimator = newVisualAnimator;
        nonlethalPushSettings = newNonlethalPushSettings != null
            ? newNonlethalPushSettings.CreateRuntimeCopy()
            : NonlethalPushSettings.CreateDefault();
        directAttackSettings = newDirectAttackSettings != null
            ? newDirectAttackSettings.CreateRuntimeCopy()
            : DirectAttackSettings.CreateDefault();
        inputBindings = newInputBindings != null
            ? newInputBindings.CreateRuntimeCopy()
            : PlayerCombatInputBindings.CreateDefault();
        actionArbitrationSettings = newActionArbitrationSettings != null
            ? newActionArbitrationSettings.CreateRuntimeCopy()
            : CombatActionArbitrationSettings.CreateDefault();

        CacheComponents();
        ConfigureQueryFilter();

        combatInputEnabled = false;
        issuedAttackCount = 0;
        issuedNonlethalPushAttackCount = 0;
        issuedDirectAttackCount = 0;
        lastIssuedAttackId = default(CombatAttackId);
        leftPushInputCount = 0;
        successfulLeftPushActionCount = 0;
        acceptedLeftPushTargetCount = 0;
        lastAcceptedTargetCount = 0;
        lastActionFacing = movement != null
            ? movement.CurrentFacing
            : CharacterFacingDirection.South;
        lastAimDirection = movement != null
            ? movement.FacingVector
            : Vector2.down;
        facingBasedActionCount = 0;
        sameFrameTurnActionCount = 0;
        arcRejectedTargetCount = 0;
        lastArcRejectedTargetCount = 0;
        observedActionFacingMask = 0;
        visualFacingSyncCount = 0;
        startedLeftPushActionCount = 0;
        recoveryRejectedLeftPushCount = 0;
        cooldownRejectedLeftPushCount = 0;
        afterlagMovementStartCount = 0;
        afterlagMovementRejectCount = 0;
        lastLeftPushStartedAt = -1f;
        actionRecoveryEndsAt = -1f;
        nextLeftPushReadyAt = -1f;
        startedDirectAttackActionCount = 0;
        successfulDirectAttackActionCount = 0;
        acceptedDirectAttackTargetCount = 0;
        lastAcceptedDirectAttackTargetCount = 0;
        directAttackRecoveryRejectCount = 0;
        directAttackCooldownRejectCount = 0;
        directAttackAfterlagMovementStartCount = 0;
        directAttackAfterlagMovementRejectCount = 0;
        directAttackArcRejectedTargetCount = 0;
        lastDirectAttackArcRejectedTargetCount = 0;
        directAttackFacingSnapshotCount = 0;
        directAttackSameFrameTurnCount = 0;
        directAttackObservedFacingMask = 0;
        directAttackVisualFacingSyncCount = 0;
        lastDirectAttackStartedAt = -1f;
        directAttackRecoveryEndsAt = -1f;
        nextDirectAttackReadyAt = -1f;
        lastDirectAttackFacing = movement != null
            ? movement.CurrentFacing
            : CharacterFacingDirection.South;
        lastDirectAttackAimDirection = movement != null
            ? movement.FacingVector
            : Vector2.down;
        mousePushInputCount = 0;
        primaryKeyPushInputCount = 0;
        secondaryKeyPushInputCount = 0;
        mousePushStartedActionCount = 0;
        primaryKeyPushStartedActionCount = 0;
        secondaryKeyPushStartedActionCount = 0;
        mousePushSuccessfulActionCount = 0;
        primaryKeyPushSuccessfulActionCount = 0;
        secondaryKeyPushSuccessfulActionCount = 0;
        coalescedPushInputFrameCount = 0;
        directAttackInputFrameCount = 0;
        mouseDirectAttackInputCount = 0;
        primaryKeyDirectAttackInputCount = 0;
        secondaryKeyDirectAttackInputCount = 0;
        coalescedDirectAttackInputFrameCount = 0;
        reservedDirectAttackRequestCount = 0;
        executedDirectAttackActionCount = 0;
        mouseDirectAttackStartedActionCount = 0;
        primaryKeyDirectAttackStartedActionCount = 0;
        secondaryKeyDirectAttackStartedActionCount = 0;
        mouseDirectAttackSuccessfulActionCount = 0;
        primaryKeyDirectAttackSuccessfulActionCount = 0;
        secondaryKeyDirectAttackSuccessfulActionCount = 0;
        simultaneousActionInputFrameCount = 0;
        simultaneousPushPriorityResolutionCount = 0;
        simultaneousDirectPriorityResolutionCount = 0;
        simultaneousRejectBothCount = 0;
        suppressedPushInputFrameCount = 0;
        suppressedDirectAttackInputFrameCount = 0;
        pushBlockedByDirectRecoveryCount = 0;
        directBlockedByPushRecoveryCount = 0;
        pushCancelledDirectRecoveryCount = 0;
        directCancelledPushRecoveryCount = 0;
        pushStartedWhileDirectCooldownActiveCount = 0;
        directStartedWhilePushCooldownActiveCount = 0;
        dualActionStartFrameViolationCount = 0;
        lastPushActionStartFrame = -1;
        lastDirectActionStartFrame = -1;
        lastStartedActionKind = CombatActionKind.Unspecified;

        initialized =
            movement != null &&
            body != null &&
            health != null &&
            nonlethalPushSettings != null &&
            directAttackSettings != null &&
            inputBindings != null &&
            actionArbitrationSettings != null;
    }

    public void SetCombatInputEnabled(bool shouldEnable)
    {
        combatInputEnabled = initialized && shouldEnable;
    }

    /// <summary>
    /// Allocates one id for one complete action. All targets hit by that
    /// action receive this same id.
    /// </summary>
    public CombatAttackId IssueAttackId()
    {
        return IssueAttackId(CombatActionKind.Unspecified);
    }

    private CombatAttackId IssueAttackId(CombatActionKind actionKind)
    {
        if (!initialized)
        {
            return default(CombatAttackId);
        }

        lastIssuedAttackId = CombatAttackIdGenerator.Next();
        issuedAttackCount++;

        if (actionKind == CombatActionKind.NonlethalPush)
        {
            issuedNonlethalPushAttackCount++;
        }
        else if (actionKind == CombatActionKind.DirectAttack)
        {
            issuedDirectAttackCount++;
        }

        return lastIssuedAttackId;
    }

    private CombatInputFrame ReadNonlethalPushInputs()
    {
        return new CombatInputFrame(
            inputBindings.EnableMouseNonlethalPush &&
            Input.GetMouseButtonDown(0),
            IsKeyPressedThisFrame(
                inputBindings.NonlethalPushPrimaryKey),
            IsKeyPressedThisFrame(
                inputBindings.NonlethalPushSecondaryKey));
    }

    private CombatInputFrame ReadDirectAttackInputs()
    {
        return new CombatInputFrame(
            inputBindings.EnableMouseDirectAttack &&
            Input.GetMouseButtonDown(1),
            IsKeyPressedThisFrame(
                inputBindings.DirectAttackPrimaryKey),
            IsKeyPressedThisFrame(
                inputBindings.DirectAttackSecondaryKey));
    }

    private void ResolveCombatInputFrame(
        CombatInputFrame pushInput,
        CombatInputFrame directInput)
    {
        RecordPushInputFrame(pushInput);
        RecordDirectAttackInputFrame(directInput);

        if (!pushInput.AnyPressed && !directInput.AnyPressed)
        {
            return;
        }

        if (pushInput.AnyPressed && directInput.AnyPressed &&
            actionArbitrationSettings != null &&
            actionArbitrationSettings.Enabled)
        {
            simultaneousActionInputFrameCount++;

            switch (actionArbitrationSettings.SimultaneousInputPolicy)
            {
                case SimultaneousCombatActionPolicy.PreferNonlethalPush:
                    simultaneousPushPriorityResolutionCount++;
                    suppressedDirectAttackInputFrameCount++;
                    ProcessNonlethalPushInput(pushInput);
                    return;

                case SimultaneousCombatActionPolicy.PreferDirectAttack:
                    simultaneousDirectPriorityResolutionCount++;
                    suppressedPushInputFrameCount++;
                    ProcessDirectAttackInput(directInput);
                    return;

                case SimultaneousCombatActionPolicy.RejectBoth:
                    simultaneousRejectBothCount++;
                    suppressedPushInputFrameCount++;
                    suppressedDirectAttackInputFrameCount++;
                    return;
            }
        }

        // Legacy/disabled arbitration retains deterministic processing order.
        // With CB8 enabled, only one of these paths is reached per dual frame.
        if (directInput.AnyPressed)
        {
            ProcessDirectAttackInput(directInput);
        }

        if (pushInput.AnyPressed)
        {
            ProcessNonlethalPushInput(pushInput);
        }
    }

    private void RecordPushInputFrame(CombatInputFrame inputFrame)
    {
        if (!inputFrame.AnyPressed)
        {
            return;
        }

        if (inputFrame.MousePressed)
        {
            mousePushInputCount++;
        }

        if (inputFrame.PrimaryPressed)
        {
            primaryKeyPushInputCount++;
        }

        if (inputFrame.SecondaryPressed)
        {
            secondaryKeyPushInputCount++;
        }

        if (inputFrame.SourceCount > 1)
        {
            coalescedPushInputFrameCount++;
        }
    }

    private void RecordDirectAttackInputFrame(CombatInputFrame inputFrame)
    {
        if (!inputFrame.AnyPressed)
        {
            return;
        }

        directAttackInputFrameCount++;

        if (inputFrame.MousePressed)
        {
            mouseDirectAttackInputCount++;
        }

        if (inputFrame.PrimaryPressed)
        {
            primaryKeyDirectAttackInputCount++;
        }

        if (inputFrame.SecondaryPressed)
        {
            secondaryKeyDirectAttackInputCount++;
        }

        if (inputFrame.SourceCount > 1)
        {
            coalescedDirectAttackInputFrameCount++;
        }
    }

    private void ProcessNonlethalPushInput(CombatInputFrame inputFrame)
    {
        int startedBefore = startedLeftPushActionCount;
        bool successful = TryPerformNonlethalPush();
        bool actionStarted = startedLeftPushActionCount > startedBefore;

        if (actionStarted)
        {
            if (inputFrame.MousePressed)
            {
                mousePushStartedActionCount++;
            }

            if (inputFrame.PrimaryPressed)
            {
                primaryKeyPushStartedActionCount++;
            }

            if (inputFrame.SecondaryPressed)
            {
                secondaryKeyPushStartedActionCount++;
            }
        }

        if (!successful)
        {
            return;
        }

        if (inputFrame.MousePressed)
        {
            mousePushSuccessfulActionCount++;
        }

        if (inputFrame.PrimaryPressed)
        {
            primaryKeyPushSuccessfulActionCount++;
        }

        if (inputFrame.SecondaryPressed)
        {
            secondaryKeyPushSuccessfulActionCount++;
        }
    }

    private void ProcessDirectAttackInput(CombatInputFrame inputFrame)
    {
        int startedBefore = startedDirectAttackActionCount;
        bool successful = TryPerformDirectAttack();
        bool actionStarted =
            startedDirectAttackActionCount > startedBefore;

        if (actionStarted)
        {
            if (inputFrame.MousePressed)
            {
                mouseDirectAttackStartedActionCount++;
            }

            if (inputFrame.PrimaryPressed)
            {
                primaryKeyDirectAttackStartedActionCount++;
            }

            if (inputFrame.SecondaryPressed)
            {
                secondaryKeyDirectAttackStartedActionCount++;
            }
        }

        if (!successful)
        {
            return;
        }

        if (inputFrame.MousePressed)
        {
            mouseDirectAttackSuccessfulActionCount++;
        }

        if (inputFrame.PrimaryPressed)
        {
            primaryKeyDirectAttackSuccessfulActionCount++;
        }

        if (inputFrame.SecondaryPressed)
        {
            secondaryKeyDirectAttackSuccessfulActionCount++;
        }
    }

    /// <summary>
    /// Executes the facing-based damage action. CB7 adds a small collision-safe
    /// nudge and Hit pause, while explicitly keeping nonlethal knockback decay
    /// and post-knockback pursuit recovery isolated.
    /// </summary>
    public bool TryPerformDirectAttack()
    {
        if (!initialized ||
            !combatInputEnabled ||
            directAttackSettings == null ||
            !directAttackSettings.Enabled ||
            body == null ||
            health == null ||
            health.IsDead)
        {
            return false;
        }

        reservedDirectAttackRequestCount++;

        float actionStartedAt = Time.time;

        if (directAttackRecoveryEndsAt >= 0f &&
            actionStartedAt < directAttackRecoveryEndsAt)
        {
            directAttackRecoveryRejectCount++;
            return false;
        }

        if (nextDirectAttackReadyAt >= 0f &&
            actionStartedAt < nextDirectAttackReadyAt)
        {
            directAttackCooldownRejectCount++;
            return false;
        }

        if (!ResolveDirectAttackAgainstPushRecovery(actionStartedAt))
        {
            return false;
        }

        if (nextLeftPushReadyAt >= 0f &&
            actionStartedAt < nextLeftPushReadyAt)
        {
            directStartedWhilePushCooldownActiveCount++;
        }

        BeginDirectAttackActionTiming(actionStartedAt);

        bool facingChangedThisFrame =
            movement.RefreshInputAndFacingForCombat();

        Vector2 origin = body.position;
        CharacterFacingDirection actionFacing = movement.CurrentFacing;
        Vector2 aimDirection = movement.FacingVector;

        if (aimDirection.sqrMagnitude < MinimumAimMagnitude)
        {
            aimDirection = Vector2.down;
            actionFacing = CharacterFacingDirection.South;
        }

        aimDirection.Normalize();
        lastDirectAttackAimDirection = aimDirection;
        lastDirectAttackFacing = actionFacing;
        directAttackFacingSnapshotCount++;
        directAttackObservedFacingMask |= 1 << (int)actionFacing;

        if (facingChangedThisFrame)
        {
            directAttackSameFrameTurnCount++;
        }

        if (visualAnimator != null)
        {
            visualAnimator.SetFacingDirection(actionFacing);
            directAttackVisualFacingSyncCount++;
        }

        CombatActionAnimationRequested?.Invoke(
            this,
            CombatActionKind.DirectAttack,
            actionFacing);

        CollectDirectAttackCandidates(origin, aimDirection);

        CombatAttackId attackId = IssueAttackId(
            CombatActionKind.DirectAttack);

        int acceptedTargetCount = 0;
        int targetLimit = Mathf.Min(
            directAttackSettings.MaximumTargets,
            directAttackCandidates.Count);

        for (int i = 0; i < targetLimit; i++)
        {
            PushCandidate candidate = directAttackCandidates[i];

            if (candidate.Receiver == null ||
                candidate.Collider == null)
            {
                continue;
            }

            Vector2 targetPosition =
                candidate.Receiver.Motor != null &&
                candidate.Receiver.Motor.Body != null
                    ? candidate.Receiver.Motor.Body.position
                    : (Vector2)candidate.Receiver.transform.position;

            Vector2 hitDirection = targetPosition - origin;

            if (hitDirection.sqrMagnitude < MinimumAimMagnitude)
            {
                hitDirection = aimDirection;
            }

            hitDirection.Normalize();

            CombatDisplacementRequest weakDisplacement =
                default(CombatDisplacementRequest);

            if (directAttackSettings.WeakDisplacementDistance > 0f)
            {
                weakDisplacement = new CombatDisplacementRequest(
                    attackId,
                    hitDirection,
                    directAttackSettings.WeakDisplacementDistance,
                    directAttackSettings.WeakDisplacementDuration,
                    shouldCancelTimedNavigationSpeed: false);
            }

            CombatReactionRequest weakReaction =
                default(CombatReactionRequest);

            if (directAttackSettings.WeakHitPauseDuration > 0f)
            {
                weakReaction = new CombatReactionRequest(
                    attackId,
                    CombatReactionKind.Hit,
                    directAttackSettings.WeakHitPauseDuration,
                    shouldExtendExistingReaction: false,
                    shouldCancelTimedNavigationSpeed: false,
                    newReason: "CB7 direct attack weak hit reaction");
            }

            CombatHit hit = new CombatHit(
                attackId,
                CombatActionKind.DirectAttack,
                gameObject,
                DamageFaction.Player,
                DamageAttribution.Player,
                candidate.HitPoint,
                hitDirection,
                directAttackSettings.Damage,
                weakDisplacement,
                weakReaction,
                shouldCountTowardKnockbackDecay: false,
                shouldTriggerPursuitRecovery: false);

            if (candidate.Receiver.TryReceiveCombatHit(hit))
            {
                acceptedTargetCount++;
            }
        }

        lastAcceptedDirectAttackTargetCount = acceptedTargetCount;
        acceptedDirectAttackTargetCount += acceptedTargetCount;

        if (acceptedTargetCount > 0)
        {
            successfulDirectAttackActionCount++;
            return true;
        }

        return false;
    }

    private static bool IsKeyPressedThisFrame(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    public bool TryPerformNonlethalPush()
    {
        if (!initialized ||
            !combatInputEnabled ||
            nonlethalPushSettings == null ||
            !nonlethalPushSettings.Enabled ||
            body == null ||
            health == null ||
            health.IsDead)
        {
            return false;
        }

        leftPushInputCount++;

        float actionStartedAt = Time.time;

        if (actionRecoveryEndsAt >= 0f &&
            actionStartedAt < actionRecoveryEndsAt)
        {
            recoveryRejectedLeftPushCount++;
            return false;
        }

        if (nextLeftPushReadyAt >= 0f &&
            actionStartedAt < nextLeftPushReadyAt)
        {
            cooldownRejectedLeftPushCount++;
            return false;
        }

        if (!ResolvePushAgainstDirectAttackRecovery(actionStartedAt))
        {
            return false;
        }

        if (nextDirectAttackReadyAt >= 0f &&
            actionStartedAt < nextDirectAttackReadyAt)
        {
            pushStartedWhileDirectCooldownActiveCount++;
        }

        BeginLeftPushActionTiming(actionStartedAt);

        bool facingChangedThisFrame =
            movement.RefreshInputAndFacingForCombat();

        Vector2 origin = body.position;
        CharacterFacingDirection actionFacing = movement.CurrentFacing;
        Vector2 aimDirection = movement.FacingVector;

        if (aimDirection.sqrMagnitude < MinimumAimMagnitude)
        {
            aimDirection = Vector2.down;
            actionFacing = CharacterFacingDirection.South;
        }

        aimDirection.Normalize();
        lastAimDirection = aimDirection;
        lastActionFacing = actionFacing;
        facingBasedActionCount++;
        observedActionFacingMask |= 1 << (int)actionFacing;

        if (facingChangedThisFrame)
        {
            sameFrameTurnActionCount++;
        }

        if (visualAnimator != null)
        {
            visualAnimator.SetFacingDirection(actionFacing);
            visualFacingSyncCount++;
        }

        CombatActionAnimationRequested?.Invoke(
            this,
            CombatActionKind.NonlethalPush,
            actionFacing);

        CollectPushCandidates(origin, aimDirection);

        CombatAttackId attackId = IssueAttackId(
            CombatActionKind.NonlethalPush);
        int acceptedTargetCount = 0;
        int targetLimit = Mathf.Min(
            nonlethalPushSettings.MaximumTargets,
            pushCandidates.Count);

        for (int i = 0; i < targetLimit; i++)
        {
            PushCandidate candidate = pushCandidates[i];

            if (candidate.Receiver == null ||
                candidate.Collider == null)
            {
                continue;
            }

            Vector2 targetPosition =
                candidate.Receiver.Motor != null &&
                candidate.Receiver.Motor.Body != null
                    ? candidate.Receiver.Motor.Body.position
                    : (Vector2)candidate.Receiver.transform.position;

            Vector2 pushDirection = targetPosition - origin;

            if (pushDirection.sqrMagnitude < MinimumAimMagnitude)
            {
                pushDirection = aimDirection;
            }

            pushDirection.Normalize();

            CombatDisplacementRequest displacement =
                new CombatDisplacementRequest(
                    attackId,
                    pushDirection,
                    nonlethalPushSettings.DisplacementDistance,
                    nonlethalPushSettings.DisplacementDuration);

            CombatReactionRequest reaction =
                new CombatReactionRequest(
                    attackId,
                    CombatReactionKind.Hit,
                    newDuration: 0f,
                    shouldExtendExistingReaction: false,
                    newReason: "CB4 facing push: pause resolved per EnemyDefinition");

            CombatHit hit = new CombatHit(
                attackId,
                CombatActionKind.NonlethalPush,
                gameObject,
                DamageFaction.Player,
                DamageAttribution.Player,
                candidate.HitPoint,
                pushDirection,
                newDamage: 0f,
                newDisplacement: displacement,
                newReaction: reaction,
                shouldCountTowardKnockbackDecay: true,
                shouldTriggerPursuitRecovery: true);

            if (candidate.Receiver.TryReceiveCombatHit(hit))
            {
                acceptedTargetCount++;
            }
        }

        lastAcceptedTargetCount = acceptedTargetCount;
        acceptedLeftPushTargetCount += acceptedTargetCount;

        if (acceptedTargetCount > 0)
        {
            successfulLeftPushActionCount++;
            return true;
        }

        return false;
    }


    private bool ResolveDirectAttackAgainstPushRecovery(float now)
    {
        if (actionArbitrationSettings == null ||
            !actionArbitrationSettings.Enabled ||
            actionRecoveryEndsAt < 0f ||
            now >= actionRecoveryEndsAt)
        {
            return true;
        }

        if (actionArbitrationSettings.DirectAttackDuringPushRecovery ==
            CrossActionRecoveryPolicy.CancelCurrentRecovery)
        {
            CancelPushRecoveryForArbitration();
            directCancelledPushRecoveryCount++;
            return true;
        }

        directBlockedByPushRecoveryCount++;
        return false;
    }

    private bool ResolvePushAgainstDirectAttackRecovery(float now)
    {
        if (actionArbitrationSettings == null ||
            !actionArbitrationSettings.Enabled ||
            directAttackRecoveryEndsAt < 0f ||
            now >= directAttackRecoveryEndsAt)
        {
            return true;
        }

        if (actionArbitrationSettings.PushDuringDirectAttackRecovery ==
            CrossActionRecoveryPolicy.CancelCurrentRecovery)
        {
            CancelDirectAttackRecoveryForArbitration();
            pushCancelledDirectRecoveryCount++;
            return true;
        }

        pushBlockedByDirectRecoveryCount++;
        return false;
    }

    private void CancelPushRecoveryForArbitration()
    {
        actionRecoveryEndsAt = -1f;

        if (movement != null)
        {
            movement.ClearTimedMovementScale(countCompletion: false);
        }
    }

    private void CancelDirectAttackRecoveryForArbitration()
    {
        directAttackRecoveryEndsAt = -1f;

        if (movement != null)
        {
            movement.ClearTimedMovementScale(countCompletion: false);
        }
    }

    private void RecordActionStart(CombatActionKind actionKind)
    {
        int frame = Time.frameCount;

        if (actionKind == CombatActionKind.NonlethalPush)
        {
            if (lastDirectActionStartFrame == frame)
            {
                dualActionStartFrameViolationCount++;
            }

            lastPushActionStartFrame = frame;
        }
        else if (actionKind == CombatActionKind.DirectAttack)
        {
            if (lastPushActionStartFrame == frame)
            {
                dualActionStartFrameViolationCount++;
            }

            lastDirectActionStartFrame = frame;
        }

        lastStartedActionKind = actionKind;
    }

    private void BeginDirectAttackActionTiming(float startedAt)
    {
        startedDirectAttackActionCount++;
        executedDirectAttackActionCount++;
        RecordActionStart(CombatActionKind.DirectAttack);
        lastDirectAttackStartedAt = startedAt;
        directAttackRecoveryEndsAt =
            startedAt + directAttackSettings.AfterlagDuration;
        nextDirectAttackReadyAt =
            startedAt + directAttackSettings.CooldownDuration;

        if (movement == null)
        {
            directAttackAfterlagMovementRejectCount++;
            return;
        }

        bool movementAccepted =
            movement.TryBeginTimedMovementScale(
                directAttackSettings.AfterlagMovementMultiplier,
                directAttackSettings.AfterlagDuration,
                replaceExisting: true);

        if (movementAccepted)
        {
            directAttackAfterlagMovementStartCount++;
        }
        else
        {
            directAttackAfterlagMovementRejectCount++;
        }
    }

    private void BeginLeftPushActionTiming(float startedAt)
    {
        startedLeftPushActionCount++;
        RecordActionStart(CombatActionKind.NonlethalPush);
        lastLeftPushStartedAt = startedAt;
        actionRecoveryEndsAt =
            startedAt + nonlethalPushSettings.AfterlagDuration;
        nextLeftPushReadyAt =
            startedAt + nonlethalPushSettings.CooldownDuration;

        if (movement == null)
        {
            afterlagMovementRejectCount++;
            return;
        }

        bool movementAccepted =
            movement.TryBeginTimedMovementScale(
                nonlethalPushSettings.AfterlagMovementMultiplier,
                nonlethalPushSettings.AfterlagDuration,
                replaceExisting: true);

        if (movementAccepted)
        {
            afterlagMovementStartCount++;
        }
        else
        {
            afterlagMovementRejectCount++;
        }
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        if (!initialized)
        {
            errors.Add(
                gameObject.name +
                ": PlayerCombatController is not initialized.");
        }

        if (movement == null)
        {
            errors.Add(
                gameObject.name +
                ": combat controller has no RuntimeDungeonPlayer.");
        }

        if (body == null)
        {
            errors.Add(
                gameObject.name +
                ": combat controller has no Rigidbody2D.");
        }

        if (health == null)
        {
            errors.Add(
                gameObject.name +
                ": combat controller has no Health reference.");
        }

        if (nonlethalPushSettings == null)
        {
            errors.Add(
                gameObject.name +
                ": Nonlethal Push settings are missing.");
        }
        else
        {
            nonlethalPushSettings.CollectValidationErrors(
                errors,
                gameObject.name);
        }

        if (directAttackSettings == null)
        {
            errors.Add(
                gameObject.name +
                ": Direct Attack settings are missing.");
        }
        else
        {
            directAttackSettings.CollectValidationErrors(
                errors,
                gameObject.name);
        }

        if (inputBindings == null)
        {
            errors.Add(
                gameObject.name +
                ": Player Combat Input Bindings are missing.");
        }
        else
        {
            inputBindings.CollectValidationErrors(
                errors,
                gameObject.name);
        }

        if (actionArbitrationSettings == null)
        {
            errors.Add(
                gameObject.name +
                ": Action Arbitration settings are missing.");
        }
        else
        {
            actionArbitrationSettings.CollectValidationErrors(
                errors,
                gameObject.name);
        }
    }

    private void CollectDirectAttackCandidates(
        Vector2 origin,
        Vector2 aimDirection)
    {
        uniqueReceivers.Clear();
        directAttackCandidates.Clear();
        lastDirectAttackArcRejectedTargetCount = 0;

        int overlapCount = Physics2D.OverlapCircle(
            origin,
            directAttackSettings.Range,
            combatQueryFilter,
            overlapResults);

        float minimumDot = directAttackSettings.ArcAngle >= 359.9f
            ? -1f
            : Mathf.Cos(
                directAttackSettings.HalfArcAngle *
                Mathf.Deg2Rad);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D candidateCollider = overlapResults[i];

            if (candidateCollider == null ||
                candidateCollider.isTrigger)
            {
                continue;
            }

            EnemyCombatReceiver receiver =
                candidateCollider.GetComponentInParent<
                    EnemyCombatReceiver>();

            if (receiver == null ||
                !receiver.IsInitialized ||
                receiver.Health == null ||
                receiver.Health.IsDead ||
                uniqueReceivers.Contains(receiver))
            {
                continue;
            }

            uniqueReceivers.Add(receiver);

            Vector2 targetCenter = receiver.Motor != null &&
                                   receiver.Motor.Body != null
                ? receiver.Motor.Body.position
                : (Vector2)receiver.transform.position;

            Vector2 toTarget = targetCenter - origin;

            if (toTarget.sqrMagnitude < MinimumAimMagnitude)
            {
                toTarget = aimDirection;
            }

            Vector2 targetDirection = toTarget.normalized;

            if (Vector2.Dot(aimDirection, targetDirection) <
                minimumDot)
            {
                lastDirectAttackArcRejectedTargetCount++;
                directAttackArcRejectedTargetCount++;
                continue;
            }

            Vector2 hitPoint = candidateCollider.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, hitPoint);

            if (distance > directAttackSettings.Range + 0.001f)
            {
                continue;
            }

            if (!HasClearLineToTarget(
                    origin,
                    candidateCollider,
                    receiver))
            {
                continue;
            }

            directAttackCandidates.Add(
                new PushCandidate(
                    receiver,
                    candidateCollider,
                    hitPoint,
                    distance));
        }

        directAttackCandidates.Sort(
            (first, second) =>
                first.Distance.CompareTo(second.Distance));
    }

    private void CollectPushCandidates(
        Vector2 origin,
        Vector2 aimDirection)
    {
        uniqueReceivers.Clear();
        pushCandidates.Clear();
        lastArcRejectedTargetCount = 0;

        int overlapCount = Physics2D.OverlapCircle(
            origin,
            nonlethalPushSettings.Range,
            combatQueryFilter,
            overlapResults);

        float minimumDot = nonlethalPushSettings.ArcAngle >= 359.9f
            ? -1f
            : Mathf.Cos(
                nonlethalPushSettings.HalfArcAngle *
                Mathf.Deg2Rad);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D candidateCollider = overlapResults[i];

            if (candidateCollider == null ||
                candidateCollider.isTrigger)
            {
                continue;
            }

            EnemyCombatReceiver receiver =
                candidateCollider.GetComponentInParent<
                    EnemyCombatReceiver>();

            if (receiver == null ||
                !receiver.IsInitialized ||
                receiver.Health == null ||
                receiver.Health.IsDead ||
                uniqueReceivers.Contains(receiver))
            {
                continue;
            }

            // Mark the receiver before fan rejection so an enemy with more
            // than one collider contributes at most one accepted or rejected
            // target to this action's diagnostics.
            uniqueReceivers.Add(receiver);

            Vector2 targetCenter = receiver.Motor != null &&
                                   receiver.Motor.Body != null
                ? receiver.Motor.Body.position
                : (Vector2)receiver.transform.position;

            Vector2 toTarget = targetCenter - origin;

            if (toTarget.sqrMagnitude < MinimumAimMagnitude)
            {
                toTarget = aimDirection;
            }

            Vector2 targetDirection = toTarget.normalized;

            if (Vector2.Dot(aimDirection, targetDirection) <
                minimumDot)
            {
                lastArcRejectedTargetCount++;
                arcRejectedTargetCount++;
                continue;
            }

            Vector2 hitPoint = candidateCollider.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, hitPoint);

            if (distance > nonlethalPushSettings.Range + 0.001f)
            {
                continue;
            }

            if (!HasClearLineToTarget(
                    origin,
                    candidateCollider,
                    receiver))
            {
                continue;
            }

            pushCandidates.Add(
                new PushCandidate(
                    receiver,
                    candidateCollider,
                    hitPoint,
                    distance));
        }

        pushCandidates.Sort(
            (first, second) =>
                first.Distance.CompareTo(second.Distance));
    }

    private bool HasClearLineToTarget(
        Vector2 origin,
        Collider2D targetCollider,
        EnemyCombatReceiver receiver)
    {
        Vector2 targetPoint = targetCollider.ClosestPoint(origin);
        Vector2 line = targetPoint - origin;
        float distance = line.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        int hitCount = Physics2D.Raycast(
            origin,
            line / distance,
            combatQueryFilter,
            lineOfSightResults,
            distance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider =
                lineOfSightResults[i].collider;

            if (hitCollider == null ||
                hitCollider.isTrigger ||
                IsColliderOwnedBy(hitCollider, transform) ||
                IsColliderOwnedBy(hitCollider, receiver.transform))
            {
                continue;
            }

            Rigidbody2D attachedBody =
                hitCollider.attachedRigidbody;

            if (attachedBody == null ||
                attachedBody.bodyType != RigidbodyType2D.Dynamic)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsColliderOwnedBy(
        Collider2D collider,
        Transform ownerRoot)
    {
        if (collider == null || ownerRoot == null)
        {
            return false;
        }

        Transform colliderTransform = collider.transform;
        return colliderTransform == ownerRoot ||
               colliderTransform.IsChildOf(ownerRoot);
    }

    private void CacheComponents()
    {
        if (movement == null)
        {
            movement = GetComponent<RuntimeDungeonPlayer>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (visualAnimator == null)
        {
            visualAnimator =
                GetComponent<DirectionalSpriteAnimator>();
        }

    }

    private void ConfigureQueryFilter()
    {
        combatQueryFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = Physics2D.AllLayers,
            useTriggers = false
        };
    }

    public bool HasObservedActionFacing(
        CharacterFacingDirection facing)
    {
        return (observedActionFacingMask & (1 << (int)facing)) != 0;
    }

    private static int CountObservedFacingDirections(int mask)
    {
        int count = 0;

        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                count++;
            }
        }

        return count;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = body != null
            ? body.position
            : (Vector2)transform.position;

        if (nonlethalPushSettings != null)
        {
            Vector2 pushDirection = lastAimDirection.sqrMagnitude >
                                    MinimumAimMagnitude
                ? lastAimDirection.normalized
                : Vector2.down;

            DrawFanGizmo(
                origin,
                pushDirection,
                nonlethalPushSettings.Range,
                nonlethalPushSettings.HalfArcAngle,
                new Color(0.2f, 0.9f, 1f, 0.8f));
        }

        if (directAttackSettings != null)
        {
            Vector2 attackDirection =
                lastDirectAttackAimDirection.sqrMagnitude >
                MinimumAimMagnitude
                    ? lastDirectAttackAimDirection.normalized
                    : Vector2.down;

            DrawFanGizmo(
                origin,
                attackDirection,
                directAttackSettings.Range,
                directAttackSettings.HalfArcAngle,
                new Color(1f, 0.35f, 0.25f, 0.8f));
        }
    }

    private static void DrawFanGizmo(
        Vector2 origin,
        Vector2 direction,
        float range,
        float halfArcAngle,
        Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, range);

        Vector2 leftBoundary = (Vector2)(Quaternion.Euler(
            0f,
            0f,
            halfArcAngle) * (Vector3)direction);

        Vector2 rightBoundary = (Vector2)(Quaternion.Euler(
            0f,
            0f,
            -halfArcAngle) * (Vector3)direction);

        Gizmos.DrawLine(
            origin,
            origin + leftBoundary * range);

        Gizmos.DrawLine(
            origin,
            origin + rightBoundary * range);
    }

    private readonly struct CombatInputFrame
    {
        public readonly bool MousePressed;
        public readonly bool PrimaryPressed;
        public readonly bool SecondaryPressed;

        public CombatInputFrame(
            bool mousePressed,
            bool primaryPressed,
            bool secondaryPressed)
        {
            MousePressed = mousePressed;
            PrimaryPressed = primaryPressed;
            SecondaryPressed = secondaryPressed;
        }

        public bool AnyPressed =>
            MousePressed || PrimaryPressed || SecondaryPressed;

        public int SourceCount =>
            (MousePressed ? 1 : 0) +
            (PrimaryPressed ? 1 : 0) +
            (SecondaryPressed ? 1 : 0);
    }

    private readonly struct PushCandidate
    {
        public readonly EnemyCombatReceiver Receiver;
        public readonly Collider2D Collider;
        public readonly Vector2 HitPoint;
        public readonly float Distance;

        public PushCandidate(
            EnemyCombatReceiver receiver,
            Collider2D collider,
            Vector2 hitPoint,
            float distance)
        {
            Receiver = receiver;
            Collider = collider;
            HitPoint = hitPoint;
            Distance = distance;
        }
    }
}
