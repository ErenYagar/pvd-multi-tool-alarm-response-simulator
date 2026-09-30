$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$player = Join-Path $project 'Builds/Windows/PvdMultiToolSimulator.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'Build the Windows player first with Tools/Run-UnityChecks.ps1 -Build.' }
$arguments = @('-batchmode','-pvdVerify','-logFile',"`"$project/TestResults/player-batch.log`"")
$process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(120000)) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'Player batch verification exceeded 120 seconds; see TestResults/player-batch.log.'
}
if ($process.ExitCode -ne 0) { throw "Player batch verification exit $($process.ExitCode); see TestResults/player-batch.log." }
Write-Output 'Windows player batch verification completed. 3D-only captures and results: Builds/Windows/Verification/Batch-*'
