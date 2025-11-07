# Environment Variable Injection Guide

This guide explains how to use environment variable injection for Kubernetes manifests in the AI Kernel Platform.

## Overview

The AI Kernel Platform uses environment variables from a `.env` file to inject configuration values into Kubernetes manifests at deployment time. This approach provides:

- **Security**: Keep sensitive credentials out of version control
- **Flexibility**: Use different values for dev, staging, and production
- **Simplicity**: Single source of truth for all configuration
- **Automation**: Integrate with CI/CD pipelines easily

## Quick Start

### 1. Setup Environment File

```bash
# Copy the template
cp .env.example .env

# Edit with your actual values
vi .env

# Update all CHANGE_ME_* placeholders with real values
```

### 2. Load and Validate Configuration

```bash
# Load environment variables
source scripts/load-env.sh

# Verify configuration (optional)
./scripts/load-env.sh
```

### 3. Deploy with Environment Injection

```bash
# Deploy all infrastructure with environment variable injection
./infrastructure/prod/k8s/deploy-all.sh

# Or deploy individual manifests
./scripts/apply-with-env.sh infrastructure/prod/k8s/01-postgres-secret.yaml
```

## How It Works

### Environment Variables in Manifests

Manifests use shell variable syntax for values that should be injected:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres-credentials
  namespace: aikernel-data
type: Opaque
stringData:
  postgres-password: "${POSTGRES_PASSWORD}"  # Injected from .env
  postgres-user: "${POSTGRES_USER}"
  postgres-database: "${POSTGRES_DB}"
```

### Variable Substitution Process

1. **Load .env file**: Variables are exported to the shell environment
2. **Run envsubst**: The `envsubst` command substitutes `${VAR}` with actual values
3. **Apply to Kubernetes**: The processed manifest is piped to `kubectl apply`

```bash
# Example: Manual substitution and apply
source .env
envsubst < manifest.yaml | kubectl apply -f -
```

## Files with Environment Variable Injection

The following Kubernetes manifests use environment variable injection:

### Database Secrets

- **01-postgres-secret.yaml**
  - `POSTGRES_PASSWORD`
  - `POSTGRES_USER`
  - `POSTGRES_DB`

- **04-neo4j-secret.yaml**
  - `NEO4J_PASSWORD`
  - `NEO4J_USERNAME`

- **07-redis-statefulset.yaml**
  - `REDIS_PASSWORD` (in ConfigMap section)

### External Services Configuration

- **27-external-services-config.yaml**
  - Harbor:
    - `HARBOR_URL`
    - `HARBOR_REGISTRY`
    - `HARBOR_PROJECT`
  - Artifactory:
    - `ARTIFACTORY_URL`
    - `ARTIFACTORY_USERNAME`
    - `ARTIFACTORY_PASSWORD`
    - `ARTIFACTORY_HELM_REPO`
    - `ARTIFACTORY_NUGET_REPO`
    - `ARTIFACTORY_DOCKER_REPO`
    - `ARTIFACTORY_GENERIC_REPO`
  - MinIO:
    - `MINIO_ENDPOINT`
    - `MINIO_ACCESS_KEY`
    - `MINIO_SECRET_KEY`
    - `MINIO_USE_SSL`
    - `MINIO_BUCKET`
    - `MINIO_REGION`

### Monitoring

- **13-grafana-deployment.yaml**
  - `GRAFANA_USERNAME`
  - `GRAFANA_PASSWORD`

## Deployment Methods

### Method 1: Full Deployment Script (Recommended)

The `deploy-all.sh` script automatically loads `.env` and applies all manifests with variable substitution:

```bash
# Deploy everything
./infrastructure/prod/k8s/deploy-all.sh

# Dry run to preview changes
DRY_RUN=true ./infrastructure/prod/k8s/deploy-all.sh
```

**Features**:
- ✅ Automatically loads `.env` file
- ✅ Validates environment before deployment
- ✅ Uses `envsubst` for variable substitution
- ✅ Waits for resources to be ready
- ✅ Provides detailed progress output

### Method 2: Individual Manifest Deployment

Use the `apply-with-env.sh` helper script for individual manifests:

```bash
# Apply a single manifest
./scripts/apply-with-env.sh infrastructure/prod/k8s/01-postgres-secret.yaml

# Dry run
./scripts/apply-with-env.sh infrastructure/prod/k8s/27-external-services-config.yaml --dry-run=client

# Apply to different namespace
./scripts/apply-with-env.sh infrastructure/prod/k8s/01-postgres-secret.yaml -n custom-namespace
```

### Method 3: Manual Substitution

For advanced use cases or CI/CD pipelines:

```bash
# Load environment variables
source .env

# Apply with substitution
envsubst < infrastructure/prod/k8s/01-postgres-secret.yaml | kubectl apply -f -

# Or save processed manifest first
envsubst < infrastructure/prod/k8s/01-postgres-secret.yaml > /tmp/postgres-secret-processed.yaml
kubectl apply -f /tmp/postgres-secret-processed.yaml
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Deploy to Kubernetes

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Create .env from secrets
        run: |
          cat > .env << EOF
          POSTGRES_PASSWORD=${{ secrets.POSTGRES_PASSWORD }}
          NEO4J_PASSWORD=${{ secrets.NEO4J_PASSWORD }}
          HARBOR_URL=${{ secrets.HARBOR_URL }}
          HARBOR_USERNAME=${{ secrets.HARBOR_USERNAME }}
          HARBOR_PASSWORD=${{ secrets.HARBOR_PASSWORD }}
          # ... other variables
          EOF

      - name: Configure kubectl
        uses: azure/k8s-set-context@v3
        with:
          kubeconfig: ${{ secrets.KUBECONFIG_PROD }}

      - name: Deploy infrastructure
        run: |
          source .env
          ./infrastructure/prod/k8s/deploy-all.sh
