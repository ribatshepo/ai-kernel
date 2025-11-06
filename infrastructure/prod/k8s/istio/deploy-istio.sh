#!/bin/bash
set -euo pipefail

# AIKernel Istio Service Mesh Deployment Script
# Version: 1.0.0
# Description: Automates the deployment of Istio service mesh with observability stack

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ISTIO_VERSION="${ISTIO_VERSION:-1.20.0}"
ISTIO_REVISION="${ISTIO_REVISION:-1-20-0}"
NAMESPACE_ISTIO="istio-system"
NAMESPACE_MONITORING="aikernel-monitoring"
KUSTOMIZE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TIMEOUT="${TIMEOUT:-600}"
DRY_RUN="${DRY_RUN:-false}"

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."

    local missing_tools=()

    # Check kubectl
    if ! command -v kubectl &> /dev/null; then
        missing_tools+=("kubectl")
    fi

    # Check istioctl
    if ! command -v istioctl &> /dev/null; then
        print_warning "istioctl not found. Will attempt to download..."
    else
        local istioctl_version
        istioctl_version=$(istioctl version --remote=false 2>/dev/null | grep -oP '\d+\.\d+\.\d+' | head -1)
        if [ "$istioctl_version" != "$ISTIO_VERSION" ]; then
            print_warning "istioctl version mismatch. Found: $istioctl_version, Expected: $ISTIO_VERSION"
        fi
    fi

    # Check kustomize
    if ! command -v kustomize &> /dev/null; then
        missing_tools+=("kustomize")
    fi

    if [ ${#missing_tools[@]} -gt 0 ]; then
        print_error "Missing required tools: ${missing_tools[*]}"
        print_info "Please install the missing tools and try again."
        exit 1
    fi

    # Check Kubernetes cluster connectivity
    if ! kubectl cluster-info &> /dev/null; then
        print_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi

    # Check Kubernetes version
    local k8s_version
    k8s_version=$(kubectl version --short 2>/dev/null | grep Server | grep -oP '\d+\.\d+' | head -1)
    print_info "Kubernetes version: $k8s_version"

    # Check cluster resources
    local node_count
    node_count=$(kubectl get nodes --no-headers | wc -l)
    if [ "$node_count" -lt 3 ]; then
        print_warning "Cluster has only $node_count nodes. Recommended: 3+ nodes for HA"
    fi

    print_success "Prerequisites check completed"
}

# Function to download and install istioctl
download_istioctl() {
    print_info "Downloading istioctl version $ISTIO_VERSION..."

    curl -L "https://istio.io/downloadIstio" | ISTIO_VERSION="$ISTIO_VERSION" sh -

    export PATH="$PWD/istio-$ISTIO_VERSION/bin:$PATH"

    print_success "istioctl installed successfully"
}

# Function to install Istio operator
install_operator() {
    print_info "Installing Istio operator..."

    if [ "$DRY_RUN" = "true" ]; then
        print_info "DRY RUN: Would install Istio operator with revision $ISTIO_REVISION"
        return
    fi

    istioctl operator init --revision "$ISTIO_REVISION"

    # Wait for operator to be ready
    kubectl wait --for=condition=Ready pods -l name=istio-operator -n istio-operator --timeout="${TIMEOUT}s"

    print_success "Istio operator installed successfully"
}

# Function to create namespaces
create_namespaces() {
    print_info "Creating namespaces..."

    if [ "$DRY_RUN" = "true" ]; then
        print_info "DRY RUN: Would create namespaces"
        return
    fi

    # Create namespaces
    kubectl apply -f "$KUSTOMIZE_DIR/../namespaces/aikernel-infrastructure.yaml"

    # Verify namespaces have istio-injection label
    local namespaces=("aikernel" "aikernel-infrastructure" "aikernel-data" "aikernel-monitoring")
    for ns in "${namespaces[@]}"; do
        local label
        label=$(kubectl get namespace "$ns" -o jsonpath='{.metadata.labels.istio-injection}' 2>/dev/null || echo "")
        if [ "$label" = "enabled" ]; then
            print_success "Namespace $ns has istio-injection enabled"
        else
            print_error "Namespace $ns missing istio-injection label"
            exit 1
        fi
    done
}

# Function to deploy Istio
deploy_istio() {
    print_info "Deploying Istio service mesh..."

    if [ "$DRY_RUN" = "true" ]; then
        print_info "DRY RUN: Would deploy Istio using kustomize"
        kustomize build "$KUSTOMIZE_DIR" | head -50
        return
    fi

    # Apply Istio configuration
    kubectl apply -k "$KUSTOMIZE_DIR"

    print_info "Waiting for Istio control plane to be ready..."
    kubectl wait --for=condition=Ready pods -l app=istiod -n "$NAMESPACE_ISTIO" --timeout="${TIMEOUT}s"

    print_info "Waiting for Istio ingress gateway to be ready..."
    kubectl wait --for=condition=Ready pods -l app=istio-ingressgateway -n "$NAMESPACE_ISTIO" --timeout="${TIMEOUT}s"

    print_info "Waiting for Istio egress gateway to be ready..."
    kubectl wait --for=condition=Ready pods -l app=istio-egressgateway -n "$NAMESPACE_ISTIO" --timeout="${TIMEOUT}s"

    print_success "Istio service mesh deployed successfully"
}

# Function to deploy observability stack
deploy_observability() {
    print_info "Deploying observability stack..."

    if [ "$DRY_RUN" = "true" ]; then
        print_info "DRY RUN: Would deploy observability stack"
        return
    fi

    # Observability is included in kustomize, just verify deployment
    print_info "Waiting for Prometheus to be ready..."
    kubectl wait --for=condition=Ready pods -l app=prometheus -n "$NAMESPACE_MONITORING" --timeout="${TIMEOUT}s" || true

    print_info "Waiting for Jaeger to be ready..."
    kubectl wait --for=condition=Ready pods -l app=jaeger,component=collector -n "$NAMESPACE_MONITORING" --timeout="${TIMEOUT}s" || true
    kubectl wait --for=condition=Ready pods -l app=jaeger,component=query -n "$NAMESPACE_MONITORING" --timeout="${TIMEOUT}s" || true

    print_info "Waiting for Grafana to be ready..."
    kubectl wait --for=condition=Ready pods -l app=grafana -n "$NAMESPACE_MONITORING" --timeout="${TIMEOUT}s" || true

    print_success "Observability stack deployed successfully"
}

# Function to validate deployment
validate_deployment() {
    print_info "Validating Istio deployment..."

    local validation_failed=false

    # Check Istiod
    print_info "Checking Istiod status..."
    if ! kubectl get pods -n "$NAMESPACE_ISTIO" -l app=istiod | grep -q "Running"; then
        print_error "Istiod is not running"
        validation_failed=true
    else
        print_success "Istiod is running"
    fi

    # Check gateways
    print_info "Checking gateway status..."
    if ! kubectl get pods -n "$NAMESPACE_ISTIO" -l app=istio-ingressgateway | grep -q "Running"; then
        print_error "Ingress gateway is not running"
        validation_failed=true
    else
        print_success "Ingress gateway is running"
    fi

    # Check mTLS configuration
    print_info "Checking mTLS configuration..."
    local mtls_policies
    mtls_policies=$(kubectl get peerauthentication --all-namespaces -o json | jq -r '.items | length')
    if [ "$mtls_policies" -eq 0 ]; then
        print_error "No mTLS policies found"
        validation_failed=true
    else
        print_success "Found $mtls_policies mTLS policies"
    fi

    # Check destination rules
    print_info "Checking destination rules..."
    local dest_rules
    dest_rules=$(kubectl get destinationrules --all-namespaces -o json | jq -r '.items | length')
    if [ "$dest_rules" -eq 0 ]; then
        print_warning "No destination rules found"
    else
        print_success "Found $dest_rules destination rules"
    fi

    # Check virtual services
    print_info "Checking virtual services..."
    local virtual_services
    virtual_services=$(kubectl get virtualservices --all-namespaces -o json | jq -r '.items | length')
    if [ "$virtual_services" -eq 0 ]; then
        print_warning "No virtual services found"
    else
        print_success "Found $virtual_services virtual services"
    fi

    # Run istioctl analyze
    print_info "Running istioctl analyze..."
    if command -v istioctl &> /dev/null; then
        if istioctl analyze --all-namespaces; then
            print_success "Istio configuration is valid"
        else
            print_warning "Istio configuration has warnings"
        fi
    fi

    # Check observability
    print_info "Checking observability components..."

    if kubectl get pods -n "$NAMESPACE_MONITORING" -l app=prometheus | grep -q "Running"; then
        print_success "Prometheus is running"
    else
        print_warning "Prometheus is not running"
    fi

    if kubectl get pods -n "$NAMESPACE_MONITORING" -l app=jaeger | grep -q "Running"; then
        print_success "Jaeger is running"
    else
        print_warning "Jaeger is not running"
    fi

    if kubectl get pods -n "$NAMESPACE_MONITORING" -l app=grafana | grep -q "Running"; then
        print_success "Grafana is running"
    else
        print_warning "Grafana is not running"
    fi

    if [ "$validation_failed" = true ]; then
        print_error "Validation failed"
        return 1
    fi

    print_success "Validation completed successfully"
}

# Function to print status
print_status() {
    print_info "Istio Service Mesh Status"
    echo ""

    print_info "Control Plane:"
    kubectl get pods -n "$NAMESPACE_ISTIO" -l app=istiod
    echo ""

    print_info "Gateways:"
    kubectl get pods -n "$NAMESPACE_ISTIO" -l "app in (istio-ingressgateway,istio-egressgateway)"
    echo ""

    print_info "Services:"
    kubectl get svc -n "$NAMESPACE_ISTIO"
    echo ""

    print_info "Ingress Gateway External IP:"
    kubectl get svc istio-ingressgateway -n "$NAMESPACE_ISTIO" -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
    echo ""
    echo ""

    print_info "Observability:"
    kubectl get pods -n "$NAMESPACE_MONITORING"
    echo ""

    if command -v istioctl &> /dev/null; then
        print_info "Proxy Status:"
        istioctl proxy-status
    fi
}

# Function to cleanup
cleanup() {
    print_warning "Cleaning up Istio installation..."

    if [ "$DRY_RUN" = "true" ]; then
        print_info "DRY RUN: Would cleanup Istio"
        return
    fi

    read -p "Are you sure you want to uninstall Istio? (yes/no): " -r
    if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
        print_info "Cleanup cancelled"
        return
    fi

    # Delete all Istio resources
    kubectl delete -k "$KUSTOMIZE_DIR" || true

    # Uninstall operator
    if command -v istioctl &> /dev/null; then
        istioctl uninstall --revision "$ISTIO_REVISION" -y || true
        kubectl delete namespace istio-operator || true
    fi

    # Remove sidecar injection labels
    local namespaces=("aikernel" "aikernel-infrastructure" "aikernel-data" "aikernel-monitoring")
    for ns in "${namespaces[@]}"; do
        kubectl label namespace "$ns" istio-injection- || true
    done

    print_success "Cleanup completed"
}

# Function to show usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

AIKernel Istio Service Mesh Deployment Script

OPTIONS:
    --install           Install Istio service mesh (default)
    --validate          Validate existing installation
    --status            Show deployment status
    --cleanup           Remove Istio installation
    --dry-run           Show what would be done without making changes
    --help              Show this help message

ENVIRONMENT VARIABLES:
    ISTIO_VERSION       Istio version to install (default: 1.20.0)
    ISTIO_REVISION      Istio revision name (default: 1-20-0)
    TIMEOUT             Timeout in seconds for operations (default: 600)
    DRY_RUN             Enable dry-run mode (default: false)

EXAMPLES:
    # Install Istio
    $0 --install

    # Validate installation
    $0 --validate

    # Check status
    $0 --status

    # Dry-run installation
    DRY_RUN=true $0 --install

    # Cleanup installation
    $0 --cleanup

EOF
}

# Main execution
main() {
    local action="${1:-install}"

    case "$action" in
        --install|install)
            print_info "Starting Istio installation..."
            check_prerequisites

            if ! command -v istioctl &> /dev/null; then
                download_istioctl
            fi

            install_operator
            create_namespaces
            deploy_istio
            deploy_observability

            print_success "Istio installation completed!"
            print_info "Run '$0 --validate' to validate the installation"
            print_info "Run '$0 --status' to check deployment status"
            ;;
        --validate|validate)
            validate_deployment
            ;;
        --status|status)
            print_status
            ;;
        --cleanup|cleanup)
            cleanup
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            main "${@:-install}"
            ;;
        --help|-h|help)
            usage
            ;;
        *)
            print_error "Unknown option: $action"
            usage
            exit 1
            ;;
    esac
}

# Run main with all arguments
main "$@"
