Describe 'Reset-EVXEventCheckpoint' {
    It 'atomically resets one key and exposes its companion paths' {
        $CheckpointPath = Join-Path $TestDrive 'reset-surface.json'
        $Initial = [EventViewerX.EventCheckpointStore]::Update(
            $CheckpointPath,
            [EventViewerX.EventCheckpointUpdate[]] @(
                [EventViewerX.EventCheckpointUpdate]::new('one', 10, [guid]::Empty),
                [EventViewerX.EventCheckpointUpdate]::new('two', 20, [guid]::Empty)
            ))

        $Snapshot = Reset-EVXEventCheckpoint -Path $CheckpointPath -Key one -PassThru -Confirm:$false

        $Snapshot.CheckpointPath | Should -Be ([IO.Path]::GetFullPath($CheckpointPath))
        $Snapshot.StatePath | Should -Be ([IO.Path]::GetFullPath($CheckpointPath) + '.state.json')
        $Snapshot.LockPath | Should -Be ([IO.Path]::GetFullPath($CheckpointPath) + '.lock')
        $Snapshot.Records.ContainsKey('one') | Should -BeFalse
        $Snapshot.Records['two'] | Should -Be 20
        $Snapshot.Checkpoints['one'].GenerationId | Should -Not -Be $Initial.Checkpoints['one'].GenerationId
    }

    It 'resets every derived source checkpoint for a RecordIdKey alias' {
        $CheckpointPath = Join-Path $TestDrive 'reset-derived-sources.json'
        $Initial = [EventViewerX.EventCheckpointStore]::Update(
            $CheckpointPath,
            [EventViewerX.EventCheckpointUpdate[]] @(
                [EventViewerX.EventCheckpointUpdate]::new('CriticalEvents|AD1|System', 10, [guid]::Empty),
                [EventViewerX.EventCheckpointUpdate]::new('CriticalEvents|AD2|System', 20, [guid]::Empty),
                [EventViewerX.EventCheckpointUpdate]::new('CriticalEvents2|AD1|System', 30, [guid]::Empty)
            ))

        $Snapshot = Reset-EVXEventCheckpoint -Path $CheckpointPath -RecordIdKey CriticalEvents -PassThru -Confirm:$false

        $Snapshot.Records.ContainsKey('CriticalEvents|AD1|System') | Should -BeFalse
        $Snapshot.Records.ContainsKey('CriticalEvents|AD2|System') | Should -BeFalse
        $Snapshot.Records['CriticalEvents2|AD1|System'] | Should -Be 30
        $Snapshot.Checkpoints['CriticalEvents|AD1|System'].GenerationId |
            Should -Not -Be $Initial.Checkpoints['CriticalEvents|AD1|System'].GenerationId
        $Snapshot.Checkpoints['CriticalEvents|AD2|System'].GenerationId |
            Should -Not -Be $Initial.Checkpoints['CriticalEvents|AD2|System'].GenerationId
    }

    It 'supports WhatIf without changing persisted progress' {
        $CheckpointPath = Join-Path $TestDrive 'reset-whatif.json'
        $Initial = [EventViewerX.EventCheckpointStore]::Update(
            $CheckpointPath,
            [EventViewerX.EventCheckpointUpdate[]] @(
                [EventViewerX.EventCheckpointUpdate]::new('one', 10, [guid]::Empty)
            ))

        Reset-EVXEventCheckpoint -Path $CheckpointPath -Key one -WhatIf
        $After = [EventViewerX.EventCheckpointStore]::Load($CheckpointPath)

        $After.Records['one'] | Should -Be 10
        $After.Checkpoints['one'].GenerationId | Should -Be $Initial.Checkpoints['one'].GenerationId
    }
}
