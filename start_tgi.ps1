# PowerShell script to start TGI server using Docker

$MODEL_PATH = "q:\Experiments\TuxAIService\models\smollm-135m"
$PORT = "8080"

Write-Host "Starting TGI (Text Generation Inference) server..."
Write-Host "Model: $MODEL_PATH"
Write-Host "Port: $PORT"
Write-Host ""

# Pull the TGI Docker image if not already present
Write-Host "Pulling TGI Docker image (this may take a while on first run)..."
docker pull ghcr.io/huggingface/text-generation-inference:latest

# Start TGI server
Write-Host "Starting TGI server..."
docker run --rm -it `
    --gpus all `
    -p ${PORT}:80 `
    -v "${MODEL_PATH}:/data" `
    -e MODEL_ID=/data `
    ghcr.io/huggingface/text-generation-inference:latest

# Note: Remove --gpus all if you don't have NVIDIA GPU support
