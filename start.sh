#!/bin/bash
# RagSystem Quick Start Script

# Function to run docker compose (handling v1/v2 naming)
run_compose() {
    if docker compose version >/dev/null 2>&1; then
        docker compose "$@"
    else
        docker-compose "$@"
    fi
}

echo "--- Infrastructure Check ---"
COMPOSE_VER=$(run_compose version --short 2>/dev/null || docker-compose version --short 2>/dev/null)
echo "Detected Compose Version: $COMPOSE_VER"

if [[ "$COMPOSE_VER" == 1.* ]]; then
    echo "Warning: You are using Docker Compose V1. If you see 'ContainerConfig' errors, we will attempt an automatic recovery."
fi

# Try to start. If it fails with the common v1 bug, force a reset.
if ! run_compose up -d 2>/tmp/compose_error; then
    ERROR_MSG=$(cat /tmp/compose_error)
    echo "$ERROR_MSG"
    if [[ "$ERROR_MSG" == *"ContainerConfig"* ]]; then
        echo "Detected 'ContainerConfig' bug. Performing hard reset of containers..."
        run_compose down --remove-orphans
        run_compose up -d
    else
        echo "Error: Infrastructure failed to start. Please check the logs above."
        exit 1
    fi
fi

echo "--- Verifying Ollama ---"
if curl -s http://localhost:11434/api/tags > /dev/null; then
    echo "Ollama is active."
else
    echo "Warning: Ollama not detected. Ensure it is running on port 11434."
fi

echo "--- Building Solution ---"
dotnet build RagSystem.sln

echo "--- Launching Web Dashboard ---"
dotnet run --project Diploma.Web
