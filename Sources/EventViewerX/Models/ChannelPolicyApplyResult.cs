namespace EventViewerX;

using System.Collections.Generic;

/// <summary>
/// Result of attempting to apply a <see cref="ChannelPolicy"/> to a channel, including partial success details.
/// </summary>
public sealed class ChannelPolicyApplyResult {
    /// <summary>Log name that was targeted.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Remote computer where the policy was applied.</summary>
    public string? MachineName { get; set; }

    /// <summary>True only when every requested value was verified after the operation.</summary>
    public bool Success { get; set; }

    /// <summary>True when at least one change was applied, but some failed or were unsupported.</summary>
    public bool PartialSuccess { get; set; }

    /// <summary>Policy properties explicitly requested by the caller.</summary>
    public List<string> RequestedProperties { get; } = new();
    /// <summary>Properties that were saved and verified successfully.</summary>
    public List<string> AppliedProperties { get; } = new();
    /// <summary>Requested properties that already had the desired value.</summary>
    public List<string> UnchangedProperties { get; } = new();
    /// <summary>Properties that were skipped or unsupported on the target.</summary>
    public List<string> SkippedOrUnsupported { get; } = new();
    /// <summary>Error messages captured while applying the policy.</summary>
    public List<string> Errors { get; } = new();
    /// <summary>Policy snapshot read before applying changes.</summary>
    public ChannelPolicy? Before { get; set; }
    /// <summary>Policy snapshot read after the save attempt.</summary>
    public ChannelPolicy? After { get; set; }
    /// <summary>Whether Windows persisted at least one requested change.</summary>
    public bool Changed => AppliedProperties.Count > 0;
}
