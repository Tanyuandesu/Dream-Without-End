using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-side combat boundary.
/// CB4.5 keeps the complete CB4 nonlethal-push pipeline and adds configurable
/// mouse/keyboard bindings. Every push input enters the same action method.
/// Direct-attack inputs are captured through a reserved common entry point,
/// but do not yet issue attack ids, damage, cooldown or recovery.
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

    [Header("CB4.5 mouse and keyboard input bindings")]
    [SerializeField] private PlayerCombatInputBindings inputBindings =
        PlayerCombatInputBindings.CreateDefault();

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

    private ContactFilter2D combatQueryFilter;

    private readonly Collider2D[] overlapResults =
        new Collider2D[QueryResultCapacity];

    private readonly RaycastHit2D[] lineOfSightResults =
        new RaycastHit2D[QueryResultCapacity];

    private readonly HashSet<EnemyCombatReceiver> uniqueReceivers =
        new HashSet<EnemyCombatReceiver>();

    private readonly List<PushCandidate> pushCandidates =
        new List<PushCandidate>(16);

    public bool IsInitialized => initialized;
    public bool CombatInputEnabled => combatInputEnabled;
    public RuntimeDungeonPlayer Movement => movement;
    public Rigidbody2D Body => body;
    public Health Health => health;
    public DirectionalSpriteAnimator VisualAnimator => visualAnimator;
    public NonlethalPushSettings PushSettings =>
        nonlethalPushSettings;
    public PlayerCombatInputBindings InputBindings => inputBindings;

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

    private void Awake()
    {
        CacheComponents();
        ConfigureQueryFilter();
    }

    private void Update()
    {
        if (!initialized ||
            !combatInputEnabled ||
            health == null ||
            health.IsDead ||
            inputBindings == null)
        {
            return;
        }

        CaptureReservedDirectAttackInputs();

        if (nonlethalPushSettings != null &&
            nonlethalPushSettings.Enabled)
        {
            CaptureNonlethalPushInputs();
        }
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
            PlayerCombatInputBindings.CreateDefault());
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
            PlayerCombatInputBindings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings,
        PlayerCombatInputBindings newInputBindings)
    {
        movement = newMovement;
        body = newBody;
        health = newHealth;
        visualAnimator = newVisualAnimator;
        nonlethalPushSettings = newNonlethalPushSettings != null
            ? newNonlethalPushSettings.CreateRuntimeCopy()
            : NonlethalPushSettings.CreateDefault();
        inputBindings = newInputBindings != null
            ? newInputBindings.CreateRuntimeCopy()
            : PlayerCombatInputBindings.CreateDefault();

        CacheComponents();
        ConfigureQueryFilter();

        combatInputEnabled = false;
        issuedAttackCount = 0;
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

        initialized =
            movement != null &&
            body != null &&
            health != null &&
            nonlethalPushSettings != null &&
            inputBindings != null;
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
        if (!initialized)
        {
            return default(CombatAttackId);
        }

        lastIssuedAttackId = CombatAttackIdGenerator.Next();
        issuedAttackCount++;
        return lastIssuedAttackId;
    }

    private void CaptureNonlethalPushInputs()
    {
        bool mousePressed =
            inputBindings.EnableMouseNonlethalPush &&
            Input.GetMouseButtonDown(0);

        bool primaryPressed = IsKeyPressedThisFrame(
            inputBindings.NonlethalPushPrimaryKey);

        bool secondaryPressed = IsKeyPressedThisFrame(
            inputBindings.NonlethalPushSecondaryKey);

        int sourceCount =
            (mousePressed ? 1 : 0) +
            (primaryPressed ? 1 : 0) +
            (secondaryPressed ? 1 : 0);

        if (sourceCount <= 0)
        {
            return;
        }

        if (mousePressed)
        {
            mousePushInputCount++;
        }

        if (primaryPressed)
        {
            primaryKeyPushInputCount++;
        }

        if (secondaryPressed)
        {
            secondaryKeyPushInputCount++;
        }

        if (sourceCount > 1)
        {
            coalescedPushInputFrameCount++;
        }

        int startedBefore = startedLeftPushActionCount;
        bool successful = TryPerformNonlethalPush();
        bool actionStarted = startedLeftPushActionCount > startedBefore;

        if (actionStarted)
        {
            if (mousePressed)
            {
                mousePushStartedActionCount++;
            }

            if (primaryPressed)
            {
                primaryKeyPushStartedActionCount++;
            }

            if (secondaryPressed)
            {
                secondaryKeyPushStartedActionCount++;
            }
        }

        if (!successful)
        {
            return;
        }

        if (mousePressed)
        {
            mousePushSuccessfulActionCount++;
        }

        if (primaryPressed)
        {
            primaryKeyPushSuccessfulActionCount++;
        }

        if (secondaryPressed)
        {
            secondaryKeyPushSuccessfulActionCount++;
        }
    }

    private void CaptureReservedDirectAttackInputs()
    {
        bool mousePressed =
            inputBindings.EnableMouseDirectAttack &&
            Input.GetMouseButtonDown(1);

        bool primaryPressed = IsKeyPressedThisFrame(
            inputBindings.DirectAttackPrimaryKey);

        bool secondaryPressed = IsKeyPressedThisFrame(
            inputBindings.DirectAttackSecondaryKey);

        int sourceCount =
            (mousePressed ? 1 : 0) +
            (primaryPressed ? 1 : 0) +
            (secondaryPressed ? 1 : 0);

        if (sourceCount <= 0)
        {
            return;
        }

        directAttackInputFrameCount++;

        if (mousePressed)
        {
            mouseDirectAttackInputCount++;
        }

        if (primaryPressed)
        {
            primaryKeyDirectAttackInputCount++;
        }

        if (secondaryPressed)
        {
            secondaryKeyDirectAttackInputCount++;
        }

        if (sourceCount > 1)
        {
            coalescedDirectAttackInputFrameCount++;
        }

        TryPerformDirectAttack();
    }

    /// <summary>
    /// Stable direct-attack entry point reserved by CB4.5. The next combat
    /// phase will implement damage through this method. Until then, requests
    /// are observed without issuing attack ids or changing action timing.
    /// </summary>
    public bool TryPerformDirectAttack()
    {
        if (!initialized ||
            !combatInputEnabled ||
            body == null ||
            health == null ||
            health.IsDead)
        {
            return false;
        }

        reservedDirectAttackRequestCount++;
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

        CollectPushCandidates(origin, aimDirection);

        CombatAttackId attackId = IssueAttackId();
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


    private void BeginLeftPushActionTiming(float startedAt)
    {
        startedLeftPushActionCount++;
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
        NonlethalPushSettings settings = nonlethalPushSettings;

        if (settings == null)
        {
            return;
        }

        Vector2 origin = body != null
            ? body.position
            : (Vector2)transform.position;

        Vector2 direction = lastAimDirection.sqrMagnitude >
                            MinimumAimMagnitude
            ? lastAimDirection.normalized
            : Vector2.down;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(origin, settings.Range);

        Vector2 leftBoundary = (Vector2)(Quaternion.Euler(
            0f,
            0f,
            settings.HalfArcAngle) * (Vector3)direction);

        Vector2 rightBoundary = (Vector2)(Quaternion.Euler(
            0f,
            0f,
            -settings.HalfArcAngle) * (Vector3)direction);

        Gizmos.DrawLine(
            origin,
            origin + leftBoundary * settings.Range);

        Gizmos.DrawLine(
            origin,
            origin + rightBoundary * settings.Range);
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
