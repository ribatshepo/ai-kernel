# Harbor and Artifactory Integration Guide

Production-grade artifact management with on-premise Harbor container registry and JFrog Artifactory for the AI Kernel Platform.

---

> **⚠️ IMPORTANT NOTE - EXTERNAL BARE-METAL DEPLOYMENT**
>
> **This document is for REFERENCE ONLY** and describes the original in-cluster Kubernetes deployment approach.
>
> **The AI Kernel Platform now uses EXTERNAL BARE-METAL SERVERS for Harbor, Artifactory, and MinIO.**
>
> **For the current deployment approach, please refer to:**
> - **[EXTERNAL_SERVICES_SETUP.md](./EXTERNAL_SERVICES_SETUP.md)** - Complete setup guide for bare-metal services
> - **[EXTERNAL_SERVICES_SUMMARY.md](../../../EXTERNAL_SERVICES_SUMMARY.md)** - Architecture overview
> - **[QUICK_REFERENCE.md](./QUICK_REFERENCE.md)** - Daily operations reference
>
> **Environment Configuration:**
> - See `.env.example` for environment variables template
> - Use `source scripts/load-env.sh` to load configuration
>
> The files referenced in this document (`23-harbor-registry.yaml`, `24-harbor-trivy-scanner.yaml`, `25-artifactory.yaml`) are **NOT deployed** in the current architecture. They are kept for reference purposes only.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Harbor Container Registry](#harbor-container-registry)
4. [JFrog Artifactory](#jfrog-artifactory)
5. [CI/CD Integration](#cicd-integration)
6. [Security Features](#security-features)
7. [Deployment Guide](#deployment-guide)
8. [Operations](#operations)
9. [Troubleshooting](#troubleshooting)

## Overview

The AI Kernel Platform uses a complete on-premise artifact management solution:

- **Harbor**: Container image registry with vulnerability scanning, signing, and replication
- **Artifactory**: Universal artifact repository for Helm charts, NuGet packages, NPM modules, and build artifacts
- **Trivy**: Integrated vulnerability scanner for container images
- **Cosign**: Image signing for supply chain security

### Why On-Premise?

- **Data Sovereignty**: Keep all artifacts within your infrastructure
- **Performance**: Faster pulls from local network
- **Cost Control**: No egress charges or rate limits
- **Security**: Full control over access and scanning policies
- **Compliance**: Meet regulatory requirements for artifact storage

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      GitHub Actions CI/CD                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Build Images │→ │ Scan with    │→ │ Sign with    │          │
│  │              │  │ Trivy        │  │ Cosign       │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
                             ↓
              ┌──────────────────────────────┐
              │    On-Premise Infrastructure  │
              │                              │
              │  ┌────────────────────────┐  │
              │  │   Harbor Registry      │  │
              │  │  • Docker images       │  │
              │  │  • Vulnerability DB    │  │
              │  │  • Image signatures    │  │
              │  │  • Replication         │  │
              │  └────────────────────────┘  │
              │            ↓                  │
              │  ┌────────────────────────┐  │
              │  │   Artifactory          │  │
              │  │  • Helm charts         │  │
              │  │  • NuGet packages      │  │
              │  │  • NPM modules         │  │
              │  │  • Build metadata      │  │
              │  └────────────────────────┘  │
              │            ↓                  │
              │  ┌────────────────────────┐  │
              │  │  Kubernetes Cluster    │  │
              │  │  • Pull images         │  │
              │  │  • Deploy Helm charts  │  │
              │  │  • Verify signatures   │  │
              │  └────────────────────────┘  │
              └──────────────────────────────┘
```

## Harbor Container Registry

### Features

Harbor is an open-source container image registry with enterprise-grade features:

#### Core Features
- **Multi-tenancy**: Project-based access control
- **Vulnerability Scanning**: Integrated Trivy scanner
- **Content Trust**: Image signing and verification
- **Image Replication**: Multi-site replication
- **Garbage Collection**: Automated cleanup of unused images
- **Webhooks**: Integration with CI/CD pipelines
- **Audit Logging**: Complete audit trail

#### Components

1. **Harbor Core**: Main API and UI
2. **Harbor Registry**: Docker registry v2
3. **Harbor Portal**: Web UI
4. **Harbor JobService**: Async job execution
5. **Trivy Adapter**: Vulnerability scanning
6. **PostgreSQL**: Metadata database
7. **Redis**: Caching layer

### Configuration

#### 1. Deploy Harbor

```bash
# Deploy Harbor infrastructure
kubectl apply -f infrastructure/prod/k8s/23-harbor-registry.yaml
kubectl apply -f infrastructure/prod/k8s/24-harbor-trivy-scanner.yaml

# Wait for all components to be ready
kubectl wait --for=condition=ready pod -l app=harbor-core -n aikernel-registry --timeout=5m
kubectl wait --for=condition=ready pod -l app=harbor-registry -n aikernel-registry --timeout=5m
kubectl wait --for=condition=ready pod -l app=harbor-trivy -n aikernel-registry --timeout=5m
```

#### 2. Access Harbor UI

```bash
# Get Harbor URL
echo "Harbor URL: https://harbor.aikernel.local"

# Or via NodePort for on-prem
NODE_IP=$(kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
echo "Harbor URL: http://${NODE_IP}:30200"

# Get admin password
kubectl -n aikernel-registry get secret harbor-core -o jsonpath='{.data.HARBOR_ADMIN_PASSWORD}' | base64 -d
echo
```

#### 3. Initial Configuration

After accessing Harbor UI:

1. **Change Admin Password**
   - Login with default password
   - User Menu → Change Password

2. **Create Projects**
   ```
   - aikernel (public: true)
   - aikernel-dev (public: false)
   - aikernel-staging (public: false)
   - aikernel-prod (public: false)
   ```

3. **Configure Image Scanning**
   - Administration → Interrogation Services
   - Set Trivy as default scanner
   - Enable "Scan on push"
   - Set severity threshold: High/Critical

4. **Create Robot Accounts**
   ```
   Name: robot$github-actions
   Expiration: 1 year
   Permissions:
     - Pull artifact
     - Push artifact
     - Create tag
     - Delete artifact
   ```

5. **Configure Retention Policies**
   - Projects → aikernel → Policy
   - Rule 1: Retain last 10 versions of any tag
   - Rule 2: Retain production images for 30 days
   - Schedule: Daily at 2 AM

6. **Configure Replication** (Optional for DR)
   - Administration → Replications
   - Add destination registry (DR site)
   - Filter: production images only
   - Trigger: Event based

### Usage

#### Push Images

```bash
# Login
docker login harbor.aikernel.local

# Tag image
docker tag myapp:latest harbor.aikernel.local/aikernel/myapp:latest

# Push
docker push harbor.aikernel.local/aikernel/myapp:latest
```

#### Pull Images

```bash
# Create image pull secret for Kubernetes
kubectl create secret docker-registry harbor-credentials \
  --docker-server=harbor.aikernel.local \
  --docker-username=robot\$github-actions \
  --docker-password=<token> \
  -n aikernel-core

# Use in deployment
spec:
  imagePullSecrets:
  - name: harbor-credentials
  containers:
  - name: myapp
    image: harbor.aikernel.local/aikernel/myapp:latest
```

## JFrog Artifactory

### Features

Artifactory is a universal artifact repository supporting all major package formats:

#### Supported Package Types
- **Docker**: Container images (can complement Harbor)
- **Helm**: Kubernetes charts
- **NuGet**: .NET packages
- **NPM**: Node.js modules
- **Maven**: Java artifacts
- **PyPI**: Python packages
- **Generic**: Any binary artifacts

#### Core Features
- **Universal Repository**: Single source for all artifacts
- **Build Integration**: JFrog CLI integration
- **Advanced Search**: Query by properties, checksums
- **Replication**: Multi-site artifact replication
- **Access Control**: Fine-grained permissions
- **Build Info**: Complete build metadata tracking
- **Xray Integration**: Vulnerability scanning (requires license)

### Configuration

#### 1. Deploy Artifactory

```bash
# Deploy Artifactory
kubectl apply -f infrastructure/prod/k8s/25-artifactory.yaml

# Wait for Artifactory to be ready (may take 5-10 minutes)
kubectl wait --for=condition=ready pod -l app=artifactory -n aikernel-artifactory --timeout=15m
```

#### 2. Access Artifactory UI

```bash
# Get Artifactory URL
echo "Artifactory URL: https://artifactory.aikernel.local"

# Or via NodePort
NODE_IP=$(kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
echo "Artifactory URL: http://${NODE_IP}:30250"

# Default credentials
# Username: admin
# Password: password (change immediately!)
```

#### 3. Initial Configuration

After accessing Artifactory UI:

1. **Change Admin Password**
   - Admin → Security → Users → admin → Edit

2. **Create Repositories** (via UI or REST API)

   Helm repositories:
   ```bash
   # Local repository for custom charts
   curl -u admin:password -X PUT \
     "https://artifactory.aikernel.local/artifactory/api/repositories/helm-local" \
     -H "Content-Type: application/json" \
     -d '{
       "key": "helm-local",
       "rclass": "local",
       "packageType": "helm",
       "description": "Local Helm charts"
     }'

   # Remote repository (proxy for helm.sh)
   curl -u admin:password -X PUT \
     "https://artifactory.aikernel.local/artifactory/api/repositories/helm-remote" \
     -H "Content-Type: application/json" \
     -d '{
       "key": "helm-remote",
       "rclass": "remote",
       "packageType": "helm",
       "url": "https://charts.helm.sh/stable"
     }'

   # Virtual repository
   curl -u admin:password -X PUT \
     "https://artifactory.aikernel.local/artifactory/api/repositories/helm" \
     -H "Content-Type: application/json" \
     -d '{
       "key": "helm",
       "rclass": "virtual",
       "packageType": "helm",
       "repositories": ["helm-local", "helm-remote"]
     }'
   ```

   NuGet repositories:
   ```bash
   # For .NET packages
   curl -u admin:password -X PUT \
     "https://artifactory.aikernel.local/artifactory/api/repositories/nuget-local" \
     -H "Content-Type: application/json" \
     -d '{
       "key": "nuget-local",
       "rclass": "local",
       "packageType": "nuget"
     }'
   ```

3. **Create Service Accounts**
   - Admin → Security → Users → New User
   - Username: github-actions
   - Password: Generate strong password
   - Groups: Add to "deployers" and "readers"

4. **Configure Permissions**
   - Admin → Security → Permissions
   - Create permission target for CI/CD
   - Assign to github-actions user

### Usage

#### Helm Charts

```bash
# Configure Helm to use Artifactory
helm repo add aikernel-charts https://artifactory.aikernel.local/artifactory/helm \
  --username github-actions \
  --password <password>

# Update repository
helm repo update

# Search for charts
helm search repo aikernel-charts

# Install chart
helm install myapp aikernel-charts/myapp

# Package and upload chart
helm package ./charts/myapp
curl -u github-actions:password \
  -X PUT \
  "https://artifactory.aikernel.local/artifactory/helm-local/myapp-1.0.0.tgz" \
  -T myapp-1.0.0.tgz
```

#### NuGet Packages

```bash
# Configure NuGet
dotnet nuget add source https://artifactory.aikernel.local/artifactory/api/nuget/v3/nuget \
  --name aikernel-nuget \
  --username github-actions \
  --password <password>

# Restore packages
dotnet restore

# Publish package
dotnet nuget push MyPackage.1.0.0.nupkg \
  --source aikernel-nuget \
  --api-key <api-key>
```

#### Generic Artifacts

```bash
# Upload build metadata
curl -u github-actions:password \
  -X PUT \
  "https://artifactory.aikernel.local/artifactory/generic-local/builds/123/metadata.json" \
  -T metadata.json

# Download artifact
curl -u github-actions:password \
  -O \
  "https://artifactory.aikernel.local/artifactory/generic-local/builds/123/metadata.json"
```

## CI/CD Integration

### GitHub Actions Integration

#### Application Deployment Workflow

The `application-deploy.yml` workflow now includes:

1. **Build Images**: Build Docker images
2. **Scan Images**: Trivy vulnerability scanning
3. **Sign Images**: Cosign image signing
4. **Push to Harbor**: Push to on-premise registry
5. **Upload Metadata**: Upload build info to Artifactory

Key changes:
```yaml
env:
  REGISTRY: harbor.aikernel.local  # Changed from ghcr.io
  IMAGE_NAME: aikernel

jobs:
  build:
    steps:
      - name: Log in to Harbor Registry
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ secrets.HARBOR_USERNAME }}
          password: ${{ secrets.HARBOR_PASSWORD }}

      - name: Scan image with Trivy
        # Fails build on HIGH/CRITICAL vulnerabilities

      - name: Sign image with Cosign
        # Signs production images

  upload-artifacts:
    steps:
      - name: Setup JFrog CLI
        # Upload build metadata to Artifactory
```

#### Infrastructure Deployment Workflow

The `infrastructure-deploy.yml` workflow now includes:

1. **Setup Helm**: Configure Helm CLI
2. **Add Artifactory Repo**: Add Helm repository
3. **Deploy Charts**: Deploy Helm charts from Artifactory

Key changes:
```yaml
- name: Configure Helm with Artifactory
  env:
    ARTIFACTORY_USER: ${{ secrets.ARTIFACTORY_USERNAME }}
    ARTIFACTORY_PASSWORD: ${{ secrets.ARTIFACTORY_PASSWORD }}
  run: |
    helm repo add aikernel-charts \
      https://artifactory.aikernel.local/artifactory/helm-local \
      --username "${ARTIFACTORY_USER}" \
      --password "${ARTIFACTORY_PASSWORD}"
```

### Required GitHub Secrets

Add these secrets to your GitHub repository:

```
HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<harbor-robot-token>
ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<artifactory-token>
COSIGN_PRIVATE_KEY=<base64-encoded-private-key>
COSIGN_PASSWORD=<cosign-key-password>
```

See `.github/SETUP_SECRETS.md` for detailed instructions.

## Security Features

### Image Vulnerability Scanning

Harbor integrates Trivy scanner for automatic vulnerability detection:

#### Scan on Push
- All images scanned automatically on push
- Results available in Harbor UI
- CI/CD pipeline fails on HIGH/CRITICAL vulnerabilities

#### Manual Scanning
```bash
# Scan specific image
curl -u admin:password -X POST \
  "https://harbor.aikernel.local/api/v2.0/projects/aikernel/repositories/myapp/artifacts/latest/scan"

# Get scan results
curl -u admin:password \
  "https://harbor.aikernel.local/api/v2.0/projects/aikernel/repositories/myapp/artifacts/latest/scan/\
{scan_req_id\}/log"
```

#### CVE Allowlist
For accepted vulnerabilities:
1. Harbor UI → Projects → aikernel → Configuration
2. Add CVE IDs to allowlist
3. Set expiration date
4. Add justification

### Image Signing with Cosign

All production images are signed with Cosign:

#### Generate Key Pair
```bash
# Generate keys
cosign generate-key-pair

# Store public key for verification
kubectl create configmap cosign-public-key \
  --from-file=cosign.pub=cosign.pub \
  -n aikernel-core
```

#### Sign Images (Automated in CI/CD)
```bash
cosign sign --key cosign.key harbor.aikernel.local/aikernel/myapp:v1.0.0
```

#### Verify Signatures
```bash
# Verify before deployment
cosign verify --key cosign.pub harbor.aikernel.local/aikernel/myapp:v1.0.0
```

#### Policy Enforcement
Use admission controllers to enforce signature verification:

```yaml
# Example: Kyverno policy
apiVersion: kyverno.io/v1
kind: ClusterPolicy
metadata:
  name: verify-image-signatures
spec:
  validationFailureAction: enforce
  rules:
  - name: verify-signatures
    match:
      resources:
        kinds:
        - Pod
    verifyImages:
    - image: "harbor.aikernel.local/aikernel/*"
      key: |-
        -----BEGIN PUBLIC KEY-----
        <cosign-public-key>
        -----END PUBLIC KEY-----
```

### Access Control

#### Harbor RBAC

Projects have role-based access:
- **Project Admin**: Full project control
- **Master**: Read/write/scan
- **Developer**: Read/write
- **Guest**: Read only
- **Limited Guest**: Pull only

Robot accounts for automation:
```bash
# Create robot account via API
curl -u admin:password -X POST \
  "https://harbor.aikernel.local/api/v2.0/robots" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "robot$cicd",
    "duration": 365,
    "permissions": [
      {
        "kind": "project",
        "namespace": "aikernel",
        "access": [
          {"resource": "repository", "action": "pull"},
          {"resource": "repository", "action": "push"}
        ]
      }
    ]
  }'
```

#### Artifactory Permissions

Permission targets control access:
```bash
# Create permission target
curl -u admin:password -X PUT \
  "https://artifactory.aikernel.local/artifactory/api/v2/security/permissions/cicd-deploy" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "cicd-deploy",
    "repo": {
      "repositories": ["helm-local", "nuget-local", "generic-local"],
      "actions": {
        "users": {
          "github-actions": ["read", "write", "annotate", "delete"]
        }
      }
    }
  }'
```

### Network Security

#### mTLS with Istio

Harbor and Artifactory are deployed in Istio service mesh:
```yaml
apiVersion: security.istio.io/v1beta1
kind: PeerAuthentication
metadata:
  name: default-mtls
  namespace: aikernel-registry
spec:
  mtls:
    mode: STRICT
```

#### Network Policies

Restrict access to registry services:
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: harbor-access
  namespace: aikernel-registry
spec:
  podSelector:
    matchLabels:
      app: harbor-registry
  policyTypes:
  - Ingress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: aikernel-core
    - namespaceSelector:
        matchLabels:
          name: aikernel-ai
    ports:
    - protocol: TCP
      port: 5000
```

## Deployment Guide

### Prerequisites

- Kubernetes cluster v1.28+
- 500GB+ storage for Harbor images
- 500GB+ storage for Artifactory artifacts
- NFS storage class or equivalent
- Ingress controller (NGINX)
- Certificate manager (cert-manager)

### Step-by-Step Deployment

#### 1. Deploy Harbor

```bash
# Step 1: Update secrets
kubectl create namespace aikernel-registry

# Generate secure passwords
HARBOR_DB_PASSWORD=$(openssl rand -base64 32)
HARBOR_ADMIN_PASSWORD=$(openssl rand -base64 16)
HARBOR_SECRET_KEY=$(openssl rand -base64 32)
HARBOR_REDIS_PASSWORD=$(openssl rand -base64 32)

# Update secrets in 23-harbor-registry.yaml
sed -i "s/CHANGE_ME_HARBOR_DB_PASSWORD/$HARBOR_DB_PASSWORD/g" infrastructure/prod/k8s/23-harbor-registry.yaml
sed -i "s/CHANGE_ME_HARBOR_ADMIN_PASSWORD/$HARBOR_ADMIN_PASSWORD/g" infrastructure/prod/k8s/23-harbor-registry.yaml
sed -i "s/CHANGE_ME_SECRET_KEY_32_CHARS_MIN/$HARBOR_SECRET_KEY/g" infrastructure/prod/k8s/23-harbor-registry.yaml
sed -i "s/CHANGE_ME_HARBOR_REDIS_PASSWORD/$HARBOR_REDIS_PASSWORD/g" infrastructure/prod/k8s/23-harbor-registry.yaml

# Step 2: Deploy Harbor
kubectl apply -f infrastructure/prod/k8s/23-harbor-registry.yaml

# Step 3: Deploy Trivy scanner
kubectl apply -f infrastructure/prod/k8s/24-harbor-trivy-scanner.yaml

# Step 4: Wait for deployment
kubectl wait --for=condition=ready pod -l app=harbor-core -n aikernel-registry --timeout=10m

# Step 5: Get access information
echo "Harbor URL: https://harbor.aikernel.local"
echo "Admin password: $HARBOR_ADMIN_PASSWORD"
```

#### 2. Deploy Artifactory

```bash
# Step 1: Update secrets
kubectl create namespace aikernel-artifactory

# Generate secure keys
ARTIFACTORY_DB_PASSWORD=$(openssl rand -base64 32)
ARTIFACTORY_MASTER_KEY=$(openssl rand -hex 32)
ARTIFACTORY_JOIN_KEY=$(openssl rand -hex 32)

# Update secrets in 25-artifactory.yaml
sed -i "s/CHANGE_ME_ARTIFACTORY_DB_PASSWORD/$ARTIFACTORY_DB_PASSWORD/g" infrastructure/prod/k8s/25-artifactory.yaml
sed -i "s/CHANGE_ME_MASTER_KEY_64_CHARS_HEX/$ARTIFACTORY_MASTER_KEY/g" infrastructure/prod/k8s/25-artifactory.yaml
sed -i "s/CHANGE_ME_JOIN_KEY_64_CHARS_HEX/$ARTIFACTORY_JOIN_KEY/g" infrastructure/prod/k8s/25-artifactory.yaml

# Step 2: Add license (required for Artifactory Pro)
# Edit 25-artifactory.yaml and paste your license in the artifactory-license secret

# Step 3: Deploy Artifactory
kubectl apply -f infrastructure/prod/k8s/25-artifactory.yaml

# Step 4: Wait for deployment (takes 5-10 minutes)
kubectl wait --for=condition=ready pod -l app=artifactory -n aikernel-artifactory --timeout=15m

# Step 5: Get access information
echo "Artifactory URL: https://artifactory.aikernel.local"
echo "Default username: admin"
echo "Default password: password (CHANGE IMMEDIATELY)"
```

#### 3. Configure DNS

For on-premise deployments:

```bash
# Option 1: Add to /etc/hosts
NODE_IP=$(kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
echo "$NODE_IP harbor.aikernel.local" | sudo tee -a /etc/hosts
echo "$NODE_IP artifactory.aikernel.local" | sudo tee -a /etc/hosts

# Option 2: Configure internal DNS
# Add A records:
# harbor.aikernel.local -> <node-ip> or <loadbalancer-ip>
# artifactory.aikernel.local -> <node-ip> or <loadbalancer-ip>
```

#### 4. Configure TLS

```bash
# If using cert-manager with self-signed certificates
kubectl apply -f - <<EOF
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: selfsigned-issuer
spec:
  selfSigned: {}
---
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: harbor-tls
  namespace: aikernel-registry
spec:
  secretName: harbor-tls
  issuerRef:
    name: selfsigned-issuer
    kind: ClusterIssuer
  dnsNames:
  - harbor.aikernel.local
---
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: artifactory-tls
  namespace: aikernel-artifactory
spec:
  secretName: artifactory-tls
  issuerRef:
    name: selfsigned-issuer
    kind: ClusterIssuer
  dnsNames:
  - artifactory.aikernel.local
EOF
```

#### 5. Initial Configuration

Complete initial setup via UI:
1. Access Harbor and Artifactory UIs
2. Change default passwords
3. Create projects/repositories
4. Create robot/service accounts
5. Configure scanning policies
6. Set up retention policies

#### 6. Integrate with CI/CD

```bash
# Add secrets to GitHub
# See .github/SETUP_SECRETS.md for details

# Test integration
git push origin develop
# Watch GitHub Actions workflow
```

## Operations

### Backup and Restore

#### Harbor Backup

Automated daily backups:
```bash
# Backup CronJob runs daily at 1 AM
kubectl get cronjob harbor-backup -n aikernel-registry

# Manual backup
kubectl create job harbor-backup-manual \
  --from=cronjob/harbor-backup \
  -n aikernel-registry

# List backups
kubectl exec -it harbor-database-0 -n aikernel-registry -- ls -lh /backup/

# Restore from backup
kubectl exec -it harbor-database-0 -n aikernel-registry -- \
  pg_restore -U harbor -d registry /backup/harbor_db_20240101_010000.dump
```

#### Artifactory Backup

```bash
# Backup CronJob runs daily at 1 AM
kubectl get cronjob artifactory-backup -n aikernel-artifactory

# Manual backup
kubectl create job artifactory-backup-manual \
  --from=cronjob/artifactory-backup \
  -n aikernel-artifactory

# Export configuration
curl -u admin:password \
  "https://artifactory.aikernel.local/artifactory/api/export/system" \
  -o artifactory-config-backup.zip
```

### Monitoring

#### Metrics

Both Harbor and Artifactory expose Prometheus metrics:

```yaml
# ServiceMonitor for Harbor
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: harbor
  namespace: aikernel-registry
spec:
  selector:
    matchLabels:
      app: harbor-core
  endpoints:
  - port: metrics
    path: /metrics
```

Key metrics to monitor:
- Registry pull/push operations
- Storage usage
- Scan duration
- Database connections
- Cache hit ratio

#### Logs

```bash
# Harbor logs
kubectl logs -f deployment/harbor-core -n aikernel-registry
kubectl logs -f deployment/harbor-registry -n aikernel-registry
kubectl logs -f statefulset/harbor-trivy -n aikernel-registry

# Artifactory logs
kubectl logs -f statefulset/artifactory -n aikernel-artifactory
```

#### Health Checks

```bash
# Harbor health
curl -u admin:password https://harbor.aikernel.local/api/v2.0/health

# Artifactory health
curl -u admin:password https://artifactory.aikernel.local/artifactory/api/system/ping
```

### Scaling

#### Harbor Scaling

Harbor core and registry can be scaled horizontally:

```bash
# Scale Harbor core
kubectl scale deployment harbor-core --replicas=3 -n aikernel-registry

# Scale Harbor registry
kubectl scale deployment harbor-registry --replicas=3 -n aikernel-registry

# Scale Harbor portal
kubectl scale deployment harbor-portal --replicas=2 -n aikernel-registry
```

#### Artifactory Scaling

Artifactory Pro supports HA with multiple nodes:

```bash
# Scale Artifactory (requires Pro license with HA)
kubectl scale statefulset artifactory --replicas=3 -n aikernel-artifactory
```

### Maintenance

#### Garbage Collection

Harbor garbage collection removes unused layers:

```bash
# Trigger GC via API
curl -u admin:password -X POST \
  "https://harbor.aikernel.local/api/v2.0/system/gc/schedule" \
  -H "Content-Type: application/json" \
  -d '{
    "schedule": {
      "type": "Manual"
    },
    "delete_untagged": true,
    "workers": 2
  }'

# Monitor GC job
kubectl logs -f job/harbor-gc-<id> -n aikernel-registry
```

Automated daily GC runs at 2 AM via CronJob.

#### Storage Cleanup

```bash
# Check storage usage
kubectl exec -it harbor-registry-<pod> -n aikernel-registry -- df -h /storage
kubectl exec -it artifactory-0 -n aikernel-artifactory -- df -h /var/opt/jfrog/artifactory

# Artifactory storage summary
curl -u admin:password \
  "https://artifactory.aikernel.local/artifactory/api/storageinfo"
```

#### Database Maintenance

```bash
# PostgreSQL vacuum (Harbor)
kubectl exec -it harbor-database-0 -n aikernel-registry -- \
  vacuumdb -U harbor -d registry -v -z

# PostgreSQL vacuum (Artifactory)
kubectl exec -it artifactory-database-0 -n aikernel-artifactory -- \
  vacuumdb -U artifactory -d artifactory -v -z
```

### Upgrading

#### Harbor Upgrade

```bash
# Backup first!
kubectl create job harbor-backup-pre-upgrade \
  --from=cronjob/harbor-backup \
  -n aikernel-registry

# Update image versions in 23-harbor-registry.yaml
# Example: goharbor/harbor-core:v2.10.0 -> v2.11.0

# Apply updates
kubectl apply -f infrastructure/prod/k8s/23-harbor-registry.yaml

# Monitor rollout
kubectl rollout status deployment/harbor-core -n aikernel-registry
```

#### Artifactory Upgrade

```bash
# Backup first!
kubectl create job artifactory-backup-pre-upgrade \
  --from=cronjob/artifactory-backup \
  -n aikernel-artifactory

# Update image version in 25-artifactory.yaml
# Example: jfrog/artifactory-pro:7.77.5 -> 7.78.0

# Apply updates
kubectl apply -f infrastructure/prod/k8s/25-artifactory.yaml

# Monitor upgrade (check logs)
kubectl logs -f statefulset/artifactory -n aikernel-artifactory
```

## Troubleshooting

### Common Issues

#### Harbor Issues

**Issue: Cannot push images**
```bash
# Check Harbor registry logs
kubectl logs deployment/harbor-registry -n aikernel-registry

# Check storage space
kubectl exec harbor-registry-<pod> -n aikernel-registry -- df -h /storage

# Verify authentication
docker login harbor.aikernel.local
```

**Issue: Scanning fails**
```bash
# Check Trivy logs
kubectl logs statefulset/harbor-trivy -n aikernel-registry

# Update Trivy database
kubectl delete pod -l app=harbor-trivy -n aikernel-registry
```

**Issue: Harbor UI not accessible**
```bash
# Check all components
kubectl get pods -n aikernel-registry

# Check ingress
kubectl get ingress -n aikernel-registry
kubectl describe ingress harbor-ingress -n aikernel-registry

# Check services
kubectl get svc -n aikernel-registry
```

#### Artifactory Issues

**Issue: Artifactory not starting**
```bash
# Check logs
kubectl logs statefulset/artifactory -n aikernel-artifactory

# Common causes:
# 1. Database connection failure
kubectl exec artifactory-database-0 -n aikernel-artifactory -- pg_isready

# 2. License issues
kubectl get secret artifactory-license -n aikernel-artifactory -o yaml

# 3. Storage issues
kubectl exec artifactory-0 -n aikernel-artifactory -- df -h
```

**Issue: Helm repo not accessible**
```bash
# Check Artifactory nginx logs
kubectl logs deployment/artifactory-nginx -n aikernel-artifactory

# Test direct access
curl -u admin:password \
  https://artifactory.aikernel.local/artifactory/api/system/ping

# Check Helm repo index
curl -u admin:password \
  https://artifactory.aikernel.local/artifactory/helm-local/index.yaml
```

#### Network Issues

**Issue: Cannot resolve harbor.aikernel.local**
```bash
# Check DNS
nslookup harbor.aikernel.local

# Check /etc/hosts
cat /etc/hosts | grep harbor

# Check CoreDNS (if using in-cluster)
kubectl get configmap coredns -n kube-system -o yaml
```

**Issue: SSL certificate errors**
```bash
# Check certificate
openssl s_client -connect harbor.aikernel.local:443 -showcerts

# For self-signed certificates, add to trust store:
# On Linux:
sudo cp harbor-ca.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates

# For Docker:
sudo mkdir -p /etc/docker/certs.d/harbor.aikernel.local
sudo cp harbor-ca.crt /etc/docker/certs.d/harbor.aikernel.local/ca.crt
sudo systemctl restart docker
```

#### Performance Issues

**Issue: Slow image pulls**
```bash
# Check network bandwidth
kubectl exec -it harbor-registry-<pod> -n aikernel-registry -- iperf3 -c <client>

# Check storage I/O
kubectl exec -it harbor-registry-<pod> -n aikernel-registry -- \
  dd if=/dev/zero of=/storage/test bs=1G count=1 oflag=direct

# Check Redis cache
kubectl exec -it harbor-redis-0 -n aikernel-registry -- redis-cli INFO stats
```

**Issue: High database load**
```bash
# Check PostgreSQL stats
kubectl exec -it harbor-database-0 -n aikernel-registry -- \
  psql -U harbor -c "SELECT * FROM pg_stat_activity;"

# Check slow queries
kubectl exec -it harbor-database-0 -n aikernel-registry -- \
  psql -U harbor -c "SELECT query, calls, total_time FROM pg_stat_statements ORDER BY total_time DESC LIMIT 10;"
```

### Debug Commands

```bash
# Get all resources
kubectl get all -n aikernel-registry
kubectl get all -n aikernel-artifactory

# Describe problematic pods
kubectl describe pod <pod-name> -n aikernel-registry

# Check events
kubectl get events -n aikernel-registry --sort-by='.lastTimestamp'

# Check PVC status
kubectl get pvc -n aikernel-registry
kubectl get pvc -n aikernel-artifactory

# Execute shell in pod
kubectl exec -it harbor-core-<pod> -n aikernel-registry -- /bin/sh
kubectl exec -it artifactory-0 -n aikernel-artifactory -- /bin/bash

# Port forward for local testing
kubectl port-forward svc/harbor-core 8080:8080 -n aikernel-registry
kubectl port-forward svc/artifactory 8081:8081 -n aikernel-artifactory
```

## Additional Resources

### Documentation
- [Harbor Documentation](https://goharbor.io/docs/)
- [JFrog Artifactory Documentation](https://www.jfrog.com/confluence/display/JFROG/JFrog+Artifactory)
- [Trivy Documentation](https://aquasecurity.github.io/trivy/)
- [Cosign Documentation](https://docs.sigstore.dev/cosign/overview/)

### API References
- [Harbor REST API](https://goharbor.io/docs/latest/build-customize-contribute/configure-swagger/)
- [Artifactory REST API](https://www.jfrog.com/confluence/display/JFROG/Artifactory+REST+API)

### Tools
- [Harbor CLI (harborctl)](https://github.com/goharbor/harbor-cli)
- [JFrog CLI](https://www.jfrog.com/confluence/display/CLI/JFrog+CLI)
- [Helm](https://helm.sh/)
- [Cosign](https://github.com/sigstore/cosign)

## Support

For issues or questions:
1. Check troubleshooting section above
2. Review component logs
3. Consult official documentation
4. Open an issue with detailed information

---

**Production-Grade Artifact Management for AI Kernel Platform**
- Harbor: Enterprise container registry
- Artifactory: Universal artifact repository
- Trivy: Vulnerability scanning
- Cosign: Image signing
- Complete CI/CD integration
