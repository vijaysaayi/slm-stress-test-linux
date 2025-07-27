@echo off
setlocal enabledelayedexpansion

REM TuxAI Service Docker Deployment Script
REM Usage: deploy.bat [tag] [registry]
REM Example: deploy.bat v1.0.0
REM Example: deploy.bat latest breakfreetest.azurecr.io

REM Set default values
set "TAG=%~1"
if "%TAG%"=="" set "TAG=latest"

set "REGISTRY=%~2"
if "%REGISTRY%"=="" set "REGISTRY=breakfreetest.azurecr.io"

set "IMAGE_NAME=smol"
set "LOCAL_IMAGE_NAME=tux-ai-service"
set "FULL_IMAGE_NAME=%REGISTRY%/%IMAGE_NAME%:%TAG%"

echo === TuxAI Service Deployment Script ===
echo Registry: %REGISTRY%
echo Image: %IMAGE_NAME%
echo Tag: %TAG%
echo Full Image Name: %FULL_IMAGE_NAME%
echo.

REM Check if Docker is running
echo Checking Docker status...
docker version >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker is not running or not accessible. Please start Docker Desktop.
    exit /b 1
)
echo ✓ Docker is running

REM Build the image
echo.
echo Building Docker image...
echo Command: docker build -t %LOCAL_IMAGE_NAME%:%TAG% .
docker build -t "%LOCAL_IMAGE_NAME%:%TAG%" .
if errorlevel 1 (
    echo ❌ Docker build failed
    exit /b 1
)
echo ✓ Image built successfully

REM Tag the image for ACR
echo.
echo Tagging image for Azure Container Registry...
echo Command: docker tag %LOCAL_IMAGE_NAME%:%TAG% %FULL_IMAGE_NAME%
docker tag "%LOCAL_IMAGE_NAME%:%TAG%" "%FULL_IMAGE_NAME%"
if errorlevel 1 (
    echo ❌ Docker tag failed
    exit /b 1
)
echo ✓ Image tagged successfully

REM Login to ACR (if needed)
echo.
echo Checking Azure Container Registry login...
az acr login --name breakfreetest >nul 2>&1
if errorlevel 1 (
    echo ⚠ ACR login failed. Please run 'az login' first, then try again.
    exit /b 1
)
echo ✓ ACR login verified

REM Push the image
echo.
echo Pushing image to Azure Container Registry...
echo Command: docker push %FULL_IMAGE_NAME%
docker push "%FULL_IMAGE_NAME%"
if errorlevel 1 (
    echo ❌ Docker push failed
    exit /b 1
)
echo ✓ Image pushed successfully

echo.
echo === Deployment Successful ===
echo Image: %FULL_IMAGE_NAME%
echo Registry: %REGISTRY%
echo.
echo You can now deploy this image using:
echo   az container create --name smol-ai --image %FULL_IMAGE_NAME% ...
echo   or reference it in your Azure Container Apps/AKS deployments

endlocal
