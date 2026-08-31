using UnityEngine;

public enum RunLaunchMode
{
    None = 0,
    NewGame = 1,
    Continue = 2
}

/// <summary>
/// One pending request passed from Title/App Flow to GameScene startup.
/// SYS8 only establishes the request. SYS9 will consume it before the first
/// floor is generated and restore the matching run progress.
/// </summary>
public sealed class RunLaunchRequest
{
    public RunLaunchMode Mode { get; }
    public SaveGameData SaveData { get; }

    public RunLaunchRequest(
        RunLaunchMode mode,
        SaveGameData saveData)
    {
        Mode = mode;
        SaveData = saveData != null
            ? saveData.CreateCopy()
            : null;
    }
}

public static class RunLaunchContext
{
    private static RunLaunchRequest pendingRequest;

    public static bool HasPendingRequest =>
        pendingRequest != null &&
        pendingRequest.Mode != RunLaunchMode.None;

    public static RunLaunchMode PendingMode =>
        pendingRequest != null
            ? pendingRequest.Mode
            : RunLaunchMode.None;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingRequest = null;
    }

    public static void RequestNewGame()
    {
        pendingRequest =
            new RunLaunchRequest(
                RunLaunchMode.NewGame,
                null);
    }

    public static void RequestContinue(
        SaveGameData saveData)
    {
        if (saveData == null)
        {
            Debug.LogError(
                "RunLaunchContext：Continue request requires SaveGameData.");
            return;
        }

        pendingRequest =
            new RunLaunchRequest(
                RunLaunchMode.Continue,
                saveData);
    }

    public static bool TryPeek(
        out RunLaunchRequest request)
    {
        if (!HasPendingRequest)
        {
            request = null;
            return false;
        }

        request =
            new RunLaunchRequest(
                pendingRequest.Mode,
                pendingRequest.SaveData);

        return true;
    }

    public static bool TryConsume(
        out RunLaunchRequest request)
    {
        if (!TryPeek(out request))
        {
            return false;
        }

        pendingRequest = null;
        return true;
    }

    public static void Clear()
    {
        pendingRequest = null;
    }
}
