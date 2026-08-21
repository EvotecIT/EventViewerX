using DnsClientX;

namespace EventViewerX;

/// <summary>Applies optional, failure-isolated enrichment to projected typed events.</summary>
internal sealed class EventEnricher : IDisposable {
    private readonly EventEnrichmentOptions _options;
    private readonly Func<string, CancellationToken, Task<DnsResponse>>? _resolver;
    private readonly ConcurrentDictionary<string, Lazy<Task<DnsResponse>>> _pendingDnsRequests =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim? _dnsConcurrency;
    private readonly ClientX? _dnsClient;

    internal EventEnricher(
        EventEnrichmentOptions options,
        Func<string, CancellationToken, Task<DnsResponse>>? dnsResolver = null) {

        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        if (!_options.ResolveDns) {
            return;
        }

        _dnsConcurrency = new SemaphoreSlim(
            _options.DnsMaxConcurrency,
            _options.DnsMaxConcurrency);
        _resolver = dnsResolver;
        if (_resolver == null) {
            _dnsClient = new ClientX(
                DnsEndpoint.System,
                timeOutMilliseconds: _options.DnsTimeoutMilliseconds,
                enableCache: true);
        }
    }

    /// <summary>Maximum projected events enriched in one ordered batch.</summary>
    internal int MaxConcurrency => _options.ResolveDns
        ? _options.DnsMaxConcurrency
        : 1;

    /// <summary>Enriches a projected event without converting lookup failures into missing records.</summary>
    internal async Task EnrichAsync(
        EventTypeRecord eventObject,
        CancellationToken cancellationToken) {

        if (!_options.ResolveDns || _dnsConcurrency == null) {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (eventObject is Rules.ActiveDirectory.SMBServerAudit smbServerAudit) {
            await smbServerAudit
                .ResolveClientDnsNameAsync(
                    ResolveDnsAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<DnsResponse> ResolveDnsAsync(
        string address,
        CancellationToken cancellationToken) {

        Lazy<Task<DnsResponse>> request = _pendingDnsRequests.GetOrAdd(
            address,
            key => new Lazy<Task<DnsResponse>>(
                () => RunDnsQueryAsync(key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try {
            return await request.Value.ConfigureAwait(false);
        } finally {
            ((ICollection<KeyValuePair<string, Lazy<Task<DnsResponse>>>>)_pendingDnsRequests)
                .Remove(new KeyValuePair<string, Lazy<Task<DnsResponse>>>(address, request));
        }
    }

    private async Task<DnsResponse> RunDnsQueryAsync(
        string address,
        CancellationToken cancellationToken) {

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(
            _options.DnsTimeoutMilliseconds);
        bool entered = false;
        try {
            await _dnsConcurrency!
                .WaitAsync(timeoutCancellation.Token)
                .ConfigureAwait(false);
            entered = true;

            DnsResponse response = _resolver != null
                ? await _resolver(address, timeoutCancellation.Token).ConfigureAwait(false)
                : await _dnsClient!.Resolve(
                    address,
                    DnsRecordType.PTR,
                    retryOnTransient: _options.RetryDnsOnTransient,
                    maxRetries: _options.RetryDnsOnTransient ? 1 : 0,
                    cancellationToken: timeoutCancellation.Token).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (timeoutCancellation.IsCancellationRequested) {
                throw new TimeoutException(
                    $"The reverse-DNS lookup exceeded {_options.DnsTimeoutMilliseconds} ms.");
            }
            return response;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(cancellationToken);
        } catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested) {
            throw new TimeoutException(
                $"The reverse-DNS lookup exceeded {_options.DnsTimeoutMilliseconds} ms.");
        } finally {
            if (entered) {
                _dnsConcurrency!.Release();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        _dnsClient?.Dispose();
        _dnsConcurrency?.Dispose();
    }
}
