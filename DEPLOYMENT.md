# TuxAI Service Deployment Scripts

This folder contains scripts to build, tag, and push your Docker image to Azure Container Registry.

## Prerequisites

1. **Docker Desktop** installed and running
2. **Azure CLI** installed and logged in (`az login`)
3. **Access to breakfreetest.azurecr.io** container registry

## Quick Start

### PowerShell (Recommended)
```powershell
# Build and push with default tag "latest"
.\deploy.ps1

# Build and push with custom tag
.\deploy.ps1 -Tag "v1.0.0"

# Only tag and push existing image (skip build)
.\deploy.ps1 -Tag "dev" -SkipBuild
```

### Command Prompt
```cmd
REM Build and push with default tag "latest"
deploy.bat

REM Build and push with custom tag
deploy.bat v1.0.0

REM With custom registry
deploy.bat latest myregistry.azurecr.io
```

## Script Features

### deploy.ps1 (PowerShell)
- ✅ Parameter validation and help documentation
- ✅ Colored output for better visibility
- ✅ Error handling with detailed messages
- ✅ Skip build option for faster iterations
- ✅ ACR login verification
- ✅ Docker status checks

### deploy.bat (Batch)
- ✅ Simple command-line interface
- ✅ Basic error handling
- ✅ Cross-platform Windows compatibility
- ✅ Automatic ACR login

## Image Naming Convention

Your images will be tagged as:
```
breakfreetest.azurecr.io/smol:<tag>
```

Examples:
- `breakfreetest.azurecr.io/smol:latest`
- `breakfreetest.azurecr.io/smol:v1.0.0`
- `breakfreetest.azurecr.io/smol:dev`

## Troubleshooting

### Docker not running
```
❌ Docker is not running or not accessible
```
**Solution**: Start Docker Desktop

### ACR login failed
```
❌ Azure Container Registry login failed
```
**Solution**: 
1. Run `az login`
2. Run `az acr login --name breakfreetest`

### Build failed
```
❌ Docker build failed
```
**Solution**: Check Dockerfile syntax and ensure all required files exist

### Push failed
```
❌ Docker push failed
```
**Solution**: 
1. Verify ACR permissions
2. Check network connectivity
3. Ensure you're logged into the correct Azure subscription

## Deployment Examples

After pushing, you can deploy using:

### Azure Container Instances
```bash
az container create \
  --name smol-ai-service \
  --resource-group your-rg \
  --image breakfreetest.azurecr.io/smol:latest \
  --cpu 2 \
  --memory 4 \
  --ports 11434 \
  --environment-variables OMP_NUM_THREADS=2
```

### Azure Container Apps
```bash
az containerapp create \
  --name smol-ai-service \
  --resource-group your-rg \
  --environment your-env \
  --image breakfreetest.azurecr.io/smol:latest \
  --target-port 11434 \
  --cpu 2 \
  --memory 4Gi
```

### Kubernetes
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: smol-ai-service
spec:
  replicas: 1
  selector:
    matchLabels:
      app: smol-ai-service
  template:
    metadata:
      labels:
        app: smol-ai-service
    spec:
      containers:
      - name: smol-ai-service
        image: breakfreetest.azurecr.io/smol:latest
        ports:
        - containerPort: 11434
        resources:
          requests:
            cpu: 1
            memory: 2Gi
          limits:
            cpu: 2
            memory: 4Gi
```

## Version Management

Recommended tagging strategy:
- `latest` - Latest stable build
- `v1.0.0` - Semantic versioning for releases
- `dev` - Development builds
- `staging` - Staging environment builds
- `feature-xyz` - Feature branch builds
