#!/bin/bash

# Get number of CPU cores
CORES=$(nproc)

# Calculate optimal threading settings
# For AI inference, we typically want:
# - Fewer threads per library to avoid contention
# - Leave some cores for the OS and other processes
if [ $CORES -le 2 ]; then
    # Low-core systems: use 1 thread
    THREADS=1
    WORKERS=1
elif [ $CORES -le 4 ]; then
    # Medium systems: use 2 threads
    THREADS=2
    WORKERS=1
elif [ $CORES -le 8 ]; then
    # Higher-end systems: use more threads but leave headroom
    THREADS=$((CORES / 2))
    WORKERS=1
else
    # High-core systems: cap threads and consider multiple workers
    THREADS=4
    WORKERS=2
fi

echo "Detected $CORES CPU cores"
echo "Setting thread count to: $THREADS"
echo "Setting worker count to: $WORKERS"

# Set environment variables
export OMP_NUM_THREADS=$THREADS
export OPENBLAS_NUM_THREADS=$THREADS
export MKL_NUM_THREADS=$THREADS

# Start the server
exec uvicorn server:app --host 0.0.0.0 --port 11434 --workers $WORKERS
