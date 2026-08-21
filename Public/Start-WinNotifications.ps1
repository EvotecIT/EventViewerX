function Start-WinNotifications {
    [CmdletBinding()]
    param(
        [System.Collections.IDictionary] $Options,
        [System.Collections.IDictionary] $Definitions,
        [System.Collections.IDictionary] $Target,
        [int] $EventID,
        [int64] $EventRecordID,
        [string] $EventChannel
    )
    # Logger Setup
    if ($Options.Logging) {
        $LoggerParameters = $Options.Logging
    } else {
        $LoggerParameters = $Script:LoggerParameters
    }
    $Logger = Get-Logger @LoggerParameters

    $Results = @{ }

    $Logger.AddInfoRecord("Executed Trigger for ID: $eventid and RecordID: $eventRecordID")
    $Logger.AddInfoRecord("Using Microsoft Teams: $($Options.Notifications.MicrosoftTeams.Enabled)")
    if ($Options.Notifications.MicrosoftTeams.Enabled) {
        foreach ($Priority in $Options.Notifications.MicrosoftTeams.Keys | Where-Object { $_ -notcontains 'Enabled' }) {
            [string] $URI = Format-FirstXChars -Text $Options.Notifications.MicrosoftTeams.$Priority.Uri -NumberChars 50
            $Logger.AddInfoRecord("Priority: $Priority, TeamsID: $URI...")
        }
    }
    $Logger.AddInfoRecord("Using Slack: $($Options.Notifications.Slack.Enabled)")
    if ($Options.Notifications.Slack.Enabled) {
        foreach ($Priority in $Options.Notifications.Slack.Keys | Where-Object { $_ -notcontains 'Enabled' }) {
            [string] $URI = Format-FirstXChars -Text $Options.Notifications.Slack.$Priority.URI -NumberChars 25
            $Logger.AddInfoRecord("Priority: $Priority, Slack URI: $URI...")
            $Logger.AddInfoRecord("Priority: $Priority, Slack Channel: $($($Options.Notifications.Slack.$Priority.Channel))...")
        }
    }
    $Logger.AddInfoRecord("Using Discord: $($Options.Notifications.Discord.Enabled)")
    if ($Options.Notifications.Discord.Enabled) {
        foreach ($Priority in $Options.Notifications.Discord.Keys | Where-Object { $_ -notcontains 'Enabled' }) {
            [string] $URI = Format-FirstXChars -Text $Options.Notifications.Discord.$Priority.URI -NumberChars 25
            $Logger.AddInfoRecord("Priority: $Priority, Discord URI: $URI...")
        }
    }
    $Logger.AddInfoRecord("Using MSSQL: $($Options.Notifications.MSSQL.Enabled)")
    if ($Options.Notifications.MSSQL.Enabled) {
        foreach ($Priority in $Options.Notifications.MSSQL.Keys | Where-Object { $_ -notcontains 'Enabled' }) {
            $Logger.AddInfoRecord("Priority: $Priority, Server\Instance: $($Options.Notifications.MSSQL.$Priority.SqlServer)")
            $Logger.AddInfoRecord("Priority: $Priority, Database: $($Options.Notifications.MSSQL.$Priority.SqlDatabase)")
        }
    }
    $Logger.AddInfoRecord("Using Email: $($Options.Notifications.Email.Enabled)")
    if ($Options.Notifications.Email.Enabled) {
        foreach ($Priority in $Options.Notifications.Email.Keys | Where-Object { 'Enabled', 'Formatting' -notcontains $_ }) {
            $Logger.AddInfoRecord("Priority: $Priority, Email TO: $($Options.Notifications.Email.$Priority.Parameters.To), Email CC: $($Options.Notifications.Email.$Priority.Parameters.CC)")
        }
    }

    if (-not $Options.Notifications.Slack.Enabled -and
        -not $Options.Notifications.MicrosoftTeams.Enabled -and
        -not $Options.Notifications.MSSQL.Enabled -and
        -not $Options.Notifications.Discord.Enabled -and
        -not $Options.Notifications.Email.Enabled) {
        # Terminating as no options are $true
        return
    }

    [Array] $ExtendedInput = Get-ServersListLimited -Target $Target -EventID $EventID -RecordID $EventRecordID -LogName $EventChannel
    foreach ($Entry in $ExtendedInput) {
        if ($Entry.Type -eq 'Computer') {
            $Logger.AddInfoRecord("Computer $($Entry.Server) added to scan $($Entry.LogName) log for events: $($Entry.EventID -join ', ')")
        } else {
            $Logger.AddInfoRecord("File $($Entry.Server) added to scan $($Entry.LogName) log for events: $($Entry.EventID -join ', ')")
        }
    }

    $AllEvents = Get-Events -ExtendedInput $ExtendedInput -EventID $eventid -RecordID $eventRecordID -Verbose:$Options.Debug.Verbose

    # Prepare the results based on chosen criteria
    foreach ($Report in  $Definitions.Keys | Where-Object { $_ -notcontains 'Enabled' }) {
        if ($Definitions.$Report.Enabled) {
            #$ReportNameTitle = Format-AddSpaceToSentence -Text $Report -ToLowerCase
            $Logger.AddInfoRecord("Running $Report")
            $TimeExecution = Start-TimeLog
            foreach ($SubReport in $Definitions.$Report.Keys | Where-Object { $_ -notcontains 'Enabled', 'SqlExport' }) {
                if ($Definitions.$Report.$SubReport.Enabled) {
                    $Logger.AddInfoRecord("Running $Report with subsection $SubReport")
                    [string] $EventsType = $Definitions.$Report.$SubReport.LogName
                    [Array] $EventsNeeded = $Definitions.$Report.$SubReport.Events
                    #[Array] $EventsFound = Find-EventsNeeded -Events $AllEvents -EventIDs $EventsNeeded -EventsType $EventsType
                    [Array] $EventsFound = Get-EventsTranslation -Events $AllEvents -EventsDefinition $Definitions.$Report.$SubReport -EventIDs $EventsNeeded -EventsType $EventsType
                    $Logger.AddInfoRecord("Ending $Report with subsection $SubReport events found $($EventsFound.Count)")
                    $Results.$Report = $EventsFound
                }
            }
            $ElapsedTimeReport = Stop-TimeLog -Time $TimeExecution -Option OneLiner
            $Logger.AddInfoRecord("Ending $Report - Time to run $ElapsedTimeReport")
        }
    }
    [bool] $FoundPriorityEvent = $false
    foreach ($ReportName in $Definitions.Keys | Where-Object { $_ -notcontains 'Enabled', 'SqlExport', 'Priority' }) {
        if ($Results.$ReportName) {
            if ($null -ne $Definitions.$ReportName.Priority) {
                foreach ($Priority in $Definitions.$ReportName.Priority.Keys) {
                    [Array] $MyValue = Find-EventsTo -Prioritize -Events $Results.$ReportName -DataSet  $Definitions.$ReportName.Priority.$Priority
                    if ($MyValue.Count) {
                        $Logger.AddInfoRecord("Sending event with $Priority priority.")
                        Send-Notificaton -Events $MyValue -Options $Options -Priority $Priority
                        $FoundPriorityEvent = $true
                    }
                }
            }
            if (-not $FoundPriorityEvent) {
                $Logger.AddInfoRecord("Sending event with default priority.")
                Send-Notificaton -Events $Results.$ReportName -Options $Options -Priority 'Default'
            }
        }
    }

    if ($Options.Backup.Enabled) {
        Protect-ArchivedLogs -TableEventLogClearedLogs $TableEventLogClearedLogs -DestinationPath $Options.Backup.DestinationPath -Verbose:$Options.Debug.Verbose
    }
}
