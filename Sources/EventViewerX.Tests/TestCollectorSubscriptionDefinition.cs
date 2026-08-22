using EventViewerX;
using Xunit;

namespace EventViewerX.Tests;

public class TestCollectorSubscriptionDefinition {
    [Fact]
    public void CollectorReadinessPropagatesCallerCancellation() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            CollectorSubscriptionManager.GetCollectorReadiness(cancellation.Token));
    }

    [Fact]
    public void RemoveIsIdempotentWhenSubscriptionIsAlreadyAbsent() {
        bool runnerCalled = false;

        CollectorSubscriptionRemovalResult result =
            CollectorSubscriptionManager
                .RemoveCollectorSubscription(
                    "Missing",
                    _ => null,
                    (_, _) => {
                        runnerCalled = true;
                        return string.Empty;
                    },
                    1,
                    TimeSpan.Zero,
                    CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Changed);
        Assert.Null(result.Before);
        Assert.Null(result.After);
        Assert.False(runnerCalled);
    }

    [Fact]
    public void RemoveDeletesAndVerifiesExistingSubscription() {
        CollectorSubscriptionSnapshot snapshot =
            CreateSnapshot("Existing");
        bool exists = true;
        int deleteCount = 0;

        CollectorSubscriptionRemovalResult result =
            CollectorSubscriptionManager
                .RemoveCollectorSubscription(
                    snapshot.SubscriptionName,
                    _ => exists ? snapshot : null,
                    (arguments, _) => {
                        Assert.Equal("ds", arguments[0]);
                        deleteCount++;
                        exists = false;
                        return string.Empty;
                    },
                    1,
                    TimeSpan.Zero,
                    CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Same(snapshot, result.Before);
        Assert.Null(result.After);
        Assert.Equal(1, deleteCount);
    }

    [Fact]
    public void RemoveFailsWhenPersistedStateRemainsPresent() {
        CollectorSubscriptionSnapshot snapshot =
            CreateSnapshot("StillPresent");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager
                    .RemoveCollectorSubscription(
                        snapshot.SubscriptionName,
                        _ => snapshot,
                        (_, _) => string.Empty,
                        2,
                        TimeSpan.Zero,
                        CancellationToken.None));

        Assert.Contains(
            "still present",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProducesCollectorInitiatedSubscriptionWithTypedQuery() {
        var definition = new CollectorSubscriptionDefinition {
            SubscriptionId = "Security failures",
            QueryXml = "<QueryList><Query Id=\"0\" Path=\"Security\"><Select Path=\"Security\">*[System[EventID=4625]]</Select></Query></QueryList>",
            Sources = new[] {
                new CollectorSubscriptionSource("dc01.contoso.com")
            }
        };

        string xml = definition.ToXml();

        Assert.Contains("<SubscriptionType>CollectorInitiated</SubscriptionType>", xml, StringComparison.Ordinal);
        Assert.Contains("<Address>dc01.contoso.com</Address>", xml, StringComparison.Ordinal);
        Assert.Contains("<![CDATA[<QueryList>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Locale", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProducesSourceInitiatedSubscriptionWithoutExplicitSources() {
        var definition = new CollectorSubscriptionDefinition {
            SubscriptionId = "Domain controller audit",
            SubscriptionType = CollectorSubscriptionType.SourceInitiated,
            DeliveryMode = CollectorSubscriptionDeliveryMode.Push,
            CollectorHostName = "wec01.ad.evotec.xyz",
            QueryXml = "<QueryList><Query Id=\"0\" Path=\"Security\"><Select Path=\"Security\">*[System[EventID=5136]]</Select></Query></QueryList>",
            AllowedSourceDomainComputersSddl = "O:NSG:NSD:(A;;GA;;;S-1-5-21-1-2-3-516)"
        };

        string xml = definition.ToXml();

        Assert.Contains("<SubscriptionType>SourceInitiated</SubscriptionType>", xml, StringComparison.Ordinal);
        Assert.Contains("<Delivery Mode=\"Push\">", xml, StringComparison.Ordinal);
        Assert.Contains("<AllowedSourceDomainComputers>O:NSG:NSD:(A;;GA;;;S-1-5-21-1-2-3-516)</AllowedSourceDomainComputers>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<EventSources>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<CredentialsType>", xml, StringComparison.Ordinal);
        Assert.Equal(
            "Server=http://wec01.ad.evotec.xyz:5985/wsman/SubscriptionManager/WEC,Refresh=60",
            definition.SourceSubscriptionManagerValue);
    }

    [Fact]
    public void SourcePolicyBuildsAuthorizationFromDomainControllersSid() {
        string sddl = CollectorSourcePolicy.BuildAllowedSourceSddl(new[] {
            "S-1-5-21-853615985-2870445339-3163598659-516"
        });

        Assert.Equal(
            "O:NSG:NSD:(A;;GA;;;S-1-5-21-853615985-2870445339-3163598659-516)(A;;GA;;;NS)",
            sddl);
        Assert.Equal(
            "Server=https://wec01.ad.evotec.xyz:5986/wsman/SubscriptionManager/WEC,Refresh=120",
            CollectorSourcePolicy.BuildSubscriptionManagerValue(
                "wec01.ad.evotec.xyz",
                "HTTPS",
                refreshIntervalSeconds: 120));
    }

    [Fact]
    public void CollectorInitiatedPushRequiresAndEmitsCollectorHostName() {
        CollectorSubscriptionDefinition definition = CreateDefinition("Push");
        definition.DeliveryMode = CollectorSubscriptionDeliveryMode.Push;
        Assert.Throws<ArgumentException>(definition.Validate);

        definition.CollectorHostName = "wec01.ad.evotec.xyz";
        string xml = definition.ToXml();

        Assert.Contains("<HostName>wec01.ad.evotec.xyz</HostName>", xml, StringComparison.Ordinal);
        Assert.True(xml.IndexOf("<HostName>", StringComparison.Ordinal) <
                    xml.IndexOf("<Heartbeat", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeStatusParserRetainsOverallAndPerSourceEvidence() {
        const string output = """
            Subscription: Domain audit
                RunTimeStatus: Active
                Events processed: 157
                LastError: 0
                EventSources:
                    AD0.ad.evotec.xyz
                        RunTimeStatus: Active
                        Events processed: 144
                        LastError: 0
                        LastHeartbeatTime: 2026-08-18T22:16:58.626
                    AD1.ad.evotec.xyz
                        RunTimeStatus: Trying
                        LastError: 80338012
                        ErrorMessage: The client cannot connect.
            """;

        CollectorSubscriptionRuntimeStatus status =
            CollectorSubscriptionManager.ParseRuntimeStatus(output, "fallback");

        Assert.Equal("Domain audit", status.SubscriptionName);
        Assert.Equal("Active", status.Status);
        Assert.Equal(157, status.EventsProcessed);
        Assert.Equal((uint)0, status.LastErrorCode);
        Assert.Equal(2, status.Sources.Count);
        Assert.True(status.Sources[0].IsHealthy);
        Assert.Equal((uint)0x80338012, status.Sources[1].LastErrorCode);
        Assert.Equal("The client cannot connect.", status.Sources[1].ErrorMessage);
        Assert.False(status.IsHealthy);
        Assert.Equal(output, status.RawStatus);
    }

    [Fact]
    public void NormalizationReadsSelectsEmbeddedInQueryCData() {
        const string xml = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <Description>Security feed</Description>
              <Query><![CDATA[
                <QueryList>
                  <Query Id="0" Path="Security">
                    <Select Path="Security">*[System[EventID=4625]]</Select>
                  </Query>
                </QueryList>
              ]]></Query>
            </Subscription>
            """;

        Assert.True(CollectorSubscriptionXml.TryNormalize(
            xml,
            out CollectorSubscriptionXmlDetails? details,
            out string? error), error);
        Assert.Equal("Security feed", details!.Description);
        Assert.Equal(new[] { "*[System[EventID=4625]]" }, details.Queries);
    }

    [Fact]
    public void ComparisonIgnoresWindowsDefaultLocaleForRawEvents() {
        const string requested = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>Test</SubscriptionId>
              <Query><![CDATA[<QueryList><Query Id="0" Path="System"><Select Path="System">*</Select></Query></QueryList>]]></Query>
              <ContentFormat>Events</ContentFormat>
              <EventSources><EventSource Enabled="true"><Address>server01</Address></EventSource></EventSources>
            </Subscription>
            """;
        const string persisted = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>Test</SubscriptionId>
              <Query><![CDATA[
                <QueryList>
                  <Query Id="0" Path="System"><Select Path="System">*</Select></Query>
                </QueryList>
              ]]></Query>
              <ContentFormat>Events</ContentFormat>
              <Locale Language="en-US" />
              <EventSources><EventSource Enabled="true"><Address>server01</Address></EventSource></EventSources>
            </Subscription>
            """;

        Assert.True(CollectorSubscriptionXml.AreEquivalent(requested, persisted));
    }

    [Fact]
    public void ComparisonIgnoresWindowsEmptyNonDomainSourceDefault() {
        const string requested = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>Test</SubscriptionId>
              <SubscriptionType>SourceInitiated</SubscriptionType>
              <ContentFormat>Events</ContentFormat>
              <AllowedSourceDomainComputers>O:NSG:NSD:(A;;GA;;;S-1-5-21-1-2-3-516)</AllowedSourceDomainComputers>
            </Subscription>
            """;
        const string persisted = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>Test</SubscriptionId>
              <SubscriptionType>SourceInitiated</SubscriptionType>
              <ContentFormat>Events</ContentFormat>
              <Locale Language="pl-PL" />
              <AllowedSourceNonDomainComputers></AllowedSourceNonDomainComputers>
              <AllowedSourceDomainComputers>O:NSG:NSD:(A;;GA;;;S-1-5-21-1-2-3-516)</AllowedSourceDomainComputers>
            </Subscription>
            """;

        Assert.True(CollectorSubscriptionXml.AreEquivalent(requested, persisted));
    }

    [Fact]
    public void ApplyReportsUnknownStateWhenCreateRollbackFails() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("CreateRollbackFailure");
        bool exists = false;
        string mismatched = CreatePersistedXml(
            definition.SubscriptionId,
            "Different description");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => exists
                        ? CreateSnapshot(definition.SubscriptionId)
                        : null,
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "cs":
                                exists = true;
                                return string.Empty;
                            case "gs":
                                return mismatched;
                            case "ds":
                                throw new InvalidOperationException(
                                    "Injected delete rollback failure.");
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "persisted state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        AggregateException aggregate =
            Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.True(exists);
    }

    [Fact]
    public void ApplyVerifiesSuccessfulCreateRollbackBeforeRethrowingOriginalFailure() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("CreateRollbackSuccess");
        bool exists = false;
        string mismatched = CreatePersistedXml(
            definition.SubscriptionId,
            "Different description");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => exists
                        ? CreateSnapshot(definition.SubscriptionId)
                        : null,
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "cs":
                                exists = true;
                                return string.Empty;
                            case "gs":
                                return mismatched;
                            case "ds":
                                exists = false;
                                return string.Empty;
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "did not retain the requested definition",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(exists);
    }

    [Fact]
    public void ApplyDoesNotDeleteUnownedSubscriptionWhenCreateCommandFails() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("CreateOwnershipUnknown");
        bool exists = false;
        bool deleteCalled = false;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => exists
                        ? CreateSnapshot(definition.SubscriptionId)
                        : null,
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "cs":
                                exists = true;
                                throw new InvalidOperationException(
                                    "Injected create failure after another actor won the name.");
                            case "ds":
                                deleteCalled = true;
                                exists = false;
                                return string.Empty;
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "persisted state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        Assert.IsType<AggregateException>(exception.InnerException);
        Assert.False(deleteCalled);
        Assert.True(exists);
    }

    [Fact]
    public void ApplyDeletesProvenOwnedCreateWithoutSnapshotVisibility() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("CreateOwnedButNotVisible");
        string mismatched = CreatePersistedXml(
            definition.SubscriptionId,
            "Different description");
        int deleteCount = 0;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => null,
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "cs":
                                return string.Empty;
                            case "gs":
                                return mismatched;
                            case "ds":
                                deleteCount++;
                                return string.Empty;
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "did not retain the requested definition",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(1, deleteCount);
    }

    [Fact]
    public void ApplyReportsUnknownStateWhenUpdateRollbackFails() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("UpdateRollbackFailure");
        string previous = CreatePersistedXml(
            definition.SubscriptionId,
            "Previous description");
        string mismatched = CreatePersistedXml(
            definition.SubscriptionId,
            "Different description");
        int getCount = 0;
        int setCount = 0;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => CreateSnapshot(definition.SubscriptionId),
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "gs":
                                getCount++;
                                return getCount == 1
                                    ? previous
                                    : mismatched;
                            case "ss":
                                setCount++;
                                if (setCount == 2) {
                                    throw new InvalidOperationException(
                                        "Injected update rollback failure.");
                                }
                                return string.Empty;
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "persisted state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(2, setCount);
        Assert.IsType<AggregateException>(exception.InnerException);
    }

    [Fact]
    public void ApplyVerifiesSuccessfulUpdateRollbackBeforeRethrowingOriginalFailure() {
        CollectorSubscriptionDefinition definition =
            CreateDefinition("UpdateRollbackSuccess");
        string previous = CreatePersistedXml(
            definition.SubscriptionId,
            "Previous description");
        string mismatched = CreatePersistedXml(
            definition.SubscriptionId,
            "Different description");
        int getCount = 0;
        int setCount = 0;

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                CollectorSubscriptionManager.ApplyCollectorSubscription(
                    definition,
                    _ => CreateSnapshot(definition.SubscriptionId),
                    (arguments, _) => {
                        switch (arguments[0]) {
                            case "gs":
                                getCount++;
                                return getCount == 1 || getCount == 3
                                    ? previous
                                    : mismatched;
                            case "ss":
                                setCount++;
                                return string.Empty;
                            default:
                                throw new InvalidOperationException(
                                    "Unexpected WEC operation.");
                        }
                    },
                    CancellationToken.None));

        Assert.Contains(
            "did not retain the requested definition",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "state is unknown",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(2, setCount);
        Assert.Equal(3, getCount);
    }

    private static CollectorSubscriptionDefinition CreateDefinition(
        string subscriptionId) {

        return new CollectorSubscriptionDefinition {
            SubscriptionId = subscriptionId,
            Description = "Requested description",
            QueryXml =
                "<QueryList><Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*</Select></Query></QueryList>",
            Sources = new[] {
                new CollectorSubscriptionSource("server01")
            }
        };
    }

    private static CollectorSubscriptionSnapshot CreateSnapshot(
        string subscriptionId) {

        return new CollectorSubscriptionSnapshot {
            SubscriptionName = subscriptionId
        };
    }

    private static string CreatePersistedXml(
        string subscriptionId,
        string description) {

        return $"""
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>{subscriptionId}</SubscriptionId>
              <SubscriptionType>CollectorInitiated</SubscriptionType>
              <Description>{description}</Description>
              <Enabled>false</Enabled>
              <Uri>http://schemas.microsoft.com/wbem/wsman/1/windows/EventLog</Uri>
              <ConfigurationMode>Normal</ConfigurationMode>
              <Delivery Mode="Pull"><Batching><MaxItems>5</MaxItems><MaxLatencyTime>15000</MaxLatencyTime></Batching><PushSettings><Heartbeat Interval="900000" /></PushSettings></Delivery>
              <Query><![CDATA[<QueryList><Query Id="0" Path="System"><Select Path="System">*</Select></Query></QueryList>]]></Query>
              <ReadExistingEvents>false</ReadExistingEvents>
              <TransportName>HTTP</TransportName>
              <ContentFormat>Events</ContentFormat>
              <LogFile>ForwardedEvents</LogFile>
              <PublisherName>Microsoft-Windows-EventCollector</PublisherName>
              <EventSources><EventSource Enabled="true"><Address>server01</Address></EventSource></EventSources>
            </Subscription>
            """;
    }
}
