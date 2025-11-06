

# DNS Service Discovery Infrastructure

Production-ready DNS infrastructure for the AI-Kernel platform, providing internal service discovery, external DNS management, health-aware routing, and geographic load balancing.

## Overview

The DNS system consists of four main components:

1. **CoreDNS**: Internal service discovery with health-aware responses
2. **External-DNS**: Automated public DNS record management
3. **DNS Health Controller**: Health-based endpoint filtering
4. **Geographic Routing**: Latency-based traffic distribution

## Architecture

```
External Clients
       ↓
External DNS Provider (Route53/CloudDNS/Azure DNS)
       ↓
LoadBalancer (Geographic Routing)
       ↓
Kong API Gateway
       ↓
Internal CoreDNS (Service Discovery)
       ↓
Service Mesh (Istio)
       ↓
Application Services
```

### Components

**CoreDNS Custom Deployment**
- 3 replicas with HPA (3-10 pods)
- 30-second TTL for rapid updates
- Health-aware endpoint selection
- DNS caching with prefetch
- Prometheus metrics

**External-DNS**
- Automatic DNS record creation from Kubernetes resources
- Multi-cloud provider support
- 30-second sync interval
- TXT record registry

**DNS Health Controller**
- Monitors endpoint health every 10 seconds
- Removes unhealthy endpoints within 30 seconds
- Integrates with Kubernetes endpoints
- Prometheus metrics

**Geographic Routing**
- Latency-based routing
- Multi-region support
- Automatic failover
- Priority-based selection

## Prerequisites

- Kubernetes cluster 1.28+
- Prometheus Operator for monitoring
- External DNS provider account (AWS Route53, Google Cloud DNS, Azure DNS, or Cloudflare)
- Service mesh deployed (for health checks)

## Deployment

### 1. Deploy CoreDNS

```bash
kubectl apply -f coredns-config.yaml
kubectl apply -f coredns-deployment.yaml
```

**Verify:**

```bash
kubectl get pods -n kube-system -l app=coredns
kubectl logs -n kube-system -l app=coredns --tail=50
```

### 2. Deploy External-DNS

```bash
# Configure credentials for your DNS provider

# AWS Route53
kubectl create secret generic external-dns-credentials \
  --from-literal=aws-access-key-id=<KEY> \
  --from-literal=aws-secret-access-key=<SECRET> \
  --namespace=kube-system

# Google Cloud DNS
kubectl create secret generic external-dns-google-credentials \
  --from-file=key.json=<SERVICE_ACCOUNT_KEY> \
  --namespace=kube-system

# Azure DNS
kubectl create secret generic external-dns-credentials \
  --from-literal=azure-tenant-id=<TENANT> \
  --from-literal=azure-client-id=<CLIENT_ID> \
  --from-literal=azure-client-secret=<SECRET> \
  --namespace=kube-system

# Cloudflare
kubectl create secret generic external-dns-credentials \
  --from-literal=cloudflare-api-token=<TOKEN> \
  --namespace=kube-system

# Deploy
kubectl apply -f external-dns.yaml
```

**Verify:**

```bash
kubectl get pods -n kube-system -l app=external-dns
kubectl logs -n kube-system -l app=external-dns --tail=50
```

### 3. Deploy DNS Health Controller

```bash
# Build and push image
cd ../../../services/dns-health-controller
make docker-build IMAGE_REGISTRY=ghcr.io/aikernel
make docker-push IMAGE_REGISTRY=ghcr.io/aikernel

# Deploy
cd ../../infrastructure/prod/k8s/dns
kubectl apply -f dns-health-controller.yaml
```

**Verify:**

```bash
kubectl get pods -n kube-system -l app=dns-health-controller
kubectl logs -n kube-system -l app=dns-health-controller --tail=50
```

### 4. Configure Geographic Routing (Optional)

```bash
kubectl apply -f geographic-routing.yaml
```

### 5. Deploy Monitoring

```bash
kubectl apply -f dns-monitoring.yaml
```

## Configuration

### CoreDNS

Edit `coredns-config.yaml` to customize:

**TTL Configuration:**
```yaml
ttl 30  # 30-second TTL for quick updates
```

**Cache Settings:**
```yaml
cache 30 {
    success 9984 30    # Cache successful responses for 30s
    denial 9984 30     # Cache NXDOMAIN for 30s
    prefetch 10 60s    # Prefetch expiring records
}
```

**Upstream DNS:**
```yaml
forward . 8.8.8.8 8.8.4.4 1.1.1.1
```

### External-DNS

Edit `external-dns.yaml` deployment args:

**Domain Filter:**
```yaml
- --domain-filter=aikernel.io
- --domain-filter=example.com
```

