#!/bin/bash
# RagSystem Production-Ready Orchestrator

# 1. Force modern Docker Compose
DOCKER_CMD="docker compose"
if ! $DOCKER_CMD version >/dev/null 2>&1; then
    echo "Error: Modern 'docker compose' (V2) not found."
    exit 1
fi

echo "--- GPU check ---"
if command -v nvidia-smi &> /dev/null; then
    nvidia-smi -L
else
    echo "Warning: NVIDIA GPU not detected. Ollama will run on CPU (Slow)."
fi

echo "--- Orchestrating Environment ---"
# --remove-orphans cleans up containers not defined in the current file
# We remove -v to preserve volumes (like AI models and Database) between restarts
$DOCKER_CMD down --remove-orphans
$DOCKER_CMD up --build -d

echo "--- Following Web Server Logs ---"
echo "System is starting. Model pulling continues in the background."
$DOCKER_CMD logs -f web
