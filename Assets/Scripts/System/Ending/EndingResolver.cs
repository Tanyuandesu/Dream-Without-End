/// <summary>
/// Single authority for selecting which ending the completed run receives.
///
/// SYS12 installs only the default route. Future ending conditions belong here
/// (or in data-driven rules called from here), not inside GameManager,
/// EndingSceneController or individual NPC scripts.
/// </summary>
public static class EndingResolver
{
    public const string DefaultEndingId =
        "ENDING_DEFAULT";

    public static string Resolve(EndingRunData data)
    {
        // Future examples:
        // - collected stable Item IDs
        // - kill count
        // - final HP
        // - NPC / choice / event flags added to EndingRunData
        //
        // Keep priority evaluation centralized here when alternate endings
        // are introduced.
        return DefaultEndingId;
    }
}
