#!/bin/bash
#
# Complete Deployment Script for AI Kernel Platform
# This script deploys the entire infrastructure stack
#

set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
ENV_FILE="$PROJECT_ROOT/.env"

DRY_RUN="${DRY_RUN:-false}"
SKIP_ISTIO="${SKIP_ISTIO:-false}"
SKIP_ARGOCD="${SKIP_ARGOCD:-false}"
SKIP_DASHBOARD="${SKIP_DASHBOARD:-false}"
SKIP_HARBOR="${SKIP_HARBOR:-false}"
SKIP_ARTIFACTORY="${SKIP_ARTIFACTORY:-false}"

# Load environment variables from .env if it exists
if [ -f "$ENV_FILE" ]; then
    echo -e "${GREEN}→${NC} Loading environment variables from .env..."
    set -a
    source "$ENV_FILE"
    set +a
    echo -e "${GREEN}✓${NC} Environment variables loaded"
else
    echo -e "${YELLOW}⚠${NC} No .env file found. Some manifests may have placeholder values."
    echo -e "${YELLOW}→${NC} To use environment variable injection:"
    echo -e "   1. Copy .env.example to .env: cp .env.example .env"
    echo -e "   2. Edit .env with your actual values"
    echo -e "   3. Run this script again"
    echo ""
fi

echo -e "${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  AI Kernel Platform - Infrastructure Deployment         ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Function to print section headers
print_header() {
    echo ""
    echo -e "${YELLOW}═══ $1 ═══${NC}"
    echo ""
}

# Function to apply Kubernetes manifests with environment variable substitution
apply_manifests() {
    local file=$1
    local description=$2

    echo -e "${GREEN}→${NC} Applying $description..."

    # Check if envsubst is available
    if command -v envsubst &> /dev/null; then
        # Apply with environment variable substitution
        if [ "$DRY_RUN" = "true" ]; then
            envsubst < "$file" | kubectl apply --dry-run=client -f -
        else
            envsubst < "$file" | kubectl apply -f -
        fi
    else
        # Fallback to direct apply without substitution
        echo -e "${YELLOW}⚠${NC} envsubst not found, applying without variable substitution"
        if [ "$DRY_RUN" = "true" ]; then
            kubectl apply -f "$file" --dry-run=client
        else
            kubectl apply -f "$file"
        fi
    fi

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $description applied successfully"
    else
        echo -e "${RED}✗${NC} Failed to apply $description"
        return 1
    fi
}

# Function to wait for deployment
wait_for_deployment() {
    local namespace=$1
    local deployment=$2
    local timeout=${3:-300}

    echo -e "${GREEN}→${NC} Waiting for $deployment in $namespace to be ready..."

    if kubectl wait --for=condition=available --timeout=${timeout}s deployment/$deployment -n $namespace 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $deployment is ready"
    else
        echo -e "${YELLOW}⚠${NC} $deployment may not be ready yet (continuing...)"
    fi
}

# Function to wait for statefulset
wait_for_statefulset() {
    local namespace=$1
    local statefulset=$2
    local replicas=$3
    local timeout=${4:-300}

    echo -e "${GREEN}→${NC} Waiting for $statefulset in $namespace to be ready..."

    local end_time=$((SECONDS + timeout))
    while [ $SECONDS -lt $end_time ]; do
        local ready=$(kubectl get statefulset $statefulset -n $namespace -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo "0")
        if [ "$ready" = "$replicas" ]; then
            echo -e "${GREEN}✓${NC} $statefulset is ready ($ready/$replicas replicas)"
            return 0
        fi
        echo -e "  Waiting... ($ready/$replicas replicas ready)"
        sleep 10
    done

    echo -e "${YELLOW}⚠${NC} $statefulset may not be ready yet (continuing...)"
}

# Pre-flight checks
print_header "Pre-flight Checks"

echo -e "${GREEN}→${NC} Checking kubectl..."
if ! command -v kubectl &> /dev/null; then
    echo -e "${RED}✗${NC} kubectl not found. Please install kubectl first."
    exit 1
fi
echo -e "${GREEN}✓${NC} kubectl found"

