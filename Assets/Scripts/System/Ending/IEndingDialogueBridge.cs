using System;

/// <summary>
/// Request handed to a future dialogue implementation.
/// dialogueId identifies the script/sequence while runData exposes the ending
/// facts for optional conditional lines.
/// </summary>
public sealed class EndingDialogueRequest
{
    public string endingId { get; }
    public string dialogueId { get; }
    public EndingRunData runData { get; }

    public EndingDialogueRequest(
        string endingId,
        string dialogueId,
        EndingRunData runData)
    {
        this.endingId = endingId ?? string.Empty;
        this.dialogueId = dialogueId ?? string.Empty;
        this.runData =
            runData != null
                ? runData.Clone()
                : EndingRunData.CreateDirectSceneFallback();
    }
}

/// <summary>
/// Future dialogue-system seam for EndingScene.
///
/// IMPORTANT CONTRACT:
/// - EndingSceneController calls Begin only after the player's first click.
/// - Later clicks call Advance.
/// - The bridge pushes visible text through onTextChanged.
/// - The bridge calls onCompleted only after the entire ending dialogue ends.
/// - EndingSceneController then reveals Return to Title; it never returns
///   automatically on the same click.
/// </summary>
public interface IEndingDialogueBridge
{
    void Begin(
        EndingDialogueRequest request,
        Action<string> onTextChanged,
        Action onCompleted);

    void Advance();
}
