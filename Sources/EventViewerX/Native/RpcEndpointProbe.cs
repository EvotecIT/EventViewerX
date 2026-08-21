using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX.Native;

/// <summary>Classifies an RPC endpoint probe without conflating its deadline with a definitive failure.</summary>
internal enum RpcEndpointProbeStatus {
    Connected,
    Failed,
    TimedOut
}

internal static class RpcEndpointProbe {
    /// <summary>Attempts one bounded TCP connection and preserves timeout as a distinct result.</summary>
    internal static RpcEndpointProbeStatus Probe(
        string host,
        int port,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        Func<Task>? connectAsyncOverride = null,
        Func<bool>? connectedOverride = null) {

        cancellationToken.ThrowIfCancellationRequested();
        try {
            using var client = new TcpClient();
            Task connect = connectAsyncOverride?.Invoke() ??
                           client.ConnectAsync(host, port);
            bool completed;
            try {
                completed = connect.Wait(
                    timeoutMilliseconds,
                    cancellationToken);
            } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
                ObserveLateFault(connect);
                throw;
            } catch (AggregateException) {
                connect.GetAwaiter().GetResult();
                return RpcEndpointProbeStatus.Failed;
            }

            if (!completed) {
                ObserveLateFault(connect);
                return RpcEndpointProbeStatus.TimedOut;
            }
            return (connectedOverride?.Invoke() ??
                    client.Connected)
                ? RpcEndpointProbeStatus.Connected
                : RpcEndpointProbeStatus.Failed;
        } catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            return RpcEndpointProbeStatus.Failed;
        }
    }

    private static void ObserveLateFault(Task task) {
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
