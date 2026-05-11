#!/bin/bash
# RagSystem Quick Start Script

echo "--- Starting Docker Dependencies ---"
docker-compose up -d

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
