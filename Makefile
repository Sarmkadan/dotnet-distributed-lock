.PHONY: help clean build test pack run-docker-compose docker-build docker-push

# Variables
PROJECT_NAME := SarmKadan.DistributedLock
VERSION := 1.2.0
REGISTRY ?= docker.io/sarmkadan
DOTNET := dotnet

help:
	@echo "$(PROJECT_NAME) - Distributed Locking Library for .NET"
	@echo ""
	@echo "Available targets:"
	@echo "  make clean             - Remove build artifacts and temporary files"
	@echo "  make build             - Build the project in Release configuration"
	@echo "  make test              - Run unit tests"
	@echo "  make pack              - Create NuGet package"
	@echo "  make run-examples      - Run example applications"
	@echo "  make run-docker-compose - Start development environment (Docker)"
	@echo "  make docker-build      - Build Docker image"
	@echo "  make docker-push       - Push Docker image to registry"
	@echo "  make format            - Format code with .NET formatter"
	@echo "  make lint              - Run code analysis"
	@echo "  make docs              - Generate documentation"
	@echo "  make ci                - Run full CI pipeline (clean, build, test, pack)"
	@echo ""

clean:
	@echo "Cleaning build artifacts..."
	$(DOTNET) clean -c Release
	rm -rf ./bin ./obj ./dist ./packages
	find . -name "*.dll" -o -name "*.pdb" | xargs rm -f
	@echo "✓ Clean complete"

build:
	@echo "Building $(PROJECT_NAME)..."
	$(DOTNET) build -c Release --nologo
	@echo "✓ Build complete"

test:
	@echo "Running tests..."
	$(DOTNET) test -c Release --nologo --verbosity normal
	@echo "✓ Tests complete"

pack: clean build
	@echo "Creating NuGet package..."
	mkdir -p ./packages
	$(DOTNET) pack -c Release -o ./packages --nologo
	@echo "✓ Package created in ./packages"

run-examples:
	@echo "Running example applications..."
	@echo ""
	@echo "Example 1: Basic Usage"
	@echo "---------------------"
	$(DOTNET) run --project examples/BasicExample.cs
	@echo ""

run-docker-compose:
	@echo "Starting development environment..."
	docker-compose up -d
	@echo "✓ Services started:"
	@echo "  - PostgreSQL: localhost:5432"
	@echo "  - Redis: localhost:6379"
	@echo ""
	@echo "To stop: make stop-docker-compose"

stop-docker-compose:
	@echo "Stopping development environment..."
	docker-compose down
	@echo "✓ Services stopped"

docker-build:
	@echo "Building Docker image..."
	docker build -t $(REGISTRY)/$(PROJECT_NAME):$(VERSION) .
	docker tag $(REGISTRY)/$(PROJECT_NAME):$(VERSION) $(REGISTRY)/$(PROJECT_NAME):latest
	@echo "✓ Image built: $(REGISTRY)/$(PROJECT_NAME):$(VERSION)"

docker-push: docker-build
	@echo "Pushing Docker image..."
	docker push $(REGISTRY)/$(PROJECT_NAME):$(VERSION)
	docker push $(REGISTRY)/$(PROJECT_NAME):latest
	@echo "✓ Image pushed"

format:
	@echo "Formatting code..."
	$(DOTNET) format --no-restore
	@echo "✓ Code formatted"

lint:
	@echo "Running code analysis..."
	$(DOTNET) build -c Release /p:TreatWarningsAsErrors=true --nologo
	@echo "✓ Analysis complete"

docs:
	@echo "Documentation:"
	@echo "  - README.md (comprehensive guide)"
	@echo "  - docs/GETTING_STARTED.md (quick start)"
	@echo "  - docs/ARCHITECTURE.md (design overview)"
	@echo "  - docs/API_REFERENCE.md (complete API)"
	@echo "  - docs/DEPLOYMENT.md (production setup)"
	@echo "  - docs/FAQ.md (common questions)"
	@echo "  - CHANGELOG.md (version history)"

ci: clean build test pack
	@echo "✓ Full CI pipeline complete"
	@echo ""
	@echo "Artifacts:"
	@echo "  - Built assemblies in ./bin/Release"
	@echo "  - NuGet packages in ./packages"

# Development helpers
install-tools:
	@echo "Installing development tools..."
	dotnet tool install -g dotnet-format
	dotnet tool install -g dotnet-reportgenerator-globaltool
	@echo "✓ Tools installed"

run-local:
	@echo "Building and running locally..."
	$(DOTNET) build -c Release
	@echo "✓ Build complete"
	@echo ""
	@echo "Run examples with:"
	@echo "  - make run-examples"

# Default target
.DEFAULT_GOAL := help
