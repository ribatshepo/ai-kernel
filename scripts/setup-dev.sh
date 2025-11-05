# Development setup script for AiKernel
# This script sets up the development environment and verifies all components

#!/bin/bash

set -e

echo "🚀 Setting up AiKernel development environment..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Check prerequisites
echo "Checking prerequisites..."

# Check Docker
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Please install Docker first."
    exit 1
fi
print_status "Docker is installed"

# Check Docker Compose
if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi
print_status "Docker Compose is installed"

# Check .NET 8
if ! command -v dotnet &> /dev/null; then
    print_error ".NET is not installed. Please install .NET 8 SDK first."
    exit 1
fi

dotnet_version=$(dotnet --version)
if [[ ! $dotnet_version =~ ^8\. ]]; then
    print_warning "Detected .NET version: $dotnet_version. .NET 8 is recommended."
else
    print_status ".NET 8 SDK is installed"
fi

# Check Git
if ! command -v git &> /dev/null; then
    print_error "Git is not installed. Please install Git first."
    exit 1
fi
print_status "Git is installed"

echo ""
echo "🔧 Setting up project..."

# Restore .NET dependencies
echo "Restoring .NET dependencies..."
if dotnet restore; then
    print_status "Dependencies restored successfully"
else
    print_error "Failed to restore dependencies"
    exit 1
fi

# Build the project
echo "Building the project..."
if dotnet build --configuration Debug; then
    print_status "Project built successfully"
else
    print_error "Failed to build project"
    exit 1
fi

# Generate gRPC code
echo "Generating gRPC code..."
if dotnet build src/Core/AiKernel.Core.csproj; then
    print_status "gRPC code generated successfully"
else
    print_warning "gRPC code generation had issues (this may be normal for initial setup)"
fi

echo ""
echo "🐳 Setting up Docker environment..."

# Build development Docker image
echo "Building development Docker image..."
if docker build -f Dockerfile.dev -t aikernel:dev .; then
    print_status "Development Docker image built successfully"
else
    print_error "Failed to build development Docker image"
    exit 1
fi

# Start development environment
echo "Starting development environment..."
if docker-compose up -d; then
    print_status "Development environment started"
else
    print_error "Failed to start development environment"
    exit 1
fi

# Wait for services to be ready
echo "Waiting for services to be ready..."
sleep 10

# Health check function
check_service() {
    local service_name=$1
    local health_url=$2
    local max_attempts=30
    local attempt=1

    echo "Checking $service_name health..."
    
    while [ $attempt -le $max_attempts ]; do
        if curl -f -s "$health_url" > /dev/null 2>&1; then
            print_status "$service_name is healthy"
            return 0
        fi
        
        echo "Attempt $attempt/$max_attempts: $service_name not ready yet..."
        sleep 2
        ((attempt++))
    done
    
    print_warning "$service_name health check failed after $max_attempts attempts"
    return 1
}

# Check service health
echo ""
echo "🏥 Performing health checks..."

# Check Redis
if check_service "Redis" "http://localhost:6379"; then
    redis_healthy=true
else
    redis_healthy=false
fi

# Check PostgreSQL
if docker-compose exec -T postgres pg_isready -U aikernel > /dev/null 2>&1; then
    print_status "PostgreSQL is healthy"
    postgres_healthy=true
else
    print_warning "PostgreSQL health check failed"
    postgres_healthy=false
fi

# Check Jaeger
if check_service "Jaeger" "http://localhost:16686"; then
    jaeger_healthy=true
else
    jaeger_healthy=false
fi

# Check Prometheus
if check_service "Prometheus" "http://localhost:9090/-/healthy"; then
    prometheus_healthy=true
else
    prometheus_healthy=false
fi

# Check Grafana
if check_service "Grafana" "http://localhost:3000/api/health"; then
    grafana_healthy=true
else
    grafana_healthy=false
fi

# Check Vault
if check_service "Vault" "http://localhost:8200/v1/sys/health"; then
    vault_healthy=true
else
    vault_healthy=false
fi

# Check NATS
if check_service "NATS" "http://localhost:8222"; then
    nats_healthy=true
else
    nats_healthy=false
fi

echo ""
echo "📊 Environment Status:"
echo "===================="

# Service status summary
services=(
    "Redis:$redis_healthy:6379"
    "PostgreSQL:$postgres_healthy:5432"
    "Jaeger:$jaeger_healthy:16686"
    "Prometheus:$prometheus_healthy:9090"
    "Grafana:$grafana_healthy:3000"
    "Vault:$vault_healthy:8200"
    "NATS:$nats_healthy:4222"
)

for service in "${services[@]}"; do
    IFS=':' read -r name healthy port <<< "$service"
    if [ "$healthy" = "true" ]; then
        print_status "$name: http://localhost:$port"
    else
        print_warning "$name: http://localhost:$port (not responding)"
    fi
done

echo ""
echo "🔑 Default Credentials:"
echo "====================="
echo "Grafana: admin / admin"
echo "Vault Token: dev_token_123"
echo "PostgreSQL: aikernel / dev_password_123"

echo ""
echo "🚀 Development Commands:"
echo "======================="
echo "Start development server:"
echo "  docker-compose exec aikernel-core dotnet watch run"
echo ""
echo "Run tests:"
echo "  dotnet test"
echo ""
echo "View logs:"
echo "  docker-compose logs -f aikernel-core"
echo ""
echo "Stop environment:"
echo "  docker-compose down"
echo ""
echo "Rebuild and restart:"
echo "  docker-compose down && docker-compose build && docker-compose up -d"

echo ""
print_status "Development environment setup complete! 🎉"

# Check if AiKernel core is responding
echo ""
echo "Checking AiKernel Core service..."
if check_service "AiKernel Core" "http://localhost:8080/health"; then
    print_status "AiKernel Core is running and healthy!"
    echo ""
    echo "🌟 You can now access:"
    echo "  - AiKernel Core API: http://localhost:8080"
    echo "  - Swagger UI: http://localhost:8080/swagger"
    echo "  - Health endpoint: http://localhost:8080/health"
else
    print_warning "AiKernel Core is not responding yet. Check logs with: docker-compose logs aikernel-core"
fi

echo ""
echo "Happy coding! 🚀"