@echo off
setlocal
if "%~1"=="" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-AssetImportLogs.ps1"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-AssetImportLogs.ps1" -GameRoot "%~1"
)
echo.
pause
