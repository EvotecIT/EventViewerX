using Microsoft.Win32;

namespace EventViewerX;

/// <summary>
/// Reads normalized Windows Event Collector subscription configuration.
/// </summary>
public static partial class CollectorSubscriptionManager {
    // Windows Event Collector (WEC) subscriptions registry path
    private const string SubscriptionsKey = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\EventCollector\\Subscriptions";

    /// <summary>
    /// Enumerates Windows Event Collector (WEC) subscriptions from the registry.
    /// </summary>
    /// <param name="machineName">Remote computer or null for local.</param>
    public static IEnumerable<SubscriptionInfo> GetCollectorSubscriptions(string? machineName = null) {
        // First try 64-bit view (typical). If anything found, short-circuit for efficiency.
        var first = GetCollectorSubscriptionsInternal(machineName, RegistryView.Registry64).ToList();
        foreach (var sub in first) yield return sub;
        if (first.Count > 0) yield break;

        // Fallback to 32-bit view if 64-bit view had no data.
        foreach (var sub in GetCollectorSubscriptionsInternal(machineName, RegistryView.Registry32)) yield return sub;
    }

    /// <summary>
    /// Returns normalized collector subscription snapshots suitable for preview and reporting.
    /// </summary>
    public static IReadOnlyList<CollectorSubscriptionSnapshot> GetCollectorSubscriptionSnapshots(
        string? machineName = null,
        string? nameContains = null,
        bool enabledOnly = false) {
        var targetMachineName = ResolveCollectorTargetMachineName(machineName);

        IEnumerable<SubscriptionInfo> query = GetCollectorSubscriptions(machineName);
        if (!string.IsNullOrWhiteSpace(nameContains)) {
            query = query.Where(subscription =>
                !string.IsNullOrWhiteSpace(subscription.Name)
                && subscription.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (enabledOnly) {
            query = query.Where(static subscription => subscription.Enabled == true);
        }

        return query
            .OrderBy(static subscription => subscription.Name, StringComparer.OrdinalIgnoreCase)
            .Select(subscription => CollectorSubscriptionSnapshot.FromSubscriptionInfo(subscription, targetMachineName))
            .ToArray();
    }

    /// <summary>
    /// Returns a normalized collector subscription snapshot for the exact subscription name, if present.
    /// </summary>
    public static CollectorSubscriptionSnapshot? GetCollectorSubscriptionSnapshot(string name, string? machineName = null) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Subscription name cannot be null or empty.", nameof(name));
        }

        var targetMachineName = ResolveCollectorTargetMachineName(machineName);
        var subscription = GetCollectorSubscriptions(machineName)
            .FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Name)
                && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        return subscription is null
            ? null
            : CollectorSubscriptionSnapshot.FromSubscriptionInfo(subscription, targetMachineName);
    }

    private static IEnumerable<SubscriptionInfo> GetCollectorSubscriptionsInternal(string? machineName, RegistryView view) {
        var results = new List<SubscriptionInfo>();
        RegistryKey? hklm = null;
        RegistryKey? subsKey = null;
        try {
            hklm = string.IsNullOrEmpty(machineName)
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                : RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, machineName!, view);
            subsKey = hklm.OpenSubKey(SubscriptionsKey, writable: false);
            if (subsKey == null) {
                Settings._logger.WriteVerbose($"WEC subscriptions key not found or inaccessible on '{ResolveCollectorTargetMachineName(machineName)}' (view={view}).");
                return results;
            }

            foreach (var name in subsKey.GetSubKeyNames()) {
                using var subKey = subsKey.OpenSubKey(name, false);
                if (subKey == null) continue;

                var info = new SubscriptionInfo {
                    Name = name,
                    MachineName = machineName
                };

                // Enabled may be stored as DWORD or string; read loosely
                object? enabled = subKey.GetValue("Enabled");
                if (enabled is int dword) {
                    info.Enabled = dword != 0;
                } else if (enabled is string s) {
                    if (bool.TryParse(s, out var b)) info.Enabled = b;
                    else if (int.TryParse(s, out var i)) info.Enabled = i != 0;
                }

                info.ContentFormat = subKey.GetValue("ContentFormat") as string;
                info.DeliveryMode = subKey.GetValue("DeliveryMode") as string;
                info.Description = subKey.GetValue("Description") as string;

                string? queryXml = subKey.GetValue("Query") as string;
                if (!string.IsNullOrWhiteSpace(queryXml)) {
                    info.RawXml = queryXml;
                    info.Queries = ReadSelectQueries(queryXml!);
                }

                // Try multiple well-known value names to find the XML
                var xml = subKey.GetValue("Subscription") as string
                          ?? subKey.GetValue("SubscriptionXml") as string
                          ?? subKey.GetValue("Configuration") as string
                          ?? subKey.GetValue("Config") as string;
                if (!string.IsNullOrWhiteSpace(xml) && xml!.IndexOf("<Subscription", StringComparison.OrdinalIgnoreCase) >= 0) {
                    info.RawXml = xml;

                    if (CollectorSubscriptionXml.TryNormalize(xml, out var details, out var parseError)
                        && details != null) {
                        info.Description = details.Description;
                        info.Queries = details.Queries;
                    } else {
                        Settings._logger.WriteWarning($"Failed to parse subscription '{name}' XML on '{ResolveCollectorTargetMachineName(machineName)}': {parseError}");
                    }
                }

                results.Add(info);
            }
        } catch (UnauthorizedAccessException ex) {
            throw new InvalidOperationException(
                $"Access denied when enumerating WEC subscriptions on '{ResolveCollectorTargetMachineName(machineName)}' (view={view}). Ensure Remote Registry permissions and firewall allow access.",
                ex);
        } catch (System.Security.SecurityException ex) {
            throw new InvalidOperationException(
                $"Security policy blocked WEC subscription access on '{ResolveCollectorTargetMachineName(machineName)}' (view={view}).",
                ex);
        } catch (System.IO.IOException ex) {
            throw new InvalidOperationException(
                $"WEC subscription access failed on '{ResolveCollectorTargetMachineName(machineName)}' (view={view}). The Remote Registry service, firewall, or network path may be unavailable.",
                ex);
        } catch (Exception ex) {
            throw new InvalidOperationException(
                $"Failed to enumerate WEC subscriptions on '{ResolveCollectorTargetMachineName(machineName)}' (view={view}).",
                ex);
        } finally {
            subsKey?.Dispose();
            hklm?.Dispose();
        }
        return results;
    }

    private static IReadOnlyList<string> ReadSelectQueries(string queryXml) {
        string subscriptionXml =
            "<Subscription><Query><![CDATA[" +
            queryXml.Replace("]]>", "]]&gt;") +
            "]]></Query></Subscription>";
        return CollectorSubscriptionXml.TryNormalize(
                subscriptionXml,
                out CollectorSubscriptionXmlDetails? details,
                out _)
            ? details!.Queries
            : Array.Empty<string>();
    }

    private static string ResolveCollectorTargetMachineName(string? machineName) {
        if (machineName == null) {
            return Environment.MachineName;
        }

        var trimmed = machineName.Trim();
        return trimmed.Length == 0 ? Environment.MachineName : trimmed;
    }
}
