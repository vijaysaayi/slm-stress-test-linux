#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build, tag, and push Docker image to Azure Container Registry

.DESCRIPTION
    This script builds the AI service Docker image, tags it with the specified tag,
    and pushes it to breakfreetest.azurecr.io/smol registry.

.PARAMETER Tag
    The tag to apply to the image (default: "latest")

.PARAMETER SkipBuild
    Skip the build step and only tag/push existing image

.PARAMETER Registry
    The Azure Container Registry URL (default: "breakfreetest.azurecr.io")

.EXAMPLE
    .\deploy.ps1
    # Builds and pushes with tag "latest"

.EXAMPLE
    .\deploy.ps1 -Tag "v1.0.0"
    # Builds and pushes with tag "v1.0.0"

.EXAMPLE
    .\deploy.ps1 -Tag "dev" -SkipBuild
    # Only tags and pushes existing image with tag "dev"
#>

param(
    [Parameter(Position = 0)]
    [string]$Tag = "latest",
    
    [Parameter()]
    [switch]$SkipBuild,
    
    [Parameter()]
    [string]$Registry = "breakfreetest.azurecr.io"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$ImageName = "smol"
$LocalImageName = "tux-ai-service"
$FullImageName = "$Registry/$ImageName`:$Tag"

Write-Host "=== TuxAI Service Deployment Script ===" -ForegroundColor Cyan
Write-Host "Registry: $Registry" -ForegroundColor Yellow
Write-Host "Image: $ImageName" -ForegroundColor Yellow
Write-Host "Tag: $Tag" -ForegroundColor Yellow
Write-Host "Full Image Name: $FullImageName" -ForegroundColor Green
Write-Host ""

try {
    # Check if Docker is running
    Write-Host "Checking Docker status..." -ForegroundColor Blue
    docker version --format "Client: {{.Client.Version}}, Server: {{.Server.Version}}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running or not accessible. Please start Docker Desktop."
    }
    Write-Host "✓ Docker is running" -ForegroundColor Green

    # Build the image (unless skipped)
    if (-not $SkipBuild) {
        Write-Host "`nBuilding Docker image..." -ForegroundColor Blue
        Write-Host "Command: docker build -t $LocalImageName`:$Tag ." -ForegroundColor Gray
        
        docker build -t "$LocalImageName`:$Tag" .
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed"
        }
        Write-Host "✓ Image built successfully" -ForegroundColor Green
    } else {
        Write-Host "`nSkipping build step..." -ForegroundColor Yellow
        
        # Check if local image exists
        $imageExists = docker images --format "{{.Repository}}:{{.Tag}}" | Select-String "^$LocalImageName`:$Tag$"
        if (-not $imageExists) {
            throw "Local image '$LocalImageName`:$Tag' not found. Run without -SkipBuild to build it first."
        }
        Write-Host "✓ Local image found" -ForegroundColor Green
    }

    # Tag the image for ACR
    Write-Host "`nTagging image for Azure Container Registry..." -ForegroundColor Blue
    Write-Host "Command: docker tag $LocalImageName`:$Tag $FullImageName" -ForegroundColor Gray
    
    docker tag "$LocalImageName`:$Tag" $FullImageName
    if ($LASTEXITCODE -ne 0) {
        throw "Docker tag failed"
    }
    Write-Host "✓ Image tagged successfully" -ForegroundColor Green

    # Check ACR login status
    Write-Host "`nChecking Azure Container Registry login..." -ForegroundColor Blue
    docker images $Registry/* 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠ Not logged into ACR. Attempting login..." -ForegroundColor Yellow
        Write-Host "Command: az acr login --name breakfreetest" -ForegroundColor Gray
        
        az acr login --name breakfreetest
        if ($LASTEXITCODE -ne 0) {
            throw "Azure Container Registry login failed. Please run 'az login' first, then 'az acr login --name breakfreetest'"
        }
    }
    Write-Host "✓ ACR login verified" -ForegroundColor Green

    # Push the image
    Write-Host "`nPushing image to Azure Container Registry..." -ForegroundColor Blue
    Write-Host "Command: docker push $FullImageName" -ForegroundColor Gray
    
    docker push $FullImageName
    if ($LASTEXITCODE -ne 0) {
        throw "Docker push failed"
    }
    Write-Host "✓ Image pushed successfully" -ForegroundColor Green

    # Summary
    Write-Host "`n=== Deployment Successful ===" -ForegroundColor Cyan
    Write-Host "Image: $FullImageName" -ForegroundColor Green
    Write-Host "Registry: $Registry" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now deploy this image using:" -ForegroundColor Yellow
    Write-Host "  az container create --name smol-ai --image $FullImageName ..." -ForegroundColor Gray
    Write-Host "  or reference it in your Azure Container Apps/AKS deployments" -ForegroundColor Gray

} catch {
    Write-Host "`n❌ Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
