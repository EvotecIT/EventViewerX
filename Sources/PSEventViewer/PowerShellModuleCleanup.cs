using System;
using System.Collections.Concurrent;
using System.Management.Automation;

namespace PSEventViewer;

/// <summary>Runs exact-owner cleanup when the PowerShell script module is removed.</summary>
public static class PowerShellModuleCleanup {
    private static readonly ConcurrentDictionary<Guid, CleanupRegistration> Registrations = new();

    internal static void Register(PSModuleInfo module, Guid runspaceId, Guid ownerId) {
        if (module == null) {
            throw new ArgumentNullException(nameof(module));
        }

        Guid registrationId = Guid.NewGuid();
        var registration = new CleanupRegistration(module.OnRemove);
        Registrations[registrationId] = registration;
        try {
            module.OnRemove = ScriptBlock.Create(
                $"[PSEventViewer.PowerShellModuleCleanup]::Run(" +
                $"[guid]'{runspaceId:D}', [guid]'{ownerId:D}', [guid]'{registrationId:D}')");
        } catch {
            Registrations.TryRemove(registrationId, out _);
            throw;
        }
    }

    /// <summary>Stops resources owned by one module instance and invokes any previous removal callback.</summary>
    /// <param name="runspaceId">Runspace that created the module-owned resources.</param>
    /// <param name="ownerId">Exact module resource owner to remove.</param>
    /// <param name="registrationId">Removal callback registration whose previous callback should be invoked.</param>
    public static void Run(Guid runspaceId, Guid ownerId, Guid registrationId) {
        PowerShellWatcherRegistry.StopAndRemoveOwner(runspaceId, ownerId);
        if (Registrations.TryRemove(registrationId, out CleanupRegistration? registration) &&
            registration.PreviousOnRemove != null) {
            registration.PreviousOnRemove.Invoke();
        }
    }

    private sealed class CleanupRegistration {
        internal CleanupRegistration(ScriptBlock? previousOnRemove) {
            PreviousOnRemove = previousOnRemove;
        }

        internal ScriptBlock? PreviousOnRemove { get; }
    }
}
