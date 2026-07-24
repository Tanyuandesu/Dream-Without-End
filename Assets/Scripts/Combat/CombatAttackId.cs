using System;
using UnityEngine;

/// <summary>
/// Session-local identity for one combat action.
/// Every target hit by the same swing or push receives the same id, allowing
/// receivers to reject duplicate collider hits without coupling to input code.
/// </summary>
[Serializable]
public struct CombatAttackId : IEquatable<CombatAttackId>
{
    [SerializeField] private int value;

    public int Value => value;
    public bool IsValid => value > 0;

    public CombatAttackId(int newValue)
    {
        value = Mathf.Max(0, newValue);
    }

    public bool Equals(CombatAttackId other)
    {
        return value == other.value;
    }

    public override bool Equals(object obj)
    {
        return obj is CombatAttackId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return value;
    }

    public override string ToString()
    {
        return IsValid ? value.ToString() : "Invalid";
    }

    public static bool operator ==(
        CombatAttackId left,
        CombatAttackId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        CombatAttackId left,
        CombatAttackId right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Produces unique action ids for the current Play Mode session.
/// Resetting on SubsystemRegistration keeps disabled-domain-reload sessions
/// deterministic and prevents old ids leaking into the next run.
/// </summary>
public static class CombatAttackIdGenerator
{
    private static int nextValue = 1;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        nextValue = 1;
    }

    public static CombatAttackId Next()
    {
        if (nextValue <= 0)
        {
            nextValue = 1;
        }

        CombatAttackId result =
            new CombatAttackId(nextValue);

        unchecked
        {
            nextValue++;
        }

        if (nextValue <= 0)
        {
            nextValue = 1;
        }

        return result;
    }
}
