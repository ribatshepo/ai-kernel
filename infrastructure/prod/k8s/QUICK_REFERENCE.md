# External Services Quick Reference

Quick reference for Harbor, Artifactory, and MinIO bare-metal servers.

## Environment Configuration

### Setup .env File

Before working with external services, configure your environment:

```bash
# 1. Copy template
cp .env.example .env

# 2. Edit with your actual values
vi .env
# Update all CHANGE_ME_* placeholders

# 3. Load environment variables
source scripts/load-env.sh

# 4. Verify configuration
./scripts/load-env.sh
```

### Key Environment Variables

The following variables must be configured in `.env`:

**External Services**:
- `HARBOR_URL`, `HARBOR_USERNAME`, `HARBOR_PASSWORD`
- `ARTIFACTORY_URL`, `ARTIFACTORY_USERNAME`, `ARTIFACTORY_PASSWORD`
- `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY`

**Server Addresses**:
- `HARBOR_SERVER_IP`, `HARBOR_SERVER_HOST`
- `ARTIFACTORY_SERVER_IP`, `ARTIFACTORY_SERVER_HOST`
- `MINIO_SERVER_IP`, `MINIO_SERVER_HOST`

**Kubernetes**:
- `KUBE_CONTEXT`, `KUBE_NAMESPACE`
- `KUBECONFIG_DEV`, `KUBECONFIG_STAGING`, `KUBECONFIG_PROD`

See `.env.example` for complete list of available variables.

### Security Best Practices

```bash
# Generate strong passwords
openssl rand -base64 32

# Generate secure tokens
openssl rand -hex 32

# Never commit .env to git (already in .gitignore)
# Rotate credentials quarterly
```

## Service URLs

| Service | URL | Purpose |
|---------|-----|---------|
| Harbor | https://harbor.aikernel.local | Container registry |
| Artifactory | https://artifactory.aikernel.local | Artifact repository |
| MinIO API | https://minio.aikernel.local:9000 | S3 storage |
| MinIO Console | https://minio.aikernel.local:9001 | MinIO web UI |

## Default Credentials

| Service | Username | Password | Notes |
|---------|----------|----------|-------|
| Harbor | admin | See `/opt/harbor/harbor.yml` | Change immediately |
| Artifactory | admin | password | Change immediately |
| MinIO | admin | See `/etc/default/minio` | MINIO_ROOT_PASSWORD |

## Server Locations

Assuming your servers are:
- Harbor: `192.168.1.10` or `harbor-server`
- Artifactory: `192.168.1.11` or `artifactory-server`
- MinIO: `192.168.1.12` or `minio-server`

## Quick Commands

### Harbor

```bash
# SSH to Harbor server
ssh harbor-server

# Check status
sudo docker-compose ps

# View logs
sudo docker-compose logs -f --tail=100 harbor-core

# Restart Harbor
cd /opt/harbor
sudo docker-compose down
sudo docker-compose up -d

# Get admin password
grep harbor_admin_password /opt/harbor/harbor.yml

# Backup database
sudo docker-compose exec database pg_dump -U postgres registry > harbor-backup.sql
```

### Artifactory

```bash
# SSH to Artifactory server
ssh artifactory-server

# Check status
sudo systemctl status artifactory

# View logs
sudo tail -f /opt/artifactory/var/log/artifactory-service.log

# Restart Artifactory
sudo systemctl restart artifactory

# Check health
curl -u admin:password https://artifactory.aikernel.local/artifactory/api/system/ping
```

### MinIO

```bash
# SSH to MinIO server
ssh minio-server

# Check status
sudo systemctl status minio

# View logs
sudo journalctl -u minio -f

# Restart MinIO
sudo systemctl restart minio

# Check info
mc admin info local

# List buckets
mc ls local

# Check bucket usage
mc du local/harbor-registry
```

### From Kubernetes

