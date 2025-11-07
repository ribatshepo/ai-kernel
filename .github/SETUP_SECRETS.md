# GitHub Secrets Setup Guide

This guide helps you configure the required secrets for CI/CD pipelines to work with your on-premise Kubernetes cluster.

## Required Secrets

### 1. Kubernetes Configuration Secrets

These secrets provide kubectl access to your Kubernetes clusters.

#### KUBECONFIG (Validation)
Used for manifest validation in PR checks.

**How to create:**
```bash
# Generate base64-encoded kubeconfig
cat ~/.kube/config | base64 -w 0

# Add to GitHub:
# Repository → Settings → Secrets → Actions → New repository secret
# Name: KUBECONFIG
# Value: <paste the base64 output>
```

#### KUBECONFIG_DEV (Development Cluster)
```bash
# If you have a separate dev cluster config
cat ~/.kube/config-dev | base64 -w 0

# Or create from current context
kubectl config view --flatten --minify > /tmp/kubeconfig-dev.yaml
cat /tmp/kubeconfig-dev.yaml | base64 -w 0
rm /tmp/kubeconfig-dev.yaml

# Add to GitHub as KUBECONFIG_DEV
```

#### KUBECONFIG_STAGING (Staging Cluster)
```bash
# Same process for staging cluster
kubectl config use-context staging-cluster
kubectl config view --flatten --minify > /tmp/kubeconfig-staging.yaml
cat /tmp/kubeconfig-staging.yaml | base64 -w 0
rm /tmp/kubeconfig-staging.yaml

# Add to GitHub as KUBECONFIG_STAGING
```

#### KUBECONFIG_PROD (Production Cluster)
```bash
# Same process for production cluster
kubectl config use-context prod-cluster
kubectl config view --flatten --minify > /tmp/kubeconfig-prod.yaml
cat /tmp/kubeconfig-prod.yaml | base64 -w 0
rm /tmp/kubeconfig-prod.yaml

# Add to GitHub as KUBECONFIG_PROD
```

**Security Notes:**
- ⚠️ These kubeconfigs have full cluster access
- 🔒 Create a dedicated service account with limited permissions instead
- 🔄 Rotate these credentials regularly (monthly recommended)
- 🚫 Never commit kubeconfig files to Git

### 2. ArgoCD Secrets

Used for GitOps deployments to staging and production.

#### ARGOCD_SERVER
The ArgoCD server URL.

**How to get:**
```bash
# If using LoadBalancer
kubectl get svc argocd-server -n argocd -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

# If using NodePort (on-prem)
NODE_IP=$(kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
NODE_PORT=$(kubectl get svc argocd-server-nodeport -n argocd -o jsonpath='{.spec.ports[?(@.name=="https")].nodePort}')
echo "${NODE_IP}:${NODE_PORT}"

# If using Ingress
echo "argocd.aikernel.local"  # Your ingress hostname

# Add to GitHub as ARGOCD_SERVER (just the hostname/IP:port, no https://)
```

#### ARGOCD_TOKEN
Authentication token for ArgoCD API.

**How to create:**
```bash
# Install ArgoCD CLI
curl -sSL -o argocd https://github.com/argoproj/argo-cd/releases/latest/download/argocd-linux-amd64
chmod +x argocd
sudo mv argocd /usr/local/bin/

# Get ArgoCD password
ARGOCD_PASSWORD=$(kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d)

# Login to ArgoCD
argocd login $ARGOCD_SERVER --username admin --password $ARGOCD_PASSWORD --insecure

# Generate a token
argocd account generate-token --account admin

# Add the token to GitHub as ARGOCD_TOKEN
```

**Token expiration:**
- Tokens don't expire by default
- For security, create a dedicated CI user with limited permissions
- Rotate tokens quarterly

### 3. Harbor Container Registry Secrets (Required - External Bare-Metal Server)

The platform uses Harbor running on a **bare-metal server** as the container registry for Docker images.

#### HARBOR_USERNAME
Harbor administrator or robot account username.

**How to create:**
```bash
# Via Harbor UI (on bare-metal server):
# 1. Login to Harbor: https://harbor.aikernel.local
# 2. Go to Administration → Robot Accounts
# 3. Click "New Robot Account"
# 4. Name: github-actions
# 5. Permissions: Push and Pull artifact, Create tag
# 6. Copy the generated token

# Add to GitHub as HARBOR_USERNAME (e.g., robot$github-actions)
```

