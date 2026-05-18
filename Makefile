# RagSystem Orchestration Makefile

DOCKER_CMD := $(shell command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1 && echo "docker compose" || (command -v docker-compose >/dev/null 2>&1 && echo "docker-compose" || echo "exit 1;"))

.PHONY: help start up down status gpu-check build run tests

help:
	@echo "RagSystem Management Commands:"
	@echo "  make start        - PRODUCTION: Full reset, rebuild, and start in Docker (mimics start.sh)"
	@echo "  make up           - Start Docker dependencies in background"
	@echo "  make down         - Stop and remove all containers"
	@echo "  make status       - Check health of system and AI services"
	@echo "  make gpu-check    - Verify NVIDIA GPU availability for AI acceleration"
	@echo "  make build        - LOCAL: Build the .NET solution"
	@echo "  make tests        - LOCAL: Run all unit and integration tests"
	@echo "  make run          - LOCAL: Run the Web application directly"
	@echo "  make train-ml     - LOCAL: Manually train the Intent Classification model"
	@echo "  make inspect-ml   - LOCAL: Interactively test the trained ML model"

start: gpu-check
	@echo "--- Orchestrating Production Environment ---"
	@if [ "$(DOCKER_CMD)" = "exit 1;" ]; then echo "Error: Docker Compose not found."; exit 1; fi
	$(DOCKER_CMD) down --remove-orphans
	$(DOCKER_CMD) up --build -d
	@echo "--- Following Web Server Logs ---"
	$(DOCKER_CMD) logs -f web

up:
	@echo "Starting containers..."
	$(DOCKER_CMD) up -d

down:
	$(DOCKER_CMD) down --remove-orphans

status:
	$(DOCKER_CMD) ps
	@echo "\n--- AI Service Health ---"
	@curl -s http://localhost:11434/api/tags > /dev/null && echo "Ollama:  [RUNNING]" || echo "Ollama:  [NOT RUNNING]"
	@curl -s http://localhost:6333/dashboard > /dev/null && echo "Qdrant:  [RUNNING]" || echo "Qdrant:  [NOT RUNNING]"

gpu-check:
	@echo "--- GPU check ---"
	@if command -v nvidia-smi &> /dev/null; then \
		nvidia-smi -L; \
	else \
		echo "Warning: NVIDIA GPU not detected. AI will run on CPU (Slow)."; \
	fi

build:
	dotnet build RagSystem.sln

tests:
	dotnet test RagSystem.sln

run:
	@curl -s http://localhost:11434/api/tags > /dev/null || (echo "Error: Ollama not found on port 11434." && exit 1)
	dotnet run --project Diploma.Web

train-ml:
	@echo "--- Starting Manual ML Training ---"
	dotnet run --project Diploma.TrainModel
	@echo "--- Model check ---"
	@ls -lh Diploma.Web/Data/intent_model.zip || echo "Warning: Model file not found."

inspect-ml:
	@echo "--- Starting ML Model Inspector ---"
	dotnet run --project Diploma.TrainModel -- inspect
