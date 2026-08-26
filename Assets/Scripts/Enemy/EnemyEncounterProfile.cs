using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyEncounterDensity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Fixed = 3
}

[Serializable]
public sealed class EnemyEncounterMember
{
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Min(1)]
    [SerializeField] private int count = 1;

    [Tooltip(
        "Optional semantic spawn group such as Door, Center or LongSight. " +
        "Empty means any legal Enemy spawn point in the room.")]
    [SerializeField] private string spawnPointGroup = string.Empty;

    public EnemyDefinition EnemyDefinition => enemyDefinition;
    public int Count => Mathf.Max(1, count);
    public string SpawnPointGroup => spawnPointGroup;
}

[Serializable]
public sealed class WeightedEnemyDefinition
{
    [SerializeField] private EnemyDefinition enemyDefinition;

    [Min(1)]
    [SerializeField] private int weight = 1;

    public EnemyDefinition EnemyDefinition => enemyDefinition;
    public int Weight => Mathf.Max(1, weight);
}

[Serializable]
public sealed class EnemyRandomMemberGroup
{
    [SerializeField] private string groupId = "RandomGroup";

    [Min(0)]
    [SerializeField] private int minimumPicks = 1;

    [Min(0)]
    [SerializeField] private int maximumPicks = 1;

    [Tooltip(
        "When disabled, one Enemy Definition can be selected only once " +
        "inside this group. The selected member can still have Count > 1 " +
        "through a fixed member entry.")]
    [SerializeField] private bool allowDuplicatePicks = true;

    [SerializeField] private List<WeightedEnemyDefinition> candidates =
        new List<WeightedEnemyDefinition>();

    public string GroupId => groupId;
    public int MinimumPicks => Mathf.Max(0, minimumPicks);
    public int MaximumPicks => Mathf.Max(MinimumPicks, maximumPicks);
    public bool AllowDuplicatePicks => allowDuplicatePicks;
    public IReadOnlyList<WeightedEnemyDefinition> Candidates => candidates;
}

[Serializable]
public sealed class EnemyEncounterOption
{
    [SerializeField] private string optionId = "EncounterOption";

    [Tooltip("Relative weight among complete encounter options.")]
    [Min(1)]
    [SerializeField] private int weight = 1;

    [SerializeField] private EnemyEncounterDensity density =
        EnemyEncounterDensity.Medium;

    [Tooltip(
        "Always spawned if this complete option is selected. " +
        "Leave empty for an empty-room option.")]
    [SerializeField] private List<EnemyEncounterMember> fixedMembers =
        new List<EnemyEncounterMember>();

    [Tooltip(
        "Optional weighted additions. Fixed and random members can coexist.")]
    [SerializeField] private List<EnemyRandomMemberGroup> randomMemberGroups =
        new List<EnemyRandomMemberGroup>();

    public string OptionId => optionId;
    public int Weight => Mathf.Max(1, weight);
    public EnemyEncounterDensity Density => density;
    public IReadOnlyList<EnemyEncounterMember> FixedMembers => fixedMembers;

    public IReadOnlyList<EnemyRandomMemberGroup> RandomMemberGroups =>
        randomMemberGroups;
}

/// <summary>
/// Room-owned encounter data. A room chooses one complete option; that option
/// may combine fixed members and one or more weighted random member groups.
/// </summary>
[CreateAssetMenu(
    fileName = "RoomEncounterProfile",
    menuName = "Dream Dungeon/Enemy System/Room Encounter Profile")]
public sealed class EnemyEncounterProfile : ScriptableObject
{
    [SerializeField] private string profileId =
        "room_encounter_unassigned";

    [SerializeField] private string displayName =
        "Unassigned Room Encounter";

    [SerializeField] private List<EnemyEncounterOption> options =
        new List<EnemyEncounterOption>();

    public string ProfileId => profileId;
    public string DisplayName => displayName;
    public IReadOnlyList<EnemyEncounterOption> Options => options;

    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(profileId))
        {
            errors.Add(name + ": Profile Id is empty.");
        }

        if (options == null || options.Count == 0)
        {
            errors.Add(name + ": no complete encounter options are configured.");
            return;
        }

        HashSet<string> optionIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int optionIndex = 0;
             optionIndex < options.Count;
             optionIndex++)
        {
            EnemyEncounterOption option = options[optionIndex];

            if (option == null)
            {
                errors.Add(
                    name + ": option slot " + optionIndex + " is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(option.OptionId))
            {
                errors.Add(
                    name + ": option " + optionIndex + " has no Option Id.");
            }
            else if (!optionIds.Add(option.OptionId))
            {
                errors.Add(
                    name + ": duplicate Option Id '" + option.OptionId + "'.");
            }

            ValidateFixedMembers(option, optionIndex, errors);
            ValidateRandomGroups(option, optionIndex, errors);
        }
    }

    private void OnValidate()
    {
        profileId = string.IsNullOrWhiteSpace(profileId)
            ? name
            : profileId.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }
    }

    private void ValidateFixedMembers(
        EnemyEncounterOption option,
        int optionIndex,
        List<string> errors)
    {
        IReadOnlyList<EnemyEncounterMember> members =
            option.FixedMembers;

        if (members == null)
        {
            return;
        }

        for (int memberIndex = 0;
             memberIndex < members.Count;
             memberIndex++)
        {
            EnemyEncounterMember member = members[memberIndex];

            if (member == null || member.EnemyDefinition == null)
            {
                errors.Add(
                    name + ": option " + optionIndex +
                    " fixed member " + memberIndex +
                    " has no Enemy Definition.");
            }
        }
    }

    private void ValidateRandomGroups(
        EnemyEncounterOption option,
        int optionIndex,
        List<string> errors)
    {
        IReadOnlyList<EnemyRandomMemberGroup> groups =
            option.RandomMemberGroups;

        if (groups == null)
        {
            return;
        }

        for (int groupIndex = 0;
             groupIndex < groups.Count;
             groupIndex++)
        {
            EnemyRandomMemberGroup group = groups[groupIndex];

            if (group == null)
            {
                errors.Add(
                    name + ": option " + optionIndex +
                    " random group " + groupIndex + " is null.");
                continue;
            }

            if (group.Candidates == null || group.Candidates.Count == 0)
            {
                errors.Add(
                    name + ": option " + optionIndex +
                    " random group '" + group.GroupId +
                    "' has no candidates.");
                continue;
            }

            if (!group.AllowDuplicatePicks &&
                group.MaximumPicks > group.Candidates.Count)
            {
                errors.Add(
                    name + ": option " + optionIndex +
                    " random group '" + group.GroupId +
                    "' requests more unique picks than candidates.");
            }

            for (int candidateIndex = 0;
                 candidateIndex < group.Candidates.Count;
                 candidateIndex++)
            {
                WeightedEnemyDefinition candidate =
                    group.Candidates[candidateIndex];

                if (candidate == null ||
                    candidate.EnemyDefinition == null)
                {
                    errors.Add(
                        name + ": option " + optionIndex +
                        " random group '" + group.GroupId +
                        "' candidate " + candidateIndex +
                        " has no Enemy Definition.");
                }
            }
        }
    }
}
