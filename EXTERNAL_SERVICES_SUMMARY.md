# External Services Integration - Summary

## Overview

The AI Kernel Platform is configured to integrate with **external bare-metal servers** running Harbor (container registry), Artifactory (artifact repository), and MinIO (S3 storage).

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              External Bare-Metal Servers                  │
│                                                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐      │
│  │   Harbor    │  │ Artifactory │  │   MinIO     │      │
│  │   Server    │  │   Server    │  │   Server    │      │
│  │             │  │             │  │             │      │
│  │ Port: 443   │  │ Port: 443   │  │ Port: 9000  │      │
│  │             │  │             │  │      9001   │      │
│  └─────────────┘  └─────────────┘  └─────────────┘      │
│        ↕                ↕                  ↕              │
└──────────────────────────────────────────────────────────┘
         ↕                ↕                  ↕
┌──────────────────────────────────────────────────────────┐
│            Kubernetes Cluster (On-Premise)                │
│                                                           │
│  • ExternalName Services for DNS resolution              │
│  • ImagePullSecrets for Harbor authentication            │
│  • ConfigMaps with endpoint configuration                │
│  • Secrets for credentials                               │
└──────────────────────────────────────────────────────────┘
         ↕
┌──────────────────────────────────────────────────────────┐
│              GitHub Actions CI/CD                         │
│                                                           │
│  • Build images → Push to Harbor                         │
│  • Scan with Trivy (in Harbor)                          │
│  • Sign with Cosign                                      │
│  • Upload artifacts to Artifactory                       │
│  • Deploy to Kubernetes                                  │
└──────────────────────────────────────────────────────────┘
```

## What Was Configured

### 1. External Services Configuration

**File**: `infrastructure/prod/k8s/27-external-services-config.yaml`

**Components**:
- ✅ ExternalName Services for Harbor, Artifactory, MinIO
- ✅ Secrets for credentials (Harbor, Artifactory, MinIO)
- ✅ ConfigMap with service endpoints
- ✅ ImagePullSecrets for Harbor
- ✅ NetworkPolicy for external access
- ✅ RBAC for external services access
- ✅ EndpointSlice templates (for IP-based access)

### 2. Comprehensive Setup Guide

**File**: `infrastructure/prod/k8s/EXTERNAL_SERVICES_SETUP.md` (1,200+ lines)

**Covers**:
- **MinIO Setup**: Installation, configuration, systemd service, SSL/TLS, bucket creation
- **Harbor Setup**: Installation with Docker Compose, MinIO S3 backend, Trivy scanner, SSL certificates
- **Artifactory Setup**: Installation, PostgreSQL database, Nginx reverse proxy, SSL configuration
- **Kubernetes Integration**: External services, secrets, DNS configuration
- **CI/CD Configuration**: GitHub secrets setup
- **Backup Strategy**: Automated backups for all services
- **Monitoring**: Health checks and status monitoring
- **Troubleshooting**: Common issues and solutions

### 3. Updated CI/CD Workflows

**Files Modified**:
- `.github/workflows/application-deploy.yml` ✅ (Already configured for external Harbor/Artifactory)
- `.github/workflows/infrastructure-deploy.yml` ✅ (Already configured for external Artifactory)

**No changes needed** - workflows use `HARBOR_USERNAME`, `HARBOR_PASSWORD`, `ARTIFACTORY_USERNAME`, `ARTIFACTORY_PASSWORD` which work with both Kubernetes and bare-metal deployments!

### 4. Updated Secrets Documentation

**File**: `.github/SETUP_SECRETS.md`

**Updated Sections**:
- Harbor Container Registry Secrets (marked as **External Bare-Metal Server**)
- JFrog Artifactory Secrets (marked as **External Bare-Metal Server**)
- MinIO S3 Storage Secrets (new section for bare-metal MinIO)
- Instructions updated to reference SSH access to bare-metal servers

### 5. Updated Deployment Script

**File**: `infrastructure/prod/k8s/deploy-all.sh`

**Changes**:
- ❌ Removed Step 10 & 11 (Harbor and Artifactory in-cluster deployment)
- ✅ Added Step 10: Configure External Services Integration
- ✅ Deploys `27-external-services-config.yaml`
- ✅ Shows external service URLs in deployment summary
- ✅ Removed Harbor/Artifactory from namespace checks

## Server Requirements

### 1. Harbor Bare-Metal Server

**Specifications**:
- OS: Linux (Ubuntu 20.04+, CentOS 8+)
- CPU: 4+ cores
- RAM: 8+ GB
- Storage: 500+ GB
- Software: Docker + Docker Compose

**Services**:
- Harbor Core (container registry)
- Harbor Portal (web UI)
- Harbor JobService (background jobs)
- PostgreSQL (metadata)
- Redis (caching)
- Trivy Scanner (vulnerability scanning)
- Nginx (reverse proxy)

**Ports**:
- 80 (HTTP)
- 443 (HTTPS)

### 2. Artifactory Bare-Metal Server

**Specifications**:
- OS: Linux (Ubuntu 20.04+, CentOS 8+)
- CPU: 4+ cores
- RAM: 8+ GB
- Storage: 500+ GB
- Software: Java 11+, Nginx

**Services**:
- Artifactory (artifact repository)
- PostgreSQL (metadata)
- Nginx (reverse proxy)

**Ports**:
- 80 (HTTP)
- 443 (HTTPS)

### 3. MinIO Bare-Metal Server

**Specifications**:
- OS: Linux (Ubuntu 20.04+, CentOS 8+)
- CPU: 2+ cores
- RAM: 4+ GB
- Storage: 1+ TB
- Software: MinIO binary

**Services**:
- MinIO Server (S3 API)
- MinIO Console (web UI)

**Ports**:
- 9000 (S3 API)
- 9001 (Console)

## Network Requirements

### DNS Configuration

All services must be accessible via DNS or /etc/hosts:

```bash
# Add to /etc/hosts on all Kubernetes nodes and CI/CD runners
192.168.1.10    harbor.aikernel.local
192.168.1.11    artifactory.aikernel.local
192.168.1.12    minio.aikernel.local
```

Or configure internal DNS:
- `harbor.aikernel.local` → Harbor server IP
- `artifactory.aikernel.local` → Artifactory server IP
- `minio.aikernel.local` → MinIO server IP

### Firewall Rules

**From Kubernetes Cluster**:
- Allow outbound to Harbor: 443, 80
- Allow outbound to Artifactory: 443, 80
- Allow outbound to MinIO: 9000, 9001

**From CI/CD Runners** (GitHub Actions or self-hosted):
- Allow outbound to Harbor: 443, 80
- Allow outbound to Artifactory: 443, 80
- Allow outbound to MinIO: 9000 (if used for backups)

**To External Services**:
- Harbor: Allow inbound 443, 80 from Kubernetes nodes and CI/CD
- Artifactory: Allow inbound 443, 80 from Kubernetes nodes and CI/CD
- MinIO: Allow inbound 9000, 9001 from Harbor server

## Configuration Management

### Environment Variables (.env)

The platform uses a `.env` file for centralized configuration:

```bash
# 1. Copy template
cp .env.example .env

