using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EventViewerX;

/// <summary>Creates a stable identity for the event currently stored at a checkpoint boundary.</summary>
public static class EventCheckpointBoundaryIdentity {
    /// <summary>Creates a SHA-256 identity from metadata that remains available in <see cref="EventReadMode.Metadata"/>.</summary>
    /// <param name="eventObject">Detached event whose stable metadata identifies the checkpoint boundary.</param>
    /// <returns>Uppercase hexadecimal SHA-256 identity.</returns>
    public static string Create(EventObject eventObject) {
        if (eventObject == null) {
            throw new ArgumentNullException(nameof(eventObject));
        }

        string?[] identity = {
            eventObject.RecordId?.ToString(CultureInfo.InvariantCulture),
            eventObject.TimeCreated.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            eventObject.Id.ToString(CultureInfo.InvariantCulture),
            eventObject.ProviderName,
            eventObject.ProviderId?.ToString("D"),
            NormalizeSource(eventObject.ContainerLog),
            NormalizeSource(eventObject.MachineName),
            eventObject.ActivityId?.ToString("D"),
            eventObject.RelatedActivityId?.ToString("D"),
            eventObject.UserId?.Value,
            eventObject.ProcessId?.ToString(CultureInfo.InvariantCulture),
            eventObject.ThreadId?.ToString(CultureInfo.InvariantCulture),
            eventObject.Version?.ToString(CultureInfo.InvariantCulture),
            eventObject.Level?.ToString(CultureInfo.InvariantCulture),
            eventObject.Task?.ToString(CultureInfo.InvariantCulture),
            eventObject.Opcode?.ToString(CultureInfo.InvariantCulture),
            eventObject.Keywords?.ToString(CultureInfo.InvariantCulture),
            eventObject.Qualifiers
        };

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(identity)));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static string NormalizeSource(string? source) {
        return (source ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
    }
}
