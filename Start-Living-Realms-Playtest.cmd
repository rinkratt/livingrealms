@echo off
setlocal

set "PROJECT_DIR=%~dp0client\LivingRealms.Client"

if defined GODOT_EXE if exist "%GODOT_EXE%" goto godot_found

set "GODOT_EXE="
for /f "delims=" %%G in ('where Godot_v4.7.1-stable_mono_win64.exe 2^>nul') do if not defined GODOT_EXE set "GODOT_EXE=%%G"
for /f "delims=" %%G in ('where godot4.exe 2^>nul') do if not defined GODOT_EXE set "GODOT_EXE=%%G"
for /f "delims=" %%G in ('where godot.exe 2^>nul') do if not defined GODOT_EXE set "GODOT_EXE=%%G"

if defined GODOT_EXE goto godot_found

if exist "%USERPROFILE%\Tools\Godot-4.7.1-mono\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe" (
    set "GODOT_EXE=%USERPROFILE%\Tools\Godot-4.7.1-mono\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
    goto godot_found
)

if exist "%LOCALAPPDATA%\Programs\Godot\Godot_v4.7.1-stable_mono_win64.exe" (
    set "GODOT_EXE=%LOCALAPPDATA%\Programs\Godot\Godot_v4.7.1-stable_mono_win64.exe"
    goto godot_found
)

echo Godot 4.7.1 .NET was not found.
echo Install the .NET edition, add it to PATH, or set the GODOT_EXE environment variable.
echo You can also open Godot manually and import:
echo %PROJECT_DIR%\project.godot
pause
exit /b 1

:godot_found
powershell.exe -NoProfile -Command "try { $health = Invoke-RestMethod -Uri 'https://living-realms.com/game-api/health/ready' -TimeoutSec 15; $api = Invoke-RestMethod -Uri 'https://living-realms.com/game-api/api' -TimeoutSec 15; if ($health.status -ne 'Healthy' -or $api.phase -lt 6) { exit 1 } } catch { exit 1 }"
if errorlevel 1 (
    echo The Living Realms play-test server is not ready.
    echo Check your internet connection and try again.
    pause
    exit /b 1
)

start "Living Realms Play Test" "%GODOT_EXE%" --path "%PROJECT_DIR%"
exit /b 0
