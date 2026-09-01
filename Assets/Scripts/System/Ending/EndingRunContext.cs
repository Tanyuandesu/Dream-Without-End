using System;
using System.Collections.Generic;

/// <summary>
/// Detached snapshot passed from the final legacy maze into EndingScene.
/// It deliberately contains only run facts needed to resolve/present an ending.
/// Future NPC/choice systems can extend this contract without coupling the
/// EndingScene back to GameScene runtime objects.
/// </summary>
[Serializable]
public sealed class EndingRunData
{
    public string endingId;
    public int sourceFloor;
    public float finalHP;
    public List<string> collectedItemIds;
    public int killCount;

    public EndingRunData(
        int sourceFloor,
        float finalHP,
        IEnumerable<string> collectedItemIds,
        int killCount)
    {
        this.sourceFloor = Math.Max(1, sourceFloor);
        this.finalHP = finalHP;
        this.collectedItemIds =
            collectedItemIds != null
                ? new List<string>(collectedItemIds)
                : new List<string>();
        this.killCount = Math.Max(0, killCount);
        endingId = string.Empty;
    }

    public EndingRunData Clone()
    {
        EndingRunData clone =
            new EndingRunData(
                sourceFloor,
                finalHP,
                collectedItemIds,
                killCount);

        clone.endingId = endingId;
        return clone;
    }

    public static EndingRunData CreateDirectSceneFallback()
    {
        EndingRunData data =
            new EndingRunData(
                1,
                1f,
                Array.Empty<string>(),
                0);

        data.endingId = EndingResolver.DefaultEndingId;
        return data;
    }
}

/// <summary>
/// One-shot handoff. GameScene queues a detached result immediately before
/// loading EndingScene; EndingScene consumes it once.
/// </summary>
public static class EndingRunContext
{
    private static EndingRunData pending;

    public static bool HasPending => pending != null;

    public static void Queue(EndingRunData data)
    {
        pending = data != null
            ? data.Clone()
            : null;
    }

    public static bool TryPeek(out EndingRunData data)
    {
        data = pending != null
            ? pending.Clone()
            : null;

        return data != null;
    }

    public static bool TryConsume(out EndingRunData data)
    {
        if (pending == null)
        {
            data = null;
            return false;
        }

        data = pending.Clone();
        pending = null;
        return true;
    }

    public static void Clear()
    {
        pending = null;
    }
}
