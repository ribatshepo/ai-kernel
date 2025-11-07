#!/bin/bash
#
# Kubernetes Dashboard Installation Script
# Installs the official Kubernetes Dashboard with proper RBAC and security
#

set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
DASHBOARD_VERSION="v2.7.0"
NAMESPACE="kubernetes-dashboard"

echo -e "${GREEN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║     Kubernetes Dashboard Installation for AI Kernel     ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Step 1: Install Kubernetes Dashboard
echo -e "${GREEN}→${NC} Installing Kubernetes Dashboard ${DASHBOARD_VERSION}..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/${DASHBOARD_VERSION}/aio/deploy/recommended.yaml

# Wait for deployment
echo -e "${GREEN}→${NC} Waiting for dashboard to be ready..."
kubectl wait --for=condition=available --timeout=300s deployment/kubernetes-dashboard -n ${NAMESPACE} || echo -e "${YELLOW}⚠${NC} Dashboard may not be ready yet"

# Step 2: Create admin service account
echo -e "${GREEN}→${NC} Creating admin service account..."
kubectl apply -f - <<EOF
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: admin-user
  namespace: ${NAMESPACE}
  labels:
    app.kubernetes.io/part-of: ai-kernel
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: admin-user
  labels:
    app.kubernetes.io/part-of: ai-kernel
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: cluster-admin
subjects:
  - kind: ServiceAccount
    name: admin-user
    namespace: ${NAMESPACE}
EOF

# Step 3: Create read-only user
echo -e "${GREEN}→${NC} Creating read-only service account..."
kubectl apply -f - <<EOF
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: dashboard-viewer
  namespace: ${NAMESPACE}
  labels:
    app.kubernetes.io/part-of: ai-kernel
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: dashboard-viewer
  labels:
    app.kubernetes.io/part-of: ai-kernel
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: view
subjects:
  - kind: ServiceAccount
    name: dashboard-viewer
    namespace: ${NAMESPACE}
EOF

# Step 4: Create LoadBalancer service
echo -e "${GREEN}→${NC} Creating LoadBalancer service for Dashboard..."
kubectl apply -f - <<EOF
---
apiVersion: v1
kind: Service
metadata:
  name: kubernetes-dashboard-lb
  namespace: ${NAMESPACE}
  labels:
    k8s-app: kubernetes-dashboard
    app.kubernetes.io/part-of: ai-kernel
  annotations:
    service.beta.kubernetes.io/aws-load-balancer-additional-resource-tags: "Name=aikernel-dashboard"
    # Uncomment for internal-only access (recommended for production)
    # service.beta.kubernetes.io/aws-load-balancer-internal: "true"
spec:
  type: LoadBalancer
  selector:
    k8s-app: kubernetes-dashboard
  ports:
    - port: 443
      targetPort: 8443
      protocol: TCP
      name: https
  sessionAffinity: ClientIP
EOF

# Step 5: Get access token
echo ""
echo -e "${GREEN}═══ Kubernetes Dashboard Installation Complete ═══${NC}"
echo ""

# Wait a bit for service account token to be created
sleep 5

echo -e "${YELLOW}═══ Access Information ═══${NC}"
echo ""

# Get LoadBalancer IP/Hostname
echo -e "${GREEN}→${NC} LoadBalancer Address:"
kubectl get svc kubernetes-dashboard-lb -n ${NAMESPACE} -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || \
kubectl get svc kubernetes-dashboard-lb -n ${NAMESPACE} -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || \
echo "  Pending... (run 'kubectl get svc -n kubernetes-dashboard' to check)"
echo ""

# Get admin token
echo -e "${GREEN}→${NC} Admin User Token (Full Access):"
echo "  Run this command to get the token:"
echo -e "  ${YELLOW}kubectl -n ${NAMESPACE} create token admin-user${NC}"
echo ""

# Get viewer token
echo -e "${GREEN}→${NC} Viewer User Token (Read-Only Access):"
echo "  Run this command to get the token:"
echo -e "  ${YELLOW}kubectl -n ${NAMESPACE} create token dashboard-viewer${NC}"
echo ""

echo -e "${YELLOW}═══ Access Methods ═══${NC}"
echo ""
echo "1. Via LoadBalancer (when IP is assigned):"
echo "   https://<LOADBALANCER-IP>"
echo ""
echo "2. Via kubectl proxy (local access):"
echo "   kubectl proxy"
echo "   http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/"
echo ""
echo "3. Via port-forward:"
echo "   kubectl port-forward -n kubernetes-dashboard service/kubernetes-dashboard 8443:443"
echo "   https://localhost:8443"
echo ""

echo -e "${YELLOW}═══ Security Notes ═══${NC}"
echo ""
echo "- The admin-user has full cluster access (cluster-admin role)"
echo "- The dashboard-viewer has read-only access across the cluster"
echo "- For production, consider:"
echo "  1. Using internal LoadBalancer only"
echo "  2. Implementing IP whitelisting"
echo "  3. Using OAuth/OIDC authentication"
echo "  4. Regular token rotation"
echo ""

echo -e "${GREEN}═══ Installation Complete ═══${NC}"
echo ""
