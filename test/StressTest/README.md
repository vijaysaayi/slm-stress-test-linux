# TuxAI Service Stress Testing Tool

A comprehensive .NET 9 application for stress testing your TuxAI service with **executive-ready HTML reports** and real-time performance monitoring.

## Features

✅ **Executive HTML Reports**: Beautiful, interactive reports perfect for CEO presentations  
✅ **Load Testing**: Configurable requests-per-minute testing with precise timing  
✅ **Performance Monitoring**: Real-time CPU, memory, and disk usage tracking  
✅ **Container Monitoring**: Docker container resource monitoring  
✅ **VM Monitoring**: Host system resource monitoring  
✅ **Decision Metrics**: Automated performance analysis and deployment recommendations  
✅ **Interactive Analysis**: Click any request to see detailed response data  
✅ **Multiple Formats**: HTML (executive), JSON, and CSV output formats  
✅ **Single Executable**: Self-contained Linux x64 binary  

## New: HTML Executive Reports

The stress test now generates a beautiful HTML report (`stress_test_report.html`) designed specifically for executive presentations and infrastructure decision-making. The report includes:

- **📊 Executive Dashboard**: Key metrics, success rates, and performance indicators
- **🎯 Smart Recommendations**: Automated go/no-go deployment decisions
- **📈 Interactive Charts**: Response time distribution, resource usage, and timeline analysis
- **🔍 Request Drill-down**: Click any request to see full request/response details
- **💼 Business Metrics**: Cost analysis, scalability assessment, and resource utilization  

## Quick Start

### 1. Build the Application

```bash
# On Windows (for Linux target)
cd test/StressTest
dotnet publish -c Release -r linux-x64 --self-contained true

# Copy the executable to your Linux VM
scp bin/Release/net9.0/linux-x64/publish/StressTest user@your-vm:/path/to/stresstest
```

### 2. Run on Linux VM

```bash
# Make executable
chmod +x StressTest

# Basic test (default settings)
./StressTest

# Custom configuration
./StressTest -u http://localhost:11434 -d 30 -t 250
```

## Command Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `-u, --url` | Service URL | http://localhost:11434 |
| `-c, --container` | Container name for monitoring | tux-ai-service |
| `-d, --duration` | Test duration per batch (minutes) | 30 |
| `-t, --tokens` | Max tokens per request | 250 |
| `-b, --batches` | Comma-separated batch sizes | 10,15,50,100 |
| `-o, --output` | Output directory | ./results |
| `--no-container` | Disable container monitoring | false |
| `--no-vm` | Disable VM monitoring | false |

## Test Scenarios

### Default Test Plan
1. **Batch 1**: 10 requests over 30 minutes (1 request every 3 minutes)
2. **Batch 2**: 15 requests over 30 minutes (1 request every 2 minutes)  
3. **Batch 3**: 50 requests over 30 minutes (1 request every 36 seconds)
4. **Batch 4**: 100 requests over 30 minutes (1 request every 18 seconds)

### Test Prompts
The tool uses 10 different AI prompts to simulate realistic usage:
- Scientific questions (Mars moons, photosynthesis)
- Educational content (water cycle, colors)
- Technical explanations (AI, digestive system)
- General knowledge (geography, energy sources)

## Output Files

### JSON Results
- `batch_10_20250126_143022.json` - Detailed batch results
- `stress_test_summary_20250126_150045.json` - Combined summary

### CSV Summary  
- `stress_test_summary_20250126_150045.csv` - Easy analysis format

### Sample Metrics Tracked
```json
{
  "batchSize": 50,
  "statistics": {
    "totalRequests": 50,
    "successfulRequests": 48,
    "successRate": 96.0,
    "averageResponseTime": "00:00:02.150",
    "requestsPerSecond": 0.027,
    "tokensPerSecond": 6.75,
    "averageCpuUsage": 45.2,
    "maxCpuUsage": 78.5,
    "averageMemoryUsage": 1024,
    "maxMemoryUsage": 1536
  }
}
```

## Performance Monitoring

### Container Metrics (via Docker)
- CPU usage percentage
- Memory consumption (MB)
- Disk I/O (MB)

### VM/Host Metrics (via Linux commands)
- Overall CPU utilization
- System memory usage
- Root filesystem usage

## Example Usage Scenarios

### Quick Test (5-minute batches)
```bash
./StressTest -d 5 -b 5,10,20
```

### High-Load Test (custom endpoint)
```bash
./StressTest -u http://192.168.1.100:11434 -b 25,50,100,200 -t 500
```

### Container-Only Monitoring
```bash
./StressTest --no-vm -c my-ai-container
```

### Custom Output Location
```bash
./StressTest -o /tmp/ai-test-results
```

## Interpreting Results

### Key Metrics to Watch

1. **Success Rate**: Should be >95% for good performance
2. **Response Time**: Lower is better (target <3 seconds)
3. **Requests/Second**: Higher throughput indicates better performance
4. **CPU Usage**: Monitor for consistent performance under load
5. **Memory Usage**: Watch for memory leaks or excessive consumption

### Performance Indicators

| Metric | Good | Warning | Critical |
|--------|------|---------|----------|
| Success Rate | >95% | 90-95% | <90% |
| Avg Response Time | <2s | 2-5s | >5s |
| CPU Usage | <70% | 70-85% | >85% |
| Memory Growth | Stable | Gradual | Rapid increase |

## Troubleshooting

### Common Issues

**Service not accessible**
```bash
# Check if container is running
docker ps | grep tux-ai-service

# Test endpoint manually
curl http://localhost:11434/health
```

**Permission denied**
```bash
chmod +x StressTest
```

**Docker stats not working**
```bash
# Ensure user can access Docker
sudo usermod -aG docker $USER
# Logout and login again
```

## Building from Source

```bash
# Restore packages
dotnet restore

# Build for Linux
dotnet publish -c Release -r linux-x64 --self-contained true

# Build for current platform
dotnet run -- --help
```

## Integration with CI/CD

You can integrate this tool into your deployment pipeline:

```bash
# Run stress test and check results
./StressTest -d 10 -b 20,50
if [ $? -eq 0 ]; then
    echo "Stress test passed"
else
    echo "Stress test failed"
    exit 1
fi
```

## Support

This tool provides comprehensive insights into your TuxAI service performance under various load conditions, helping you:

- Validate service stability
- Identify performance bottlenecks  
- Monitor resource consumption
- Plan capacity requirements
- Ensure SLA compliance
