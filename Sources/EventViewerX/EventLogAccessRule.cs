namespace EventViewerX;

/// <summary>
/// Describes one explicit access-control entry from an event log's channel security descriptor.
/// This is not an effective-access calculation; group membership and ACE ordering may affect access.
/// </summary>
public sealed class EventLogAccessRule {
    internal EventLogAccessRule(string trusteeSid, string aceType, bool isAllow, bool isDeny, bool isInherited, int accessMask) {
        TrusteeSid = trusteeSid;
        AceType = aceType;
        IsAllow = isAllow;
        IsDeny = isDeny;
        IsInherited = isInherited;
        AccessMask = accessMask;
    }

    /// <summary>SID of the trustee named by the ACE, independent of account-name localization.</summary>
    public string TrusteeSid { get; }

    /// <summary>Native ACE type, retained so callers can distinguish unusual entries.</summary>
    public string AceType { get; }

    /// <summary>True when this ACE explicitly allows its rights.</summary>
    public bool IsAllow { get; }

    /// <summary>True when this ACE explicitly denies its rights.</summary>
    public bool IsDeny { get; }

    /// <summary>True when this ACE was inherited.</summary>
    public bool IsInherited { get; }

    /// <summary>Unmodified native access mask from the channel security descriptor.</summary>
    public int AccessMask { get; }

    /// <summary>True when the ACE contains the event-log Read bit (0x1).</summary>
    public bool IncludesReadRight => (AccessMask & 0x1) != 0;

    /// <summary>True when the ACE contains the event-log Write bit (0x2).</summary>
    public bool IncludesWriteRight => (AccessMask & 0x2) != 0;

    /// <summary>True when the ACE contains the event-log Clear bit (0x4).</summary>
    public bool IncludesClearRight => (AccessMask & 0x4) != 0;
}
