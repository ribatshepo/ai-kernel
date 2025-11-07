# LoadBalancer Services and Kubernetes Dashboard

This document describes how to access the AI Kernel Platform services through LoadBalancer services and the Kubernetes Dashboard.

## Overview

The platform exposes the following services via LoadBalancer:

1. **Kubernetes Dashboard** - Web UI for cluster management
2. **Grafana** - Monitoring dashboards
3. **Prometheus** - Metrics and alerting
4. **Jaeger** - Distributed tracing
5. **ArgoCD** - GitOps deployment UI
6. **Istio Ingress Gateway** - Main API gateway
7. **Neo4j Browser** - Graph database UI (optional)
8. **HashiCorp Vault** - Secrets management (use with caution)

## Quick Start

### Deploy LoadBalancer Services

LoadBalancer services are automatically created when running the main deployment:

```bash
./deploy-all.sh
```

Or deploy them separately:

```bash
kubectl apply -f 20-loadbalancer-services.yaml
```

### Install Kubernetes Dashboard

```bash
./21-kubernetes-dashboard-install.sh
kubectl apply -f 22-kubernetes-dashboard-rbac.yaml
```

### Check LoadBalancer Status

```bash
# View all LoadBalancer services
kubectl get svc -A | grep LoadBalancer

# Get specific service external IP
kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb
kubectl get svc -n aikernel-monitoring grafana-lb
kubectl get svc -n argocd argocd-server-lb
```

## Accessing Services

### 1. Kubernetes Dashboard

**URL**: `https://<EXTERNAL-IP>` (from kubernetes-dashboard-lb)

**Get External IP:**
```bash
kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
# Or for hostname-based LBs (like AWS ELB):
kubectl get svc -n kubernetes-dashboard kubernetes-dashboard-lb -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
```

**Authentication:**

The dashboard requires a bearer token for authentication. We've created several service accounts with different permission levels:

**Admin Access (Full Cluster Access):**
```bash
kubectl -n kubernetes-dashboard create token admin-user
```

**Read-Only Access:**
```bash
kubectl -n kubernetes-dashboard create token dashboard-viewer
```

**Namespace-Specific Admin:**
```bash
# Core namespace admin
kubectl -n kubernetes-dashboard create token aikernel-core-admin

# Data namespace admin
kubectl -n kubernetes-dashboard create token aikernel-data-admin

# Monitoring namespace admin
kubectl -n kubernetes-dashboard create token aikernel-monitoring-admin
```

**Developer Access (Limited Write):**
```bash
kubectl -n kubernetes-dashboard create token developer
```

**Monitoring Viewer:**
```bash
kubectl -n kubernetes-dashboard create token monitoring-viewer
```

### 2. Grafana (Monitoring Dashboards)

**URL**: `http://<EXTERNAL-IP>`

**Get External IP:**
```bash
kubectl get svc -n aikernel-monitoring grafana-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

**Login:**
- Username: `admin`
- Password:
  ```bash
  kubectl -n aikernel-monitoring get secret grafana-credentials -o jsonpath='{.data.admin-password}' | base64 -d
  ```

### 3. Prometheus (Metrics)

**URL**: `http://<EXTERNAL-IP>`

**Get External IP:**
```bash
kubectl get svc -n aikernel-monitoring prometheus-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

No authentication required (configure in production).

### 4. Jaeger (Distributed Tracing)

**URL**: `http://<EXTERNAL-IP>`

**Get External IP:**
```bash
kubectl get svc -n aikernel-monitoring jaeger-query-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

No authentication required (configure in production).

### 5. ArgoCD (GitOps UI)

**URL**: `https://<EXTERNAL-IP>`

**Get External IP:**
```bash
kubectl get svc -n argocd argocd-server-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

**Login:**
- Username: `admin`
- Password:
  ```bash
  kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d
  ```

**Change Password:**
```bash
argocd login <ARGOCD-SERVER>
argocd account update-password
```

### 6. Istio Ingress Gateway (Main API)

**URL**: `https://<EXTERNAL-IP>`

**Get External IP:**
```bash
kubectl get svc -n istio-system istio-ingressgateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

This is the main entry point for your applications. Configure virtual services and gateways to route traffic.

### 7. Neo4j Browser (Graph Database)

**URL**: `http://<EXTERNAL-IP>:7474`

