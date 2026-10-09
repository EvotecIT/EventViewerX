---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXDiagnostic
## SYNOPSIS
Reads one bounded captured-log batch or interprets a diagnostic code in its declared context.

## SYNTAX
### Log (Default)
```powershell
Get-EVXDiagnostic [-Path] <string> [-Checkpoint <EventDiagnosticCheckpoint>] [-Options <EventDiagnosticReadOptions>] [<CommonParameters>]
```

### Code
```powershell
Get-EVXDiagnostic -Code <string> [-Kind <EventDiagnosticCodeKind>] [<CommonParameters>]
```

## DESCRIPTION
Checkpoints preserve complete-frame boundaries. The command never executes a collection command or changes the endpoint.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXDiagnostic -Path 'C:\Path'
```


### EXAMPLE 2
```powershell
Get-EVXDiagnostic -Code 'Value'
```


## PARAMETERS

### -Checkpoint
Previous batch restart position.

```yaml
Type: EventDiagnosticCheckpoint
Parameter Sets: Log
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Code
Signed decimal, unsigned decimal, or 0x-prefixed hexadecimal number.

```yaml
Type: String
Parameter Sets: Code
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind
Emitting API family. Unknown preserves the number without guessing an outcome.

```yaml
Type: EventDiagnosticCodeKind
Parameter Sets: Code
Aliases: None
Possible values: Unknown, Win32, HResult, WindowsInstaller, ProcessExit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
Finite bounds, declared writer offset, and final-file behavior.

```yaml
Type: EventDiagnosticReadOptions
Parameter Sets: Log
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Log file to read, using UTF-8 or BOM-marked UTF-16.

```yaml
Type: String
Parameter Sets: Log
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.EventDiagnosticReadResult`
- `EventViewerX.EventDiagnosticCode`

## RELATED LINKS

- None
