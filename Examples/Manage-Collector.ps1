Import-Module PSEventViewer -Force

# Inventory can target remote collectors. Readiness, runtime, and updates are
# local-only because the Windows Event Collector APIs have no remote session.
Get-EVXCollectorSubscription -Readiness

# Build a source-initiated definition without hand-authoring WEC XML, its
# source authorization SDDL, or the SubscriptionManager URI.
$DomainControllersSid = (Get-ADGroup 'Domain Controllers').SID.Value
$Definition = New-EVXCollectorSubscription `
    -Name 'Domain controller authentication' `
    -SubscriptionType SourceInitiated `
    -CollectorHostName WEC01.ad.contoso.com `
    -AllowedSourceSid $DomainControllersSid `
    -Type ActiveDirectoryAuthentication `
    -Description 'Typed authentication events from domain controllers' `
    -Enabled $false

# Deploy this value through the Event Forwarding SubscriptionManager computer
# policy on the source domain controllers.
$Definition.SourceSubscriptionManagerValue

# Applying a definition and one-time collector initialization remain explicit
# and support WhatIf/Confirm.
$Definition | Set-EVXCollectorSubscription `
    -InitializeCollector -Confirm:$false

Get-EVXCollectorSubscription `
    -Name $Definition.SubscriptionId `
    -IncludeRuntimeStatus

Set-EVXCollectorSubscription `
    -Name $Definition.SubscriptionId `
    -Enabled $true `
    -Confirm:$false