```bash
# Test Harbor access
docker login harbor.aikernel.local

# Push test image
docker pull alpine:latest
docker tag alpine:latest harbor.aikernel.local/aikernel/test:latest
docker push harbor.aikernel.local/aikernel/test:latest

# Test Helm repo
helm repo add aikernel-charts https://artifactory.aikernel.local/artifactory/helm-local \
  --username github-actions --password <password>

# Pull image in pod
kubectl run test --image=harbor.aikernel.local/aikernel/test:latest \
  --image-pull-secrets=harbor-registry-credentials -n aikernel-core

# Check external services config
kubectl get svc -n aikernel-core | grep external
kubectl get secret harbor-registry-credentials -n aikernel-core
kubectl get configmap external-services -n aikernel-core -o yaml
```

## Common Tasks

### Create Harbor Robot Account

```bash
# Via Web UI:
# 1. Login to https://harbor.aikernel.local
# 2. Administration → Robot Accounts → New Robot Account
# 3. Name: robot$github-actions
# 4. Permissions: Pull/Push
# 5. Copy token

# Via API:
curl -u "admin:Harbor12345" -X POST \
  "https://harbor.aikernel.local/api/v2.0/robots" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "robot$cicd",
    "duration": 365,
    "permissions": [{
      "kind": "project",
      "namespace": "aikernel",
      "access": [
        {"resource": "repository", "action": "pull"},
        {"resource": "repository", "action": "push"}
      ]
    }]
  }'
```

### Create Artifactory User

```bash
# Via Web UI:
# 1. Login to https://artifactory.aikernel.local
# 2. Administration → Security → Users → New User
# 3. Username: github-actions
# 4. Password: <strong-password>
# 5. Groups: deployers, readers

# Generate Access Token:
# User Profile → Generate Access Token → Copy
```

### Create MinIO User

```bash
# SSH to MinIO server
mc admin user add local github-actions <password>
mc admin policy attach local readwrite --user=github-actions
```

### Update Kubernetes Secrets

```bash
# Update Harbor credentials
kubectl create secret docker-registry harbor-registry-credentials \
  --docker-server=harbor.aikernel.local \
  --docker-username=robot\$github-actions \
  --docker-password=<token> \
  --namespace=aikernel-core \
  --dry-run=client -o yaml | kubectl apply -f -

# Update Artifactory credentials
kubectl create secret generic artifactory-credentials \
  --from-literal=ARTIFACTORY_URL=https://artifactory.aikernel.local \
  --from-literal=ARTIFACTORY_USERNAME=github-actions \
  --from-literal=ARTIFACTORY_PASSWORD=<token> \
  --namespace=aikernel-core \
  --dry-run=client -o yaml | kubectl apply -f -
```

## Troubleshooting

### Harbor Not Accessible

```bash
# Check Harbor containers
ssh harbor-server
sudo docker-compose ps

# Check logs
sudo docker-compose logs harbor-core

# Restart if needed
sudo docker-compose restart

# Check MinIO connection from Harbor
sudo docker-compose exec core ping minio.aikernel.local
```

### Can't Push Images to Harbor

```bash
# Check Docker login
docker login harbor.aikernel.local

# Check storage space
ssh harbor-server
df -h /data

# Check MinIO
ssh minio-server
mc du local/harbor-registry

# Check Harbor logs
ssh harbor-server
sudo docker-compose logs registry
```

### Artifactory Not Starting

```bash
# Check status and logs
ssh artifactory-server
sudo systemctl status artifactory
sudo tail -100 /opt/artifactory/var/log/console.log

# Check Java
java -version

# Check database
sudo -u postgres psql -c "\l" | grep artifactory

# Restart
sudo systemctl restart artifactory
```

### MinIO Issues

```bash
# Check status
ssh minio-server
sudo systemctl status minio

# Check storage
df -h /data/minio

# Check configuration
cat /etc/default/minio

# View logs
sudo journalctl -u minio -n 100

# Restart
sudo systemctl restart minio
```

