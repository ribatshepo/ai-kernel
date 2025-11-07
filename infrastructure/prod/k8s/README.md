# AI Kernel Platform - Kubernetes Infrastructure

This directory contains all Kubernetes manifests and deployment scripts for the AI Kernel Platform's production infrastructure.

## Overview

The infrastructure is organized into several layers following the platform's architectural design:

### Architecture Layers

1. **Data Layer** (`aikernel-data` namespace)
   - PostgreSQL cluster (3 replicas) - Relational catalog storage
   - Neo4j - Graph database for relationships
   - Elasticsearch cluster (3 nodes) - Search and indexing
   - Redis cluster (3 nodes) - Distributed caching
   - Kafka cluster (3 brokers) - Event streaming
   - Zookeeper cluster (3 nodes) - Kafka coordination

2. **Security Layer** (`aikernel-security` namespace)
   - HashiCorp Vault - Secrets management

3. **Monitoring Layer** (`aikernel-monitoring` namespace)
   - Prometheus (2 replicas) - Metrics collection and alerting
   - Grafana (2 replicas) - Visualization and dashboards
   - Jaeger - Distributed tracing

4. **Service Mesh** (`istio-system` namespace)
   - Istio control plane - Traffic management and security
   - Ingress/Egress gateways - External connectivity
   - mTLS enforcement - Zero-trust networking

5. **GitOps** (`argocd` namespace)
   - ArgoCD - Continuous deployment and sync

## Prerequisites

### Required Tools

- **kubectl** (v1.28+) - Kubernetes CLI
- **Kubernetes cluster** (v1.28+) with:
  - Minimum 6 nodes (recommended)
  - 16 CPU cores and 64GB RAM total
  - Storage provisioner for PersistentVolumes
  - LoadBalancer support (for cloud deployments)

### Optional Tools

- **helm** (v3.12+) - For additional charts
- **istioctl** (v1.20+) - Istio management
- **argocd** CLI - ArgoCD management

## Quick Start

### 1. Deploy Everything

The fastest way to deploy the entire infrastructure:

```bash
cd infrastructure/prod/k8s
chmod +x *.sh
./deploy-all.sh
```

This will deploy all components in order with health checks, including:
- All backing services (databases, caching, messaging)
- Istio service mesh with zero-trust security
- Complete observability stack (Prometheus, Grafana, Jaeger)
- GitOps automation with ArgoCD
- **LoadBalancer services** for external access
- **Kubernetes Dashboard** for cluster management

See [LOADBALANCER_AND_DASHBOARD.md](LOADBALANCER_AND_DASHBOARD.md) for detailed access instructions.

### 2. Verify Deployment

Check all pods are running:

```bash
kubectl get pods -A
```

Expected namespaces:
- `aikernel-data` - All data stores running
- `aikernel-security` - Vault running
- `aikernel-monitoring` - Prometheus, Grafana, Jaeger running
- `aikernel-core` - Empty (for application services)
- `aikernel-ai` - Empty (for AI services)
- `istio-system` - Istio components running
- `argocd` - ArgoCD running

### 3. Access Services

**Via LoadBalancer (Recommended for Cloud Deployments):**

All services are exposed via LoadBalancer services. Get external IPs:

```bash
# Check all LoadBalancer services
kubectl get svc -A | grep LoadBalancer

# Kubernetes Dashboard
kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb
# Access: https://<EXTERNAL-IP>
# Get token: kubectl -n kubernetes-dashboard create token admin-user

# Grafana
kubectl get svc -n aikernel-monitoring grafana-lb
# Access: http://<EXTERNAL-IP>

# Prometheus
kubectl get svc -n aikernel-monitoring prometheus-lb
# Access: http://<EXTERNAL-IP>

# Jaeger
kubectl get svc -n aikernel-monitoring jaeger-query-lb
# Access: http://<EXTERNAL-IP>

# ArgoCD
kubectl get svc -n argocd argocd-server-lb
# Access: https://<EXTERNAL-IP>
```

