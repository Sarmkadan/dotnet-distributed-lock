# Deployment Guide

## Production Deployment Strategies

This guide covers deploying SarmKadan.DistributedLock in production environments.

## Backend Selection by Deployment Model

### Single-Server Deployment

**Recommended Backend: SQLite**

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.SQLite;
    options.ConnectionString = "Data Source=/var/locks/app.db;Cache=Shared;";
    options.DefaultLockDuration = TimeSpan.FromSeconds(30);
});
```

**Advantages:**
- No external dependencies
- Zero configuration
- Suitable for single host

**Considerations:**
- Not suitable for multi-host deployments
- Backup `app.db` regularly

### Multi-Server with Shared Database

**Recommended Backend: PostgreSQL**

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.PostgreSQL;
    options.ConnectionString = 
        "Host=db.internal;Database=locks;Username=app;Password=***;SSL Mode=Require;Connection Timeout=30;";
    
    // Connection pooling
    // Connection string should be: "...;Max Pool Size=100;"
});
```

**Database Setup:**

```bash
# Connect to PostgreSQL
psql -U postgres

# Create database and tables
CREATE DATABASE distributed_locks;

\c distributed_locks

CREATE TABLE locks (
    key VARCHAR(255) PRIMARY KEY,
    owner_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(50) NOT NULL,
    renewal_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_expires_at ON locks(expires_at);
CREATE INDEX idx_owner_id ON locks(owner_id);

-- Auto-cleanup trigger
CREATE OR REPLACE FUNCTION cleanup_expired_locks()
RETURNS void AS $$
BEGIN
    DELETE FROM locks WHERE expires_at < NOW();
END;
$$ LANGUAGE plpgsql;

-- Run cleanup on access
CREATE OR REPLACE FUNCTION check_expired_before_select()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM cleanup_expired_locks();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_cleanup_on_access
BEFORE SELECT ON locks
FOR EACH STATEMENT
EXECUTE FUNCTION check_expired_before_select();
```

**Backup Strategy:**

```bash
# Nightly backup
0 2 * * * pg_dump -Fc distributed_locks > /backups/locks-$(date +\%Y\%m\%d).dump

# Restore from backup
pg_restore -d distributed_locks /backups/locks-20240104.dump
```

### Distributed Systems with High Throughput

**Recommended Backend: Redis Cluster**

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = 
        "redis-node-1:6379,redis-node-2:6379,redis-node-3:6379";
});
```

**Docker Compose for Local Testing:**

```yaml
version: '3.8'
services:
  redis-node-1:
    image: redis:latest
    command: redis-server --port 6379 --cluster-enabled yes
    ports:
      - "6379:6379"
  
  redis-node-2:
    image: redis:latest
    command: redis-server --port 6380 --cluster-enabled yes
    ports:
      - "6380:6380"
  
  redis-node-3:
    image: redis:latest
    command: redis-server --port 6381 --cluster-enabled yes
    ports:
      - "6381:6381"

# Initialize cluster (run once):
# redis-cli --cluster create 127.0.0.1:6379 127.0.0.1:6380 127.0.0.1:6381
```

**Redis Sentinel for Failover:**

```yaml
version: '3.8'
services:
  redis-master:
    image: redis:latest
    ports:
      - "6379:6379"
  
  redis-slave-1:
    image: redis:latest
    command: redis-server --slaveof redis-master 6379
    ports:
      - "6380:6379"
  
  sentinel-1:
    image: redis:latest
    command: redis-sentinel /etc/redis/sentinel.conf
    volumes:
      - ./sentinel.conf:/etc/redis/sentinel.conf
    ports:
      - "26379:26379"
```

**sentinel.conf:**
```
port 26379
sentinel monitor mymaster redis-master 6379 2
sentinel down-after-milliseconds mymaster 5000
sentinel failover-timeout mymaster 10000
```

**Connection String with Sentinel:**
```csharp
options.ConnectionString = "sentinel1:26379,sentinel2:26379,serviceName=mymaster";
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10 AS builder
WORKDIR /src

COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/runtime:10
WORKDIR /app
COPY --from=builder /app/build .

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet-health-check.dll || exit 1

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Production docker-compose.yml

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: distributed_locks
      POSTGRES_USER: lockuser
      POSTGRES_PASSWORD: secure-password
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U lockuser"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  app:
    build: .
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      LockBackend: PostgreSQL
      LockConnectionString: "Host=postgres;Database=distributed_locks;Username=lockuser;Password=secure-password;SSL Mode=Disable;"
      RedisConnectionString: "redis:6379"
    ports:
      - "80:80"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped

