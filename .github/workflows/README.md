## CI/CD Pipelines for AI Kernel Platform

This directory contains GitHub Actions workflows for automated deployment to on-premise Kubernetes clusters.

### Workflows

#### 1. Infrastructure Deployment (`infrastructure-deploy.yml`)

Deploys the core infrastructure components (databases, monitoring, service mesh, etc.) to Kubernetes.

**Triggers:**
- Push to `main` or `develop` branches (when infrastructure files change)
- Pull requests to `main` (validation only)
- Manual dispatch via GitHub UI

**Environments:**
- **Development**: Auto-deploys on push to `develop`
- **Staging**: Auto-deploys on push to `main`
- **Production**: Manual approval required via workflow_dispatch

**Features:**
- ✅ Manifest validation with kubeconform
- ✅ Shell script linting with shellcheck
- ✅ Secret scanning
- ✅ Dry-run on pull requests
- ✅ Health checks and rollout monitoring
- ✅ Automatic rollback on failure
- ✅ Deployment summaries in GitHub

#### 2. Application Deployment (`application-deploy.yml`)

Builds Docker images and deploys application services.

**Triggers:**
- Push to `main` or `develop` (when application code changes)
- Pull requests to `main`
- Manual dispatch

**Features:**
- ✅ Multi-service Docker builds with caching
- ✅ Image pushed to GitHub Container Registry
- ✅ Development: Direct kubectl deployment
- ✅ Staging/Production: ArgoCD GitOps deployment
- ✅ Canary deployments for production
- ✅ Automated smoke tests

### Setup Instructions

#### Prerequisites

1. **On-Premise Kubernetes Cluster**
   - Kubernetes v1.28+
   - kubectl access configured
   - Sufficient resources (see infrastructure/prod/k8s/README.md)

2. **GitHub Secrets Configuration**

Navigate to your repository: `Settings → Secrets and variables → Actions`

Add the following secrets:

| Secret Name | Description | How to Get |
|-------------|-------------|------------|
| `KUBECONFIG` | Base64-encoded kubeconfig for validation | `cat ~/.kube/config \| base64 -w 0` |
| `KUBECONFIG_DEV` | Kubeconfig for development cluster | Same as above for dev cluster |
| `KUBECONFIG_STAGING` | Kubeconfig for staging cluster | Same as above for staging cluster |
| `KUBECONFIG_PROD` | Kubeconfig for production cluster | Same as above for prod cluster |
| `ARGOCD_SERVER` | ArgoCD server URL | e.g., `argocd.aikernel.local` |
| `ARGOCD_TOKEN` | ArgoCD authentication token | `argocd account generate-token` |
| `GITHUB_TOKEN` | GitHub token (auto-provided) | Automatically available |

#### Getting Kubeconfig

```bash
# On your machine with kubectl access to the cluster
kubectl config view --flatten --minify > kubeconfig-dev.yaml

# Base64 encode it
cat kubeconfig-dev.yaml | base64 -w 0

# Copy the output and add as GitHub secret
```

#### Getting ArgoCD Token

```bash
# Install ArgoCD CLI
curl -sSL -o argocd https://github.com/argoproj/argo-cd/releases/latest/download/argocd-linux-amd64
chmod +x argocd
sudo mv argocd /usr/local/bin/

# Login to ArgoCD
ARGOCD_SERVER=$(kubectl get svc argocd-server-nodeport -n argocd -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
ARGOCD_PASSWORD=$(kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d)
argocd login $ARGOCD_SERVER --username admin --password $ARGOCD_PASSWORD --insecure

# Generate token
argocd account generate-token --account admin
```

#### Configuring GitHub Environments

1. Go to `Settings → Environments`
2. Create three environments: `development`, `staging`, `production`
3. For `production`:
   - Add required reviewers
   - Set deployment branch to `main` only
   - Add environment secrets if different from repository secrets

### Usage

#### Deploying Infrastructure

**Automatic (on push):**
```bash
# Deploy to development
git checkout develop
git add infrastructure/
git commit -m "Update infrastructure"
git push origin develop

# Deploy to staging
git checkout main
git merge develop
git push origin main
```

**Manual (workflow dispatch):**
1. Go to `Actions` tab
2. Select "Infrastructure Deployment"
3. Click "Run workflow"
4. Choose environment and options
5. Click "Run workflow"

#### Deploying Applications

**Automatic (on push):**
```bash
# Deploy to development
git checkout develop
git add src/
git commit -m "Update application"
git push origin develop

# Deploy to staging
git checkout main
git merge develop
git push origin main
```

**Manual (for production):**
1. Go to `Actions` tab
2. Select "Application Deployment"
3. Click "Run workflow"
4. Choose "production" environment
5. Choose service or "all"
6. Click "Run workflow"
7. Approve deployment when prompted

### Monitoring Deployments

#### View Workflow Status

1. Go to `Actions` tab
2. Click on the running workflow
3. View real-time logs
4. Check deployment summary at the bottom

#### Check Cluster Status

```bash
# Get deployment status
kubectl get pods -A

# Check specific namespace
kubectl get pods -n aikernel-core

# View deployment logs
kubectl logs -f deployment/orchestration-kernel -n aikernel-core

# Get service endpoints (NodePort for on-prem)
kubectl get svc -A | grep NodePort
```

### Troubleshooting

#### Workflow fails with "kubeconfig: not found"

- Ensure KUBECONFIG_* secrets are properly base64 encoded
- Verify secret is added to the correct environment
- Check secret name matches workflow YAML

