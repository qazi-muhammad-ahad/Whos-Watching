$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$running = @()
try {
    for ($i=0; $i -lt 2; $i++) {
        $role = if ($i -eq 0) { 'host' } else { 'client' }
        $log = Join-Path $project "Logs/Arena-$i.log"
        $running += Start-Process -FilePath (Join-Path $project 'Builds/Windows/WhosWatching.exe') -ArgumentList @('-batchmode','-nographics','-wwArena',$role,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
    }
    foreach ($process in $running) { if (-not $process.WaitForExit(120000)) { throw 'Arena acceptance timeout.' } }
    for ($i=0; $i -lt 2; $i++) {
        $log = Join-Path $project "Logs/Arena-$i.log"
        $result = Select-String -LiteralPath $log -Pattern 'WW_ARENA (PASS|FAIL)'
        $result.Line
        if ($running[$i].ExitCode -ne 0 -or -not $result -or $result.Line -notmatch 'WW_ARENA PASS') { throw "Arena acceptance failed: $log" }
        if (Select-String -LiteralPath $log -Pattern 'Exception:|\[Error\]') { throw "Runtime error: $log" }
    }
} finally { foreach ($process in $running) { if (-not $process.HasExited) { Stop-Process -Id $process.Id } } }

