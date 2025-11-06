# Redis Distributed Cache Infrastructure

Production-grade Redis deployment for AIKernel with high availability, monitoring, and security.

## Architecture

- Redis 7.2+ with 3-node master-replica configuration
- Redis Sentinel for automatic failover
- Pod anti-affinity for node distribution
- PodDisruptionBudget ensuring minimum 2 replicas available
- Persistent storage with 10Gi per instance
- Built-in Prometheus metrics exporter
- Network policies for security isolation

## Prerequisites

- Kubernetes 1.24+
- StorageClass named `fast-ssd` (or modify in statefulset)
- Prometheus Operator (for ServiceMonitor)
- Grafana (for dashboard)
- kubectl and kustomize installed

## Quick Start

### 1. Generate Secure Password

```bash
export REDIS_PASSWORD=$(openssl rand -base64 32)
echo "Redis Password: $REDIS_PASSWORD"
```

### 2. Update Secret

Edit `redis-pdb.yaml` and replace `CHANGE_ME_IN_PRODUCTION` with your generated password, or use:

```bash
kubectl create secret generic redis-password \
  --from-literal=password="$REDIS_PASSWORD" \
  --namespace=aikernel \
  --dry-run=client -o yaml | kubectl apply -f -
```

### 3. Deploy Redis Cluster

```bash
kubectl create namespace aikernel --dry-run=client -o yaml | kubectl apply -f -

kubectl apply -k infrastructure/kubernetes/redis/
```

### 4. Verify Deployment

```bash
kubectl get pods -n aikernel -l app=redis

kubectl logs -n aikernel redis-0 -c redis

kubectl exec -n aikernel redis-0 -c redis -- redis-cli -a "$REDIS_PASSWORD" ping
```

### 5. Check Sentinel Status

```bash
kubectl exec -n aikernel redis-0 -c sentinel -- redis-cli -p 26379 sentinel master mymaster

kubectl exec -n aikernel redis-0 -c sentinel -- redis-cli -p 26379 sentinel replicas mymaster
```

## Connection Configuration

### Application Configuration

Update your `appsettings.json`:

```json
{
  "Caching": {
    "Redis": {
      "ConnectionString": "redis-0.redis-headless.aikernel.svc.cluster.local:6379,redis-1.redis-headless.aikernel.svc.cluster.local:6379,redis-2.redis-headless.aikernel.svc.cluster.local:6379,password=YOUR_PASSWORD,ssl=false,abortConnect=false",
      "InstanceName": "aikernel_",
      "ConnectTimeout": 5000,
      "SyncTimeout": 5000,
      "ConnectRetry": 3,
      "AbortOnConnectFail": false,
      "UseSsl": false
    },
    "Defaults": {
      "ResourceCacheTtlSeconds": 300,
      "SearchResultsTtlSeconds": 600,
      "VersionHistoryTtlSeconds": 3600,
      "LineageQueryTtlSeconds": 900,
      "RelationshipCacheTtlSeconds": 600,
      "EnableCompression": true,
      "CompressionThresholdBytes": 1024
    },
    "Metrics": {
      "Enabled": true,
      "PublishIntervalSeconds": 60,
      "EnableDetailedMetrics": true
    }
  }
}
```

### Environment Variables

```bash
export REDIS_CONNECTION_STRING="redis-0.redis-headless.aikernel.svc.cluster.local:6379,redis-1.redis-headless.aikernel.svc.cluster.local:6379,redis-2.redis-headless.aikernel.svc.cluster.local:6379,password=YOUR_PASSWORD"
```

## Monitoring

### Prometheus Metrics

Redis metrics are exposed on port 9121 and automatically scraped by Prometheus via ServiceMonitor.

Key metrics:
- `redis_up` - Redis instance availability
- `redis_memory_used_bytes` - Memory usage
- `redis_connected_clients` - Connected clients
- `aikernel_cache_hit_rate` - Cache hit rate
- `aikernel_cache_hits_total` - Total cache hits
- `aikernel_cache_misses_total` - Total cache misses

### Grafana Dashboard