See [LOADBALANCER_AND_DASHBOARD.md](LOADBALANCER_AND_DASHBOARD.md) for complete access information including authentication, RBAC, and security configuration.

**Via kubectl port-forward (Alternative for Local Access):**

#### Grafana (Monitoring)
```bash
kubectl port-forward svc/grafana 3000:3000 -n aikernel-monitoring
# Visit: http://localhost:3000
# Default credentials: admin / (get from secret)
kubectl -n aikernel-monitoring get secret grafana-credentials -o jsonpath='{.data.admin-password}' | base64 -d
```

#### Prometheus (Metrics)
```bash
kubectl port-forward svc/prometheus 9090:9090 -n aikernel-monitoring
# Visit: http://localhost:9090
```

#### Jaeger (Tracing)
```bash
kubectl port-forward svc/jaeger-query 16686:16686 -n aikernel-monitoring
# Visit: http://localhost:16686
```

#### ArgoCD (GitOps)
```bash
kubectl port-forward svc/argocd-server 8080:443 -n argocd
# Visit: https://localhost:8080
# Username: admin
# Password:
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d
```

#### Kubernetes Dashboard
```bash
kubectl port-forward -n kubernetes-dashboard svc/kubernetes-dashboard 8443:443
# Visit: https://localhost:8443
# Get token: kubectl -n kubernetes-dashboard create token admin-user
```

## File Structure

```
infrastructure/prod/k8s/
├── 00-namespaces.yaml                    # Namespace definitions
├── 01-postgres-secret.yaml               # PostgreSQL credentials
├── 02-postgres-configmap.yaml            # PostgreSQL configuration
├── 03-postgres-statefulset.yaml          # PostgreSQL cluster
├── 04-neo4j-secret.yaml                  # Neo4j credentials
├── 05-neo4j-statefulset.yaml             # Neo4j database
├── 06-elasticsearch-statefulset.yaml     # Elasticsearch cluster
├── 07-redis-statefulset.yaml             # Redis cluster
├── 08-zookeeper-statefulset.yaml         # Zookeeper cluster
├── 09-kafka-statefulset.yaml             # Kafka cluster
├── 10-vault-statefulset.yaml             # HashiCorp Vault
├── 11-prometheus-configmap.yaml          # Prometheus config
├── 12-prometheus-statefulset.yaml        # Prometheus deployment
├── 13-grafana-deployment.yaml            # Grafana deployment
├── 14-jaeger-deployment.yaml             # Jaeger tracing
├── 15-istio-install.sh                   # Istio installation script
├── 16-istio-security.yaml                # mTLS and security policies
├── 17-istio-gateway.yaml                 # Gateway and routing config
├── 18-argocd-install.sh                  # ArgoCD installation
├── 19-argocd-apps.yaml                   # ArgoCD application definitions
├── 20-loadbalancer-services.yaml         # LoadBalancer services
├── 21-kubernetes-dashboard-install.sh    # Dashboard installation
├── 22-kubernetes-dashboard-rbac.yaml     # Dashboard RBAC config
├── deploy-all.sh                         # Complete deployment script
├── README.md                             # This file
└── LOADBALANCER_AND_DASHBOARD.md         # LoadBalancer & Dashboard guide
```

## Deployment Scenarios

### Scenario 1: Fresh Installation

Deploy everything from scratch:

```bash
./deploy-all.sh
```

### Scenario 2: Update Existing Deployment

Update specific components:

```bash
# Update PostgreSQL configuration
kubectl apply -f 02-postgres-configmap.yaml
kubectl rollout restart statefulset postgres -n aikernel-data

# Update Istio security policies
kubectl apply -f 16-istio-security.yaml
```

### Scenario 3: Development/Testing (Skip Service Mesh)

```bash
SKIP_ISTIO=true ./deploy-all.sh
```

### Scenario 4: Dry Run

Test what would be deployed:

```bash
DRY_RUN=true ./deploy-all.sh
```

## Manual Deployment Steps

If you prefer step-by-step deployment:

### Step 1: Create Namespaces
```bash
kubectl apply -f 00-namespaces.yaml
```

