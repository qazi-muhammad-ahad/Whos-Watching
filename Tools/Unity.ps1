param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet('Test','Build')][string]$Action = 'Test'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force (Join-Path $project 'Logs') | Out-Null
$arguments = @('-batchmode','-nographics','-projectPath', ('"' + $project + '"'))
if ($Action -eq 'Test') {
    $arguments += @('-runTests','-testPlatform','EditMode','-testResults', ('"' + (Join-Path $project 'Logs/EditMode.xml') + '"'))
} else {
    $arguments += @('-quit','-executeMethod','WhosWatching.Editor.ProjectSetup.Build')
}
$arguments += @('-logFile', ('"' + (Join-Path $project "Logs/$Action.log") + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity $Action failed with exit $($process.ExitCode). Inspect Logs/$Action.log." }
if ($Action -eq 'Test') {
    [xml]$report = Get-Content (Join-Path $project 'Logs/EditMode.xml')
    if ($report.'test-run'.result -ne 'Passed') { throw 'Unity tests did not pass.' }
    $report.'test-run' | Select-Object result,total,passed,failed
}
