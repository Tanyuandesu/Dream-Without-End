using UnityEngine;

/// <summary>
/// Session-local CB6 observations that survive enemy destruction until Play
/// Mode ends. They are reset at SubsystemRegistration, including projects
/// with Enter Play Mode Options and domain reload disabled.
/// </summary>
public static class EnemyDeathLifecycleDiagnostics
{
    public static int ProcessedDeathCount { get; private set; }
    public static int PlayerAttributedDeathCount { get; private set; }
    public static int OtherAttributedDeathCount { get; private set; }
    public static int ContactDamageShutdownCount { get; private set; }
    public static int ColliderShutdownCount { get; private set; }
    public static int PhysicsShutdownCount { get; private set; }
    public static int AiShutdownCount { get; private set; }
    public static int LifecycleViolationCount { get; private set; }

    public static string LastInstanceId { get; private set; }
    public static DamageAttribution LastAttribution { get; private set; }
    public static bool LastStateWasDead { get; private set; }
    public static bool LastMotorWasInactive { get; private set; }
    public static bool LastReactionWasInactive { get; private set; }
    public static bool LastContactDamageWasDisabled { get; private set; }
    public static bool LastCollidersWereDisabled { get; private set; }
    public static bool LastPhysicsWasDisabled { get; private set; }
    public static bool LastAiWasDisabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ProcessedDeathCount = 0;
        PlayerAttributedDeathCount = 0;
        OtherAttributedDeathCount = 0;
        ContactDamageShutdownCount = 0;
        ColliderShutdownCount = 0;
        PhysicsShutdownCount = 0;
        AiShutdownCount = 0;
        LifecycleViolationCount = 0;

        LastInstanceId = string.Empty;
        LastAttribution = DamageAttribution.Unspecified;
        LastStateWasDead = false;
        LastMotorWasInactive = false;
        LastReactionWasInactive = false;
        LastContactDamageWasDisabled = false;
        LastCollidersWereDisabled = false;
        LastPhysicsWasDisabled = false;
        LastAiWasDisabled = false;
    }

    public static void Record(
        string instanceId,
        DamageAttribution attribution,
        bool stateWasDead,
        bool motorWasInactive,
        bool reactionWasInactive,
        bool contactDamageWasDisabled,
        bool collidersWereDisabled,
        bool physicsWasDisabled,
        bool aiWasDisabled)
    {
        ProcessedDeathCount++;

        if (attribution == DamageAttribution.Player)
        {
            PlayerAttributedDeathCount++;
        }
        else
        {
            OtherAttributedDeathCount++;
        }

        if (contactDamageWasDisabled)
        {
            ContactDamageShutdownCount++;
        }

        if (collidersWereDisabled)
        {
            ColliderShutdownCount++;
        }

        if (physicsWasDisabled)
        {
            PhysicsShutdownCount++;
        }

        if (aiWasDisabled)
        {
            AiShutdownCount++;
        }

        bool valid =
            stateWasDead &&
            motorWasInactive &&
            reactionWasInactive &&
            contactDamageWasDisabled &&
            collidersWereDisabled &&
            physicsWasDisabled &&
            aiWasDisabled;

        if (!valid)
        {
            LifecycleViolationCount++;
        }

        LastInstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? "unknown_enemy"
            : instanceId;

        LastAttribution = attribution;
        LastStateWasDead = stateWasDead;
        LastMotorWasInactive = motorWasInactive;
        LastReactionWasInactive = reactionWasInactive;
        LastContactDamageWasDisabled = contactDamageWasDisabled;
        LastCollidersWereDisabled = collidersWereDisabled;
        LastPhysicsWasDisabled = physicsWasDisabled;
        LastAiWasDisabled = aiWasDisabled;
    }
}
