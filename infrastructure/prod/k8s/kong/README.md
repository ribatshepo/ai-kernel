# Kong API Gateway - Production Deployment

This directory contains production-ready Kubernetes manifests for deploying Kong API Gateway with catalog integration for the AI-Kernel platform.

## Overview

Kong serves as the external API Gateway, providing:
- External traffic management and routing
- TLS 1.3 termination with automatic certificate management
- Rate limiting and DDoS protection
- Request/response transformation
- Authentication and authorization
- Dynamic routing based on catalog metadata
- Comprehensive observability

## Architecture

```
External Traffic
       ↓
Load Balancer (TLS termination)
       ↓
Kong Gateway (3+ replicas with HPA)
       ↓
Service Mesh (Istio)
       ↓
Internal Services
```

### Components

1. **Kong Gateway**: API gateway with proxy and admin API
2. **Kong Catalog Sync**: Controller that syncs routes from catalog
3. **PostgreSQL**: Kong configuration database
4. **Redis**: Distributed rate limiting
5. **Cert-Manager**: Automated TLS certificate management
6. **Prometheus**: Metrics collection and monitoring

## Prerequisites

- Kubernetes cluster 1.28+
- PostgreSQL 14+ deployed (see `infrastructure/prod/k8s/postgresql`)
- Redis cluster deployed (see `infrastructure/prod/k8s/redis`)
- Cert-Manager installed for TLS certificates
- Istio service mesh deployed
- Catalog API deployed

## Deployment

### 1. Create Namespace

```bash
kubectl apply -f kong-namespace.yaml
```

### 2. Create Secrets

#### PostgreSQL Credentials

```bash
# Generate strong password
KONG_PG_PASSWORD=$(openssl rand -base64 32)

# Create PostgreSQL database and user
kubectl exec -it postgresql-0 -n aikernel-infrastructure -- psql -U postgres <<EOF
CREATE DATABASE kong;
CREATE USER kong WITH ENCRYPTED PASSWORD '${KONG_PG_PASSWORD}';
GRANT ALL PRIVILEGES ON DATABASE kong TO kong;
EOF

# Create Kubernetes secret
kubectl create secret generic kong-postgres-credentials \
  --from-literal=password="${KONG_PG_PASSWORD}" \
  --namespace=kong
```

#### Catalog API Credentials

```bash
# Use existing catalog API key or create new one
kubectl create secret generic catalog-api-credentials \
  --from-literal=api-key="<CATALOG_API_KEY>" \
  --namespace=kong
```

### 3. Deploy Kong Infrastructure

```bash
# Apply in order
kubectl apply -f kong-config.yaml
kubectl apply -f kong-rbac.yaml
kubectl apply -f kong-certificate.yaml
kubectl apply -f kong-deployment.yaml
kubectl apply -f kong-service.yaml
kubectl apply -f kong-pdb.yaml
kubectl apply -f kong-plugins.yaml
kubectl apply -f kong-monitoring.yaml
```

### 4. Deploy Catalog Sync Controller

```bash
# Build and push image (if not using pre-built)
cd ../../services/kong-catalog-sync
make docker-build IMAGE_REGISTRY=ghcr.io/aikernel
make docker-push IMAGE_REGISTRY=ghcr.io/aikernel

# Deploy
cd ../../infrastructure/prod/k8s/kong
kubectl apply -f kong-catalog-sync.yaml
```

### 5. Verify Deployment

```bash
# Check Kong pods
kubectl get pods -n kong
kubectl logs -n kong -l app=kong --tail=100

# Check Kong admin API
kubectl exec -it -n kong deploy/kong-gateway -- kong health

# Check catalog sync
kubectl logs -n kong -l app=kong-catalog-sync --tail=100

# Verify services created in Kong
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  wget -qO- http://localhost:8001/services

# Test external access (after DNS configured)
curl https://api.aikernel.io/health
```

## Configuration

### Rate Limiting Tiers

Configure rate limits via catalog service metadata:

```yaml
metadata:
  rateLimit.minute: "100"
  rateLimit.hour: "1000"
  rateLimit.day: "10000"
```

### Authentication

Enable authentication via metadata:

```yaml
metadata:
  auth.type: "jwt"
  auth.required: "true"
```

### CORS

Configure CORS via metadata:

```yaml
metadata:
  cors.enabled: "true"
  cors.origins: "*"
  cors.methods: "GET,POST,PUT,DELETE"
  cors.headers: "Authorization,Content-Type"
```

### Custom Plugins

Add custom Kong plugins by modifying `kong-plugins.yaml`.

## Monitoring

### Metrics

Kong exposes Prometheus metrics on port 8100:

```bash
# Forward port
kubectl port-forward -n kong svc/kong-status 8100:8100

# View metrics
curl http://localhost:8100/metrics
```

Key metrics:
- `kong_http_requests_total`: Total requests
- `kong_latency_bucket`: Request latency histogram
- `kong_bandwidth_bytes`: Bandwidth usage
- `kong_nginx_connections_total`: Connection statistics
- `kong_upstream_target_health`: Upstream health status

### Dashboards

Import the Grafana dashboard:

```bash
kubectl get configmap kong-grafana-dashboard -n kong -o jsonpath='{.data.kong-dashboard\.json}'
```

### Alerts

