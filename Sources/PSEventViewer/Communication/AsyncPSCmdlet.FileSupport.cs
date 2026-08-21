using System;
using System.IO;
using System.Management.Automation;

namespace PSEventViewer;

public abstract partial class AsyncPSCmdlet {
    /// <summary>Returns the effective error action preference.</summary>
    protected ActionPreference GetErrorActionPreference() {
        ActionPreference preference =
            (ActionPreference)SessionState.PSVariable.GetValue("ErrorActionPreference");
        if (MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
            string? errorActionString = MyInvocation.BoundParameters["ErrorAction"]?.ToString();
            if (!string.IsNullOrWhiteSpace(errorActionString) &&
                Enum.TryParse(errorActionString, true, out ActionPreference parsed)) {
                preference = parsed;
            }
        }

        return preference;
    }

    /// <summary>Verifies that the specified file exists.</summary>
    protected bool EnsureFileExists(string path, ActionPreference errorAction) {
        if (File.Exists(path)) {
            return true;
        }

        string message = $"{MyInvocation.InvocationName} - The specified file does not exist: {path}";
        if (errorAction == ActionPreference.Stop) {
            FileNotFoundException exception = new("The specified file does not exist.", path);
            ThrowTerminatingError(
                new ErrorRecord(
                    exception,
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    path));
        } else {
            WriteWarning(message);
        }

        return false;
    }
}
