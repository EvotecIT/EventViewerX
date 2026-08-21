namespace EventViewerX;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a Windows Event Collector (WEC) subscription stored under
/// HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\EventCollector\Subscriptions.
/// </summary>
public sealed class SubscriptionInfo {
    /// <summary>Subscription name (registry key).</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Administrator-provided description.</summary>
    public string? Description { get; set; }
    /// <summary>Indicates whether the subscription is enabled.</summary>
    public bool? Enabled { get; set; }
    /// <summary>Content format (Events, RenderedText, etc.).</summary>
    public string? ContentFormat { get; set; }
    /// <summary>Delivery mode (Pull, Push, or Custom).</summary>
    public string? DeliveryMode { get; set; }
    /// <summary>Collector machine that hosts the subscription.</summary>
    public string? MachineName { get; set; }
    /// <summary>Raw XML definition as stored in the registry.</summary>
    public string? RawXml { get; set; }
    /// <summary>Event queries included in the subscription.</summary>
    public IReadOnlyList<string> Queries { get; set; } = Array.Empty<string>();
}

