@echo off
if not exist "%~dp0Builds\Windows\PvdMultiToolSimulator.exe" (
  echo Windows build is not available yet. Activate Unity, then run Tools/Run-UnityChecks.ps1 -Build.
  pause
  exit /b 1
)
start "PVD Simulator" "%~dp0Builds\Windows\PvdMultiToolSimulator.exe"
