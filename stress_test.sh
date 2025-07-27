#!/bin/bash

# Stress Test Script for TuxAI Service
# Makes batches of requests (10, 15, 50, 100) over 30 minutes each
# Usage: ./stress_test.sh <container_name> [endpoint_url]

set -e

# Configuration
CONTAINER_NAME="${1:-tux-ai-service}"
ENDPOINT_URL="${2:-http://localhost:11434/v1/chat/completions}"
MAX_TOKENS=250
TEST_DURATION=1800  # 30 minutes in seconds

# Test batches: number of requests to make in 30 minutes
BATCHES=(10 15 50 100)

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Create results directory
RESULTS_DIR="./stress_test_results_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$RESULTS_DIR"

log() {
    echo -e "${BLUE}[$(date '+%Y-%m-%d %H:%M:%S')]${NC} $1" | tee -a "$RESULTS_DIR/test.log"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" | tee -a "$RESULTS_DIR/test.log"
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1" | tee -a "$RESULTS_DIR/test.log"
}

warn() {
    echo -e "${YELLOW}[WARNING]${NC} $1" | tee -a "$RESULTS_DIR/test.log"
}

# Check prerequisites
check_prerequisites() {
    log "Checking prerequisites..."
    
    # Check if docker is available
    if ! command -v docker &> /dev/null; then
        error "Docker not found. Please install Docker."
        exit 1
    fi
    
    # Check if curl is available
    if ! command -v curl &> /dev/null; then
        error "curl not found. Please install curl."
        exit 1
    fi
    
    # Check if jq is available
    if ! command -v jq &> /dev/null; then
        warn "jq not found. JSON response parsing will be limited."
    fi
    
    # Check if container exists and is running
    if ! docker ps | grep -q "$CONTAINER_NAME"; then
        error "Container '$CONTAINER_NAME' is not running."
        log "Available containers:"
        docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
        exit 1
    fi
    
    success "Prerequisites check passed"
}

# Test endpoint availability
test_endpoint() {
    log "Testing endpoint availability: $ENDPOINT_URL"
    
    local response=$(curl -s -o /dev/null -w "%{http_code}" \
        -X POST "$ENDPOINT_URL" \
        -H "Content-Type: application/json" \
        -d '{
            "model": "smollm-135m",
            "messages": [{"role": "user", "content": "Hi"}],
            "max_new_tokens": 10
        }' \
        --connect-timeout 10 \
        --max-time 30)
    
    if [[ "$response" -eq 200 ]]; then
        success "Endpoint is responding (HTTP $response)"
    else
        error "Endpoint test failed (HTTP $response)"
        exit 1
    fi
}

