Import-Module PSEventViewer -Force

$EvtxPath = 'C:\Logs\Security.evtx'
$OutputRoot = 'C:\Exports'

# Fastest byte-stable interchange path.
Export-EVXEvent `
    -Path $EvtxPath `
    -OutputPath (Join-Path $OutputRoot 'Security.xml') `
    -Format Xml `
    -Oldest `
    -Force

# Direct compiled export avoids a PowerShell object-per-event file pipeline.
Export-EVXEvent `
    -Path $EvtxPath `
    -OutputPath (Join-Path $OutputRoot 'Security.jsonl') `
    -Format JsonLines `
    -ReadMode Full `
    -MessageCulture en-US `
    -Oldest `
    -Force

# Remote CSV and JSONL are written locally with bounded buffering. Native EVTX
# export is local-only because Windows creates that file in the target session.
Export-EVXEvent `
    -LogName System `
    -MachineName DC01 `
    -OutputPath (Join-Path $OutputRoot 'DC01-System.csv') `
    -Format Csv `
    -ReadMode Message `
    -MessageCulture en-US `
    -BufferCapacity 64 `
    -Force
