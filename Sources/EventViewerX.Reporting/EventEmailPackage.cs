using HtmlForgeX.Email;

namespace EventViewerX.Reporting;

/// <summary>Transport-neutral email payload suitable for Mailozaurr or another mail transport.</summary>
public sealed class EventEmailPackage {
    internal EventEmailPackage(string subject, string plainText, EmailRenderResult result) {
        Subject = subject;
        Html = result.Html;
        PlainText = plainText;
        InlineResources = result.InlineResources;
        Attachments = Array.Empty<object>();
        EstimatedSizeBytes = Encoding.UTF8.GetByteCount(result.Html) + Encoding.UTF8.GetByteCount(plainText);
    }

    /// <summary>Message subject.</summary>
    public string Subject { get; }
    /// <summary>HTML body.</summary>
    public string Html { get; }
    /// <summary>Plain-text alternative.</summary>
    public string PlainText { get; }
    /// <summary>Inline CID resources.</summary>
    public IReadOnlyList<EmailInlineResource> InlineResources { get; }
    /// <summary>Regular attachments.</summary>
    public IReadOnlyList<object> Attachments { get; }
    /// <summary>Estimated MIME payload size.</summary>
    public long EstimatedSizeBytes { get; }
}
