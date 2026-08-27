using System;

/// <summary>
/// JSON payload shape used by localization files in Resources/Localization.
/// Keeping content in TextAssets means future UI, item and dialogue text can
/// be added without changing runtime code.
/// </summary>
[Serializable]
public sealed class LocalizationTableData
{
    public LocalizationEntry[] entries;
}
