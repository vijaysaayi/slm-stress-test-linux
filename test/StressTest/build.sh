#!/bin/bash

# TuxAI Service Stress Test Build Script
# This script builds a self-contained executable for Linux

echo "🔨 Building TuxAI Stress Test Tool for Linux..."
echo "=============================================="

# Navigate to the project directory
cd "$(dirname "$0")"

# Clean previous builds
echo "🧹 Cleaning previous builds..."
dotnet clean > /dev/null 2>&1

# Restore packages
echo "📦 Restoring NuGet packages..."
dotnet restore

if [ $? -ne 0 ]; then
    echo "❌ Failed to restore packages"
    exit 1
fi

# Build and publish
echo "🚀 Building self-contained Linux executable..."
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

# Get the output path
OUTPUT_PATH="bin/Release/net9.0/linux-x64/publish/StressTest"

if [ -f "$OUTPUT_PATH" ]; then
    echo "✅ Build successful!"
    echo "📁 Executable location: $OUTPUT_PATH"
    echo "📊 File size: $(du -h "$OUTPUT_PATH" | cut -f1)"
    echo ""
    echo "🚚 To deploy to your Linux VM:"
    echo "   scp \"$OUTPUT_PATH\" user@your-vm:/path/to/destination/"
    echo ""
    echo "🏃 To run on Linux:"
    echo "   chmod +x StressTest"
    echo "   ./StressTest --help"
else
    echo "❌ Build completed but executable not found"
    exit 1
fi
