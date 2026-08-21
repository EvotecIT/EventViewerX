$Script:ScriptBlock = {
    Param (
        [string]$Comp,
        [ValidateNotNull()]
        [alias('Credentials')][System.Management.Automation.PSCredential]
        [System.Management.Automation.Credential()]$Credential = [System.Management.Automation.PSCredential]::Empty,
        [hashtable]$EventFilter,
        [int]$MaxEvents,
        [bool] $Oldest,
        [bool] $Verbose
    )
    if ($Verbose) {
        $VerbosePreference = 'continue'
    }
    function Get-EventsFilter {
        <#
        .SYNOPSIS
        This function generates an xpath filter that can be used with the -FilterXPath
        parameter of Get-WinEvent.  It may also be used inside the <Select></Select tags
        of a Custom View in Event Viewer.
        .DESCRIPTION
        This function generates an xpath filter that can be used with the -FilterXPath
        parameter of Get-WinEvent.  It may also be used inside the <Select></Select tags
        of a Custom View in Event Viewer.

        This function allows for the create of xpath which can select events based on
        many properties of the event including those of named data nodes in the event's
        XML.

        XPath is case sensetive and the data passed to the parameters here must
        match the case of the data in the event's XML.
        .NOTES
        Original Code by https://community.spiceworks.com/scripts/show/3238-powershell-xpath-generator-for-windows-events
        Extended by Justin Grote
        Extended by Przemyslaw Klys
        .LINK

        .PARAMETER ID
        This parameter accepts and array of event ids to include in the xpath filter.
        .PARAMETER StartTime
        This parameter sets the oldest event that may be returned by the xpath.

        Please, note that the xpath time selector created here is based of of the
        time the xpath is generated.  XPath uses a time difference method to select
        events by time; that time difference being the number of milliseconds between
        the time and now.
        .PARAMETER EndTime
        This parameter sets the newest event that may be returned by the xpath.

        Please, note that the xpath time selector created here is based of of the
        time the xpath is generated.  XPath uses a time difference method to select
        events by time; that time difference being the number of milliseconds between
        the time and now.
        .PARAMETER Data
        This parameter will accept an array of values that may be found in the data
        section of the event's XML.
        .PARAMETER ProviderName
        This parameter will accept an array of values that select events from event
        providers.
        .PARAMETER Level
        This parameter will accept an array of values that specify the severity
        rating of the events to be returned.

        It accepts the following values.

        'Critical',
        'Error',
        'Informational',
        'LogAlways',
        'Verbose',
        'Warning'
        .PARAMETER Keywords
        This parameter accepts and array of long integer keywords. You must
        pass this parameter the long integer value of the keywords you want
        to search and not the keyword description.
        .PARAMETER UserID
        This parameter will accept an array of SIDs or domain accounts.
        .PARAMETER NamedDataFilter
        This parameter will accept and array of hashtables that define the key
        value pairs for which you want to filter against the event's named data
        fields.

        Key values, as with XPath filters, are case sensetive.

        You may assign an array as the value of any key. This will search
        for events where any of the values are present in that particular
        data field. If you wanted to define a filter that searches for a SubjectUserName
        of either john.doe or jane.doe, pass the following

        @{'SubjectUserName'=('john.doe','jane.doe')}

        You may specify multiple data files and values. Doing so will create
        an XPath filter that will only return results where both values
        are found. If you only wanted to return events where both the
        SubjectUserName is john.doe and the TargetUserName is jane.doe, then
        pass the following

        @{'SubjectUserName'='john.doe';'TargetUserName'='jane.doe'}

        You may pass an array of hash tables to create an 'or' XPath filter
        that will return objects where either key value set will be returned.
        If you wanted to define a filter that searches for either a
        SubjectUserName of john.doe or a TargetUserName of jane.doe then pass
        the following

        (@{'SubjectUserName'='john.doe'},@{'TargetUserName'='jane.doe'})
        .EXAMPLE
        Get-EventsFilter -ID 4663 -NamedDataFilter @{'SubjectUserName'='john.doe'} -LogName 'ForwardedEvents'

        This will return an XPath filter that will return any events with
        the id of 4663 and has a SubjectUserName of 'john.doe'

        Output:
        <QueryList>
            <Query Id="0" Path="ForwardedEvents">
                <Select Path="ForwardedEvents">
                        (*[System[EventID=4663]]) and (*[EventData[Data[@Name='SubjectUserName'] = 'john.doe']])
                </Select>
            </Query>
        </QueryList>

        .EXAMPLE
        Get-EventsFilter -StartTime '1/1/2015 01:30:00 PM' -EndTime '1/1/2015 02:00:00 PM' -LogName 'ForwardedEvents

        This will return an XPath filter that will return events that occured between 1:30
        2:00 PM on 1/1/2015.  The filter will only be good if used immediately.  XPath time
        filters are based on the number of milliseconds that have occured since the event
        and when the filter is used.  StartTime and EndTime simply calculate the number of
        milliseconds and use that for the filter.

        Output:
        <QueryList>
            <Query Id="0" Path="ForwardedEvents">
                <Select Path="ForwardedEvents">
                        (*[System[TimeCreated[timediff(@SystemTime) &lt;= 125812885399]]]) and (*[System[TimeCreated[timediff(@SystemTime)
    &gt;= 125811085399]]])
                </Select>
            </Query>
        </QueryList>

        .EXAMPLE
        Get-EventsFilter -StartTime (Get-Date).AddDays(-1) -LogName System

        This will return an XPath filter that will get events that occured within the last 24 hours.

        Output:
        <QueryList>
            <Query Id="0" Path="System">
                    <Select Path="System">
                        *[System[TimeCreated[timediff(@SystemTime) &lt;= 86404194]]]
                </Select>
            </Query>
        </QueryList>

        .EXAMPLE
        Get-EventsFilter -ID 1105 -LogName 'ForwardedEvents' -RecordID '3512231','3512232'

        This will return an XPath filter that will get events with EventRecordID 3512231 or 3512232 in Log ForwardedEvents with EventID 1105

        Output:
        <QueryList>
            <Query Id="0" Path="ForwardedEvents">
                    <Select Path="ForwardedEvents">
                        (*[System[EventID=1105]]) and (*[System[(EventRecordID=3512231) or (EventRecordID=3512232)]])
                </Select>
            </Query>
        </QueryList>
        #>

        [CmdletBinding()]
        Param
        (
            [String[]]
            $ID,

            [alias('RecordID')][string[]]
            $EventRecordID,

            [DateTime]
            $StartTime,

            [DateTime]
            $EndTime,

            [String[]]
            $Data,

            [String[]]
            $ProviderName,

            [Long[]]
            $Keywords,

            [ValidateSet(
                'Critical',
                'Error',
                'Informational',
                'LogAlways',
                'Verbose',
                'Warning'
            )]
            [String[]]
            $Level,

            [String[]]
            $UserID,

            [Hashtable[]]
            $NamedDataFilter,

            [Hashtable[]]
            $NamedDataExcludeFilter,

            [String[]]
            $ExcludeID,

            [String]
            $LogName,

            [String]
            $Path,

            [switch] $XPathOnly
        )

        #region Function definitions
        Function Join-XPathFilter {
            Param
            (
                [Parameter(
                    Mandatory = $True,
                    Position = 0
                )]
                [String]
                $NewFilter,

                [Parameter(
                    Position = 1
                )]
                [String]
                $ExistingFilter = '',

                [Parameter(
                    Position = 2
                )]
                # and and or are case sensitive
                # in xpath
                [ValidateSet(
                    "and",
                    "or",
                    IgnoreCase = $False
                )]
                [String]
                $Logic = 'and',

                [switch]$NoParenthesis
            )

            If ($ExistingFilter) {
                # If there is an existing filter add parenthesis unless noparenthesis is specified
                # and the logical operator
                if ($NoParenthesis) {
                    Return "$ExistingFilter $Logic $NewFilter"
                } Else {
                    Return "($ExistingFilter) $Logic ($NewFilter)"
                }
            } Else {
                Return $NewFilter
            }
            <#
        .SYNOPSIS
        This function handles the parenthesis and logical joining
        of XPath statements inside of Get-EventsFilter
        #>
        }

        Function Initialize-XPathFilter {
            Param
            (
                [Object[]]
                $Items,

                [String]
                $ForEachFormatString,

                [String]
                $FinalizeFormatString,

                [ValidateSet("and", "or", IgnoreCase = $False)]
                [String]
                $Logic = 'or',

                [switch]$NoParenthesis
            )

            $filter = ''

            ForEach ($item in $Items) {
                $options = @{'NewFilter' = ($ForEachFormatString -f $item)
                    'ExistingFilter'     = $filter
                    'Logic'              = $logic
                    'NoParenthesis'      = $NoParenthesis
                }
                $filter = Join-XPathFilter @options
            }

            Return $FinalizeFormatString -f $filter
            <#
        .SYNOPSIS
        This function loops thru a set of items and injecting each
        item in the format string given by ForEachFormatString, then
        combines each of those items together with 'or' logic
        using the function Join-XPathFilter, which handles the
        joining and parenthesis.  Before returning the result,
        it injects the resultant xpath into FinalizeFormatString.

        This function is a part of Get-EventsFilter
        #>
        }
        #endregion Function definitions

        [string] $filter = ''

        #region ID filter
        If ($ID) {
            $options = @{
                'Items'                = $ID
                'ForEachFormatString'  = "EventID={0}"
                'FinalizeFormatString' = "*[System[{0}]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion ID filter

        # region EventRecordID filter
        If ($EventRecordID) {
            $options = @{
                'Items'                = $EventRecordID
                'ForEachFormatString'  = "EventRecordID={0}"
                'FinalizeFormatString' = "*[System[{0}]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion EventRecordID filter

        #region Exclude ID filter
        If ($ExcludeID) {
            $options = @{
                'Items'                = $ExcludeID
                'ForEachFormatString'  = "EventID!={0}"
                'FinalizeFormatString' = "*[System[{0}]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion Exclude ID filter

        #region Date filters
        $Now = Get-Date

        # Time in XPath is filtered based on the number of milliseconds
        # between the creation of the event and when the XPath filter is
        # used.
        #
        # The timediff xpath function is used against the SystemTime
        # attribute of the TimeCreated node.

        ## Special chars needs replacement
        # <= is &lt;=
        # <  is &lt;
        # >  is &gt;
        # >= is &gt;=
        #

        If ($StartTime) {
            $Diff = [Math]::Round($Now.Subtract($StartTime).TotalMilliseconds)
            $filter = Join-XPathFilter -NewFilter "*[System[TimeCreated[timediff(@SystemTime) &lt;= $Diff]]]" -ExistingFilter $filter
        }

        If ($EndTime) {
            $Diff = [Math]::Round($Now.Subtract($EndTime).TotalMilliseconds)
            $filter = Join-XPathFilter -NewFilter "*[System[TimeCreated[timediff(@SystemTime) &gt;= $Diff]]]" -ExistingFilter $filter
        }
        #endregion Date filters

        #region Data filter
        If ($Data) {
            $options = @{
                'Items'                = $Data
                'ForEachFormatString'  = "Data='{0}'"
                'FinalizeFormatString' = "*[EventData[{0}]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion Data filter

        #region ProviderName filter
        If ($ProviderName) {
            $options = @{
                'Items'                = $ProviderName
                'ForEachFormatString'  = "@Name='{0}'"
                'FinalizeFormatString' = "*[System[Provider[{0}]]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion ProviderName filter

        #region Level filter
        If ($Level) {
            $levels = ForEach ($item in $Level) {
                # Levels in an event's XML are defined
                # with integer values.
                [Int][System.Diagnostics.Tracing.EventLevel]::$item
            }

            $options = @{
                'Items'                = $levels
                'ForEachFormatString'  = "Level={0}"
                'FinalizeFormatString' = "*[System[{0}]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion Level filter

        #region Keyword filter
        # Keywords are stored as a long integer
        # numeric value.  That integer is the
        # flagged (binary) combination of
        # all the keywords.
        #
        # By combining all given keywords
        # with a binary OR operation, and then
        # taking the resultant number and
        # comparing that against the number
        # stored in the events XML with a
        # binary AND operation will return
        # events that have any of the submitted
        # keywords assigned.
        If ($Keywords) {
            $keyword_filter = ''

            ForEach ($item in $Keywords) {
                If ($keyword_filter) {
                    $keyword_filter = $keyword_filter -bor $item
                } Else {
                    $keyword_filter = $item
                }
            }

            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter "*[System[band(Keywords,$keyword_filter)]]"
        }
        #endregion Keyword filter

        #region UserID filter
        # The UserID attribute of the Security node contains a Sid.
        If ($UserID) {
            $sids = ForEach ($item in $UserID) {
                Try {
                    #If the value submitted isn't a valid sid, it'll error.
                    $sid = [System.Security.Principal.SecurityIdentifier]($item)
                    $sid = $sid.Translate([System.Security.Principal.SecurityIdentifier])
                } Catch [System.Management.Automation.RuntimeException] {
                    # If a RuntimeException occured with an InvalidArgument category
                    # attempt to create an NTAccount object and resolve.
                    If ($Error[0].CategoryInfo.Category -eq 'InvalidArgument') {
                        Try {
                            $user = [System.Security.Principal.NTAccount]($item)
                            $sid = $user.Translate([System.Security.Principal.SecurityIdentifier])
                        } Catch {
                            #There was an error with either creating the NTAccount or
                            #Translating that object to a sid.
                            Throw $Error[0]
                        }
                    } Else {
                        #There was a RuntimeException from either creating the
                        #SecurityIdentifier object or the translation
                        #and the category was not InvalidArgument
                        Throw $Error[0]
                    }
                } Catch {
                    #There was an error from ether the creation of the SecurityIdentifier
                    #object or the Translatation
                    Throw $Error[0]
                }

                $sid.Value
            }

            $options = @{
                'Items'                = $sids
                'ForEachFormatString'  = "@UserID='{0}'"
                'FinalizeFormatString' = "*[System[Security[{0}]]]"
            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion UserID filter

        #region NamedDataFilter
        If ($NamedDataFilter) {
            $options = @{
                'Items'                = $(
                    # This will create set of datafilters for each of
                    # the hash tables submitted in the hash table array
                    ForEach ($item in $NamedDataFilter) {
                        $options = @{
                            'Items'                = $(
                                #This will result in as set of XPath subexpressions
                                #for each key submitted in the hashtable
                                ForEach ($key in $item.Keys) {
                                    If ($item[$key]) {
                                        #If there is a value for the key, create the
                                        #XPath for the Data node with that Name attribute
                                        #and value. Use 'and' logic to join the data values.
                                        #to the Name Attribute.
                                        $options = @{
                                            'Items'                = $item[$key]
                                            'NoParenthesis'        = $true
                                            'ForEachFormatString'  = "Data[@Name='$key'] = '{0}'"
                                            'FinalizeFormatString' = "{0}"
                                        }
                                        Initialize-XPathFilter @options
                                    } Else {
                                        #If there isn't a value for the key, create
                                        #XPath for the existence of the Data node with
                                        #that paritcular Name attribute.
                                        "Data[@Name='$key']"
                                    }
                                }
                            )
                            'ForEachFormatString'  = "{0}"
                            'FinalizeFormatString' = "{0}"
                        }
                        Initialize-XPathFilter @options
                    }
                )
                'ForEachFormatString'  = "{0}"
                'FinalizeFormatString' = "*[EventData[{0}]]"

            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion NamedDataFilter

        #region NamedDataExcludeFilter
        If ($NamedDataExcludeFilter) {
            $options = @{
                'Items'                = $(
                    # This will create set of datafilters for each of
                    # the hash tables submitted in the hash table array
                    ForEach ($item in $NamedDataExcludeFilter) {
                        $options = @{
                            'Items'                = $(
                                #This will result in as set of XPath subexpressions
                                #for each key submitted in the hashtable
                                ForEach ($key in $item.Keys) {
                                    If ($item[$key]) {
                                        #If there is a value for the key, create the
                                        #XPath for the Data node with that Name attribute
                                        #and value. Use 'and' logic to join the data values.
                                        #to the Name Attribute.
                                        $options = @{
                                            'Items'                = $item[$key]
                                            'NoParenthesis'        = $true
                                            'ForEachFormatString'  = "Data[@Name='$key'] != '{0}'"
                                            'FinalizeFormatString' = "{0}"
                                            'Logic'                = 'and'
                                        }
                                        Initialize-XPathFilter @options
                                    } Else {
                                        #If there isn't a value for the key, create
                                        #XPath for the existence of the Data node with
                                        #that paritcular Name attribute.
                                        "Data[@Name='$key']"
                                    }
                                }
                            )
                            'ForEachFormatString'  = "{0}"
                            'FinalizeFormatString' = "{0}"
                        }
                        Initialize-XPathFilter @options
                    }
                )
                'ForEachFormatString'  = "{0}"
                'FinalizeFormatString' = "*[EventData[{0}]]"

            }
            $filter = Join-XPathFilter -ExistingFilter $filter -NewFilter (Initialize-XPathFilter @options)
        }
        #endregion NamedDataExcludeFilter

        if ($XPathOnly) {
            return $Filter
        } else {
            if ($Path -ne '') {
                $FilterXML = @"
                    <QueryList>
                        <Query Id="0" Path="file://$Path">
                            <Select>
                                    $filter
                            </Select>
                        </Query>
                    </QueryList>
"@
            } else {
                $FilterXML = @"
                    <QueryList>
                        <Query Id="0" Path="$LogName">
                            <Select Path="$LogName">
                                    $filter
                            </Select>
                        </Query>
                    </QueryList>
"@
            }
            return $FilterXML
        }
    } # Function Get-EventsFilter
    function Get-EventsInternal () {
        [CmdLetBinding()]
        param (
            [string]$Comp,
            [ValidateNotNull()]
            [alias('Credentials')][System.Management.Automation.PSCredential]
            [System.Management.Automation.Credential()]$Credential = [System.Management.Automation.PSCredential]::Empty,
            [hashtable]$EventFilter,
            [int]$MaxEvents,
            [switch] $Oldest
        )
        $Measure = [System.Diagnostics.Stopwatch]::StartNew() # Timer Start

        Write-Verbose "Get-Events - Inside $Comp for Events ID: $($EventFilter.ID)"
        Write-Verbose "Get-Events - Inside $Comp for Events LogName: $($EventFilter.LogName)"
        Write-Verbose "Get-Events - Inside $Comp for Events RecordID: $($EventFilter.RecordID)"
        Write-Verbose "Get-Events - Inside $Comp for Events Oldest: $Oldest"
        try {
            [Array] $Events = @(
                if ($null -ne $EventFilter.RecordID -or `
                        $null -ne $EventFilter.NamedDataFilter -or `
                        $null -ne $EventFilter.ExcludeID -or `
                        $null -ne $EventFilter.NamedDataExcludeFilter -or `
                        $null -ne $EventFilter.UserID
                ) {
                    $FilterXML = Get-EventsFilter @EventFilter
                    $SplatEvents = @{
                        ErrorAction  = 'Stop'
                        ComputerName = $Comp
                        Oldest       = $Oldest
                        FilterXml    = $FilterXML
                    }
                    Write-Verbose "Get-Events - Inside $Comp - Custom FilterXML: `n$FilterXML"
                } else {
                    $SplatEvents = @{
                        ErrorAction     = 'Stop'
                        ComputerName    = $Comp
                        Oldest          = $Oldest
                        FilterHashtable = $EventFilter
                    }
                    foreach ($k in $EventFilter.Keys) {
                        Write-Verbose "Get-Events - Inside $Comp Data in FilterHashTable $k $($EventFilter[$k])"
                    }
                }
                if ($MaxEvents -ne 0) {
                    $SplatEvents.MaxEvents = $MaxEvents
                    Write-Verbose "Get-Events - Inside $Comp for Events Max Events: $MaxEvents"
                }
                if ($Credential -ne [System.Management.Automation.PSCredential]::Empty) {
                    $SplatEvents.Credential = $Credential
                    Write-Verbose "Get-Events - Inside $Comp for Events Credential: $Credential"
                }
                Get-WinEvent @SplatEvents
            )
            #$EventsCount = ($Events | Measure-Object).Count
            Write-Verbose -Message "Get-Events - Inside $Comp Events found $($Events.Count)"
        } catch {
            if ($_.Exception -match "No events were found that match the specified selection criteria") {
                Write-Verbose -Message "Get-Events - Inside $Comp No events found."
            } elseif ($_.Exception -match "There are no more endpoints available from the endpoint") {
                Write-Verbose -Message "Get-Events - Inside $Comp Error $($_.Exception.Message)"
                Write-Error -Message "$Comp`: $_"
            } else {
                Write-Verbose -Message "Get-Events - Inside $Comp Error $($_.Exception.Message)"
                Write-Error -Message "$Comp`: $_"
            }
            Write-Verbose "Get-Events - Inside $Comp Time to generate $($Measure.Elapsed.Hours) hours, $($Measure.Elapsed.Minutes) minutes, $($Measure.Elapsed.Seconds) seconds, $($Measure.Elapsed.Milliseconds) milliseconds"
            $Measure.Stop()
            return
        }
        Write-Verbose "Get-Events - Inside $Comp Processing events..."

        # Parse out the event message data
        ForEach ($Event in $Events) {
            # Convert the event to XML
            $eventXML = [xml]$Event.ToXml()
            # Iterate through each one of the XML message properties
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name "Computer" -Value $event.MachineName.ToString() -Force
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name "Date" -Value $Event.TimeCreated -Force
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('Computer', $event.MachineName.ToString()))
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('Date', $Event.TimeCreated))

            #$EventTopNodes = Get-Member -InputObject $eventXML.Event -MemberType Properties | Where-Object { $_.Name -ne 'System' -and $_.Name -ne 'xmlns' }
            $EventTopNodes = $eventXML.Event.PSAdapted.PSObject.Properties.Name #| Where-Object { $_ -ne 'System' -and $_ -ne 'xmlns' }

            [Array] $EventTopNodes = foreach ($Entry in $EventTopNodes) {
                if ($Entry -ne 'System' -and $Entry -ne 'xmlns' ) {
                    $Entry
                }
            }
            foreach ($TopNode in $EventTopNodes) {
                #$TopNode = $EventTopNode.Name

                #$EventSubsSubs = Get-Member -InputObject $eventXML.Event.$TopNode -MemberType Properties
                $EventSubsSubs = $eventXML.Event.$TopNode.PSAdapted.PSObject.Properties
                $h = 0
                foreach ($EventSubSub in $EventSubsSubs) {
                    $SubNode = $EventSubSub.Name
                    #$EventSubSub | ft -a
                    if ($EventSubSub.TypeNameOfValue -like "System.Object*") {
                        #if (Get-Member -InputObject $eventXML.Event.$TopNode -Name "$SubNode" -MemberType Properties) {
                        if ($eventXML.Event.$TopNode.$SubNode) {
                            # Case 1
                            #$SubSubNode = Get-Member -InputObject $eventXML.Event.$TopNode.$SubNode -MemberType Properties | Where-Object { $_.Name -ne 'xmlns' -and $_.Definition -like "string*" }
                            $SubSubNode = $eventXML.Event.$TopNode.$SubNode.PSAdapted.PSObject.Properties #| Where-Object { $_.Name -ne 'xmlns' -and $_.TypeNameOfValue -like "string*" }
                            [Array] $SubSubNode = foreach ($Entry in $SubSubNode) {
                                if ($Entry.Name -ne 'xmls' -and $_.TypeNameOfValue -like "string*") {
                                    $Entry
                                }
                            }
                            foreach ($Name in $SubSubNode.Name) {
                                $fieldName = $Name
                                $fieldValue = $eventXML.Event.$TopNode.$SubNode.$Name
                                #Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $fieldValue -Force
                                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $fieldValue))
                            }
                            # Case 1

                            For ($i = 0; $i -lt $eventXML.Event.$TopNode.$SubNode.Count; $i++) {
                                #if (Get-Member -InputObject $eventXML.Event.$TopNode.$SubNode[$i] -Name "Name" -MemberType Properties) {
                                if ($eventXML.Event.$TopNode.$SubNode[$i].Name) {
                                    # Case 2
                                    $fieldName = $eventXML.Event.$TopNode.$SubNode[$i].Name
                                    #if (Get-Member -InputObject $eventXML.Event.$TopNode.$SubNode[$i] -Name "#text" -MemberType Properties) {
                                    if ($eventXML.Event.$TopNode.$SubNode[$i]."#text") {
                                        $fieldValue = $eventXML.Event.$TopNode.$SubNode[$i]."#text"
                                        if ($fieldValue -eq "-".Trim()) { $fieldValue = $fieldValue -replace "-" }
                                    } else {
                                        $fieldValue = ""
                                    }
                                    if ($fieldName -ne "") {
                                        #Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $fieldValue -Force
                                        $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $fieldValue))
                                    }
                                    # Case 2
                                } else {
                                    # Case 3
                                    $Value = $eventXML.Event.$TopNode.$SubNode[$i]
                                    if ($Value.Name -ne 'Name' -and $Value.Name -ne '#text') {
                                        $fieldName = "NoNameA$i"
                                        $fieldValue = $Value
                                        # Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $fieldValue -Force
                                        $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $fieldValue))
                                    }
                                    # Case 3
                                }

                            }
                        }
                    } elseif ($EventSubSub.TypeNameOfValue -like "System.Xml.XmlElement*") {
                        # Case 1
                        #$SubSubNode = Get-Member -InputObject $eventXML.Event.$TopNode.$SubNode -MemberType Properties | Where-Object { $_.Name -ne 'xmlns' -and $_.Definition -like "string*" }
                        $SubSubNode = $eventXML.Event.$TopNode.$SubNode.PSAdapted.PSObject.Properties #| Where-Object { $_.Name -ne 'xmlns' -and $_.TypeNameOfValue -like "string*" }
                        [Array] $SubSubNode = foreach ($Entry in $SubSubNode) {
                            if ($Entry.Name -ne 'xmls' -and $_.TypeNameOfValue -like "string*") {
                                $Entry
                            }
                        }
                        foreach ($Name in $SubSubNode.Name) {
                            $fieldName = $Name
                            $fieldValue = $eventXML.Event.$TopNode.$SubNode.$Name
                            # Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $fieldValue -Force
                            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $fieldValue))
                        }
                        # Case 1
                    } else {
                        # Case 4 - Where Data has no Names
                        $fieldValue = $eventXML.Event.$TopNode.$SubNode
                        if ($fieldValue -match "\n") {
                            # this is case with ADConnect - event id 6946 where 1 Value has multiple values line per line
                            $SplittedValues = $fieldValue -split '\n'
                            foreach ($Split in $SplittedValues) {
                                $h++
                                $fieldName = "NoNameB$h"
                                #Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $Split -Force
                                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $Split))
                            }
                        } else {
                            $h++
                            $fieldName = "NoNameB$h"
                            #Add-Member -InputObject $Event -MemberType NoteProperty -Name $fieldName -Value $fieldValue -Force
                            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new($fieldName, $fieldValue))
                        }
                        # Case 4
                    }
                }
            }
            # This adds some fields specific to PSWinReporting
            [string] $MessageSubject = ($Event.Message -split '\n')[0] -replace "`n", '' -replace "`r", '' -replace "`t", ''
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'MessageSubject' -Value $MessageSubject -Force
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'Action' -Value $MessageSubject -Force
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('MessageSubject', $MessageSubject))
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('Action', $MessageSubject))

            # Level value is not needed because there is actually LevelDisplayName
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'LevelTranslated' -Value ([PSEventViewer.Level] $Event.Level)

            # Overwrite value - the old value is collection
            # Add-Member -InputObject $Event -MemberType NoteProperty -Name 'KeywordDisplayName' -Value ($Event.KeywordsDisplayNames -join ',') -Force
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('KeywordDisplayName', ($Event.KeywordsDisplayNames -join ',')))

            if ($Event.SubjectDomainName -and $Event.SubjectUserName) {
                #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'Who' -Value "$($Event.SubjectDomainName)\$($Event.SubjectUserName)" -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('Who', "$($Event.SubjectDomainName)\$($Event.SubjectUserName)" ))
            } elseif ($Event.SubjectUserName) {
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('Who', "$($Event.SubjectUserName)"))
            }
            if ($Event.TargetDomainName -and $Event.TargetUserName) {
                #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'ObjectAffected' -Value "$($Event.TargetDomainName)\$($Event.TargetUserName)" -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('ObjectAffected', "$($Event.TargetDomainName)\$($Event.TargetUserName)"))
            } elseif ($Event.TargetUserName) {
                # Add-Member -InputObject $Event -MemberType NoteProperty -Name 'ObjectAffected' -Value "$($Event.TargetUserName)" -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('ObjectAffected', "$($Event.TargetUserName)"))
            }
            if ($Event.MemberName) {
                [string] $MemberNameWithoutCN = $Event.MemberName -replace 'CN=|\\|,(OU|DC|CN).*$'
                #Add-Member -InputObject $Event -MemberType NoteProperty -Name 'MemberNameWithoutCN' -Value $MemberNameWithoutCN -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('MemberNameWithoutCN', $MemberNameWithoutCN))
            }
            if ($EventFilter.Path) {
                #Add-Member -InputObject $Event -MemberType NoteProperty -Name "GatheredFrom" -Value $EventFilter.Path -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('GatheredFrom', $EventFilter.Path))
            } else {
                #Add-Member -InputObject $Event -MemberType NoteProperty -Name "GatheredFrom" -Value $Comp -Force
                $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('GatheredFrom', $Comp))
            }
            #Add-Member -InputObject $Event -MemberType NoteProperty -Name "GatheredLogName" -Value $EventFilter.LogName -Force
            $Event.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('GatheredLogName', $EventFilter.LogName))
        }
        Write-Verbose "Get-Events - Inside $Comp Time to generate $($Measure.Elapsed.Hours) hours, $($Measure.Elapsed.Minutes) minutes, $($Measure.Elapsed.Seconds) seconds, $($Measure.Elapsed.Milliseconds) milliseconds"
        $Measure.Stop()
        return $Events
    }
    Write-Verbose "Get-Events -------------START---------------------"
    [Array] $Data = Get-EventsInternal -Comp $Comp -EventFilter $EventFilter -MaxEvents $MaxEvents -Oldest:$Oldest -Verbose:$Verbose -Credential $Credential
    Write-Verbose "Get-Events --------------END----------------------"
    return $Data
}
