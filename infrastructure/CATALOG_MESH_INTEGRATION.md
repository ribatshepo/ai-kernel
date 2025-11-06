# Catalog-Mesh Integration Design

## Overview

The Catalog-Mesh Integration provides automatic synchronization between the AIKernel Service Catalog and Istio Service Mesh, enabling dynamic service discovery, health-based routing, and metadata-driven traffic management.

## Architecture

```
┌─────────────────┐        ┌──────────────────┐        ┌─────────────────┐
│  Service        │        │  Catalog-Mesh    │        │  Istio          │
│  Catalog API    │◄──────►│  Sync Controller │◄──────►│  Control Plane  │
└─────────────────┘        └──────────────────┘        └─────────────────┘
        │                           │                           │
        │                           │                           │
        ▼                           ▼                           ▼
┌─────────────────┐        ┌──────────────────┐        ┌─────────────────┐
│  Catalog        │        │  ServiceBinding  │        │  VirtualService │
│  Database       │        │  CRDs            │        │  DestinationRule│
│  (PostgreSQL)   │        └──────────────────┘        └─────────────────┘
└─────────────────┘
```

## Components

### 1. Catalog-Mesh Sync Controller

A Kubernetes operator that:
- Watches ServiceCatalog entries via API or database
- Creates/Updates Istio configurations based on catalog metadata
- Maintains bidirectional synchronization
- Handles health status propagation

### 2. ServiceBinding CRD

Custom Resource Definition that represents the binding between catalog services and Istio configurations.

### 3. Health Monitor

Monitors service health and updates both catalog and Istio configurations.

## Implementation

### Phase 1: CRD Definition

```yaml
apiVersion: apiextensions.k8s.io/v1
kind: CustomResourceDefinition
metadata:
  name: servicebindings.aikernel.io
spec:
  group: aikernel.io
  versions:
  - name: v1alpha1
    served: true
    storage: true
    schema:
      openAPIV3Schema:
        type: object
        properties:
          spec:
            type: object
            properties:
              catalogServiceId:
                type: string
                description: ID of the service in the catalog
              serviceName:
                type: string
                description: Kubernetes service name
              namespace:
                type: string
                description: Kubernetes namespace
              trafficPolicy:
                type: object
                properties:
                  retries:
                    type: integer
                  timeout:
                    type: string
                  circuitBreaker:
                    type: object
              healthCheck:
                type: object
                properties:
                  endpoint:
                    type: string
                  interval:
                    type: string
                  timeout:
                    type: string
          status:
            type: object
            properties:
              syncStatus:
                type: string
                enum: [Synced, Syncing, Error]
              lastSync:
                type: string
                format: date-time
              healthStatus:
                type: string
                enum: [Healthy, Degraded, Unhealthy]
```

### Phase 2: Controller Implementation

The controller implements the following reconciliation logic:

```go
func (r *ServiceBindingReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
    // 1. Fetch ServiceBinding CR
    binding := &aikernelv1alpha1.ServiceBinding{}
    if err := r.Get(ctx, req.NamespacedName, binding); err != nil {
        return ctrl.Result{}, client.IgnoreNotFound(err)
    }

    // 2. Fetch service metadata from catalog API
    catalogService, err := r.CatalogClient.GetService(binding.Spec.CatalogServiceId)
    if err != nil {
        return ctrl.Result{}, err
    }

    // 3. Create/Update VirtualService
    vs := r.buildVirtualService(binding, catalogService)
    if err := r.createOrUpdateVirtualService(ctx, vs); err != nil {
        return ctrl.Result{}, err
    }

    // 4. Create/Update DestinationRule
    dr := r.buildDestinationRule(binding, catalogService)
    if err := r.createOrUpdateDestinationRule(ctx, dr); err != nil {
        return ctrl.Result{}, err
    }

    // 5. Update health status
    health := r.checkServiceHealth(binding)
    if err := r.updateCatalogHealth(catalogService.ID, health); err != nil {
        return ctrl.Result{}, err
    }

    // 6. Update ServiceBinding status
    binding.Status.SyncStatus = "Synced"
    binding.Status.LastSync = metav1.Now()
    binding.Status.HealthStatus = health
    if err := r.Status().Update(ctx, binding); err != nil {
        return ctrl.Result{}, err
    }

    return ctrl.Result{RequeueAfter: 30 * time.Second}, nil
}
```

### Phase 3: Catalog Event Listener

The controller listens to catalog events (service registration, updates, deletions) and triggers reconciliation:

```go
func (r *ServiceBindingReconciler) startCatalogEventListener(ctx context.Context) {
    eventStream := r.CatalogClient.SubscribeToEvents(ctx)

    for event := range eventStream {
        switch event.Type {
        case "ServiceRegistered":
            r.handleServiceRegistration(event)
        case "ServiceUpdated":
            r.handleServiceUpdate(event)
        case "ServiceDeregistered":
            r.handleServiceDeregistration(event)
        case "HealthChanged":
            r.handleHealthChange(event)
        }
    }
}
```

