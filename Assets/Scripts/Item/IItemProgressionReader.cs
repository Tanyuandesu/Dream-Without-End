public interface IItemProgressionReader
{
    int CollectedItemCount { get; }
    int ProgressionScore { get; }

    bool HasCollected(string itemId);
    ItemProgressSnapshot CreateProgressSnapshot();
}
