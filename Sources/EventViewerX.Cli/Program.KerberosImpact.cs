using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> KerberosImpactAsync(CliArguments options) {
        string path = options.Require("store");
        EventStoreQuery query = CreateStoreQuery(options);
        query.Types = new[] { EventType.KerberosKdcRc4Audit };
        KerberosRc4ImpactAccumulator accumulator = KerberosRc4ImpactEngine.CreateAccumulator(
            options.GetInt("maximum-groups", 10_000),
            options.GetInt("maximum-evidence-per-group", 25));
        EventStoreRowReadResult read = await new EventStore(path)
            .StreamRowsAsync(query, (row, _) => {
                accumulator.Add(row);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        return WriteJson(accumulator.Complete(read.IsComplete, read.CompletenessDiagnostic));
    }
}
