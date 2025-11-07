#!/bin/bash
#
# Istio Installation Script for AI Kernel Platform
# This script installs and configures Istio service mesh
#

set -e

# Configuration
ISTIO_VERSION="1.20.0"
CLUSTER_NAME="${CLUSTER_NAME:-aikernel}"
NAMESPACE="istio-system"

echo "=== Installing Istio ${ISTIO_VERSION} for AI Kernel Platform ==="

# Download Istio
echo "Downloading Istio ${ISTIO_VERSION}..."
curl -L https://istio.io/downloadIstio | ISTIO_VERSION=${ISTIO_VERSION} sh -
cd istio-${ISTIO_VERSION}

# Add istioctl to PATH
export PATH=$PWD/bin:$PATH

# Create namespace
echo "Creating Istio namespace..."
kubectl create namespace ${NAMESPACE} --dry-run=client -o yaml | kubectl apply -f -

# Install Istio with production profile
echo "Installing Istio with production configuration..."
istioctl install -y -f - <<EOF
apiVersion: install.istio.io/v1alpha1
kind: IstioOperator
metadata:
  namespace: ${NAMESPACE}
  name: aikernel-istio
spec:
  profile: production

  # Hub and tag for the images
  hub: docker.io/istio
  tag: ${ISTIO_VERSION}

  # Component configuration
  components:
    # Ingress Gateway
    ingressGateways:
      - name: istio-ingressgateway
        enabled: true
        k8s:
          service:
            type: LoadBalancer
            ports:
              - port: 80
                targetPort: 8080
                name: http2
              - port: 443
                targetPort: 8443
                name: https
              - port: 15021
                targetPort: 15021
                name: status-port
          resources:
            requests:
              cpu: 500m
              memory: 1Gi
            limits:
              cpu: 2000m
              memory: 2Gi
          hpaSpec:
            minReplicas: 2
            maxReplicas: 5
            metrics:
              - type: Resource
                resource:
                  name: cpu
                  targetAverageUtilization: 80

    # Egress Gateway
    egressGateways:
      - name: istio-egressgateway
        enabled: true
        k8s:
          resources:
            requests:
              cpu: 250m
              memory: 512Mi
            limits:
              cpu: 1000m
              memory: 1Gi
          hpaSpec:
            minReplicas: 2
            maxReplicas: 3

    # Pilot (Control Plane)
    pilot:
      enabled: true
      k8s:
        resources:
          requests:
            cpu: 500m
            memory: 2Gi
          limits:
            cpu: 2000m
            memory: 4Gi
        hpaSpec:
          minReplicas: 2
          maxReplicas: 5
        env:
          - name: PILOT_TRACE_SAMPLING
            value: "1.0"  # 100% tracing for comprehensive observability

  # Mesh configuration
  meshConfig:
    # Access logging
    accessLogFile: /dev/stdout
    accessLogEncoding: JSON
    accessLogFormat: |
      {
        "timestamp": "%START_TIME%",
        "duration": "%DURATION%",
        "method": "%REQ(:METHOD)%",
        "path": "%REQ(X-ENVOY-ORIGINAL-PATH?:PATH)%",
        "protocol": "%PROTOCOL%",
        "response_code": "%RESPONSE_CODE%",
        "response_flags": "%RESPONSE_FLAGS%",
        "bytes_received": "%BYTES_RECEIVED%",
        "bytes_sent": "%BYTES_SENT%",
        "upstream_host": "%UPSTREAM_HOST%",
        "user_agent": "%REQ(USER-AGENT)%",
        "request_id": "%REQ(X-REQUEST-ID)%",
        "authority": "%REQ(:AUTHORITY)%",
        "upstream_service_time": "%RESP(X-ENVOY-UPSTREAM-SERVICE-TIME)%",
        "trace_id": "%REQ(X-B3-TRACEID)%",
        "span_id": "%REQ(X-B3-SPANID)%"
      }

    # Default configuration for all sidecars
    defaultConfig:
      tracing:
        sampling: 100.0  # 100% sampling for development, reduce in production
        zipkin:
          address: jaeger-collector.aikernel-monitoring:9411

      # Proxy resources
      proxyMetadata:
        ISTIO_META_DNS_CAPTURE: "true"

    # Enable automatic mTLS
    enableAutoMtls: true

    # Outbound traffic policy
    outboundTrafficPolicy:
      mode: REGISTRY_ONLY  # Only allow traffic to registered services

    # Service discovery
    defaultServiceExportTo:
      - "*"
    defaultVirtualServiceExportTo:
      - "*"
    defaultDestinationRuleExportTo:
      - "*"

    # Trust domain
    trustDomain: cluster.local

  # Values for additional configuration
  values:
    global:
      # Logging level
      logging:
        level: "default:info"

      # Multi-cluster configuration
      multiCluster:
        enabled: false
        clusterName: ${CLUSTER_NAME}

      # Certificate configuration
      pilotCertProvider: istiod

      # Prometheus integration
      proxy:
        resources:
          requests:
            cpu: 100m
            memory: 128Mi
          limits:
            cpu: 2000m
            memory: 1Gi

    # Telemetry
    telemetry:
      enabled: true
      v2:
        enabled: true
        prometheus:
          enabled: true
        stackdriver:
          enabled: false

    # Kiali integration
    kiali:
      enabled: false  # Install separately if needed

    # Grafana integration
    grafana:
      enabled: false  # Using our own Grafana installation

    # Prometheus integration
    prometheus:
      enabled: false  # Using our own Prometheus installation

    # Jaeger integration
    tracing:
      enabled: false  # Using our own Jaeger installation
EOF

echo "Istio installation complete!"

# Verify installation
echo "Verifying Istio installation..."
istioctl verify-install

# Create monitoring integration
echo "Creating monitoring integration..."
kubectl apply -f - <<EOF
---
apiVersion: v1
kind: Service
metadata:
  name: istio-telemetry
  namespace: ${NAMESPACE}
  labels:
    app: istio-telemetry
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "15014"
spec:
  selector:
    app: istiod
  ports:
    - name: prometheus
      port: 15014
      targetPort: 15014
EOF

echo ""
echo "=== Istio Installation Summary ==="
echo "Version: ${ISTIO_VERSION}"
echo "Namespace: ${NAMESPACE}"
echo "Cluster: ${CLUSTER_NAME}"
echo ""
echo "Next steps:"
echo "1. Enable automatic sidecar injection for application namespaces:"
echo "   kubectl label namespace aikernel-core istio-injection=enabled"
echo "   kubectl label namespace aikernel-data istio-injection=enabled"
echo "   kubectl label namespace aikernel-security istio-injection=enabled"
echo "   kubectl label namespace aikernel-ai istio-injection=enabled"
echo ""
echo "2. Apply mTLS and security policies (see 16-istio-security.yaml)"
echo "3. Configure gateways and virtual services (see 17-istio-gateway.yaml)"
echo ""