Prometheus alerts are configured in `kong-monitoring.yaml`:
- High error rate (>5%)
- High latency (p99 >1s)
- Kong instances down
- High connection usage (>80%)
- Unhealthy upstreams

## Troubleshooting

### Kong Not Starting

```bash
# Check logs
kubectl logs -n kong deploy/kong-gateway -c kong

# Common issues:
# 1. PostgreSQL not accessible
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  nc -zv postgresql.aikernel-infrastructure.svc.cluster.local 5432

# 2. Database not initialized
kubectl logs -n kong deploy/kong-gateway -c kong-migrations
```

### Routes Not Syncing

```bash
# Check catalog sync logs
kubectl logs -n kong deploy/kong-catalog-sync -f

# Verify catalog API accessible
kubectl exec -it -n kong deploy/kong-catalog-sync -- \
  wget -qO- http://catalog-api.aikernel.svc.cluster.local:8080/api/v1/services

# Force sync by restarting controller
kubectl rollout restart deployment/kong-catalog-sync -n kong
```

### High Latency

```bash
# Check Kong metrics
kubectl port-forward -n kong svc/kong-status 8100:8100
curl http://localhost:8100/metrics | grep latency

# Check upstream health
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  wget -qO- http://localhost:8001/upstreams/{upstream}/health

# Check resource usage
kubectl top pods -n kong
```

### Rate Limiting Issues

```bash
# Check Redis connectivity
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  nc -zv redis.aikernel-infrastructure.svc.cluster.local 6379

# Check rate limit plugin configuration
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  wget -qO- http://localhost:8001/plugins | grep rate-limiting
```

## Scaling

### Horizontal Pod Autoscaling

HPA is configured to scale based on CPU and memory:

```bash
# View HPA status
kubectl get hpa -n kong

# Manually scale
kubectl scale deployment/kong-gateway --replicas=5 -n kong
```

### Performance Tuning

Edit `kong-config.yaml`:

```yaml
# Increase worker processes
KONG_NGINX_WORKER_PROCESSES: "8"

# Increase worker connections
KONG_NGINX_WORKER_CONNECTIONS: "20000"

# Increase memory cache
KONG_MEM_CACHE_SIZE: "256m"
```

## Security

### TLS Configuration

- TLS 1.3 enforced
- Strong cipher suites only
- Automated certificate rotation via cert-manager
- Certificates renewed 30 days before expiration

### Network Policies

Network policies restrict Kong to:
- Receive traffic from anywhere on proxy ports
- Access PostgreSQL for configuration
- Access Redis for rate limiting
- Access Catalog API for synchronization
- Access services via service mesh

### RBAC

Kong service account has minimal permissions:
- Read ConfigMaps and Secrets
- Read Services and Endpoints
- Create Events for logging

### Secret Management

Never commit secrets to Git. Use:
- Kubernetes Secrets for credentials
- External secret managers (Vault, AWS Secrets Manager)
- Sealed Secrets for GitOps

## Backup and Recovery

### Configuration Backup

```bash
# Export Kong configuration
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  kong config db_export /tmp/kong-backup.yaml

kubectl cp kong/kong-gateway-xxx:/tmp/kong-backup.yaml ./kong-backup.yaml
```

### Database Backup

PostgreSQL backups include Kong configuration. See PostgreSQL documentation.

### Disaster Recovery

1. Restore PostgreSQL database
2. Redeploy Kong manifests
3. Verify catalog sync operational
4. Test external access

## Maintenance

### Upgrading Kong

```bash
# Update image version in kong-deployment.yaml
# Apply with rolling update
kubectl apply -f kong-deployment.yaml

# Monitor rollout
kubectl rollout status deployment/kong-gateway -n kong
```

### Database Migrations

```bash
# Migrations run automatically via init container
# For manual migration:
kubectl exec -it -n kong deploy/kong-gateway -c kong -- \
  kong migrations up
```

### Certificate Rotation

Automatic via cert-manager. Monitor:

```bash
kubectl get certificate -n kong
kubectl describe certificate kong-tls -n kong
```

## Integration

### Adding a New Service

Services are automatically discovered from catalog. Register in catalog:

```bash
curl -X POST https://catalog-api.aikernel.io/api/v1/services \
  -H "Authorization: Bearer $CATALOG_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "my-service",
    "version": "v1.0.0",
    "endpoints": [{
      "protocol": "https",
      "host": "my-service.aikernel.svc.cluster.local",
      "port": 8080,
      "path": "/api/v1/*",
      "methods": ["GET", "POST"]
    }],
    "metadata": {
      "rateLimit.minute": "100",
      "auth.type": "jwt",
      "auth.required": "true"
    }
  }'
```

Kong will automatically create routes within 30 seconds.

## Support

For issues and questions:
- Check logs: `kubectl logs -n kong -l app=kong`
- View metrics: Grafana dashboards
- Review alerts: Prometheus Alertmanager
- GitHub Issues: [AI-Kernel Issues](https://github.com/ribatshepo/ai-kernel/issues)

## References

- [Kong Documentation](https://docs.konghq.com/)
- [Kong Admin API](https://docs.konghq.com/gateway/latest/admin-api/)
- [Kong Plugins](https://docs.konghq.com/hub/)
- [Kubernetes Deployment Guide](https://docs.konghq.com/kubernetes-ingress-controller/)
