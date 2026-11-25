@echo off
rem Runs the Windows screenshot pass in the logged-in desktop session, where the
rem game can actually open a window. Launched by a scheduled task from macOS.
set SHOTS=\\Mac\Home\RiderProjects\PSSCollection\Stores\windows\shots
if not exist "%SHOTS%" mkdir "%SHOTS%"
powershell -NoProfile -ExecutionPolicy Bypass -File "\\Mac\Home\RiderProjects\PSSCollection\tools\capture_windows.ps1" -App "C:\pssbuild\pub\win-x64\PSSCollection.exe" -Out "%SHOTS%" > "%SHOTS%\run.log" 2>&1
echo exit=%ERRORLEVEL% >> "%SHOTS%\run.log"
