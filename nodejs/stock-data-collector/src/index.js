const express = require('express');
const mongoose = require('mongoose');
const { Kafka } = require('kafkajs');
const Redis = require('ioredis');
const cors = require('cors'); const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3006;
app.use(cors()); app.use(helmet()); app.use(express.json());

mongoose.connect(process.env.MONGODB_URI || 'mongodb://gstp_admin:gstp_secret_2026@localhost:27017/market_data_db?authSource=admin');
const redis = new Redis(process.env.REDIS_URL || 'redis://:gstp_redis_2026@localhost:6379');

const kafka = new Kafka({ clientId: 'stock-data-collector', brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',') });
let producer;

// Simulated stock prices for demo
const STOCKS = {
  AAPL: { base: 178.50, volatility: 0.02 }, GOOGL: { base: 141.80, volatility: 0.015 },
  MSFT: { base: 378.90, volatility: 0.018 }, AMZN: { base: 185.20, volatility: 0.022 },
  TSLA: { base: 245.60, volatility: 0.04 }, NVDA: { base: 875.30, volatility: 0.03 },
  META: { base: 505.40, volatility: 0.025 }, JPM: { base: 198.70, volatility: 0.012 },
  NFLX: { base: 620.00, volatility: 0.028 }, AMD: { base: 178.50, volatility: 0.035 },
};

function simulatePrice(stock) {
  const change = (Math.random() - 0.5) * 2 * stock.volatility * stock.base;
  stock.base = Math.max(1, stock.base + change);
  return {
    symbol: null, // set in loop
    price: parseFloat(stock.base.toFixed(2)),
    change: parseFloat(change.toFixed(2)),
    changePercent: parseFloat(((change / stock.base) * 100).toFixed(2)),
    volume: Math.floor(Math.random() * 1000000) + 100000,
    high: parseFloat((stock.base * 1.01).toFixed(2)),
    low: parseFloat((stock.base * 0.99).toFixed(2)),
    timestamp: new Date()
  };
}

// Publish simulated price updates every 2 seconds
async function collectAndPublish() {
  if (!producer) return;
  for (const [symbol, stock] of Object.entries(STOCKS)) {
    const quote = simulatePrice(stock);
    quote.symbol = symbol;
    try {
      await producer.send({ topic: 'prices.updates', messages: [{ key: symbol, value: JSON.stringify(quote) }] });
      await redis.setex(`quote:${symbol}`, 10, JSON.stringify(quote));
    } catch (err) { /* ignore transient errors */ }
  }
}

app.get('/health', (req, res) => res.json({ service: 'Stock Data Collector', status: 'running' }));
app.get('/api/v1/collector/symbols', (req, res) => res.json({ success: true, data: Object.keys(STOCKS) }));

async function start() {
  try {
    producer = kafka.producer();
    await producer.connect();
    console.log('✅ Kafka producer connected');
    setInterval(collectAndPublish, 2000); // every 2 sec
  } catch (err) { console.error('Kafka error, retrying...'); setTimeout(start, 10000); }
}

app.listen(PORT, () => { console.log(`🚀 Stock Data Collector on port ${PORT}`); start(); });