**Get External IP:**
```bash
kubectl get svc -n aikernel-data neo4j-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

**Login:**
- Bolt URL: `bolt://<EXTERNAL-IP>:7687`
- Username: `neo4j`
- Password:
  ```bash
  kubectl -n aikernel-data get secret neo4j-credentials -o jsonpath='{.data.neo4j-password}' | base64 -d
  ```

### 8. HashiCorp Vault (Secrets Management)

**URL**: `http://<EXTERNAL-IP>:8200`

**Get External IP:**
```bash
kubectl get svc -n aikernel-security vault-lb -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

⚠️ **Security Warning**: Vault should be accessed securely. Consider using internal-only LoadBalancer or kubectl port-forward.

## Service Account Roles

### Dashboard RBAC Explained

| Service Account | Role | Permissions | Use Case |
|----------------|------|-------------|----------|
| `admin-user` | cluster-admin | Full cluster access | Platform administrators |
| `dashboard-viewer` | view | Read-only cluster-wide | Monitoring teams |
| `aikernel-core-admin` | admin (aikernel-core) | Full access to core namespace | Core service management |
| `aikernel-data-admin` | admin (aikernel-data) | Full access to data namespace | Database administration |
| `aikernel-monitoring-admin` | admin (aikernel-monitoring) | Full access to monitoring | Monitoring configuration |
| `developer` | custom | Limited write, pod exec | Application developers |
| `monitoring-viewer` | custom | Metrics and resource viewing | SRE teams |

## Production Security Considerations

### 1. Internal LoadBalancers

For production, consider using internal-only LoadBalancers:

```yaml
metadata:
  annotations:
    # AWS
    service.beta.kubernetes.io/aws-load-balancer-internal: "true"
    # GCP
    cloud.google.com/load-balancer-type: "Internal"
    # Azure
    service.beta.kubernetes.io/azure-load-balancer-internal: "true"
```

### 2. IP Whitelisting

Restrict access to specific IP ranges:

```yaml
spec:
  loadBalancerSourceRanges:
    - "10.0.0.0/8"      # Internal network
    - "203.0.113.0/24"  # Corporate network
```

### 3. TLS/SSL Termination

Configure SSL termination at the LoadBalancer level:

```yaml
metadata:
  annotations:
    # AWS
    service.beta.kubernetes.io/aws-load-balancer-ssl-cert: "arn:aws:acm:..."
    service.beta.kubernetes.io/aws-load-balancer-backend-protocol: "https"
    # GCP
    cloud.google.com/load-balancer-type: "Internal"
    networking.gke.io/load-balancer-type: "Internal"
```

### 4. Authentication

- **Kubernetes Dashboard**: Always use token authentication, rotate regularly
- **Grafana**: Enable OAuth/OIDC integration
- **ArgoCD**: Configure SSO (SAML/OIDC)
- **Prometheus/Jaeger**: Add authentication proxy (OAuth2 Proxy, etc.)

### 5. Network Policies

Ensure network policies are in place to restrict traffic:

```bash
kubectl get networkpolicies -A
```

### 6. Monitoring Access

Monitor LoadBalancer access logs:

```bash
# AWS
aws elb describe-load-balancer-attributes --load-balancer-name <name>

# GCP
gcloud compute forwarding-rules describe <name>

# Azure
az network lb show --name <name>
```

## DNS Configuration

### Option 1: Manual DNS Records

Create A/CNAME records pointing to LoadBalancer IPs:

```
dashboard.aikernel.io    -> <kubernetes-dashboard-lb-ip>
grafana.aikernel.io      -> <grafana-lb-ip>
prometheus.aikernel.io   -> <prometheus-lb-ip>
jaeger.aikernel.io       -> <jaeger-lb-ip>
argocd.aikernel.io       -> <argocd-lb-ip>
api.aikernel.io          -> <istio-ingressgateway-ip>
```

### Option 2: External DNS (Automated)

Install External DNS to automatically create DNS records:

```bash
# Install via Helm
helm repo add external-dns https://kubernetes-sigs.github.io/external-dns/
helm install external-dns external-dns/external-dns \
  --set provider=aws \
  --set policy=sync \
  --set txtOwnerId=aikernel-cluster

# Add annotations to services
metadata:
  annotations:
    external-dns.alpha.kubernetes.io/hostname: dashboard.aikernel.io
```

## Troubleshooting

### LoadBalancer Stuck in Pending

**Check cloud provider quotas:**
```bash
# AWS
aws service-quotas list-service-quotas --service-code elasticloadbalancing

