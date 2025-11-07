#!/bin/bash

# Apply Kubernetes Manifests with Environment Variable Substitution
# This script loads .env file and applies K8s manifests with variable substitution

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ENV_FILE="$PROJECT_ROOT/.env"

# Function to print messages
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Usage information
usage() {
    echo "Usage: $0 <manifest-file> [kubectl-args]"
    echo ""
    echo "Applies Kubernetes manifest with environment variable substitution from .env"
    echo ""
    echo "Arguments:"
    echo "  <manifest-file>  Path to Kubernetes manifest file (required)"
    echo "  [kubectl-args]   Additional arguments to pass to kubectl (optional)"
    echo ""
    echo "Examples:"
    echo "  $0 infrastructure/prod/k8s/01-postgres-secret.yaml"
    echo "  $0 infrastructure/prod/k8s/27-external-services-config.yaml --dry-run=client"
    echo "  $0 infrastructure/prod/k8s/01-postgres-secret.yaml -n custom-namespace"
    echo ""
    echo "Environment:"
    echo "  Requires .env file in project root with all required variables"
    echo "  Run 'source scripts/load-env.sh' first to validate configuration"
    exit 1
}

# Check arguments
if [ $# -lt 1 ]; then
    usage
fi

MANIFEST_FILE="$1"
shift
KUBECTL_ARGS="$@"

# Check if manifest file exists
if [ ! -f "$MANIFEST_FILE" ]; then
    log_error "Manifest file not found: $MANIFEST_FILE"
    exit 1
fi

# Check if .env file exists
if [ ! -f "$ENV_FILE" ]; then
    log_error ".env file not found at $ENV_FILE"
    echo ""
    echo "To get started:"
    echo "  1. Copy .env.example to .env:"
    echo "     cp .env.example .env"
    echo ""
    echo "  2. Edit .env and update all CHANGE_ME_* values"
    echo ""
    echo "  3. Load and validate environment:"
    echo "     source scripts/load-env.sh"
    exit 1
fi

# Load environment variables
log_info "Loading environment variables from .env..."
set -a
source "$ENV_FILE"
set +a

# Check for required tool
if ! command -v envsubst &> /dev/null; then
    log_error "envsubst command not found"
    echo ""
    echo "Please install gettext package:"
    echo "  Ubuntu/Debian: sudo apt-get install gettext-base"
    echo "  CentOS/RHEL:   sudo yum install gettext"
    echo "  macOS:         brew install gettext"
    exit 1
fi

# Apply manifest with environment variable substitution
log_info "Applying manifest: $MANIFEST_FILE"
log_info "kubectl args: ${KUBECTL_ARGS:-none}"

# Substitute environment variables and apply
if envsubst < "$MANIFEST_FILE" | kubectl apply $KUBECTL_ARGS -f -; then
    log_info "Successfully applied $MANIFEST_FILE"
else
    log_error "Failed to apply $MANIFEST_FILE"
    exit 1
fi
