using System.Reflection;
using EventViewerX.Native;
using EventViewerX.Rules.Kerberos;
using Xunit;

namespace EventViewerX.Tests;

public class TestKerberosEncryptionContracts
{
    [Theory]
    [InlineData("0x08", KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1, true, false)]
    [InlineData("0x10", KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1, true, false)]
    [InlineData("0x1C (RC4, AES128-SHA96, AES256-SHA96)",
        KerberosSupportedEncryptionTypes.Rc4Hmac |
        KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1 |
        KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1,
        true,
        true)]
    [InlineData("0x20", KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1SessionKey, true, false)]
    [InlineData("0x24", KerberosSupportedEncryptionTypes.Rc4Hmac | KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1SessionKey, true, true)]
    [InlineData("24", KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1 | KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1, true, false)]
    [InlineData("0", KerberosSupportedEncryptionTypes.NotDefined, false, false)]
    public void SupportedEncryptionTypes_PreserveCorrectWindowsBitAssignments(
        string raw,
        KerberosSupportedEncryptionTypes expected,
        bool supportsAes,
        bool supportsRc4)
    {
        KerberosSupportedEncryptionTypes? parsed = EventsHelper.GetKerberosSupportedEncryptionTypes(raw);

        Assert.Equal(expected, parsed);
        Assert.Equal(supportsAes, EventsHelper.SupportsKerberosAes(parsed));
        Assert.Equal(supportsRc4, EventsHelper.SupportsKerberosRc4(parsed));
    }

    [Fact]
    public void SupportedEncryptionTypes_PreserveUnknownBitsAndMissingEvidence()
    {
        KerberosSupportedEncryptionTypes? parsed = EventsHelper.GetKerberosSupportedEncryptionTypes("0x80000018");

        Assert.Equal(0x80000018U, (uint)parsed!.Value);
        Assert.True(EventsHelper.SupportsKerberosAes(parsed));
        Assert.Null(EventsHelper.GetKerberosSupportedEncryptionTypes("N/A"));
        Assert.Null(EventsHelper.SupportsKerberosAes(null));
        Assert.Null(EventsHelper.SupportsKerberosRc4(null));
    }

    [Theory]
    [InlineData(201, KerberosKdcRc4Issue.ClientOnlySupportsInsecureEncryption, KerberosKdcRc4Disposition.AuditWarning)]
    [InlineData(202, KerberosKdcRc4Issue.ServiceOnlyHasInsecureKeys, KerberosKdcRc4Disposition.AuditWarning)]
    [InlineData(203, KerberosKdcRc4Issue.ClientOnlySupportsInsecureEncryption, KerberosKdcRc4Disposition.EnforcementBlock)]
    [InlineData(204, KerberosKdcRc4Issue.ServiceOnlyHasInsecureKeys, KerberosKdcRc4Disposition.EnforcementBlock)]
    [InlineData(205, KerberosKdcRc4Issue.ExplicitInsecureDomainDefault, KerberosKdcRc4Disposition.ExplicitConfigurationWarning)]
    [InlineData(206, KerberosKdcRc4Issue.ClientDoesNotSupportAes, KerberosKdcRc4Disposition.AuditWarning)]
    [InlineData(207, KerberosKdcRc4Issue.ServiceMissingAesKeys, KerberosKdcRc4Disposition.AuditWarning)]
    [InlineData(208, KerberosKdcRc4Issue.ClientDoesNotSupportAes, KerberosKdcRc4Disposition.EnforcementBlock)]
    [InlineData(209, KerberosKdcRc4Issue.ServiceMissingAesKeys, KerberosKdcRc4Disposition.EnforcementBlock)]
    public void KdcRc4Events_ClassifyEventLocalMeaning(
        int eventId,
        KerberosKdcRc4Issue expectedIssue,
        KerberosKdcRc4Disposition expectedDisposition)
    {
        EventObject source = BuildEventObject(eventId, "System", "Kdcsvc", new Dictionary<string, string>(), string.Empty);
        var rule = new KerberosKdcRc4Audit(source);

        Assert.Equal(expectedIssue, rule.Issue);
        Assert.Equal(expectedDisposition, rule.Disposition);
    }

