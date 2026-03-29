const express = require('express');
const { Kafka } = require('kafkajs');
const Redis = require('ioredis');
const WebSocket = require('ws');
const cors = require('cors'); const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3007;
app.use(cors()); app.use(helmet()); app.use(express.json());

const redis = new Redis(process.env.REDIS_URL || 'redis://:gstp_redis_2026@localhost:6379');

const server = app.listen(PORT, () => console.log(`🚀 Price Streaming Service on port ${PORT}`));
const wss = new WebSocket.Server({ server, path: '/ws/prices' });

// Track symbol subscriptions
const subscriptions = new Map(); // symbol -> Set<ws>

wss.on('connection', (ws) => {
  let subscribedSymbols = new Set();

  ws.on('message', (msg) => {
    try {
      const data = JSON.parse(msg);
      if (data.action === 'subscribe' && data.symbols) {
        data.symbols.forEach(sym => {
          const s = sym.toUpperCase();
          subscribedSymbols.add(s);
          if (!subscriptions.has(s)) subscriptions.set(s, new Set());
          subscriptions.get(s).add(ws);
        });
        ws.send(JSON.stringify({ type: 'subscribed', symbols: data.symbols }));
      }
      if (data.action === 'unsubscribe' && data.symbols) {
        data.symbols.forEach(sym => {
          const s = sym.toUpperCase();
          subscribedSymbols.delete(s);
          subscriptions.get(s)?.delete(ws);
        });
      }
    } catch (e) { /* ignore bad messages */ }
  });

  ws.on('close', () => {
    subscribedSymbols.forEach(sym => subscriptions.get(sym)?.delete(ws));
  });
});

function broadcastPrice(symbol, priceData) {
  const clients = subscriptions.get(symbol);
  if (!clients) return;
  const msg = JSON.stringify({ type: 'price_update', ...priceData });
  clients.forEach(ws => { if (ws.readyState === WebSocket.OPEN) ws.send(msg); });
}

// Kafka consumer for price updates
async function startKafka() {
  try {
    const kafka = new Kafka({ clientId: 'price-streaming', brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',') });
    const consumer = kafka.consumer({ groupId: 'price-streaming-service' });
    await consumer.connect();
    await consumer.subscribe({ topic: 'prices.updates', fromBeginning: false });
    await consumer.run({
      eachMessage: async ({ message }) => {
        const data = JSON.parse(message.value.toString());
        broadcastPrice(data.symbol, data);
        await redis.setex(`stream:${data.symbol}`, 10, JSON.stringify(data));
      }
    });
    console.log('✅ Kafka consumer started');
  } catch (err) { setTimeout(startKafka, 10000); }
}

app.get('/health', (req, res) => res.json({ service: 'Price Streaming Service', status: 'running' }));
app.get('/api/v1/streaming/stats', (req, res) => {
  const stats = {};
  subscriptions.forEach((clients, symbol) => { stats[symbol] = clients.size; });
  res.json({ success: true, data: { connectedClients: wss.clients.size, subscriptions: stats } });
});

startKafka();
