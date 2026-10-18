param([string]$Executable = (Join-Path $PSScriptRoot '../Builds/Windows/WhosWatching.exe'))
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$processes = @()
try {
    for ($i = 0; $i -lt 4; $i++) {
        $role = if ($i -eq 0) { 'host' } else { 'client' }
        $log = Join-Path $project "Logs/Appearance-$i.log"
        $processes += Start-Process -FilePath $Executable -ArgumentList @('-batchmode','-nographics','-wwAppearance',$role,'-logFile',('"' + $log + '"')) -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 2
    }
    foreach ($process in $processes) {
        if (-not $process.WaitForExit(90000)) { throw 'Appearance acceptance timed out.' }
        if ($process.ExitCode -ne 0) { throw 'Appearance acceptance failed; inspect Logs/Appearance-*.log.' }
    }
    for ($i = 0; $i -lt 4; $i++) {
        if (-not (Select-String -LiteralPath (Join-Path $project "Logs/Appearance-$i.log") -Pattern 'WW_APPEARANCE PASS')) { throw "Missing PASS for peer $i" }
    }
    Write-Output 'Appearance acceptance passed: four peers, fourteen stages including team switching, capacity, leadership and runtime visual tints.'
} finally {
    foreach ($process in $processes) { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
}