    [Fact]
    public void KdcRc4Event_ParsesRepeatedMessageLabelsWithinTheirSections()
    {
        const string message = """
            The Key Distribution Center detected RC4 usage that will be unsupported in enforcement phase.
            Account Information
                Account Name: CLIENT01$
                Supplied Realm Name: AD.EVOTEC.XYZ
                msds-SupportedEncryptionTypes: 0x4 (RC4)
                Available Keys: RC4
            Service Information:
                Service Name: svc-legacy
                Service ID: S-1-5-21-1-2-3-1001
                msds-SupportedEncryptionTypes: 0x1C (RC4, AES128-SHA96, AES256-SHA96)
                Available Keys: AES-SHA1, RC4
            Domain Controller Information:
                msds-SupportedEncryptionTypes: 0x18 (AES128-SHA96, AES256-SHA96)
                DefaultDomainSupportedEncTypes: 0x18
                Available Keys: AES-SHA1, RC4
            Network Information:
                Client Address: ::ffff:192.168.241.50
                Client Port: 53001
                Advertized Etypes: RC4-HMAC-NT
            """;
        EventObject source = BuildEventObject(201, "System", "Kdcsvc", new Dictionary<string, string>(), message);

        var rule = new KerberosKdcRc4Audit(source);

        Assert.Equal("CLIENT01$", rule.AccountName);
        Assert.Equal("AD.EVOTEC.XYZ", rule.SuppliedRealmName);
        Assert.Equal(KerberosSupportedEncryptionTypes.Rc4Hmac, rule.AccountSupportedEncryptionTypes);
        Assert.Equal(
            KerberosSupportedEncryptionTypes.Rc4Hmac |
            KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1 |
            KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1,
            rule.ServiceSupportedEncryptionTypes);
        Assert.Equal(
            KerberosSupportedEncryptionTypes.Aes128CtsHmacSha1 |
            KerberosSupportedEncryptionTypes.Aes256CtsHmacSha1,
            rule.DomainControllerSupportedEncryptionTypes);
        Assert.Equal("192.168.241.50", rule.ClientAddress);
        Assert.Equal("RC4-HMAC-NT", rule.ClientAdvertizedEncryptionTypes);
    }

    [Fact]
    public void KdcRc4Event_UsesVersionZeroPayloadPositionsWithoutEnglishMessageLabels()
    {
        object?[] properties = {
            "RC4-HMAC",
            "CLIENT01$",
            "AD.EVOTEC.XYZ",
            "0x4 (RC4)",
            "RC4",
            "svc-legacy",
            "S-1-5-21-1-2-3-1001",
            "0x1C (RC4, AES128-SHA96, AES256-SHA96)",
            "AES-SHA1, RC4",
            "0x18 (AES128-SHA96, AES256-SHA96)",
            "0x18",
            "AES-SHA1, RC4",
            "::ffff:192.168.241.50",
            53001,
            "RC4-HMAC-NT"
        };
        EventObject source = BuildEventObject(
            201,
            "System",
            "Kdcsvc",
            new Dictionary<string, string>(),
            "Lokalisierte Meldung ohne englische Feldnamen.",
            properties);

        var rule = new KerberosKdcRc4Audit(source);

        Assert.Equal("RC4-HMAC", rule.CipherName);
        Assert.Equal("CLIENT01$", rule.AccountName);
        Assert.Equal("AD.EVOTEC.XYZ", rule.SuppliedRealmName);
        Assert.Equal(KerberosSupportedEncryptionTypes.Rc4Hmac, rule.AccountSupportedEncryptionTypes);
        Assert.Equal("svc-legacy", rule.ServiceName);
        Assert.Equal("S-1-5-21-1-2-3-1001", rule.ServiceSid);
        Assert.True(EventsHelper.SupportsKerberosAes(rule.ServiceSupportedEncryptionTypes));
        Assert.True(EventsHelper.SupportsKerberosAes(rule.DomainControllerSupportedEncryptionTypes));
        Assert.Equal("0x18", rule.DefaultDomainSupportedEncTypesRaw);
        Assert.Equal("192.168.241.50", rule.ClientAddress);
        Assert.Equal("53001", rule.ClientPort);
        Assert.Equal("RC4-HMAC-NT", rule.ClientAdvertizedEncryptionTypes);
    }

