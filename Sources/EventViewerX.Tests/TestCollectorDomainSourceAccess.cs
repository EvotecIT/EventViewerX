using Xunit;

namespace EventViewerX.Tests;

public class TestCollectorDomainSourceAccess {
    [Fact]
    public void SourceRulesRetainOrderedSidsDeniesAndNativeGenericRights() {
        CollectorDomainSourceAccess access = CollectorDomainSourceAccess.Inspect(
            "O:NSG:NSD:(D;;GA;;;SY)(A;;GA;;;NS)");

        Assert.Equal(SecurityDescriptorDaclState.Entries, access.DaclState);
        Assert.Null(access.Diagnostic);
        Assert.Collection(access.AccessRules!,
            deny => {
                Assert.Equal("S-1-5-18", deny.TrusteeSid);
                Assert.True(deny.IsDeny);
                Assert.True(deny.IncludesGenericAll);
            },
            allow => {
                Assert.Equal("S-1-5-20", allow.TrusteeSid);
                Assert.True(allow.IsAllow);
                Assert.True(allow.IncludesGenericAll);
            });
    }

    [Theory]
    [InlineData("O:SYG:SY", SecurityDescriptorDaclState.NotPresent)]
    [InlineData("O:SYG:SYD:NO_ACCESS_CONTROL", SecurityDescriptorDaclState.Null)]
    [InlineData("O:SYG:SYD:", SecurityDescriptorDaclState.Empty)]
    public void SourceAuthorizationDistinguishesDaclStates(string sddl, SecurityDescriptorDaclState state) {
        CollectorDomainSourceAccess access = CollectorDomainSourceAccess.Inspect(sddl);

        Assert.Equal(state, access.DaclState);
        Assert.Empty(access.AccessRules!);
    }

    [Fact]
    public void MissingOrMalformedSddlCannotBeReportedAsEmptyOrAllowed() {
        foreach (string? sddl in new string?[] { null, "not-sddl" }) {
            CollectorDomainSourceAccess access = CollectorDomainSourceAccess.Inspect(sddl);

            Assert.Equal(SecurityDescriptorDaclState.Unavailable, access.DaclState);
            Assert.Null(access.AccessRules);
            Assert.False(string.IsNullOrWhiteSpace(access.Diagnostic));
        }
    }

    [Fact]
    public void AuthoritativeReadUsesWecutilXmlAndDoesNotDependOnRegistryQueryXml() {
        IReadOnlyList<string>? arguments = null;
        CollectorSourceAuthorization access = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "SourceFeed",
            (actual, _) => {
                arguments = actual;
                return SourceSubscription("O:NSG:NSD:(A;;GA;;;NS)");
            },
            CancellationToken.None);