Import the dashboard from `infrastructure/grafana/redis-cache-dashboard.json`:

```bash
kubectl create configmap redis-dashboard \
  --from-file=infrastructure/grafana/redis-cache-dashboard.json \
  --namespace=monitoring
```

Or import via Grafana UI using dashboard UID: `aikernel-redis-cache`

### Alerts

Prometheus alerts are configured for:
- Redis instance down (5min)
- High memory usage (>90%)
- High latency (>100ms)
- Too many connections (>5000)
- Rejected connections
- Master link down (2min)
- High key eviction rate (>100/sec)
- Replication lag
- Cache hit rate below 70%

## Security

### Network Policies

Network policies restrict traffic to:
- Ingress: Only from catalog-api pods and Prometheus
- Egress: Only to other Redis pods and DNS

### TLS/SSL

To enable TLS:

1. Create TLS certificates:
```bash
kubectl create secret tls redis-tls \
  --cert=path/to/cert.pem \
  --key=path/to/key.pem \
  --namespace=aikernel
```

2. Update configuration:
- Set `UseSsl: true` in application config
- Update Redis StatefulSet to mount certificates
- Modify Redis config to enable TLS

### Authentication

All Redis instances require password authentication. The password is stored in a Kubernetes Secret and mounted as a file with restricted permissions (0400).

## Maintenance

### Scaling

To change replica count:

```bash
kubectl scale statefulset redis --replicas=5 -n aikernel
```

Note: Always use odd numbers (3, 5, 7) for Sentinel quorum.

### Backup

Redis persistence is enabled with:
- RDB snapshots every 60 seconds if 10000 keys changed
- AOF rewrite when needed

Backup PVC data:

```bash
kubectl exec -n aikernel redis-0 -c redis -- redis-cli -a "$REDIS_PASSWORD" BGSAVE

kubectl cp aikernel/redis-0:/data/dump.rdb ./backup/dump.rdb -c redis
```

### Restore

```bash
kubectl cp ./backup/dump.rdb aikernel/redis-0:/data/dump.rdb -c redis

kubectl delete pod redis-0 -n aikernel
```

### Manual Failover

```bash
kubectl exec -n aikernel redis-0 -c sentinel -- \
  redis-cli -p 26379 sentinel failover mymaster
```

## Troubleshooting

### Check Redis Status

```bash
kubectl exec -n aikernel redis-0 -c redis -- \
  redis-cli -a "$REDIS_PASSWORD" INFO replication
```

### Check Sentinel Status

```bash
kubectl exec -n aikernel redis-0 -c sentinel -- \
  redis-cli -p 26379 sentinel masters
```

### View Logs

```bash
kubectl logs -n aikernel redis-0 -c redis --tail=100
kubectl logs -n aikernel redis-0 -c sentinel --tail=100
kubectl logs -n aikernel redis-0 -c redis-exporter --tail=100
```

### Common Issues

1. **Connection Refused**: Check if password is correct and pods are ready
2. **High Memory Usage**: Review cache TTL settings and maxmemory policy
3. **Replication Lag**: Check network connectivity between pods
4. **Sentinel Not Detecting Failover**: Verify quorum settings and network policies

## Performance Tuning

### Memory Optimization

Adjust `maxmemory` in ConfigMap based on workload:

```yaml
maxmemory 4gb  # For larger datasets
maxmemory-policy allkeys-lru  # Or allkeys-lfu for frequency-based eviction
```

### Persistence Trade-offs

For better performance with acceptable data loss risk:

```yaml
save ""  # Disable RDB
appendonly no  # Disable AOF
```

For maximum durability:

```yaml
appendonly yes
appendfsync always
```

## Cost Optimization

- Adjust resource requests/limits based on actual usage
- Use node selectors to run on cheaper instance types
- Consider reducing replicas to 1 for non-production environments
- Adjust storage size based on actual data volume

## Compliance

This deployment complies with:
- Article II Section 2.3 (Communication Protocols)
- Specification section 2.5 (Request Routing Specifications)
- 90% cache hit rate target
- <5 seconds invalidation latency requirement
