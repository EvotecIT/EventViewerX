using Xunit;

namespace EventViewerX.Tests;

public class TestLoggerIsolation {
    [Fact]
    public async Task ContextLoggersDoNotCrossConcurrentOperations() {
        var firstMessages = new List<string>();
        var secondMessages = new List<string>();
        var firstLogger = new InternalLogger();
        var secondLogger = new InternalLogger();
        firstLogger.OnWarningMessage += (_, args) => firstMessages.Add(args.FullMessage);
        secondLogger.OnWarningMessage += (_, args) => secondMessages.Add(args.FullMessage);

        using var barrier = new Barrier(2);
        Task first = Task.Run(() => WriteInContext(firstLogger, "first", barrier));
        Task second = Task.Run(() => WriteInContext(secondLogger, "second", barrier));
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { "first" }, firstMessages);
        Assert.Equal(new[] { "second" }, secondMessages);
    }

    private static void WriteInContext(InternalLogger logger, string message, Barrier barrier) {
        Settings._logger = logger;
        barrier.SignalAndWait();
        Settings._logger.WriteWarning(message);
    }
}