#### HARBOR_PASSWORD
Harbor password or robot account token.

**How to create:**
```bash
# For robot account (recommended):
# Use the token from Harbor UI when creating the robot account

# For admin account:
# SSH to Harbor bare-metal server
ssh harbor-server
grep harbor_admin_password /opt/harbor/harbor.yml

# Add to GitHub as HARBOR_PASSWORD
```

### 4. JFrog Artifactory Secrets (Required - External Bare-Metal Server)

The platform uses Artifactory running on a **bare-metal server** for build artifacts, Helm charts, and NuGet packages.

#### ARTIFACTORY_USERNAME
Artifactory administrator or service account username.

**How to create:**
```bash
# Via Artifactory UI (on bare-metal server):
# 1. Login to Artifactory: https://artifactory.aikernel.local
# 2. Go to Administration → Security → Users
# 3. Click "New User"
# 4. Username: github-actions
# 5. Password: Generate strong password
# 6. Groups: Add to appropriate groups (deployers, readers)

# Or use access token (recommended):
# 1. Go to User Profile → Authentication Settings
# 2. Generate Access Token
# 3. Scopes: api:*, repositories:read,write

# Add to GitHub as ARTIFACTORY_USERNAME
```

#### ARTIFACTORY_PASSWORD
Artifactory password or access token.

**How to create:**
```bash
# For access token (recommended):
# 1. Login to Artifactory web UI
# 2. User Profile → Edit Profile → Generate Access Token
# 3. Scopes: Applied Permissions/Repositories - Deploy/Cache
# 4. Expiry: Set appropriate expiry (1 year recommended)
# 5. Copy the generated token
# Add to GitHub as ARTIFACTORY_PASSWORD

# For password:
# Use the password set when creating the user
```

### 5. MinIO S3 Storage Secrets (Optional - External Bare-Metal Server)

MinIO runs on a **bare-metal server** and provides S3-compatible storage backend for Harbor.

#### MINIO_ACCESS_KEY
MinIO service account access key.

**How to create:**
```bash
# SSH to MinIO bare-metal server
ssh minio-server

# Create service account for Harbor
mc admin user add local harbor-service <generate-strong-password>
mc admin policy attach local readwrite --user=harbor-service

# Access key is: harbor-service
# Add to GitHub as MINIO_ACCESS_KEY (if needed for backups)
```

#### MINIO_SECRET_KEY
MinIO service account secret key.

**How to create:**
```bash
# Use the password generated when creating the MinIO user
# Add to GitHub as MINIO_SECRET_KEY (if needed for backups)
```

### 6. Image Signing Secrets (Required for Production)

Used for signing container images with Cosign for supply chain security.

#### COSIGN_PRIVATE_KEY
Private key for signing container images.

**How to create:**
```bash
# Generate a new Cosign key pair
cosign generate-key-pair

# This creates:
# - cosign.key (private key)
# - cosign.pub (public key)

# Convert private key to base64 for GitHub secret
cat cosign.key | base64 -w 0

# Add to GitHub as COSIGN_PRIVATE_KEY
# Store cosign.pub securely for image verification
```

#### COSIGN_PASSWORD
Password for the Cosign private key.

**How to create:**
```bash
# The password you set when running cosign generate-key-pair

# Add to GitHub as COSIGN_PASSWORD
```

### 7. Container Registry Secrets (Optional - Legacy)

If using additional external registries:

#### REGISTRY_USERNAME
```bash
# Your registry username (e.g., for Docker Hub, etc.)
```

#### REGISTRY_PASSWORD
```bash
# Your registry password or token
```

## Security Best Practices

### 1. Use Dedicated Service Accounts

Instead of using your admin kubeconfig, create dedicated service accounts:

