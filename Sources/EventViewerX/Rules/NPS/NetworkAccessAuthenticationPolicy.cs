namespace EventViewerX.Rules.NPS;

/// <summary>
/// Network Access Authentication Policy
/// 6272: Network Policy Server granted access to a user
/// 6273: Network Policy Server denied access to a user
/// </summary>
public class NetworkAccessAuthenticationPolicy : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 6272, 6273 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.NetworkAccessAuthenticationPolicy;

    /// <summary>Accepts any event whose ID/log match the overrides.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }
    /// <summary>
    /// Computer where the policy event originated.
    /// </summary>
    public string Computer;

    /// <summary>
    /// Brief description of the action.
    /// </summary>
    public string Action;

    /// <summary>
    /// Security identifier of the user.
    /// </summary>
    public string SecurityID;

    /// <summary>
    /// Account name of the user.
    /// </summary>
    public string AccountName;

    /// <summary>
    /// Domain of the user account.
    /// </summary>
    public string AccountDomain;

    /// <summary>
    /// Called station identifier.
    /// </summary>
    public string CalledStationID;

    /// <summary>
    /// Calling station identifier.
    /// </summary>
    public string CallingStationID;

    /// <summary>
    /// IPv4 address of the NAS device.
    /// </summary>
    public string NASIPv4Address;

    /// <summary>
    /// IPv6 address of the NAS device.
    /// </summary>
    public string NASIPv6Address;

    /// <summary>
    /// NAS identifier string.
    /// </summary>
    public string NASIdentifier;

    /// <summary>
    /// Type of the network access server port.
    /// </summary>
    public NasPortType? NASPortType;

    /// <summary>
    /// NAS port number.
    /// </summary>
    public string NASPort;

    /// <summary>
    /// Friendly name of the client.
    /// </summary>
    public string ClientFriendlyName;

    /// <summary>
    /// Client IP address in readable form.
    /// </summary>
    public string ClientFriendlyIPAddress;

    /// <summary>
    /// Connection request policy name.
    /// </summary>
    public string ConnectionRequestPolicyName;

    /// <summary>
    /// Network policy name applied.
    /// </summary>
    public string NetworkPolicyName;

    /// <summary>
    /// Authentication provider used.
    /// </summary>
    public string AuthenticationProvider;

    /// <summary>
    /// Server performing the authentication.
    /// </summary>
    public string AuthenticationServer;

    /// <summary>
    /// Authentication type selected.
    /// </summary>
    public AuthenticationType AuthenticationType;

    internal static AuthenticationType ParseAuthenticationType(string value) {
        return Enum.TryParse<AuthenticationType>(value, true, out var parsed) ? parsed : AuthenticationType.Unknown;
    }

    /// <summary>
    /// EAP type value if applicable.
    /// </summary>
    public string EAPType;

    /// <summary>
    /// Human readable reason string.
    /// </summary>
    public string Reason;

    /// <summary>
    /// Numeric reason code.
    /// </summary>
    public string ReasonCode;

    /// <summary>
    /// User that triggered the policy event.
    /// </summary>
    public string Who;

    /// <summary>
    /// Time when the policy event occurred.
    /// </summary>
    public DateTime When;

    /// <summary>Initialises an NPS authentication policy wrapper from an event record.</summary>
    public NetworkAccessAuthenticationPolicy(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "NetworkAccessAuthenticationPolicy";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        SecurityID = SourceEvent.GetDataValueOrEmpty("SubjectUserSid");
        AccountName = SourceEvent.GetDataValueOrEmpty(KnownEventField.SubjectUserName);
        AccountDomain = SourceEvent.GetDataValueOrEmpty(KnownEventField.SubjectDomainName);

        CalledStationID = SourceEvent.GetDataValueOrEmpty("CalledStationID");
        CallingStationID = SourceEvent.GetDataValueOrEmpty("CallingStationID");

        NASIPv4Address = SourceEvent.GetDataValueOrEmpty("NASIPv4Address");
        NASIPv6Address = SourceEvent.GetDataValueOrEmpty("NASIPv6Address");

        NASIdentifier = SourceEvent.GetDataValueOrEmpty("NASIdentifier");
        NASPort = SourceEvent.GetDataValueOrEmpty("NASPort");
        NASPortType = EventsHelper.GetNasPortType(
            SourceEvent.GetDataValueOrEmpty("NASPortType"));


        AuthenticationProvider = SourceEvent.GetDataValueOrEmpty("AuthenticationProvider");
        AuthenticationServer = SourceEvent.GetDataValueOrEmpty("AuthenticationServer");
        var authType = SourceEvent.GetDataValueOrEmpty("AuthenticationType");
        AuthenticationType = ParseAuthenticationType(authType);

        EAPType = SourceEvent.GetDataValueOrEmpty("EAPType");

        ClientFriendlyIPAddress = SourceEvent.GetDataValueOrEmpty("ClientIPAddress");
        ClientFriendlyName = SourceEvent.GetDataValueOrEmpty("ClientName");

        ConnectionRequestPolicyName = SourceEvent.GetDataValueOrEmpty("ProxyPolicyName");

        NetworkPolicyName = SourceEvent.GetDataValueOrEmpty("NetworkPolicyName");

        Reason = SourceEvent.GetDataValueOrEmpty("Reason");
        ReasonCode = SourceEvent.GetDataValueOrEmpty("ReasonCode");
        // common fields
        Who = SourceEvent.GetDataValueOrEmpty("FullyQualifiedSubjectUserName");
        When = SourceEvent.TimeCreated;
    }
}