echo -e "${GREEN}→${NC} Checking envsubst (for environment variable substitution)..."
if ! command -v envsubst &> /dev/null; then
    echo -e "${YELLOW}⚠${NC} envsubst not found. Install gettext package for variable substitution:"
    echo -e "   Ubuntu/Debian: sudo apt-get install gettext-base"
    echo -e "   CentOS/RHEL:   sudo yum install gettext"
    echo -e "   macOS:         brew install gettext"
    echo -e "${YELLOW}→${NC} Continuing without environment variable substitution..."
else
    echo -e "${GREEN}✓${NC} envsubst found"
fi

echo -e "${GREEN}→${NC} Checking cluster connection..."
if ! kubectl cluster-info &> /dev/null; then
    echo -e "${RED}✗${NC} Cannot connect to Kubernetes cluster"
    exit 1
fi
echo -e "${GREEN}✓${NC} Connected to cluster: $(kubectl config current-context)"

echo -e "${GREEN}→${NC} Checking cluster version..."
K8S_VERSION=$(kubectl version --short | grep Server | awk '{print $3}')
echo -e "${GREEN}✓${NC} Kubernetes version: $K8S_VERSION"

# Step 1: Create Namespaces
print_header "Step 1: Creating Namespaces"
apply_manifests "$SCRIPT_DIR/00-namespaces.yaml" "Namespaces"

# Step 2: Deploy Secrets and ConfigMaps
print_header "Step 2: Creating Secrets and ConfigMaps"
apply_manifests "$SCRIPT_DIR/01-postgres-secret.yaml" "PostgreSQL Secrets"
apply_manifests "$SCRIPT_DIR/02-postgres-configmap.yaml" "PostgreSQL ConfigMap"
apply_manifests "$SCRIPT_DIR/04-neo4j-secret.yaml" "Neo4j Secret"
apply_manifests "$SCRIPT_DIR/07-redis-statefulset.yaml" "Redis ConfigMap (embedded)"
apply_manifests "$SCRIPT_DIR/10-vault-statefulset.yaml" "Vault ConfigMap (embedded)"
apply_manifests "$SCRIPT_DIR/11-prometheus-configmap.yaml" "Prometheus ConfigMap"

# Step 3: Deploy Data Layer
print_header "Step 3: Deploying Data Layer"
apply_manifests "$SCRIPT_DIR/03-postgres-statefulset.yaml" "PostgreSQL Cluster"
wait_for_statefulset "aikernel-data" "postgres" "3" 600

apply_manifests "$SCRIPT_DIR/05-neo4j-statefulset.yaml" "Neo4j Database"
wait_for_statefulset "aikernel-data" "neo4j" "1" 300

apply_manifests "$SCRIPT_DIR/06-elasticsearch-statefulset.yaml" "Elasticsearch Cluster"
wait_for_statefulset "aikernel-data" "elasticsearch" "3" 600

apply_manifests "$SCRIPT_DIR/07-redis-statefulset.yaml" "Redis Cluster"
wait_for_statefulset "aikernel-data" "redis" "3" 300

apply_manifests "$SCRIPT_DIR/08-zookeeper-statefulset.yaml" "Zookeeper Cluster"
wait_for_statefulset "aikernel-data" "zookeeper" "3" 300

apply_manifests "$SCRIPT_DIR/09-kafka-statefulset.yaml" "Kafka Cluster"
wait_for_statefulset "aikernel-data" "kafka" "3" 600

# Step 4: Deploy Security Layer
print_header "Step 4: Deploying Security Layer"
apply_manifests "$SCRIPT_DIR/10-vault-statefulset.yaml" "HashiCorp Vault"
wait_for_statefulset "aikernel-security" "vault" "1" 300

# Step 5: Deploy Monitoring Stack
print_header "Step 5: Deploying Monitoring Stack"
apply_manifests "$SCRIPT_DIR/12-prometheus-statefulset.yaml" "Prometheus"
wait_for_statefulset "aikernel-monitoring" "prometheus" "2" 300

apply_manifests "$SCRIPT_DIR/13-grafana-deployment.yaml" "Grafana"
wait_for_deployment "aikernel-monitoring" "grafana" 300

apply_manifests "$SCRIPT_DIR/14-jaeger-deployment.yaml" "Jaeger"
wait_for_deployment "aikernel-monitoring" "jaeger-collector" 300
wait_for_deployment "aikernel-monitoring" "jaeger-query" 300

