# Metrics Standards and Naming Conventions

This document defines the standard metrics, naming conventions, and labeling standards for the AI-Kernel platform.

## Metric Naming Conventions

### General Rules

1. **Lowercase with underscores**: Use lowercase letters with underscores for separation
   - Good: `http_requests_total`
   - Bad: `httpRequestsTotal`, `HTTP-Requests-Total`

2. **Prefix with component**: Start with the component or subsystem name
   - Good: `catalog_api_requests_total`
   - Bad: `requests_total`

3. **Use base units**: Use base units (seconds, bytes) not derived units (milliseconds, kilobytes)
   - Good: `http_request_duration_seconds`
   - Bad: `http_request_duration_milliseconds`

4. **Suffix conventions**:
   - `_total` for counters: `http_requests_total`
   - `_seconds` for durations: `request_duration_seconds`
   - `_bytes` for sizes: `response_size_bytes`
   - `_ratio` for ratios: `cpu_utilization_ratio`
   - `_count` for gauge counts: `active_connections_count`

5. **Use descriptive names**: Names should be self-explanatory
   - Good: `http_requests_total`
   - Bad: `requests`, `http_req`

## Standard Metrics

### HTTP/API Metrics

#### Request Counter
```prometheus
# HELP http_requests_total Total number of HTTP requests
# TYPE http_requests_total counter
http_requests_total{method="GET",status="200",service="catalog-api",namespace="aikernel"} 1234
```

**Required Labels:**
- `method`: HTTP method (GET, POST, PUT, DELETE, PATCH)
- `status`: HTTP status code (200, 404, 500)
- `service`: Service name
- `namespace`: Kubernetes namespace

**Optional Labels:**
- `endpoint`: API endpoint path template
- `protocol`: Protocol version (http/1.1, http/2)

#### Request Duration
```prometheus
# HELP http_request_duration_seconds HTTP request latency in seconds
# TYPE http_request_duration_seconds histogram
http_request_duration_seconds_bucket{method="GET",status="200",service="catalog-api",le="0.1"} 950
http_request_duration_seconds_bucket{method="GET",status="200",service="catalog-api",le="0.5"} 990
http_request_duration_seconds_bucket{method="GET",status="200",service="catalog-api",le="1.0"} 995
http_request_duration_seconds_bucket{method="GET",status="200",service="catalog-api",le="+Inf"} 1000
http_request_duration_seconds_sum{method="GET",status="200",service="catalog-api"} 250
http_request_duration_seconds_count{method="GET",status="200",service="catalog-api"} 1000
```

**Buckets:** `[0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30]` seconds

#### Request Size
```prometheus
# HELP http_request_size_bytes HTTP request size in bytes
# TYPE http_request_size_bytes histogram
http_request_size_bytes_sum{method="POST",service="catalog-api"} 1048576
http_request_size_bytes_count{method="POST",service="catalog-api"} 100
```

**Buckets:** `[100, 1000, 10000, 100000, 1000000, 10000000]` bytes

#### Response Size
```prometheus
# HELP http_response_size_bytes HTTP response size in bytes
# TYPE http_response_size_bytes histogram
http_response_size_bytes_sum{method="GET",service="catalog-api"} 2097152
http_response_size_bytes_count{method="GET",service="catalog-api"} 500
```

### Database Metrics

#### Connection Pool
```prometheus
# HELP db_connections_active Number of active database connections
# TYPE db_connections_active gauge
db_connections_active{database="catalog",pool="primary"} 25

# HELP db_connections_idle Number of idle database connections
# TYPE db_connections_idle gauge
db_connections_idle{database="catalog",pool="primary"} 75

# HELP db_connections_max Maximum number of database connections
# TYPE db_connections_max gauge
db_connections_max{database="catalog",pool="primary"} 100
```

#### Query Duration
```prometheus
# HELP db_query_duration_seconds Database query duration in seconds
# TYPE db_query_duration_seconds histogram
db_query_duration_seconds_bucket{database="catalog",operation="SELECT",le="0.01"} 900
db_query_duration_seconds_bucket{database="catalog",operation="SELECT",le="0.1"} 990
db_query_duration_seconds_bucket{database="catalog",operation="SELECT",le="1.0"} 999
```

**Labels:**
- `database`: Database name
- `operation`: SQL operation type (SELECT, INSERT, UPDATE, DELETE)
- `table`: Table name (optional)

