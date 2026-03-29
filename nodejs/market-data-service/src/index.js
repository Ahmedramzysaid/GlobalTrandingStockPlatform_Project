const express = require('express');
const mongoose = require('mongoose');
const cors = require('cors');
const helmet = require('helmet');
const morgan = require('morgan');
const { Kafka } = require('kafkajs');
const Redis = require('ioredis');
const WebSocket = require('ws');

const app = express();
const PORT = process.env.PORT || 3001;

// Middleware
app.use(cors());
app.use(helmet());
app.use(morgan('combined'));
app.use(express.json());

// MongoDB Connection
mongoose.connect(process.env.MONGODB_URI || 'mongodb://gstp_admin:gstp_secret_2026@localhost:27017/market_data_db?authSource=admin')
  .then(() => console.log('✅ MongoDB connected'))
  .catch(err => console.error('❌ MongoDB error:', err));

// Redis Connection
const redis = new Redis(process.env.REDIS_URL || 'redis://:gstp_redis_2026@localhost:6379');
redis.on('connect', () => console.log('✅ Redis connected'));

// MongoDB Schemas
const StockQuoteSchema = new mongoose.Schema({
  symbol: { type: String, required: true, unique: true, index: true },
  companyName: String,
  price: { type: Number, required: true },
  change: Number,
  changePercent: Number,
  volume: Number,
  high: Number,
  low: Number,
  open: Number,
  previousClose: Number,
  marketCap: Number,
  updatedAt: { type: Date, default: Date.now }
});

const CandleSchema = new mongoose.Schema({
  symbol: { type: String, required: true, index: true },
  interval: { type: String, enum: ['1m', '5m', '15m', '1h', '1d'], required: true },
  open: Number, high: Number, low: Number, close: Number, volume: Number,
  timestamp: { type: Date, required: true }
});
CandleSchema.index({ symbol: 1, interval: 1, timestamp: -1 });

const StockQuote = mongoose.model('StockQuote', StockQuoteSchema);
const Candle = mongoose.model('Candle', CandleSchema);

// --- REST API Routes ---
app.get('/health', (req, res) => res.json({ service: 'Market Data Service', status: 'running' }));
app.get('/', (req, res) => res.json({ service: 'GSTP Market Data Service', status: 'running', version: '1.0.0' }));

app.get('/api/v1/market/quotes', async (req, res) => {
  try {
    const symbols = req.query.symbols?.split(',') || [];
    let quotes;
    if (symbols.length > 0) {
      quotes = await StockQuote.find({ symbol: { $in: symbols.map(s => s.toUpperCase()) } });
    } else {
      quotes = await StockQuote.find().limit(50).sort({ symbol: 1 });
    }
    res.json({ success: true, data: quotes });
  } catch (err) {
    res.status(500).json({ success: false, error: err.message });
  }
});

app.get('/api/v1/market/quotes/:symbol', async (req, res) => {
  try {
    const symbol = req.params.symbol.toUpperCase();
    // Try cache first
    const cached = await redis.get(`quote:${symbol}`);
    if (cached) return res.json({ success: true, data: JSON.parse(cached), source: 'cache' });

    const quote = await StockQuote.findOne({ symbol });
    if (!quote) return res.status(404).json({ success: false, error: 'Symbol not found' });

    await redis.setex(`quote:${symbol}`, 5, JSON.stringify(quote));
    res.json({ success: true, data: quote });
  } catch (err) {
    res.status(500).json({ success: false, error: err.message });
  }
});

app.get('/api/v1/market/candles/:symbol', async (req, res) => {
  try {
    const { interval = '1d', limit = 100 } = req.query;
    const candles = await Candle.find({ symbol: req.params.symbol.toUpperCase(), interval })
      .sort({ timestamp: -1 }).limit(parseInt(limit));
    res.json({ success: true, data: candles });
  } catch (err) {
    res.status(500).json({ success: false, error: err.message });
  }
});

// --- Kafka Consumer (price updates) ---
const kafka = new Kafka({
  clientId: 'market-data-service',
  brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',')
});

async function startKafkaConsumer() {
  try {
    const consumer = kafka.consumer({ groupId: 'market-data-service' });
    await consumer.connect();
    await consumer.subscribe({ topic: 'prices.updates', fromBeginning: false });
    await consumer.run({
      eachMessage: async ({ message }) => {
        try {
          const priceUpdate = JSON.parse(message.value.toString());
          await StockQuote.findOneAndUpdate(
            { symbol: priceUpdate.symbol },
            { ...priceUpdate, updatedAt: new Date() },
            { upsert: true }
          );
          await redis.setex(`quote:${priceUpdate.symbol}`, 5, JSON.stringify(priceUpdate));
        } catch (err) {
          console.error('Price update error:', err.message);
        }
      }
    });
    console.log('✅ Kafka consumer started');
  } catch (err) {
    console.error('Kafka connection failed, retrying in 10s...', err.message);
    setTimeout(startKafkaConsumer, 10000);
  }
}

// --- Seed sample data ---
async function seedData() {
  const count = await StockQuote.countDocuments();
  if (count === 0) {
    const stocks = [
      { symbol: 'AAPL', companyName: 'Apple Inc.', price: 178.50, change: 2.30, changePercent: 1.3, volume: 52341000, high: 179.80, low: 176.20, open: 177.00, previousClose: 176.20, marketCap: 2800000000000 },
      { symbol: 'GOOGL', companyName: 'Alphabet Inc.', price: 141.80, change: -0.50, changePercent: -0.35, volume: 23100000, high: 143.00, low: 140.90, open: 142.10, previousClose: 142.30, marketCap: 1780000000000 },
      { symbol: 'MSFT', companyName: 'Microsoft Corp.', price: 378.90, change: 4.10, changePercent: 1.09, volume: 19500000, high: 380.00, low: 375.50, open: 376.00, previousClose: 374.80, marketCap: 2810000000000 },
      { symbol: 'AMZN', companyName: 'Amazon.com Inc.', price: 185.20, change: 1.80, changePercent: 0.98, volume: 31200000, high: 186.50, low: 183.40, open: 184.00, previousClose: 183.40, marketCap: 1920000000000 },
      { symbol: 'TSLA', companyName: 'Tesla Inc.', price: 245.60, change: -3.40, changePercent: -1.37, volume: 98700000, high: 250.00, low: 243.10, open: 249.00, previousClose: 249.00, marketCap: 780000000000 },
      { symbol: 'NVDA', companyName: 'NVIDIA Corp.', price: 875.30, change: 12.50, changePercent: 1.45, volume: 42000000, high: 880.00, low: 860.00, open: 865.00, previousClose: 862.80, marketCap: 2160000000000 },
      { symbol: 'META', companyName: 'Meta Platforms Inc.', price: 505.40, change: 6.20, changePercent: 1.24, volume: 15600000, high: 508.00, low: 499.00, open: 501.00, previousClose: 499.20, marketCap: 1300000000000 },
      { symbol: 'JPM', companyName: 'JPMorgan Chase', price: 198.70, change: 1.10, changePercent: 0.56, volume: 8900000, high: 200.00, low: 197.50, open: 198.00, previousClose: 197.60, marketCap: 570000000000 },
    ];
    await StockQuote.insertMany(stocks);
    console.log('✅ Sample stock data seeded');
  }
}

// Start
const server = app.listen(PORT, async () => {
  console.log(`🚀 Market Data Service running on port ${PORT}`);
  await seedData();
  startKafkaConsumer();
});
