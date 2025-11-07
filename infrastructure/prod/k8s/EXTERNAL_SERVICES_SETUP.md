# External Services Setup Guide

This guide explains how to configure AI Kernel Platform to work with **external bare-metal** Harbor, Artifactory, and MinIO servers.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              GitHub Actions CI/CD                            │
│          Build → Scan → Sign → Deploy                       │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│                 Bare-Metal Servers                           │
│                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────┐  │
│  │  Harbor Server   │  │ Artifactory      │  │  MinIO   │  │
│  │  (Bare-Metal)    │  │ Server           │  │  Server  │  │
│  │                  │  │ (Bare-Metal)     │  │ (Bare-   │  │
│  │ • Docker images  │  │                  │  │  Metal)  │  │
│  │ • Trivy scanner  │  │ • Helm charts    │  │          │  │
│  │ • Image signing  │  │ • NuGet packages │  │ • S3 API │  │
│  │                  │  │ • Build metadata │  │ • Harbor │  │
│  │ Uses MinIO for   │  │                  │  │   backend│  │
│  │ storage ────────────→                  │  │ • Backups│  │
│  └──────────────────┘  └──────────────────┘  └──────────┘  │
│        ↑ 443/80              ↑ 443/80           ↑ 9000/9001│
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│          Kubernetes Cluster (On-Premise)                     │
│                                                              │
│  • Pulls images from Harbor                                 │
│  • Pulls Helm charts from Artifactory                       │
│  • ExternalName services for DNS resolution                 │
│  • Network policies for external access                     │
│  • ImagePullSecrets for Harbor authentication               │
└─────────────────────────────────────────────────────────────┘
```

## Environment Configuration

### Using .env File

The platform uses a `.env` file for configuration management. This makes it easy to manage different environments and keep credentials secure.

**Setup**:
```bash
# 1. Copy the example file
cp .env.example .env

# 2. Edit .env with your actual values
vi .env

# Update all CHANGE_ME_* placeholders with real values:
# - HARBOR_PASSWORD
# - ARTIFACTORY_PASSWORD
# - MINIO_SECRET_KEY
# - POSTGRES_PASSWORD
# - etc.

# 3. Load environment variables
source scripts/load-env.sh

# 4. Verify configuration
./scripts/load-env.sh
```

**Important**:
- ✅ `.env` is in `.gitignore` - never commit it
- ✅ Use `.env.example` as template
- ✅ Generate strong passwords: `openssl rand -base64 32`
- ✅ Rotate credentials quarterly

### Environment Variables Reference

See `.env.example` for complete list of variables. Key variables:

```bash
# External Services
HARBOR_URL=https://harbor.aikernel.local
HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<your-harbor-token>

ARTIFACTORY_URL=https://artifactory.aikernel.local
ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<your-artifactory-token>

MINIO_ENDPOINT=minio.aikernel.local:9000
MINIO_ACCESS_KEY=harbor-service
MINIO_SECRET_KEY=<your-minio-password>

# Server IPs
HARBOR_SERVER_IP=192.168.1.10
ARTIFACTORY_SERVER_IP=192.168.1.11
MINIO_SERVER_IP=192.168.1.12
```

## Prerequisites

### Bare-Metal Servers

You need three separate servers:

1. **Harbor Server**
   - OS: Linux (Ubuntu 20.04+, CentOS 8+, or similar)
   - CPU: 4+ cores
   - RAM: 8+ GB
   - Storage: 500+ GB (for images)
   - Docker + Docker Compose installed

2. **Artifactory Server**
   - OS: Linux (Ubuntu 20.04+, CentOS 8+, or similar)
   - CPU: 4+ cores
   - RAM: 8+ GB
   - Storage: 500+ GB (for artifacts)
   - Java 11+ installed

3. **MinIO Server**
   - OS: Linux (Ubuntu 20.04+, CentOS 8+, or similar)
   - CPU: 2+ cores
   - RAM: 4+ GB
   - Storage: 1+ TB (for object storage)
   - MinIO binary installed

### Network Requirements

- All servers must be accessible from:
  - Kubernetes cluster nodes
  - CI/CD runners (GitHub Actions or self-hosted)
- DNS names or static IPs configured
- Firewall rules allowing:
  - Harbor: 80/443 (HTTP/HTTPS)
  - Artifactory: 80/443 (HTTP/HTTPS)
  - MinIO: 9000 (API), 9001 (Console)

## MinIO Setup (S3 Storage)

MinIO provides S3-compatible object storage for Harbor and backups.

### 1. Install MinIO on Bare-Metal

```bash
# Download MinIO binary
wget https://dl.min.io/server/minio/release/linux-amd64/minio
chmod +x minio
sudo mv minio /usr/local/bin/