### DNS Resolution Issues

```bash
# From Kubernetes node
nslookup harbor.aikernel.local
nslookup artifactory.aikernel.local
nslookup minio.aikernel.local

# Check /etc/hosts
cat /etc/hosts | grep aikernel

# Test connectivity
telnet harbor.aikernel.local 443
telnet artifactory.aikernel.local 443
telnet minio.aikernel.local 9000
```

### Certificate Issues

```bash
# Add Harbor CA to Docker trusted certs
sudo mkdir -p /etc/docker/certs.d/harbor.aikernel.local
sudo scp harbor-server:/data/cert/server.crt \
  /etc/docker/certs.d/harbor.aikernel.local/ca.crt
sudo systemctl restart docker

# Test
docker login harbor.aikernel.local
```

## Monitoring

### Check All Services

```bash
# Harbor
curl -k https://harbor.aikernel.local/api/v2.0/health

# Artifactory
curl -u admin:password https://artifactory.aikernel.local/artifactory/api/system/ping

# MinIO
mc admin info local
```

### Storage Usage

```bash
# Harbor (via MinIO)
mc du local/harbor-registry

# Artifactory
ssh artifactory-server
du -sh /opt/artifactory/var/data

# MinIO
ssh minio-server
df -h /data/minio
```

### Service Logs

```bash
# Harbor
ssh harbor-server
sudo docker-compose logs -f --tail=50

# Artifactory
ssh artifactory-server
sudo tail -f /opt/artifactory/var/log/artifactory-service.log

# MinIO
ssh minio-server
sudo journalctl -u minio -f
```

## Backup

### Manual Backups

```bash
# Harbor database
ssh harbor-server
cd /opt/harbor
sudo docker-compose exec database pg_dump -U postgres registry > harbor-backup-$(date +%Y%m%d).sql

# Artifactory
ssh artifactory-server
curl -u admin:password -X POST \
  "https://artifactory.aikernel.local/artifactory/api/export/system"
sudo tar -czf artifactory-backup-$(date +%Y%m%d).tar.gz /opt/artifactory/var/backup

# MinIO
ssh minio-server
mc mirror local/harbor-registry /backup/minio/harbor-registry-$(date +%Y%m%d)
```

### Restore from Backup

```bash
# Harbor database
ssh harbor-server
cd /opt/harbor
sudo docker-compose exec -T database psql -U postgres registry < harbor-backup-YYYYMMDD.sql

# Artifactory
ssh artifactory-server
sudo systemctl stop artifactory
# Restore files
sudo systemctl start artifactory

# MinIO
ssh minio-server
mc mirror /backup/minio/harbor-registry-YYYYMMDD local/harbor-registry
```

## GitHub Secrets

Quick reference for GitHub repository secrets:

```bash
# Add to: Repository → Settings → Secrets → Actions

HARBOR_USERNAME=robot$github-actions
HARBOR_PASSWORD=<harbor-robot-token>

ARTIFACTORY_USERNAME=github-actions
ARTIFACTORY_PASSWORD=<artifactory-token>

MINIO_ACCESS_KEY=harbor-service
MINIO_SECRET_KEY=<minio-password>

COSIGN_PRIVATE_KEY=<base64-encoded-key>
COSIGN_PASSWORD=<key-password>
```

## Emergency Contacts

Document your team contacts:

- Harbor Admin: _____________
- Artifactory Admin: _____________
- MinIO Admin: _____________
- Network Team: _____________
- Security Team: _____________

## Useful Links

- Harbor Documentation: https://goharbor.io/docs/
- Artifactory Documentation: https://www.jfrog.com/confluence/display/JFROG/JFrog+Artifactory
- MinIO Documentation: https://docs.min.io/
- Setup Guide: `EXTERNAL_SERVICES_SETUP.md`
- Secrets Guide: `.github/SETUP_SECRETS.md`

---

**Keep this reference handy for daily operations!**
