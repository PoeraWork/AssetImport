@echo off
setlocal
chcp 65001 >nul
"%~dp0Diagnostic\AssetImport.DemoCheck.exe" --kit "%~dp0."
set "check_result=%ERRORLEVEL%"
echo.
echo Reports are in the Reports folder next to this script.
echo PASS only means native model parsing passed, not an in-game test.
echo LWS plugin-io is a known FAIL in AssetImport 4.1.4. Keep the report.
echo Exit code: %check_result%  ^(0=all passed, 2=model failures, 1=fatal error^)
pause
exit /b %check_result%
