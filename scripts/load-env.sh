#!/bin/bash

# Load Environment Variables Script
# This script loads environment variables from .env file and validates them

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
ENV_EXAMPLE="$PROJECT_ROOT/.env.example"

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

log_debug() {
    if [ "${DEBUG}" = "true" ]; then
        echo -e "${BLUE}[DEBUG]${NC} $1"
    fi
}

# Check if .env file exists
check_env_file() {
    if [ ! -f "$ENV_FILE" ]; then
        log_error ".env file not found at $ENV_FILE"

        if [ -f "$ENV_EXAMPLE" ]; then
            log_info "Found .env.example file"
            echo ""
            echo "To get started:"
            echo "  1. Copy .env.example to .env:"
            echo "     cp .env.example .env"
            echo ""
            echo "  2. Edit .env and update all CHANGE_ME_* values:"
            echo "     vi .env"
            echo ""
            echo "  3. Source this script again:"
            echo "     source scripts/load-env.sh"
            echo ""
        else
            log_error ".env.example file also not found"
        fi

        return 1
    fi

    log_info "Found .env file at $ENV_FILE"
    return 0
}

# Load environment variables from .env
load_env_file() {
    log_info "Loading environment variables from .env..."

    # Export all variables from .env
    set -a
    source "$ENV_FILE"
    set +a

    log_info "Environment variables loaded successfully"
}

