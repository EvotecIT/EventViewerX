namespace EventViewerX;

internal static class EventLogSecurityDescriptor {
    internal static EventLogParsedSecurityDescriptor Parse(string sddl) {
        ParsedSecurityDescriptorDacl parsed = SecurityDescriptorDaclParser.Parse(sddl);
        return new EventLogParsedSecurityDescriptor(
            parsed.DaclState,
            parsed.AccessRules
                .Select(static rule => new EventLogAccessRule(
                    rule.TrusteeSid,
                    rule.AceType,
                    rule.IsAllow,
                    rule.IsDeny,
                    rule.IsInherited,
                    rule.AccessMask))
                .ToList()
                .AsReadOnly());
    }
}

internal sealed class EventLogParsedSecurityDescriptor {
    internal EventLogParsedSecurityDescriptor(SecurityDescriptorDaclState daclState, IReadOnlyList<EventLogAccessRule> accessRules) {
        DaclState = daclState;
        AccessRules = accessRules;
    }

    internal SecurityDescriptorDaclState DaclState { get; }
    internal IReadOnlyList<EventLogAccessRule> AccessRules { get; }
}
