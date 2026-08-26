using System;
using UnityEngine;

/// <summary>
/// Stable, serialization-friendly identity for one enemy type.
/// Display names may change; this value must not change after content ships.
/// </summary>
[Serializable]
public struct EnemyId : IEquatable<EnemyId>
{
    [SerializeField] private string value;

    public string Value => value ?? string.Empty;
    public bool IsValid => IsValidValue(value);

    public EnemyId(string rawValue)
    {
        value = Normalize(rawValue);
    }

    public static EnemyId From(string rawValue)
    {
        return new EnemyId(rawValue);
    }

    public static bool IsValidValue(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string trimmed = candidate.Trim();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char character = trimmed[i];

            if (char.IsLetterOrDigit(character) ||
                character == '_' ||
                character == '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static string Normalize(string rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : rawValue.Trim().ToLowerInvariant();
    }

    public bool Equals(EnemyId other)
    {
        return string.Equals(
            Value,
            other.Value,
            StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is EnemyId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }

    public override string ToString()
    {
        return Value;
    }

    public static bool operator ==(EnemyId left, EnemyId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EnemyId left, EnemyId right)
    {
        return !left.Equals(right);
    }
}
