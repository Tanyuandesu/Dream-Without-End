/// <summary>
/// Single authority for selecting which ending the completed run receives.
///
/// NPC code may contribute event flags, but only this resolver maps those
/// facts to an ending ID. Existing exit-driven ending behaviour stays here.
/// </summary>
public static class EndingResolver
{
    public const string DefaultEndingId =
        "ENDING_DEFAULT";

    // SYS14 development/content contract. A Final Legacy NPC dialogue can
    // emit this flag. Ending presentation may later map ENDING_NPC_FINAL to
    // its authored CG/dialogue without changing NPC runtime code.
    public const string NpcFinalDialogueEventFlag =
        "NPC_FINAL_DIALOGUE";

    public const string NpcFinalDialogueEndingId =
        "ENDING_NPC_FINAL";

    public static string Resolve(EndingRunData data)
    {
        if (data != null &&
            data.HasEventFlag(NpcFinalDialogueEventFlag))
        {
            return NpcFinalDialogueEndingId;
        }

        return DefaultEndingId;
    }
}
