---
external help file: PSEventViewer-help.xml
Module Name: PSEventViewer
online version: https://github.com/EvotecIT/EventViewerX
schema: 2.0.0
---
# Get-EVXTarget
## SYNOPSIS
Discovers local or explicitly selected Active Directory event targets.

Returns the local machine by default. Current-domain, current-forest, named-domain, named-forest, and trusted-forest discovery are opt-in and preserve per-domain successes and failures.

## SYNTAX
### __AllParameterSets
```powershell
Get-EVXTarget [[-ActiveDirectory] <EventTargetDiscoveryScope>] [-Name <string>] [-IncludeTrustedForests] [-Credential <pscredential>] [-TimeoutMs <int>] [-MaximumDomainCount <int>] [-MaximumTargetCount <int>] [<CommonParameters>]
```

## DESCRIPTION
Discovers local or explicitly selected Active Directory event targets.

Returns the local machine by default. Current-domain, current-forest, named-domain, named-forest, and trusted-forest discovery are opt-in and preserve per-domain successes and failures.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EVXTarget
```

No Active Directory discovery occurs.

### EXAMPLE 2
```powershell
Get-EVXTarget -ActiveDirectory CurrentForest
```

Returns one bounded result with domain controllers and any per-domain failures.

### EXAMPLE 3
```powershell
Get-EVXTarget -ActiveDirectory Domain -Name ad.example.com -Credential (Get-Credential)
```

The credential is used only for this explicitly named directory scope.

## PARAMETERS

### -ActiveDirectory
Explicit discovery scope. The default is LocalMachine.

```yaml
Type: EventTargetDiscoveryScope
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: LocalMachine, CurrentDomain, CurrentForest, Domain, Forest

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credential used only for a named domain or forest.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeTrustedForests
Traverses forest trusts only when forest discovery was explicitly selected.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumDomainCount
Maximum domains retained by one explicit discovery.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumTargetCount
Maximum distinct event-log targets retained by one explicit discovery.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
DNS name required by Domain or Forest scope.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutMs
Total discovery budget in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `EventViewerX.EventTargetDiscoveryResult`

## RELATED LINKS

- None