volumes:
  postgres-data:
  redis-data:
```

## Kubernetes Deployment

### Deployment YAML

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: distributed-lock-app
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: distributed-lock-app
  template:
    metadata:
      labels:
        app: distributed-lock-app
    spec:
      containers:
      - name: app
        image: registry.example.com/distributed-lock-app:1.2.0
        imagePullPolicy: Always
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: LockBackend
          value: "PostgreSQL"
        - name: LockConnectionString
          valueFrom:
            secretKeyRef:
              name: lock-secrets
              key: postgres-connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: distributed-lock-service
spec:
  selector:
    app: distributed-lock-app
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer

---
apiVersion: v1
kind: ConfigMap
metadata:
  name: lock-config
data:
  appsettings.Production.json: |
    {
      "DistributedLock": {
        "DefaultLockDuration": "00:00:30",
        "DefaultAcquisitionTimeout": "00:00:05",
        "EnableAutoRenewal": true,
        "UseFencingTokens": true,
        "EnableMetrics": true
      }
    }
```

### Create Secrets

```bash
kubectl create secret generic lock-secrets \
  --from-literal=postgres-connection-string="Host=postgres-service;Database=locks;..."
```

## Configuration Management

### appsettings.json

```json
{
  "DistributedLock": {
    "BackendType": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=locks;Username=postgres;Password=password;",
    "DefaultLockDuration": "00:00:30",
    "DefaultAcquisitionTimeout": "00:00:05",
    "DefaultRenewalInterval": "00:00:10",
    "DefaultAcquisitionMode": "Blocking",
    "DefaultMaxRetries": 3,
    "DefaultRetryDelayMs": 100,
    "EnableAutoRenewal": true,
    "UseFencingTokens": true,
    "EnableMetrics": true,
    "EnableLogging": true,
    "EnableCaching": true,
    "CacheDurationSeconds": 30,
    "MaxCacheSize": 10000,
    "MaxConcurrentLocks": 1000,
    "WebhookEndpoint": "https://monitoring.example.com/locks",
    "WebhookTimeout": "00:00:05",
    "EnableWebhookRetry": true,
    "MaxWebhookRetries": 3
  }
}
```

### Environment-Specific Configuration

**appsettings.Development.json:**
```json
{
  "DistributedLock": {
    "BackendType": "InMemory",
    "EnableLogging": true
  }
}
```

**appsettings.Production.json:**
```json
{
  "DistributedLock": {
    "BackendType": "PostgreSQL",
    "EnableMetrics": true,
    "CacheDurationSeconds": 60,
    "MaxCacheSize": 50000
  }
}
```

## Monitoring and Observability

### Metrics Collection

```csharp
public class MetricsService : BackgroundService
{
    private readonly ILockService _lockService;
    private readonly ILogger<MetricsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_lockService is LockService concrete)
            {
                var metrics = concrete.GetMetrics();
                
                // Send to monitoring system
                await SendMetricsAsync(new
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveLocks = metrics.CurrentActiveLocks,
                    SuccessRate = metrics.AcquisitionSuccessRate,
                    AvgAcquisitionTime = metrics.AverageAcquisitionTimeMs,
                    TotalAttempts = metrics.TotalAcquisitionAttempts
                });
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendMetricsAsync(object metrics)
    {
        using var client = _httpClientFactory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(metrics),
            Encoding.UTF8,
            "application/json"
        );
        
        await client.PostAsync("https://monitoring.example.com/metrics", content);
    }
}
```

### Health Checks

```csharp
public class LockHealthCheck : IHealthCheck
{
    private readonly ILockService _lockService;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testLockKey = "health-check-lock";
            var testOwnerId = "health-check";

            // Try to acquire a test lock
            var @lock = await _lockService.TryAcquireAsync(testLockKey, testOwnerId);
            
            if (@lock != null)
            {
                await _lockService.ReleaseAsync(testLockKey, testOwnerId);
                return HealthCheckResult.Healthy("Lock service is operational");
            }
            else
            {
                return HealthCheckResult.Degraded("Lock service is responsive but contended");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Lock service is unhealthy", ex);
        }
    }
}

// Register in Startup
services.AddHealthChecks()
    .AddCheck<LockHealthCheck>("distributed-lock");
```

## Troubleshooting

### Database Connection Issues

