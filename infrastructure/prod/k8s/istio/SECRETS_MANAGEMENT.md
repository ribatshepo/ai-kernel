# Secrets Management Guide

This document provides production-grade guidance for managing secrets in the AIKernel service mesh deployment.

## Overview

The Istio service mesh deployment requires several secrets for secure operation:
- API credentials for catalog integration
- TLS certificates for ingress gateway
- Database credentials for observability components
- Alert notification credentials

## Security Principles

1. **Never commit secrets to version control**
2. **Use external secret management systems in production**
3. **Rotate secrets regularly**
4. **Use least privilege access**
5. **Audit secret access**

## Secret Management Options

### Option 1: Manual Secret Creation (Development/Testing)

For development and testing environments, create secrets manually:

```bash
# Catalog API credentials
kubectl create secret generic catalog-api-credentials \
  --from-literal=api-key=$(openssl rand -base64 32) \
  --namespace=aikernel-infrastructure \
  --dry-run=client -o yaml | kubectl apply -f -

# Elasticsearch credentials for Jaeger
kubectl create secret generic elasticsearch-credentials \
  --from-literal=username=jaeger \
  --from-literal=password=$(openssl rand -base64 32) \
  --namespace=aikernel-monitoring \
  --dry-run=client -o yaml | kubectl apply -f -

# Grafana credentials
kubectl create secret generic grafana-credentials \
  --from-literal=admin-password=$(openssl rand -base64 32) \
  --from-literal=db-password=$(openssl rand -base64 32) \
  --namespace=aikernel-monitoring \
  --dry-run=client -o yaml | kubectl apply -f -

# TLS certificate (using self-signed for testing)
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /tmp/tls.key -out /tmp/tls.crt \
  -subj "/CN=*.aikernel.io/O=AIKernel"

kubectl create secret tls aikernel-tls-cert \
  --cert=/tmp/tls.crt \
  --key=/tmp/tls.key \
  --namespace=istio-system

rm /tmp/tls.key /tmp/tls.crt
```

### Option 2: Sealed Secrets (Recommended for GitOps)

Sealed Secrets allows you to encrypt secrets and commit them to Git safely.

#### Install Sealed Secrets

```bash
# Install sealed-secrets controller
kubectl apply -f https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.24.0/controller.yaml

# Install kubeseal CLI
wget https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.24.0/kubeseal-0.24.0-linux-amd64.tar.gz
tar xfz kubeseal-0.24.0-linux-amd64.tar.gz
sudo install -m 755 kubeseal /usr/local/bin/kubeseal
```

#### Create and Seal Secrets

```bash
# Create a regular secret (don't apply yet)
kubectl create secret generic catalog-api-credentials \
  --from-literal=api-key=your-secure-api-key \
  --namespace=aikernel-infrastructure \
  --dry-run=client -o yaml > catalog-api-secret.yaml

# Seal the secret
kubeseal --format yaml < catalog-api-secret.yaml > catalog-api-sealed-secret.yaml

# Delete the plaintext secret file
rm catalog-api-secret.yaml

# Apply the sealed secret (safe to commit to Git)
kubectl apply -f catalog-api-sealed-secret.yaml
```

### Option 3: External Secrets Operator (Production Recommended)

External Secrets Operator syncs secrets from external secret management systems.

#### Install External Secrets Operator

```bash
helm repo add external-secrets https://charts.external-secrets.io
helm install external-secrets \
  external-secrets/external-secrets \
  --namespace external-secrets-system \
  --create-namespace
```

#### Configure with AWS Secrets Manager

```bash
# Create IAM role with access to Secrets Manager
# Attach role to Kubernetes service account

# Create SecretStore
cat <<EOF | kubectl apply -f -
apiVersion: external-secrets.io/v1beta1
kind: SecretStore
metadata:
  name: aws-secretsmanager
  namespace: aikernel-infrastructure
spec:
  provider:
    aws:
      service: SecretsManager
      region: us-west-2
      auth:
        jwt:
          serviceAccountRef:
            name: external-secrets-sa
EOF

# Create ExternalSecret
cat <<EOF | kubectl apply -f -
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: catalog-api-credentials
  namespace: aikernel-infrastructure
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: aws-secretsmanager
    kind: SecretStore
  target:
    name: catalog-api-credentials
    creationPolicy: Owner
  data:
  - secretKey: api-key
    remoteRef:
      key: aikernel/catalog-api-key
EOF
```