# Create MinIO user and directories
sudo useradd -r minio-user -s /sbin/nologin
sudo mkdir -p /data/minio
sudo chown -R minio-user:minio-user /data/minio

# Create MinIO configuration directory
sudo mkdir -p /etc/minio
sudo chown minio-user:minio-user /etc/minio
```

### 2. Configure MinIO

```bash
# Create MinIO environment file
sudo tee /etc/default/minio > /dev/null <<EOF
# MinIO local volumes
MINIO_VOLUMES="/data/minio"

# MinIO root credentials
MINIO_ROOT_USER=admin
MINIO_ROOT_PASSWORD=$(openssl rand -base64 32)

# MinIO options
MINIO_OPTS="--console-address :9001"

# MinIO server URL
MINIO_SERVER_URL="https://minio.aikernel.local"
EOF

# Secure the file
sudo chmod 600 /etc/default/minio
```

### 3. Create MinIO Systemd Service

```bash
sudo tee /etc/systemd/system/minio.service > /dev/null <<EOF
[Unit]
Description=MinIO
Documentation=https://docs.min.io
Wants=network-online.target
After=network-online.target
AssertFileIsExecutable=/usr/local/bin/minio

[Service]
WorkingDirectory=/usr/local

User=minio-user
Group=minio-user

EnvironmentFile=/etc/default/minio
ExecStart=/usr/local/bin/minio server \$MINIO_OPTS \$MINIO_VOLUMES

# Let systemd restart this service always
Restart=always

# Specifies the maximum file descriptor number that can be opened by this process
LimitNOFILE=65536

# Specifies the maximum number of threads this process can create
TasksMax=infinity

# Disable timeout logic and wait until process is stopped
TimeoutStopSec=infinity
SendSIGKILL=no

[Install]
WantedBy=multi-user.target
EOF

# Enable and start MinIO
sudo systemctl daemon-reload
sudo systemctl enable minio
sudo systemctl start minio

# Check status
sudo systemctl status minio
```

### 4. Configure MinIO for Harbor

```bash
# Install MinIO client (mc)
wget https://dl.min.io/client/mc/release/linux-amd64/mc
chmod +x mc
sudo mv mc /usr/local/bin/

# Configure mc alias
mc alias set local http://localhost:9000 admin <password-from-/etc/default/minio>

# Create bucket for Harbor
mc mb local/harbor-registry

# Create service account for Harbor
mc admin user add local harbor-service <harbor-service-password>
mc admin policy attach local readwrite --user=harbor-service

# Get credentials for Harbor
echo "MinIO Access Key: harbor-service"
echo "MinIO Secret Key: <harbor-service-password>"
echo "MinIO Endpoint: minio.aikernel.local:9000"
echo "MinIO Bucket: harbor-registry"
```

### 5. Setup SSL/TLS for MinIO (Optional but Recommended)

```bash
# Generate self-signed certificate (or use Let's Encrypt)
sudo mkdir -p /etc/minio/certs
sudo openssl req -new -x509 -days 365 -nodes \
  -out /etc/minio/certs/public.crt \
  -keyout /etc/minio/certs/private.key \
  -subj "/CN=minio.aikernel.local"

sudo chown -R minio-user:minio-user /etc/minio/certs

# Restart MinIO
sudo systemctl restart minio

# Access MinIO Console
echo "MinIO Console: https://minio.aikernel.local:9001"
```

## Harbor Setup (Container Registry)

Harbor will use MinIO as its storage backend.

### 1. Install Harbor on Bare-Metal

```bash
# Install Docker and Docker Compose
sudo apt-get update
sudo apt-get install -y docker.io docker-compose