    [Fact]
    public void KdcRc4Event205_UsesVersionZeroPayloadPositionsWithoutMessageResources()
    {
        EventObject source = BuildEventObject(
            205,
            "System",
            "Kdcsvc",
            new Dictionary<string, string>(),
            string.Empty,
            new object?[] { "RC4-HMAC", "0x1C" });

        var rule = new KerberosKdcRc4Audit(source);

        Assert.Equal("RC4-HMAC", rule.EnabledInsecureCiphers);
        Assert.Equal("0x1C", rule.DefaultDomainSupportedEncTypesRaw);
        Assert.True(EventsHelper.SupportsKerberosRc4(rule.DefaultDomainSupportedEncTypes));
    }

    [Fact]
    public void KdcRc4Event205_PreservesExplicitDomainDefaultWithoutInventingDomainPosture()
    {
        const string message = """
            The Key Distribution Center detected explicit cipher enablement in the Default Domain Supported Encryption Types policy configuration.
            Cipher(s): RC4-HMAC
            DefaultDomainSupportedEncTypes: 0x1C
            """;
        EventObject source = BuildEventObject(205, "System", "Kdcsvc", new Dictionary<string, string>(), message);

        var rule = new KerberosKdcRc4Audit(source);

        Assert.Equal("RC4-HMAC", rule.EnabledInsecureCiphers);
        Assert.Equal("0x1C", rule.DefaultDomainSupportedEncTypesRaw);
        Assert.True(EventsHelper.SupportsKerberosRc4(rule.DefaultDomainSupportedEncTypes));
        Assert.Equal(KerberosKdcRc4Disposition.ExplicitConfigurationWarning, rule.Disposition);
    }

    [Fact]
    public void Kerberos4769_ProjectsCurrentEncryptionEvidenceAndKeepsMissingDistinct()
    {
        var data = new Dictionary<string, string> {
            ["TargetUserName"] = "CLIENT01$",
            ["TargetDomainName"] = "AD.EVOTEC.XYZ",
            ["LogonGuid"] = "{00000000-0000-0000-0000-000000000001}",
            ["AccountSupportedEncryptionTypes"] = "N/A",
            ["AccountAvailableKeys"] = "N/A",
            ["ServiceName"] = "svc-payroll",
            ["ServiceSid"] = "S-1-5-21-1-2-3-2001",
            ["ServiceSupportedEncryptionTypes"] = "0x1C (RC4, AES128-SHA96, AES256-SHA96)",
            ["ServiceAvailableKeys"] = "AES-SHA1, RC4",
            ["DCSupportedEncryptionTypes"] = "0x18 (AES128-SHA96, AES256-SHA96)",
            ["DCAvailableKeys"] = "AES-SHA1, RC4",
            ["IpAddress"] = "::ffff:10.20.30.40",
            ["IpPort"] = "52104",
            ["TicketOptions"] = "0x40810010",
            ["Status"] = "0x0",
            ["TicketEncryptionType"] = "0x12",
            ["SessionKeyEncryptionType"] = "0x17",
            ["ClientAdvertizedEncryptionTypes"] = "AES256-CTS-HMAC-SHA1-96 RC4-HMAC-NT",
            ["TransmittedServices"] = "-"
        };
        EventObject source = BuildEventObject(
            4769,
            "Security",
            "Microsoft-Windows-Security-Auditing",
            data,
            "A Kerberos service ticket was requested.");

        var rule = new KerberosServiceTicket(source);

        Assert.Null(rule.AccountSupportedEncryptionTypesFlags);
        Assert.True(EventsHelper.SupportsKerberosAes(rule.ServiceSupportedEncryptionTypesFlags));
        Assert.True(EventsHelper.SupportsKerberosRc4(rule.ServiceSupportedEncryptionTypesFlags));
        Assert.Equal(TicketEncryptionType.AES256_CTS_HMAC_SHA1_96, rule.EncryptionType);
        Assert.False(rule.WeakEncryptionAlgorithm);
        Assert.Equal(TicketEncryptionType.RC4_HMAC, rule.SessionKeyEncryptionType);
        Assert.True(rule.WeakSessionKeyEncryptionAlgorithm);
        Assert.Equal("10.20.30.40", rule.IpAddress);
        Assert.Contains("AES256", rule.EncryptionTypeText);
        Assert.Contains("RC4", rule.SessionKeyEncryptionTypeText);

        var sessionOnlyWeakness = new Dictionary<string, object?> {
            ["WeakEncryptionAlgorithm"] = false,
            ["WeakSessionKeyEncryptionAlgorithm"] = true
        };
        foreach (string ruleId in new[] { "EVX-AUTH-0004", "EVX-AUTH-0005" }) {
            EventDetectionRuleDefinition definition = EventDetectionCatalog.GetBuiltInRules()
                .Single(candidate => candidate.Definition.RuleId == ruleId)
                .Definition;
            Assert.NotNull(definition.Predicate);
            Assert.True(EventPredicateEvaluator.Matches(definition.Predicate!, sessionOnlyWeakness));
        }
    }

