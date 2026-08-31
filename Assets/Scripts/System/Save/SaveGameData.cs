using System;
using System.Collections.Generic;

/// <summary>
/// Lightweight run-progress save contract.
///
/// Intentionally excludes generated map layout, player position, enemy state,
/// room visit state and other world snapshots. SYS9 restores only the fields
/// defined here, then lets the requested floor generate normally.
/// </summary>
[Serializable]
public sealed class SaveGameData
{
    // Keep required scalar defaults invalid so truncated/minimal JSON such as
    // "{}" cannot silently become a legitimate floor-1 save.
    public int saveVersion;
    public int floorIndex;
    public float currentHP;
    public List<string> collectedItemIds = new List<string>();
    public int killCount;

    public SaveGameData()
    {
    }

    public SaveGameData(
        int floorIndex,
        float currentHP,
        IEnumerable<string> collectedItemIds,
        int killCount)
    {
        saveVersion = SaveSystemManager.CurrentSaveVersion;
        this.floorIndex = floorIndex;
        this.currentHP = currentHP;
        this.killCount = killCount;

        if (collectedItemIds != null)
        {
            this.collectedItemIds.AddRange(collectedItemIds);
        }
    }

    public SaveGameData CreateCopy()
    {
        return new SaveGameData(
            floorIndex,
            currentHP,
            collectedItemIds,
            killCount)
        {
            saveVersion = saveVersion
        };
    }
}