### Phase 4: Health-Based Routing

The controller implements health-based routing by:

1. Monitoring service health via health check endpoints
2. Updating DestinationRule outlier detection based on health
3. Removing unhealthy instances within 30 seconds (per requirements)

```go
func (r *ServiceBindingReconciler) buildDestinationRule(binding *ServiceBinding, catalog *CatalogService) *istiov1beta1.DestinationRule {
    return &istiov1beta1.DestinationRule{
        ObjectMeta: metav1.ObjectMeta{
            Name:      binding.Spec.ServiceName,
            Namespace: binding.Spec.Namespace,
        },
        Spec: networkingv1beta1.DestinationRule{
            Host: fmt.Sprintf("%s.%s.svc.cluster.local", binding.Spec.ServiceName, binding.Spec.Namespace),
            TrafficPolicy: &networkingv1beta1.TrafficPolicy{
                OutlierDetection: &networkingv1beta1.OutlierDetection{
                    ConsecutiveErrors: catalog.HealthCheck.FailureThreshold,
                    Interval:          &duration.Duration{Seconds: 30},
                    BaseEjectionTime:  &duration.Duration{Seconds: 30},
                    MaxEjectionPercent: 50,
                },
                ConnectionPool: &networkingv1beta1.ConnectionPoolSettings{
                    Http: &networkingv1beta1.HTTPSettings{
                        MaxRetries: binding.Spec.TrafficPolicy.Retries,
                    },
                },
            },
        },
    }
}
```

## Deployment

### Prerequisites

- Kubernetes cluster with Istio installed
- Service Catalog API deployed
- PostgreSQL database for catalog

### Installation

```bash
# 1. Install CRDs
kubectl apply -f catalog-mesh-integration/crds/

# 2. Deploy controller
kubectl apply -f catalog-mesh-integration/deployment/

# 3. Create RBAC
kubectl apply -f catalog-mesh-integration/rbac/

# 4. Configure catalog connection
kubectl create secret generic catalog-credentials \
  --from-literal=api-url=http://catalog-api.aikernel.svc.cluster.local \
  --from-literal=api-key=<api-key> \
  -n aikernel-infrastructure
```

### Example ServiceBinding

```yaml
apiVersion: aikernel.io/v1alpha1
kind: ServiceBinding
metadata:
  name: worker-service-binding
  namespace: aikernel
spec:
  catalogServiceId: "worker-service-001"
  serviceName: worker-service
  namespace: aikernel
  trafficPolicy:
    retries: 3
    timeout: "30s"
    circuitBreaker:
      consecutiveErrors: 5
      interval: "30s"
  healthCheck:
    endpoint: "/health"
    interval: "10s"
    timeout: "3s"
```

## Integration Points

### 1. Catalog API Integration

The controller integrates with the catalog API to:

- **Fetch service metadata**: Retrieves service configuration, endpoints, and capabilities
- **Update health status**: Reports service health back to catalog
- **Subscribe to events**: Listens for service lifecycle events

```go
type CatalogClient interface {
    GetService(id string) (*CatalogService, error)
    UpdateHealth(id string, health HealthStatus) error
    SubscribeToEvents(ctx context.Context) <-chan Event
    ListServices(filter ServiceFilter) ([]*CatalogService, error)
}
```

### 2. Istio API Integration

The controller manages Istio resources:

```go
type IstioManager interface {
    CreateVirtualService(vs *networkingv1beta1.VirtualService) error
    UpdateVirtualService(vs *networkingv1beta1.VirtualService) error
    DeleteVirtualService(name, namespace string) error
    CreateDestinationRule(dr *networkingv1beta1.DestinationRule) error
    UpdateDestinationRule(dr *networkingv1beta1.DestinationRule) error
    DeleteDestinationRule(name, namespace string) error
}
```

## Configuration Mapping

### Catalog Metadata → Istio Configuration

| Catalog Field | Istio Configuration | Purpose |
|---------------|---------------------|---------|
| `service.timeout` | `VirtualService.spec.http[].timeout` | Request timeout |
| `service.retries` | `VirtualService.spec.http[].retries` | Retry policy |
| `service.circuitBreaker` | `DestinationRule.spec.trafficPolicy.outlierDetection` | Circuit breaker |
| `service.loadBalancing` | `DestinationRule.spec.trafficPolicy.loadBalancer` | Load balancing algorithm |
| `service.connectionPool` | `DestinationRule.spec.trafficPolicy.connectionPool` | Connection limits |
| `service.healthCheck` | `DestinationRule.spec.trafficPolicy.outlierDetection` | Health checking |

