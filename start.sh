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

echo "--- Starting Docker Dependencies ---"
# Check if containers are already running to avoid the 'ContainerConfig' bug in old compose versions
if [[ $(run_compose ps --services --filter "status=running" | wc -l) -ge 2 ]]; then
    echo "Dependencies are already running."
else
    echo "Starting containers..."
    run_compose up -d || {
        echo "Error: Compose failed. Attempting to fix by resetting containers..."
        run_compose down
        run_compose up -d
    }
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
