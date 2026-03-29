const express = require('express');
const mongoose = require('mongoose');
const { Kafka } = require('kafkajs');
const cors = require('cors'); const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3004;
app.use(cors()); app.use(helmet()); app.use(express.json());

mongoose.connect(process.env.MONGODB_URI || 'mongodb://gstp_admin:gstp_secret_2026@localhost:27017/logs_db?authSource=admin');

const LogSchema = new mongoose.Schema({
  service: { type: String, required: true, index: true },
  level: { type: String, enum: ['DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL'], default: 'INFO', index: true },
  message: String, correlationId: { type: String, index: true },
  metadata: mongoose.Schema.Types.Mixed,
  timestamp: { type: Date, default: Date.now, index: { expireAfterSeconds: 2592000 } } // TTL 30 days
});
const Log = mongoose.model('ServiceLog', LogSchema);

app.get('/health', (req, res) => res.json({ service: 'Logging Service', status: 'running' }));

app.get('/api/v1/logs', async (req, res) => {
  const { service, level, correlationId, page = 1, limit = 50 } = req.query;
  const filter = {};
  if (service) filter.service = service;
  if (level) filter.level = level;
  if (correlationId) filter.correlationId = correlationId;
  const logs = await Log.find(filter).sort({ timestamp: -1 }).skip((page - 1) * limit).limit(parseInt(limit));
  res.json({ success: true, data: logs });
});

app.post('/api/v1/logs', async (req, res) => {
  const log = await Log.create(req.body);
  res.status(201).json({ success: true, data: log });
});

// Kafka consumer
async function startKafka() {
  try {
    const kafka = new Kafka({ clientId: 'logging-service', brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',') });
    const consumer = kafka.consumer({ groupId: 'logging-service' });
    await consumer.connect();
    await consumer.subscribe({ topic: 'logs.all', fromBeginning: false });
    await consumer.run({
      eachMessage: async ({ message }) => {
        const data = JSON.parse(message.value.toString());
        await Log.create(data);
      }
    });
  } catch (err) { setTimeout(startKafka, 10000); }
}

app.listen(PORT, () => { console.log(`🚀 Logging Service on port ${PORT}`); startKafka(); });
