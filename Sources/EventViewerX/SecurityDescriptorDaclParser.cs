using System.Security.AccessControl;

namespace EventViewerX;

/// <summary>Parses the DACL once for channel and collector authorization views.</summary>
internal static class SecurityDescriptorDaclParser {
    internal static ParsedSecurityDescriptorDacl Parse(string sddl) {
        var descriptor = new RawSecurityDescriptor(sddl);
        if ((descriptor.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0) {
            return new ParsedSecurityDescriptorDacl(SecurityDescriptorDaclState.NotPresent, Array.Empty<ParsedSecurityDescriptorAce>());
        }

        RawAcl? dacl = descriptor.DiscretionaryAcl;
        if (dacl == null) {
            return new ParsedSecurityDescriptorDacl(SecurityDescriptorDaclState.Null, Array.Empty<ParsedSecurityDescriptorAce>());
        }
        if (dacl.Count == 0) {
            return new ParsedSecurityDescriptorDacl(SecurityDescriptorDaclState.Empty, Array.Empty<ParsedSecurityDescriptorAce>());
        }

        var rules = new List<ParsedSecurityDescriptorAce>(dacl.Count);
        foreach (GenericAce ace in dacl) {
            if (ace is not QualifiedAce qualified || ace is not KnownAce known) {
                throw new NotSupportedException($"DACL contains unsupported ACE type '{ace.AceType}'.");
            }

            rules.Add(new ParsedSecurityDescriptorAce(
                known.SecurityIdentifier.Value,
                ace.AceType.ToString(),
                qualified.AceQualifier == AceQualifier.AccessAllowed,
                qualified.AceQualifier == AceQualifier.AccessDenied,
                (ace.AceFlags & AceFlags.Inherited) != 0,
                known.AccessMask));
        }

        return new ParsedSecurityDescriptorDacl(SecurityDescriptorDaclState.Entries, rules.AsReadOnly());
    }
}

internal sealed class ParsedSecurityDescriptorDacl {
    internal ParsedSecurityDescriptorDacl(SecurityDescriptorDaclState daclState, IReadOnlyList<ParsedSecurityDescriptorAce> accessRules) {
        DaclState = daclState;
        AccessRules = accessRules;
    }

    internal SecurityDescriptorDaclState DaclState { get; }
    internal IReadOnlyList<ParsedSecurityDescriptorAce> AccessRules { get; }
}

internal sealed class ParsedSecurityDescriptorAce {
    internal ParsedSecurityDescriptorAce(string trusteeSid, string aceType, bool isAllow, bool isDeny, bool isInherited, int accessMask) {
        TrusteeSid = trusteeSid;
        AceType = aceType;
        IsAllow = isAllow;
        IsDeny = isDeny;
        IsInherited = isInherited;
        AccessMask = accessMask;
    }

    internal string TrusteeSid { get; }
    internal string AceType { get; }
    internal bool IsAllow { get; }
    internal bool IsDeny { get; }
    internal bool IsInherited { get; }
    internal int AccessMask { get; }
}
