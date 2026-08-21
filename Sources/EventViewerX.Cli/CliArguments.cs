using System.Globalization;

namespace EventViewerX.Cli;

internal sealed class CliArguments {
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.OrdinalIgnoreCase);
    internal CliArguments(string[] args) {
        Command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "help";
        Subcommand = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1].Trim().ToLowerInvariant() : string.Empty;
        int start = Subcommand.Length == 0 ? 1 : 2;
        for (int index = start; index < args.Length; index++) {
            string token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal)) {
                throw new ArgumentException($"Unexpected argument '{token}'. Options must use --name value.");
            }
            string name = token.Substring(2);
            string value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : "true";
            if (!_options.TryGetValue(name, out List<string>? values)) {
                values = new List<string>();
                _options[name] = values;
            }
            values.Add(value);
        }
    }
    internal string Command { get; }
    internal string Subcommand { get; }
    internal bool Has(string name) => _options.ContainsKey(name);
    internal string? Get(string name) => _options.TryGetValue(name, out List<string>? values) ? values[^1] : null;
    internal string Require(string name) => Get(name) ?? throw new ArgumentException($"--{name} is required.");
    internal string[] GetMany(string name) => _options.TryGetValue(name, out List<string>? values)
        ? values.SelectMany(static value => value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).Select(static value => value.Trim()).ToArray()
        : Array.Empty<string>();
    internal int GetInt(string name, int fallback = 0) => Get(name) is string value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
    internal long GetLong(string name, long fallback = 0) => Get(name) is string value ? long.Parse(value, CultureInfo.InvariantCulture) : fallback;

    internal void ValidateAllowed(params string[] names) {
        var allowed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        string[] unknown = _options.Keys
            .Where(name => !allowed.Contains(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknown.Length > 0) {
            throw new ArgumentException(
                $"Unknown option(s): {string.Join(", ", unknown.Select(static name => $"--{name}"))}.");
        }
    }
}
