@echo off
REM TuxAI Service Stress Test Build Script for Windows
REM This script builds a self-contained executable for Linux

echo 🔨 Building TuxAI Stress Test Tool for Linux...
echo ==============================================

REM Navigate to the project directory
cd /d "%~dp0"

REM Clean previous builds
echo 🧹 Cleaning previous builds...
dotnet clean >nul 2>&1

REM Restore packages
echo 📦 Restoring NuGet packages...
dotnet restore

if errorlevel 1 (
    echo ❌ Failed to restore packages
    exit /b 1
)

REM Build and publish
echo 🚀 Building self-contained Linux executable...
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

if errorlevel 1 (
    echo ❌ Build failed
    exit /b 1
)

REM Check if executable was created
set "OUTPUT_PATH=bin\Release\net9.0\linux-x64\publish\StressTest"

if exist "%OUTPUT_PATH%" (
    echo ✅ Build successful!
    echo 📁 Executable location: %OUTPUT_PATH%
    for %%I in ("%OUTPUT_PATH%") do echo 📊 File size: %%~zI bytes
    echo.
    echo 🚚 To deploy to your Linux VM:
    echo    scp "%OUTPUT_PATH%" user@your-vm:/path/to/destination/
    echo.
    echo 🏃 To run on Linux:
    echo    chmod +x StressTest
    echo    ./StressTest --help
) else (
    echo ❌ Build completed but executable not found
    exit /b 1
)