# Make a single API request
make_request() {
    local request_id="$1"
    local batch_size="$2"
    local batch_dir="$3"
    
    local start_time=$(date +%s.%N)
    
    local response=$(curl -s -w "\n%{http_code}\n%{time_total}\n" \
        -X POST "$ENDPOINT_URL" \
        -H "Content-Type: application/json" \
        -d "{
            \"model\": \"smollm-135m\",
            \"messages\": [
                {\"role\": \"system\", \"content\": \"You are a helpful assistant.\"},
                {\"role\": \"user\", \"content\": \"Write a short story about a robot learning to paint. Keep it under $MAX_TOKENS tokens.\"}
            ],
            \"max_new_tokens\": $MAX_TOKENS,
            \"temperature\": 0.7
        }" \
        --connect-timeout 30 \
        --max-time 120)
    
    local end_time=$(date +%s.%N)
    local duration=$(echo "$end_time - $start_time" | bc -l)
    
    # Parse response (last 2 lines are status code and curl time)
    local json_response=$(echo "$response" | head -n -2)
    local http_code=$(echo "$response" | tail -n 2 | head -n 1)
    local curl_time=$(echo "$response" | tail -n 1)
    
    # Log request details
    echo "$(date '+%Y-%m-%d %H:%M:%S'),batch_$batch_size,request_$request_id,$http_code,$duration,$curl_time" >> "$batch_dir/requests.csv"
    
    if [[ "$http_code" -eq 200 ]]; then
        # Save successful response
        echo "$json_response" > "$batch_dir/response_${request_id}.json"
        
        # Extract token count if jq is available
        if command -v jq &> /dev/null; then
            local tokens=$(echo "$json_response" | jq -r '.usage.total_tokens // "N/A"' 2>/dev/null || echo "N/A")
            echo "$(date '+%Y-%m-%d %H:%M:%S'),batch_$batch_size,request_$request_id,tokens,$tokens" >> "$batch_dir/tokens.csv"
        fi
        
        log "Batch $batch_size - Request $request_id: SUCCESS (${duration}s, ${tokens:-N/A} tokens)"
    else
        error "Batch $batch_size - Request $request_id: FAILED (HTTP $http_code, ${duration}s)"
        echo "$json_response" > "$batch_dir/error_${request_id}.txt"
    fi
}

# Run stress test for a specific batch size
run_batch_test() {
    local batch_size="$1"
    local batch_dir="$RESULTS_DIR/batch_$batch_size"
    mkdir -p "$batch_dir"
    
    log "Starting batch test: $batch_size requests over 30 minutes"
    
    # Create CSV headers
    echo "timestamp,batch,request_id,http_code,total_time,curl_time" > "$batch_dir/requests.csv"
    echo "timestamp,batch,request_id,metric,value" > "$batch_dir/tokens.csv"
    
    # Calculate interval between requests
    local interval=$(echo "scale=2; $TEST_DURATION / $batch_size" | bc)
    log "Interval between requests: ${interval} seconds"
    
    local start_time=$(date +%s)
    
    for ((i=1; i<=batch_size; i++)); do
        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))
        
        if [[ $elapsed -ge $TEST_DURATION ]]; then
            warn "30-minute window exceeded, stopping batch"
            break
        fi
        
        # Make request in background
        make_request "$i" "$batch_size" "$batch_dir" &
        
        # Wait for interval, but don't exceed 30 minutes total
        if [[ $i -lt $batch_size ]]; then
            local remaining_time=$((TEST_DURATION - elapsed))
            local sleep_time=$(echo "scale=2; if ($interval < $remaining_time) $interval else $remaining_time" | bc)
            
            if (( $(echo "$sleep_time > 0" | bc -l) )); then
                sleep "$sleep_time"
            fi
        fi
    done
    
    # Wait for all background requests to complete
    wait
    
    local end_time=$(date +%s)
    local total_duration=$((end_time - start_time))
    
    success "Batch $batch_size completed in ${total_duration} seconds"
    
    # Generate batch summary
    local success_count=$(grep ",200," "$batch_dir/requests.csv" | wc -l)
    local total_requests=$(tail -n +2 "$batch_dir/requests.csv" | wc -l)
    local success_rate=$(echo "scale=2; $success_count * 100 / $total_requests" | bc -l 2>/dev/null || echo "0")
    
    echo "Batch Size: $batch_size" > "$batch_dir/summary.txt"
    echo "Total Requests: $total_requests" >> "$batch_dir/summary.txt"
    echo "Successful Requests: $success_count" >> "$batch_dir/summary.txt"
    echo "Success Rate: ${success_rate}%" >> "$batch_dir/summary.txt"
    echo "Total Duration: ${total_duration}s" >> "$batch_dir/summary.txt"
    echo "Target Duration: ${TEST_DURATION}s" >> "$batch_dir/summary.txt"
    
    log "Batch $batch_size summary: $success_count/$total_requests successful (${success_rate}%)"
}

# Main execution
main() {
    log "=== TuxAI Service Stress Test ==="
    log "Container: $CONTAINER_NAME"
    log "Endpoint: $ENDPOINT_URL"
    log "Max Tokens: $MAX_TOKENS"
    log "Results Directory: $RESULTS_DIR"
    
    check_prerequisites
    test_endpoint
    
    # Create overall summary file
    echo "timestamp,batch_size,total_requests,successful_requests,success_rate,duration" > "$RESULTS_DIR/overall_summary.csv"
    
    for batch_size in "${BATCHES[@]}"; do
        log "=== Starting Batch Test: $batch_size requests ==="
        
        local batch_start=$(date +%s)
        run_batch_test "$batch_size"
        local batch_end=$(date +%s)
        local batch_duration=$((batch_end - batch_start))
        
        # Read batch summary and add to overall summary
        local batch_dir="$RESULTS_DIR/batch_$batch_size"
        if [[ -f "$batch_dir/summary.txt" ]]; then
            local total_requests=$(grep "Total Requests:" "$batch_dir/summary.txt" | cut -d' ' -f3)
            local success_requests=$(grep "Successful Requests:" "$batch_dir/summary.txt" | cut -d' ' -f3)
            local success_rate=$(grep "Success Rate:" "$batch_dir/summary.txt" | cut -d' ' -f3 | tr -d '%')
            
            echo "$(date '+%Y-%m-%d %H:%M:%S'),$batch_size,$total_requests,$success_requests,$success_rate,$batch_duration" >> "$RESULTS_DIR/overall_summary.csv"
        fi
        
        log "=== Batch $batch_size completed, waiting 2 minutes before next batch ==="
        sleep 120  # 2-minute cooldown between batches
    done
    
    success "All stress tests completed!"
    log "Results saved to: $RESULTS_DIR"
    log "View overall summary: cat $RESULTS_DIR/overall_summary.csv"
}

# Handle Ctrl+C gracefully
trap 'error "Test interrupted by user"; exit 1' SIGINT SIGTERM

main "$@"
