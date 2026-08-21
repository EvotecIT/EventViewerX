namespace EventViewerX;

/// <summary>
/// Represents a reconstructed PowerShell script from event logs.
/// </summary>
public class RestoredPowerShellScript {
    /// <summary>
    /// Identifier of the script block.
    /// </summary>
    public string ScriptBlockId { get; set; } = string.Empty;

    /// <summary>
    /// Full script text reconstructed from events.
    /// </summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether every numbered fragment declared by the event stream was available.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Number of script fragments declared by the event stream.
    /// </summary>
    public int ExpectedPartCount { get; set; }

    /// <summary>
    /// Number of distinct script fragments that were available for reconstruction.
    /// </summary>
    public int AvailablePartCount { get; set; }

    /// <summary>
    /// Managed event snapshots that compose the script.
    /// </summary>
    public IReadOnlyList<EventObject> Events { get; set; } = Array.Empty<EventObject>();

    /// <summary>
    /// Primary event snapshot for convenience access.
    /// </summary>
    public EventObject? Event => Events.Count > 0 ? Events[0] : null;

    /// <summary>
    /// Parsed data dictionary from the event.
    /// </summary>
    public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();

    /// <summary>
    /// Saves the script to the specified directory.
    /// </summary>
    public string Save(string directory, bool addComment = true, bool unblock = false) {
        EventObject primary = Event ?? throw new InvalidOperationException("No event data available to save script.");
        string destination = Path.GetFullPath(directory);
        Directory.CreateDirectory(destination);
        string machineName = SanitizeFileNameComponent(primary.MachineName, "machine");
        string scriptBlockId = SanitizeFileNameComponent(ScriptBlockId, "script");
        string identity = primary.MachineName + "\0" + ScriptBlockId;
        string suffix;
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create()) {
            byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity));
            suffix = BitConverter.ToString(hash, 0, 6).Replace("-", string.Empty);
        }
        string fileName = $"{machineName}_{scriptBlockId}_{suffix}.ps1";
        string filePath = Path.GetFullPath(Path.Combine(destination, fileName));
        string destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("The reconstructed script filename resolved outside the requested destination.");
        }
        string contents;
        if (addComment) {
            string header = string.Join(Environment.NewLine,
                "<#",
                $"RecordID = {primary.RecordId}",
                $"LogName = {primary.LogName}",
                $"MachineName = {primary.MachineName}",
                $"TimeCreated = {primary.TimeCreated}",
                "#>");
            contents = header + Environment.NewLine + Script;
        } else {
            contents = Script;
        }

        bool created = false;
        try {
            WriteNewFile(filePath, contents);
            created = true;
            if (!unblock) {
                WriteNewFile(filePath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3");
            }
        } catch {
            if (created) {
                DeleteOwnedFile(filePath);
            }
            throw;
        }
        return filePath;
    }

    internal static void WriteNewFile(
        string path,
        string contents,
        Action<StreamWriter, string>? write = null) {

        bool created = false;
        try {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            created = true;
            using var writer = new StreamWriter(
                stream,
                new System.Text.UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            if (write == null) {
                writer.Write(contents);
            } else {
                write(writer, contents);
            }
        } catch {
            if (created) {
                DeleteOwnedFile(path);
            }
            throw;
        }
    }

    private static void DeleteOwnedFile(
        string path) {

        try {
            File.Delete(path);
        } catch (IOException) {
            // Preserve the authoritative write or metadata failure.
        } catch (UnauthorizedAccessException) {
            // Preserve the authoritative write or metadata failure.
        }
    }

    private static string SanitizeFileNameComponent(string? value, string fallback) {
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
        char[] invalid = Path.GetInvalidFileNameChars()
            .Concat(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':', '\0' })
            .Distinct()
            .ToArray();
        foreach (char character in invalid) {
            candidate = candidate.Replace(character, '_');
        }
        while (candidate.IndexOf("..", StringComparison.Ordinal) >= 0) {
            candidate = candidate.Replace("..", "_");
        }
        candidate = candidate.Trim(' ', '.');
        if (candidate.Length == 0) {
            candidate = fallback;
        }
        return candidate.Length <= 80 ? candidate : candidate.Substring(0, 80);
    }
}
