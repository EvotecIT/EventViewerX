namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    private bool UsesBuiltInTypeQuery =>
        ParameterSetName == "Type" ||
        ParameterSetName == "Preset" ||
        _typedFilter?.Type != null;

    private EventPredicate? ResolvePreset() {
        if (ParameterSetName != "Preset") {
            return null;
        }
        if (!Preset.HasValue) {
            throw new PSArgumentException("Specify Preset for the preset event query.", nameof(Preset));
        }
        EventMonitoringPresetDefinition definition = EventMonitoringPresetCatalog.Get(Preset.Value);
        Type = definition.Types.ToArray();
        return definition.Predicate?.Clone();
    }

    private async Task ProcessGroupPolicyContextAsync() {
        if (Type.Length != 1 || Type[0] != EventType.GroupPolicyDirectoryAudit) {
            throw new PSArgumentException(
                "ContextStorePath requires exactly -Type GroupPolicyDirectoryAudit so the persistent context engine owns the complete Group Policy directory-audit timeline.",
                nameof(ContextStorePath));
        }
        if (Where != null || MessageRegex != null || ResolveDns.IsPresent || EventRecordId?.Length > 0 ||
            !string.IsNullOrWhiteSpace(RecordIdFile)) {
            throw new PSArgumentException(
                "ContextStorePath cannot be combined with Where, MessageRegex, ResolveDns, EventRecordId, or RecordIdFile because persistent Group Policy context requires its own complete timeline and checkpoints.",
                nameof(ContextStorePath));
        }
        List<string?>? targets = Collector ?? MachineName;
        var query = new GroupPolicyAuditQuery {
            ContextStore = new SqliteEventContextStore(ContextStorePath!),
            AuthorizationContext = ContextAuthorization,
            Paths = Path.Length == 0 ? null : Path,
            MachineNames = targets,
            CollectorLogName = Collector == null ? null : "ForwardedEvents",
            StartTime = StartTime,
            EndTime = EndTime,
            TimePeriod = TimePeriod,
            MaxEvents = MaxEvents,
            MaxCandidates = MaxEventsScanned,
            MaxConcurrency = DisableParallel.IsPresent ? 1 : MaxConcurrency,
            Oldest = true,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            RemoteConnectionTimeoutMilliseconds = EffectiveRemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = EffectiveRemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity > 0 ? BufferCapacity : 64,
            MessageCulture = MessageCulture,
            FallbackMessageCulture = FallbackMessageCulture,
            ContinueOnRemoteFailure = ContinueOnError.IsPresent || (targets?.Count ?? 0) > 1
        };
        var execution = new GroupPolicyAuditQueryExecutionInfo();
        await foreach (GroupPolicyAuditRecord record in GroupPolicyAuditEngine.ReadAsync(
                           query,
                           execution,
                           CancelToken)) {
            WriteObject(record);
        }
        WriteNamedTargetFailures(execution.TargetFailures);
    }
}
