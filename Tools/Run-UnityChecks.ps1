param([string]$UnityEditor,[switch]$Build)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (-not $UnityEditor) {
    $candidates = @(Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs/Unity') -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue)
    if ($candidates.Count -ne 1) { throw 'Supply the actual installed UnityEditor path.' }
    $UnityEditor = $candidates[0].FullName
}
New-Item -ItemType Directory -Path "$project/TestResults" -Force | Out-Null
function Run-Unity([string[]]$Extra,[string]$LogName) {
    $arguments = @('-batchmode','-nographics','-projectPath',"`"$project`"",'-logFile',"`"$project/TestResults/$LogName`"") + $Extra
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exit $($process.ExitCode): $LogName" }
}
Run-Unity -Extra @('-executeMethod','PvdSim.Editor.DemoBuilder.PrepareBatch') -LogName 'prepare.log'
Run-Unity -Extra @('-runTests','-testPlatform','EditMode','-testResults',"`"$project/TestResults/EditMode.xml`"") -LogName 'EditMode.log'
if ($Build) { Run-Unity -Extra @('-executeMethod','PvdSim.Editor.DemoBuilder.BuildWindows') -LogName 'build.log' }