# 2. Edit with your values
vi .env
# Update all CHANGE_ME_* placeholders

# 3. Load environment
source scripts/load-env.sh

# 4. Verify
./scripts/load-env.sh
```

**Key Variables** (see `.env.example` for complete list):
- `HARBOR_URL`, `HARBOR_USERNAME`, `HARBOR_PASSWORD`
- `ARTIFACTORY_URL`, `ARTIFACTORY_USERNAME`, `ARTIFACTORY_PASSWORD`
- `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY`
- Server IPs for SSH access

**Security**:
- ✅ `.env` is gitignored - never commit
- ✅ Generate strong passwords: `openssl rand -base64 32`
- ✅ Rotate credentials quarterly

## Quick Start

### Step 0: Configure Environment

```bash
# Setup .env file first
cp .env.example .env
vi .env  # Update all credentials
source scripts/load-env.sh
```

### Step 1: Setup MinIO (S3 Storage)

```bash
# On MinIO bare-metal server
wget https://dl.min.io/server/minio/release/linux-amd64/minio
chmod +x minio
sudo mv minio /usr/local/bin/

# Configure and start (see EXTERNAL_SERVICES_SETUP.md for full instructions)
sudo systemctl enable minio
sudo systemctl start minio

# Create bucket for Harbor
mc mb local/harbor-registry
```

### Step 2: Setup Harbor (Container Registry)

```bash
# On Harbor bare-metal server
cd /opt
sudo wget https://github.com/goharbor/harbor/releases/download/v2.10.0/harbor-offline-installer-v2.10.0.tgz
sudo tar xzvf harbor-offline-installer-v2.10.0.tgz
cd harbor

