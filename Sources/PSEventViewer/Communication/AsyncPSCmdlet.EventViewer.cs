using System;
using System.Threading;
using EventViewerX;

namespace PSEventViewer;

public abstract partial class AsyncPSCmdlet {
    private InternalLogger? _eventViewerLogger;
    private Guid _powerShellResourceOwnerId;

#if NET8_0_OR_GREATER
    /// <summary>Stopping token compatible with newer PowerShell builds.</summary>
    protected CancellationToken StoppingToken => CancelToken;
#endif

    /// <summary>Gets the module instance that owns resources created by this invocation.</summary>
    protected Guid PowerShellResourceOwnerId => _powerShellResourceOwnerId;

    /// <summary>Keeps the EventViewerX logger attached across PowerShell lifecycle phases.</summary>
    protected void SetEventViewerLogger(InternalLogger logger) {
        _eventViewerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        Settings.Logger = logger;
    }
}
