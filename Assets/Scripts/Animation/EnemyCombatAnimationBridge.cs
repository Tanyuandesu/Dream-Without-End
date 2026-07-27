using UnityEngine;

/// <summary>
/// CB10A enemy presentation adapter.
/// DirectAttack accepted response -> Hurt.
/// NonlethalPush accepted response -> Special (strong knockback/stun pose).
/// Death -> a visual-only echo, so the existing synchronous death cleanup and
/// manager bookkeeping remain unchanged while the death animation is visible.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyCombatReceiver))]
[RequireComponent(typeof(Health))]
public sealed class EnemyCombatAnimationBridge : MonoBehaviour
{
    [SerializeField] private EnemyCombatReceiver combatReceiver;
    [SerializeField] private Health health;
    [SerializeField] private EnemyVisual visual;
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private bool initialized;

    [Header("CB10A runtime diagnostics")]
    [SerializeField] private int weakHitAnimationCount;
    [SerializeField] private int strongHitAnimationCount;
    [SerializeField] private int deathEchoCount;
    [SerializeField] private int missingSequenceCount;

    private bool subscribed;

    public bool IsInitialized => initialized;
    public int WeakHitAnimationCount => weakHitAnimationCount;
    public int StrongHitAnimationCount => strongHitAnimationCount;
    public int DeathEchoCount => deathEchoCount;
    public int MissingSequenceCount => missingSequenceCount;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        EnemyCombatReceiver newCombatReceiver,
        Health newHealth,
        EnemyVisual newVisual)
    {
        Unsubscribe();
        combatReceiver = newCombatReceiver;
        health = newHealth;
        visual = newVisual;
        animator = visual != null ? visual.Animator : null;
        CacheComponents();
        initialized = combatReceiver != null &&
                      health != null &&
                      visual != null &&
                      animator != null;
        Subscribe();
    }

    private void CacheComponents()
    {
        if (combatReceiver == null)
        {
            combatReceiver = GetComponent<EnemyCombatReceiver>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (visual == null)
        {
            visual = GetComponent<EnemyVisual>();
        }

        if (animator == null && visual != null)
        {
            animator = visual.Animator;
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        if (combatReceiver != null)
        {
            combatReceiver.CombatAnimationHitAccepted +=
                HandleCombatAnimationHitAccepted;
        }

        if (health != null)
        {
            health.Died += HandleDied;
        }

        subscribed = combatReceiver != null || health != null;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (combatReceiver != null)
        {
            combatReceiver.CombatAnimationHitAccepted -=
                HandleCombatAnimationHitAccepted;
        }

        if (health != null)
        {
            health.Died -= HandleDied;
        }

        subscribed = false;
    }

    private void HandleCombatAnimationHitAccepted(
        EnemyCombatReceiver source,
        CombatAnimationHitEvent presentationEvent)
    {
        if (!initialized || animator == null ||
            health == null || health.IsDead)
        {
            return;
        }

        CombatActionKind actionKind = presentationEvent.Hit.ActionKind;

        if (actionKind == CombatActionKind.NonlethalPush &&
            presentationEvent.HasVisibleKnockback)
        {
            bool played = animator.PlayAction(
                CharacterAnimationState.Special,
                restart: true,
                returnToLocomotion: true);

            if (played) strongHitAnimationCount++;
            else missingSequenceCount++;

            CombatAnimationDiagnostics.RecordEnemyStrongHit(played);
            return;
        }

        if (actionKind == CombatActionKind.DirectAttack &&
            presentationEvent.DamageAccepted &&
            !presentationEvent.DirectReactionSuppressed)
        {
            bool played = animator.PlayAction(
                CharacterAnimationState.Hurt,
                restart: true,
                returnToLocomotion: true);

            if (played) weakHitAnimationCount++;
            else missingSequenceCount++;

            CombatAnimationDiagnostics.RecordEnemyWeakHit(played);
        }
    }

    private void HandleDied(Health deadHealth)
    {
        SpawnDeathEcho();
    }

    private void SpawnDeathEcho()
    {
        if (animator == null || visual == null ||
            visual.Renderer == null || animator.Profile == null)
        {
            missingSequenceCount++;
            CombatAnimationDiagnostics.RecordDeathEchoSpawned(false);
            return;
        }

        CharacterFacingDirection facing = animator.Facing;

        if (!animator.Profile.HasSequence(
                CharacterAnimationState.Death,
                facing))
        {
            missingSequenceCount++;
            CombatAnimationDiagnostics.RecordDeathEchoSpawned(false);
            return;
        }

        SpriteRenderer sourceRenderer = visual.Renderer;
        GameObject echo = new GameObject(
            gameObject.name + "_CB10A_DeathEcho");
        echo.transform.position = sourceRenderer.transform.position;
        echo.transform.rotation = Quaternion.identity;
        echo.transform.localScale = Vector3.one;

        SpriteRenderer echoRenderer =
            echo.AddComponent<SpriteRenderer>();
        echoRenderer.color = sourceRenderer.color;
        echoRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        echoRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        echoRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

        float worldHeight = Mathf.Max(
            0.05f,
            sourceRenderer.bounds.size.y);

        DirectionalSpriteAnimator echoAnimator =
            echo.AddComponent<DirectionalSpriteAnimator>();
        echoAnimator.Initialize(
            animator.Profile,
            echoRenderer,
            worldHeight);
        echoAnimator.SetFacingDirection(facing);

        bool played = echoAnimator.PlayAction(
            CharacterAnimationState.Death,
            restart: true,
            returnToLocomotion: false);

        if (!played)
        {
            Destroy(echo);
            missingSequenceCount++;
            CombatAnimationDiagnostics.RecordDeathEchoSpawned(false);
            return;
        }

        float duration = Mathf.Max(
            0.1f,
            echoAnimator.GetActionDuration(
                CharacterAnimationState.Death,
                facing));

        TemporaryDeathAnimationEcho lifetime =
            echo.AddComponent<TemporaryDeathAnimationEcho>();
        lifetime.Initialize(duration + 0.05f);

        deathEchoCount++;
        CombatAnimationDiagnostics.RecordDeathEchoSpawned(true);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
