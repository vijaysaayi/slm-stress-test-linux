# PowerShell script to start the TuxAI service with proper environment variables

# Set environment variables
$env:MODEL_DIR = "q:\Experiments\TuxAIService\models\smollm-135m"
$env:TGI_URL = "http://localhost:8080"
$env:TGI_TIMEOUT = "30.0"

Write-Host "Starting TuxAI Service..."
Write-Host "MODEL_DIR: $env:MODEL_DIR"
Write-Host "TGI_URL: $env:TGI_URL"
Write-Host ""
Write-Host "Note: Make sure TGI server is running on port 8080 before starting this service"
Write-Host ""

# Activate virtual environment if it exists
if (Test-Path ".venv\Scripts\Activate.ps1") {
    Write-Host "Activating virtual environment..."
    .\.venv\Scripts\Activate.ps1
}

# Start the FastAPI server
python -m uvicorn server:app --host 0.0.0.0 --port 11434 --reload