#### Configure with HashiCorp Vault

```bash
# Create SecretStore for Vault
cat <<EOF | kubectl apply -f -
apiVersion: external-secrets.io/v1beta1
kind: SecretStore
metadata:
  name: vault-backend
  namespace: aikernel-infrastructure
spec:
  provider:
    vault:
      server: "https://vault.example.com"
      path: "secret"
      version: "v2"
      auth:
        kubernetes:
          mountPath: "kubernetes"
          role: "aikernel-role"
          serviceAccountRef:
            name: external-secrets-sa
EOF

# Create ExternalSecret
cat <<EOF | kubectl apply -f -
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: catalog-api-credentials
  namespace: aikernel-infrastructure
spec:
  refreshInterval: 15m
  secretStoreRef:
    name: vault-backend
    kind: SecretStore
  target:
    name: catalog-api-credentials
  data:
  - secretKey: api-key
    remoteRef:
      key: aikernel/catalog
      property: api-key
EOF
```

### Option 4: cert-manager for TLS Certificates

cert-manager automates TLS certificate management.

#### Install cert-manager

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml
```

#### Configure Let's Encrypt

```bash
# Create ClusterIssuer for Let's Encrypt
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@aikernel.io
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: istio
EOF

# Create Certificate
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: aikernel-tls-cert
  namespace: istio-system
spec:
  secretName: aikernel-tls-cert
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
  dnsNames:
  - "*.aikernel.io"
  - "api.aikernel.io"
  - "catalog.aikernel.io"
EOF
```

## Secret Rotation

### Automated Rotation with External Secrets

External Secrets Operator automatically rotates secrets based on the `refreshInterval`:

```yaml
spec:
  refreshInterval: 1h  # Check for updates every hour
```

### Manual Rotation

```bash
# Generate new secret
NEW_KEY=$(openssl rand -base64 32)

# Update secret
kubectl create secret generic catalog-api-credentials \
  --from-literal=api-key=$NEW_KEY \
  --namespace=aikernel-infrastructure \
  --dry-run=client -o yaml | kubectl apply -f -

# Restart pods to pick up new secret
kubectl rollout restart deployment catalog-mesh-controller \
  --namespace=aikernel-infrastructure
```

### Certificate Rotation

cert-manager automatically renews certificates before expiration:

```bash
# Check certificate status
kubectl get certificate -n istio-system

# Force renewal
kubectl delete certificaterequest <request-name> -n istio-system
```

## Secret Access Control

### RBAC for Secrets

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: secret-reader
  namespace: aikernel-infrastructure
rules:
- apiGroups: [""]
  resources: ["secrets"]
  resourceNames:
  - catalog-api-credentials
  verbs: ["get"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: catalog-mesh-controller-secret-reader
  namespace: aikernel-infrastructure
subjects:
- kind: ServiceAccount
  name: catalog-mesh-controller
  namespace: aikernel-infrastructure
roleRef:
  kind: Role
  name: secret-reader
  apiGroup: rbac.authorization.k8s.io
```

### Pod Security

Ensure pods use secrets securely:

```yaml
spec:
  securityContext:
    runAsNonRoot: true
    runAsUser: 1000
    fsGroup: 1000
  containers:
  - name: controller
    securityContext:
      allowPrivilegeEscalation: false
      readOnlyRootFilesystem: true
      capabilities:
        drop:
        - ALL
    env:
    - name: CATALOG_API_KEY
      valueFrom:
        secretKeyRef:
          name: catalog-api-credentials
          key: api-key
```

## Audit and Monitoring

### Enable Audit Logging

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: audit-policy
  namespace: kube-system
