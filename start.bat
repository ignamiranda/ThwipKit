@echo off
REM Spider-Man Modding Tool - Startup Script
REM This script builds and runs the Spider-Man Modding Tool GUI application

REM Set working directory to where this script is located
cd /d "%~dp0"
echo Working directory: %CD%

echo.
echo Restoring NuGet packages...
dotnet restore >nul 2>&1
if errorlevel 1 (
    echo Warning: dotnet restore failed or not needed, trying to build anyway...
)

echo.
echo Building Spider-Man Modding Tool...
dotnet build "SpiderManModdingTool\SpiderManModdingTool.csproj" -c Debug
if errorlevel 1 (
    echo Error: Failed to build application
    pause
    exit /b 1
)

echo.
echo Launching Spider-Man Modding Tool...
dotnet run --project "SpiderManModdingTool\SpiderManModdingTool.csproj"
if errorlevel 1 (
    echo Error: Failed to run application
    pause
    exit /b 1
)

echo.
echo Application exited normally.
pause