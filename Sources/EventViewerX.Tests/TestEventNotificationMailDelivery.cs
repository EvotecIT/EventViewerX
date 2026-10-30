#if NET10_0
using EventViewerX.Cli;
using Mailozaurr;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventNotificationMailDelivery {
    [Theory]
    [InlineData(MailProfileKind.Smtp)]
    [InlineData(MailProfileKind.Graph)]
    public async Task SendsRenderedContentThroughSelectedMailozaurrHandler(MailProfileKind kind) {
        var profiles = new InMemoryMailProfileStore();
        await profiles.SaveAsync(CreateProfile(kind));
        var smtp = new FakeSendHandler(MailProfileKind.Smtp);
        var graph = new FakeSendHandler(MailProfileKind.Graph);
        var delivery = new EventNotificationMailDelivery(
            profiles, new RoutedMailSendService(profiles, new IMailSendHandler[] { smtp, graph }));

        SendResult result = await delivery.SendAsync(
            "event-alerts", "<strong>Two events</strong>", "Two events", "Daily events",
            new[] { "operator@example.test" }, dryRun: false);

        Assert.True(result.Succeeded);
        FakeSendHandler selected = kind == MailProfileKind.Smtp ? smtp : graph;
        FakeSendHandler other = kind == MailProfileKind.Smtp ? graph : smtp;
        SendMessageRequest request = Assert.Single(selected.Requests);
        Assert.Empty(other.Requests);
        Assert.Equal("event-alerts", request.ProfileId);
        Assert.Equal("sender@example.test", request.Message.From?.Address);
        Assert.Equal("operator@example.test", Assert.Single(request.Message.To).Address);
        Assert.Equal("Daily events", request.Message.Subject);
        Assert.Equal("<strong>Two events</strong>", request.Message.HtmlBody);
        Assert.Equal("Two events", request.Message.TextBody);
    }

    [Theory]
    [InlineData(MailProfileKind.Smtp)]
    [InlineData(MailProfileKind.Graph)]
    public async Task DryRunDoesNotInvokeProvider(MailProfileKind kind) {
        var profiles = new InMemoryMailProfileStore();
        await profiles.SaveAsync(CreateProfile(kind));
        var handler = new FakeSendHandler(kind);
        var delivery = new EventNotificationMailDelivery(
            profiles, new RoutedMailSendService(profiles, new[] { handler }));

        SendResult result = await delivery.SendAsync(
            "event-alerts", "<strong>Two events</strong>", "Two events", "Daily events",
            new[] { "operator@example.test" }, dryRun: true);

        Assert.False(result.Succeeded);
        Assert.Equal(kind, result.ProfileKind);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RejectsUnacknowledgedSendResult(bool succeeded, bool queued) {
        var profiles = new InMemoryMailProfileStore();
        await profiles.SaveAsync(CreateProfile(MailProfileKind.Smtp));
        var handler = new FakeSendHandler(MailProfileKind.Smtp, succeeded, queued);
        var delivery = new EventNotificationMailDelivery(
            profiles, new RoutedMailSendService(profiles, new[] { handler }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.SendAsync("event-alerts", "<strong>Two events</strong>", "Two events", "Daily events",
                new[] { "operator@example.test" }, dryRun: false));

        Assert.Contains("not acknowledged", exception.Message);
        Assert.Single(handler.Requests);
    }

    private static MailProfile CreateProfile(MailProfileKind kind) => new() {
        Id = "event-alerts",
        DisplayName = "Event alerts",
        Kind = kind,
        DefaultSender = "sender@example.test",
        DefaultMailbox = "sender@example.test",
        Settings = kind == MailProfileKind.Smtp
            ? new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "relay.example.test" }
            : new Dictionary<string, string>()
    };

    private sealed class FakeSendHandler(MailProfileKind kind, bool succeeded = true, bool queued = false) : IMailSendHandler {
        public MailProfileKind Kind => kind;
        public List<SendMessageRequest> Requests { get; } = new();

        public Task<SendResult> SendAsync(
            MailProfile profile,
            SendMessageRequest request,
            CancellationToken cancellationToken = default) {
            Requests.Add(request);
            return Task.FromResult(new SendResult {
                Succeeded = succeeded,
                Queued = queued,
                Message = succeeded && !queued ? null : "synthetic rejection",
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                ProviderMessageId = "synthetic-message"
            });
        }
    }
}
#endif
