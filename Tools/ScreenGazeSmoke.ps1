param([ValidateSet(2,4)][int]$Players = 2)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project 'Builds/Windows/WhosWatching.exe'
$running = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$probes = @()
try {
    for ($i=0; $i -lt $Players; $i++) {
        $role = if ($i -eq 0) { 'host' } else { 'client' }
        $log = Join-Path $project "Logs/ScreenGaze${Players}-$i.log"
        $process = Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','-wwScreenGaze',$role,'-wwExpected',$Players,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
        $running.Add($process)
        $probes += @{Process=$process;Log=$log}
        if ($i -eq 0) { Start-Sleep -Seconds 2 }
    }
    foreach ($probe in $probes) {
        if (!$probe.Process.WaitForExit(90000)) { throw 'Screen gaze probe timed out.' }
        $result = Select-String -LiteralPath $probe.Log -Pattern 'WW_SCREEN (PASS|FAIL)'
        $result.Line
        if ($probe.Process.ExitCode -ne 0 -or !$result -or $result.Line -notmatch 'WW_SCREEN PASS') { throw "Screen gaze test failed: $($probe.Log)" }
        if (Select-String -LiteralPath $probe.Log -Pattern 'Exception:|\[Error\]|error CS') { throw "Runtime errors: $($probe.Log)" }
    }
} finally { foreach ($process in $running) { if (!$process.HasExited) { Stop-Process -Id $process.Id } } }