# Step 6: Install Istio
if [ "$SKIP_ISTIO" != "true" ]; then
    print_header "Step 6: Installing Istio Service Mesh"

    if [ -f "$SCRIPT_DIR/15-istio-install.sh" ]; then
        bash "$SCRIPT_DIR/15-istio-install.sh"

        # Apply Istio security policies
        sleep 10  # Wait for Istio to be fully ready
        apply_manifests "$SCRIPT_DIR/16-istio-security.yaml" "Istio Security Policies"
        apply_manifests "$SCRIPT_DIR/17-istio-gateway.yaml" "Istio Gateway Configuration"
    else
        echo -e "${YELLOW}⚠${NC} Istio installation script not found, skipping..."
    fi
else
    echo -e "${YELLOW}⚠${NC} Skipping Istio installation (SKIP_ISTIO=true)"
fi

# Step 7: Install ArgoCD
if [ "$SKIP_ARGOCD" != "true" ]; then
    print_header "Step 7: Installing ArgoCD for GitOps"

    if [ -f "$SCRIPT_DIR/18-argocd-install.sh" ]; then
        bash "$SCRIPT_DIR/18-argocd-install.sh"

        # Apply ArgoCD applications
        sleep 10
        apply_manifests "$SCRIPT_DIR/19-argocd-apps.yaml" "ArgoCD Applications"
    else
        echo -e "${YELLOW}⚠${NC} ArgoCD installation script not found, skipping..."
    fi
else
    echo -e "${YELLOW}⚠${NC} Skipping ArgoCD installation (SKIP_ARGOCD=true)"
fi

# Step 8: Create LoadBalancer Services
print_header "Step 8: Creating LoadBalancer Services"
apply_manifests "$SCRIPT_DIR/20-loadbalancer-services.yaml" "LoadBalancer Services"

echo -e "${GREEN}→${NC} Waiting for LoadBalancer IPs to be assigned (this may take a few minutes)..."
sleep 10

# Step 9: Install Kubernetes Dashboard
if [ "${SKIP_DASHBOARD}" != "true" ]; then
    print_header "Step 9: Installing Kubernetes Dashboard"

    if [ -f "$SCRIPT_DIR/21-kubernetes-dashboard-install.sh" ]; then
        bash "$SCRIPT_DIR/21-kubernetes-dashboard-install.sh"

        # Apply additional RBAC
        sleep 5
        apply_manifests "$SCRIPT_DIR/22-kubernetes-dashboard-rbac.yaml" "Dashboard RBAC Configuration"
    else
        echo -e "${YELLOW}⚠${NC} Dashboard installation script not found, skipping..."
    fi
else
    echo -e "${YELLOW}⚠${NC} Skipping Dashboard installation (SKIP_DASHBOARD=true)"
fi

# Step 10: Configure External Services (Harbor, Artifactory, MinIO)
print_header "Step 10: Configuring External Services Integration"

echo -e "${YELLOW}NOTE:${NC} Harbor, Artifactory, and MinIO are running on bare-metal servers"
echo -e "${YELLOW}→${NC} Deploying Kubernetes integration configuration..."

apply_manifests "$SCRIPT_DIR/27-external-services-config.yaml" "External Services Configuration"

echo -e "${GREEN}✓${NC} External services configuration complete"
echo ""
echo -e "${YELLOW}External Services:${NC}"
echo -e "  • Harbor Registry: https://harbor.aikernel.local (bare-metal)"
echo -e "  • Artifactory: https://artifactory.aikernel.local (bare-metal)"
echo -e "  • MinIO S3 Storage: https://minio.aikernel.local:9000 (bare-metal)"
echo ""
echo -e "${YELLOW}→${NC} See infrastructure/prod/k8s/EXTERNAL_SERVICES_SETUP.md for setup instructions"

# Final status check
print_header "Deployment Summary"

echo ""
echo -e "${GREEN}✓${NC} Infrastructure deployment complete!"
echo ""
echo "Deployment status by namespace:"
echo ""

for ns in aikernel-data aikernel-security aikernel-monitoring istio-system argocd kubernetes-dashboard; do
    if kubectl get namespace $ns &> /dev/null; then
        echo -e "${GREEN}→${NC} Namespace: $ns"
        kubectl get pods -n $ns 2>/dev/null | grep -v "NAME" | awk '{print "  "$1" - "$3}' || echo "  No pods found"
    fi