**Provider Configuration:**
```yaml
# AWS Route53
- --provider=aws
- --aws-zone-type=public

# Google Cloud DNS
- --provider=google
- --google-project=my-project

# Azure DNS
- --provider=azure
- --azure-resource-group=my-rg

# Cloudflare
- --provider=cloudflare
- --cloudflare-proxied
```

**TTL:**
```yaml
- --txt-cache-interval=30s
```

### Health Controller

Edit `dns-health-controller-config` ConfigMap:

```yaml
healthCheck:
  interval: 10s              # Check every 10 seconds
  timeout: 3s                # 3-second timeout
  failureThreshold: 3        # Remove after 3 failures
  successThreshold: 1        # Add after 1 success

endpoints:
  healthCheckPath: /health   # Health check endpoint
  removeUnhealthyDelay: 30s  # Wait before removal
```

## Service Annotations

### External DNS

Annotate services to create DNS records:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: my-service
  annotations:
    external-dns.alpha.kubernetes.io/hostname: api.aikernel.io
    external-dns.alpha.kubernetes.io/ttl: "30"
spec:
  type: LoadBalancer
  ports:
    - port: 443
```

### Health Checks

Enable health checking:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: my-service
  annotations:
    dns.aikernel.io/health-check-enabled: "true"
    dns.aikernel.io/health-check-path: "/health"
spec:
  ports:
    - port: 8080
```

### Geographic Routing

Configure region-specific services:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: api-us-east-1
  annotations:
    external-dns.alpha.kubernetes.io/hostname: api-us-east-1.aikernel.io
    dns.aikernel.io/geo-region: "us-east-1"
    dns.aikernel.io/geo-priority: "1"
spec:
  type: LoadBalancer
```

## Monitoring

### Metrics

**CoreDNS Metrics:**
- `coredns_dns_requests_total`: Total DNS requests
- `coredns_dns_request_duration_seconds`: Request latency
- `coredns_dns_responses_total`: Response counts by rcode
- `coredns_cache_hits_total`: Cache hit count
- `coredns_cache_misses_total`: Cache miss count

**Health Controller Metrics:**
- `dns_health_checks_total`: Total health checks
- `dns_health_check_duration_seconds`: Check duration
- `dns_healthy_endpoints`: Healthy endpoint count
- `dns_unhealthy_endpoints`: Unhealthy endpoint count
- `dns_updates_total`: DNS update count
- `dns_endpoints_removed_total`: Removed endpoints
- `dns_endpoints_added_total`: Added endpoints

**External-DNS Metrics:**
- `external_dns_registry_endpoints_total`: Managed endpoints
- `external_dns_registry_errors_total`: Sync errors
- `external_dns_source_endpoints_total`: Source endpoints

### Dashboards

Access Grafana dashboards:

```bash
# Forward Grafana port
kubectl port-forward -n monitoring svc/grafana 3000:3000

# Open browser
open http://localhost:3000
```

Dashboard: "DNS Service Discovery"

### Alerts

Configured Prometheus alerts:

1. **CoreDNSDown**: CoreDNS pods not running
2. **HighDNSErrorRate**: Error rate >5%
3. **HighDNSLatency**: p99 latency >100ms
4. **LowCacheHitRate**: Cache hit rate <50%
5. **HighUnhealthyEndpoints**: >5 unhealthy endpoints
6. **DNSHealthControllerDown**: Controller not running
7. **ExternalDNSSyncFailure**: External DNS sync errors
8. **CoreDNSPodCrashLooping**: CoreDNS crash loop

## Troubleshooting

### DNS Resolution Not Working

```bash
# Check CoreDNS pods
kubectl get pods -n kube-system -l app=coredns

# Check logs
kubectl logs -n kube-system -l app=coredns --tail=100

# Test DNS resolution from pod
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup kubernetes.default

# Check CoreDNS config
kubectl get configmap coredns-custom -n kube-system -o yaml
```

### External DNS Not Creating Records

```bash
# Check external-dns logs
kubectl logs -n kube-system -l app=external-dns --tail=100

# Verify credentials
kubectl get secret external-dns-credentials -n kube-system

# Check service annotations
kubectl get svc my-service -o yaml | grep external-dns

# Test provider access
kubectl exec -it -n kube-system deploy/external-dns -- /bin/sh
# Try listing DNS zones
```

### Unhealthy Endpoints Not Removed

```bash
# Check health controller logs
kubectl logs -n kube-system -l app=dns-health-controller --tail=100

# Check endpoint health
kubectl get endpoints -n aikernel my-service -o yaml

# Verify health check path
curl http://<endpoint-ip>:<port>/health