# Validate required variables
validate_required_vars() {
    log_info "Validating required environment variables..."

    local missing_vars=()
    local placeholder_vars=()

    # Define required variables
    local required_vars=(
        "HARBOR_URL"
        "HARBOR_USERNAME"
        "HARBOR_PASSWORD"
        "ARTIFACTORY_URL"
        "ARTIFACTORY_USERNAME"
        "ARTIFACTORY_PASSWORD"
        "MINIO_ENDPOINT"
        "MINIO_ACCESS_KEY"
        "MINIO_SECRET_KEY"
    )

    # Check if required variables are set
    for var in "${required_vars[@]}"; do
        if [ -z "${!var}" ]; then
            missing_vars+=("$var")
        elif [[ "${!var}" == CHANGE_ME_* ]]; then
            placeholder_vars+=("$var")
        fi
    done

    # Report missing variables
    if [ ${#missing_vars[@]} -gt 0 ]; then
        log_error "Missing required environment variables:"
        for var in "${missing_vars[@]}"; do
            echo "  - $var"
        done
        return 1
    fi

    # Report placeholder values
    if [ ${#placeholder_vars[@]} -gt 0 ]; then
        log_warn "The following variables still have placeholder values:"
        for var in "${placeholder_vars[@]}"; do
            echo "  - $var = ${!var}"
        done
        echo ""
        log_warn "Please update these values in .env file before deploying to production"

        if [ "${ENVIRONMENT}" = "production" ]; then
            log_error "Cannot deploy to production with placeholder values"
            return 1
        fi
    fi

    log_info "All required variables are set"
    return 0
}

# Test external services connectivity
test_connectivity() {
    if [ "${SKIP_CONNECTIVITY_TEST}" = "true" ]; then
        log_info "Skipping connectivity tests (SKIP_CONNECTIVITY_TEST=true)"
        return 0
    fi

    log_info "Testing connectivity to external services..."

    local failed_tests=()

    # Test Harbor
    log_debug "Testing Harbor at ${HARBOR_URL}..."
    if curl -sSf -k "${HARBOR_URL}/api/v2.0/systeminfo" > /dev/null 2>&1; then
        log_info "✓ Harbor is reachable"
    else
        log_warn "✗ Harbor is not reachable at ${HARBOR_URL}"
        failed_tests+=("Harbor")
    fi

    # Test Artifactory
    log_debug "Testing Artifactory at ${ARTIFACTORY_URL}..."
    if curl -sSf -k "${ARTIFACTORY_URL}/artifactory/api/system/ping" > /dev/null 2>&1; then
        log_info "✓ Artifactory is reachable"
    else
        log_warn "✗ Artifactory is not reachable at ${ARTIFACTORY_URL}"
        failed_tests+=("Artifactory")
    fi

    # Test MinIO
    log_debug "Testing MinIO at ${MINIO_ENDPOINT}..."
    local minio_url="https://${MINIO_ENDPOINT}/minio/health/live"
    if [ "${MINIO_USE_SSL}" != "true" ]; then
        minio_url="http://${MINIO_ENDPOINT}/minio/health/live"
    fi

    if curl -sSf -k "${minio_url}" > /dev/null 2>&1; then
        log_info "✓ MinIO is reachable"
    else
        log_warn "✗ MinIO is not reachable at ${MINIO_ENDPOINT}"
        failed_tests+=("MinIO")
    fi

    # Report connectivity test results
    if [ ${#failed_tests[@]} -gt 0 ]; then
        log_warn "Some services are not reachable. This is OK if:"
        log_warn "  - Services are not started yet"
        log_warn "  - Running from a network without access to bare-metal servers"
        log_warn "  - DNS/hosts file not configured yet"
        echo ""
        log_warn "Failed connectivity tests:"
        for service in "${failed_tests[@]}"; do
            echo "  - $service"
        done
        return 0  # Don't fail, just warn
    fi

    log_info "All connectivity tests passed"
    return 0
}

# Export environment variables for Kubernetes
export_k8s_vars() {
    log_info "Exporting Kubernetes environment variables..."

    # Set KUBECONFIG based on environment
    case "${ENVIRONMENT}" in
        development)
            if [ -n "${KUBECONFIG_DEV}" ]; then
                export KUBECONFIG="${KUBECONFIG_DEV}"
                log_info "Using development kubeconfig: ${KUBECONFIG_DEV}"
            fi
            ;;
        staging)
            if [ -n "${KUBECONFIG_STAGING}" ]; then
                export KUBECONFIG="${KUBECONFIG_STAGING}"
                log_info "Using staging kubeconfig: ${KUBECONFIG_STAGING}"
            fi
            ;;
        production)
            if [ -n "${KUBECONFIG_PROD}" ]; then
                export KUBECONFIG="${KUBECONFIG_PROD}"
                log_info "Using production kubeconfig: ${KUBECONFIG_PROD}"
            fi
            ;;
    esac
}

# Display loaded configuration
display_config() {
    if [ "${VERBOSE}" != "true" ]; then
        return 0
    fi

    echo ""
    echo -e "${BLUE}╔══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║         Loaded Environment Configuration                 ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo -e "${GREEN}Environment:${NC} ${ENVIRONMENT}"
    echo -e "${GREEN}Namespace:${NC} ${KUBE_NAMESPACE}"
    echo ""
    echo -e "${GREEN}External Services:${NC}"
    echo -e "  Harbor:      ${HARBOR_URL}"
    echo -e "  Artifactory: ${ARTIFACTORY_URL}"
    echo -e "  MinIO:       ${MINIO_ENDPOINT}"
    echo ""
    echo -e "${GREEN}Kubernetes:${NC}"
    echo -e "  Context:     ${KUBE_CONTEXT}"
    if [ -n "${KUBECONFIG}" ]; then
        echo -e "  Config:      ${KUBECONFIG}"
    fi
    echo ""
    echo -e "${GREEN}Feature Flags:${NC}"
    echo -e "  Skip Istio:     ${SKIP_ISTIO}"
    echo -e "  Skip ArgoCD:    ${SKIP_ARGOCD}"
    echo -e "  Skip Dashboard: ${SKIP_DASHBOARD}"
    echo -e "  Dry Run:        ${DRY_RUN}"
    echo ""
}

# Main execution
main() {
    echo ""
    echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}   AI Kernel Platform - Environment Configuration Loader${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
    echo ""

    # Check and load .env file
    if ! check_env_file; then
        return 1
    fi

    load_env_file

    # Validate configuration
    if ! validate_required_vars; then
        log_error "Environment validation failed"
        return 1
    fi

    # Test connectivity (non-blocking)
    test_connectivity

    # Export Kubernetes variables
    export_k8s_vars

    # Display configuration
    display_config

    echo ""
    log_info "Environment configuration loaded successfully!"
    echo ""
    echo "You can now run deployment scripts with loaded configuration."
    echo ""

    return 0
}

# Run main if script is executed (not sourced)
if [ "${BASH_SOURCE[0]}" = "${0}" ]; then
    main "$@"
    exit $?
else
    # Script is being sourced
    main "$@"
fi