data:
  policy.yaml: |
    apiVersion: audit.k8s.io/v1
    kind: Policy
    rules:
    - level: RequestResponse
      resources:
      - group: ""
        resources: ["secrets"]
      namespaces: ["aikernel-infrastructure", "aikernel-monitoring", "istio-system"]
```

### Monitor Secret Access

```bash
# View secret access logs
kubectl logs -n kube-system -l component=kube-apiserver | grep secrets

# Alert on secret access
# Add Prometheus alert rule
- alert: UnexpectedSecretAccess
  expr: rate(apiserver_audit_event_total{verb="get",objectRef_resource="secrets"}[5m]) > 10
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "High rate of secret access detected"
```

## Backup and Recovery

### Backup Secrets

```bash
# Backup all secrets in a namespace
kubectl get secrets -n aikernel-infrastructure -o yaml > secrets-backup.yaml

# Encrypt the backup
gpg --symmetric --cipher-algo AES256 secrets-backup.yaml
rm secrets-backup.yaml

# Store encrypted backup securely
```

### Restore Secrets

```bash
# Decrypt backup
gpg --decrypt secrets-backup.yaml.gpg > secrets-backup.yaml

# Restore secrets
kubectl apply -f secrets-backup.yaml

# Secure delete backup file
shred -u secrets-backup.yaml
```

## Best Practices Checklist

- [ ] Never commit plaintext secrets to Git
- [ ] Use external secret management in production
- [ ] Enable automatic secret rotation
- [ ] Implement least privilege RBAC
- [ ] Enable audit logging for secret access
- [ ] Use TLS for all external communication
- [ ] Encrypt secrets at rest (enabled by default in Kubernetes)
- [ ] Regularly rotate credentials (at least every 90 days)
- [ ] Monitor for unusual secret access patterns
- [ ] Maintain encrypted backups of critical secrets
- [ ] Use separate secrets for different environments
- [ ] Document secret ownership and rotation procedures

## Troubleshooting

### Secret Not Found

```bash
# Verify secret exists
kubectl get secret catalog-api-credentials -n aikernel-infrastructure

# Check secret data
kubectl describe secret catalog-api-credentials -n aikernel-infrastructure

# View secret values (decode base64)
kubectl get secret catalog-api-credentials -n aikernel-infrastructure -o jsonpath='{.data.api-key}' | base64 -d
```

### Certificate Issues

```bash
# Check certificate status
kubectl describe certificate aikernel-tls-cert -n istio-system

# Check certificate renewal
kubectl get certificaterequest -n istio-system

# View certificate details
kubectl get secret aikernel-tls-cert -n istio-system -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -text -noout
```

### External Secrets Not Syncing

```bash
# Check ExternalSecret status
kubectl describe externalsecret catalog-api-credentials -n aikernel-infrastructure

# Check SecretStore status
kubectl describe secretstore vault-backend -n aikernel-infrastructure

# View operator logs
kubectl logs -n external-secrets-system -l app.kubernetes.io/name=external-secrets
```

## Security Incident Response

If a secret is compromised:

1. **Immediate Actions**:
   ```bash
   # Revoke the compromised secret immediately
   kubectl delete secret <secret-name> -n <namespace>

   # Generate new secret
   kubectl create secret generic <secret-name> --from-literal=key=$(openssl rand -base64 32) -n <namespace>

   # Restart affected services
   kubectl rollout restart deployment -n <namespace>
   ```

2. **Investigation**:
   ```bash
   # Check audit logs for unauthorized access
   kubectl logs -n kube-system -l component=kube-apiserver | grep <secret-name>

   # Review recent pod access
   kubectl get events -n <namespace> --field-selector involvedObject.kind=Secret
   ```

3. **Post-Incident**:
   - Update external secret management system
   - Review and tighten RBAC policies
   - Update monitoring and alerting
   - Document incident and response

## References

- [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)
- [Sealed Secrets](https://github.com/bitnami-labs/sealed-secrets)
- [External Secrets Operator](https://external-secrets.io/)
- [cert-manager](https://cert-manager.io/)
- [HashiCorp Vault](https://www.vaultproject.io/)
- [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/)