**PostgreSQL connection refused:**
```bash
# Check PostgreSQL is running
sudo systemctl status postgresql

# Check connection
psql -h localhost -U postgres -d distributed_locks

# Verify pg_hba.conf allows local connections
sudo grep "local" /etc/postgresql/*/main/pg_hba.conf
```

**Redis connection refused:**
```bash
# Check Redis is running
redis-cli ping
# Should respond with PONG

# Check listening ports
netstat -tuln | grep 6379
```

### Performance Under Load

**Enable query logging for PostgreSQL:**
```sql
ALTER DATABASE distributed_locks SET log_statement = 'all';
ALTER DATABASE distributed_locks SET log_min_duration_statement = 0;
```

**Monitor lock contention:**
```csharp
var metrics = lockService.GetMetrics();
Console.WriteLine($"Active locks: {metrics.CurrentActiveLocks}");
Console.WriteLine($"Success rate: {metrics.AcquisitionSuccessRate:P}");
```

## Backup and Recovery

### PostgreSQL Backup Strategy

```bash
#!/bin/bash
# backup-locks.sh

BACKUP_DIR="/backups/locks"
RETENTION_DAYS=30

# Full backup
pg_dump -Fc distributed_locks > "$BACKUP_DIR/locks-$(date +%Y%m%d-%H%M%S).dump"

# Clean old backups
find "$BACKUP_DIR" -name "locks-*.dump" -mtime +$RETENTION_DAYS -delete
```

### Restore from Backup

```bash
pg_restore -d distributed_locks /backups/locks-20240104-020000.dump
```

## Security Considerations

### PostgreSQL Security

```sql
-- Create limited user
CREATE USER lockapp WITH PASSWORD 'strong-password';

-- Grant only necessary permissions
GRANT CONNECT ON DATABASE distributed_locks TO lockapp;
GRANT USAGE ON SCHEMA public TO lockapp;
GRANT SELECT, INSERT, UPDATE, DELETE ON locks TO lockapp;

-- Restrict to application hosts
-- In pg_hba.conf:
# host  distributed_locks  lockapp  10.0.0.0/8  md5
```

### Connection String Security

**Never hardcode in code:**
```csharp
// ❌ Bad
options.ConnectionString = "Host=localhost;Username=user;Password=secret;";

// ✅ Good
options.ConnectionString = configuration["LockConnectionString"];
// Store in environment variables or secrets manager
```

### Use SSL/TLS for Connections

```csharp
// PostgreSQL with SSL
options.ConnectionString = "Host=postgres.example.com;SSL Mode=Require;...";

// Redis with TLS
options.ConnectionString = "redis.example.com:6380,ssl=true,...";
```

## Scaling Considerations

### Vertical Scaling (Single Host)

Increase host resources (CPU, RAM, disk). Works well for:
- SQLite deployments
- Under 10K locks
- < 1K lock operations/sec

### Horizontal Scaling (Multiple Hosts)

Distribute across multiple hosts. Best practices:
- Use PostgreSQL or Redis backend (not SQLite or InMemory)
- Deploy 3+ replicas
- Use load balancer for health-based routing
- Configure connection pooling

**Load balancer configuration:**
```nginx
upstream lock-app {
    least_conn;
    server app1:80;
    server app2:80;
    server app3:80;
    
    server app4:80 backup;
}

server {
    location / {
        proxy_pass http://lock-app;
        proxy_connect_timeout 5s;
        proxy_read_timeout 10s;
    }
}
```

## Performance Tuning

### PostgreSQL Tuning

```ini
# postgresql.conf
shared_buffers = 25% of RAM
effective_cache_size = 75% of RAM
maintenance_work_mem = RAM / 16
work_mem = RAM / 8 / max_connections

# Connection pooling (use PgBouncer)
max_connections = 100  # Pooler handles excess
```

### Redis Tuning

```conf
# redis.conf
maxmemory 2gb
maxmemory-policy allkeys-lru
appendonly yes
appendfsync everysec
```

## Release and Rollback

### Blue-Green Deployment

1. Deploy new version to "green" environment
2. Run smoke tests
3. Switch load balancer from "blue" to "green"
4. Keep "blue" ready for quick rollback

### Canary Deployment

```yaml
# Deploy to 10% of replicas first
replicas: 10
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1
    maxUnavailable: 0
```

### Quick Rollback

```bash
# Revert to previous image
kubectl set image deployment/distributed-lock-app \
  app=registry.example.com/distributed-lock-app:1.1.0
```
