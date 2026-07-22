using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyDangerStage
{
    Initial = 0,
    Uneasy = 1,
    Dangerous = 2,
    HighPressure = 3,
    Final = 4
}

/// <summary>
/// External room-definition layer. Multiple bindings may reuse the same
/// visual DreamRoomTemplate while carrying different encounter identities.
/// </summary>
[Serializable]
public sealed class RoomEncounterBinding
{
    [SerializeField] private string roomDefinitionId =
        "room_definition_unassigned";

    [SerializeField] private DreamRoomTemplate visualTemplate;
    [SerializeField] private EnemyDangerStage minimumDangerStage;
    [SerializeField] private EnemyDangerStage maximumDangerStage =
        EnemyDangerStage.Final;

    [Min(1)]
    [SerializeField] private int weight = 1;

    [SerializeField] private EnemyEncounterProfile encounterProfile;

    public string RoomDefinitionId => roomDefinitionId;
    public DreamRoomTemplate VisualTemplate => visualTemplate;
    public EnemyDangerStage MinimumDangerStage => minimumDangerStage;
    public EnemyDangerStage MaximumDangerStage => maximumDangerStage;
    public int Weight => Mathf.Max(1, weight);
    public EnemyEncounterProfile EncounterProfile => encounterProfile;

    public bool Supports(EnemyDangerStage dangerStage)
    {
        return dangerStage >= minimumDangerStage &&
               dangerStage <= maximumDangerStage;
    }
}

[CreateAssetMenu(
    fileName = "RoomEncounterCatalog",
    menuName = "Dream Dungeon/Enemy System/Room Encounter Catalog")]
public sealed class RoomEncounterCatalog : ScriptableObject
{
    [Tooltip(
        "EA1 may remain empty. EA6 will bind real room definitions after " +
        "room difficulty and encounter probabilities are authored.")]
    [SerializeField] private List<RoomEncounterBinding> bindings =
        new List<RoomEncounterBinding>();

    public IReadOnlyList<RoomEncounterBinding> Bindings => bindings;

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null || bindings == null)
        {
            return;
        }

        HashSet<string> identities =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < bindings.Count; i++)
        {
            RoomEncounterBinding binding = bindings[i];

            if (binding == null)
            {
                errors.Add(name + ": binding slot " + i + " is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.RoomDefinitionId))
            {
                errors.Add(
                    name + ": binding " + i + " has no Room Definition Id.");
            }
            else if (!identities.Add(binding.RoomDefinitionId))
            {
                errors.Add(
                    name + ": duplicate Room Definition Id '" +
                    binding.RoomDefinitionId + "'.");
            }

            if (binding.VisualTemplate == null)
            {
                errors.Add(
                    name + ": binding '" + binding.RoomDefinitionId +
                    "' has no visual DreamRoomTemplate.");
            }

            if (binding.EncounterProfile == null)
            {
                errors.Add(
                    name + ": binding '" + binding.RoomDefinitionId +
                    "' has no Encounter Profile.");
            }

            if (binding.MinimumDangerStage >
                binding.MaximumDangerStage)
            {
                errors.Add(
                    name + ": binding '" + binding.RoomDefinitionId +
                    "' has an inverted Danger Stage range.");
            }
        }
    }
}