# Download Harbor installer
cd /opt
sudo wget https://github.com/goharbor/harbor/releases/download/v2.10.0/harbor-offline-installer-v2.10.0.tgz
sudo tar xzvf harbor-offline-installer-v2.10.0.tgz
cd harbor
```

### 2. Configure Harbor with MinIO Backend

```bash
# Copy and edit harbor.yml
sudo cp harbor.yml.tmpl harbor.yml

# Edit harbor.yml with the following configuration:
sudo tee harbor.yml > /dev/null <<EOF
hostname: harbor.aikernel.local

# HTTP/HTTPS configuration
http:
  port: 80

https:
  port: 443
  certificate: /data/cert/server.crt
  private_key: /data/cert/server.key

# Harbor admin password
harbor_admin_password: $(openssl rand -base64 16)

# Database configuration
database:
  password: $(openssl rand -base64 32)
  max_idle_conns: 50
  max_open_conns: 1000

# Data volume
data_volume: /data

# Storage backend - MinIO S3
storage_service:
  s3:
    accesskey: harbor-service
    secretkey: <harbor-service-password-from-minio>
    region: us-east-1
    regionendpoint: https://minio.aikernel.local:9000
    bucket: harbor-registry
    secure: true
    v4auth: true
    multipartcopychunksize: 33554432
    multipartcopymaxconcurrency: 100
    multipartcopythresholdsize: 33554432

# Trivy scanner
trivy:
  ignore_unfixed: false
  skip_update: false
  insecure: false

# Job service
jobservice:
  max_job_workers: 10

# Notification
notification:
  webhook_job_max_retry: 10

# Log configuration
log:
  level: info
  local:
    rotate_count: 50
    rotate_size: 200M
    location: /var/log/harbor

# Configuration for garbage collection
gc:
  enabled: true
  schedule: "0 2 * * *"
EOF
```

### 3. Generate SSL Certificates for Harbor

```bash
# Create certificate directory
sudo mkdir -p /data/cert

# Generate self-signed certificate (or use Let's Encrypt)
sudo openssl req -new -x509 -days 365 -nodes \
  -out /data/cert/server.crt \
  -keyout /data/cert/server.key \
  -subj "/CN=harbor.aikernel.local"

# Or use Let's Encrypt
# sudo certbot certonly --standalone -d harbor.aikernel.local
# sudo cp /etc/letsencrypt/live/harbor.aikernel.local/fullchain.pem /data/cert/server.crt
# sudo cp /etc/letsencrypt/live/harbor.aikernel.local/privkey.pem /data/cert/server.key
```

### 4. Install and Start Harbor

```bash
cd /opt/harbor

# Install Harbor with Trivy
sudo ./install.sh --with-trivy

# Verify Harbor is running
sudo docker-compose ps

# Harbor should show:
# - harbor-core
# - harbor-portal
# - harbor-jobservice
# - harbor-db
# - harbor-redis
# - registry
# - registryctl
# - trivy-adapter
# - nginx
```

### 5. Access Harbor

```bash
# Get Harbor admin password
grep harbor_admin_password /opt/harbor/harbor.yml

# Access Harbor UI
echo "Harbor URL: https://harbor.aikernel.local"
echo "Username: admin"
echo "Password: <from-harbor.yml>"
```

### 6. Configure Harbor

Via Harbor UI:

1. **Change Admin Password**
2. **Create Projects**:
   - `aikernel` (public)
   - `aikernel-dev` (private)
   - `aikernel-staging` (private)
   - `aikernel-prod` (private)

3. **Configure Scanner**:
   - Administration → Interrogation Services
   - Set Trivy as default
   - Enable "Scan on push"
   - Severity: HIGH, CRITICAL

4. **Create Robot Account**:
   - Administration → Robot Accounts → New Robot Account
   - Name: `robot$github-actions`
   - Permissions: Pull/Push artifacts
   - Copy token for CI/CD

5. **Configure Retention**:
   - Project → aikernel → Policy
   - Retain last 10 images
   - Retain production images for 30 days

## Artifactory Setup (Artifact Repository)

### 1. Install Artifactory on Bare-Metal

```bash
# Install Java
sudo apt-get update
sudo apt-get install -y openjdk-11-jdk