```bash
# Create service account for CI/CD
kubectl create serviceaccount github-actions -n kube-system

# Create ClusterRoleBinding (adjust permissions as needed)
kubectl create clusterrolebinding github-actions \
  --clusterrole=cluster-admin \
  --serviceaccount=kube-system:github-actions

# Get the token
SECRET_NAME=$(kubectl get serviceaccount github-actions -n kube-system -o jsonpath='{.secrets[0].name}')
TOKEN=$(kubectl get secret $SECRET_NAME -n kube-system -o jsonpath='{.data.token}' | base64 -d)

# Create kubeconfig with this token
CLUSTER_NAME=$(kubectl config view -o jsonpath='{.clusters[0].name}')
CLUSTER_SERVER=$(kubectl config view -o jsonpath='{.clusters[0].cluster.server}')
CLUSTER_CA=$(kubectl config view --raw -o jsonpath='{.clusters[0].cluster.certificate-authority-data}')

cat > /tmp/github-actions-kubeconfig.yaml <<EOF
apiVersion: v1
kind: Config
clusters:
- cluster:
    certificate-authority-data: ${CLUSTER_CA}
    server: ${CLUSTER_SERVER}
  name: ${CLUSTER_NAME}
contexts:
- context:
    cluster: ${CLUSTER_NAME}
    user: github-actions
  name: github-actions@${CLUSTER_NAME}
current-context: github-actions@${CLUSTER_NAME}
users:
- name: github-actions
  user:
    token: ${TOKEN}
EOF

# Base64 encode and use this
cat /tmp/github-actions-kubeconfig.yaml | base64 -w 0
rm /tmp/github-actions-kubeconfig.yaml
```

### 2. Limit Permissions

Create a custom ClusterRole with only necessary permissions:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: github-actions-deploy
rules:
  # Allow reading cluster info
  - apiGroups: [""]
    resources: ["namespaces", "nodes"]
    verbs: ["get", "list"]

  # Allow managing aikernel namespaces only
  - apiGroups: [""]
    resources: ["pods", "services", "configmaps", "secrets"]
    verbs: ["get", "list", "create", "update", "patch", "delete"]
    # Limit to specific namespaces

  # Allow deployments and statefulsets
  - apiGroups: ["apps"]
    resources: ["deployments", "statefulsets", "replicasets", "daemonsets"]
    verbs: ["get", "list", "create", "update", "patch", "delete"]

  # Allow checking rollout status
  - apiGroups: ["apps"]
    resources: ["deployments/status", "statefulsets/status"]
    verbs: ["get"]
```

### 3. Use GitHub Environments

Configure environment protection rules:

1. Go to `Settings → Environments`
2. Create `development`, `staging`, `production`
3. For `production`:
   - ✅ Required reviewers: Add team members
   - ✅ Wait timer: 5 minutes
   - ✅ Deployment branches: `main` only
   - ✅ Environment secrets: Use production-specific secrets

### 4. Enable Audit Logging

Monitor GitHub Actions access to secrets:

1. Organization → Settings → Security → Log streaming
2. Configure log streaming to your SIEM
3. Alert on secret access patterns

### 5. Rotate Secrets Regularly

Set up a rotation schedule:

| Secret | Rotation Frequency | Owner |
|--------|-------------------|-------|
| KUBECONFIG_* | Monthly | DevOps Team |
| ARGOCD_TOKEN | Quarterly | Platform Team |
| HARBOR_USERNAME/PASSWORD | Quarterly | Security Team |
| ARTIFACTORY_USERNAME/PASSWORD | Quarterly | Security Team |
| COSIGN_PRIVATE_KEY | Annually | Security Team |
| Registry credentials (external) | Quarterly | Security Team |

## Verification

After adding all secrets, verify they work:

### 1. Test Kubeconfig

```bash
# Decode and test locally
echo "$KUBECONFIG_DEV_BASE64" | base64 -d > /tmp/test-kubeconfig
export KUBECONFIG=/tmp/test-kubeconfig
kubectl get nodes
rm /tmp/test-kubeconfig
```

### 2. Test ArgoCD Token

```bash
argocd login $ARGOCD_SERVER --auth-token $ARGOCD_TOKEN --insecure
argocd app list
```

### 3. Test Harbor Access

```bash
# Test Harbor login
docker login harbor.aikernel.local -u $HARBOR_USERNAME -p $HARBOR_PASSWORD

# Test push (optional)
docker pull alpine:latest
docker tag alpine:latest harbor.aikernel.local/aikernel/test:latest
docker push harbor.aikernel.local/aikernel/test:latest

# Cleanup
docker rmi harbor.aikernel.local/aikernel/test:latest
```

### 4. Test Artifactory Access

```bash
# Test Artifactory API access
curl -u "$ARTIFACTORY_USERNAME:$ARTIFACTORY_PASSWORD" \
  https://artifactory.aikernel.local/artifactory/api/system/ping

