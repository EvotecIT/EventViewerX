using Xunit;

namespace EventViewerX.Tests;

/// <summary>Serializes tests that mutate the process-wide watcher registry.</summary>
[CollectionDefinition("WatcherManager", DisableParallelization = true)]
public sealed class WatcherManagerCollection {
}