# GCP
gcloud compute project-info describe

# Azure
az vm list-usage --location eastus
```

**Check events:**
```bash
kubectl describe svc <service-name> -n <namespace>
kubectl get events -n <namespace>
```

### Cannot Access Services

**Verify LoadBalancer is assigned:**
```bash
kubectl get svc -A | grep LoadBalancer
```

**Check security groups/firewall rules:**
```bash
# AWS
aws ec2 describe-security-groups

# GCP
gcloud compute firewall-rules list

# Azure
az network nsg list
```

**Test connectivity:**
```bash
# From your machine
curl -v http://<EXTERNAL-IP>

# From inside cluster
kubectl run -it --rm debug --image=nicolaka/netshoot --restart=Never -- curl http://<service>.<namespace>
```

### Dashboard Token Issues

**Token expired:**
Tokens are short-lived. Generate a new one:
```bash
kubectl -n kubernetes-dashboard create token admin-user
```

**For long-lived tokens (not recommended for production):**
```bash
kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: admin-user-token
  namespace: kubernetes-dashboard
  annotations:
    kubernetes.io/service-account.name: admin-user
type: kubernetes.io/service-account-token
EOF

kubectl -n kubernetes-dashboard get secret admin-user-token -o jsonpath='{.data.token}' | base64 -d
```

## Cost Optimization

LoadBalancers can be expensive. Consider:

1. **Use a single LoadBalancer** with path-based routing:
   - One LoadBalancer for all HTTP services
   - Configure Istio/Ingress routing

2. **Use NodePort for development:**
   ```bash
   kubectl patch svc <service-name> -n <namespace> -p '{"spec":{"type":"NodePort"}}'
   ```

3. **Use kubectl port-forward:**
   ```bash
   kubectl port-forward -n <namespace> svc/<service-name> <local-port>:<remote-port>
   ```

4. **Regional vs Global LoadBalancers:**
   - Use regional LBs when possible (cheaper)
   - Reserve global LBs for multi-region setups

## Monitoring LoadBalancer Costs

### AWS
```bash
# Get LoadBalancer costs
aws ce get-cost-and-usage \
  --time-period Start=2025-01-01,End=2025-01-31 \
  --granularity MONTHLY \
  --metrics UnblendedCost \
  --filter file://filter.json
```

### GCP
```bash
gcloud billing accounts list
gcloud alpha billing accounts get-iam-policy <ACCOUNT_ID>
```

### Azure
```bash
az consumption usage list
```

## Best Practices

1. ✅ **Use Internal LoadBalancers** for non-public services
2. ✅ **Enable Access Logging** on all LoadBalancers
3. ✅ **Configure Health Checks** properly
4. ✅ **Use SSL/TLS** for all external endpoints
5. ✅ **Implement IP Whitelisting** where possible
6. ✅ **Regular Token Rotation** for dashboard access
7. ✅ **Monitor LoadBalancer Metrics** (connections, latency, errors)
8. ✅ **Tag LoadBalancers** properly for cost tracking
9. ✅ **Document DNS Records** and update documentation
10. ✅ **Test Failover Scenarios** regularly

## Alternative Access Methods

If you don't want to use LoadBalancers:

### 1. Ingress Controller
```bash
# Install NGINX Ingress
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.1/deploy/static/provider/cloud/deploy.yaml

# Create Ingress resources for services
kubectl apply -f ingress-resources.yaml
```

### 2. kubectl port-forward
```bash
# Kubernetes Dashboard
kubectl port-forward -n kubernetes-dashboard svc/kubernetes-dashboard 8443:443

# Grafana
kubectl port-forward -n aikernel-monitoring svc/grafana 3000:3000

# Access via https://localhost:8443
```

### 3. kubectl proxy
```bash
kubectl proxy --port=8001
# Access Dashboard at:
# http://localhost:8001/api/v1/namespaces/kubernetes-dashboard/services/https:kubernetes-dashboard:/proxy/
```

## Summary

You now have LoadBalancer services exposing:
- ✅ Kubernetes Dashboard with multi-level RBAC
- ✅ Grafana monitoring dashboards
- ✅ Prometheus metrics
- ✅ Jaeger distributed tracing
- ✅ ArgoCD GitOps UI
- ✅ Istio Ingress Gateway
- ✅ Optional: Neo4j, Vault

All services are accessible via external IPs and properly secured with appropriate RBAC configurations.
