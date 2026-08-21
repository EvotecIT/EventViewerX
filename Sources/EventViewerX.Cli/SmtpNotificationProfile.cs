using System.Text.Json;
using EventViewerX.Reporting;
using MailKit.Security;
using Mailozaurr;

namespace EventViewerX.Cli;

/// <summary>Serializable SMTP delivery settings for portable EventViewerX notification hosts.</summary>
internal sealed class SmtpNotificationProfile {
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string SecureSocketOptions { get; set; } = nameof(MailKit.Security.SecureSocketOptions.StartTls);
    public bool UseSsl { get; set; }
    public string From { get; set; } = string.Empty;
    public string[] To { get; set; } = Array.Empty<string>();
    public string[] Cc { get; set; } = Array.Empty<string>();
    public string[] Bcc { get; set; } = Array.Empty<string>();
    public string? UserName { get; set; }
    public string? PasswordEnvironmentVariable { get; set; }
    public string Subject { get; set; } = "{Title}";
    public int TimeoutMilliseconds { get; set; } = 100000;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 500;
    public bool DryRun { get; set; }

    public static SmtpNotificationProfile Load(string path) {
        string fullPath = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        SmtpNotificationProfile? profile = JsonSerializer.Deserialize<SmtpNotificationProfile>(
            File.ReadAllText(fullPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (profile == null) {
            throw new InvalidDataException($"SMTP profile '{fullPath}' is empty.");
        }
        profile.Validate();
        return profile;
    }

    public void Validate() {
        if (string.IsNullOrWhiteSpace(Server)) {
            throw new InvalidDataException("SMTP profile Server is required.");
        }
        if (Port is < 1 or > 65535) {
            throw new InvalidDataException("SMTP profile Port must be between 1 and 65535.");
        }
        if (string.IsNullOrWhiteSpace(From)) {
            throw new InvalidDataException("SMTP profile From is required.");
        }
        To = NormalizeAddresses(To);
        Cc = NormalizeAddresses(Cc);
        Bcc = NormalizeAddresses(Bcc);
        if (To.Length + Cc.Length + Bcc.Length == 0) {
            throw new InvalidDataException("SMTP profile requires at least one To, Cc, or Bcc recipient.");
        }
        if (!Enum.TryParse(SecureSocketOptions, true, out MailKit.Security.SecureSocketOptions parsed) ||
            !Enum.IsDefined(parsed)) {
            throw new InvalidDataException("SMTP profile SecureSocketOptions must be None, Auto, SslOnConnect, StartTls, or StartTlsWhenAvailable.");
        }
        SecureSocketOptions = parsed.ToString();
        UserName = Normalize(UserName);
        PasswordEnvironmentVariable = Normalize(PasswordEnvironmentVariable);
        if ((UserName == null) != (PasswordEnvironmentVariable == null)) {
            throw new InvalidDataException("SMTP profile UserName and PasswordEnvironmentVariable must be supplied together.");
        }
        if (TimeoutMilliseconds <= 0) {
            throw new InvalidDataException("SMTP profile TimeoutMilliseconds must be positive.");
        }
        if (RetryCount < 0 || RetryDelayMilliseconds < 0) {
            throw new InvalidDataException("SMTP profile retry values cannot be negative.");
        }
    }

    public async Task<SmtpResult> SendAsync(
        EventEmailPackage package,
        string title,
        CancellationToken cancellationToken = default) {

        ArgumentNullException.ThrowIfNull(package);
        Validate();
        var smtp = new Smtp {
            From = From.Trim(),
            To = To.Cast<object>().ToArray(),
            Cc = Cc.Cast<object>().ToArray(),
            Bcc = Bcc.Cast<object>().ToArray(),
            Subject = (string.IsNullOrWhiteSpace(Subject) ? "{Title}" : Subject).Replace("{Title}", title, StringComparison.Ordinal),
            HtmlBody = package.Html,
            TextBody = package.PlainText,
            AutoCreateMessage = true,
            Timeout = TimeoutMilliseconds,
            RetryCount = RetryCount,
            RetryDelayMilliseconds = RetryDelayMilliseconds,
            DryRun = DryRun
        };
        try {
            MailKit.Security.SecureSocketOptions socketOptions = Enum.Parse<MailKit.Security.SecureSocketOptions>(SecureSocketOptions, true);
            if (UserName != null) {
                string? secret = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable!);
                if (string.IsNullOrEmpty(secret)) {
                    throw new InvalidOperationException(
                        $"SMTP secret environment variable '{PasswordEnvironmentVariable}' is not set.");
                }
                SmtpConnectAuthenticateResult connected = await smtp.ConnectAndAuthenticateAsync(
                    Server.Trim(), Port, UserName, secret, socketOptions, UseSsl,
                    ProtocolAuthMode.Basic, cancellationToken).ConfigureAwait(false);
                if (!connected.IsSuccess) {
                    throw new InvalidOperationException($"SMTP connect/authentication failed: {connected.Error}");
                }
            } else {
                SmtpResult connected = await smtp.ConnectAsync(
                    Server.Trim(), Port, socketOptions, UseSsl, cancellationToken).ConfigureAwait(false);
                if (!connected.Status) {
                    throw new InvalidOperationException($"SMTP connection failed: {connected.Error}");
                }
            }
            SmtpResult result = await smtp.SendAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Status && !DryRun) {
                throw new InvalidOperationException($"SMTP delivery failed: {result.Error}");
            }
            return result;
        } finally {
            smtp.Disconnect();
            smtp.Dispose();
        }
    }

    private static string[] NormalizeAddresses(IEnumerable<string>? values) => (values ?? Array.Empty<string>())
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
