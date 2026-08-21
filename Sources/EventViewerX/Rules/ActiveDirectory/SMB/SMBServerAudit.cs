using EventViewerX;
using System.Net;

using DnsClientX;

namespace EventViewerX.Rules.ActiveDirectory;
/// <summary>
/// SMB Server Audit
/// 3000: SMB1 access
///
/// Before running the script, you need to enable the audit policy on the server.
/// Set-SmbServerConfiguration -AuditSmb1Access $true
///
/// Alternatively via registry:
/// HKLM\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters
/// AuditSmb1Access => REG_DWORD => 1
/// </summary>
public class SMBServerAudit : EventRuleBase {
    // public EventObject EventObject { get; }
    /// <summary>Server that raised the SMB audit event.</summary>
    public string Computer;
    /// <summary>Short description of the SMB audit action.</summary>
    public string Action;
    /// <summary>Client IP or host that accessed SMB.</summary>
    public string ClientAddress;
    /// <summary>Reverse DNS name for the client when resolved.</summary>
    public string ClientDNSName = string.Empty;
    /// <summary>Outcome of optional reverse-DNS enrichment.</summary>
    public ReverseDnsResolutionStatus ClientDnsResolutionStatus { get; private set; } = ReverseDnsResolutionStatus.NotRequested;
    /// <summary>Diagnostic text for a failed reverse-DNS lookup.</summary>
    public string ClientDnsResolutionError { get; private set; } = string.Empty;
    /// <summary>Timestamp of the SMB access.</summary>
    public DateTime When;
    /// <inheritdoc />
    public override List<int> EventIds => new() { 3000 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-SMBServer/Audit";
    /// <inheritdoc />
    public override EventType Type => EventType.ADSMBServerAuditV1;

    /// <summary>
    /// Accepts events emitted by the Microsoft-Windows-SMBServer provider.
    /// </summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider matches; otherwise <c>false</c>.</returns>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-SMBServer");
    }

    /// <summary>Initialises an SMB audit wrapper from an event record.</summary>
    public SMBServerAudit(EventObject eventObject) : base(eventObject) {
        //EventObject = eventObject;

        SourceEvent = eventObject;
        TypeName = "ADSMBServerAuditV1";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ClientAddress = SourceEvent.GetValueFromDataDictionary("ClientName");
        When = SourceEvent.TimeCreated;
    }

    /// <summary>
    /// Resolves and stores the client reverse-DNS name on demand.
    /// </summary>
    /// <returns>The resolved DNS name, or an empty string when no result is available.</returns>
    public Task<string> ResolveClientDnsNameAsync() {
        return ResolveClientDnsNameAsync(CancellationToken.None);
    }

    /// <summary>
    /// Resolves and stores the client reverse-DNS name on demand.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the lookup.</param>
    /// <returns>The resolved DNS name, or an empty string when no result is available.</returns>
    public async Task<string> ResolveClientDnsNameAsync(CancellationToken cancellationToken) {
        using var enricher = new EventEnricher(new EventEnrichmentOptions { ResolveDns = true });
        await enricher.EnrichAsync(this, cancellationToken).ConfigureAwait(false);
        return ClientDNSName;
    }

    internal async Task ResolveClientDnsNameAsync(
        Func<string, CancellationToken, Task<DnsResponse>> resolver,
        CancellationToken cancellationToken) {
        if (resolver == null) {
            throw new ArgumentNullException(nameof(resolver));
        }

        ClientDNSName = string.Empty;
        ClientDnsResolutionError = string.Empty;
        string normalizedAddress = NormalizeClientAddress(ClientAddress);
        if (string.IsNullOrWhiteSpace(normalizedAddress)) {
            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.InvalidAddress;
            return;
        }
        if (!IPAddress.TryParse(normalizedAddress, out _)) {
            if (Uri.CheckHostName(normalizedAddress) == UriHostNameType.Dns) {
                ClientDNSName = normalizedAddress.TrimEnd('.');
                ClientDnsResolutionStatus = ReverseDnsResolutionStatus.AlreadyNamed;
            } else {
                ClientDnsResolutionStatus = ReverseDnsResolutionStatus.InvalidAddress;
            }
            return;
        }

        try {
            Settings._logger.WriteVerbose($"Querying reverse DNS for address: {normalizedAddress}");
            DnsResponse response = await resolver(normalizedAddress, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedNames = new List<string>();
            var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DnsAnswer answer in response.Answers) {
                if (answer.Type != DnsRecordType.PTR || string.IsNullOrWhiteSpace(answer.Data)) {
                    continue;
                }

                string name = answer.Data.Trim().TrimEnd('.');
                if (name.Length > 0 && uniqueNames.Add(name)) {
                    resolvedNames.Add(name);
                }
            }

            if (resolvedNames.Count > 0) {
                ClientDNSName = string.Join(", ", resolvedNames);
                ClientDnsResolutionStatus = ReverseDnsResolutionStatus.Resolved;
                Settings._logger.WriteVerbose($"Resolved reverse DNS names: {ClientDNSName}");
                return;
            }

            if (response.Status == DnsResponseCode.NoError || response.Status == DnsResponseCode.NXDomain) {
                ClientDnsResolutionStatus = ReverseDnsResolutionStatus.NoRecord;
                return;
            }

            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.Failed;
            ClientDnsResolutionError = string.IsNullOrWhiteSpace(response.Error)
                ? $"DNS resolver returned {response.Status}."
                : response.Error;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.Cancelled;
            throw;
        } catch (OperationCanceledException ex) {
            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.TimedOut;
            ClientDnsResolutionError = string.IsNullOrWhiteSpace(ex.Message)
                ? "The DNS lookup timed out."
                : ex.Message;
        } catch (TimeoutException ex) {
            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.TimedOut;
            ClientDnsResolutionError = ex.Message;
        } catch (Exception ex) {
            ClientDnsResolutionStatus = ReverseDnsResolutionStatus.Failed;
            ClientDnsResolutionError = ex.Message;
            Settings._logger.WriteVerbose($"Querying reverse DNS for address '{normalizedAddress}' failed: {ex.Message}");
        }
    }

    private static string NormalizeClientAddress(string? clientAddress) {
        string value = clientAddress?.Trim() ?? string.Empty;
        value = value.TrimStart('\\');
        if (value.Length >= 2 && value[0] == '[' && value[value.Length - 1] == ']') {
            value = value.Substring(1, value.Length - 2);
        }
        return value;
    }
}
