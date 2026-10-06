using System.Globalization;
using System.Text.Json;

namespace EventViewerX;

// Shared durable evidence representation for investigations and correlation checkpoints.
// Runtime type names from a document are never used to load assemblies.
internal sealed class EventObservationSnapshot {
    public string Identity { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Collector { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public EventLogQuerySourceKind SourceKind { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public DateTime ProcessedUtc { get; set; }
    public SavedEventRecord Source { get; set; } = new();
    public Dictionary<string, EventAnalysisValue> Fields { get; set; } = new();

    internal static EventObservationSnapshot Capture(EventObservation observation) {
        EventObject source = observation.SourceEvent;
        return new EventObservationSnapshot {
            Identity = observation.Identity, TypeName = observation.TypeName,
            Collector = observation.CollectorComputer, Container = observation.ContainerLog,
            SourceKind = source.QuerySourceKind, ReceivedUtc = observation.ReceivedTimeUtc, ProcessedUtc = observation.ProcessedTimeUtc,
            Source = new SavedEventRecord {
                ProviderName = source.ProviderName, ProviderId = source.ProviderId, EventId = source.Id,
                Qualifiers = ushort.TryParse(source.Qualifiers, NumberStyles.None, CultureInfo.InvariantCulture, out ushort qualifiers) ? qualifiers : null,
                RecordId = source.RecordId, Channel = source.OriginalLogName, Computer = source.SourceComputer,
                TimeCreatedUtc = observation.EventTimeUtc, Level = source.Level, Version = source.Version,
                Task = source.Task, Opcode = source.Opcode, Keywords = source.Keywords, ProcessId = source.ProcessId,
                ThreadId = source.ThreadId, ActivityId = source.ActivityId, RelatedActivityId = source.RelatedActivityId,
                UserId = source.UserIdText, RawXml = source.XMLData, Data = new Dictionary<string, string>(source.Data),
                Message = source.Message, MessageCulture = source.MessageCulture,
                MessageRenderStatus = source.MessageRenderStatus, MessageRenderErrorCode = source.MessageRenderErrorCode
            },
            Fields = observation.Fields.ToDictionary(item => item.Key, item => EventAnalysisValue.Capture(item.Value), StringComparer.OrdinalIgnoreCase)
        };
    }

    internal EventObservation Restore() {
        if (Source == null || Fields == null || ReceivedUtc.Kind != DateTimeKind.Utc || ProcessedUtc.Kind != DateTimeKind.Utc ||
            Source.TimeCreatedUtc.Kind != DateTimeKind.Utc || !Enum.IsDefined(typeof(EventLogQuerySourceKind), SourceKind)) {
            throw new InvalidDataException("Invalid durable observation metadata.");
        }
        EventObject source = Source.ToEventObject(Container, EventReadMode.Full);
        source.QueriedMachine = Collector;
        source.QuerySourceKind = SourceKind;
        return EventObservation.Restore(source, Identity, TypeName,
            Fields.ToDictionary(item => item.Key, item => item.Value.Restore(), StringComparer.OrdinalIgnoreCase), ReceivedUtc, ProcessedUtc);
    }
}

internal sealed class EventAnalysisValue {
    private static readonly Dictionary<string, Type> Scalars = new[] { typeof(string), typeof(bool), typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
        typeof(decimal), typeof(char), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid), typeof(byte[]) }
        .ToDictionary(type => type.FullName!, type => type, StringComparer.Ordinal);
    public string Type { get; set; } = "null";
    public JsonElement Value { get; set; }
    public EventAnalysisValue[]? Items { get; set; }

    internal static EventAnalysisValue Capture(object? value) {
        if (value == null) { return new EventAnalysisValue { Value = JsonSerializer.SerializeToElement<object?>(null) }; }
        Type type = value.GetType();
        string name = type.FullName!;
        if (Scalars.ContainsKey(name) || (type.IsEnum && type.Assembly == typeof(EventObject).Assembly)) {
            return new EventAnalysisValue { Type = name, Value = JsonSerializer.SerializeToElement(value, type) };
        }
        if (value is System.Net.IPAddress address) {
            return new EventAnalysisValue { Type = "IPAddress", Value = JsonSerializer.SerializeToElement(address.ToString()) };
        }
        if (value is System.Collections.IEnumerable values && value is not System.Collections.IDictionary) {
            return new EventAnalysisValue { Type = "array", Value = JsonSerializer.SerializeToElement<object?>(null),
                Items = values.Cast<object?>().Select(Capture).ToArray() };
        }
        throw new NotSupportedException($"Durable observation field type '{name}' is not supported.");
    }

    internal object? Restore() {
        if (Type == "null") { return null; }
        if (Type == "array") { return (Items ?? throw new InvalidDataException("Missing array values.")).Select(item => item.Restore()).ToArray(); }
        if (Type == "IPAddress") { return System.Net.IPAddress.Parse(Value.GetString()!); }
        if (!Scalars.TryGetValue(Type, out Type? type)) {
            type = typeof(EventObject).Assembly.GetType(Type, throwOnError: false);
            if (type == null || !type.IsEnum) { throw new InvalidDataException($"Unsupported durable field type '{Type}'."); }
        }
        return JsonSerializer.Deserialize(Value.GetRawText(), type);
    }
}
