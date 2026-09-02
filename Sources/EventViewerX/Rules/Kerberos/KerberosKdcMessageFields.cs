namespace EventViewerX.Rules.Kerberos;

internal sealed class KerberosKdcMessageFields
{
    private readonly Dictionary<string, string> _topLevel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _account = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _service = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _domainController = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _network = new(StringComparer.OrdinalIgnoreCase);

    internal static KerberosKdcMessageFields Parse(string? message)
    {
        var result = new KerberosKdcMessageFields();
        if (string.IsNullOrWhiteSpace(message)) {
            return result;
        }

        Dictionary<string, string> current = result._topLevel;
        string[] lines = message!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (string rawLine in lines) {
            string line = rawLine.Trim();
            if (line.Length == 0) {
                continue;
            }

            if (IsSection(line, "Account Information")) {
                current = result._account;
                continue;
            }
            if (IsSection(line, "Service Information")) {
                current = result._service;
                continue;
            }
            if (IsSection(line, "Domain Controller Information")) {
                current = result._domainController;
                continue;
            }
            if (IsSection(line, "Network Information")) {
                current = result._network;
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0) {
                continue;
            }

            string key = line.Substring(0, colon).Trim();
            string value = line.Substring(colon + 1).Trim();
            if (key.Length > 0) {
                current[key] = value;
            }
        }

        return result;
    }

    internal string TopLevel(params string[] keys) => Read(_topLevel, keys);
    internal string Account(params string[] keys) => Read(_account, keys);
    internal string Service(params string[] keys) => Read(_service, keys);
    internal string DomainController(params string[] keys) => Read(_domainController, keys);
    internal string Network(params string[] keys) => Read(_network, keys);

    private static bool IsSection(string line, string section) =>
        line.TrimEnd(':').Equals(section, StringComparison.OrdinalIgnoreCase);

    private static string Read(IReadOnlyDictionary<string, string> fields, IEnumerable<string> keys)
    {
        foreach (string key in keys) {
            if (fields.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)) {
                return value.Trim();
            }
        }
        return string.Empty;
    }
}
