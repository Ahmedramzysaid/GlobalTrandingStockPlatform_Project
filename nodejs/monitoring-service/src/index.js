const express = require('express');
const Redis = require('ioredis');
const cors = require('cors'); const helmet = require('helmet');
const http = require('http');

const app = express();
const PORT = process.env.PORT || 3005;
app.use(cors()); app.use(helmet()); app.use(express.json());

const redis = new Redis(process.env.REDIS_URL || 'redis://:gstp_redis_2026@localhost:6379');

// Service registry
const SERVICES = [
  { name: 'API Gateway', url: 'http://api-gateway:5000', port: 5000 },
  { name: 'Auth Service', url: 'http://auth-service:5001/health', port: 5001 },
  { name: 'Order Service', url: 'http://order-service:5002/health', port: 5002 },
  { name: 'Payment Service', url: 'http://payment-service:5003/health', port: 5003 },
  { name: 'Audit Service', url: 'http://audit-service:5004/health', port: 5004 },
  { name: 'Trading Engine', url: 'http://trading-engine:8081/actuator/health', port: 8081 },
  { name: 'Portfolio Service', url: 'http://portfolio-service:8082/api/v1/portfolio/health', port: 8082 },
  { name: 'Risk Management', url: 'http://risk-management:8083/api/v1/risk/health', port: 8083 },
  { name: 'Settlement Service', url: 'http://settlement-service:8084/api/v1/settlement/health', port: 8084 },
  { name: 'Market Data', url: 'http://market-data-service:3001/health', port: 3001 },
  { name: 'Notification', url: 'http://notification-service:3002/health', port: 3002 },
  { name: 'Analytics', url: 'http://analytics-service:3003/health', port: 3003 },
  { name: 'Logging', url: 'http://logging-service:3004/health', port: 3004 },
  { name: 'User Service', url: 'http://user-service:8001/api/v1/users/health', port: 8001 },
  { name: 'Admin Service', url: 'http://admin-service:8002/api/v1/admin/health', port: 8002 },
];

async function checkServiceHealth(service) {
  return new Promise((resolve) => {
    const url = new URL(service.url);
    const req = http.get({ hostname: url.hostname, port: url.port, path: url.pathname, timeout: 3000 }, (res) => {
      resolve({ ...service, status: res.statusCode === 200 ? 'healthy' : 'degraded', statusCode: res.statusCode, latency: Date.now() - start });
    });
    const start = Date.now();
    req.on('error', () => resolve({ ...service, status: 'down', latency: Date.now() - start }));
    req.on('timeout', () => { req.destroy(); resolve({ ...service, status: 'timeout', latency: 3000 }); });
  });
}

// Health check all services every 30 seconds
let lastHealthCheck = [];
async function runHealthChecks() {
  const results = await Promise.all(SERVICES.map(checkServiceHealth));
  lastHealthCheck = results;
  await redis.setex('monitoring:health', 60, JSON.stringify(results));
  const downServices = results.filter(r => r.status !== 'healthy');
  if (downServices.length > 0) console.warn(`⚠️ ${downServices.length} services unhealthy:`, downServices.map(s => s.name));
}
setInterval(runHealthChecks, 30000);

app.get('/health', (req, res) => res.json({ service: 'Monitoring Service', status: 'running' }));

app.get('/api/v1/monitoring/status', async (req, res) => {
  if (lastHealthCheck.length === 0) await runHealthChecks();
  const healthy = lastHealthCheck.filter(s => s.status === 'healthy').length;
  res.json({
    success: true,
    data: {
      totalServices: lastHealthCheck.length, healthy, unhealthy: lastHealthCheck.length - healthy,
      overallStatus: healthy === lastHealthCheck.length ? 'ALL_HEALTHY' : 'DEGRADED',
      services: lastHealthCheck,
      checkedAt: new Date()
    }
  });
});

app.listen(PORT, () => {
  console.log(`🚀 Monitoring Service on port ${PORT}`);
  setTimeout(runHealthChecks, 5000);
});
