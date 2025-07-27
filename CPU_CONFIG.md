# AI Service CPU Configuration Guide

## Current Configuration
The Dockerfile now uses conservative defaults to prevent CPU saturation:
- OMP_NUM_THREADS=2
- OPENBLAS_NUM_THREADS=2  
- MKL_NUM_THREADS=2
- Single Uvicorn worker

## Runtime Overrides

### For Development (local machine):
```bash
docker run -p 11434:11434 \
  -e OMP_NUM_THREADS=2 \
  -e OPENBLAS_NUM_THREADS=2 \
  your-ai-service
```

### For Production (dedicated server):
```bash
docker run -p 11434:11434 \
  -e OMP_NUM_THREADS=4 \
  -e OPENBLAS_NUM_THREADS=4 \
  --cpus="4.0" \
  your-ai-service
```

### For Resource-Constrained Environments:
```bash
docker run -p 11434:11434 \
  -e OMP_NUM_THREADS=1 \
  -e OPENBLAS_NUM_THREADS=1 \
  --cpus="1.0" \
  --memory="2g" \
  your-ai-service
```

## Monitoring CPU Usage

Monitor your container's CPU usage:
```bash
docker stats your-container-name
```

## Recommended Settings by Environment

| Environment | CPU Cores | OMP_THREADS | WORKERS | CPU Limit |
|-------------|-----------|-------------|---------|-----------|
| Development | 2-4       | 1-2         | 1       | 2.0       |
| Staging     | 4-8       | 2-4         | 1       | 4.0       |
| Production  | 8+        | 4-6         | 1-2     | 6.0       |

## Notes
- AI inference is often better with fewer workers and more threads per worker
- Monitor actual usage and adjust based on your specific workload
- The startup script automatically calculates optimal settings
