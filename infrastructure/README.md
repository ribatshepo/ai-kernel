# AI Kernel Infrastructure

This directory contains infrastructure setup for the AI-Powered Identity and Secrets Management Kernel platform.

## Overview

The infrastructure is organized into two deployment environments:

- **Development (dev/)**: Docker Compose setup for local development
- **Production (prod/)**: Kubernetes manifests for production deployment

## Infrastructure Components

### Phase 0: Infrastructure Prerequisites

All components must be deployed before Phase 1 (Foundation Setup) can begin.

| Component | Version | Purpose | Dev Port(s) |
|-----------|---------|---------|-------------|
| PostgreSQL | 15+ | Catalog metadata storage | 5432, 5433 |
| Neo4j | 5+ | Relationship graph storage | 7474, 7687 |
| Elasticsearch | 8+ | Full-text search & logs | 9200, 9300 |
| Kibana | 8+ | Elasticsearch UI | 5601 |
| Redis | 7+ | Distributed caching | 6379, 6380 |
| Kafka | 7.5+ | Event streaming | 9092, 29092 |
| Zookeeper | Latest | Kafka coordination | 2181 |
| HashiCorp Vault | 1.15+ | Secrets management | 8200 |
| Prometheus | 2.47+ | Metrics collection | 9090 |
| Grafana | 10+ | Metrics visualization | 3000 |
| Jaeger | 1.50+ | Distributed tracing | 16686, 4317 |

## Development Setup

### Prerequisites

- Docker 20.10+
- Docker Compose 2.0+
- 16GB RAM minimum (32GB recommended)
- 50GB free disk space

### Quick Start

```bash
# Start all infrastructure services
./scripts/start-dev.sh

# Stop all services
./scripts/stop-dev.sh

# Clean all data (WARNING: Destroys all volumes)
cd dev && docker-compose down -v
```

### Manual Control

```bash
# Navigate to dev directory
cd dev

# Start specific services
docker-compose up -d postgres-primary redis-master

# View logs
docker-compose logs -f [service-name]

# Check service status
docker-compose ps

# Stop services
docker-compose down
```

### Service Access

#### PostgreSQL
```bash
# Primary
psql -h localhost -p 5432 -U aikernel -d aikernel_catalog
# Password: aikernel_dev_password

# Replica
psql -h localhost -p 5433 -U aikernel -d aikernel_catalog
```

#### Neo4j
- Browser: http://localhost:7474
- User: neo4j
- Password: aikernel_dev_password

#### Elasticsearch
```bash
# Health check
curl http://localhost:9200/_cluster/health

# List indices
curl http://localhost:9200/_cat/indices
```

#### Kibana
- URL: http://localhost:5601
- No authentication in dev mode

#### Redis
```bash
# Master
redis-cli -h localhost -p 6379

# Replica
redis-cli -h localhost -p 6380
```

#### Kafka
```bash
# List topics
kafka-topics --bootstrap-server localhost:29092 --list

# Create topic
kafka-topics --bootstrap-server localhost:29092 \
  --create --topic test-topic --partitions 3 --replication-factor 1
```

#### Vault
- URL: http://localhost:8200
- Dev Root Token: aikernel-dev-token

```bash
# Set environment variable
export VAULT_ADDR=http://localhost:8200
export VAULT_TOKEN=aikernel-dev-token

# List secrets
vault kv list secret/
```

#### Prometheus
- URL: http://localhost:9090
- Targets: http://localhost:9090/targets

#### Grafana
- URL: http://localhost:3000
- User: admin
- Password: aikernel_dev_password

#### Jaeger
- UI: http://localhost:16686
- OTLP Endpoint: http://localhost:4317

## Production Deployment

### Prerequisites

- Kubernetes 1.28+
- kubectl configured
- Helm 3+ (optional, for charts)
- Persistent storage provisioner
- 100GB+ storage per database node

### Deployment Steps

#### 1. Create Namespaces

```bash
kubectl apply -f prod/k8s/namespaces/aikernel-infrastructure.yaml
```

#### 2. Deploy PostgreSQL

```bash
# Update secrets first
kubectl create secret generic postgres-secret \
  --from-literal=POSTGRES_PASSWORD=<strong-password> \
  -n aikernel-data

# Deploy StatefulSet
kubectl apply -f prod/k8s/postgresql/postgresql-statefulset.yaml

# Verify deployment
kubectl get pods -n aikernel-data -l app=postgres
kubectl get pvc -n aikernel-data
```

#### 3. Deploy Other Services

```bash
# Deploy in order
kubectl apply -f prod/k8s/redis/
kubectl apply -f prod/k8s/elasticsearch/
kubectl apply -f prod/k8s/neo4j/
kubectl apply -f prod/k8s/kafka/
kubectl apply -f prod/k8s/vault/
kubectl apply -f prod/k8s/monitoring/
```

#### 4. Verify Deployment

```bash
# Check all pods
kubectl get pods -n aikernel-data
kubectl get pods -n aikernel-infrastructure
kubectl get pods -n aikernel-monitoring

# Check services
kubectl get svc -n aikernel-data
```

### Production Configuration

#### Storage Classes

Ensure your cluster has appropriate storage classes:

```bash
kubectl get storageclass
```

Update `volumeClaimTemplates` in StatefulSets to use your storage class.

#### Resource Limits

Production manifests include resource requests/limits. Adjust based on your cluster capacity:

- PostgreSQL: 1-2 CPU, 2-4GB RAM per pod
- Neo4j: 2-4 CPU, 4-8GB RAM per pod
- Elasticsearch: 2-4 CPU, 4-8GB RAM per pod
- Redis: 0.5-1 CPU, 1-2GB RAM per pod

#### High Availability

For production HA:

- PostgreSQL: Use Patroni or Stolon for automatic failover
- Neo4j: Deploy 3 core servers + 2 read replicas
- Elasticsearch: Minimum 3 nodes
- Redis: Use Redis Sentinel or Redis Cluster mode
- Kafka: Minimum 3 brokers with replication factor 3

## Configuration

### PostgreSQL

Configuration files:
- `config/postgres/init-db.sql`: Database initialization
- `config/postgres/postgresql.conf`: PostgreSQL settings

Databases created:
- `aikernel_catalog`: Main catalog database
- `aikernel_audit`: Audit logging database

Schemas:
- `catalog`: Resource metadata
- `metadata`: Property tracking
- `lineage`: Data lineage
- `audit`: Event logging

### Prometheus

Configuration: `config/prometheus/prometheus.yml`

Scrape targets:
- Prometheus itself
- PostgreSQL exporters
- Redis exporters
- Elasticsearch
- Kafka
- Application services (to be added)

### Grafana

Provisioning: `config/grafana/provisioning/`

Pre-configured datasources:
- Prometheus (default)
- Elasticsearch (logs)
- Jaeger (traces)

## Monitoring

### Health Checks

```bash
# Check all service health in dev
docker-compose ps

# PostgreSQL
docker-compose exec postgres-primary pg_isready

# Redis
docker-compose exec redis-master redis-cli ping

# Elasticsearch
curl http://localhost:9200/_cluster/health
```

### Metrics

All services export metrics to Prometheus:
- View in Prometheus: http://localhost:9090
- View dashboards in Grafana: http://localhost:3000

### Logs

View logs in development:
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f postgres-primary

# Last 100 lines
docker-compose logs --tail=100 elasticsearch
```

In production, logs are aggregated in Elasticsearch and viewable in Kibana.

## Troubleshooting

### Services Won't Start

```bash
# Check Docker resources
docker system df

# Check logs
docker-compose logs [service-name]

# Clean and restart
docker-compose down
docker-compose up -d
```

### Out of Memory

Increase Docker memory limit in Docker Desktop settings (minimum 8GB, recommended 16GB).

### Port Conflicts

Check if ports are already in use:
```bash
lsof -i :5432  # PostgreSQL
lsof -i :9200  # Elasticsearch
```

Stop conflicting services or modify port mappings in docker-compose.yml.

### Data Persistence

Development data is stored in Docker volumes:
```bash
# List volumes
docker volume ls | grep aikernel

# Backup volume
docker run --rm -v aikernel_postgres-primary-data:/data -v $(pwd):/backup alpine tar czf /backup/postgres-backup.tar.gz -C /data .

# Restore volume
docker run --rm -v aikernel_postgres-primary-data:/data -v $(pwd):/backup alpine tar xzf /backup/postgres-backup.tar.gz -C /data
```

## Backup and Recovery

### PostgreSQL

```bash
# Backup
docker-compose exec postgres-primary pg_dump -U aikernel aikernel_catalog > backup.sql

# Restore
docker-compose exec -T postgres-primary psql -U aikernel aikernel_catalog < backup.sql
```

### Neo4j

```bash
# Backup
docker-compose exec neo4j neo4j-admin dump --database=neo4j --to=/backups/neo4j-backup.dump

# Restore
docker-compose exec neo4j neo4j-admin load --from=/backups/neo4j-backup.dump --database=neo4j --force
```

### Elasticsearch

```bash
# Create snapshot repository
curl -X PUT "localhost:9200/_snapshot/backup_repo" -H 'Content-Type: application/json' -d'
{
  "type": "fs",
  "settings": {
    "location": "/usr/share/elasticsearch/backups"
  }
}
'

# Create snapshot
curl -X PUT "localhost:9200/_snapshot/backup_repo/snapshot_1?wait_for_completion=true"
```

## Security Considerations

### Development Environment

Development setup uses default credentials for convenience. **DO NOT use in production.**

Default credentials:
- PostgreSQL: aikernel / aikernel_dev_password
- Neo4j: neo4j / aikernel_dev_password
- Grafana: admin / aikernel_dev_password
- Vault: aikernel-dev-token

### Production Environment

For production:

1. Use strong, unique passwords
2. Enable TLS/SSL for all services
3. Configure network policies
4. Enable authentication and authorization
5. Use secrets management (Vault or Kubernetes Secrets)
6. Regular security updates
7. Enable audit logging
8. Implement backup encryption

## Next Steps

After infrastructure is running:

1. Verify all services are healthy
2. Run dependency validation checklist from `infrastructure-prerequisites.md`
3. Proceed to Phase 1: Foundation Setup and Catalog Bootstrap
4. Deploy .NET Core orchestration services
5. Implement catalog API

## References

- Infrastructure Prerequisites: `infrastructure-prerequisites.md`
- Architecture Design: `../openspec/changes/implement-ai-kernel-platform/design.md`
- Project Context: `../openspec/project.md`
- Tasks: `../openspec/changes/implement-ai-kernel-platform/tasks.md`

## Support

For issues or questions:
- Review logs: `docker-compose logs [service]`
- Check service health: `docker-compose ps`
- Consult documentation in `openspec/` directory
