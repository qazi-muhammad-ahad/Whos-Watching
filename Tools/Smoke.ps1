param([ValidateSet(2,4)][int]$Players = 2)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project 'Builds/Windows/WhosWatching.exe'
if (!(Test-Path $exe)) { throw 'Build the Windows development player first using Tools/Unity.ps1 -Action Build.' }
$running = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Start-Probe([string]$name,[string]$role,[int]$seconds,[switch]$BadToken) {
    $log = Join-Path $project "Logs/$name.log"
    $arguments = @('-batchmode','-nographics','-wwSmoke',$role,'-wwExpected',$Players,'-wwSeconds',$seconds,'-logFile',('"'+$log+'"'))
    if ($BadToken) { $arguments += '-wwBadToken' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $running.Add($process)
    return @{Process=$process; Log=$log; Name=$name}
}
try {
    $probes = @()
    $probes += Start-Probe "Smoke${Players}-Host" 'host' 30
    Start-Sleep -Seconds 2
    for ($i=1; $i -lt $Players; $i++) { $probes += Start-Probe "Smoke${Players}-Client$i" 'client' 22 }
    Start-Sleep -Seconds 4
    if ($Players -eq 4) { $probes += Start-Probe 'Smoke4-Rejected' 'reject' 10 }
    $probes += Start-Probe "Smoke${Players}-BadToken" 'badtoken' 10 -BadToken
    foreach ($probe in $probes) {
        if (!$probe.Process.WaitForExit(45000)) { throw "Timed out: $($probe.Name)" }
        $result = Select-String -LiteralPath $probe.Log -Pattern 'WW_SMOKE (PASS|FAIL)'
        $result.Line
        if ($probe.Process.ExitCode -ne 0 -or !$result -or $result.Line -notmatch 'WW_SMOKE PASS') { throw "Failed: $($probe.Name). Inspect its log." }
        if (Select-String -LiteralPath $probe.Log -Pattern 'Exception:|\[Error\]|error CS') { throw "Runtime errors: $($probe.Name)" }
    }
} finally {
    foreach ($process in $running) { if (!$process.HasExited) { Stop-Process -Id $process.Id } }
}
