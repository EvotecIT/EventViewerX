namespace PSEventViewer;

public sealed partial class CmdletGetEVXEvent {
    private async Task ProcessTypeAsync(CancellationToken token, EventPredicate? predicate) {
        // let's find the events prepared for search
        List<EventType> typeList = Type.ToList();
        int typeThreads = DisableParallel.IsPresent
            ? 1
            : MaxConcurrency;
        var typeQueryInfo = new EventTypeQueryExecutionInfo();
        if (ExecutionInfo != null) {
            ExecutionInfo.Value = typeQueryInfo;
        }
        Func<EventTypeRecord, bool>? typeResultPredicate = MessageRegex == null
            ? null
            : eventObject => MessageMatches(eventObject.SourceEvent);
        EventEnrichmentOptions? enrichmentOptions = ResolveDns
            ? new EventEnrichmentOptions {
                ResolveDns = true,
                DnsTimeoutMilliseconds = DnsTimeoutMs,
                DnsMaxConcurrency = DnsMaxConcurrency,
                RetryDnsOnTransient = false
            }
            : null;
        var typeQuery =
            new EventTypeQuery(typeList) {
                Paths = Path.Length == 0
                    ? null
                    : Path,
                SavedEventReader = _resolvedSavedEventReader,
                SavedEventDiagnosticHandler = _resolvedSavedEventReader != null
                    ? WriteSavedEventDiagnostic
                    : null,
                MachineNames = Collector ?? MachineName,
                CollectorLogName = Collector == null
                    ? null
                    : "ForwardedEvents",
                StartTime = StartTime,
                EndTime = EndTime,
                TimePeriod = TimePeriod,
                SourceLogName = null,
                SourceEventIds = null,
                SourceRecordIds = EventRecordId,
                MaxConcurrency =
                    typeThreads,
                MaxEvents = MaxEvents,
                MaxCandidates =
                    MaxEventsScanned,
                MinimumRecordIdExclusiveResolver =
                    GetCheckpointLowerBound,
                CandidateObserver =
                    candidate =>
                        TrackCheckpointProgress(
                            candidate),
                Oldest = EffectiveOldest,
                ReadMode =
                    ReadMode,
                ResultPredicate =
                    typeResultPredicate,
                Predicate = predicate,
                Enrichment =
                    enrichmentOptions,
                MessageCulture =
                    MessageCulture,
                FallbackMessageCulture =
                    FallbackMessageCulture,
                Credential =
                    Credential?.GetNetworkCredential(),
                Authentication =
                    Authentication,
                RemoteConnectionTimeoutMilliseconds =
                    EffectiveRemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    EffectiveRemoteReadTimeoutMilliseconds,
                BufferCapacity =
                    BufferCapacity > 0
                        ? BufferCapacity
                        : 64,
                ContinueOnRemoteFailure =
                    ContinueOnError.IsPresent ||
                    (MachineName?.Count ?? 0) > 1,
                IncludeBookmark =
                    IncludeBookmark.IsPresent
            };
        if (Explain.IsPresent) {
            WriteObject(EventTypeEngine.Explain(typeQuery));
            return;
        }
        await foreach (EventTypeRecord eventObject in
                       EventTypeEngine.ReadAsync(
                           typeQuery,
                           typeQueryInfo,
                           token)) {
            token.ThrowIfCancellationRequested();
            if (!TrackCheckpointProgress(eventObject.SourceEvent)) {
                continue;
            }
            object output = ExpandData
                ? GetExpandedObject(eventObject, eventObject.SourceEvent)
                : eventObject;
            WriteObjectWithBackpressure(output);
            _eventsOutput++;
            if (OutputLimitReached) {
                break;
            }
        }
        WriteNamedTargetFailures(
            typeQueryInfo.TargetFailures);
    }

    private async Task ProcessDefinitionAsync(CancellationToken token, EventPredicate? predicate) {
        EventDefinition definition = ResolveEventDefinition();
        if (Collector != null && MachineName != null) {
            throw new PSArgumentException(
                "-Collector and -MachineName cannot be used together. Use -Collector for ForwardedEvents or -MachineName for direct source queries.");
        }
        var query = new EventDefinitionQuery(definition) {
            Paths = Path.Length == 0 ? null : Path,
            SavedEventReader = _resolvedSavedEventReader,
            SavedEventDiagnosticHandler = _resolvedSavedEventReader != null ? WriteSavedEventDiagnostic : null,
            MachineNames = Collector ?? MachineName,
            CollectorLogName = Collector == null ? null : "ForwardedEvents",
            StartTime = StartTime,
            EndTime = EndTime,
            TimePeriod = TimePeriod,
            RecordIds = EventRecordId,
            MaxEvents = MaxEvents,
            MaxCandidates = MaxEventsScanned,
            MaxConcurrency = DisableParallel.IsPresent ? 1 : MaxConcurrency,
            Oldest = EffectiveOldest,
            ReadMode = ReadMode,
            IncludeBookmark = IncludeBookmark.IsPresent,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            RemoteConnectionTimeoutMilliseconds = EffectiveRemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = EffectiveRemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity > 0 ? BufferCapacity : 64,
            MessageCulture = MessageCulture,
            FallbackMessageCulture = FallbackMessageCulture,
            Predicate = predicate,
            ResultPredicate = MessageRegex == null ? null : record => MessageMatches(record.SourceEvent),
            MinimumRecordIdExclusiveResolver = GetCheckpointLowerBound,
            CandidateObserver = candidate => TrackCheckpointProgress(candidate),
            ContinueOnRemoteFailure = ContinueOnError.IsPresent || (MachineName?.Count ?? 0) > 1
        };
        var info = new EventDefinitionQueryExecutionInfo();
        if (Explain.IsPresent) {
            WriteObject(EventDefinitionEngine.Explain(query));
            return;
        }
        if (ExecutionInfo != null) {
            ExecutionInfo.Value = info;
        }
        await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info, token)) {
            token.ThrowIfCancellationRequested();
            if (!TrackCheckpointProgress(record.SourceEvent)) {
                continue;
            }
            PSObject output = new(record);
            foreach (KeyValuePair<string, object?> value in record.Values.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                if (output.Properties[value.Key] == null) {
                    output.Properties.Add(new PSNoteProperty(value.Key, value.Value));
                }
            }
            if (ExpandData.IsPresent) {
                foreach (KeyValuePair<string, string> value in record.SourceEvent.Data.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                    if (output.Properties[value.Key] == null) {
                        output.Properties.Add(new PSNoteProperty(value.Key, value.Value));
                    }
                }
            }
            WriteObjectWithBackpressure(output);
            _eventsOutput++;
            if (OutputLimitReached) {
                break;
            }
        }
        WriteNamedTargetFailures(info.TargetFailures);
    }

}