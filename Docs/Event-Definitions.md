# Custom event definitions

An EventViewerX event definition describes what an event means, not merely
where it is stored. It owns one or more source channels, event IDs, optional
providers, and the named fields projected from each matching event. The same
JSON works with the C# engine, `Get-EVXEvent`, `Show-EVXEvent`,
`Start-EVXWatcher`, Windows Event Collector subscription generation, and the
portable `evx.exe` host.

```json
{
  "Name": "ServiceStartTypeChange",
  "DisplayName": "Service start-type changes",
  "Category": "Services",
  "Sources": [
    {
      "LogName": "System",
      "EventIds": [ 7040 ],
      "ProviderNames": [ "Service Control Manager" ]
    }
  ],
  "Fields": [
    { "Name": "ServiceName", "DisplayName": "Service", "Source": "Data", "SourceName": "param1" },
    { "Name": "OldStartType", "DisplayName": "Previous start type", "Source": "Data", "SourceName": "param2" },
    { "Name": "NewStartType", "DisplayName": "New start type", "Source": "Data", "SourceName": "param3" },
    { "Name": "Computer", "Source": "Metadata", "SourceName": "SourceComputer" }
  ]
}
```

`Source` is `Data`, `MessageData`, `Metadata`, `Message`, or `Constant`.
`SourceName` identifies the EventData key, parsed message field, EventObject
property, or constant value. `DefaultValue` is optional. Names must be unique,
event IDs must be positive, and every declared provider and channel must be
non-empty. Fields keep their declared order in typed output. Optional
`DisplayName` controls the HTML, Excel, and email heading without changing the
stable field name used by C# and PowerShell.

## PowerShell

```powershell
# Direct, remote, or offline query. A definition replaces -LogName.
Get-EVXEvent -Definition .\ServiceChanges.json `
    -MachineName SRV01, SRV02 -TimePeriod Last24Hours -MaxEvents 500

Get-EVXEvent -Definition .\ServiceChanges.json `
    -Path C:\Logs\System.evtx -Oldest

# One query, several presentation formats.
Show-EVXEvent -Definition .\ServiceChanges.json `
    -HtmlPath .\ServiceChanges.html `
    -ExcelPath .\ServiceChanges.xlsx

# Real-time typed projection without a new cmdlet.
$watcher = Start-EVXWatcher -Definition .\ServiceChanges.json `
    -Action { $_ | ConvertTo-Json -Depth 5 }

# The same definition becomes a collector subscription query.
New-EVXCollectorSubscription -Name ServiceChanges `
    -SourceComputer SRV01, SRV02 `
    -Definition .\ServiceChanges.json |
    Set-EVXCollectorSubscription -Confirm:$false
```

Use `-Collector WEC01` instead of `-MachineName` after the source computers
are forwarding. EventViewerX reads `ForwardedEvents` while matching the
definition's original channel, provider, and event IDs. `-Path` is likewise a
container override; neither option changes the definition's semantics.

A custom definition produces only its declared fields in the primary report
table and worksheet. Excel stores event ID, provider, channel, record ID, and
raw message in `Event Provenance` instead of mixing transport metadata into the
domain schema.

## C#

```csharp
EventDefinition definition = EventDefinition.Load("ServiceChanges.json");
var query = new EventDefinitionQuery(definition) {
    MachineNames = new string?[] { "SRV01", "SRV02" },
    TimePeriod = TimePeriod.Last24Hours,
    MaxEvents = 500,
    MaxCandidates = 10_000
};

var execution = new EventDefinitionQueryExecutionInfo();
await foreach (CustomEventRecord record in
               EventDefinitionEngine.ReadAsync(query, execution)) {
    Console.WriteLine($"{record.TypeName}: {record.Values["ServiceName"]}");
}
```

`MaxEvents` counts accepted custom records. `MaxCandidates` separately limits
raw matching source records, and `ScanLimitReached` reports when another
candidate existed beyond that cap. The query also supports record IDs,
checkpoints through observers, message culture, bookmarks, cancellation,
bounded remote reads, and isolated remote-target failures.

## Portable host

```powershell
evx query --definition .\ServiceChanges.json --machine SRV01 --since 1.00:00:00
evx report --definition .\ServiceChanges.json --collector WEC01 `
    --html .\ServiceChanges.html --excel .\ServiceChanges.xlsx
evx watch --definition .\ServiceChanges.json --collector WEC01 `
    --jsonl .\ServiceChanges.jsonl --stop-after 100
```

Definitions are data and should be reviewed and versioned like code. They do
not install a provider or change the source machine. To create a new native
manifest provider and strongly typed Windows Event Log schema, use the
[custom provider guide](Custom-Providers.md).
