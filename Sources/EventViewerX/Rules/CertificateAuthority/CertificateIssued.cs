namespace EventViewerX.Rules.CertificateAuthority;

/// <summary>
/// Certificate issued by Certificate Authority
/// 4887: Certificate Services approved a certificate request and issued a certificate
/// </summary>
public class CertificateIssued : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4887 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Security-Auditing" };
    /// <inheritdoc />
    public override EventType Type => EventType.CertificateIssued;

    /// <summary>Accepts issued certificates, excluding requests that have only been received.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return eventObject.Id == 4887 && RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing");
    }
    /// <summary>Certificate Services host.</summary>
    public string Computer;
    /// <summary>Certificate issuance action.</summary>
    public string Action;
    /// <summary>Certificate template involved.</summary>
    public string CertificateTemplate;
    /// <summary>Requester (subject) of the certificate.</summary>
    public string Requester;
    /// <summary>Issued certificate serial number.</summary>
    public string SerialNumber;
    /// <summary>Certificate Authority request identifier, preserved as emitted.</summary>
    public string RequestId;
    /// <summary>Issued certificate subject, when present in the event version.</summary>
    public string Subject;
    /// <summary>Attributes attached to the certificate request.</summary>
    public string Attributes;
    /// <summary>User account responsible for the operation.</summary>
    public string Who;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a certificate issuance wrapper from an event record.</summary>
    public CertificateIssued(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "CertificateIssued";
        Computer = SourceEvent.ComputerName;
        Action = "Certificate issued";
        RequestId = Value("RequestId");
        Subject = Value("Subject", "RequestSubjectName");
        Attributes = Value("Attributes");
        CertificateTemplate = Value("CertificateTemplate", "CertificateTemplateOid");
        if (CertificateTemplate.Length == 0) {
            foreach (string attribute in Attributes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                string text = attribute.Trim();
                const string prefix = "CertificateTemplate:";
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    CertificateTemplate = text.Substring(prefix.Length).Trim();
                    break;
                }
            }
        }
        Requester = Value("Requester");
        SerialNumber = Value("SerialNumber");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }

}



