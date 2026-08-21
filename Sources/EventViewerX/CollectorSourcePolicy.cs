using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;

namespace EventViewerX;

/// <summary>Builds the Windows policy values required by source-initiated event forwarding.</summary>
public static class CollectorSourcePolicy {
    /// <summary>Builds one SubscriptionManager policy value for a collector.</summary>
    public static string BuildSubscriptionManagerValue(
        string collectorHostName,
        string transportName = "HTTP",
        int transportPort = 0,
        int refreshIntervalSeconds = 60) {

        if (string.IsNullOrWhiteSpace(collectorHostName)) {
            throw new ArgumentException("Collector host name cannot be empty.", nameof(collectorHostName));
        }
        string host = collectorHostName.Trim();
        if (host.IndexOfAny(new[] { '/', '\\', ',', ' ', '\t', '\r', '\n' }) >= 0) {
            throw new ArgumentException("Collector host name must be a DNS name, NetBIOS name, or IP address.", nameof(collectorHostName));
        }
        string transport = transportName?.Trim().ToUpperInvariant() ?? string.Empty;
        if (transport != "HTTP" && transport != "HTTPS") {
            throw new ArgumentException("Transport name must be HTTP or HTTPS.", nameof(transportName));
        }
        if (transportPort < 0 || transportPort > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(transportPort));
        }
        if (refreshIntervalSeconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(refreshIntervalSeconds));
        }
        int port = transportPort > 0 ? transportPort : transport == "HTTPS" ? 5986 : 5985;
        return string.Format(
            CultureInfo.InvariantCulture,
            "Server={0}://{1}:{2}/wsman/SubscriptionManager/WEC,Refresh={3}",
            transport.ToLowerInvariant(),
            host,
            port,
            refreshIntervalSeconds);
    }

    /// <summary>Builds source authorization SDDL from explicit computer or group SIDs.</summary>
    public static string BuildAllowedSourceSddl(
        IEnumerable<string> sourceSids,
        bool includeDomainComputers = false,
        bool includeNetworkService = true) {

        if (sourceSids == null) {
            throw new ArgumentNullException(nameof(sourceSids));
        }
        string[] normalized = sourceSids
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0 && !includeDomainComputers) {
            throw new ArgumentException("At least one source SID is required when Domain Computers is not included.", nameof(sourceSids));
        }
        foreach (string value in normalized) {
            try {
                _ = new SecurityIdentifier(value);
            } catch (ArgumentException exception) {
                throw new ArgumentException($"Source SID '{value}' is not valid.", nameof(sourceSids), exception);
            }
        }

        string dacl = string.Concat(
            includeDomainComputers ? "(A;;GA;;;DC)" : string.Empty,
            string.Concat(normalized.Select(static sid => $"(A;;GA;;;{sid})")),
            includeNetworkService ? "(A;;GA;;;NS)" : string.Empty);
        string sddl = "O:NSG:NSD:" + dacl;
        _ = new RawSecurityDescriptor(sddl);
        return sddl;
    }
}
