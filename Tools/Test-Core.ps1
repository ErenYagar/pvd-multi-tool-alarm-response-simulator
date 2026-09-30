$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$sources = @(Get-ChildItem -LiteralPath "$project/Assets/PvdSimulator/Core" -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += "$project/Assets/PvdSimulator/Tests/EditMode/CoreChecks.cs"
Add-Type -Path $sources
$result = [PvdSim.Checks.CoreChecks]::RunAll()
New-Item -ItemType Directory -Path "$project/TestResults" -Force | Out-Null
$result | Set-Content -LiteralPath "$project/TestResults/core-checks.txt" -Encoding utf8
Write-Output $result
if ($result.Contains('FAIL')) { exit 1 }
