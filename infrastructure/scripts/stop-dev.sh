#!/bin/bash
# AI Kernel Infrastructure - Development Environment Shutdown Script

set -e

echo "Stopping AI Kernel Infrastructure (Development)"
echo "=============================================="

cd "$(dirname "$0")/../dev"

echo "Stopping services..."
docker compose down

echo ""
echo "Infrastructure services stopped successfully!"
echo ""
echo "To start again: ./start-dev.sh"
echo "To clean all data: docker compose down -v"
