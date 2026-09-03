using System;
using System.Collections.Generic;

namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Password synchronization failed
/// Event ID 611: Failed password synchronization
/// </summary>
public class PasswordSyncFailed : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 611 };

    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };

    /// <inheritdoc />
    public override EventType Type => EventType.AADConnectPasswordSyncFailed;

    /// <summary>Accepts all password sync failure events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    /// <summary>Server running Azure AD Connect.</summary>
    public string Computer;

    /// <summary>User whose password failed to sync.</summary>
    public string User;

    /// <summary>Error code associated with the failure.</summary>
    public string Error;

    /// <summary>Timestamp of the failure.</summary>
    public DateTime When;

    /// <summary>Initialises a password sync failure wrapper from an event record.</summary>
    public PasswordSyncFailed(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "AADConnectPasswordSyncFailed";
        Computer = SourceEvent.ComputerName;
        User = SourceEvent.GetValueFromDataDictionary("User", "AccountName");
        Error = SourceEvent.GetValueFromDataDictionary("ErrorCode", "FailureCode");
        When = SourceEvent.TimeCreated;
        if (string.IsNullOrEmpty(User)) {
            ParseMessage(SourceEvent.Message);
        }
    }

    private void ParseMessage(string message) {
        if (string.IsNullOrEmpty(message)) return;
        var match = System.Text.RegularExpressions.Regex.Match(message, "user:?\\s*(?<user>[^\\s'\\\"]+)");
        if (match.Success) {
            User = match.Groups["user"].Value;
        }
    }
}