        Assert.Equal(new[] { "gs", "SourceFeed", "/f:XML" }, arguments);
        Assert.Equal(CollectorSourceAuthorizationStatus.Available, access.Status);
        Assert.Equal(SecurityDescriptorDaclState.Entries, access.DomainComputers!.DaclState);
        Assert.Equal("S-1-5-20", Assert.Single(access.DomainComputers.AccessRules!).TrusteeSid);
    }

    [Fact]
    public void ConfirmedCollectorInitiatedSubscriptionHasNoDomainSourceAcl() {
        CollectorSourceAuthorization access = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "PullFeed",
            (_, _) => "<Subscription><SubscriptionId>PullFeed</SubscriptionId><SubscriptionType>CollectorInitiated</SubscriptionType></Subscription>",
            CancellationToken.None);

        Assert.Equal(CollectorSourceAuthorizationStatus.NotApplicable, access.Status);
        Assert.Null(access.DomainComputers);
    }

    [Fact]
    public void MissingSourceSddlAndReadFailuresRemainDiagnosedUnknowns() {
        CollectorSourceAuthorization missing = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "SourceFeed",
            (_, _) => SourceSubscription(null),
            CancellationToken.None);
        CollectorSourceAuthorization failed = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "SourceFeed",
            (_, _) => throw new InvalidOperationException("Collector service unavailable"),
            CancellationToken.None);

        Assert.Equal(SecurityDescriptorDaclState.Unavailable, missing.DomainComputers!.DaclState);
        Assert.Null(missing.DomainComputers.AccessRules);
        Assert.Contains("did not provide", missing.DomainComputers.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(CollectorSourceAuthorizationStatus.Unavailable, failed.Status);
        Assert.Equal(SecurityDescriptorDaclState.Unavailable, failed.DomainComputers!.DaclState);
        Assert.Contains("Collector service unavailable", failed.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongSubscriptionIdentityCannotSupplyAuthorizationForAnotherFeed() {
        CollectorSourceAuthorization authorization = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "ExpectedFeed",
            (_, _) => SourceSubscription("O:NSG:NSD:(A;;GA;;;NS)"),
            CancellationToken.None);

        Assert.Equal(CollectorSourceAuthorizationStatus.Unavailable, authorization.Status);
        Assert.Equal(SecurityDescriptorDaclState.Unavailable, authorization.DomainComputers!.DaclState);
        Assert.Contains("SourceFeed", authorization.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationIsNotConvertedIntoAnAuthorizationDiagnostic() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
                "SourceFeed",
                (_, _) => throw new Exception("Runner must not be called"),
                cancellation.Token));
    }

    [Fact]
    public void SourceReadRetainsCertificateSubjectPolicySeparateFromDomainDacl() {
        const string xml = """
            <Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
              <SubscriptionId>CertificateFeed</SubscriptionId>
              <SubscriptionType>SourceInitiated</SubscriptionType>
              <AllowedSourceDomainComputers>O:NSG:NSD:(A;;GA;;;NS)</AllowedSourceDomainComputers>
              <AllowedSourceNonDomainComputers>
                <AllowedIssuerCAList>issuer-one</AllowedIssuerCAList>
                <AllowedSubjectList>CN=source.example</AllowedSubjectList>
                <DeniedSubjectList>CN=blocked.example</DeniedSubjectList>
              </AllowedSourceNonDomainComputers>
            </Subscription>
            """;

        CollectorSourceAuthorization authorization = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "CertificateFeed",
            (_, _) => xml,
            CancellationToken.None);

        Assert.Equal(CollectorSourceAuthorizationStatus.Available, authorization.Status);
        Assert.Equal("issuer-one", authorization.AllowedIssuerCAs);
        Assert.Equal("CN=source.example", authorization.AllowedSubjects);
        Assert.Equal("CN=blocked.example", authorization.DeniedSubjects);
        Assert.Contains("AllowedSourceNonDomainComputers", authorization.RawNonDomainSourceXml, StringComparison.Ordinal);
        Assert.Equal(SecurityDescriptorDaclState.Entries, authorization.DomainComputers!.DaclState);
    }

    [Fact]
    public void NestedCertificateRulesArePreservedRatherThanConcatenated() {
        const string xml = """
            <Subscription>
              <SubscriptionId>NestedFeed</SubscriptionId>
              <SubscriptionType>SourceInitiated</SubscriptionType>
              <AllowedSourceNonDomainComputers>
                <AllowedIssuerCAList><IssuerCA>first</IssuerCA><IssuerCA>second</IssuerCA></AllowedIssuerCAList>
              </AllowedSourceNonDomainComputers>
            </Subscription>
            """;

        CollectorSourceAuthorization authorization = CollectorSubscriptionManager.ReadCollectorSourceAuthorization(
            "NestedFeed", (_, _) => xml, CancellationToken.None);

        Assert.Equal(CollectorSourceAuthorizationStatus.Available, authorization.Status);
        Assert.Contains("<IssuerCA>first</IssuerCA><IssuerCA>second</IssuerCA>", authorization.AllowedIssuerCAs, StringComparison.Ordinal);
        Assert.Equal(SecurityDescriptorDaclState.Unavailable, authorization.DomainComputers!.DaclState);
    }

    private static string SourceSubscription(string? sddl) =>
        $"<Subscription><SubscriptionId>SourceFeed</SubscriptionId><SubscriptionType>SourceInitiated</SubscriptionType><AllowedSourceDomainComputers>{sddl}</AllowedSourceDomainComputers></Subscription>";
}
