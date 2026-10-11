# Create an editable incident brief from an event snapshot

Use `Show-EVXEvent` for EventViewerX's native HTML, Excel, CSV and transport-neutral email reports. When the handoff also needs an editable Word brief and PDF, pass a bounded `Get-EVXEvent` snapshot to [PSWriteOffice's incident-brief recipe](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/Recipe-PSEventViewer-OfficeReport.ps1).

Install the two modules, then run the recipe from a PSWriteOffice checkout:

```powershell
Install-Module PSEventViewer, PSWriteOffice -Scope CurrentUser

$events = @(Get-EVXEvent -LogName Application -Level 1,2,3 -TimePeriod Last24Hours -MaxEvents 100 -ReadMode Message)
./Examples/Integrations/Recipe-PSEventViewer-OfficeReport.ps1 -Event $events -MaxEvents 100 -OutputDirectory ./Output/EventBrief
```

The query runs once. The Word/PDF brief shows the retained record count and top eight providers, with separate critical, error and warning counts. Excel retains every supplied record up to `MaxEvents`, including its timestamp, event ID, provider, numeric level and full message. Empty input creates an explicit empty report rather than inventing a placeholder event. Timestamps retain the values returned by the reader.

For an offline demonstration, omit `-Event`; the recipe uses a small synthetic incident exercise. For a direct local query, use `-CollectLive -LogName Application`. Live collection requires Windows and access to the selected channel. The recipe uses PSEventViewer 4.0.1 and PSWriteOffice 3.0.8 or later.

The [integration guide](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/README.md) also shows how to attach the resulting PDF and XLSX to a local EML using Mailozaurr. EventViewerX owns collection, PSWriteOffice owns the document composition, and Mailozaurr owns message packaging or delivery. Review full event messages before distributing the register.
