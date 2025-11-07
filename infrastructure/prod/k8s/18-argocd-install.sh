#!/bin/bash
#
# ArgoCD Installation Script for AI Kernel Platform
# This script installs and configures ArgoCD for GitOps
#

set -e

# Configuration
ARGOCD_VERSION="v2.9.3"
NAMESPACE="argocd"
REPO_URL="${REPO_URL:-https://github.com/ribatshepo/ai-kernel.git}"
BRANCH="${BRANCH:-main}"

echo "=== Installing ArgoCD ${ARGOCD_VERSION} for AI Kernel Platform ==="

# Create namespace
echo "Creating ArgoCD namespace..."
kubectl create namespace ${NAMESPACE} --dry-run=client -o yaml | kubectl apply -f -

# Install ArgoCD
echo "Installing ArgoCD ${ARGOCD_VERSION}..."
kubectl apply -n ${NAMESPACE} -f https://raw.githubusercontent.com/argoproj/argo-cd/${ARGOCD_VERSION}/manifests/install.yaml

# Wait for ArgoCD to be ready
echo "Waiting for ArgoCD to be ready..."
kubectl wait --for=condition=available --timeout=300s deployment/argocd-server -n ${NAMESPACE}

# Patch ArgoCD server for LoadBalancer (optional, use Ingress in production)
echo "Configuring ArgoCD server service..."
kubectl patch svc argocd-server -n ${NAMESPACE} -p '{"spec": {"type": "LoadBalancer"}}'

# Get initial admin password
echo ""
echo "=== ArgoCD Installation Complete ==="
echo ""
echo "ArgoCD is installed in namespace: ${NAMESPACE}"
echo ""
echo "Get the initial admin password:"
echo "  kubectl -n ${NAMESPACE} get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d"
echo ""
echo "Access ArgoCD UI:"
echo "  kubectl port-forward svc/argocd-server -n ${NAMESPACE} 8080:443"
echo "  Then visit: https://localhost:8080"
echo ""
echo "Login with:"
echo "  Username: admin"
echo "  Password: (from the command above)"
echo ""

# Create ArgoCD applications
echo "Creating ArgoCD applications..."

# Wait a bit for ArgoCD to be fully ready
sleep 10

kubectl apply -f - <<EOF
---
# ArgoCD Application for infrastructure
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: aikernel-infrastructure
  namespace: ${NAMESPACE}
  finalizers:
    - resources-finalizer.argocd.argoproj.io
spec:
  project: default
  source:
    repoURL: ${REPO_URL}
    targetRevision: ${BRANCH}
    path: infrastructure/prod/k8s
  destination:
    server: https://kubernetes.default.svc
    namespace: aikernel-data
  syncPolicy:
    automated:
      prune: false  # Don't auto-prune for infrastructure
      selfHeal: true
      allowEmpty: false
    syncOptions:
      - CreateNamespace=true
      - PruneLast=true
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
---
# ArgoCD AppProject for AI Kernel
apiVersion: argoproj.io/v1alpha1
kind: AppProject
metadata:
  name: aikernel
  namespace: ${NAMESPACE}
spec:
  description: AI Kernel Platform
  sourceRepos:
    - ${REPO_URL}
  destinations:
    - namespace: 'aikernel-*'
      server: https://kubernetes.default.svc
    - namespace: istio-system
      server: https://kubernetes.default.svc
    - namespace: argocd
      server: https://kubernetes.default.svc
  clusterResourceWhitelist:
    - group: '*'
      kind: '*'
  namespaceResourceWhitelist:
    - group: '*'
      kind: '*'
  roles:
    - name: admin
      description: Admin privileges for AI Kernel
      policies:
        - p, proj:aikernel:admin, applications, *, aikernel/*, allow
      groups:
        - aikernel-admins
    - name: developer
      description: Developer privileges for AI Kernel
      policies:
        - p, proj:aikernel:developer, applications, get, aikernel/*, allow
        - p, proj:aikernel:developer, applications, sync, aikernel/*, allow
      groups:
        - aikernel-developers
EOF

echo ""
echo "=== Next Steps ==="
echo ""
echo "1. Login to ArgoCD:"
echo "   argocd login <ARGOCD_SERVER>"
echo ""
echo "2. Change the admin password:"
echo "   argocd account update-password"
echo ""
echo "3. Configure Git repository credentials (if private):"
echo "   argocd repo add ${REPO_URL} --username <username> --password <password>"
echo ""
echo "4. Sync the infrastructure application:"
echo "   argocd app sync aikernel-infrastructure"
echo ""
echo "5. Monitor deployment:"
echo "   argocd app get aikernel-infrastructure"
echo "   kubectl get pods -A"
echo ""
