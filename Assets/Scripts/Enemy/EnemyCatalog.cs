using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative list of enemy types allowed by the current game build.
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyCatalog",
    menuName = "Dream Dungeon/Enemy System/Enemy Catalog")]
public sealed class EnemyCatalog : ScriptableObject
{
    [SerializeField] private List<EnemyDefinition> definitions =
        new List<EnemyDefinition>();

    [NonSerialized] private Dictionary<EnemyId, EnemyDefinition> lookup;

    public IReadOnlyList<EnemyDefinition> Definitions => definitions;
    public int Count => definitions != null ? definitions.Count : 0;

    public bool Contains(EnemyDefinition definition)
    {
        return definition != null &&
               definitions != null &&
               definitions.Contains(definition);
    }

    public bool TryGet(EnemyId enemyId, out EnemyDefinition definition)
    {
        EnsureLookup();
        return lookup.TryGetValue(enemyId, out definition);
    }

    public bool TryGet(string enemyId, out EnemyDefinition definition)
    {
        return TryGet(EnemyId.From(enemyId), out definition);
    }

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        if (definitions == null || definitions.Count == 0)
        {
            errors.Add(name + ": catalog contains no Enemy Definitions.");
            return;
        }

        HashSet<EnemyId> usedIds = new HashSet<EnemyId>();

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null)
            {
                errors.Add(name + ": definition slot " + i + " is null.");
                continue;
            }

            definition.CollectValidationErrors(errors);

            if (!definition.Id.IsValid)
            {
                continue;
            }

            if (!usedIds.Add(definition.Id))
            {
                errors.Add(
                    name + ": duplicate EnemyId '" + definition.Id + "'.");
            }
        }
    }

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    private void EnsureLookup()
    {
        if (lookup == null)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        lookup = new Dictionary<EnemyId, EnemyDefinition>();

        if (definitions == null)
        {
            return;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null || !definition.Id.IsValid)
            {
                continue;
            }

            if (!lookup.ContainsKey(definition.Id))
            {
                lookup.Add(definition.Id, definition);
            }
        }
    }
}
