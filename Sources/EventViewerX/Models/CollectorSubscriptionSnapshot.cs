namespace EventViewerX;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Canonical snapshot of a Windows Event Collector subscription for preview, rollback, and reporting.
/// </summary>
public sealed record CollectorSubscriptionSnapshot {
    /// <summary>Subscription name (registry key).</summary>
    public string SubscriptionName { get; set; } = string.Empty;

    /// <summary>Collector machine that hosts the subscription.</summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>Administrator-provided description.</summary>
    public string? Description { get; set; }

    /// <summary>Indicates whether the subscription is enabled.</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>Content format (Events, RenderedText, etc.).</summary>
    public string? ContentFormat { get; set; }

    /// <summary>Delivery mode (Pull, Push, or Custom).</summary>
    public string? DeliveryMode { get; set; }

    /// <summary>Destination event-log channel on the collector.</summary>
    public string? DestinationLog { get; set; }

    /// <summary>Raw XML definition as stored or requested.</summary>
    public string? RawXml { get; set; }

    /// <summary>Indicates whether any XML payload is present.</summary>
    public bool HasXml { get; set; }

    /// <summary>Number of event queries contained in the XML payload.</summary>
    public int QueryCount { get; set; }

    /// <summary>Normalized query fragments extracted from the XML payload.</summary>
    public IReadOnlyList<string> Queries { get; set; } = Array.Empty<string>();

    /// <summary>Optional local runtime state populated on request.</summary>
    public CollectorSubscriptionRuntimeStatus? RuntimeStatus { get; set; }

    /// <summary>
    /// Creates a reusable snapshot from a raw subscription model.
    /// </summary>
    public static CollectorSubscriptionSnapshot FromSubscriptionInfo(SubscriptionInfo subscription, string targetMachineName) {
        if (subscription == null) {
            throw new ArgumentNullException(nameof(subscription));
        }

        var queries = subscription.Queries?
            .Where(static query => !string.IsNullOrWhiteSpace(query))
            .Select(static query => query.Trim())
            .ToArray() ?? Array.Empty<string>();

        return new CollectorSubscriptionSnapshot {
            SubscriptionName = subscription.Name,
            MachineName = NormalizeOptional(subscription.MachineName) ?? targetMachineName,
            Description = NormalizeOptional(subscription.Description),
            IsEnabled = subscription.Enabled,
            ContentFormat = NormalizeOptional(subscription.ContentFormat),
            DeliveryMode = NormalizeOptional(subscription.DeliveryMode),
            DestinationLog = NormalizeOptional(subscription.DestinationLog),
            RawXml = NormalizeOptional(subscription.RawXml),
            HasXml = !string.IsNullOrWhiteSpace(subscription.RawXml) || queries.Length > 0,
            QueryCount = queries.Length,
            Queries = queries
        };
    }

    private static string? NormalizeOptional(string? value) {
        if (value == null) {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
