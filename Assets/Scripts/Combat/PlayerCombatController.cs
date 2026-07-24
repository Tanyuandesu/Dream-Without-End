using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-side combat boundary.
/// CB1 enables only the left-mouse nonlethal fan push. It deals zero damage,
/// aims toward the mouse in world space, rejects targets hidden behind solid
/// geometry and delegates every enemy movement to EnemyMotor2D.
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

    [Header("CB1 left-mouse nonlethal push")]
    [SerializeField] private NonlethalPushSettings nonlethalPushSettings =
        NonlethalPushSettings.CreateDefault();

    [Header("Runtime references")]
    [SerializeField] private RuntimeDungeonPlayer movement;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Health health;
    [SerializeField] private DirectionalSpriteAnimator visualAnimator;
    [SerializeField] private Camera worldCamera;

    [Header("Runtime diagnostics")]
    [SerializeField] private int issuedAttackCount;
    [SerializeField] private CombatAttackId lastIssuedAttackId;
    [SerializeField] private int leftPushInputCount;
    [SerializeField] private int successfulLeftPushActionCount;
    [SerializeField] private int acceptedLeftPushTargetCount;
    [SerializeField] private int lastAcceptedTargetCount;
    [SerializeField] private Vector2 lastAimDirection = Vector2.down;
    [SerializeField] private Vector2 lastMouseWorldPosition;

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

    public int IssuedAttackCount => issuedAttackCount;
    public CombatAttackId LastIssuedAttackId => lastIssuedAttackId;
    public int LeftPushInputCount => leftPushInputCount;
    public int SuccessfulLeftPushActionCount =>
        successfulLeftPushActionCount;

    public int AcceptedLeftPushTargetCount =>
        acceptedLeftPushTargetCount;

    public int LastAcceptedTargetCount => lastAcceptedTargetCount;
    public Vector2 LastAimDirection => lastAimDirection;
    public Vector2 LastMouseWorldPosition => lastMouseWorldPosition;

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
            nonlethalPushSettings == null ||
            !nonlethalPushSettings.Enabled)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPerformNonlethalPush();
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
            NonlethalPushSettings.CreateDefault());
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator,
        NonlethalPushSettings newNonlethalPushSettings)
    {
        movement = newMovement;
        body = newBody;
        health = newHealth;
        visualAnimator = newVisualAnimator;
        nonlethalPushSettings = newNonlethalPushSettings != null
            ? newNonlethalPushSettings.CreateRuntimeCopy()
            : NonlethalPushSettings.CreateDefault();

        CacheComponents();
        ConfigureQueryFilter();

        combatInputEnabled = false;
        issuedAttackCount = 0;
        lastIssuedAttackId = default(CombatAttackId);
        leftPushInputCount = 0;
        successfulLeftPushActionCount = 0;
        acceptedLeftPushTargetCount = 0;
        lastAcceptedTargetCount = 0;
        lastAimDirection = Vector2.down;
        lastMouseWorldPosition = body != null
            ? body.position
            : (Vector2)transform.position;

        initialized =
            movement != null &&
            body != null &&
            health != null &&
            nonlethalPushSettings != null;
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

        Vector2 origin = body.position;
        Vector2 aimDirection = ResolveMouseAimDirection(origin);
        lastAimDirection = aimDirection;

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
                    nonlethalPushSettings.ReactionDuration,
                    shouldExtendExistingReaction: false,
                    newReason: "CB1 nonlethal left-mouse push");

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
                shouldCountTowardKnockbackDecay: false,
                shouldTriggerPursuitRecovery: false);

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
    }

    private void CollectPushCandidates(
        Vector2 origin,
        Vector2 aimDirection)
    {
        uniqueReceivers.Clear();
        pushCandidates.Clear();

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

            uniqueReceivers.Add(receiver);

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

    private Vector2 ResolveMouseAimDirection(Vector2 origin)
    {
        CacheCamera();

        if (worldCamera == null)
        {
            return lastAimDirection.sqrMagnitude >
                   MinimumAimMagnitude
                ? lastAimDirection.normalized
                : Vector2.down;
        }

        Vector3 mouseWorld = worldCamera.ScreenToWorldPoint(
            Input.mousePosition);

        lastMouseWorldPosition = new Vector2(
            mouseWorld.x,
            mouseWorld.y);

        Vector2 aim = lastMouseWorldPosition - origin;

        if (aim.sqrMagnitude < MinimumAimMagnitude)
        {
            return lastAimDirection.sqrMagnitude >
                   MinimumAimMagnitude
                ? lastAimDirection.normalized
                : Vector2.down;
        }

        return aim.normalized;
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

        CacheCamera();
    }

    private void CacheCamera()
    {
        if (worldCamera != null && worldCamera.isActiveAndEnabled)
        {
            return;
        }

        worldCamera = Camera.main;

        if (worldCamera == null)
        {
            Camera[] cameras =
                Resources.FindObjectsOfTypeAll<Camera>();

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];

                if (candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.isActiveAndEnabled)
                {
                    worldCamera = candidate;
                    break;
                }
            }
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
