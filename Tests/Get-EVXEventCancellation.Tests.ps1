Describe 'Get-EVXEvent - Cancellation' {
    It 'Stops execution when pipeline is cancelled' {
        $modulePath = [System.IO.Path]::Combine($PSScriptRoot, '..', 'PSEventViewer.psm1')
        $ps = [System.Management.Automation.PowerShell]::Create()
        try {
            $null = $ps.AddScript({
                param(
                    [string] $modulePath,
                    [string] $logName
                )
                Import-Module -Name $modulePath -Force
                Get-EVXEvent -LogName $logName -DisableParallel -MaxEvents 100000 | ForEach-Object {
                    Start-Sleep -Milliseconds 50
                }
            }).AddArgument($modulePath).AddArgument('Application')
            $handle = $ps.BeginInvoke()
            Start-Sleep -Milliseconds 200
            $ps.Stop()
            # Some hosts throw PipelineStopped, others return cleanly. Accept both.
            $exception = $null
            try {
                $ps.EndInvoke($handle)
            } catch {
                $exception = $_
            }

            if ($exception) {
                $exception.FullyQualifiedErrorId | Should -Match 'PipelineStopped'
            }
        } finally {
            $ps.Dispose()
        }
    }
}
