using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace EventViewerX;

/// <summary>
/// Typed event projection with common event metadata and the original event snapshot.
/// </summary>
public class EventTypeRecord {
    /// <summary>
    /// Gets the detailed event snapshot used to build this rule result.
    /// </summary>
    public EventObject SourceEvent { get; protected set; } = null!;

    /// <summary>
    /// Identifier of the event.
    /// </summary>
    public int EventId { get; }

    /// <summary>
    /// Record identifier of the event.
    /// </summary>
    public long? RecordId { get; }

    /// <summary>
    /// Source machine from which the event was gathered.
    /// </summary>
    public string MachineName { get; }

    /// <summary>
    /// Log name where the event originated.
    /// </summary>
    public string SourceLogName { get; }

    /// <summary>Channel or file container from which the event was read.</summary>
    public string ContainerLogName { get; }

    /// <summary>Computer that originally emitted the event.</summary>
    public string SourceComputer { get; }

    /// <summary>Collector or direct query target from which the event was retrieved.</summary>
    public string CollectorComputer { get; }

    /// <summary>
    /// Name of the rule type handling the event.
    /// </summary>
    public string TypeName { get; protected set; } = string.Empty;

    /// <summary>Time at which the source event was created.</summary>
    public DateTime TimeCreated => SourceEvent.TimeCreated;

    /// <summary>Provider that emitted the source event.</summary>
    public string ProviderName => SourceEvent.ProviderName;

    /// <summary>Native Windows event severity level.</summary>
    public Level? Level => SourceEvent.Level.HasValue
        ? (EventViewerX.Level?)SourceEvent.Level.Value
        : null;

    /// <summary>Rendered source-event message.</summary>
    public string Message => SourceEvent.Message;

    private static readonly Dictionary<int, string> uacFlags = new() {
        { 0x0001, "SCRIPT" },
        { 0x0002, "ACCOUNTDISABLE" },
        { 0x0008, "HOMEDIR_REQUIRED" },
        { 0x0010, "LOCKOUT" },
        { 0x0020, "PASSWD_NOTREQD" },
        { 0x0040, "PASSWD_CANT_CHANGE" },
        { 0x0080, "ENCRYPTED_TEXT_PWD_ALLOWED" },
        { 0x0100, "TEMP_DUPLICATE_ACCOUNT" },
        { 0x0200, "NORMAL_ACCOUNT" },
        { 0x0800, "INTERDOMAIN_TRUST_ACCOUNT" },
        { 0x1000, "WORKSTATION_TRUST_ACCOUNT" },
        { 0x2000, "SERVER_TRUST_ACCOUNT" },
        { 0x10000, "DONT_EXPIRE_PASSWORD" },
        { 0x20000, "MNS_LOGON_ACCOUNT" },
        { 0x40000, "SMARTCARD_REQUIRED" },
        { 0x80000, "TRUSTED_FOR_DELEGATION" },
        { 0x100000, "NOT_DELEGATED" },
        { 0x200000, "USE_DES_KEY_ONLY" },
        { 0x400000, "DONT_REQ_PREAUTH" },
        { 0x800000, "PASSWORD_EXPIRED" },
        { 0x1000000, "TRUSTED_TO_AUTH_FOR_DELEGATION" },
        { 0x04000000, "PARTIAL_SECRETS_ACCOUNT" }
    };

    private static readonly Dictionary<string, string> OperationTypeLookup = new() {
        { "%%14674", "Value Added" },
        { "%%14675", "Value Deleted" },
        { "%%14676", "Unknown" }
    };

    /// <summary>
    /// Creates a typed projection of an <see cref="EventObject"/> for rule processing and serialization.
    /// </summary>
    /// <param name="eventObject">Full event wrapper to down-sample.</param>
    public EventTypeRecord(EventObject eventObject) {
        SourceEvent = eventObject ?? throw new ArgumentNullException(nameof(eventObject));
        EventId = SourceEvent.Id;
        RecordId = SourceEvent.RecordId;
        MachineName = SourceEvent.MachineName;
        SourceLogName = SourceEvent.OriginalLogName;
        ContainerLogName = SourceEvent.ContainerLogName;
        SourceComputer = SourceEvent.SourceComputer;
        CollectorComputer = SourceEvent.CollectorComputer;
    }

    internal static string ConvertToObjectAffected(EventObject eventObject) {
        if (eventObject.Data.TryGetValue("TargetUserName", out var targetUserName)) {
            if (eventObject.Data.TryGetValue("TargetDomainName", out var targetDomainName)) {
                return targetDomainName + "\\" + targetUserName;
            }

            return targetUserName;
        }

        return string.Empty;
    }

    internal static string ConvertToSamAccountName(EventObject eventObject) {
        return eventObject.Data.TryGetValue("SamAccountName", out var samAccountName)
            ? samAccountName
            : string.Empty;
    }

    internal string ConvertFromOperationType(string s) {
        if (OperationTypeLookup.ContainsKey(s)) {
            return OperationTypeLookup[s];
        }

        return "Unknown Operation";
    }

    internal static string OverwriteByField(string findField, string expectedValue, string currentValue, string insertValue) {
        if (findField == expectedValue) {
            return insertValue;
        }
        return currentValue;
    }

    internal static string TranslateUacValue(string hexValue) {
        if (hexValue == null || hexValue.Trim() == "-") {
            return "";
        }

        var uacValue = int.Parse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var translatedFlags = new List<string>();
        foreach (var flag in uacFlags) {
            if ((uacValue & flag.Key) != 0) {
                translatedFlags.Add(flag.Value);
            }
        }

        return string.Join(", ", translatedFlags);
    }
}
