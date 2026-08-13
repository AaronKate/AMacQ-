@echo off
setlocal
title AMacQ Release Build

echo.
echo Building AMacQ verification and author editions...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-RenamedRelease.ps1"
set "buildExitCode=%ERRORLEVEL%"

echo.
if not "%buildExitCode%"=="0" (
    echo Build failed. Exit code: %buildExitCode%
) else (
    echo Build completed.
    echo Output folder: %~dp0dist\net48
)
echo.
pause
exit /b %buildExitCode%
