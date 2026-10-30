using EventViewerX.Reporting;
using Mailozaurr;

namespace EventViewerX.Cli;

/// <summary>Maps an EventViewerX notification to a Mailozaurr profile send.</summary>
internal sealed class EventNotificationMailDelivery {
    private readonly IMailProfileStore _profiles;
    private readonly IMailSendService _send;

    internal EventNotificationMailDelivery(IMailProfileStore profiles, IMailSendService send) {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _send = send ?? throw new ArgumentNullException(nameof(send));
    }

    internal static EventNotificationMailDelivery Create(string? profileStorePath) {
        MailApplication application = new MailApplicationBuilder(CreateOptions(profileStorePath)).Build();
        return new EventNotificationMailDelivery(application.ProfileStore, application.Send);
    }

    internal static string[] ResolveStorePaths(string? profileStorePath) {
        MailApplicationOptions options = CreateOptions(profileStorePath);
        return new[] { options.ProfileStore.GetFilePath(), options.SecretStore.GetFilePath() };
    }

    private static MailApplicationOptions CreateOptions(string? profileStorePath) {
        var options = new MailApplicationOptions();
        if (!string.IsNullOrWhiteSpace(profileStorePath)) {
            string fullPath = Path.GetFullPath(profileStorePath);
            options.ProfileStore.DirectoryPath = Path.GetDirectoryName(fullPath);
            options.ProfileStore.FileName = Path.GetFileName(fullPath);
        }
        return options;
    }

    internal async Task<SendResult> SendAsync(
        string profileId,
        EventEmailPackage package,
        string[] recipients,
        bool dryRun,
        CancellationToken cancellationToken = default) =>
        await SendAsync(profileId, package.Html, package.PlainText, package.Subject,
            recipients, dryRun, cancellationToken).ConfigureAwait(false);

    internal async Task<SendResult> SendAsync(
        string profileId,
        string html,
        string plainText,
        string subject,
        string[] recipients,
        bool dryRun,
        CancellationToken cancellationToken = default) {

        MailProfile profile = await ResolveProfileAsync(profileId, recipients, cancellationToken).ConfigureAwait(false);
        var request = new SendMessageRequest {
            ProfileId = profile.Id,
            Message = new DraftMessage {
                ProfileId = profile.Id,
                From = new MessageRecipient { Address = profile.DefaultSender! },
                To = recipients.Select(static recipient => new MessageRecipient { Address = recipient.Trim() }).ToList(),
                Subject = subject,
                HtmlBody = html,
                TextBody = plainText
            }
        };
        if (dryRun) {
            return new SendResult {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                Succeeded = false,
                Message = "Dry run: no mail was sent."
            };
        }
        SendResult result = await _send.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Queued) {
            throw new InvalidOperationException(
                $"Mailozaurr {profile.Kind} delivery was not acknowledged: {result.Message}");
        }
        return result;
    }

    internal async Task ValidateAsync(
        string profileId,
        string[] recipients,
        CancellationToken cancellationToken = default) {
        _ = await ResolveProfileAsync(profileId, recipients, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailProfile> ResolveProfileAsync(
        string profileId,
        string[] recipients,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("A Mailozaurr profile id is required.", nameof(profileId));
        }
        if (recipients == null || recipients.Length == 0 ||
            recipients.Any(string.IsNullOrWhiteSpace)) {
            throw new ArgumentException("At least one --mail-to recipient is required.", nameof(recipients));
        }
        MailProfile profile = await _profiles.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Mailozaurr profile '{profileId}' was not found.");
        MailProfileValidationResult validation = MailProfileValidator.Validate(profile);
        if (!validation.Succeeded) {
            throw new InvalidDataException($"Mailozaurr profile '{profileId}' is invalid: {validation.Message}");
        }
        if (!profile.GetCapabilities().Supports(MailCapability.SendMessages)) {
            throw new NotSupportedException($"Mailozaurr profile '{profileId}' does not support sending mail.");
        }
        if (string.IsNullOrWhiteSpace(profile.DefaultSender)) {
            throw new InvalidDataException($"Mailozaurr profile '{profileId}' needs a default sender for EventViewerX notifications.");
        }
        return profile;
    }
}
