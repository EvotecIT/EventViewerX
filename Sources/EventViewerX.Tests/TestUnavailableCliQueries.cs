#if NET10_0
using System.Diagnostics;
using EventViewerX.Cli;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestUnavailableCliQueries {
    [Fact]
    public async Task ExplainRejectsUnavailableNativeTypesBeforeReturningAPredicatePlan() {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
        };
        foreach (string argument in new[] {
            typeof(EventNotificationMailDelivery).Assembly.Location, "query", "--type", "SqlDatabaseCreated", "--explain", "--where",
            EventPredicate.Compare("DatabaseName", EventPredicateOperator.Equal, "fixture").ToJson(indented: false)
        }) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("CLI process did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try {
            await process.WaitForExitAsync(deadline.Token);
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("SqlDatabaseCreated", await error);
            Assert.Empty(await output);
        } finally {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }
}
#endif