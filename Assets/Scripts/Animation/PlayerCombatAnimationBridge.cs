using UnityEngine;

/// <summary>
/// CB10A player presentation adapter. The combat controller emits one action
/// signal after arbitration/timing accepts an action. This bridge translates
/// that signal into the existing sprite-profile states:
/// DirectAttack -> Attack, NonlethalPush -> Special.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombatController))]
[RequireComponent(typeof(DirectionalSpriteAnimator))]
public sealed class PlayerCombatAnimationBridge : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private bool initialized;
    [SerializeField] private int handledActionCount;
    [SerializeField] private int playedActionCount;
    [SerializeField] private int missingSequenceCount;

    private bool subscribed;

    public bool IsInitialized => initialized;
    public int HandledActionCount => handledActionCount;
    public int PlayedActionCount => playedActionCount;
    public int MissingSequenceCount => missingSequenceCount;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        PlayerCombatController newCombatController,
        DirectionalSpriteAnimator newAnimator)
    {
        Unsubscribe();
        combatController = newCombatController;
        animator = newAnimator;
        CacheComponents();
        initialized = combatController != null && animator != null;
        Subscribe();
    }

    private void CacheComponents()
    {
        if (combatController == null)
        {
            combatController = GetComponent<PlayerCombatController>();
        }

        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }
    }

    private void Subscribe()
    {
        if (subscribed || combatController == null)
        {
            return;
        }

        combatController.CombatActionAnimationRequested +=
            HandleCombatActionAnimationRequested;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || combatController == null)
        {
            subscribed = false;
            return;
        }

        combatController.CombatActionAnimationRequested -=
            HandleCombatActionAnimationRequested;
        subscribed = false;
    }

    private void HandleCombatActionAnimationRequested(
        PlayerCombatController source,
        CombatActionKind actionKind,
        CharacterFacingDirection facing)
    {
        if (!initialized || animator == null)
        {
            CombatAnimationDiagnostics.RecordPlayerAction(
                actionKind,
                false);
            return;
        }

        CharacterAnimationState state;

        if (actionKind == CombatActionKind.DirectAttack)
        {
            state = CharacterAnimationState.Attack;
        }
        else if (actionKind == CombatActionKind.NonlethalPush)
        {
            state = CharacterAnimationState.Special;
        }
        else
        {
            return;
        }

        handledActionCount++;
        animator.SetFacingDirection(facing);
        bool played = animator.PlayAction(
            state,
            restart: true,
            returnToLocomotion: true);

        if (played) playedActionCount++;
        else missingSequenceCount++;

        CombatAnimationDiagnostics.RecordPlayerAction(
            actionKind,
            played);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
