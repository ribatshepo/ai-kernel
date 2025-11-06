#!/bin/bash
# AI Kernel Infrastructure - Development Environment Startup Script

set -e

echo "Starting AI Kernel Infrastructure (Development)"
echo "=============================================="

cd "$(dirname "$0")/../dev"

echo "Pulling latest images..."
docker compose pull

echo "Starting infrastructure services..."
docker compose up -d

echo "Waiting for services to be healthy..."
sleep 10

echo ""
echo "Service Status:"
echo "=============================================="
docker compose ps

echo ""
echo "Infrastructure Services Started Successfully!"
echo ""
echo "Access URLs:"
echo "  PostgreSQL:      localhost:5432"
echo "  Neo4j Browser:   http://localhost:7474"
echo "  Elasticsearch:   http://localhost:9200"
echo "  Kibana:          http://localhost:5601"
echo "  Redis:           localhost:6379"
echo "  Kafka:           localhost:29092"
echo "  Vault:           http://localhost:8200"
echo "  Prometheus:      http://localhost:9090"
echo "  Grafana:         http://localhost:3000 (admin/aikernel_dev_password)"
echo "  Jaeger UI:       http://localhost:16686"
echo ""
echo "To view logs: docker compose logs -f [service-name]"
echo "To stop:      docker compose down"
echo "To clean:     docker compose down -v (WARNING: Deletes all data)"
