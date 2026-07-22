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
