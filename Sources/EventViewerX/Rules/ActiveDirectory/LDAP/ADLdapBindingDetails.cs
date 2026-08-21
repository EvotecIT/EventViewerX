namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// LDAP binding details.
/// 2889: The security of this directory server can be significantly enhanced by configuring the server to reject SASL (Negotiate, Kerberos, NTLM, or Digest) LDAP binds that do not request signing (integrity verification) and LDAP simple binds that are performed on a cleartext (non-SSL/TLS-encrypted) connection. Even if this directory server is not used by Active Directory, it is recommended that this server be configured to reject such binds. For more details and information on how to make this configuration change to the server, please see http://go.microsoft.com/fwlink/?LinkID=87923.
/// </summary>
public class ADLdapBindingDetails : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 2889 };

    /// <inheritdoc />
    public override string LogName => "Directory Service";

    /// <inheritdoc />
    public override EventType Type => EventType.ADLdapBindingDetails;

    /// <summary>Accepts matching LDAP binding detail events without extra filters.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>Domain controller emitting the LDAP binding warning.</summary>
    public string Computer;

    /// <summary>Textual description of the security warning.</summary>
    public string Action;

    /// <summary>Raw remote endpoint string from the event.</summary>
    public string RemoteEndpoint;

    /// <summary>Parsed remote IP address.</summary>
    public string RemoteIp;

    /// <summary>Parsed remote port number if present.</summary>
    public int? RemotePort;

    /// <summary>Account name used during the LDAP bind.</summary>
    public string AccountName;

    /// <summary>Translated bind type (unsigned/simple/unknown).</summary>
    public string BindType;

    /// <summary>Time when the bind was recorded.</summary>
    public DateTime When;


    /// <summary>Initialises a detailed LDAP bind warning wrapper from an event record.</summary>
    public ADLdapBindingDetails(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "ADLdapBindingDetails";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        RemoteEndpoint = SourceEvent.GetValueFromDataDictionary("NoNameA0");
        (RemoteIp, RemotePort) = ParseEndpoint(RemoteEndpoint);
        AccountName = SourceEvent.GetValueFromDataDictionary("NoNameA1");
        BindType = TranslateBindType(SourceEvent.GetValueFromDataDictionary("NoNameA2"));
        When = SourceEvent.TimeCreated;
    }

    private static string TranslateBindType(string raw)
    {
        return raw switch
        {
            "0" => "Unsigned",
            "1" => "Simple",
            _ => string.IsNullOrWhiteSpace(raw) ? "Unknown" : raw
        };
    }

    private static (string ip, int? port) ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return (string.Empty, null);
        // Expect formats like "192.168.1.10:389" or "[::1]:389"
        try
        {
            var idx = endpoint.LastIndexOf(':');
            if (idx > 0 && idx < endpoint.Length - 1)
            {
                var ipPart = endpoint.Substring(0, idx).Trim('[', ']');
                var portPart = endpoint.Substring(idx + 1);
                return (ipPart, int.TryParse(portPart, out var p) ? p : null);
            }
            return (endpoint.Trim('[', ']'), null);
        }
        catch
        {
            return (endpoint, null);
        }
    }
}
