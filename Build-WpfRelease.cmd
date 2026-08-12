@echo off
setlocal
title AMacQ Release Build

echo.
echo Building AMacQ application and license generator...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-WpfRelease.ps1"
set "buildExitCode=%ERRORLEVEL%"

echo.
if not "%buildExitCode%"=="0" (
    echo Build failed. Exit code: %buildExitCode%
    echo Close AMacQConfigEditor.exe and AMacQLicenseGenerator.exe, then try again.
) else (
    echo Build completed.
    echo Application: %~dp0dist\net48\AMacQConfigEditor.exe
    echo Author edition: %~dp0dist\net48\AMacQConfigEditor-Author.exe
    echo License tool: %~dp0author-tools\AMacQLicenseGenerator.exe
)
echo.
pause
exit /b %buildExitCode%