### Step 2: Deploy Secrets and ConfigMaps
```bash
kubectl apply -f 01-postgres-secret.yaml
kubectl apply -f 02-postgres-configmap.yaml
kubectl apply -f 04-neo4j-secret.yaml
kubectl apply -f 11-prometheus-configmap.yaml
```

### Step 3: Deploy Data Layer
```bash
kubectl apply -f 03-postgres-statefulset.yaml
kubectl apply -f 05-neo4j-statefulset.yaml
kubectl apply -f 06-elasticsearch-statefulset.yaml
kubectl apply -f 07-redis-statefulset.yaml
kubectl apply -f 08-zookeeper-statefulset.yaml
kubectl apply -f 09-kafka-statefulset.yaml

# Wait for data layer to be ready
kubectl wait --for=condition=ready pod -l app=postgres -n aikernel-data --timeout=600s
```

### Step 4: Deploy Security Layer
```bash
kubectl apply -f 10-vault-statefulset.yaml
kubectl wait --for=condition=ready pod -l app=vault -n aikernel-security --timeout=300s
```

### Step 5: Deploy Monitoring
```bash
kubectl apply -f 12-prometheus-statefulset.yaml
kubectl apply -f 13-grafana-deployment.yaml
kubectl apply -f 14-jaeger-deployment.yaml
```

### Step 6: Install Istio
```bash
chmod +x 15-istio-install.sh
./15-istio-install.sh

kubectl apply -f 16-istio-security.yaml
kubectl apply -f 17-istio-gateway.yaml
```

### Step 7: Install ArgoCD
```bash
chmod +x 18-argocd-install.sh
./18-argocd-install.sh

kubectl apply -f 19-argocd-apps.yaml
```

## Configuration

### Update Secrets for Production

⚠️ **IMPORTANT**: Update all placeholder secrets before production deployment!

1. **PostgreSQL Password**:
```bash
kubectl create secret generic postgres-credentials \
  --from-literal=postgres-password='YOUR_SECURE_PASSWORD' \
  --from-literal=postgres-user='aikernel' \
  --from-literal=postgres-database='aikernel_catalog' \
  --from-literal=replication-password='YOUR_REPLICATION_PASSWORD' \
  -n aikernel-data --dry-run=client -o yaml | kubectl apply -f -
```

2. **Neo4j Password**:
```bash
kubectl create secret generic neo4j-credentials \
  --from-literal=neo4j-password='YOUR_SECURE_PASSWORD' \
  --from-literal=neo4j-user='neo4j' \
  -n aikernel-data --dry-run=client -o yaml | kubectl apply -f -
```

3. **Grafana Password**:
```bash
kubectl create secret generic grafana-credentials \
  --from-literal=admin-user='admin' \
  --from-literal=admin-password='YOUR_SECURE_PASSWORD' \
  -n aikernel-monitoring --dry-run=client -o yaml | kubectl apply -f -
```

### Configure TLS Certificates

For production, use cert-manager to automate certificate management:

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create ClusterIssuer for Let's Encrypt
kubectl apply -f - <<EOF
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
```

### Storage Classes

Update PersistentVolumeClaim storage classes based on your environment:

```yaml
# Example for AWS EBS
storageClassName: gp3

# Example for GCP
storageClassName: pd-ssd

