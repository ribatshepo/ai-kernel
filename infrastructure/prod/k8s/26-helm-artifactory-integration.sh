#!/bin/bash

# Helm Chart Management with Artifactory Integration
# This script packages, uploads, and deploys Helm charts via Artifactory

set -e

ARTIFACTORY_URL="${ARTIFACTORY_URL:-https://artifactory.aikernel.local}"
ARTIFACTORY_REPO="${ARTIFACTORY_REPO:-helm-local}"
ARTIFACTORY_USER="${ARTIFACTORY_USER}"
ARTIFACTORY_PASSWORD="${ARTIFACTORY_PASSWORD}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check required tools
check_dependencies() {
    log_info "Checking dependencies..."

    if ! command -v helm &> /dev/null; then
        log_error "helm is not installed. Please install Helm."
        exit 1
    fi

    if ! command -v curl &> /dev/null; then
        log_error "curl is not installed. Please install curl."
        exit 1
    fi

    log_info "All dependencies satisfied"
}

# Configure Helm to use Artifactory
configure_helm_repo() {
    log_info "Configuring Helm repository with Artifactory..."

    # Add Artifactory as Helm repository
    helm repo add aikernel-charts "${ARTIFACTORY_URL}/artifactory/${ARTIFACTORY_REPO}" \
        --username "${ARTIFACTORY_USER}" \
        --password "${ARTIFACTORY_PASSWORD}"

    # Update repository index
    helm repo update

    log_info "Helm repository configured successfully"
}

# Package Helm chart
package_chart() {
    local chart_dir="$1"
    local output_dir="${2:-.}"

    log_info "Packaging Helm chart from ${chart_dir}..."

    if [ ! -d "${chart_dir}" ]; then
        log_error "Chart directory ${chart_dir} does not exist"
        return 1
    fi

    # Package the chart
    helm package "${chart_dir}" --destination "${output_dir}"

    log_info "Chart packaged successfully"
}

# Upload chart to Artifactory
upload_chart() {
    local chart_package="$1"

    log_info "Uploading ${chart_package} to Artifactory..."

    if [ ! -f "${chart_package}" ]; then
        log_error "Chart package ${chart_package} does not exist"
        return 1
    fi

    # Upload using curl
    curl -u "${ARTIFACTORY_USER}:${ARTIFACTORY_PASSWORD}" \
        -X PUT \
        "${ARTIFACTORY_URL}/artifactory/${ARTIFACTORY_REPO}/$(basename ${chart_package})" \
        -T "${chart_package}"

    log_info "Chart uploaded successfully"
}

# Install or upgrade chart from Artifactory
deploy_chart() {
    local release_name="$1"
    local chart_name="$2"
    local namespace="$3"
    local values_file="${4:-}"

    log_info "Deploying chart ${chart_name} as release ${release_name} in namespace ${namespace}..."

    # Ensure namespace exists
    kubectl create namespace "${namespace}" --dry-run=client -o yaml | kubectl apply -f -

    # Build helm command
    local helm_cmd="helm upgrade --install ${release_name} aikernel-charts/${chart_name} \
        --namespace ${namespace} \
        --create-namespace \
        --wait \
        --timeout 10m"

    # Add values file if provided
    if [ -n "${values_file}" ] && [ -f "${values_file}" ]; then
        helm_cmd="${helm_cmd} --values ${values_file}"
    fi

    # Execute helm command
    eval "${helm_cmd}"

    log_info "Chart deployed successfully"
}

# List charts in Artifactory
list_charts() {
    log_info "Listing charts in Artifactory repository..."

    curl -u "${ARTIFACTORY_USER}:${ARTIFACTORY_PASSWORD}" \
        -X GET \
        "${ARTIFACTORY_URL}/artifactory/api/storage/${ARTIFACTORY_REPO}" \
        | jq -r '.children[].uri' | grep '.tgz$' || true
}

# Search for chart versions
search_chart() {
    local chart_name="$1"

    log_info "Searching for ${chart_name} versions..."

    helm search repo aikernel-charts/${chart_name} --versions
}

# Download chart from Artifactory
download_chart() {
    local chart_name="$1"
    local chart_version="${2:-latest}"
    local output_dir="${3:-.}"

    log_info "Downloading ${chart_name}:${chart_version} from Artifactory..."

    # Pull the chart
    helm pull aikernel-charts/${chart_name} \
        --version "${chart_version}" \
        --destination "${output_dir}"

    log_info "Chart downloaded to ${output_dir}"
}