# Download Artifactory
cd /opt
sudo wget https://releases.jfrog.io/artifactory/bintray-artifactory/org/artifactory/oss/jfrog-artifactory-oss/[RELEASE]/jfrog-artifactory-oss-[RELEASE]-linux.tar.gz

# Extract
sudo tar -xzf jfrog-artifactory-oss-*.tar.gz
sudo mv artifactory-oss-* artifactory

# Create Artifactory user
sudo useradd -r -s /bin/false artifactory
sudo chown -R artifactory:artifactory /opt/artifactory
```

### 2. Configure Artifactory

```bash
# Configure Artifactory to use external database (optional but recommended for production)
# Edit /opt/artifactory/var/etc/system.yaml

sudo tee /opt/artifactory/var/etc/system.yaml > /dev/null <<EOF
configVersion: 1

shared:
  node:
    id: "artifactory-primary"
    ip: "$(hostname -I | awk '{print $1}')"

  database:
    type: postgresql
    driver: org.postgresql.Driver
    url: "jdbc:postgresql://localhost:5432/artifactory"
    username: artifactory
    password: "$(openssl rand -base64 32)"

  security:
    joinKey: "$(openssl rand -hex 32)"
EOF

# Secure the file
sudo chmod 600 /opt/artifactory/var/etc/system.yaml
```

### 3. Create Artifactory Systemd Service

```bash
sudo tee /etc/systemd/system/artifactory.service > /dev/null <<EOF
[Unit]
Description=JFrog Artifactory
After=network.target

[Service]
Type=forking
User=artifactory
Group=artifactory
ExecStart=/opt/artifactory/app/bin/artifactory.sh start
ExecStop=/opt/artifactory/app/bin/artifactory.sh stop
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# Enable and start Artifactory
sudo systemctl daemon-reload
sudo systemctl enable artifactory
sudo systemctl start artifactory

# Check status (may take 2-3 minutes to start)
sudo systemctl status artifactory

# Check logs
sudo tail -f /opt/artifactory/var/log/console.log
```

### 4. Setup Nginx Reverse Proxy for Artifactory

```bash
# Install Nginx
sudo apt-get install -y nginx

# Create Artifactory Nginx config
sudo tee /etc/nginx/sites-available/artifactory > /dev/null <<EOF
upstream artifactory {
    server localhost:8081;
}

server {
    listen 80;
    server_name artifactory.aikernel.local;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name artifactory.aikernel.local;

    ssl_certificate /etc/ssl/certs/artifactory.crt;
    ssl_certificate_key /etc/ssl/private/artifactory.key;

    client_max_body_size 0;
    chunked_transfer_encoding on;

    location / {
        proxy_pass http://artifactory;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;

        proxy_read_timeout 900;
        proxy_buffering off;
    }
}
EOF

# Generate SSL certificate
sudo openssl req -new -x509 -days 365 -nodes \
  -out /etc/ssl/certs/artifactory.crt \
  -keyout /etc/ssl/private/artifactory.key \
  -subj "/CN=artifactory.aikernel.local"

# Enable site
sudo ln -s /etc/nginx/sites-available/artifactory /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### 5. Access and Configure Artifactory

```bash
# Access Artifactory UI
echo "Artifactory URL: https://artifactory.aikernel.local"
echo "Default Username: admin"
echo "Default Password: password"
echo "IMPORTANT: Change password immediately!"
```

Via Artifactory UI:

1. **Change Admin Password**
2. **Create Repositories**:
   - `helm-local` (Helm)
   - `helm-remote` (Proxy to charts.helm.sh)
   - `helm` (Virtual)
   - `docker-local` (Docker)
   - `nuget-local` (NuGet)
   - `npm-local` (NPM)
   - `generic-local` (Generic)

3. **Create Service Account**:
   - Administration → Security → Users
   - Username: `github-actions`
   - Password: Generate strong password
   - Groups: deployers, readers

4. **Create Access Token**:
   - User Profile → Generate Access Token
   - Scopes: All repositories
   - Expiry: 1 year

## Kubernetes Integration

### 1. Deploy External Services Configuration

