using UnityEngine;

/// <summary>
/// Session-local CB9 combat observations that survive enemy death and floor
/// replacement. This is diagnostics only; it never changes combat outcomes.
/// </summary>
public static class CombatSystemDiagnostics
{
    public static int AcceptedHitCount { get; private set; }
    public static int NonlethalPushHitCount { get; private set; }
    public static int DirectAttackHitCount { get; private set; }
    public static int UnclassifiedHitCount { get; private set; }

    public static int NonlethalDamagePayloadViolationCount
    {
        get;
        private set;
    }

    public static int NonlethalAcceptedDamageViolationCount
    {
        get;
        private set;
    }

    public static int DirectAttackDamageHitCount { get; private set; }
    public static float DirectAttackAcceptedDamage { get; private set; }
    public static int DirectAttackWeakDisplacementCount { get; private set; }
    public static int DirectAttackWeakReactionCount { get; private set; }
    public static int DirectAttackPayloadViolationCount { get; private set; }
    public static int DirectAttackDecayIsolationViolationCount
    {
        get;
        private set;
    }

    public static int DirectAttackPursuitIsolationViolationCount
    {
        get;
        private set;
    }

    public static CombatAttackId LastAcceptedAttackId { get; private set; }
    public static CombatActionKind LastAcceptedActionKind { get; private set; }
    public static float LastAcceptedDamage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        AcceptedHitCount = 0;
        NonlethalPushHitCount = 0;
        DirectAttackHitCount = 0;
        UnclassifiedHitCount = 0;
        NonlethalDamagePayloadViolationCount = 0;
        NonlethalAcceptedDamageViolationCount = 0;
        DirectAttackDamageHitCount = 0;
        DirectAttackAcceptedDamage = 0f;
        DirectAttackWeakDisplacementCount = 0;
        DirectAttackWeakReactionCount = 0;
        DirectAttackPayloadViolationCount = 0;
        DirectAttackDecayIsolationViolationCount = 0;
        DirectAttackPursuitIsolationViolationCount = 0;
        LastAcceptedAttackId = default(CombatAttackId);
        LastAcceptedActionKind = CombatActionKind.Unspecified;
        LastAcceptedDamage = 0f;
    }

    public static void RecordAcceptedHit(
        CombatHit hit,
        bool damageAccepted,
        float acceptedDamage,
        bool displacementAccepted,
        bool reactionAccepted,
        bool directPayloadViolation,
        bool directDecayIsolationViolation,
        bool directPursuitIsolationViolation)
    {
        AcceptedHitCount++;
        LastAcceptedAttackId = hit.AttackId;
        LastAcceptedActionKind = hit.ActionKind;
        LastAcceptedDamage = Mathf.Max(0f, acceptedDamage);

        switch (hit.ActionKind)
        {
            case CombatActionKind.NonlethalPush:
                NonlethalPushHitCount++;

                if (hit.HasDamage || hit.Damage > 0f)
                {
                    NonlethalDamagePayloadViolationCount++;
                }

                if (damageAccepted || acceptedDamage > 0.0001f)
                {
                    NonlethalAcceptedDamageViolationCount++;
                }
                break;

            case CombatActionKind.DirectAttack:
                DirectAttackHitCount++;

                if (damageAccepted)
                {
                    DirectAttackDamageHitCount++;
                    DirectAttackAcceptedDamage += Mathf.Max(
                        0f,
                        acceptedDamage);
                }

                if (displacementAccepted)
                {
                    DirectAttackWeakDisplacementCount++;
                }

                if (reactionAccepted)
                {
                    DirectAttackWeakReactionCount++;
                }

                if (directPayloadViolation)
                {
                    DirectAttackPayloadViolationCount++;
                }

                if (directDecayIsolationViolation)
                {
                    DirectAttackDecayIsolationViolationCount++;
                }

                if (directPursuitIsolationViolation)
                {
                    DirectAttackPursuitIsolationViolationCount++;
                }
                break;

            default:
                UnclassifiedHitCount++;
                break;
        }
    }
}
