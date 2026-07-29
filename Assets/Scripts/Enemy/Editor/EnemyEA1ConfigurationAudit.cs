using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyEA1ConfigurationAudit
{
    [MenuItem(
        "Tools/Dream Dungeon/Enemy System/Run EA1 Configuration Audit")]
    public static void RunAudit()
    {
        List<string> errors = new List<string>();
        List<string> notes = new List<string>();

        EnemyDefinition[] definitions =
            LoadAllAssets<EnemyDefinition>();

        EnemyCatalog[] catalogs =
            LoadAllAssets<EnemyCatalog>();

        EnemyEncounterProfile[] encounterProfiles =
            LoadAllAssets<EnemyEncounterProfile>();

        RoomEncounterCatalog[] roomCatalogs =
            LoadAllAssets<RoomEncounterCatalog>();

        if (definitions.Length != 5)
        {
            errors.Add(
                "Expected exactly five Enemy Definitions, found " +
                definitions.Length + ".");
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            definitions[i].CollectValidationErrors(errors);
        }

        AuditT4AuthoringVisibility(
            definitions,
            errors,
            notes);

        AuditT5ABehaviorAuthoring(
            definitions,
            errors,
            notes);

        AuditT5BPerceptionAndChaseAuthoring(
            definitions,
            errors,
            notes);

        AuditT5CLocalAlertAuthoring(
            definitions,
            errors,
            notes);

        if (catalogs.Length == 0)
        {
            errors.Add("No Enemy Catalog asset was found.");
        }

        for (int i = 0; i < catalogs.Length; i++)
        {
            catalogs[i].CollectValidationErrors(errors);

            if (catalogs[i].Count != 5)
            {
                errors.Add(
                    catalogs[i].name +
                    ": expected five registered definitions, found " +
                    catalogs[i].Count + ".");
            }
        }

        if (encounterProfiles.Length == 0)
        {
            errors.Add("No Room Encounter Profile asset was found.");
        }

        for (int i = 0; i < encounterProfiles.Length; i++)
        {
            encounterProfiles[i].CollectValidationErrors(errors);
        }

        for (int i = 0; i < roomCatalogs.Length; i++)
        {
            roomCatalogs[i].CollectValidationErrors(errors);

            if (roomCatalogs[i].Bindings.Count == 0)
            {
                notes.Add(
                    roomCatalogs[i].name +
                    ": zero runtime room bindings is expected in EA1; " +
                    "EA6 will author actual room probabilities.");
            }
        }

        EnemySpawner[] spawners =
            Resources.FindObjectsOfTypeAll<EnemySpawner>();

        int sceneSpawnerCount = 0;

        for (int i = 0; i < spawners.Length; i++)
        {
            EnemySpawner spawner = spawners[i];

            if (spawner == null ||
                !spawner.gameObject.scene.IsValid())
            {
                continue;
            }

            sceneSpawnerCount++;

            if (spawner.Catalog == null)
            {
                errors.Add(
                    spawner.name + ": Enemy Catalog is not assigned.");
            }

            if (spawner.DefaultEnemyDefinition == null)
            {
                errors.Add(
                    spawner.name +
                    ": Default Enemy Definition is not assigned.");
            }
            else if (spawner.Catalog != null &&
                     !spawner.Catalog.Contains(
                         spawner.DefaultEnemyDefinition))
            {
                errors.Add(
                    spawner.name +
                    ": Default Enemy Definition is outside its catalog.");
            }

            AuditT1SpawnModeSwitch(
                spawner,
                errors,
                notes);

            AuditT2SpawnPlan(
                spawner,
                errors,
                notes);

            AuditT3RoomAssignmentContract(
                spawner,
                errors,
                notes);

            AuditT0SpawnBaseline(
                spawner,
                errors,
                notes);
        }

        if (sceneSpawnerCount == 0)
        {
            notes.Add(
                "No scene EnemySpawner is currently loaded. Open GameScene " +
                "and rerun the audit to verify scene references.");
        }

        if (definitions.Length > 0)
        {
            AuditRunRecordContract(definitions[0], errors);
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[Enemy System/EA1] Configuration Audit");
        report.AppendLine("Definitions=" + definitions.Length);
        report.AppendLine("Catalogs=" + catalogs.Length);
        report.AppendLine(
            "EncounterProfiles=" + encounterProfiles.Length);
        report.AppendLine("SceneSpawners=" + sceneSpawnerCount);

        for (int i = 0; i < notes.Count; i++)
        {
            report.AppendLine("NOTE: " + notes[i]);
        }

        if (errors.Count == 0)
        {
            report.AppendLine("Result=PASS");
            Debug.Log(report.ToString());
            return;
        }

        report.AppendLine("Result=FAIL");

        for (int i = 0; i < errors.Count; i++)
        {
            report.AppendLine("ERROR: " + errors[i]);
        }

        Debug.LogError(report.ToString());
    }

    private static void AuditT1SpawnModeSwitch(
        EnemySpawner spawner,
        List<string> errors,
        List<string> notes)
    {
        if (spawner == null)
        {
            return;
        }

        if (!System.Enum.IsDefined(
                typeof(EnemySpawnMode),
                spawner.SpawnMode))
        {
            errors.Add(
                spawner.name +
                ": T1 Enemy Spawn Mode contains an undefined value.");
            return;
        }

        notes.Add(
            spawner.name +
            ": T1 spawn-mode switch installed. Active=" +
            spawner.SpawnMode +
            ". T2 plan builder is authoritative; T3 room assignment " +
            "is connected.");
    }

    private static void AuditT2SpawnPlan(
        EnemySpawner spawner,
        List<string> errors,
        List<string> notes)
    {
        if (spawner == null)
        {
            return;
        }

        List<EnemyDefinition> spawnPlan =
            new List<EnemyDefinition>();
        string failureReason;

        if (!spawner.TryBuildConfiguredSpawnPlan(
                spawnPlan,
                out failureReason))
        {
            errors.Add(
                spawner.name +
                ": T2 spawn plan rejected. Reason=" +
                failureReason);
            return;
        }

        int expectedCount = spawner.SpawnMode ==
            EnemySpawnMode.TemporaryFiveTypeShowcase
                ? 5
                : spawner.ConfiguredEnemyCount;

        if (spawnPlan.Count != expectedCount)
        {
            errors.Add(
                spawner.name +
                ": T2 spawn plan count mismatch. Expected=" +
                expectedCount +
                ", Actual=" + spawnPlan.Count + ".");
            return;
        }

        HashSet<EnemyId> usedIds = new HashSet<EnemyId>();

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            EnemyDefinition definition = spawnPlan[i];

            if (definition == null)
            {
                errors.Add(
                    spawner.name +
                    ": T2 spawn plan slot " + i +
                    " is null.");
                continue;
            }

            if (!usedIds.Add(definition.Id) &&
                spawner.SpawnMode ==
                    EnemySpawnMode.TemporaryFiveTypeShowcase)
            {
                errors.Add(
                    spawner.name +
                    ": T2 showcase plan contains duplicate EnemyId '" +
                    definition.Id + "'.");
            }
        }

        notes.Add(
            spawner.name +
            ": T2 authoritative spawn plan ready. Mode=" +
            spawner.SpawnMode +
            ", Plan=" + DescribePlan(spawnPlan) +
            ".");
    }

    private static void AuditT3RoomAssignmentContract(
        EnemySpawner spawner,
        List<string> errors,
        List<string> notes)
    {
        if (spawner == null)
        {
            return;
        }

        if (spawner.SpawnMode ==
            EnemySpawnMode.TemporaryFiveTypeShowcase)
        {
            List<EnemyDefinition> spawnPlan =
                new List<EnemyDefinition>();
            string failureReason;

            if (!spawner.TryBuildConfiguredSpawnPlan(
                    spawnPlan,
                    out failureReason))
            {
                errors.Add(
                    spawner.name +
                    ": T3 cannot assign rooms because the showcase " +
                    "plan is invalid. Reason=" + failureReason);
                return;
            }

            if (spawnPlan.Count != 5)
            {
                errors.Add(
                    spawner.name +
                    ": T3 showcase room assignment requires exactly " +
                    "five plan entries. Actual=" +
                    spawnPlan.Count + ".");
                return;
            }

            notes.Add(
                spawner.name +
                ": T3 five-room assignment is active. Runtime requires " +
                "at least five non-Start/non-Exit rooms and rejects the " +
                "whole request before creation when that contract fails.");
        }
        else
        {
            notes.Add(
                spawner.name +
                ": T3 room-assignment path is installed but inactive " +
                "because BaselineSingleDefinition is selected.");
        }
    }

    private static string DescribePlan(
        IReadOnlyList<EnemyDefinition> spawnPlan)
    {
        if (spawnPlan == null || spawnPlan.Count == 0)
        {
            return "[]";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append('[');

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            EnemyDefinition definition = spawnPlan[i];
            builder.Append(
                definition != null
                    ? definition.Id.Value
                    : "legacy_enemy");
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static void AuditT4AuthoringVisibility(
        EnemyDefinition[] definitions,
        List<string> errors,
        List<string> notes)
    {
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        HashSet<string> colorKeys = new HashSet<string>();

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null)
            {
                continue;
            }

            if (definition.VisualColor.a <= 0.05f)
            {
                errors.Add(
                    definition.name +
                    ": T4 Visual Color alpha is effectively invisible.");
            }

            colorKeys.Add(
                ColorUtility.ToHtmlStringRGBA(
                    definition.VisualColor));
        }

        notes.Add(
            "T4 authoring visibility is installed. DistinctVisualColors=" +
            colorKeys.Count + "/" + definitions.Length +
            ". These colors are editable presentation data only; " +
            "GameplayRole never overrides Definition values.");
    }

    private static void AuditT5ABehaviorAuthoring(
        EnemyDefinition[] definitions,
        List<string> errors,
        List<string> notes)
    {
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null)
            {
                continue;
            }

            if (definition.PatrolRadiusInCells < 0 ||
                definition.PatrolPauseDuration < 0f ||
                definition.SearchRadiusInCells < 0 ||
                definition.SearchDuration < 0f)
            {
                errors.Add(
                    definition.name +
                    ": T5A patrol/search authoring values cannot be negative.");
            }
        }

        notes.Add(
            "T5A behavior authoring is installed. Patrol Radius, Patrol Pause, " +
            "Search Radius and Search Duration are read from each " +
            "EnemyDefinition; GameplayRole does not override them.");
    }

    private static void AuditT5BPerceptionAndChaseAuthoring(
        EnemyDefinition[] definitions,
        List<string> errors,
        List<string> notes)
    {
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null)
            {
                continue;
            }

            if (definition.ViewAngle < 1f ||
                definition.ViewAngle > 360f)
            {
                errors.Add(
                    definition.name +
                    ": T5B View Angle must be between 1 and 360 degrees.");
            }

            if (definition.MaximumChasePathCost < 1)
            {
                errors.Add(
                    definition.name +
                    ": T5B Maximum Chase Path Cost must be at least 1.");
            }

            if (definition.LoseTargetRadius <
                definition.DetectionRadius)
            {
                errors.Add(
                    definition.name +
                    ": T5B Lose Target Radius cannot be smaller than " +
                    "Detection Radius.");
            }
        }

        notes.Add(
            "T5B authoring is installed. View Angle gates initial " +
            "acquisition, Require Line Of Sight gates perception, and " +
            "Maximum Chase Path Cost is supplied to the shared A* agent. " +
            "All values remain editable per EnemyDefinition.");
    }

    private static void AuditT5CLocalAlertAuthoring(
        EnemyDefinition[] definitions,
        List<string> errors,
        List<string> notes)
    {
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        int broadcasterCount = 0;

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyDefinition definition = definitions[i];

            if (definition == null)
            {
                continue;
            }

            if (definition.AlertBroadcastCooldown < 0f)
            {
                errors.Add(
                    definition.name +
                    ": T5C Alert Broadcast Cooldown cannot be negative.");
            }

            if (!definition.BroadcastsAlert)
            {
                continue;
            }

            broadcasterCount++;

            if (definition.AlertRadius <= 0f)
            {
                errors.Add(
                    definition.name +
                    ": T5C broadcaster requires Alert Radius above zero.");
            }
        }

        notes.Add(
            "T5C local alert authoring is installed. AlertBroadcasters=" +
            broadcasterCount + "/" + definitions.Length +
            ". Broadcasts Alert, Alert Radius and Alert Broadcast Cooldown " +
            "are read directly from each EnemyDefinition; no GameplayRole " +
            "branch assigns behavior.");
    }

    private static void AuditT0SpawnBaseline(
        EnemySpawner spawner,
        List<string> errors,
        List<string> notes)
    {
        if (spawner == null)
        {
            return;
        }

        if (spawner.SpawnMode !=
            EnemySpawnMode.BaselineSingleDefinition)
        {
            notes.Add(
                spawner.name +
                ": T0 baseline settings remain preserved but are " +
                "currently inactive because Spawn Mode=" +
                spawner.SpawnMode + ".");
        }

        if (spawner.ConfiguredEnemyCount != 3)
        {
            errors.Add(
                spawner.name +
                ": T0 baseline requires Enemy Count = 3. Actual=" +
                spawner.ConfiguredEnemyCount + ".");
        }

        if (spawner.DefaultEnemyDefinition != null &&
            spawner.DefaultEnemyDefinition.Id.Value !=
                "dream_wanderer")
        {
            errors.Add(
                spawner.name +
                ": T0 baseline Default Enemy Definition must be " +
                "dream_wanderer. Actual=" +
                spawner.DefaultEnemyDefinition.Id.Value + ".");
        }

        if (spawner.NavigationTopology !=
            EnemyNavigationTopology.FourDirections)
        {
            errors.Add(
                spawner.name +
                ": T0 baseline navigation must remain FourDirections.");
        }

        if (spawner.MaxPathQueriesPerFrame != 2)
        {
            errors.Add(
                spawner.name +
                ": T0 baseline Max Path Queries Per Frame must be 2. " +
                "Actual=" + spawner.MaxPathQueriesPerFrame + ".");
        }

        if (spawner.SimplifiesCollinearPathWaypoints)
        {
            errors.Add(
                spawner.name +
                ": T0 baseline Simplify Collinear Path Waypoints must be off.");
        }

        SerializedObject serializedSpawner =
            new SerializedObject(spawner);

        RequireSerializedBool(
            serializedSpawner,
            "spawnNearPlayerFirst",
            true,
            errors);

        RequireSerializedBool(
            serializedSpawner,
            "excludeExitRoom",
            true,
            errors);

        RequireSerializedBool(
            serializedSpawner,
            "r83InjectNoLegalEnemyCellForControlledFailure",
            false,
            errors);

        RequireSerializedInt(
            serializedSpawner,
            "maxExpandedPathNodesPerQuery",
            4096,
            errors);

        RequireSerializedInt(
            serializedSpawner,
            "navigationStartRecoveryRadiusInCells",
            1,
            errors);

        notes.Add(
            spawner.name +
            ": T0 spawn baseline locked as 3 x dream_wanderer, " +
            "nearest rooms first, Start/Exit excluded.");
    }

    private static void RequireSerializedBool(
        SerializedObject target,
        string fieldName,
        bool expected,
        List<string> errors)
    {
        SerializedProperty property =
            target.FindProperty(fieldName);

        if (property == null)
        {
            errors.Add(
                target.targetObject.name +
                ": missing serialized field " + fieldName + ".");
            return;
        }

        if (property.boolValue != expected)
        {
            errors.Add(
                target.targetObject.name +
                ": " + fieldName + " must be " + expected +
                ". Actual=" + property.boolValue + ".");
        }
    }

    private static void RequireSerializedInt(
        SerializedObject target,
        string fieldName,
        int expected,
        List<string> errors)
    {
        SerializedProperty property =
            target.FindProperty(fieldName);

        if (property == null)
        {
            errors.Add(
                target.targetObject.name +
                ": missing serialized field " + fieldName + ".");
            return;
        }

        if (property.intValue != expected)
        {
            errors.Add(
                target.targetObject.name +
                ": " + fieldName + " must be " + expected +
                ". Actual=" + property.intValue + ".");
        }
    }

    private static void AuditRunRecordContract(
        EnemyDefinition definition,
        List<string> errors)
    {
        GameObject firstObject = null;
        GameObject secondObject = null;
        GameObject thirdObject = null;

        try
        {
            EnemyRunRecord record = new EnemyRunRecord();
            record.BeginFloor(1, 101, 9001);

            EnemyRuntimeIdentity first = CreateAuditIdentity(
                ref firstObject,
                "EA1_Audit_PlayerKill",
                definition,
                101,
                0);

            if (!record.RegisterSpawn(first) ||
                record.RegisterSpawn(first))
            {
                errors.Add(
                    "EnemyRunRecord failed duplicate-safe spawn registration.");
            }

            record.RegisterDeath(first, DamageAttribution.Player);

            EnemyRuntimeIdentity second = CreateAuditIdentity(
                ref secondObject,
                "EA1_Audit_Survivor",
                definition,
                101,
                1);

            record.RegisterSpawn(second);
            record.MarkSurvivedFloor(second);

            EnemyRuntimeIdentity third = CreateAuditIdentity(
                ref thirdObject,
                "EA1_Audit_OtherDeath",
                definition,
                101,
                2);

            record.RegisterSpawn(third);
            record.RegisterDeath(third, DamageAttribution.Environment);
            record.FinalizeCurrentFloor();

            EnemyRunRecordSnapshot snapshot =
                record.CreateSnapshot();

            bool snapshotMatches =
                snapshot.EligibleSpawnedCount == 3 &&
                snapshot.PlayerKillCount == 1 &&
                snapshot.OtherDeathCount == 1 &&
                snapshot.SurvivedFloorCount == 1 &&
                snapshot.ActiveCount == 0 &&
                !snapshot.AreAllEligibleEnemiesDead &&
                !snapshot.WereAllEligibleEnemiesKilledByPlayer &&
                !snapshot.HasNoEnemyDeaths &&
                !snapshot.HasNoPlayerKills;

            if (!snapshotMatches)
            {
                errors.Add(
                    "EnemyRunRecord snapshot contract produced unexpected totals.");
            }
        }
        finally
        {
            DestroyAuditObject(firstObject);
            DestroyAuditObject(secondObject);
            DestroyAuditObject(thirdObject);
        }
    }

    private static EnemyRuntimeIdentity CreateAuditIdentity(
        ref GameObject owner,
        string instanceId,
        EnemyDefinition definition,
        int floorSessionId,
        int cellX)
    {
        owner = new GameObject(instanceId);
        owner.hideFlags = HideFlags.HideAndDontSave;

        EnemyRuntimeIdentity identity =
            owner.AddComponent<EnemyRuntimeIdentity>();

        identity.Initialize(
            instanceId,
            definition,
            1,
            floorSessionId,
            0,
            new Vector2Int(cellX, 0),
            true);

        return identity;
    }

    private static void DestroyAuditObject(GameObject auditObject)
    {
        if (auditObject != null)
        {
            Object.DestroyImmediate(auditObject);
        }
    }

    private static T[] LoadAllAssets<T>()
        where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:" + typeof(T).Name);

        List<T> assets = new List<T>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets.ToArray();
    }
}
