const express = require('express');
const mongoose = require('mongoose');
const { Kafka } = require('kafkajs');
const cors = require('cors');
const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3003;
app.use(cors()); app.use(helmet()); app.use(express.json());

mongoose.connect(process.env.MONGODB_URI || 'mongodb://gstp_admin:gstp_secret_2026@localhost:27017/analytics_db?authSource=admin')
  .then(() => console.log('✅ MongoDB connected'));

const TradingMetricSchema = new mongoose.Schema({
  symbol: { type: String, index: true }, tradeCount: Number, totalVolume: Number,
  totalValue: Number, avgPrice: Number, highPrice: Number, lowPrice: Number,
  timestamp: { type: Date, default: Date.now, index: true }
});
const PlatformMetricSchema = new mongoose.Schema({
  metricType: { type: String, index: true }, value: Number,
  metadata: mongoose.Schema.Types.Mixed, timestamp: { type: Date, default: Date.now }
});
const TradingMetric = mongoose.model('TradingMetric', TradingMetricSchema);
const PlatformMetric = mongoose.model('PlatformMetric', PlatformMetricSchema);

app.get('/health', (req, res) => res.json({ service: 'Analytics Service', status: 'running' }));

app.get('/api/v1/analytics/trading/:symbol', async (req, res) => {
  const metrics = await TradingMetric.find({ symbol: req.params.symbol.toUpperCase() })
    .sort({ timestamp: -1 }).limit(100);
  res.json({ success: true, data: metrics });
});

app.get('/api/v1/analytics/platform', async (req, res) => {
  const metrics = await PlatformMetric.find().sort({ timestamp: -1 }).limit(50);
  res.json({ success: true, data: metrics });
});

app.get('/api/v1/analytics/summary', async (req, res) => {
  const totalTrades = await TradingMetric.aggregate([{ $group: { _id: null, total: { $sum: '$tradeCount' }, volume: { $sum: '$totalVolume' } } }]);
  res.json({ success: true, data: totalTrades[0] || { total: 0, volume: 0 } });
});

// Kafka consumer — aggregate trade data
async function startKafka() {
  try {
    const kafka = new Kafka({ clientId: 'analytics-service', brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',') });
    const consumer = kafka.consumer({ groupId: 'analytics-service' });
    await consumer.connect();
    await consumer.subscribe({ topics: ['trades.executed', 'orders.created'], fromBeginning: false });
    await consumer.run({
      eachMessage: async ({ topic, message }) => {
        const data = JSON.parse(message.value.toString());
        if (topic === 'trades.executed') {
          await TradingMetric.create({ symbol: data.Symbol, tradeCount: 1, totalVolume: parseFloat(data.Quantity), totalValue: parseFloat(data.Quantity) * parseFloat(data.Price), avgPrice: parseFloat(data.Price), highPrice: parseFloat(data.Price), lowPrice: parseFloat(data.Price) });
        }
        await PlatformMetric.create({ metricType: topic, value: 1, metadata: { symbol: data.Symbol || data.Data?.Symbol } });
      }
    });
    console.log('✅ Kafka consumer started');
  } catch (err) { setTimeout(startKafka, 10000); }
}

app.listen(PORT, () => { console.log(`🚀 Analytics Service on port ${PORT}`); startKafka(); });