done

echo ""
echo -e "${YELLOW}═══ LoadBalancer Services ═══${NC}"
echo ""
echo "Checking LoadBalancer external IPs (may take a few minutes to assign):"
echo ""
kubectl get svc -n aikernel-monitoring grafana-lb prometheus-lb jaeger-query-lb 2>/dev/null || echo "  Monitoring LoadBalancers not yet ready"
kubectl get svc -n argocd argocd-server-lb 2>/dev/null || echo "  ArgoCD LoadBalancer not yet ready"
kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb 2>/dev/null || echo "  Dashboard LoadBalancer not yet ready"
kubectl get svc -n istio-system istio-ingressgateway 2>/dev/null || echo "  Istio Ingress not yet ready"

echo ""
echo -e "${YELLOW}═══ External Artifact Management Services (Bare-Metal) ═══${NC}"
echo ""
echo -e "${GREEN}Harbor Container Registry:${NC}"
echo "  URL: https://harbor.aikernel.local (external bare-metal server)"
echo "  Status: Check Harbor server directly"
echo ""
echo -e "${GREEN}JFrog Artifactory:${NC}"
echo "  URL: https://artifactory.aikernel.local (external bare-metal server)"
echo "  Status: Check Artifactory server directly"
echo ""
echo -e "${GREEN}MinIO S3 Storage:${NC}"
echo "  API: https://minio.aikernel.local:9000 (external bare-metal server)"
echo "  Console: https://minio.aikernel.local:9001"
echo "  Status: Check MinIO server directly"
echo ""
echo -e "${YELLOW}NOTE:${NC} Harbor, Artifactory, and MinIO run on separate bare-metal servers"
echo "  See EXTERNAL_SERVICES_SETUP.md for configuration details"
echo ""
echo ""
echo "To check LoadBalancer status later:"
echo "  kubectl get svc -A | grep LoadBalancer"
echo ""

echo -e "${YELLOW}═══ Access Information ═══${NC}"
echo ""
echo "1. Kubernetes Dashboard:"
echo "   Get external IP: kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb"
echo "   URL: https://<EXTERNAL-IP>"
echo "   Get admin token: kubectl -n kubernetes-dashboard create token admin-user"
echo "   Get viewer token: kubectl -n kubernetes-dashboard create token dashboard-viewer"
echo ""
echo "2. Grafana (Monitoring):"
echo "   Get external IP: kubectl get svc -n aikernel-monitoring grafana-lb"
echo "   URL: http://<EXTERNAL-IP>"
echo "   Get password: kubectl -n aikernel-monitoring get secret grafana-credentials -o jsonpath='{.data.admin-password}' | base64 -d"
echo ""
echo "3. Prometheus (Metrics):"
echo "   Get external IP: kubectl get svc -n aikernel-monitoring prometheus-lb"
echo "   URL: http://<EXTERNAL-IP>"
echo ""
echo "4. Jaeger (Tracing):"
echo "   Get external IP: kubectl get svc -n aikernel-monitoring jaeger-query-lb"
echo "   URL: http://<EXTERNAL-IP>"
echo ""
echo "5. ArgoCD (GitOps):"
echo "   Get external IP: kubectl get svc -n argocd argocd-server-lb"
echo "   URL: https://<EXTERNAL-IP>"
echo "   Username: admin"
echo "   Get password: kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d"
echo ""
echo "6. Main API Gateway (Istio Ingress):"
echo "   Get external IP: kubectl get svc -n istio-system istio-ingressgateway"
echo "   URL: https://<EXTERNAL-IP>"
echo ""

echo -e "${YELLOW}═══ Next Steps ═══${NC}"
echo ""
echo "1. Verify all pods are running:"
echo "   kubectl get pods -A"
echo ""
echo "2. Initialize HashiCorp Vault:"
echo "   kubectl exec -it vault-0 -n aikernel-security -- vault operator init"
echo ""
echo "3. Configure TLS certificates for production"
echo ""
echo "4. Update secrets with production values"
echo ""
echo "5. Configure DNS records to point to LoadBalancer IPs"
echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║           Infrastructure Deployment Complete!            ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""