# Configure harbor.yml with MinIO S3 backend
sudo cp harbor.yml.tmpl harbor.yml
# Edit harbor.yml (see EXTERNAL_SERVICES_SETUP.md)

# Install Harbor
sudo ./install.sh --with-trivy

# Verify
sudo docker-compose ps
```

### Step 3: Setup Artifactory (Artifact Repository)

```bash
# On Artifactory bare-metal server
cd /opt
sudo wget https://releases.jfrog.io/artifactory/bintray-artifactory/org/artifactory/oss/jfrog-artifactory-oss/[RELEASE]/jfrog-artifactory-oss-[RELEASE]-linux.tar.gz
sudo tar -xzf jfrog-artifactory-oss-*.tar.gz

# Configure and start (see EXTERNAL_SERVICES_SETUP.md for full instructions)
sudo systemctl enable artifactory
sudo systemctl start artifactory
```

### Step 4: Configure Kubernetes Integration

```bash
# Deploy external services configuration
kubectl apply -f infrastructure/prod/k8s/27-external-services-config.yaml

# Create Harbor credentials secret
kubectl create secret docker-registry harbor-registry-credentials \
  --docker-server=harbor.aikernel.local \
  --docker-username=robot\$github-actions \
  --docker-password=<harbor-robot-token> \
  --namespace=aikernel-core

# Create Artifactory credentials secret
kubectl create secret generic artifactory-credentials \
  --from-literal=ARTIFACTORY_URL=https://artifactory.aikernel.local \
  --from-literal=ARTIFACTORY_USERNAME=github-actions \
  --from-literal=ARTIFACTORY_PASSWORD=<artifactory-token> \
  --namespace=aikernel-core
```

### Step 5: Configure GitHub Secrets

Add to GitHub repository secrets:

```bash
HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<harbor-robot-token>

ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<artifactory-token>

COSIGN_PRIVATE_KEY=<base64-encoded-cosign-key>
COSIGN_PASSWORD=<cosign-key-password>
```

### Step 6: Test Integration

```bash
# Test Harbor from Kubernetes node
docker login harbor.aikernel.local
docker pull alpine:latest
docker tag alpine:latest harbor.aikernel.local/aikernel/test:latest
docker push harbor.aikernel.local/aikernel/test:latest

# Test Artifactory
helm repo add aikernel-charts https://artifactory.aikernel.local/artifactory/helm-local \
  --username github-actions --password <password>
helm repo update

# Test from Kubernetes
kubectl run test-harbor --image=harbor.aikernel.local/aikernel/test:latest \
  --image-pull-secrets=harbor-registry-credentials -n aikernel-core
```

## GitHub Secrets Required

```bash
# Harbor (External Bare-Metal)
HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<token-from-harbor-ui>

# Artifactory (External Bare-Metal)
ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<token-from-artifactory-ui>

# MinIO (Optional for backups)
MINIO_ACCESS_KEY=harbor-service
MINIO_SECRET_KEY=<minio-password>

# Image Signing
COSIGN_PRIVATE_KEY=<base64-encoded-private-key>
COSIGN_PASSWORD=<cosign-password>

# Kubernetes (Existing)
KUBECONFIG_DEV=<base64-kubeconfig>
KUBECONFIG_STAGING=<base64-kubeconfig>
KUBECONFIG_PROD=<base64-kubeconfig>