    [Fact]
    public void Kerberos4768_OlderSchemaKeepsUnavailableTypedFieldsNull()
    {
        EventObject source = BuildEventObject(
            4768,
            "Security",
            "Microsoft-Windows-Security-Auditing",
            new Dictionary<string, string> {
                ["TargetUserName"] = "legacy-client$",
                ["TargetDomainName"] = "AD.EVOTEC.XYZ"
            },
            "A Kerberos authentication ticket was requested.");

        var rule = new KerberosTGTRequest(source);

        Assert.Null(rule.EncryptionType);
        Assert.Null(rule.SessionKeyEncryptionType);
        Assert.Null(rule.PreAuthType);
        Assert.Null(rule.PreAuthEncryptionType);
        Assert.Null(rule.AccountSupportedEncryptionTypesFlags);
        Assert.Null(rule.ServiceSupportedEncryptionTypesFlags);
        Assert.Null(rule.DCSupportedEncryptionTypesFlags);
        Assert.False(rule.WeakEncryptionAlgorithm);
        Assert.False(rule.WeakSessionKeyEncryptionAlgorithm);
    }

    [Fact]
    public void KdcRc4SchemaEvidenceDistinguishesCompleteMissingAndUnavailableMetadata()
    {
        EventReadinessConfigurationEvidence complete = EventReadinessEvidenceProvider.EvaluateKdcRc4EventSchema(
            CreateKdcProvider(Enumerable.Range(201, 9).Select(static id => (long)id).ToArray()));
        EventReadinessConfigurationEvidence incomplete = EventReadinessEvidenceProvider.EvaluateKdcRc4EventSchema(
            CreateKdcProvider(Enumerable.Range(201, 8).Select(static id => (long)id).ToArray()));
        EventReadinessConfigurationEvidence unavailable = EventReadinessEvidenceProvider.EvaluateKdcRc4EventSchema(
            CreateKdcProvider(Array.Empty<long>(), "Events: EventLogException: metadata unavailable"));
        EventReadinessConfigurationEvidence unsupported = EventReadinessEvidenceProvider.EvaluateKdcRc4EventSchema(
            CreateKdcProvider(
                Enumerable.Range(201, 9).Select(static id => (long)id).ToArray(),
                version: 1));

        Assert.Equal(EventReadinessStatus.Pass, complete.Status);
        Assert.Equal(EventReadinessStatus.Fail, incomplete.Status);
        Assert.Equal(EventReadinessDiagnosticKind.Missing, incomplete.DiagnosticKind);
        Assert.Contains("209", incomplete.Evidence, StringComparison.Ordinal);
        Assert.Equal(EventReadinessStatus.Unknown, unavailable.Status);
        Assert.Equal(EventReadinessDiagnosticKind.Error, unavailable.DiagnosticKind);
        Assert.Contains("metadata unavailable", unavailable.Evidence, StringComparison.Ordinal);
        Assert.Equal(EventReadinessStatus.Fail, unsupported.Status);
        Assert.Contains("version 0", unsupported.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void KerberosComposites_IncludeKdcRc4Evidence()
    {
        Assert.Contains(EventType.KerberosKdcRc4Audit, EventTypeCatalog.Expand(new[] { EventType.KerberosActivity }));
        Assert.Contains(EventType.KerberosKdcRc4Audit, EventTypeCatalog.Expand(new[] { EventType.AuthenticationHealth }));
        EventTypeRequirement requirement = EventRequirementCatalog.GetRequirement(EventType.KerberosKdcRc4Audit);
        Assert.Contains(requirement.Sources, source => source.LogName == "System" && source.EventIds.Contains(201));
        Assert.Contains(requirement.Prerequisites, prerequisite => prerequisite.Key == "target-role:domain-controller");
        Assert.Contains(requirement.Prerequisites, prerequisite =>
            prerequisite.Key == "configuration:kdcsvc-rc4-event-schema" &&
            prerequisite.Kind == EventRequirementKind.Configuration);
        Assert.Contains(requirement.Prerequisites, prerequisite =>
            prerequisite.Key == "configuration:kdcsvc-rc4-event-schema" &&
            prerequisite.DocumentationUri ==
            "https://support.microsoft.com/en-us/topic/how-to-manage-kerberos-kdc-usage-of-rc4-for-service-account-ticket-issuance-changes-related-to-cve-2026-20833-1ebcda33-720a-4da8-93c1-b0496e1910dc");
        Assert.DoesNotContain(requirement.Prerequisites, prerequisite => prerequisite.Kind == EventRequirementKind.AuditPolicy);
        EventMonitoringPresetDefinition preset = EventMonitoringPresetCatalog.Get(EventMonitoringPreset.AuthenticationHealth);
        Assert.Contains(EventType.KerberosKdcRc4Audit, EventTypeCatalog.Expand(preset.Types));
        Assert.Contains(
            EventDetectionCatalog.GetBuiltInRules(),
            rule => rule.Definition.RuleId == "EVX-AUTH-0010" &&
                    rule.Definition.EventTypes.Contains(EventType.KerberosKdcRc4Audit));
    }

    private static EventObject BuildEventObject(
        int id,
        string log,
        string provider,
        Dictionary<string, string> data,
        string message,
        IReadOnlyList<object?>? properties = null,
        byte? version = 0)
    {
        var metadata = new NativeEventMetadata(
            provider,
            providerId: null,
            id,
            qualifiers: null,
            level: 3,
            task: null,
            opcode: 0,
            keywords: null,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            recordId: 1,
            activityId: null,
            relatedActivityId: null,
            processId: 0,
            threadId: 0,
            log,
            "DC01.ad.evotec.xyz",
            userId: null,
            version);
        var nativeMessage = new NativeEventMessage(
            metadata,
            message,
            "Warning",
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            bookmark: null,
            "de-DE",
            string.IsNullOrEmpty(message)
                ? EventMessageRenderStatus.MessageResourceUnavailable
                : EventMessageRenderStatus.Rendered,
            renderErrorCode: 0);
        var nativeStructured = new NativeEventStructured(
            metadata,
            "<Event />",
            properties?.Select(static value => new EventPropertyValue(value)).ToArray() ??
            Array.Empty<EventPropertyValue>(),
            bookmark: null);
        var source = new EventObject(
            new NativeEventFull(nativeMessage, nativeStructured),
            "testhost",
            log);
        SetProperty(source, nameof(EventObject.Data), data);
        SetProperty(source, nameof(EventObject.Attachments), Array.Empty<byte[]>());
        SetProperty(source, nameof(EventObject.NicIdentifiers), new List<string>());
        return source;
    }

    private static EventProviderMetadataSnapshot CreateKdcProvider(
        IReadOnlyList<long> eventIds,
        params string[] diagnostics)
    {
        return CreateKdcProvider(eventIds, version: 0, diagnostics);
    }

    private static EventProviderMetadataSnapshot CreateKdcProvider(
        IReadOnlyList<long> eventIds,
        byte version,
        params string[] diagnostics)
    {
        EventProviderEventMetadataSnapshot[] events = eventIds.Select(id =>
            new EventProviderEventMetadataSnapshot(
                id,
                version,
                logName: "System",
                channelId: null,
                level: null,
                opcode: null,
                task: null,
                keywords: Array.Empty<long>(),
                template: string.Empty,
                description: string.Empty)).ToArray();
        return new EventProviderMetadataSnapshot(
            "Kdcsvc",
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            helpLink: null,
            Array.Empty<EventProviderLogLink>(),
            Array.Empty<EventProviderValue>(),
            Array.Empty<EventProviderValue>(),
            Array.Empty<EventProviderValue>(),
            Array.Empty<EventProviderValue>(),
            events,
            diagnostics);
    }

    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo? property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        property!.SetValue(target, value);
    }

}