#### Cannot access on-prem cluster from GitHub Actions

**Option 1: Self-Hosted Runner (Recommended for on-prem)**

1. Set up a self-hosted runner in your on-prem environment
2. Update workflows to use: `runs-on: self-hosted`
3. Runner will have direct cluster access

Setup self-hosted runner:
```bash
# On your on-prem server
mkdir actions-runner && cd actions-runner
curl -o actions-runner-linux-x64-2.311.0.tar.gz -L https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-linux-x64-2.311.0.tar.gz
tar xzf ./actions-runner-linux-x64-2.311.0.tar.gz
./config.sh --url https://github.com/YOUR_ORG/ai-kernel --token YOUR_TOKEN
./run.sh
```

**Option 2: VPN/Tailscale**

Set up VPN connection in GitHub Actions:
```yaml
- name: Connect to VPN
  uses: tailscale/github-action@v2
  with:
    oauth-client-id: ${{ secrets.TS_OAUTH_CLIENT_ID }}
    oauth-secret: ${{ secrets.TS_OAUTH_SECRET }}
    tags: tag:ci
```

**Option 3: Expose Kubernetes API (Less Secure)**

- Expose Kubernetes API server with authentication
- Use kubectl with remote endpoint
- Not recommended for production

#### Deployment succeeds but pods are pending

```bash
# Check pod events
kubectl describe pod <pod-name> -n <namespace>

# Common issues:
# - Insufficient resources
# - Storage class not available
# - Image pull errors
# - Node selector/affinity issues
```

#### LoadBalancer stuck in Pending on on-prem

On-premise clusters need a LoadBalancer provider:

**Option 1: MetalLB (Layer 2)**
```bash
kubectl apply -f https://raw.githubusercontent.com/metallb/metallb/v0.13.12/config/manifests/metallb-native.yaml
kubectl apply -f infrastructure/prod/k8s/on-prem-config.yaml
```

**Option 2: Use NodePort instead**
```bash
kubectl apply -f infrastructure/prod/k8s/on-prem-config.yaml
# Access services on <node-ip>:<nodePort>
```

**Option 3: Use Ingress**
```bash
# Install NGINX Ingress
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.1/deploy/static/provider/baremetal/deploy.yaml

# Apply ingress resources
kubectl apply -f infrastructure/prod/k8s/on-prem-config.yaml
```

### On-Premise Specific Configuration

For on-premise deployments, use `on-prem-config.yaml`:

```bash
kubectl apply -f infrastructure/prod/k8s/on-prem-config.yaml
```

This configures:
- ✅ MetalLB for LoadBalancer support
- ✅ Storage classes (NFS, local-path)
- ✅ Ingress resources
- ✅ NodePort services as alternative

### Best Practices

#### 1. Use Self-Hosted Runners for On-Prem

Self-hosted runners provide:
- Direct cluster access
- Faster deployments
- No public exposure needed
- Better security

#### 2. Environment Separation

- **Development**: Auto-deploy, test frequently
- **Staging**: Auto-deploy, production-like
- **Production**: Manual approval, comprehensive testing

#### 3. Secrets Management

- Rotate tokens regularly
- Use environment-specific secrets
- Never commit secrets to Git
- Use external secret management (Vault) in production

#### 4. Monitoring

- Monitor workflow run times
- Set up alerts for failed deployments
- Track deployment frequency
- Monitor resource usage

#### 5. Rollback Strategy

- Always have rollback plan
- Test rollback procedure
- Keep previous versions available
- Monitor after rollback

### Advanced Scenarios

#### Blue-Green Deployment

```yaml
# In your workflow
- name: Blue-Green deployment
  run: |
    # Deploy to green environment
    kubectl apply -f k8s/green/ -n aikernel-core

    # Test green environment
    ./scripts/smoke-test.sh green

    # Switch traffic to green
    kubectl patch svc orchestration-kernel -p '{"spec":{"selector":{"version":"green"}}}'
```

#### Canary Deployment with Istio

```yaml
# In your workflow
- name: Canary deployment
  run: |
    # Deploy canary version
    kubectl apply -f k8s/canary/

    # Gradually increase traffic
    kubectl apply -f - <<EOF
    apiVersion: networking.istio.io/v1beta1
    kind: VirtualService
    metadata:
      name: orchestration-kernel
    spec:
      hosts:
      - orchestration-kernel
      http:
      - match:
        - headers:
            canary:
              exact: "true"
        route:
        - destination:
            host: orchestration-kernel
            subset: canary
      - route:
        - destination:
            host: orchestration-kernel
            subset: stable
          weight: 90
        - destination:
            host: orchestration-kernel
            subset: canary
          weight: 10
    EOF
```

### Security Considerations

1. **Least Privilege**: GitHub Actions should have minimal permissions
2. **Secret Rotation**: Rotate kubeconfig and tokens regularly
3. **Audit Logs**: Enable audit logging on Kubernetes API server
4. **Network Policies**: Restrict access between namespaces
5. **Image Scanning**: Scan container images for vulnerabilities
6. **RBAC**: Use service accounts with appropriate roles

### Support

For issues or questions:
1. Check workflow logs in GitHub Actions
2. Review Kubernetes events: `kubectl get events -A`
3. Check pod logs: `kubectl logs <pod> -n <namespace>`
4. Consult [infrastructure/prod/k8s/README.md](../../infrastructure/prod/k8s/README.md)
5. Open an issue on GitHub
