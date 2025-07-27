# Quick Deployment Commands

## Most Common Usage

### Development Build
```powershell
# Build and push latest version
.\deploy.ps1
```

### Release Build  
```powershell
# Build and push with version tag
.\deploy.ps1 -Tag "v1.0.0"
```

### Fast Iteration (after first build)
```powershell
# Skip build, just retag and push
.\deploy.ps1 -Tag "dev" -SkipBuild
```

## Command Line (Windows)
```cmd
REM Simple deployment
deploy.bat

REM With version tag
deploy.bat v1.0.0
```

## Result
Your image will be available at:
**breakfreetest.azurecr.io/smol:\<tag\>**
