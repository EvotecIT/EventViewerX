namespace EventViewerX;

public sealed partial class EventDiagnosticCode {
    private static void ApplyKnownMeaning(EventDiagnosticCode result) {
        if (!result.Value.HasValue) { return; }
        var meaning = result.Kind switch {
            EventDiagnosticCodeKind.Win32 => Win32Meaning(result.Value.Value),
            EventDiagnosticCodeKind.WindowsInstaller => InstallerMeaning(result.Value.Value),
            EventDiagnosticCodeKind.HResult when result.Win32Value.HasValue => Win32Meaning(result.Win32Value.Value),
            EventDiagnosticCodeKind.HResult => HResultMeaning(result.Value.Value),
            _ => null
        };
        if (!meaning.HasValue) { return; }
        result.SymbolicName = meaning.Value.Symbol;
        result.Explanation = meaning.Value.Explanation;
        result.NextCheck = meaning.Value.NextCheck;
        if (result.Kind == EventDiagnosticCodeKind.HResult && result.Win32Value.HasValue) {
            result.SymbolicName = "HRESULT_FROM_WIN32(" + result.SymbolicName + ")";
            result.Explanation = "The HRESULT reports failure with an embedded Win32 status. " + result.Explanation;
            result.Reference = "https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-";
        } else if (result.Kind == EventDiagnosticCodeKind.Win32) {
            result.Reference = "https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-";
        } else if (result.Kind == EventDiagnosticCodeKind.HResult) {
            result.Reference = result.Value.Value <= 1
                ? "https://learn.microsoft.com/en-us/windows/win32/learnwin32/error-handling-in-com"
                : "https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1";
        }
    }

    // These small deterministic descriptions are authored here from the documented API contracts.
    // They do not use the collector's locale, executable return-code guesses, or an external catalog.
    private static (string Symbol, string Explanation, string NextCheck)? Win32Meaning(uint value) => value switch {
        0 => ("ERROR_SUCCESS", "The operation returned success.", "Verify the operation's resulting state; this status does not establish a later workflow outcome."),
        2 => ("ERROR_FILE_NOT_FOUND", "The requested file was not found.", "Check the captured path and whether the file was present for the identity running the operation."),
        3 => ("ERROR_PATH_NOT_FOUND", "The requested path was not found.", "Check each directory segment and any unavailable share or mapped drive in the operation's execution context."),
        5 => ("ERROR_ACCESS_DENIED", "The operation was denied access.", "Check the failing operation, its target and the effective identity's required permissions; this code alone does not identify the denied resource."),
        32 => ("ERROR_SHARING_VIOLATION", "An incompatible open handle prevented access to the file.", "Inspect the file path, open handles and requested sharing mode at the failure time."),
        53 => ("ERROR_BAD_NETPATH", "The network path could not be reached.", "Check the recorded server and share, name resolution and connectivity from the affected execution context."),
        67 => ("ERROR_BAD_NET_NAME", "The requested network name was unavailable.", "Check the exact share name and its availability; distinguish a missing share from an authentication failure."),
        87 => ("ERROR_INVALID_PARAMETER", "The API rejected a parameter.", "Inspect the emitting API's argument contract and captured argument values."),
        112 => ("ERROR_DISK_FULL", "The destination has insufficient free space for the operation.", "Check space on the actual destination and temporary staging volumes used by the operation."),
        121 => ("ERROR_SEM_TIMEOUT", "The operation reached a semaphore timeout.", "Inspect the emitting API, its timeout budget and preceding storage or network evidence; the number does not identify which dependency stalled."),
        122 => ("ERROR_INSUFFICIENT_BUFFER", "The supplied buffer was too small.", "Check whether the caller retried with the size returned by the API before treating this status as terminal."),
        126 => ("ERROR_MOD_NOT_FOUND", "A required module could not be located.", "Check the requested module and its dependencies, loader search paths and execution architecture."),
        193 => ("ERROR_BAD_EXE_FORMAT", "The loader rejected the executable format.", "Check the file's format, integrity and compatibility with the process architecture."),
        267 => ("ERROR_DIRECTORY", "The API rejected a directory name.", "Check the exact directory argument and the emitting API's path and permission prerequisites."),
        _ => null
    };

    private static (string Symbol, string Explanation, string NextCheck)? InstallerMeaning(uint value) => value switch {
        0 => ("ERROR_SUCCESS", "Windows Installer returned success.", "Inspect the application's detection result and accepted reporting before concluding that deployment is complete."),
        1601 => ("ERROR_INSTALL_SERVICE_FAILURE", "Windows Installer could not use its service.", "Inspect the Windows Installer service state and contemporaneous Application events."),
        1602 => ("ERROR_INSTALL_USEREXIT", "The installation was cancelled by the user.", "Inspect the installer interaction and cancellation records for the affected execution context."),
        1603 => ("ERROR_INSTALL_FAILURE", "Windows Installer reported an installation failure without identifying its cause.", "Find the failing action and preceding errors in the verbose installer log; compare the configured return-code mapping."),
        1605 => ("ERROR_UNKNOWN_PRODUCT", "Windows Installer does not recognize the product for this operation.", "Check the product identity, installed version and per-user or per-machine installation context."),
        1612 => ("ERROR_INSTALL_SOURCE_ABSENT", "The required installation source was unavailable.", "Check the original package source, its accessibility and the installed product's repair or removal requirements."),
        1618 => ("ERROR_INSTALL_ALREADY_RUNNING", "Another Windows Installer transaction is active.", "Inspect the overlapping installer and its completion before evaluating retry policy."),
        1619 => ("ERROR_INSTALL_PACKAGE_OPEN_FAILED", "Windows Installer could not open the package.", "Check package existence, access and the captured command's package path."),
        1620 => ("ERROR_INSTALL_PACKAGE_INVALID", "Windows Installer rejected the package as invalid.", "Check the package's integrity and format; retain the failing package and installer log for comparison."),
        1622 => ("ERROR_INSTALL_LOG_FAILURE", "Windows Installer could not open its log.", "Check the requested log directory, file access and space available to the installer identity."),
        1625 => ("ERROR_INSTALL_PACKAGE_REJECTED", "Installation was blocked by policy.", "Inspect the applicable installation policy and the product's approval requirements; do not infer a policy change from the code alone."),
        1638 => ("ERROR_PRODUCT_VERSION", "Another version of the product prevents this installation.", "Compare installed product identity and version with the package's supported upgrade path."),
        1641 => ("ERROR_SUCCESS_REBOOT_INITIATED", "Windows Installer succeeded and initiated a restart.", "Inspect reboot policy and post-restart detection before concluding that deployment is complete."),
        3010 => ("ERROR_SUCCESS_REBOOT_REQUIRED", "Windows Installer succeeded and requires a restart.", "Inspect reboot policy and post-restart detection before concluding that deployment is complete."),
        _ => null
    };

    private static (string Symbol, string Explanation, string NextCheck)? HResultMeaning(uint value) => value switch {
        0 => ("S_OK", "The API returned HRESULT success.", "Verify the result required by the API and the surrounding workflow."),
        1 => ("S_FALSE", "The API returned a successful HRESULT with an API-specific false result.", "Inspect the API's S_FALSE contract; do not substitute S_OK semantics."),
        0x80004004 => ("E_ABORT", "The operation reported an abort.", "Inspect the operation's cancellation or abort evidence and the preceding diagnostic records."),
        0x80004005 => ("E_FAIL", "The API reported an unspecified failure.", "Inspect the failing API and preceding records; this generic value does not identify a root cause."),
        _ => null
    };
}