```bash
# Apply external services configuration
kubectl apply -f infrastructure/prod/k8s/27-external-services-config.yaml

# Update secrets with actual values
kubectl create secret docker-registry harbor-registry-credentials \
  --docker-server=harbor.aikernel.local \
  --docker-username=robot\$github-actions \
  --docker-password=<harbor-robot-token> \
  --namespace=aikernel-core

# Update MinIO credentials
kubectl create secret generic minio-credentials \
  --from-literal=MINIO_ENDPOINT=minio.aikernel.local:9000 \
  --from-literal=MINIO_ACCESS_KEY=harbor-service \
  --from-literal=MINIO_SECRET_KEY=<minio-password> \
  --from-literal=MINIO_USE_SSL=true \
  --from-literal=MINIO_BUCKET=harbor-registry \
  --namespace=aikernel-core \
  --dry-run=client -o yaml | kubectl apply -f -

# Update Artifactory credentials
kubectl create secret generic artifactory-credentials \
  --from-literal=ARTIFACTORY_URL=https://artifactory.aikernel.local \
  --from-literal=ARTIFACTORY_USERNAME=github-actions \
  --from-literal=ARTIFACTORY_PASSWORD=<artifactory-password> \
  --namespace=aikernel-core \
  --dry-run=client -o yaml | kubectl apply -f -
```

### 2. Update /etc/hosts or DNS

On all Kubernetes nodes and CI/CD runners:

```bash
# Add to /etc/hosts
sudo tee -a /etc/hosts > /dev/null <<EOF
192.168.1.10    harbor.aikernel.local
192.168.1.11    artifactory.aikernel.local
192.168.1.12    minio.aikernel.local
EOF

# Or configure internal DNS server with A records
```

### 3. Configure Docker on Kubernetes Nodes

On each Kubernetes node:

```bash
# For self-signed certificates, add Harbor CA to Docker
sudo mkdir -p /etc/docker/certs.d/harbor.aikernel.local
sudo scp harbor-server:/data/cert/server.crt /etc/docker/certs.d/harbor.aikernel.local/ca.crt

# Restart Docker
sudo systemctl restart docker

# Test Docker login
docker login harbor.aikernel.local
```

## CI/CD Configuration

### GitHub Secrets

Add these secrets to your GitHub repository:

```bash
HARBOR_URL=harbor.aikernel.local
HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<harbor-robot-token>

ARTIFACTORY_URL=https://artifactory.aikernel.local
ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<artifactory-token>

MINIO_ENDPOINT=minio.aikernel.local:9000
MINIO_ACCESS_KEY=harbor-service
MINIO_SECRET_KEY=<minio-password>

# Existing secrets
KUBECONFIG_DEV=<base64-kubeconfig>
KUBECONFIG_STAGING=<base64-kubeconfig>
KUBECONFIG_PROD=<base64-kubeconfig>
ARGOCD_SERVER=<argocd-url>
ARGOCD_TOKEN=<argocd-token>
COSIGN_PRIVATE_KEY=<base64-cosign-key>
COSIGN_PASSWORD=<cosign-password>
```

## Verification

### Test Harbor

```bash
# From Kubernetes node or CI/CD runner
docker login harbor.aikernel.local
docker pull alpine:latest
docker tag alpine:latest harbor.aikernel.local/aikernel/test:latest
docker push harbor.aikernel.local/aikernel/test:latest

# Verify in Harbor UI
# Check that image appears in aikernel project
# Check that Trivy scan was triggered
```

### Test Artifactory

```bash
# Test Helm repository
helm repo add aikernel-charts https://artifactory.aikernel.local/artifactory/helm-local \
  --username github-actions \
  --password <artifactory-password>

helm repo update
helm search repo aikernel-charts

# Test API access
curl -u github-actions:<password> \
  https://artifactory.aikernel.local/artifactory/api/system/ping
```

### Test MinIO

```bash
# Test S3 access
mc alias set aikernel-minio https://minio.aikernel.local:9000 harbor-service <password>
mc ls aikernel-minio/harbor-registry

# Verify Harbor is using MinIO
# Push an image to Harbor and check MinIO bucket
docker push harbor.aikernel.local/aikernel/test:v1
mc ls aikernel-minio/harbor-registry
```

### Test Kubernetes Integration

```bash
# Test pulling images in Kubernetes
kubectl run test-harbor --image=harbor.aikernel.local/aikernel/test:latest \
  --image-pull-secrets=harbor-registry-credentials \
  --namespace=aikernel-core

# Check pod status
kubectl get pod test-harbor -n aikernel-core

# Cleanup
kubectl delete pod test-harbor -n aikernel-core
```

