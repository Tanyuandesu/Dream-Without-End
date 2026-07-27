using UnityEngine;

/// <summary>
/// CB6 synchronous enemy-death seal.
///
/// Health still owns the death event and end-of-frame destruction;
/// EnemyStateMachine still owns the Dead transition; EnemyManager still owns
/// run-record attribution and active-list removal. This component only closes
/// runtime hazards between Died and Destroy by disabling contact damage,
/// collision, physics simulation and remaining AI updates.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EnemyStateMachine))]
[RequireComponent(typeof(EnemyMotor2D))]
public sealed class EnemyDeathLifecycle : MonoBehaviour
{
    [Header("Runtime references")]
    [SerializeField] private Health health;
    [SerializeField] private EnemyRuntimeIdentity identity;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private EnemyMotor2D motor;
    [SerializeField] private EnemyNavigationAgent navigationAgent;
    [SerializeField] private EnemyDetection detection;
    [SerializeField] private EnemyCombatReceiver combatReceiver;
    [SerializeField] private TestEnemyAI legacyBridge;
    [SerializeField] private Rigidbody2D body;

    [Header("CB6 death state (read only during Play Mode)")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool deathProcessed;
    [SerializeField] private float deathProcessedAt = -1f;
    [SerializeField] private DamageAttribution deathAttribution;
    [SerializeField] private bool contactDamageDisabled;
    [SerializeField] private bool collidersDisabled;
    [SerializeField] private bool physicsDisabled;
    [SerializeField] private bool aiDisabled;

    private bool subscribed;

    public bool IsInitialized => initialized;
    public bool DeathProcessed => deathProcessed;
    public float DeathProcessedAt => deathProcessedAt;
    public DamageAttribution DeathAttribution => deathAttribution;
    public bool ContactDamageDisabled => contactDamageDisabled;
    public bool CollidersDisabled => collidersDisabled;
    public bool PhysicsDisabled => physicsDisabled;
    public bool AiDisabled => aiDisabled;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        Health newHealth,
        EnemyRuntimeIdentity newIdentity,
        EnemyStateMachine newStateMachine,
        EnemyMotor2D newMotor,
        EnemyNavigationAgent newNavigationAgent,
        EnemyDetection newDetection,
        EnemyCombatReceiver newCombatReceiver,
        TestEnemyAI newLegacyBridge)
    {
        Unsubscribe();

        health = newHealth;
        identity = newIdentity;
        stateMachine = newStateMachine;
        motor = newMotor;
        navigationAgent = newNavigationAgent;
        detection = newDetection;
        combatReceiver = newCombatReceiver;
        legacyBridge = newLegacyBridge;

        CacheComponents();

        deathProcessed = false;
        deathProcessedAt = -1f;
        deathAttribution = DamageAttribution.Unspecified;
        contactDamageDisabled = false;
        collidersDisabled = false;
        physicsDisabled = false;
        aiDisabled = false;

        initialized =
            health != null &&
            stateMachine != null &&
            motor != null &&
            body != null;

        Subscribe();

        if (initialized && health.IsDead)
        {
            HandleDied(health);
        }
    }

    private void CacheComponents()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (identity == null)
        {
            identity = GetComponent<EnemyRuntimeIdentity>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<EnemyStateMachine>();
        }

        if (motor == null)
        {
            motor = GetComponent<EnemyMotor2D>();
        }

        if (navigationAgent == null)
        {
            navigationAgent = GetComponent<EnemyNavigationAgent>();
        }

        if (detection == null)
        {
            detection = GetComponent<EnemyDetection>();
        }

        if (combatReceiver == null)
        {
            combatReceiver = GetComponent<EnemyCombatReceiver>();
        }

        if (legacyBridge == null)
        {
            legacyBridge = GetComponent<TestEnemyAI>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void Subscribe()
    {
        if (subscribed || health == null)
        {
            return;
        }

        health.Died += HandleDied;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || health == null)
        {
            subscribed = false;
            return;
        }

        health.Died -= HandleDied;
        subscribed = false;
    }

    private void HandleDied(Health deadHealth)
    {
        if (deathProcessed)
        {
            return;
        }

        deathProcessed = true;
        deathProcessedAt = Time.time;
        deathAttribution = deadHealth != null &&
                           deadHealth.HasLastAcceptedDamage
            ? deadHealth.LastAcceptedDamage.ResolvedAttribution
            : DamageAttribution.Other;

        DisableContactDamage();
        DisableColliders();
        DisablePhysics();
        DisableRemainingAiUpdates();

        bool stateWasDead =
            stateMachine != null &&
            stateMachine.CurrentState == EnemyRuntimeState.Dead;

        bool motorWasInactive =
            motor == null ||
            (!motor.IsCombatDisplacementActive &&
             !motor.IsTimedNavigationSpeedActive &&
             !motor.HasMovementIntent);

        bool reactionWasInactive =
            stateMachine == null ||
            !stateMachine.IsCombatReactionActive;

        EnemyDeathLifecycleDiagnostics.Record(
            identity != null ? identity.InstanceId : gameObject.name,
            deathAttribution,
            stateWasDead,
            motorWasInactive,
            reactionWasInactive,
            contactDamageDisabled,
            collidersDisabled,
            physicsDisabled,
            aiDisabled);
    }

    private void DisableContactDamage()
    {
        ContactDamage2D[] damageSources =
            GetComponents<ContactDamage2D>();

        contactDamageDisabled = true;

        for (int i = 0; i < damageSources.Length; i++)
        {
            ContactDamage2D source = damageSources[i];

            if (source == null)
            {
                continue;
            }

            source.DisableForSourceDeath();

            if (source.enabled)
            {
                contactDamageDisabled = false;
            }
        }
    }

    private void DisableColliders()
    {
        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>(true);

        collidersDisabled = true;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider == null)
            {
                continue;
            }

            collider.enabled = false;

            if (collider.enabled)
            {
                collidersDisabled = false;
            }
        }
    }

    private void DisablePhysics()
    {
        physicsDisabled = body == null;

        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
        physicsDisabled = !body.simulated;
    }

    private void DisableRemainingAiUpdates()
    {
        if (detection != null)
        {
            detection.enabled = false;
        }

        if (navigationAgent != null)
        {
            navigationAgent.enabled = false;
        }

        if (combatReceiver != null)
        {
            combatReceiver.enabled = false;
        }

        if (legacyBridge != null)
        {
            legacyBridge.enabled = false;
        }

        aiDisabled =
            (detection == null || !detection.enabled) &&
            (navigationAgent == null || !navigationAgent.enabled) &&
            (combatReceiver == null || !combatReceiver.enabled) &&
            (legacyBridge == null || !legacyBridge.enabled);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