```

### GitLab CI Example

```yaml
deploy:
  stage: deploy
  script:
    - echo "POSTGRES_PASSWORD=${POSTGRES_PASSWORD}" > .env
    - echo "NEO4J_PASSWORD=${NEO4J_PASSWORD}" >> .env
    # ... other variables
    - source .env
    - ./infrastructure/prod/k8s/deploy-all.sh
  only:
    - main
```

## Prerequisites

### Required Tools

1. **envsubst** (from gettext package)
   ```bash
   # Ubuntu/Debian
   sudo apt-get install gettext-base

   # CentOS/RHEL
   sudo yum install gettext

   # macOS
   brew install gettext
   ```

2. **kubectl**
   ```bash
   # Verify installation
   kubectl version --client
   ```

3. **bash** (version 4+)
   ```bash
   bash --version
   ```

## Environment Variables Reference

See `.env.example` for a complete list of available environment variables organized by category:

- External Services (Harbor, Artifactory, MinIO)
- Bare-Metal Server Addresses
- Kubernetes Configuration
- Database Credentials
- Monitoring Credentials
- Security Settings
- Feature Flags

## Security Best Practices

### 1. Never Commit .env

The `.env` file is already in `.gitignore`:

```bash
# Verify .env is ignored
git status .env
# Should show: "nothing to commit"
```

### 2. Generate Strong Credentials

```bash
# Generate 32-character password
openssl rand -base64 32

# Generate hex token
openssl rand -hex 32
```

### 3. Rotate Credentials Regularly

```bash
# Update .env with new credentials
vi .env

# Redeploy affected manifests
./scripts/apply-with-env.sh infrastructure/prod/k8s/01-postgres-secret.yaml
```

### 4. Use Different .env Files per Environment

```bash
# Development
cp .env.example .env.dev

# Staging
cp .env.example .env.staging

# Production
cp .env.example .env.prod

# Use specific environment file
ln -sf .env.prod .env
source scripts/load-env.sh
```

### 5. Validate Before Deployment

```bash
# Load and validate configuration
./scripts/load-env.sh

# Check for placeholder values
grep -r "CHANGE_ME" .env
# Should return nothing

# Dry run deployment
DRY_RUN=true ./infrastructure/prod/k8s/deploy-all.sh
```

## Troubleshooting

### envsubst Not Found

```bash
# Error: envsubst: command not found

# Solution: Install gettext
# Ubuntu/Debian
sudo apt-get install gettext-base

# CentOS/RHEL
sudo yum install gettext

# macOS
brew install gettext
```

### Variables Not Substituted

```bash
# Problem: Manifest shows ${VAR} instead of actual value

# Cause 1: .env file not loaded
# Solution:
source .env

# Cause 2: Variable not exported
# Solution: Use load-env.sh
source scripts/load-env.sh

# Cause 3: Variable name mismatch
# Solution: Check variable names in .env match manifest
grep POSTGRES_PASSWORD .env
```

### Invalid YAML After Substitution

```bash
# Problem: Error applying manifest after substitution

# Debug: View processed manifest
envsubst < infrastructure/prod/k8s/01-postgres-secret.yaml

# Check for:
# - Missing quotes around values
# - Special characters in values (need escaping)
# - Empty values

# Solution: Validate .env values
./scripts/load-env.sh
```

### Placeholder Values in Production

```bash
# Problem: CHANGE_ME_* values in production

# Prevention: Use load-env.sh validation
source scripts/load-env.sh
# Will error if ENVIRONMENT=production and placeholders exist

# Fix: Update all placeholder values in .env
grep CHANGE_ME .env
# Update each one with real values
```

## Advanced Usage

### Selective Variable Substitution

```bash
# Only substitute specific variables
export POSTGRES_PASSWORD="secret123"
envsubst '${POSTGRES_PASSWORD}' < manifest.yaml | kubectl apply -f -
```

### Multiple Environment Files

```bash
# Load multiple env files
set -a
source .env.common
source .env.prod
set +a
```

### Template Processing in CI/CD

```bash
# Process all manifests and save to output directory
mkdir -p processed
for file in infrastructure/prod/k8s/*.yaml; do
  envsubst < "$file" > "processed/$(basename "$file")"
done

# Apply all processed manifests
kubectl apply -f processed/
```

## Additional Resources

- [Environment Configuration Guide](../../../EXTERNAL_SERVICES_SUMMARY.md#configuration-management)
- [External Services Setup](./EXTERNAL_SERVICES_SETUP.md)
- [Quick Reference](./QUICK_REFERENCE.md)
- [envsubst Documentation](https://www.gnu.org/software/gettext/manual/html_node/envsubst-Invocation.html)

## Support

For issues or questions:

1. Check `.env.example` for required variables
2. Run `./scripts/load-env.sh` for validation
3. Review this guide for common solutions
4. Check manifest syntax with `kubectl apply --dry-run=client`

---

**Environment Variable Injection - Secure, Flexible, Automated Configuration Management**
