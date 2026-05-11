# RagSystem Orchestration Makefile

.PHONY: help up down status logs ollama-check build run

help:
	@echo "RagSystem Management Commands:"
	@echo "  make up           - Start Docker dependencies (Postgres, Qdrant)"
	@echo "  make down         - Stop Docker dependencies"
	@echo "  make status       - Check status of all dependencies"
	@echo "  make ollama-check - Verify Ollama is running and accessible"
	@echo "  make build        - Build the .NET solution"
	@echo "  make run          - Run the Web application"

up:
	docker-compose up -d
	@echo "Docker dependencies started."

down:
	docker-compose down

status:
	docker-compose ps
	@echo "\nChecking Ollama..."
	@curl -s http://localhost:11434/api/tags > /dev/null && echo "Ollama: RUNNING" || echo "Ollama: NOT RUNNING"

ollama-check:
	@curl -s http://localhost:11434/api/tags > /dev/null && echo "Ollama is active on port 11434." || (echo "Error: Ollama not found. Ensure the service is started." && exit 1)

build:
	dotnet build RagSystem.sln

run: ollama-check
	dotnet run --project Diploma.Web