## Backup Strategy

### MinIO Backup

```bash
# Create backup cronjob
sudo tee /etc/cron.daily/minio-backup > /dev/null <<'EOF'
#!/bin/bash
BACKUP_DIR=/backup/minio
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR
mc mirror aikernel-minio/harbor-registry $BACKUP_DIR/harbor-registry-$DATE
find $BACKUP_DIR -type d -mtime +7 -exec rm -rf {} \;
EOF

sudo chmod +x /etc/cron.daily/minio-backup
```

### Harbor Backup

```bash
# Harbor database backup
sudo tee /etc/cron.daily/harbor-backup > /dev/null <<'EOF'
#!/bin/bash
BACKUP_DIR=/backup/harbor
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR
cd /opt/harbor
docker-compose exec -T database pg_dump -U postgres registry > $BACKUP_DIR/harbor-db-$DATE.sql
find $BACKUP_DIR -name "*.sql" -mtime +7 -delete
EOF

sudo chmod +x /etc/cron.daily/harbor-backup
```

### Artifactory Backup

```bash
# Artifactory backup
sudo tee /etc/cron.daily/artifactory-backup > /dev/null <<'EOF'
#!/bin/bash
BACKUP_DIR=/backup/artifactory
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR
curl -u admin:<password> -X POST \
  "https://artifactory.aikernel.local/artifactory/api/export/system?exportPath=/tmp/backup"
cp -r /opt/artifactory/var/backup/* $BACKUP_DIR/
mv $BACKUP_DIR/system.* $BACKUP_DIR/artifactory-$DATE
find $BACKUP_DIR -type d -mtime +7 -exec rm -rf {} \;
EOF

sudo chmod +x /etc/cron.daily/artifactory-backup
```

## Monitoring

### Harbor Monitoring

```bash
# Check Harbor status
sudo docker-compose ps

# Check Harbor logs
sudo docker-compose logs -f --tail=100

# Monitor storage usage
df -h /data
mc du aikernel-minio/harbor-registry
```

### Artifactory Monitoring

```bash
# Check Artifactory status
sudo systemctl status artifactory

# Check Artifactory logs
sudo tail -f /opt/artifactory/var/log/artifactory-service.log

# Check storage usage
df -h /opt/artifactory/var/data
```

### MinIO Monitoring

```bash
# Check MinIO status
sudo systemctl status minio

# Check MinIO logs
sudo journalctl -u minio -f

# Check storage usage
df -h /data/minio
mc admin info aikernel-minio
```

## Troubleshooting

See `HARBOR_ARTIFACTORY_INTEGRATION.md` for detailed troubleshooting of Harbor and Artifactory.

### Common Issues

**Cannot connect to Harbor from Kubernetes:**
- Check DNS resolution: `nslookup harbor.aikernel.local`
- Check network connectivity: `telnet harbor.aikernel.local 443`
- Check certificates: Add CA to Docker trusted certificates

**Cannot pull images:**
- Verify image pull secret: `kubectl get secret harbor-registry-credentials -n aikernel-core -o yaml`
- Check Harbor robot account permissions
- Verify network policies allow egress

**MinIO connection issues:**
- Check MinIO is running: `systemctl status minio`
- Verify firewall rules allow port 9000
- Check MinIO credentials in Harbor configuration

## Security Best Practices

1. **Use TLS/SSL** for all services
2. **Rotate credentials** quarterly
3. **Enable audit logging** on all services
4. **Use robot accounts** for automation
5. **Implement network segmentation**
6. **Regular security scans** with Trivy
7. **Backup encryption** for sensitive data
8. **Monitor access logs** for suspicious activity

## Maintenance

### Monthly Tasks
- Review storage usage
- Check backup integrity
- Review security scan results
- Update certificates if needed

### Quarterly Tasks
- Update Harbor, Artifactory, MinIO
- Rotate service account credentials
- Review and clean up old artifacts
- Performance optimization

---

**External Services Successfully Integrated!**

Harbor (Bare-Metal) + Artifactory (Bare-Metal) + MinIO (Bare-Metal) + Kubernetes
