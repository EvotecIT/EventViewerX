namespace EventViewerX.Evtx;

internal static class NewestFirstSavedEventBuffer {
    internal static IEnumerable<SavedEventRecord> Read(
        IEnumerable<SavedEventRecord> source,
        long maximumRecords,
        CancellationToken cancellationToken) {

        var records = new LinkedList<SavedEventRecord>();
        foreach (SavedEventRecord record in source) {
            cancellationToken.ThrowIfCancellationRequested();
            if (maximumRecords > 0 && records.Count >= maximumRecords) {
                records.RemoveFirst();
            }
            records.AddLast(record);
        }

        for (LinkedListNode<SavedEventRecord>? node = records.Last;
             node != null;
             node = node.Previous) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return node.Value;
        }
    }
}
