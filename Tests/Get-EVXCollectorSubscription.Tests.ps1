Describe 'Get-EVXCollectorSubscription' {
    It 'is exported as a thin wildcard-capable inventory command' {
        $Command = Get-Command Get-EVXCollectorSubscription

        $Command.CommandType | Should -Be 'Cmdlet'
        $Command.Parameters.Keys | Should -Contain 'Name'
        $Command.Parameters.Keys | Should -Contain 'MachineName'
        $Command.Parameters.Keys | Should -Contain 'EnabledOnly'
        $Command.Parameters.Keys | Should -Contain 'IncludeRuntimeStatus'
        $Command.Parameters.Keys | Should -Contain 'Readiness'
    }

    It 'returns detached local snapshots or an empty inventory' {
        $Subscriptions = @(Get-EVXCollectorSubscription -Name '*')

        foreach ($Subscription in $Subscriptions) {
            $Subscription.SubscriptionName | Should -Not -BeNullOrEmpty
            $Subscription.MachineName | Should -Not -BeNullOrEmpty
        }
    }
}