# Test Helm repository access
helm repo add aikernel-charts https://artifactory.aikernel.local/artifactory/helm-local \
  --username $ARTIFACTORY_USERNAME \
  --password $ARTIFACTORY_PASSWORD

helm repo update
helm search repo aikernel-charts
```

### 5. Test Cosign Key

```bash
# Test signing an image (requires image to exist)
echo "$COSIGN_PRIVATE_KEY" | base64 -d > /tmp/cosign.key
cosign sign --key /tmp/cosign.key harbor.aikernel.local/aikernel/test:latest
rm /tmp/cosign.key

# Verify signature
cosign verify --key cosign.pub harbor.aikernel.local/aikernel/test:latest
```

### 6. Trigger Test Workflow

Create a test workflow:

```yaml
name: Test Secrets
on: workflow_dispatch

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - name: Test Kubeconfig
        env:
          KUBECONFIG_DATA: ${{ secrets.KUBECONFIG_DEV }}
        run: |
          mkdir -p $HOME/.kube
          echo "$KUBECONFIG_DATA" | base64 -d > $HOME/.kube/config
          kubectl version --client
          kubectl cluster-info

      - name: Test ArgoCD
        env:
          ARGOCD_SERVER: ${{ secrets.ARGOCD_SERVER }}
          ARGOCD_TOKEN: ${{ secrets.ARGOCD_TOKEN }}
        run: |
          curl -sSL -o argocd https://github.com/argoproj/argo-cd/releases/latest/download/argocd-linux-amd64
          chmod +x argocd
          ./argocd version --client
          echo "ArgoCD server: $ARGOCD_SERVER"
```

## Troubleshooting

### "Invalid credentials" error

```bash
# Check if base64 encoding is correct
echo "$KUBECONFIG_DATA" | base64 -d | head -5

# Verify it's valid YAML
echo "$KUBECONFIG_DATA" | base64 -d | kubectl config view -f -
```

### "Forbidden" errors in workflows

```bash
# Check service account permissions
kubectl auth can-i --list --as=system:serviceaccount:kube-system:github-actions

# Check what the service account can do
kubectl auth can-i create deployments --as=system:serviceaccount:kube-system:github-actions -n aikernel-core
```

### Cannot reach on-prem cluster from GitHub Actions

See `.github/workflows/README.md` for solutions:
- Use self-hosted runners (recommended)
- Set up VPN/Tailscale
- Expose Kubernetes API (less secure)

## Self-Hosted Runner Setup (Recommended)

For on-premise clusters, use a self-hosted runner:

### 1. Create Runner

1. Repository → Settings → Actions → Runners → New self-hosted runner
2. Follow the setup instructions for your OS

### 2. Install Runner on On-Prem Server

```bash
# On your on-prem server (with kubectl access)
mkdir actions-runner && cd actions-runner

# Download runner
curl -o actions-runner-linux-x64-2.311.0.tar.gz -L \
  https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-linux-x64-2.311.0.tar.gz

# Extract
tar xzf ./actions-runner-linux-x64-2.311.0.tar.gz

# Configure (follow GitHub instructions for your repo-specific token)
./config.sh --url https://github.com/YOUR_ORG/ai-kernel --token YOUR_TOKEN

# Install as service
sudo ./svc.sh install
sudo ./svc.sh start
```

### 3. Update Workflows

Change `runs-on: ubuntu-latest` to `runs-on: self-hosted`:

```yaml
jobs:
  deploy:
    runs-on: self-hosted  # <-- Change this
    steps:
      # No need for kubeconfig secret - runner has direct access
      - name: Deploy
        run: kubectl apply -f manifests/
```

### 4. Runner Security

- Run runner as dedicated user (not root)
- Limit runner to specific labels
- Use ephemeral runners for sensitive workflows
- Monitor runner activity

## Additional Resources

- [GitHub Actions Secrets Documentation](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Kubernetes RBAC Documentation](https://kubernetes.io/docs/reference/access-authn-authz/rbac/)
- [ArgoCD User Management](https://argo-cd.readthedocs.io/en/stable/operator-manual/user-management/)
- [Self-Hosted Runners](https://docs.github.com/en/actions/hosting-your-own-runners)

## Support

If you need help setting up secrets:
1. Check the troubleshooting section above
2. Review workflow logs in GitHub Actions
3. Consult `.github/workflows/README.md`
4. Open an issue with the `ci-cd` label