### Cache Metrics

#### Cache Operations
```prometheus
# HELP cache_operations_total Total number of cache operations
# TYPE cache_operations_total counter
cache_operations_total{cache="redis",operation="get",result="hit"} 9000
cache_operations_total{cache="redis",operation="get",result="miss"} 1000
cache_operations_total{cache="redis",operation="set",result="success"} 500
```

**Labels:**
- `cache`: Cache system name
- `operation`: Operation type (get, set, delete, clear)
- `result`: Operation result (hit, miss, success, error)

#### Cache Size
```prometheus
# HELP cache_size_bytes Current cache size in bytes
# TYPE cache_size_bytes gauge
cache_size_bytes{cache="redis",namespace="sessions"} 104857600

# HELP cache_entries_count Number of entries in cache
# TYPE cache_entries_count gauge
cache_entries_count{cache="redis",namespace="sessions"} 10000
```

### Resource Metrics

#### CPU Usage
```prometheus
# HELP process_cpu_seconds_total Total CPU time consumed in seconds
# TYPE process_cpu_seconds_total counter
process_cpu_seconds_total{service="catalog-api",namespace="aikernel"} 3600

# HELP container_cpu_usage_seconds_total Container CPU usage in seconds
# TYPE container_cpu_usage_seconds_total counter
container_cpu_usage_seconds_total{pod="catalog-api-abc123",namespace="aikernel"} 7200
```

#### Memory Usage
```prometheus
# HELP process_resident_memory_bytes Resident memory size in bytes
# TYPE process_resident_memory_bytes gauge
process_resident_memory_bytes{service="catalog-api"} 268435456

# HELP container_memory_working_set_bytes Container memory working set in bytes
# TYPE container_memory_working_set_bytes gauge
container_memory_working_set_bytes{pod="catalog-api-abc123",namespace="aikernel"} 536870912
```

#### Disk Usage
```prometheus
# HELP disk_used_bytes Disk space used in bytes
# TYPE disk_used_bytes gauge
disk_used_bytes{mount="/data",device="/dev/sda1"} 107374182400

# HELP disk_total_bytes Total disk space in bytes
# TYPE disk_total_bytes gauge
disk_total_bytes{mount="/data",device="/dev/sda1"} 1073741824000
```

### Network Metrics

#### Network Traffic
```prometheus
# HELP network_receive_bytes_total Total bytes received
# TYPE network_receive_bytes_total counter
network_receive_bytes_total{interface="eth0",pod="catalog-api-abc123"} 1073741824

# HELP network_transmit_bytes_total Total bytes transmitted
# TYPE network_transmit_bytes_total counter
network_transmit_bytes_total{interface="eth0",pod="catalog-api-abc123"} 2147483648
```

### Business Metrics

#### Custom Application Metrics
```prometheus
# HELP catalog_resources_total Total number of resources in catalog
# TYPE catalog_resources_total gauge
catalog_resources_total{type="service",status="active"} 150

# HELP catalog_operations_total Total catalog operations
# TYPE catalog_operations_total counter
catalog_operations_total{operation="register",type="service",status="success"} 500
```

## Label Standards

### Required Labels

1. **service**: Service name (e.g., "catalog-api", "kong-gateway")
2. **namespace**: Kubernetes namespace
3. **cluster**: Cluster identifier (e.g., "aikernel-prod")
4. **environment**: Environment (e.g., "production", "staging", "development")

### Optional Labels

1. **version**: Application version
2. **region**: Geographic region
3. **zone**: Availability zone
4. **instance**: Instance identifier
5. **pod**: Pod name
6. **node**: Node name

### Label Cardinality

**Keep cardinality low:**
- Avoid using user IDs, request IDs, or high-cardinality data as labels
- Use aggregation and recording rules instead
- Maximum recommended label combinations: 10,000 per metric

**Good:**
```prometheus
http_requests_total{method="GET",status="200",service="catalog-api"}
```

**Bad:**
```prometheus
http_requests_total{method="GET",status="200",service="catalog-api",user_id="12345",request_id="abc-def-ghi"}
```

## Recording Rules

### Aggregation Patterns

#### Rate Calculation
```yaml
- record: aikernel:http_requests:rate5m
  expr: sum(rate(http_requests_total[5m])) by (service, namespace)
```

