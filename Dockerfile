# Use a slim Python base for a minimal runtime
FROM python:3.10-slim

# Tune CPU threading for better resource management
# Limit threads to prevent CPU saturation when using multiple workers
# Set to a reasonable default that can be overridden at runtime
ENV OMP_NUM_THREADS=2
ENV OPENBLAS_NUM_THREADS=2
ENV MKL_NUM_THREADS=2

# Set working directory
WORKDIR /app                                              

# Copy and install Python dependencies, including the TGI client
COPY requirements.txt .                                  
RUN pip install --no-cache-dir -r requirements.txt       

# Copy your FastAPI proxy code and startup script
COPY server.py .
COPY start.sh .
RUN chmod +x start.sh                                         

# Copy only the needed model files
COPY models/smollm-135m/model.safetensors  /app/models/  
COPY models/smollm-135m/config.json         /app/models/  
COPY models/smollm-135m/tokenizer.json      /app/models/  
COPY models/smollm-135m/tokenizer_config.json   /app/models/  
COPY models/smollm-135m/special_tokens_map.json /app/models/

# Expose the API port
EXPOSE 11434                                              

# Use the startup script for dynamic resource allocation
CMD ["./start.sh"]
