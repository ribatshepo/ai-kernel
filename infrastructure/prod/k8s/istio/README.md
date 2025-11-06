# Istio Service Mesh Installation Guide

This guide provides comprehensive instructions for deploying the Istio service mesh in the AIKernel production environment.

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Architecture](#architecture)
4. [Installation](#installation)
5. [Validation](#validation)
6. [Monitoring](#monitoring)
7. [Troubleshooting](#troubleshooting)
8. [Upgrade Procedure](#upgrade-procedure)
9. [Rollback](#rollback)

## Overview

The AIKernel service mesh implementation uses **Istio 1.20** in production profile with:
- High availability control plane (3 replicas)
- Strict mTLS with 24-hour certificate rotation
- Advanced traffic management (circuit breakers, retries, timeouts)
- Comprehensive observability (Prometheus, Jaeger, Grafana)
- Automatic sidecar injection
- Health-based routing with 30-second failover

## Prerequisites

### Required Tools

```bash
# Istio CLI
curl -L https://istio.io/downloadIstio | ISTIO_VERSION=1.20.0 sh -
export PATH=$PWD/istio-1.20.0/bin:$PATH

# kubectl
kubectl version --client

# kustomize (v5.0+)
kustomize version

# helm (v3.0+) - optional
helm version
```

### Cluster Requirements

- Kubernetes 1.26+
- Minimum 3 worker nodes
- At least 8GB RAM per node
- StorageClass `fast-ssd` configured for persistent volumes
- Network plugin supporting NetworkPolicies
- LoadBalancer support for ingress gateway

### Resource Requirements

| Component | CPU Request | Memory Request | Replicas |
|-----------|-------------|----------------|----------|
| Istiod | 500m | 2Gi | 3 |
| Ingress Gateway | 500m | 512Mi | 3 |
| Egress Gateway | 200m | 256Mi | 2 |
| Prometheus | 500m | 2Gi | 2 |
| Jaeger Collector | 200m | 512Mi | 3 |
| Jaeger Query | 200m | 256Mi | 2 |
| Grafana | 200m | 256Mi | 2 |
| **Total** | **6.4 CPU** | **16Gi RAM** | - |

## Architecture

### Control Plane

- **Istiod**: Unified control plane managing configuration, certificate issuance, and service discovery
- **High Availability**: 3 replicas with pod anti-affinity
- **Certificate Management**: Automatic 24-hour rotation with SPIFFE compliance

### Data Plane

- **Envoy Sidecars**: Automatically injected into all pods in labeled namespaces
- **mTLS**: Strict mode enforced for all service-to-service communication
- **Protocol Detection**: Automatic protocol sniffing for HTTP, HTTP/2, gRPC, TCP

### Gateways

- **Ingress Gateway**: External traffic entry point with TLS 1.3
- **Egress Gateway**: Controlled external service access
- **Load Balancing**: Layer 4 and Layer 7 load balancing with health checks

## Installation

### Step 1: Install Istio Operator

The IstioOperator CRD defines the desired state of the Istio deployment:

```bash
# Install Istio operator CRD
istioctl operator init --revision 1-20-0

# Verify operator is running
kubectl get pods -n istio-operator
```

### Step 2: Create Namespaces

```bash
# Create all required namespaces with sidecar injection labels
kubectl apply -f ../namespaces/aikernel-infrastructure.yaml

# Verify namespaces are created with istio-injection label
kubectl get namespaces -L istio-injection
```

Expected output:
```
NAME                      STATUS   AGE   ISTIO-INJECTION
aikernel                  Active   1m    enabled
aikernel-infrastructure   Active   1m    enabled
aikernel-data             Active   1m    enabled
aikernel-monitoring       Active   1m    enabled
```

### Step 3: Deploy Istio using Kustomize

```bash
# Apply all Istio configurations
kubectl apply -k .

# Wait for Istio to be ready
kubectl wait --for=condition=Ready pods --all -n istio-system --timeout=600s
```

### Step 4: Verify Installation

```bash
# Check Istiod status
kubectl get pods -n istio-system -l app=istiod

# Check gateway status
kubectl get pods -n istio-system -l app=istio-ingressgateway
kubectl get pods -n istio-system -l app=istio-egressgateway

# Check service status
kubectl get svc -n istio-system

# Verify mTLS is enabled
kubectl get peerauthentication --all-namespaces
```

### Step 5: Deploy Observability Stack

Observability components are deployed automatically with the kustomize configuration:

```bash
# Verify Prometheus
kubectl get pods -n aikernel-monitoring -l app=prometheus

# Verify Jaeger
kubectl get pods -n aikernel-monitoring -l app=jaeger

# Verify Grafana
kubectl get pods -n aikernel-monitoring -l app=grafana

# Check services
kubectl get svc -n aikernel-monitoring
```

### Step 6: Configure External Access (Optional)

```bash
# Get ingress gateway external IP
kubectl get svc istio-ingressgateway -n istio-system

# Configure DNS records:
# api.aikernel.io -> <EXTERNAL-IP>
# catalog.aikernel.io -> <EXTERNAL-IP>
# monitoring.aikernel.io -> <EXTERNAL-IP>
```

## Validation

### Automated Validation Script

Use the provided validation script:

```bash
./deploy-istio.sh --validate
```

### Manual Validation

#### 1. Verify Control Plane Health

```bash
istioctl proxy-status
istioctl analyze --all-namespaces
```

#### 2. Test mTLS

Deploy test application:

```bash
kubectl create namespace test-mesh
kubectl label namespace test-mesh istio-injection=enabled

# Deploy sleep pod
kubectl apply -f <(istioctl kube-inject -f samples/sleep/sleep.yaml) -n test-mesh

# Verify mTLS
kubectl exec -it deploy/sleep -n test-mesh -c istio-proxy -- \
  pilot-agent request GET stats | grep ssl_handshake
```

Expected output should show successful TLS handshakes.

#### 3. Test Traffic Management

```bash
# Check destination rules
kubectl get destinationrules --all-namespaces

# Check virtual services
kubectl get virtualservices --all-namespaces

# Test circuit breaker
kubectl exec -it deploy/sleep -n test-mesh -- \
  for i in $(seq 1 100); do curl -s http://catalog-api.aikernel/health; done
```

#### 4. Verify Certificate Rotation

```bash
# Check certificate expiration
kubectl exec -it deploy/sleep -n test-mesh -c istio-proxy -- \
  pilot-agent request GET certs | grep "Valid Till"

# Certificate should expire in ~24 hours
```

#### 5. Test Observability

Access Grafana dashboards:

```bash
# Port-forward Grafana
kubectl port-forward -n aikernel-monitoring svc/grafana 3000:80

# Open browser: http://localhost:3000
# Login: admin / <password from secret>
# Navigate to: Dashboards -> Istio -> Istio Mesh Dashboard
```

Access Jaeger UI:

```bash
# Port-forward Jaeger
kubectl port-forward -n aikernel-monitoring svc/jaeger-query 16686:80

# Open browser: http://localhost:16686
# Search for traces
```

## Monitoring

### Key Metrics

Monitor these critical metrics in Prometheus/Grafana:

1. **Control Plane Health**
   - `pilot_xds_pushes`: Configuration push rate
   - `pilot_proxy_convergence_time`: Time for proxies to converge

2. **Data Plane Health**
   - `istio_requests_total`: Total request count
   - `istio_request_duration_milliseconds`: Request latency
   - `istio_tcp_connections_opened_total`: TCP connection count

3. **mTLS Metrics**
   - `envoy_server_ssl_handshake`: TLS handshake count
   - `citadel_server_root_cert_expiry_timestamp`: Root cert expiration

4. **Circuit Breaker Metrics**
   - `envoy_cluster_upstream_rq_pending_overflow`: Circuit breaker triggers
   - `envoy_cluster_outlier_detection_ejections_active`: Ejected endpoints

### Alerting Rules

Pre-configured alerts in Prometheus:

- `ServiceMeshDown`: Istiod unavailable
- `HighErrorRate`: Service error rate >5%
- `HighLatency`: P99 latency >1s
- `CircuitBreakerTriggered`: Circuit breaker activated
- `CertificateExpiringSoon`: Certificate expires <24h

### Dashboards

Grafana includes these pre-configured dashboards:

1. **Istio Mesh Dashboard**: Overall mesh health
2. **Istio Service Dashboard**: Per-service metrics
3. **Istio Workload Dashboard**: Per-workload metrics
4. **Istio Performance Dashboard**: Latency and throughput

## Troubleshooting

### Common Issues

#### 1. Sidecar Not Injected

**Symptom**: Pods don't have istio-proxy container

**Solution**:
```bash
# Check namespace label
kubectl get namespace <namespace> -o yaml | grep istio-injection

# If missing, add label
kubectl label namespace <namespace> istio-injection=enabled

# Restart pods
kubectl rollout restart deployment -n <namespace>
```

#### 2. mTLS Connection Failures

**Symptom**: Services can't communicate

**Solution**:
```bash
# Check peer authentication
kubectl get peerauthentication --all-namespaces

# Check destination rules
kubectl get destinationrules --all-namespaces

# Verify certificates
istioctl proxy-config secret <pod> -n <namespace>
```

#### 3. High Latency

**Symptom**: Increased request latency

**Solution**:
```bash
# Check proxy resource usage
kubectl top pods -n <namespace> -c istio-proxy

# Increase proxy resources if needed
# Check for circuit breaker triggers
kubectl exec -it <pod> -n <namespace> -c istio-proxy -- \
  pilot-agent request GET stats | grep overflow
```

#### 4. Certificate Rotation Failures

**Symptom**: Certificate not rotating

**Solution**:
```bash
# Check Istiod logs
kubectl logs -n istio-system -l app=istiod --tail=100

# Check CA configuration
kubectl get configmap istio-ca-config -n istio-system -o yaml

# Manually trigger rotation
kubectl delete pod -n istio-system -l app=istiod
```

### Debug Commands

```bash
# Get proxy configuration
istioctl proxy-config cluster <pod> -n <namespace>
istioctl proxy-config listener <pod> -n <namespace>
istioctl proxy-config route <pod> -n <namespace>

# Get proxy logs
kubectl logs <pod> -n <namespace> -c istio-proxy

# Analyze mesh configuration
istioctl analyze --all-namespaces

# Get detailed proxy status
istioctl experimental describe pod <pod> -n <namespace>
```

## Upgrade Procedure

### Canary Upgrade (Recommended)

Istio supports canary upgrades using revisions:

```bash
# Install new revision
istioctl operator init --revision 1-21-0

# Deploy new control plane
kubectl apply -f istio-operator-1-21-0.yaml

# Update namespace labels to use new revision
kubectl label namespace <namespace> istio.io/rev=1-21-0 --overwrite
kubectl label namespace <namespace> istio-injection-

# Restart workloads
kubectl rollout restart deployment -n <namespace>

# Verify new version
istioctl proxy-status

# Remove old revision once validated
istioctl uninstall --revision 1-20-0
```

### In-Place Upgrade

```bash
# Update IstioOperator
kubectl apply -f istio-operator-updated.yaml

# Wait for rollout
kubectl rollout status deployment istiod -n istio-system

# Restart all workloads to pick up new sidecars
kubectl rollout restart deployment --all-namespaces
```

## Rollback

If issues occur during upgrade:

```bash
# Revert to previous revision
kubectl label namespace <namespace> istio.io/rev=1-20-0 --overwrite

# Restart workloads
kubectl rollout restart deployment -n <namespace>

# Remove failed revision
istioctl uninstall --revision 1-21-0
```

## Security Considerations

1. **Certificate Management**
   - Certificates rotate every 24 hours automatically
   - Root CA certificate valid for 10 years
   - SPIFFE-compliant identities

2. **Network Policies**
   - NetworkPolicies enforce additional layer of security
   - Default deny-all with explicit allow rules

3. **Authorization Policies**
   - Layer 7 authorization based on JWT claims
   - Service-to-service authorization rules

4. **Secrets Management**
   - TLS certificates stored as Kubernetes secrets
   - Use external CA integration for production (e.g., cert-manager)

## Performance Tuning

### Proxy Resources

Adjust based on workload:

```yaml
metadata:
  annotations:
    sidecar.istio.io/proxyCPU: "200m"
    sidecar.istio.io/proxyMemory: "256Mi"
```

### Connection Pool

Tune based on traffic patterns in `destination-rules.yaml`:

```yaml
connectionPool:
  tcp:
    maxConnections: 100
  http:
    http1MaxPendingRequests: 100
    http2MaxRequests: 1000
```

### Circuit Breaker

Adjust thresholds in `destination-rules.yaml`:

```yaml
outlierDetection:
  consecutiveErrors: 5
  interval: 30s
  baseEjectionTime: 30s
```

## References

- [Istio Documentation](https://istio.io/latest/docs/)
- [Service Mesh Decision](../../../SERVICE_MESH_DECISION.md)
- [AIKernel Architecture](../../../../openspec/architecture.md)
- [Constitutional Requirements](../../../../openspec/constitution.md)

## Support

For issues or questions:
1. Check logs: `kubectl logs -n istio-system -l app=istiod`
2. Run diagnostics: `istioctl analyze --all-namespaces`
3. Review metrics in Grafana
4. Consult Istio community: https://discuss.istio.io/
