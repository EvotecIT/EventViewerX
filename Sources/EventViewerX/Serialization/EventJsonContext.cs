using System.Text.Json.Serialization;

namespace EventViewerX;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(global::EventViewerX.AuthenticationType))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerProtectorType))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerVolumeType))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSourceAuthorizationStatus))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionContentFormat))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionDeliveryMode))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionType))]
[JsonSerializable(typeof(global::EventViewerX.DetectionScopeJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.ElevatedToken))]
[JsonSerializable(typeof(global::EventViewerX.EventAnalysisContractKind))]
[JsonSerializable(typeof(global::EventViewerX.EventAuditOutcome))]
[JsonSerializable(typeof(global::EventViewerX.EventCheckpointStore.CheckpointStateDocument))]
[JsonSerializable(typeof(global::EventViewerX.EventClockOrder))]
[JsonSerializable(typeof(global::EventViewerX.EventContextObjectKind))]
[JsonSerializable(typeof(global::EventViewerX.EventContextProvenance))]
[JsonSerializable(typeof(global::EventViewerX.EventContextState))]
[JsonSerializable(typeof(global::EventViewerX.EventDecisionReportKind))]
[JsonSerializable(typeof(global::EventViewerX.EventDefinition))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionCoverage.CoverageEnvelope))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionEngine.Evaluator.CheckpointState))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFindingStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFixtureKind))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPack.PackEnvelope))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPack.PackPayload))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPackSignatureStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPlan.PlanDocument))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionReplaySession.ReplayCheckpoint))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionRuleDefinition))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionRuleKind))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionSeverity))]
[JsonSerializable(typeof(global::EventViewerX.EventDiagnosticCodeKind))]
[JsonSerializable(typeof(global::EventViewerX.EventDiagnosticRecord))]
[JsonSerializable(typeof(global::EventViewerX.EventEndpointAnalysis))]
[JsonSerializable(typeof(global::EventViewerX.EventEndpointCapture))]
[JsonSerializable(typeof(global::EventViewerX.EventExportFormat))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldFilterStage))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldNumericBase))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldSource))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldValueKind))]
[JsonSerializable(typeof(global::EventViewerX.EventInvestigationManifest))]
[JsonSerializable(typeof(global::EventViewerX.EventInvestigationSession.EndpointReceipt))]
[JsonSerializable(typeof(global::EventViewerX.EventLateArrivalPolicy))]
[JsonSerializable(typeof(global::EventViewerX.EventLogAuthentication))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsReadStage))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventLogProbeStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventLogQuerySourceKind))]
[JsonSerializable(typeof(global::EventViewerX.EventLogRemoteQueryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSessionOpenStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSubscriptionStart))]
[JsonSerializable(typeof(global::EventViewerX.EventMessageRenderStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventMonitoringPreset))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizationOutcome))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizedValueKind))]
[JsonSerializable(typeof(global::EventViewerX.EventObservationSnapshot))]
[JsonSerializable(typeof(global::EventViewerX.EventPivotKind))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicate))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateKind))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateOperator))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicatePlanStage))]
[JsonSerializable(typeof(global::EventViewerX.EventReadMode))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessDiagnosticKind))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessEvidenceLevel))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessLayer))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessScenario))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessStatus))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementKind))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementVolume))]
[JsonSerializable(typeof(global::EventViewerX.EventRuleDiscoveryMode))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryScope))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetKind))]
[JsonSerializable(typeof(global::EventViewerX.EventTimelineEntryKind))]
[JsonSerializable(typeof(global::EventViewerX.EvidencePrivacyJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.FailureReason))]
[JsonSerializable(typeof(global::EventViewerX.FindingJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditEventKind))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditTargetKind))]
[JsonSerializable(typeof(global::EventViewerX.ImpersonationLevel))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Disposition))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Issue))]
[JsonSerializable(typeof(global::EventViewerX.KerberosRc4ImpactState))]
[JsonSerializable(typeof(global::EventViewerX.KerberosSupportedEncryptionTypes))]
[JsonSerializable(typeof(global::EventViewerX.Keywords))]
[JsonSerializable(typeof(global::EventViewerX.KnownEventField))]
[JsonSerializable(typeof(global::EventViewerX.KnownLog))]
[JsonSerializable(typeof(global::EventViewerX.Level))]
[JsonSerializable(typeof(global::EventViewerX.LogonType))]
[JsonSerializable(typeof(global::EventViewerX.NasPortType))]
[JsonSerializable(typeof(global::EventViewerX.ObservationJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.PlanExplanationJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.PlanHashJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.PowerShellEdition))]
[JsonSerializable(typeof(global::EventViewerX.PreAuthType))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelIsolation))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelType))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderDefinition))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldOutputType))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldType))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderInstallationState))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderMapKind))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageInstallStatus))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageManifest))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageTrustMode))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderSchemaLock))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderValidationSeverity))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationBucket))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationExecutionMode))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationInputCompleteness))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationNullPolicy))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationOperation))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationTopScope))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventDuplicateMode))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventEvidenceBundleManifest))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventReportSectionKind))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventReportSummary))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Correlation.NamedEventsTimelineQueryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Evtx.EvtxQueryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Inventory.EventCatalogFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveEventQueryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveStatsQueryFailureKind))]
[JsonSerializable(typeof(global::EventViewerX.ReverseDnsResolutionStatus))]
[JsonSerializable(typeof(global::EventViewerX.RuleTraceJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.SavedEventReadDiagnosticSeverity))]
[JsonSerializable(typeof(global::EventViewerX.SecurityDescriptorDaclState))]
[JsonSerializable(typeof(global::EventViewerX.StatusCode))]
[JsonSerializable(typeof(global::EventViewerX.SubStatusCode))]
[JsonSerializable(typeof(global::EventViewerX.SuppressionHashJsonDocument))]
[JsonSerializable(typeof(global::EventViewerX.TicketEncryptionType))]
[JsonSerializable(typeof(global::EventViewerX.TicketOptions))]
[JsonSerializable(typeof(global::EventViewerX.VirtualAccount))]
[JsonSerializable(typeof(global::System.Collections.Generic.Dictionary<string, long>))]
[JsonSerializable(typeof(global::System.Collections.Generic.Dictionary<string, object>))]
[JsonSerializable(typeof(global::System.Collections.Generic.IEnumerable<global::EventViewerX.Reporting.EventReportSectionSchema>))]
[JsonSerializable(typeof(global::System.Collections.Generic.IReadOnlyDictionary<string, object>))]
[JsonSerializable(typeof(global::System.DateTime))]
[JsonSerializable(typeof(global::System.DateTimeOffset))]
[JsonSerializable(typeof(global::System.Guid))]
[JsonSerializable(typeof(global::System.TimeSpan))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>))]
[JsonSerializable(typeof(System.Net.IPAddress))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<string>))]
[JsonSerializable(typeof(System.Collections.Generic.HashSet<string>))]
[JsonSerializable(typeof(System.Collections.Generic.List<string>))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(bool[]))]
[JsonSerializable(typeof(bool?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<bool>))]
[JsonSerializable(typeof(byte?))]
[JsonSerializable(typeof(byte?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<byte>))]
[JsonSerializable(typeof(sbyte?))]
[JsonSerializable(typeof(sbyte[]))]
[JsonSerializable(typeof(sbyte?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<sbyte>))]
[JsonSerializable(typeof(short?))]
[JsonSerializable(typeof(short[]))]
[JsonSerializable(typeof(short?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<short>))]
[JsonSerializable(typeof(ushort?))]
[JsonSerializable(typeof(ushort[]))]
[JsonSerializable(typeof(ushort?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<ushort>))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(int?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<int>))]
[JsonSerializable(typeof(uint?))]
[JsonSerializable(typeof(uint[]))]
[JsonSerializable(typeof(uint?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<uint>))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(long?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<long>))]
[JsonSerializable(typeof(ulong?))]
[JsonSerializable(typeof(ulong[]))]
[JsonSerializable(typeof(ulong?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<ulong>))]
[JsonSerializable(typeof(float?))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(float?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<float>))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(double?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<double>))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(decimal[]))]
[JsonSerializable(typeof(decimal?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<decimal>))]
[JsonSerializable(typeof(char?))]
[JsonSerializable(typeof(char[]))]
[JsonSerializable(typeof(char?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<char>))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(DateTime[]))]
[JsonSerializable(typeof(DateTime?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<DateTime>))]
[JsonSerializable(typeof(DateTimeOffset?))]
[JsonSerializable(typeof(DateTimeOffset[]))]
[JsonSerializable(typeof(DateTimeOffset?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<DateTimeOffset>))]
[JsonSerializable(typeof(TimeSpan?))]
[JsonSerializable(typeof(TimeSpan[]))]
[JsonSerializable(typeof(TimeSpan?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<TimeSpan>))]
[JsonSerializable(typeof(Guid?))]
[JsonSerializable(typeof(Guid[]))]
[JsonSerializable(typeof(Guid?[]))]
[JsonSerializable(typeof(System.Collections.Generic.List<Guid>))]
[JsonSerializable(typeof(global::EventViewerX.AuthenticationType[]))]
[JsonSerializable(typeof(global::EventViewerX.AuthenticationType?))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerProtectorType[]))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerProtectorType?))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerVolumeType[]))]
[JsonSerializable(typeof(global::EventViewerX.BitLockerVolumeType?))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSourceAuthorizationStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSourceAuthorizationStatus?))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionContentFormat[]))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionContentFormat?))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionDeliveryMode[]))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionDeliveryMode?))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionType[]))]
[JsonSerializable(typeof(global::EventViewerX.CollectorSubscriptionType?))]
[JsonSerializable(typeof(global::EventViewerX.ElevatedToken[]))]
[JsonSerializable(typeof(global::EventViewerX.ElevatedToken?))]
[JsonSerializable(typeof(global::EventViewerX.EventAnalysisContractKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventAnalysisContractKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventAuditOutcome[]))]
[JsonSerializable(typeof(global::EventViewerX.EventAuditOutcome?))]
[JsonSerializable(typeof(global::EventViewerX.EventClockOrder[]))]
[JsonSerializable(typeof(global::EventViewerX.EventClockOrder?))]
[JsonSerializable(typeof(global::EventViewerX.EventContextObjectKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventContextObjectKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventContextProvenance[]))]
[JsonSerializable(typeof(global::EventViewerX.EventContextProvenance?))]
[JsonSerializable(typeof(global::EventViewerX.EventContextState[]))]
[JsonSerializable(typeof(global::EventViewerX.EventContextState?))]
[JsonSerializable(typeof(global::EventViewerX.EventDecisionReportKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDecisionReportKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFindingStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFindingStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFixtureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionFixtureKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPackSignatureStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionPackSignatureStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionRuleKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionRuleKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionSeverity[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDetectionSeverity?))]
[JsonSerializable(typeof(global::EventViewerX.EventDiagnosticCodeKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventDiagnosticCodeKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventExportFormat[]))]
[JsonSerializable(typeof(global::EventViewerX.EventExportFormat?))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldFilterStage[]))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldFilterStage?))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldNumericBase[]))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldNumericBase?))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldSource[]))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldSource?))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldValueKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventFieldValueKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventLateArrivalPolicy[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLateArrivalPolicy?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogAuthentication[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogAuthentication?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsReadStage[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsReadStage?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogDetailsStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogProbeStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogProbeStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogQuerySourceKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogQuerySourceKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogRemoteQueryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogRemoteQueryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSessionOpenStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSessionOpenStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSubscriptionStart[]))]
[JsonSerializable(typeof(global::EventViewerX.EventLogSubscriptionStart?))]
[JsonSerializable(typeof(global::EventViewerX.EventMessageRenderStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventMessageRenderStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventMonitoringPreset[]))]
[JsonSerializable(typeof(global::EventViewerX.EventMonitoringPreset?))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizationOutcome[]))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizationOutcome?))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizedValueKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventNormalizedValueKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventPivotKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventPivotKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateOperator[]))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicateOperator?))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicatePlanStage[]))]
[JsonSerializable(typeof(global::EventViewerX.EventPredicatePlanStage?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadMode[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadMode?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessDiagnosticKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessDiagnosticKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessEvidenceLevel[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessEvidenceLevel?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessLayer[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessLayer?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessScenario[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessScenario?))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.EventReadinessStatus?))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementVolume[]))]
[JsonSerializable(typeof(global::EventViewerX.EventRequirementVolume?))]
[JsonSerializable(typeof(global::EventViewerX.EventRuleDiscoveryMode[]))]
[JsonSerializable(typeof(global::EventViewerX.EventRuleDiscoveryMode?))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryScope[]))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetDiscoveryScope?))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventTargetKind?))]
[JsonSerializable(typeof(global::EventViewerX.EventTimelineEntryKind[]))]
[JsonSerializable(typeof(global::EventViewerX.EventTimelineEntryKind?))]
[JsonSerializable(typeof(global::EventViewerX.FailureReason[]))]
[JsonSerializable(typeof(global::EventViewerX.FailureReason?))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditEventKind[]))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditEventKind?))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditTargetKind[]))]
[JsonSerializable(typeof(global::EventViewerX.GroupPolicyAuditTargetKind?))]
[JsonSerializable(typeof(global::EventViewerX.ImpersonationLevel[]))]
[JsonSerializable(typeof(global::EventViewerX.ImpersonationLevel?))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Disposition[]))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Disposition?))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Issue[]))]
[JsonSerializable(typeof(global::EventViewerX.KerberosKdcRc4Issue?))]
[JsonSerializable(typeof(global::EventViewerX.KerberosRc4ImpactState[]))]
[JsonSerializable(typeof(global::EventViewerX.KerberosRc4ImpactState?))]
[JsonSerializable(typeof(global::EventViewerX.KerberosSupportedEncryptionTypes[]))]
[JsonSerializable(typeof(global::EventViewerX.KerberosSupportedEncryptionTypes?))]
[JsonSerializable(typeof(global::EventViewerX.Keywords[]))]
[JsonSerializable(typeof(global::EventViewerX.Keywords?))]
[JsonSerializable(typeof(global::EventViewerX.KnownEventField[]))]
[JsonSerializable(typeof(global::EventViewerX.KnownEventField?))]
[JsonSerializable(typeof(global::EventViewerX.KnownLog[]))]
[JsonSerializable(typeof(global::EventViewerX.KnownLog?))]
[JsonSerializable(typeof(global::EventViewerX.Level[]))]
[JsonSerializable(typeof(global::EventViewerX.Level?))]
[JsonSerializable(typeof(global::EventViewerX.LogonType[]))]
[JsonSerializable(typeof(global::EventViewerX.LogonType?))]
[JsonSerializable(typeof(global::EventViewerX.NasPortType[]))]
[JsonSerializable(typeof(global::EventViewerX.NasPortType?))]
[JsonSerializable(typeof(global::EventViewerX.PowerShellEdition[]))]
[JsonSerializable(typeof(global::EventViewerX.PowerShellEdition?))]
[JsonSerializable(typeof(global::EventViewerX.PreAuthType[]))]
[JsonSerializable(typeof(global::EventViewerX.PreAuthType?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelIsolation[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelIsolation?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelType[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderChannelType?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldOutputType[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldOutputType?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldType[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderFieldType?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderMapKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderMapKind?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageInstallStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageInstallStatus?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageTrustMode[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderPackageTrustMode?))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderValidationSeverity[]))]
[JsonSerializable(typeof(global::EventViewerX.Providers.EventProviderValidationSeverity?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationBucket[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationBucket?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationExecutionMode[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationExecutionMode?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationInputCompleteness[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationInputCompleteness?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationNullPolicy[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationNullPolicy?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationOperation[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationOperation?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationTopScope[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventAggregationTopScope?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventDuplicateMode[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventDuplicateMode?))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventReportSectionKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reporting.EventReportSectionKind?))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Correlation.NamedEventsTimelineQueryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Correlation.NamedEventsTimelineQueryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Evtx.EvtxQueryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Evtx.EvtxQueryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Inventory.EventCatalogFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Inventory.EventCatalogFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveEventQueryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveEventQueryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveStatsQueryFailureKind[]))]
[JsonSerializable(typeof(global::EventViewerX.Reports.Live.LiveStatsQueryFailureKind?))]
[JsonSerializable(typeof(global::EventViewerX.ReverseDnsResolutionStatus[]))]
[JsonSerializable(typeof(global::EventViewerX.ReverseDnsResolutionStatus?))]
[JsonSerializable(typeof(global::EventViewerX.SavedEventReadDiagnosticSeverity[]))]
[JsonSerializable(typeof(global::EventViewerX.SavedEventReadDiagnosticSeverity?))]
[JsonSerializable(typeof(global::EventViewerX.SecurityDescriptorDaclState[]))]
[JsonSerializable(typeof(global::EventViewerX.SecurityDescriptorDaclState?))]
[JsonSerializable(typeof(global::EventViewerX.StatusCode[]))]
[JsonSerializable(typeof(global::EventViewerX.StatusCode?))]
[JsonSerializable(typeof(global::EventViewerX.SubStatusCode[]))]
[JsonSerializable(typeof(global::EventViewerX.SubStatusCode?))]
[JsonSerializable(typeof(global::EventViewerX.TicketEncryptionType[]))]
[JsonSerializable(typeof(global::EventViewerX.TicketEncryptionType?))]
[JsonSerializable(typeof(global::EventViewerX.TicketOptions[]))]
[JsonSerializable(typeof(global::EventViewerX.TicketOptions?))]
[JsonSerializable(typeof(global::EventViewerX.VirtualAccount[]))]
[JsonSerializable(typeof(global::EventViewerX.VirtualAccount?))]
[JsonSerializable(typeof(IReadOnlyList<byte>))]
[JsonSerializable(typeof(IReadOnlyList<long>))]
[JsonSerializable(typeof(Dictionary<string, EventNormalizedValue>))]
[JsonSerializable(typeof(EventType))]
[JsonSerializable(typeof(EventType[]))]
[JsonSerializable(typeof(EventType?))]
[JsonSerializable(typeof(TimePeriod))]
[JsonSerializable(typeof(TimePeriod[]))]
[JsonSerializable(typeof(TimePeriod?))]
[JsonSerializable(typeof(GroupPolicy))]
[JsonSerializable(typeof(List<GroupPolicyLinks>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
internal partial class EventJsonContext : JsonSerializerContext {
    internal Type? GetTypeByName(string name) => name switch {
        "EventViewerX.EventType" => typeof(EventType),
        "EventViewerX.TimePeriod" => typeof(TimePeriod),
        "EventViewerX.AuthenticationType" => typeof(global::EventViewerX.AuthenticationType),
        "EventViewerX.BitLockerProtectorType" => typeof(global::EventViewerX.BitLockerProtectorType),
        "EventViewerX.BitLockerVolumeType" => typeof(global::EventViewerX.BitLockerVolumeType),
        "EventViewerX.CollectorSourceAuthorizationStatus" => typeof(global::EventViewerX.CollectorSourceAuthorizationStatus),
        "EventViewerX.CollectorSubscriptionContentFormat" => typeof(global::EventViewerX.CollectorSubscriptionContentFormat),
        "EventViewerX.CollectorSubscriptionDeliveryMode" => typeof(global::EventViewerX.CollectorSubscriptionDeliveryMode),
        "EventViewerX.CollectorSubscriptionType" => typeof(global::EventViewerX.CollectorSubscriptionType),
        "EventViewerX.ElevatedToken" => typeof(global::EventViewerX.ElevatedToken),
        "EventViewerX.EventAnalysisContractKind" => typeof(global::EventViewerX.EventAnalysisContractKind),
        "EventViewerX.EventAuditOutcome" => typeof(global::EventViewerX.EventAuditOutcome),
        "EventViewerX.EventClockOrder" => typeof(global::EventViewerX.EventClockOrder),
        "EventViewerX.EventContextObjectKind" => typeof(global::EventViewerX.EventContextObjectKind),
        "EventViewerX.EventContextProvenance" => typeof(global::EventViewerX.EventContextProvenance),
        "EventViewerX.EventContextState" => typeof(global::EventViewerX.EventContextState),
        "EventViewerX.EventDecisionReportKind" => typeof(global::EventViewerX.EventDecisionReportKind),
        "EventViewerX.EventDetectionFindingStatus" => typeof(global::EventViewerX.EventDetectionFindingStatus),
        "EventViewerX.EventDetectionFixtureKind" => typeof(global::EventViewerX.EventDetectionFixtureKind),
        "EventViewerX.EventDetectionPackSignatureStatus" => typeof(global::EventViewerX.EventDetectionPackSignatureStatus),
        "EventViewerX.EventDetectionRuleKind" => typeof(global::EventViewerX.EventDetectionRuleKind),
        "EventViewerX.EventDetectionSeverity" => typeof(global::EventViewerX.EventDetectionSeverity),
        "EventViewerX.EventDiagnosticCodeKind" => typeof(global::EventViewerX.EventDiagnosticCodeKind),
        "EventViewerX.EventExportFormat" => typeof(global::EventViewerX.EventExportFormat),
        "EventViewerX.EventFieldFilterStage" => typeof(global::EventViewerX.EventFieldFilterStage),
        "EventViewerX.EventFieldNumericBase" => typeof(global::EventViewerX.EventFieldNumericBase),
        "EventViewerX.EventFieldSource" => typeof(global::EventViewerX.EventFieldSource),
        "EventViewerX.EventFieldValueKind" => typeof(global::EventViewerX.EventFieldValueKind),
        "EventViewerX.EventLateArrivalPolicy" => typeof(global::EventViewerX.EventLateArrivalPolicy),
        "EventViewerX.EventLogAuthentication" => typeof(global::EventViewerX.EventLogAuthentication),
        "EventViewerX.EventLogDetailsReadStage" => typeof(global::EventViewerX.EventLogDetailsReadStage),
        "EventViewerX.EventLogDetailsStatus" => typeof(global::EventViewerX.EventLogDetailsStatus),
        "EventViewerX.EventLogProbeStatus" => typeof(global::EventViewerX.EventLogProbeStatus),
        "EventViewerX.EventLogQuerySourceKind" => typeof(global::EventViewerX.EventLogQuerySourceKind),
        "EventViewerX.EventLogRemoteQueryFailureKind" => typeof(global::EventViewerX.EventLogRemoteQueryFailureKind),
        "EventViewerX.EventLogSessionOpenStatus" => typeof(global::EventViewerX.EventLogSessionOpenStatus),
        "EventViewerX.EventLogSubscriptionStart" => typeof(global::EventViewerX.EventLogSubscriptionStart),
        "EventViewerX.EventMessageRenderStatus" => typeof(global::EventViewerX.EventMessageRenderStatus),
        "EventViewerX.EventMonitoringPreset" => typeof(global::EventViewerX.EventMonitoringPreset),
        "EventViewerX.EventNormalizationOutcome" => typeof(global::EventViewerX.EventNormalizationOutcome),
        "EventViewerX.EventNormalizedValueKind" => typeof(global::EventViewerX.EventNormalizedValueKind),
        "EventViewerX.EventPivotKind" => typeof(global::EventViewerX.EventPivotKind),
        "EventViewerX.EventPredicateKind" => typeof(global::EventViewerX.EventPredicateKind),
        "EventViewerX.EventPredicateOperator" => typeof(global::EventViewerX.EventPredicateOperator),
        "EventViewerX.EventPredicatePlanStage" => typeof(global::EventViewerX.EventPredicatePlanStage),
        "EventViewerX.EventReadMode" => typeof(global::EventViewerX.EventReadMode),
        "EventViewerX.EventReadinessDiagnosticKind" => typeof(global::EventViewerX.EventReadinessDiagnosticKind),
        "EventViewerX.EventReadinessEvidenceLevel" => typeof(global::EventViewerX.EventReadinessEvidenceLevel),
        "EventViewerX.EventReadinessLayer" => typeof(global::EventViewerX.EventReadinessLayer),
        "EventViewerX.EventReadinessScenario" => typeof(global::EventViewerX.EventReadinessScenario),
        "EventViewerX.EventReadinessStatus" => typeof(global::EventViewerX.EventReadinessStatus),
        "EventViewerX.EventRequirementKind" => typeof(global::EventViewerX.EventRequirementKind),
        "EventViewerX.EventRequirementVolume" => typeof(global::EventViewerX.EventRequirementVolume),
        "EventViewerX.EventRuleDiscoveryMode" => typeof(global::EventViewerX.EventRuleDiscoveryMode),
        "EventViewerX.EventTargetDiscoveryFailureKind" => typeof(global::EventViewerX.EventTargetDiscoveryFailureKind),
        "EventViewerX.EventTargetDiscoveryScope" => typeof(global::EventViewerX.EventTargetDiscoveryScope),
        "EventViewerX.EventTargetKind" => typeof(global::EventViewerX.EventTargetKind),
        "EventViewerX.EventTimelineEntryKind" => typeof(global::EventViewerX.EventTimelineEntryKind),
        "EventViewerX.FailureReason" => typeof(global::EventViewerX.FailureReason),
        "EventViewerX.GroupPolicyAuditEventKind" => typeof(global::EventViewerX.GroupPolicyAuditEventKind),
        "EventViewerX.GroupPolicyAuditTargetKind" => typeof(global::EventViewerX.GroupPolicyAuditTargetKind),
        "EventViewerX.ImpersonationLevel" => typeof(global::EventViewerX.ImpersonationLevel),
        "EventViewerX.KerberosKdcRc4Disposition" => typeof(global::EventViewerX.KerberosKdcRc4Disposition),
        "EventViewerX.KerberosKdcRc4Issue" => typeof(global::EventViewerX.KerberosKdcRc4Issue),
        "EventViewerX.KerberosRc4ImpactState" => typeof(global::EventViewerX.KerberosRc4ImpactState),
        "EventViewerX.KerberosSupportedEncryptionTypes" => typeof(global::EventViewerX.KerberosSupportedEncryptionTypes),
        "EventViewerX.Keywords" => typeof(global::EventViewerX.Keywords),
        "EventViewerX.KnownEventField" => typeof(global::EventViewerX.KnownEventField),
        "EventViewerX.KnownLog" => typeof(global::EventViewerX.KnownLog),
        "EventViewerX.Level" => typeof(global::EventViewerX.Level),
        "EventViewerX.LogonType" => typeof(global::EventViewerX.LogonType),
        "EventViewerX.NasPortType" => typeof(global::EventViewerX.NasPortType),
        "EventViewerX.PowerShellEdition" => typeof(global::EventViewerX.PowerShellEdition),
        "EventViewerX.PreAuthType" => typeof(global::EventViewerX.PreAuthType),
        "EventViewerX.Providers.EventProviderChannelIsolation" => typeof(global::EventViewerX.Providers.EventProviderChannelIsolation),
        "EventViewerX.Providers.EventProviderChannelType" => typeof(global::EventViewerX.Providers.EventProviderChannelType),
        "EventViewerX.Providers.EventProviderFieldOutputType" => typeof(global::EventViewerX.Providers.EventProviderFieldOutputType),
        "EventViewerX.Providers.EventProviderFieldType" => typeof(global::EventViewerX.Providers.EventProviderFieldType),
        "EventViewerX.Providers.EventProviderMapKind" => typeof(global::EventViewerX.Providers.EventProviderMapKind),
        "EventViewerX.Providers.EventProviderPackageInstallStatus" => typeof(global::EventViewerX.Providers.EventProviderPackageInstallStatus),
        "EventViewerX.Providers.EventProviderPackageTrustMode" => typeof(global::EventViewerX.Providers.EventProviderPackageTrustMode),
        "EventViewerX.Providers.EventProviderValidationSeverity" => typeof(global::EventViewerX.Providers.EventProviderValidationSeverity),
        "EventViewerX.Reporting.EventAggregationBucket" => typeof(global::EventViewerX.Reporting.EventAggregationBucket),
        "EventViewerX.Reporting.EventAggregationExecutionMode" => typeof(global::EventViewerX.Reporting.EventAggregationExecutionMode),
        "EventViewerX.Reporting.EventAggregationInputCompleteness" => typeof(global::EventViewerX.Reporting.EventAggregationInputCompleteness),
        "EventViewerX.Reporting.EventAggregationNullPolicy" => typeof(global::EventViewerX.Reporting.EventAggregationNullPolicy),
        "EventViewerX.Reporting.EventAggregationOperation" => typeof(global::EventViewerX.Reporting.EventAggregationOperation),
        "EventViewerX.Reporting.EventAggregationTopScope" => typeof(global::EventViewerX.Reporting.EventAggregationTopScope),
        "EventViewerX.Reporting.EventDuplicateMode" => typeof(global::EventViewerX.Reporting.EventDuplicateMode),
        "EventViewerX.Reporting.EventReportSectionKind" => typeof(global::EventViewerX.Reporting.EventReportSectionKind),
        "EventViewerX.Reports.Correlation.NamedEventsTimelineQueryFailureKind" => typeof(global::EventViewerX.Reports.Correlation.NamedEventsTimelineQueryFailureKind),
        "EventViewerX.Reports.Evtx.EvtxQueryFailureKind" => typeof(global::EventViewerX.Reports.Evtx.EvtxQueryFailureKind),
        "EventViewerX.Reports.Inventory.EventCatalogFailureKind" => typeof(global::EventViewerX.Reports.Inventory.EventCatalogFailureKind),
        "EventViewerX.Reports.Live.LiveEventQueryFailureKind" => typeof(global::EventViewerX.Reports.Live.LiveEventQueryFailureKind),
        "EventViewerX.Reports.Live.LiveStatsQueryFailureKind" => typeof(global::EventViewerX.Reports.Live.LiveStatsQueryFailureKind),
        "EventViewerX.ReverseDnsResolutionStatus" => typeof(global::EventViewerX.ReverseDnsResolutionStatus),
        "EventViewerX.SavedEventReadDiagnosticSeverity" => typeof(global::EventViewerX.SavedEventReadDiagnosticSeverity),
        "EventViewerX.SecurityDescriptorDaclState" => typeof(global::EventViewerX.SecurityDescriptorDaclState),
        "EventViewerX.StatusCode" => typeof(global::EventViewerX.StatusCode),
        "EventViewerX.SubStatusCode" => typeof(global::EventViewerX.SubStatusCode),
        "EventViewerX.TicketEncryptionType" => typeof(global::EventViewerX.TicketEncryptionType),
        "EventViewerX.TicketOptions" => typeof(global::EventViewerX.TicketOptions),
        "EventViewerX.VirtualAccount" => typeof(global::EventViewerX.VirtualAccount),
        _ => null
    };
}
