BeforeAll {
    $source = @'
using System.Management.Automation;
using System.Threading.Tasks;
using PSEventViewer;

[Cmdlet("Get", "EVXBackpressureProbe")]
public sealed class EVXBackpressureProbeCommand : AsyncPSCmdlet {
    public static volatile int Produced;
    protected override async Task ProcessRecordAsync() {
    await Task.Yield();
    for (int index = 0; index < 1000; index++) {
        WriteObjectWithBackpressure(index);
        Produced = index + 1;
    }
    }
}
'@
    $compile = @{
        TypeDefinition = $source
        ReferencedAssemblies = @(
            [PSEventViewer.AsyncPSCmdlet].Assembly.Location,
            [System.Management.Automation.PSCmdlet].Assembly.Location
        )
    }
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $compile.CompilerOptions = '/nowarn:1701'
        $compile.OutputAssembly = Join-Path $TestDrive 'EVXBackpressureProbe.dll'
        Add-Type @compile
        $context = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([PSEventViewer.AsyncPSCmdlet].Assembly)
        $stream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($compile.OutputAssembly))
        try {
            $script:probeType = $context.LoadFromStream($stream).GetType('EVXBackpressureProbeCommand', $true)
        } finally {
            $stream.Dispose()
        }
    } else {
        $script:probeType = @(Add-Type @compile -PassThru)[0]
    }
    Import-Module -Assembly $script:probeType.Assembly
    $script:producedField = $script:probeType.GetField('Produced')
}

Describe 'Acknowledged asynchronous event output' {
    It 'keeps a slow sequential consumer from accumulating producer output' {
        $script:producedField.SetValue($null, 0)
        $observedAhead = -1
        $received = @(Get-EVXBackpressureProbe | ForEach-Object {
            if ($_ -eq 0) {
                Start-Sleep -Milliseconds 200
                $observedAhead = $script:producedField.GetValue([object] $null)
            }
            $_
        })
        $observedAhead | Should -BeLessOrEqual 1
        $received | Should -HaveCount 1000
        $received[999] | Should -Be 999
    }

    It 'stops an acknowledged producer when a downstream command stops early' {
        $script:producedField.SetValue($null, 0)
        $received = @(Get-EVXBackpressureProbe | Select-Object -First 1)
        $received | Should -HaveCount 1
        $script:producedField.GetValue([object] $null) | Should -BeLessOrEqual 1
    }
}

AfterAll {
    if ($script:probeType) {
        Remove-Module -Name $script:probeType.Assembly.GetName().Name -ErrorAction SilentlyContinue
    }
}
