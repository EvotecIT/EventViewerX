namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Evaluates EventViewerX events with native detection and correlation rules.</para>
/// <para type="description">Compiles one immutable indexed plan, projects each raw event once, and emits explainable findings with evidence and pack provenance.</para>
/// <para type="description">Storage is not required. Pipe events directly from Get-EVXEvent or supply an array of detached EventObject instances.</para>
/// </summary>
/// <example>
///   <summary>Run the built-in detections</summary>
///   <code>Get-EVXEvent -Type ActiveDirectoryAuthentication -TimePeriod Last24Hours -Oldest | Invoke-EVXDetection</code>
///   <para>Evaluates the built-in native packs and emits findings as typed objects. Materialized input is normalized to deterministic event-time order before correlation.</para>
/// </example>
/// <example>
///   <summary>Apply environment tuning</summary>
///   <code>$tuning = [EventViewerX.EventDetectionTuning]::new(); $tuning.DisabledRuleIds = 'EVX-AUTH-0003'; Get-EVXEvent -Type ActiveDirectoryAuthentication -Oldest | Invoke-EVXDetection -Tuning $tuning</code>
///   <para>Disables a rule without changing the versioned pack content.</para>
/// </example>
/// <example>
///   <summary>Explain the effective plan</summary>
///   <code>Invoke-EVXDetection -Explain</code>
///   <para>Returns selectors, state requirements, and required typed projections without processing events.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "EVXDetection")]
[OutputType(typeof(EventDetectionFinding))]
[OutputType(typeof(EventDetectionPlanExplanation))]
[OutputType(typeof(EventDecisionReportSnapshot))]
public sealed class CmdletInvokeEVXDetection : PSCmdlet {
    private readonly List<EventObject> _events = new();

    /// <summary>Detached EventViewerX event to evaluate.</summary>
    [Parameter(ValueFromPipeline = true)]
    public EventObject? InputObject { get; set; }

    /// <summary>Explicit native rules. When omitted, the built-in packs are used.</summary>
    [Parameter]
    public IEventDetectionRule[] Rule { get; set; } = Array.Empty<IEventDetectionRule>();

    /// <summary>Explicit versioned packs. When omitted, the built-in packs are used.</summary>
    [Parameter]
    public EventDetectionPack[] Pack { get; set; } = Array.Empty<EventDetectionPack>();

    /// <summary>Adds the built-in packs when explicit Rule or Pack values are supplied.</summary>
    [Parameter]
    public SwitchParameter IncludeBuiltIn { get; set; }

    /// <summary>Environment-specific disables, severity changes, thresholds, and suppressions.</summary>
    [Parameter]
    public EventDetectionTuning? Tuning { get; set; }

    /// <summary>Expected and successfully collected source scope attached to every finding.</summary>
    [Parameter]
    public EventDetectionCoverage? Coverage { get; set; }

    /// <summary>Returns the effective compiled plan without evaluating input.</summary>
    [Parameter]
    public SwitchParameter Explain { get; set; }

    /// <summary>Returns one decision-oriented report snapshot instead of individual findings.</summary>
    [Parameter]
    public EventDecisionReportKind? ReportKind { get; set; }

    /// <summary>Maximum observations evaluated. Zero is unlimited.</summary>
    [Parameter]
    [ValidateRange(0, long.MaxValue)]
    public long MaximumObservations { get; set; } = 1000000;

    /// <summary>Maximum active correlation groups.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumGroups { get; set; } = 10000;

    /// <summary>Maximum observations retained across correlation state.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumStateObservations { get; set; } = 100000;

    /// <summary>Maximum estimated correlation-state bytes.</summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaximumStateBytes { get; set; } = 64L * 1024L * 1024L;

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (InputObject != null && (MaximumObservations == 0 || _events.Count <= MaximumObservations)) {
            _events.Add(InputObject);
        }
    }

    /// <inheritdoc />
    protected override void EndProcessing() {
        var rules = new List<IEventDetectionRule>();
        var packs = new List<EventDetectionPack>();
        bool explicitContent = Rule.Length > 0 || Pack.Length > 0;
        if (!explicitContent || IncludeBuiltIn) {
            EventDetectionPack[] builtIn = EventDetectionCatalog.GetBuiltInPacks().ToArray();
            packs.AddRange(builtIn);
            rules.AddRange(builtIn.SelectMany(static pack => pack.GetRules()));
        }
        rules.AddRange(Rule);
        foreach (EventDetectionPack pack in Pack) {
            if (pack == null) {
                throw new PSArgumentException("Pack cannot contain null values.", nameof(Pack));
            }
            packs.Add(pack);
            rules.AddRange(pack.GetRules());
        }
        EventDetectionPlan plan = EventDetectionPlan.Compile(rules, Tuning);
        if (Explain) {
            WriteObject(plan.Explain(), enumerateCollection: false);
            return;
        }
        var options = new EventDetectionEngineOptions {
            MaximumObservations = MaximumObservations,
            MaximumGroups = MaximumGroups,
            MaximumStateObservations = MaximumStateObservations,
            MaximumStateBytes = MaximumStateBytes,
            Coverage = Coverage
        };
        EventDetectionExecutionResult execution = EventDetectionEngine.Evaluate(_events, plan, options);
        if (ReportKind.HasValue) {
            EventDecisionReportSnapshot report = EventDecisionReportEngine.Create(
                ReportKind.Value,
                execution.Observations,
                execution.Findings,
                packs,
                new EventDetectionReportOptions {
                    QueryOwner = "PowerShell pipeline"
                });
            WriteObject(report, enumerateCollection: false);
            return;
        }
        foreach (EventDetectionFinding finding in execution.Findings) {
            WriteObject(finding, enumerateCollection: false);
        }
    }
}
