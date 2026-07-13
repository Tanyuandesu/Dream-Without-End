using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DreamBackgroundStageSettings
{
    [Min(0)] public int minimumCollectedItems;

    [Header("背景")]
    [Min(0f)] public float backgroundBrightness = 1f;

    [Header("霧")]
    [Min(0f)] public float fogAlphaMultiplier = 1f;
    [Min(0f)] public float fogSpeedMultiplier = 1f;
    [Min(0f)] public float fogAmplitudeMultiplier = 1f;

    [Header("塵埃")]
    [Min(0f)] public float dustEmissionMultiplier = 1f;
    [Min(0f)] public float dustSizeMultiplier = 1f;

    [Header("幽光")]
    [Min(0f)] public float glowEmissionMultiplier = 1f;
    [Min(0f)] public float glowSizeMultiplier = 1f;

    [Header("過渡")]
    [Min(0.01f)] public float transitionDuration = 2f;
}

[CreateAssetMenu(
    fileName = "DreamBackgroundProgressionProfile",
    menuName = "Game/Background/Dream Background Progression Profile")]
public sealed class DreamBackgroundProgressionProfile : ScriptableObject
{
    [SerializeField]
    private List<DreamBackgroundStageSettings> stages =
        new List<DreamBackgroundStageSettings>();

    public DreamBackgroundStageSettings GetStage(int collectedItemCount)
    {
        if (stages == null || stages.Count == 0)
        {
            return null;
        }

        DreamBackgroundStageSettings selected = stages[0];

        for (int i = 0; i < stages.Count; i++)
        {
            DreamBackgroundStageSettings candidate = stages[i];

            if (candidate != null &&
                candidate.minimumCollectedItems <= collectedItemCount)
            {
                selected = candidate;
            }
        }

        return selected;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        List<DreamBackgroundStageSettings> newStages)
    {
        stages = newStages ?? new List<DreamBackgroundStageSettings>();
    }
#endif
}