## Health Monitoring

### Health Check Flow

```
1. Controller performs health check (HTTP GET /health)
   ↓
2. If healthy: Mark endpoints as active in Istio
   ↓
3. If unhealthy (3 consecutive failures):
   - Update DestinationRule to eject endpoint
   - Update catalog health status
   - Trigger alert
   ↓
4. Monitor for recovery:
   - Continue health checks
   - When healthy: Re-enable endpoint
   - Update catalog status
```

### Health Check Implementation

```go
func (r *ServiceBindingReconciler) checkServiceHealth(binding *ServiceBinding) string {
    endpoints := r.getServiceEndpoints(binding)

    healthyCount := 0
    totalCount := len(endpoints)

    for _, endpoint := range endpoints {
        if r.isEndpointHealthy(endpoint, binding.Spec.HealthCheck) {
            healthyCount++
        }
    }

    healthPercentage := float64(healthyCount) / float64(totalCount)

    if healthPercentage >= 0.9 {
        return "Healthy"
    } else if healthPercentage >= 0.5 {
        return "Degraded"
    }
    return "Unhealthy"
}
```

## Synchronization Strategy

### Eventual Consistency

The integration maintains eventual consistency between catalog and mesh:

1. **Catalog as Source of Truth**: Service metadata in catalog drives Istio configuration
2. **Health Feedback Loop**: Mesh health status updates catalog
3. **Reconciliation Period**: 30 seconds default, configurable
4. **Conflict Resolution**: Catalog metadata takes precedence

### Synchronization Events

| Event | Source | Action |
|-------|--------|--------|
| Service Registered | Catalog | Create ServiceBinding, VirtualService, DestinationRule |
| Service Updated | Catalog | Update Istio configurations |
| Service Deregistered | Catalog | Delete Istio configurations |
| Health Changed | Mesh | Update catalog health status |
| Endpoint Added | Mesh | Update catalog endpoint list |
| Endpoint Removed | Mesh | Update catalog endpoint list |

## Monitoring and Observability

### Metrics

The controller exposes Prometheus metrics:

```
# Synchronization metrics
catalog_mesh_sync_total{status="success|failure"}
catalog_mesh_sync_duration_seconds

# Health check metrics
catalog_mesh_health_check_total{service,status}
catalog_mesh_health_check_duration_seconds

# Resource metrics
catalog_mesh_virturalservices_total
catalog_mesh_destinationrules_total
catalog_mesh_servicebindings_total
```

### Logs

Structured logging includes:

```json
{
  "level": "info",
  "msg": "Synced service",
  "catalogServiceId": "worker-service-001",
  "serviceName": "worker-service",
  "namespace": "aikernel",
  "syncDuration": "125ms",
  "healthStatus": "Healthy"
}
```

## Security Considerations

1. **API Authentication**: Controller authenticates to catalog API using service account tokens
2. **RBAC**: Controller has minimal required permissions for Istio resources
3. **Secret Management**: Credentials stored in Kubernetes secrets
4. **Audit Logging**: All synchronization events logged for audit

## Performance

### Scalability

- Supports 1000+ services
- Synchronization latency < 1 second
- Health check throughput: 100 checks/second
- Resource usage: ~100MB memory, 100m CPU per 1000 services

### Optimization

- Batch processing of multiple service updates
- Caching of catalog metadata
- Debouncing of rapid updates
- Leader election for HA deployment

## Testing Strategy

### Unit Tests

- CRD validation
- Reconciliation logic
- Health check logic
- Configuration mapping

### Integration Tests

- End-to-end service registration flow
- Health status propagation
- Failover scenarios
- Configuration updates

### Chaos Testing

- Catalog API failures
- Istio control plane failures
- Network partitions
- High load scenarios

## Future Enhancements

1. **Multi-cluster Support**: Sync across multiple Kubernetes clusters
2. **Advanced Routing**: A/B testing, canary deployments based on catalog metadata
3. **Cost Optimization**: Route based on instance cost from catalog
4. **SLO Integration**: Automatic traffic management based on SLO targets
5. **Auto-scaling Integration**: Trigger scaling based on mesh metrics

## References

- [Istio Traffic Management](https://istio.io/latest/docs/concepts/traffic-management/)
- [Kubernetes Operators](https://kubernetes.io/docs/concepts/extend-kubernetes/operator/)
- [AIKernel Catalog API](../../docs/api/catalog.md)
- [Service Mesh Decision](./SERVICE_MESH_DECISION.md)

## Support

For implementation support:
1. Review controller logs: `kubectl logs -n aikernel-infrastructure -l app=catalog-mesh-controller`
2. Check ServiceBinding status: `kubectl get servicebindings -A`
3. Verify Istio configuration: `istioctl analyze`
