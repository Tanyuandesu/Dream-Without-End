using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-side combat boundary.
/// CB0 installs and validates the references but deliberately performs no
/// input or hit detection, preserving the accepted baseline hand feel.
/// Later phases add left/right mouse actions inside this component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public sealed class PlayerCombatController : MonoBehaviour
{
    [Header("CB0 contract state")]
    [SerializeField] private bool initialized;

    [Tooltip(
        "CB0 keeps this disabled. A later combat phase enables input after " +
        "the action definitions and hit detection are installed.")]
    [SerializeField] private bool combatInputEnabled;

    [Header("Runtime references")]
    [SerializeField] private RuntimeDungeonPlayer movement;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Health health;
    [SerializeField] private DirectionalSpriteAnimator visualAnimator;

    [Header("Runtime diagnostics")]
    [SerializeField] private int issuedAttackCount;
    [SerializeField] private CombatAttackId lastIssuedAttackId;

    public bool IsInitialized => initialized;
    public bool CombatInputEnabled => combatInputEnabled;
    public RuntimeDungeonPlayer Movement => movement;
    public Rigidbody2D Body => body;
    public Health Health => health;
    public DirectionalSpriteAnimator VisualAnimator => visualAnimator;
    public int IssuedAttackCount => issuedAttackCount;
    public CombatAttackId LastIssuedAttackId => lastIssuedAttackId;

    private void Awake()
    {
        CacheComponents();
    }

    public void Initialize(
        RuntimeDungeonPlayer newMovement,
        Rigidbody2D newBody,
        Health newHealth,
        DirectionalSpriteAnimator newVisualAnimator)
    {
        movement = newMovement;
        body = newBody;
        health = newHealth;
        visualAnimator = newVisualAnimator;

        CacheComponents();

        combatInputEnabled = false;
        issuedAttackCount = 0;
        lastIssuedAttackId = default(CombatAttackId);

        initialized =
            movement != null &&
            body != null &&
            health != null;
    }

    public void SetCombatInputEnabled(bool shouldEnable)
    {
        combatInputEnabled = initialized && shouldEnable;
    }

    /// <summary>
    /// Allocates one id for one complete action. All targets hit by that
    /// action must receive this same id.
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
}
