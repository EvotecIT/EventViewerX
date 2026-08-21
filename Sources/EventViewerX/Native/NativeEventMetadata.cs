using System;
using System.Security.Principal;

namespace EventViewerX.Native;

internal readonly struct NativeEventMetadata {
    internal NativeEventMetadata(
        string providerName,
        Guid? providerId,
        int id,
        ushort? qualifiers,
        byte? level,
        int? task,
        short? opcode,
        long? keywords,
        DateTime timeCreated,
        long? recordId,
        Guid? activityId,
        Guid? relatedActivityId,
        int? processId,
        int? threadId,
        string logName,
        string machineName,
        SecurityIdentifier? userId,
        byte? version) {

        ProviderName = providerName;
        ProviderId = providerId;
        Id = id;
        Qualifiers = qualifiers;
        Level = level;
        Task = task;
        Opcode = opcode;
        Keywords = keywords;
        TimeCreated = timeCreated;
        RecordId = recordId;
        ActivityId = activityId;
        RelatedActivityId = relatedActivityId;
        ProcessId = processId;
        ThreadId = threadId;
        LogName = logName;
        MachineName = machineName;
        UserId = userId;
        Version = version;
    }

    internal string ProviderName { get; }
    internal Guid? ProviderId { get; }
    internal int Id { get; }
    internal ushort? Qualifiers { get; }
    internal byte? Level { get; }
    internal int? Task { get; }
    internal short? Opcode { get; }
    internal long? Keywords { get; }
    internal DateTime TimeCreated { get; }
    internal long? RecordId { get; }
    internal Guid? ActivityId { get; }
    internal Guid? RelatedActivityId { get; }
    internal int? ProcessId { get; }
    internal int? ThreadId { get; }
    internal string LogName { get; }
    internal string MachineName { get; }
    internal SecurityIdentifier? UserId { get; }
    internal byte? Version { get; }
}
