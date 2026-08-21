function Find-Events {
    [CmdLetBinding(DefaultParameterSetName = 'Manual')]
    param(
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "Manual", Mandatory = $true)][DateTime] $DateFrom,
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "Manual", Mandatory = $true)][DateTime] $DateTo,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange", Mandatory = $false)][alias('Server', 'ComputerName')][string[]] $Servers = $Env:COMPUTERNAME,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange", Mandatory = $false)][alias('RunAgainstDC')][switch] $DetectDC,
        [ValidateNotNull()]
        [alias('Credentials')][System.Management.Automation.PSCredential]
        [System.Management.Automation.Credential()]$Credential = [System.Management.Automation.PSCredential]::Empty,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][switch] $Quiet,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][System.Collections.IDictionary] $LoggerParameters,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][switch] $ExtentedOutput,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][string] $Who,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][string] $Whom,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][string] $NotWho,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")][string] $NotWhom,
        [parameter(ParameterSetName = "Extended", Mandatory = $true)][System.Collections.IDictionary] $Definitions,
        [parameter(ParameterSetName = "Extended", Mandatory = $true)][System.Collections.IDictionary] $Times,
        [parameter(ParameterSetName = "Extended", Mandatory = $true)][System.Collections.IDictionary] $Target,
        [parameter(ParameterSetName = "Extended", Mandatory = $false)][int] $EventID,
        [parameter(ParameterSetName = "Extended", Mandatory = $false)][int64] $EventRecordID,
        [parameter(ParameterSetName = "Manual")]
        [parameter(ParameterSetName = "DateManual")]
        [parameter(ParameterSetName = "DateRange")]
        [ValidateScript( { $_ -in ( & $ReportScriptBlock ) } )][string[]] $Report,
        [parameter(ParameterSetName = "DateRange")]
        [ValidateScript( { $_ -in ( & $DatesRangeScriptBlock  ) })][string] $DatesRange
    )
    Process {
        # This prevents duplication of reports on second script run
        foreach ($R in $Script:ReportDefinitions.Keys) {
            $Script:ReportDefinitions[$R].Enabled = $false
        }
        foreach ($Time in $Script:ReportTimes.Keys) {
            $Script:ReportTimes[$Time].Enabled = $false
        }
        #-NamedDataFilter @{'SubjectUserName' = $User; 'TargetUserName' = $User }

        $ExecutionTime = Start-TimeLog
        ## Logging / Display to screen
        if (-not $LoggerParameters) {
            $LoggerParameters = $Script:LoggerParameters
        }
        $Logger = Get-Logger @LoggerParameters

        if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent) { $Verbose = $true } else { $Verbose = $false }

        if ($null -ne $Definitions -and $null -ne $Times -and $null -ne $Target) {
            # Using in case of Extendted Input - Mateusz asked for it.
            $Dates = Get-ChoosenDates -ReportTimes $Times
            if (-not $Dates) {
                $Logger.AddErrorRecord("Not a single date was choosen for scan. Please fix Times and try again.")
                return
            }
            if ($Dates -is [Array]) {
                $Logger.AddErrorRecord("Currently only 1 date range is supported. Please fix Times and try again.")
                return
            }
            # Fixes Reports variable
            $Reports = foreach ($R in $Definitions.Keys) {
                if ($Definitions[$R].Enabled -eq $true) {
                    $R
                }
            }

        } else {
            # Using standard case
            $Reports = $PSBoundParameters.Report
            #$DatesRange = $PSBoundParameters.DatesRange

            if (-not $Quiet) { $Logger.AddInfoRecord("Preparing reports: $($Reports -join ',')") }

            # Bring defaults
            $Definitions = $Script:ReportDefinitions

            # Set Times
            $Times = $Script:ReportTimes
            if ($DatesRange) {
                $Times.$DatesRange.Enabled = $true
            } elseif ($DateFrom -and $DateTo) {
                $Times.CustomDate.Enabled = $true
                $Times.CustomDate.DateFrom = $DateFrom
                $Times.CustomDate.DateTo = $DateTo
            } else {
                return
            }

            # Fixes ReportTimes
            $Dates = Get-ChoosenDates -ReportTimes $Times
            if (-not $Dates) {
                $Logger.AddErrorRecord("Not a single date was choosen for scan. Please fix Times and try again.")
                return
            }
            if ($Dates -is [Array]) {
                $Logger.AddErrorRecord("Currently only 1 date range is supported. Please fix Times and try again")
                return
            }
            # Fixes Definitions
            foreach ($R in $Reports) {
                $Definitions[$R].Enabled = $true
            }

            $Target = New-TargetServers -Servers $Servers -UseDC:$DetectDC
        }

        # Real deal
        if ($EventRecordID -ne 0 -and $EventID -ne 0) {
            [Array] $ExtendedInput = Get-ServersListLimited -Target $Target -Definitions $Definitions -EventID $EventID -RecordID $EventRecordID -Credential $Credential -Quiet:$Quiet -Who $Who -Whom $Whom -NotWho $NotWho -NotWhom $NotWhom
        } else {
            if ($Credential) {
                [Array] $ExtendedInput = Get-ServersList -Credential $Credential -Definitions $Definitions -Target $Target -Dates $Dates -Quiet:$Quiet -Who $Who -Whom $Whom -NotWho $NotWho -NotWhom $NotWhom
            } else {
                [Array] $ExtendedInput = Get-ServersList -Definitions $Definitions -Target $Target -Dates $Dates -Quiet:$Quiet -Who $Who -Whom $Whom -NotWho $NotWho -NotWhom $NotWhom
            }
        }
        if (-not $ExtendedInput) {
            $Logger.AddErrorRecord("There are no logs/servers to scan. Please fix Targets and try again.")
            return
        }
        foreach ($Entry in $ExtendedInput) {
            if ($Entry.Type -eq 'Computer') {
                if (-not $Quiet) { $Logger.AddInfoRecord("Computer $($Entry.Server) added to scan $($Entry.LogName) log for events: $($Entry.EventID -join ', ')") }
            } else {
                if (-not $Quiet) { $Logger.AddInfoRecord("File $($Entry.Server) added to scan $($Entry.LogName) log for events: $($Entry.EventID -join ', ')") }
            }
        }
        if (-not $Quiet) { $Logger.AddInfoRecord("Getting events for dates $($Dates.DateFrom.ToString()) to $($Dates.DateTo.ToString())") }
        # Scan all events and get everything at once
        $SplatEvents = @{
            Verbose       = $Verbose
            ExtendedInput = $ExtendedInput
            ErrorVariable = 'AllErrors'
            ErrorAction   = 'SilentlyContinue'
        }
        if ($EventRecordID -ne 0 -and $EventId -ne 0) {
            $SplatEvents.RecordID = $EventRecordID
            $SplatEvents.ID = $EventID
        }
        if ($Credential -ne [System.Management.Automation.PSCredential]::Empty) {
            $SplatEvents.Credential = $Credential
        }
        [Array] $AllEvents = Get-Events @SplatEvents

        foreach ($MyError in $AllErrors) {
            if (-not $Quiet) { $Logger.AddErrorRecord("Server $MyError") }
        }
        $Elapsed = Stop-TimeLog -Time $ExecutionTime -Option OneLiner
        if (-not $Quiet) { $Logger.AddInfoRecord("Events scanned found $($AllEvents.Count) - Time elapsed: $Elapsed") }

        $Results = Get-EventsOutput -Definitions $Definitions -AllEvents $AllEvents -Quiet:$Quiet
        if ($Results.Count -eq 1) {
            # if there is only one report to return, return Array
            $Results[$Reports]
        } else {
            # If there is more than one, return hashtable
            $Results
        }
        # This prevents duplication of reports on second script run
        foreach ($R in $Script:ReportDefinitions.Keys) {
            $Script:ReportDefinitions[$R].Enabled = $false
        }
        foreach ($Time in $Script:ReportTimes.Keys) {
            $Script:ReportTimes[$Time].Enabled = $false
        }
    }
}

$ReportScriptBlock = {
    (Get-EventsDefinitions).Keys
}
$DatesRangeScriptBlock = {
    (Get-DatesDefinitions -Skip 'CustomDate', 'CurrentDayMinuxDaysX', 'CurrentDayMinusDayX', 'OnDay')
}

Register-ArgumentCompleter -CommandName Find-Events -ParameterName Report -ScriptBlock $ReportScriptBlock
Register-ArgumentCompleter -CommandName Find-Events -ParameterName DatesRange -ScriptBlock $DatesRangeScriptBlock
