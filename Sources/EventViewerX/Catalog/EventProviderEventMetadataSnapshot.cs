namespace EventViewerX;

/// <summary>Detached event metadata snapshot from a registered provider manifest.</summary>
public sealed class EventProviderEventMetadataSnapshot {
    internal EventProviderEventMetadataSnapshot(
        long id,
        byte version,
        string logName,
        byte? channelId,
        int? level,
        int? opcode,
        int? task,
        IReadOnlyList<long> keywords,
        string template,
        string description) {

        Id = id;
        Version = version;
        LogName = logName;
        ChannelId = channelId;
        Level = level;
        Opcode = opcode;
        Task = task;
        Keywords = keywords;
        Template = template;
        Description = description;
    }

    /// <summary>Event identifier.</summary>
    public long Id { get; }
    /// <summary>Event version.</summary>
    public byte Version { get; }
    /// <summary>Owning channel.</summary>
    public string LogName { get; }
    internal byte? ChannelId { get; }
    /// <summary>Level value.</summary>
    public int? Level { get; }
    /// <summary>Opcode value.</summary>
    public int? Opcode { get; }
    /// <summary>Task value.</summary>
    public int? Task { get; }
    /// <summary>Keyword values.</summary>
    public IReadOnlyList<long> Keywords { get; }
    /// <summary>Provider event template XML.</summary>
    public string Template { get; }
    /// <summary>Localized event description template.</summary>
    public string Description { get; }
}