# Create Helm chart from Kubernetes manifests
create_chart_from_manifests() {
    local chart_name="$1"
    local manifests_dir="$2"
    local output_dir="${3:-.}"

    log_info "Creating Helm chart ${chart_name} from manifests in ${manifests_dir}..."

    # Create chart directory structure
    local chart_dir="${output_dir}/${chart_name}"
    mkdir -p "${chart_dir}/templates"

    # Create Chart.yaml
    cat > "${chart_dir}/Chart.yaml" <<EOF
apiVersion: v2
name: ${chart_name}
description: A Helm chart for ${chart_name}
type: application
version: 0.1.0
appVersion: "1.0.0"
keywords:
  - ai-kernel
  - kubernetes
home: https://github.com/aikernel/ai-kernel
sources:
  - https://github.com/aikernel/ai-kernel
maintainers:
  - name: AI Kernel Team
    email: team@aikernel.local
EOF

    # Create values.yaml
    cat > "${chart_dir}/values.yaml" <<EOF
# Default values for ${chart_name}
# This is a YAML-formatted file.
# Declare variables to be passed into your templates.

replicaCount: 1

image:
  repository: harbor.aikernel.local/aikernel/${chart_name}
  pullPolicy: IfNotPresent
  tag: "latest"

imagePullSecrets: []
nameOverride: ""
fullnameOverride: ""

serviceAccount:
  create: true
  annotations: {}
  name: ""

podAnnotations: {}
podSecurityContext: {}
securityContext: {}

service:
  type: ClusterIP
  port: 80

resources:
  limits:
    cpu: 1000m
    memory: 1Gi
  requests:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: false
  minReplicas: 1
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80

nodeSelector: {}
tolerations: []
affinity: {}
EOF

    # Copy manifests to templates
    if [ -d "${manifests_dir}" ]; then
        cp ${manifests_dir}/*.yaml "${chart_dir}/templates/" 2>/dev/null || true
    fi

    log_info "Helm chart created at ${chart_dir}"
}

# Main workflow: Package and upload chart
package_and_upload() {
    local chart_dir="$1"
    local output_dir="${2:-/tmp}"

    check_dependencies
    configure_helm_repo

    # Package the chart
    package_chart "${chart_dir}" "${output_dir}"

    # Find the packaged chart
    local chart_package=$(ls -t ${output_dir}/*.tgz | head -1)

    # Upload to Artifactory
    upload_chart "${chart_package}"

    # Update Helm repository
    helm repo update

    log_info "Chart packaging and upload completed"
}

# Main workflow: Deploy from Artifactory
deploy_from_artifactory() {
    local release_name="$1"
    local chart_name="$2"
    local namespace="$3"
    local values_file="${4:-}"

    check_dependencies
    configure_helm_repo

    # Deploy the chart
    deploy_chart "${release_name}" "${chart_name}" "${namespace}" "${values_file}"

    log_info "Deployment completed"
}

# Usage information
usage() {
    cat <<EOF
Helm Chart Management with Artifactory Integration

Usage: $0 <command> [options]

Commands:
    package-upload <chart-dir> [output-dir]
        Package a Helm chart and upload to Artifactory

    deploy <release-name> <chart-name> <namespace> [values-file]
        Deploy a chart from Artifactory

    list
        List all charts in Artifactory

    search <chart-name>
        Search for chart versions

    download <chart-name> [version] [output-dir]
        Download a chart from Artifactory

    create-from-manifests <chart-name> <manifests-dir> [output-dir]
        Create a Helm chart from Kubernetes manifests

Environment Variables:
    ARTIFACTORY_URL       - Artifactory URL (default: https://artifactory.aikernel.local)
    ARTIFACTORY_REPO      - Artifactory Helm repository (default: helm-local)
    ARTIFACTORY_USER      - Artifactory username (required)
    ARTIFACTORY_PASSWORD  - Artifactory password (required)

Examples:
    # Package and upload a chart
    $0 package-upload ./charts/myapp

    # Deploy a chart
    $0 deploy myapp-release myapp aikernel-core values-prod.yaml

    # List all charts
    $0 list

    # Search for chart versions
    $0 search myapp

    # Download a chart
    $0 download myapp 1.0.0 /tmp

    # Create chart from manifests
    $0 create-from-manifests myapp ./k8s-manifests ./charts
EOF
}

# Main execution
main() {
    if [ -z "${ARTIFACTORY_USER}" ] || [ -z "${ARTIFACTORY_PASSWORD}" ]; then
        log_error "ARTIFACTORY_USER and ARTIFACTORY_PASSWORD must be set"
        exit 1
    fi

    case "${1:-}" in
        package-upload)
            package_and_upload "$2" "${3:-/tmp}"
            ;;
        deploy)
            deploy_from_artifactory "$2" "$3" "$4" "${5:-}"
            ;;
        list)
            check_dependencies
            configure_helm_repo
            list_charts
            ;;
        search)
            check_dependencies
            configure_helm_repo
            search_chart "$2"
            ;;
        download)
            check_dependencies
            configure_helm_repo
            download_chart "$2" "${3:-latest}" "${4:-.}"
            ;;
        create-from-manifests)
            create_chart_from_manifests "$2" "$3" "${4:-.}"
            ;;
        *)
            usage
            exit 1
            ;;
    esac
}

main "$@"
