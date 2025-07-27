# Port Configuration

## Current Port: 11434

The TuxAI Service now runs on port **11434** instead of the default 8000.

### Local Development
- Server URL: `http://localhost:11434`
- Health check: `http://localhost:11434/health`
- API endpoint: `http://localhost:11434/v1/chat/completions`

### Docker Run
```bash
docker run -p 11434:11434 your-image
```

### Why Port 11434?
Port 11434 is commonly used by Ollama and other AI inference services, making it a good choice for AI model hosting to avoid conflicts with other web services that typically use port 8000.

### Testing
Use the test file `test/test.http` which is already configured for port 11434.