# Example for Azure
storageClassName: managed-premium
```

## Monitoring and Observability

### Metrics

Prometheus collects metrics from all components:

- **Infrastructure**: Node metrics, pod metrics, resource usage
- **Applications**: Custom application metrics (when deployed)
- **Service Mesh**: Request rates, latencies, error rates

Access Prometheus: http://localhost:9090 (via port-forward)

### Dashboards

Grafana includes pre-configured datasources:

- Prometheus (metrics)
- Elasticsearch (logs)
- Jaeger (traces)

Import dashboards from [Grafana Labs](https://grafana.com/grafana/dashboards/):
- Kubernetes cluster monitoring: 6417
- Istio mesh: 7639
- PostgreSQL: 9628

### Tracing

Jaeger provides distributed tracing with 100% sampling (adjust in production):

- View service dependencies
- Analyze request flows
- Debug performance issues

Access Jaeger: http://localhost:16686 (via port-forward)

### Alerts

Prometheus Alertmanager is configured with baseline alerts:

- High error rates
- High latency (p99 > 1s)
- Pod crash loops
- High resource usage
- Service downtime

Configure notification channels in Alertmanager.

## Troubleshooting

### Pods Not Starting

Check pod status:
```bash
kubectl get pods -n aikernel-data
kubectl describe pod <pod-name> -n aikernel-data
kubectl logs <pod-name> -n aikernel-data
```

Common issues:
- Insufficient resources: Check node capacity
- Storage issues: Verify PVC binding
- Image pull errors: Check image names and registry access

### Storage Issues

Check PersistentVolumeClaims:
```bash
kubectl get pvc -n aikernel-data
```

If PVCs are pending:
- Verify storage class exists
- Check storage provisioner logs
- Ensure sufficient storage capacity

### Network Issues

Check service endpoints:
```bash
kubectl get endpoints -n aikernel-data
```

Test connectivity:
```bash
kubectl run -it --rm debug --image=nicolaka/netshoot --restart=Never -- bash
# Inside the pod:
nslookup postgres-primary.aikernel-data
curl -v http://postgres-primary.aikernel-data:5432
```

### Istio Issues

Check Istio installation:
```bash
istioctl analyze -A
istioctl proxy-status
```

View sidecar logs:
```bash
kubectl logs <pod-name> -c istio-proxy -n aikernel-core
```

## Scaling

### Horizontal Scaling

Scale statefulsets:
```bash
kubectl scale statefulset postgres --replicas=5 -n aikernel-data
kubectl scale statefulset elasticsearch --replicas=5 -n aikernel-data
```

### Vertical Scaling

Update resource requests/limits in manifests and apply:
```bash
kubectl apply -f 03-postgres-statefulset.yaml
kubectl rollout restart statefulset postgres -n aikernel-data
```

## Backup and Recovery

### PostgreSQL Backup

```bash
kubectl exec postgres-0 -n aikernel-data -- \
  pg_dump -U aikernel aikernel_catalog > backup.sql
```

### Neo4j Backup

```bash
kubectl exec neo4j-0 -n aikernel-data -- \
  neo4j-admin backup --backup-dir=/backup --database=neo4j
```

### Vault Backup

```bash
kubectl exec vault-0 -n aikernel-security -- \
  vault operator raft snapshot save /vault/data/snapshot.snap
```

## Security Hardening

### Production Checklist

- [ ] Update all default passwords
- [ ] Enable TLS for all external endpoints
- [ ] Configure cert-manager for certificate management
- [ ] Enable Pod Security Policies/Standards
- [ ] Configure Network Policies (included)
- [ ] Enable audit logging
- [ ] Configure RBAC with least privilege
- [ ] Scan images for vulnerabilities
- [ ] Enable secret encryption at rest
- [ ] Configure backup encryption

### mTLS Verification

Verify mTLS is enforced:
```bash
kubectl get peerauthentication -A
istioctl experimental authz check <pod-name>
```

## Performance Tuning

### Database Optimization

PostgreSQL:
- Tune `shared_buffers`, `effective_cache_size` in ConfigMap
- Adjust connection pooling settings
- Monitor query performance with pg_stat_statements

Elasticsearch:
- Adjust heap size based on available memory
- Configure shard allocation awareness
- Tune refresh intervals for write-heavy workloads

Redis:
- Adjust maxmemory and eviction policies
- Monitor memory usage and fragmentation
- Use appropriate persistence settings

### Resource Limits

Monitor resource usage:
```bash
kubectl top nodes
kubectl top pods -A
```

Adjust limits based on actual usage patterns.

## Support

For issues or questions:

1. Check the logs
2. Review Grafana dashboards
3. Consult the [main README](../../../README.md)
4. Open an issue on GitHub

## License

MIT License - See [LICENSE](../../../LICENSE) for details