# ArgoCD (Existing)
ARGOCD_SERVER=<argocd-url>
ARGOCD_TOKEN=<argocd-token>
```

## Files Created/Modified

### New Files (2)
1. **infrastructure/prod/k8s/27-external-services-config.yaml** (250 lines)
   - ExternalName services
   - Secrets and ConfigMaps
   - RBAC configuration
   - Network policies

2. **infrastructure/prod/k8s/EXTERNAL_SERVICES_SETUP.md** (1,200+ lines)
   - Complete setup guide for MinIO
   - Complete setup guide for Harbor
   - Complete setup guide for Artifactory
   - Kubernetes integration
   - CI/CD configuration
   - Backup and monitoring
   - Troubleshooting

3. **EXTERNAL_SERVICES_SUMMARY.md** (This file)

### Modified Files (3)
1. **.github/SETUP_SECRETS.md**
   - Updated Harbor secrets (marked as external bare-metal)
   - Updated Artifactory secrets (marked as external bare-metal)
   - Added MinIO secrets section

2. **infrastructure/prod/k8s/deploy-all.sh**
   - Removed Harbor/Artifactory in-cluster deployment steps
   - Added external services configuration step
   - Updated deployment summary

3. **CI/CD Workflows** (No changes needed)
   - Already compatible with external services

### Files Removed (None Needed)
The previous Harbor and Artifactory Kubernetes manifests can remain for reference:
- `23-harbor-registry.yaml` (not deployed)
- `24-harbor-trivy-scanner.yaml` (not deployed)
- `25-artifactory.yaml` (not deployed)
- `26-helm-artifactory-integration.sh` (still useful)

## Benefits of External Bare-Metal Deployment

### Performance
✅ Dedicated resources per service
✅ No resource contention with Kubernetes workloads
✅ Optimized storage I/O
✅ Lower latency for large image pulls

### Scalability
✅ Independent scaling of each service
✅ Easier to upgrade storage
✅ Can use specialized hardware (NVMe, high-speed network)

### Reliability
✅ Services survive Kubernetes cluster issues
✅ Independent maintenance windows
✅ Easier disaster recovery
✅ Services remain available during Kubernetes upgrades

### Operations
✅ Simpler backup strategies
✅ Direct server access for troubleshooting
✅ Easier to integrate with existing infrastructure
✅ No Kubernetes overhead

### Cost
✅ Better resource utilization
✅ No need for Kubernetes storage provisioner
✅ Can use existing bare-metal servers
✅ Lower licensing costs (if applicable)

## Verification Checklist

After setup, verify:

- [ ] MinIO is running and accessible
- [ ] MinIO bucket created for Harbor
- [ ] Harbor is running with all containers up
- [ ] Harbor configured to use MinIO S3 backend
- [ ] Harbor robot account created for CI/CD
- [ ] Artifactory is running
- [ ] Artifactory repositories created (helm, docker, nuget, generic)
- [ ] Artifactory service account created for CI/CD
- [ ] DNS or /etc/hosts configured on all nodes
- [ ] Kubernetes ExternalName services deployed
- [ ] Harbor credentials secret created in Kubernetes
- [ ] Artifactory credentials secret created in Kubernetes
- [ ] GitHub secrets configured
- [ ] Docker login to Harbor succeeds from K8s nodes
- [ ] Helm repo add for Artifactory succeeds
- [ ] Test pod can pull image from Harbor
- [ ] CI/CD pipeline runs successfully

## Monitoring

### Harbor Health
```bash
# SSH to Harbor server
ssh harbor-server
sudo docker-compose ps
sudo docker-compose logs -f --tail=100

# Check storage
mc du aikernel-minio/harbor-registry
```

### Artifactory Health
```bash
# SSH to Artifactory server
ssh artifactory-server
sudo systemctl status artifactory
sudo tail -f /opt/artifactory/var/log/artifactory-service.log

# API check
curl -u admin:password https://artifactory.aikernel.local/artifactory/api/system/ping
```

### MinIO Health
```bash
# SSH to MinIO server
ssh minio-server
sudo systemctl status minio
mc admin info aikernel-minio

# Check storage
df -h /data/minio
```

## Backup

All three services have automated daily backups configured:

- **MinIO**: Mirror to backup location daily
- **Harbor**: PostgreSQL database dump daily
- **Artifactory**: System export daily

See `EXTERNAL_SERVICES_SETUP.md` for backup configuration details.

## Support

**Documentation**:
- Setup Guide: `infrastructure/prod/k8s/EXTERNAL_SERVICES_SETUP.md`
- Secrets Setup: `.github/SETUP_SECRETS.md`
- CI/CD Workflows: `.github/workflows/README.md`
- Integration Details: `HARBOR_ARTIFACTORY_INTEGRATION.md` (for reference)

**For Issues**:
1. Check service status on bare-metal servers
2. Verify network connectivity from Kubernetes
3. Check DNS resolution
4. Review service logs
5. Consult troubleshooting section in EXTERNAL_SERVICES_SETUP.md

---

**External Services Successfully Integrated!**

Harbor (Bare-Metal) + Artifactory (Bare-Metal) + MinIO (Bare-Metal) + Kubernetes Integration
