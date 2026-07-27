using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Session-local observations for CB9.5. Diagnostics never change gameplay.
/// </summary>
public static class EnemyTemporaryHealthBarDiagnostics
{
    private static readonly HashSet<int> triggeredBarIds =
        new HashSet<int>();
    public static int CreatedCount { get; private set; }
    public static int DamageTriggerCount { get; private set; }
    public static int PlayerDamageTriggerCount { get; private set; }
    public static int ShowStartCount { get; private set; }
    public static int TimerRefreshCount { get; private set; }
    public static int FadeStartCount { get; private set; }
    public static int FadeCompletedHideCount { get; private set; }
    public static int FullHealthHideCount { get; private set; }
    public static int DeathHideCount { get; private set; }
    public static int RejectedSourceCount { get; private set; }
    public static int LowestDisplayedPercent { get; private set; }
    public static int DistinctTriggeredBarCount => triggeredBarIds.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        CreatedCount = 0;
        DamageTriggerCount = 0;
        PlayerDamageTriggerCount = 0;
        ShowStartCount = 0;
        TimerRefreshCount = 0;
        FadeStartCount = 0;
        FadeCompletedHideCount = 0;
        FullHealthHideCount = 0;
        DeathHideCount = 0;
        RejectedSourceCount = 0;
        LowestDisplayedPercent = 100;
        triggeredBarIds.Clear();
    }

    public static void RecordCreated()
    {
        CreatedCount++;
    }

    public static void RecordDamageTrigger(
        int barInstanceId,
        DamageAttribution attribution,
        float normalizedHealth)
    {
        DamageTriggerCount++;
        triggeredBarIds.Add(barInstanceId);

        if (attribution == DamageAttribution.Player)
        {
            PlayerDamageTriggerCount++;
        }

        int percent = Mathf.RoundToInt(
            Mathf.Clamp01(normalizedHealth) * 100f);

        LowestDisplayedPercent = Mathf.Min(
            LowestDisplayedPercent,
            percent);
    }

    public static void RecordShowStarted()
    {
        ShowStartCount++;
    }

    public static void RecordTimerRefreshed()
    {
        TimerRefreshCount++;
    }

    public static void RecordFadeStarted()
    {
        FadeStartCount++;
    }

    public static void RecordFadeCompletedHide()
    {
        FadeCompletedHideCount++;
    }

    public static void RecordFullHealthHide()
    {
        FullHealthHideCount++;
    }

    public static void RecordDeathHide()
    {
        DeathHideCount++;
    }

    public static void RecordRejectedSource()
    {
        RejectedSourceCount++;
    }
}
