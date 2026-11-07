# Workstation health events

The Windows rules expose application faults, service failures, storage and
hardware errors, display-driver recovery, and boot or shutdown performance as
typed records. They use provider payloads rather than localized message text.
Every rule qualifies its event IDs by provider and original channel.

```csharp
using EventViewerX;
using EventViewerX.Rules.Windows;

var query = new EventTypeQuery(new[] {
    EventType.ApplicationCrash,
    EventType.ApplicationHang,
    EventType.ServiceFailure,
    EventType.DiskError,
    EventType.FileSystemError,
    EventType.StorageReset,
    EventType.HardwareError,
    EventType.DisplayDriverReset,
    EventType.BootPerformance,
    EventType.ShutdownPerformance,
    EventType.StartupDegradation,
    EventType.ShutdownDegradation
}) { MaxEvents = 100 };

await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
    if (record is ApplicationCrash crash) {
        Console.WriteLine($"{crash.ApplicationName}: {crash.ExceptionCode} in {crash.ModuleName}");
    } else if (record is ServiceFailure service) {
        Console.WriteLine($"{service.ServiceName}: {service.FailureKind}");
    }
}
```

All records retain the original event through `SourceEvent`, along with common
computer, provider, event ID and time metadata. Duration fields are nullable
millisecond counts; an absent value remains unknown. `When` is the event's
recording time. Performance rules expose the provider's earlier incident time
separately as `IncidentTimestampUtc`.

| Rule | Provider / channel | Event IDs | Projected details |
| --- | --- | --- | --- |
| `ApplicationCrash` | Application Error / Application | 1000 | Application name/version/path, faulting module name/version/path, exception code, fault offset, report ID |
| `ApplicationHang` | Application Hang / Application | 1002 | Application name/version/path, hang type, report ID |
| `ServiceFailure` | Service Control Manager / System | 7000, 7009, 7023, 7031, 7034 | Service display name, failure kind, error token, failure count, connection timeout, announced recovery action/delay |
| `DiskError` | disk / System | 7, 51, 153 | Device path; logical block address and disk number for retried I/O |
| `FileSystemError` | Ntfs or Microsoft-Windows-Ntfs / System | 55, actionable 98 | Volume/device, corruption action state, corruption description, outcome |
| `StorageReset` | stornvme / System | 129 | Storage port/device path |
| `HardwareError` | Microsoft-Windows-WHEA-Logger / System | 17, 18, 19, 47 | Component, corrected/fatal flags, error source/type, processor APIC ID, primary PCI device name or physical address |
| `DisplayDriverReset` | Display / System | 4101 | Recovered display-driver name |
| `BootPerformance` | Microsoft-Windows-Diagnostics-Performance / Operational | 100 | Total, main-path and post-boot durations, degradation flag, boot start time |
| `ShutdownPerformance` | Microsoft-Windows-Diagnostics-Performance / Operational | 200 | Total and service shutdown durations, degradation flag, shutdown start time |
| `StartupDegradation` | Microsoft-Windows-Diagnostics-Performance / Operational | 101–110 | Component category/name, optional friendly name/version/path, total and additional duration, incident time |
| `ShutdownDegradation` | Microsoft-Windows-Diagnostics-Performance / Operational | 203 | Delaying service name and optional identity fields, total and additional duration, incident time |

The full performance channel name is
`Microsoft-Windows-Diagnostics-Performance/Operational`.

Application Hang 1002 does not provide a faulting module or exception code.
SCM 7031 announces a recovery action; it does not confirm that a service restarted.
SCM 7023 describes termination with an error, while 7000 and 7009 describe start
failure and connection timeout. Error values such as `%%1053` are retained as
provider tokens rather than translated using the reader's machine.

NTFS 98 with `CorruptionActionState = 0` reports a healthy volume and is excluded.
Event 98 is accepted only with a positive action state. Disk 153 describes retried
I/O; it does not establish that a disk failed. NVMe 129 can identify a RAID port
without identifying a particular disk or volume.

WHEA 17, 19 and 47 are corrected errors; event 18 is a fatal processor error.
Display 4101 confirms that Windows recovered the driver. Vendor-specific
`nvlddmkm` 13, 14 and 153 are not treated as equivalent recovery events: no such
records were available in the inspected workstation logs to qualify their payloads.

The existing `OSUncleanShutdown` rule continues to handle Kernel-Power 41.
Existing update, Defender, BitLocker, task, firewall and device rules remain
available alongside these health rules. The built-in static catalog and native
smoke include all of the rules above; they require no reflection discovery in
trimmed or Native AOT consumers.