# Check controller metrics
kubectl port-forward -n kube-system svc/dns-health-controller 8080:8080
curl http://localhost:8080/metrics | grep dns_health
```

### High DNS Latency

```bash
# Check CoreDNS metrics
kubectl port-forward -n kube-system svc/coredns-custom 9153:9153
curl http://localhost:9153/metrics | grep coredns_dns_request_duration

# Check cache hit rate
curl http://localhost:9153/metrics | grep coredns_cache

# Check upstream DNS
kubectl exec -it -n kube-system deploy/coredns-custom -- nslookup google.com

# Scale CoreDNS
kubectl scale deployment coredns-custom --replicas=5 -n kube-system
```

### Geographic Routing Not Working

```bash
# Check service annotations
kubectl get svc -n aikernel -o yaml | grep geo

# Verify LoadBalancer IPs
kubectl get svc -n aikernel -l app=api

# Test from different regions
# From US: dig api.aikernel.io
# From EU: dig api.aikernel.io

# Check external-dns records
kubectl logs -n kube-system -l app=external-dns | grep "api-us-east-1"
```

## Performance

### Resource Usage

**CoreDNS:**
- CPU: 100m request, 500m limit per pod
- Memory: 128Mi request, 512Mi limit per pod
- Typical usage: 50-100m CPU, 100-200Mi memory

**External-DNS:**
- CPU: 50m request, 200m limit
- Memory: 128Mi request, 256Mi limit
- Typical usage: 10-20m CPU, 50-100Mi memory

**Health Controller:**
- CPU: 100m request, 500m limit
- Memory: 128Mi request, 256Mi limit
- Typical usage: 50-100m CPU, 100-150Mi memory

### Scaling

**CoreDNS HPA:**
- Min: 3 replicas
- Max: 10 replicas
- Target CPU: 70%
- Target Memory: 80%

**Query Performance:**
- Target latency: <10ms (p99)
- Cache hit rate: >80%
- Throughput: 10,000+ queries/sec

## Security

### Access Control

- CoreDNS runs with RBAC permissions for service/endpoint discovery
- External-DNS limited to DNS provider API access
- Health Controller can update endpoints only
- No cluster-admin permissions required

### Network Security

- CoreDNS listens on port 53 (UDP/TCP)
- Metrics on port 9153 (internal only)
- Health check endpoints not externally exposed
- Network policies restrict traffic

### Secret Management

- DNS provider credentials in Kubernetes Secrets
- No secrets in ConfigMaps or logs
- Secrets encrypted at rest
- Rotation procedures documented

## Backup and Recovery

### Configuration Backup

```bash
# Backup CoreDNS config
kubectl get configmap coredns-custom -n kube-system -o yaml > coredns-backup.yaml

# Backup external-dns config
kubectl get deployment external-dns -n kube-system -o yaml > external-dns-backup.yaml
```

### DNS Records Backup

External-DNS maintains TXT records for ownership tracking:

```bash
# List managed records
kubectl logs -n kube-system -l app=external-dns | grep "Updating"
```

### Disaster Recovery

1. Redeploy CoreDNS from manifests
2. Redeploy External-DNS with credentials
3. External-DNS will recreate DNS records automatically
4. Health controller will restore endpoint filtering

## Integration

### With Service Mesh (Istio)

CoreDNS integrates with Istio for service discovery:

```yaml
# Istio services automatically discoverable via DNS
curl http://catalog-api.aikernel.svc.cluster.local:8080
```

### With Kong API Gateway

Kong uses CoreDNS for upstream discovery:

```yaml
# Kong routes to internal services
upstream: catalog-api.aikernel.svc.cluster.local
```

### With Catalog

Services registered in catalog get DNS records:

```bash
# Catalog sync creates services
# External-DNS creates public records
# Health controller monitors endpoints
```

## Advanced Configuration

### Custom DNS Zones

Add custom zones to CoreDNS:

```yaml
myzone.local:53 {
    file /etc/coredns/myzone.db
    cache 30
}
```

### DNS-Based Service Discovery

Query services programmatically:

```python
import socket

# Resolve service
ip = socket.gethostbyname("catalog-api.aikernel.svc.cluster.local")

# Get SRV records
srv = socket.getaddrinfo("_http._tcp.catalog-api.aikernel.svc.cluster.local", None)
```

### Multi-Cloud DNS

Configure external-dns for multiple clouds:

```yaml
# Deploy multiple external-dns instances
# One per cloud provider
# Use different domain filters
```

## References

- [CoreDNS Documentation](https://coredns.io/manual/toc/)
- [External-DNS Documentation](https://github.com/kubernetes-sigs/external-dns)
- [Kubernetes DNS Specification](https://kubernetes.io/docs/concepts/services-networking/dns-pod-service/)
- [DNS RFC 1035](https://www.ietf.org/rfc/rfc1035.txt)