#### Percentile Calculation
```yaml
- record: aikernel:http_request_duration:p99
  expr: |
    histogram_quantile(0.99,
      sum(rate(http_request_duration_seconds_bucket[5m])) by (le, service, namespace)
    )
```

#### Error Rate
```yaml
- record: aikernel:http_requests:error_rate5m
  expr: |
    sum(rate(http_requests_total{status=~"5.."}[5m])) by (service, namespace)
    /
    sum(rate(http_requests_total[5m])) by (service, namespace)
```

## Histogram Buckets

### HTTP Latency
```
[0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30]
```

### Database Query Duration
```
[0.001, 0.01, 0.1, 0.5, 1, 5, 10]
```

### Request/Response Size
```
[100, 1000, 10000, 100000, 1000000, 10000000, 100000000]
```

## Instrumentation Guidelines

### Go Applications

```go
import (
    "github.com/prometheus/client_golang/prometheus"
    "github.com/prometheus/client_golang/prometheus/promauto"
)

var (
    httpRequestsTotal = promauto.NewCounterVec(
        prometheus.CounterOpts{
            Name: "http_requests_total",
            Help: "Total number of HTTP requests",
        },
        []string{"method", "status", "service"},
    )

    httpRequestDuration = promauto.NewHistogramVec(
        prometheus.HistogramOpts{
            Name:    "http_request_duration_seconds",
            Help:    "HTTP request latency in seconds",
            Buckets: []float64{0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30},
        },
        []string{"method", "service"},
    )
)

func handleRequest(w http.ResponseWriter, r *http.Request) {
    start := time.Now()

    // Handle request...

    duration := time.Since(start).Seconds()
    httpRequestDuration.WithLabelValues(r.Method, "catalog-api").Observe(duration)
    httpRequestsTotal.WithLabelValues(r.Method, "200", "catalog-api").Inc()
}
```

### Python Applications

```python
from prometheus_client import Counter, Histogram

http_requests_total = Counter(
    'http_requests_total',
    'Total number of HTTP requests',
    ['method', 'status', 'service']
)

http_request_duration = Histogram(
    'http_request_duration_seconds',
    'HTTP request latency in seconds',
    ['method', 'service'],
    buckets=[0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30]
)

@http_request_duration.time()
def handle_request(request):
    # Handle request...
    http_requests_total.labels(
        method=request.method,
        status='200',
        service='catalog-api'
    ).inc()
```

### .NET Applications

```csharp
using Prometheus;

private static readonly Counter HttpRequestsTotal = Metrics
    .CreateCounter(
        "http_requests_total",
        "Total number of HTTP requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "status", "service" }
        }
    );

private static readonly Histogram HttpRequestDuration = Metrics
    .CreateHistogram(
        "http_request_duration_seconds",
        "HTTP request latency in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "service" },
            Buckets = new[] { 0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30 }
        }
    );

public async Task<IActionResult> HandleRequest()
{
    using (HttpRequestDuration.WithLabels("GET", "catalog-api").NewTimer())
    {
        // Handle request...
        HttpRequestsTotal.WithLabels("GET", "200", "catalog-api").Inc();
        return Ok();
    }
}
```

## Anti-Patterns

### 1. High Cardinality Labels

**Bad:**
```prometheus
http_requests_total{user_id="12345",session_id="abc-def"}
```

**Good:**
```prometheus
http_requests_total{service="catalog-api",status="200"}
```

### 2. Using Gauges for Counters

**Bad:**
```prometheus
# TYPE requests_processed gauge
requests_processed{service="api"} 1000
```

**Good:**
```prometheus
# TYPE requests_processed_total counter
requests_processed_total{service="api"} 1000
```

### 3. Inconsistent Naming

**Bad:**
```prometheus
httpRequests_Total
HTTP-Requests
requests_http_total_count
```

**Good:**
```prometheus
http_requests_total
```

### 4. Missing Units

**Bad:**
```prometheus
request_duration{service="api"} 150
```

**Good:**
```prometheus
request_duration_seconds{service="api"} 0.150
```

## Validation

Use `promtool` to validate metrics:

```bash
# Check naming conventions
promtool check metrics < metrics.txt

# Test metric queries
promtool query instant http://prometheus:9090 'http_requests_total'
```

## References

- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
- [OpenMetrics Specification](https://openmetrics.io/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/)
