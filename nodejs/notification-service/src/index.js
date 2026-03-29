const express = require('express');
const mongoose = require('mongoose');
const { Kafka } = require('kafkajs');
const Redis = require('ioredis');
const WebSocket = require('ws');
const cors = require('cors');
const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3002;
app.use(cors()); app.use(helmet()); app.use(express.json());

// MongoDB
mongoose.connect(process.env.MONGODB_URI || 'mongodb://gstp_admin:gstp_secret_2026@localhost:27017/notifications_db?authSource=admin')
  .then(() => console.log('✅ MongoDB connected'));

const redis = new Redis(process.env.REDIS_URL || 'redis://:gstp_redis_2026@localhost:6379');

// Schema
const NotificationSchema = new mongoose.Schema({
  userId: { type: String, required: true, index: true },
  type: { type: String, enum: ['ORDER_FILLED', 'ORDER_CANCELLED', 'DEPOSIT', 'WITHDRAWAL', 'PRICE_ALERT', 'RISK_ALERT', 'SYSTEM'], required: true },
  title: { type: String, required: true },
  message: { type: String, required: true },
  read: { type: Boolean, default: false },
  data: mongoose.Schema.Types.Mixed,
  createdAt: { type: Date, default: Date.now }
});
const Notification = mongoose.model('Notification', NotificationSchema);

// WebSocket for real-time push
const server = app.listen(PORT, () => console.log(`🚀 Notification Service running on port ${PORT}`));
const wss = new WebSocket.Server({ server, path: '/ws' });
const clients = new Map(); // userId -> ws[]

wss.on('connection', (ws, req) => {
  const userId = new URL(req.url, `http://localhost:${PORT}`).searchParams.get('userId');
  if (userId) {
    if (!clients.has(userId)) clients.set(userId, []);
    clients.get(userId).push(ws);
    ws.on('close', () => {
      const arr = clients.get(userId)?.filter(c => c !== ws);
      if (arr?.length) clients.set(userId, arr); else clients.delete(userId);
    });
  }
});

function pushToUser(userId, notification) {
  const sockets = clients.get(userId);
  if (sockets) {
    const msg = JSON.stringify(notification);
    sockets.forEach(ws => { if (ws.readyState === WebSocket.OPEN) ws.send(msg); });
  }
}

// REST API
app.get('/health', (req, res) => res.json({ service: 'Notification Service', status: 'running' }));

app.get('/api/v1/notifications/:userId', async (req, res) => {
  const { page = 1, limit = 20 } = req.query;
  const notifications = await Notification.find({ userId: req.params.userId })
    .sort({ createdAt: -1 }).skip((page - 1) * limit).limit(parseInt(limit));
  const total = await Notification.countDocuments({ userId: req.params.userId });
  const unread = await Notification.countDocuments({ userId: req.params.userId, read: false });
  res.json({ success: true, data: notifications, unread, pagination: { page: parseInt(page), limit: parseInt(limit), total } });
});

app.put('/api/v1/notifications/:id/read', async (req, res) => {
  await Notification.findByIdAndUpdate(req.params.id, { read: true });
  res.json({ success: true, message: 'Marked as read' });
});

// Kafka Consumer
async function startKafka() {
  try {
    const kafka = new Kafka({ clientId: 'notification-service', brokers: (process.env.KAFKA_BROKERS || 'localhost:9092').split(',') });
    const consumer = kafka.consumer({ groupId: 'notification-service' });
    await consumer.connect();
    await consumer.subscribe({ topics: ['notifications.send', 'trades.executed', 'payments.completed', 'risk.alerts'], fromBeginning: false });
    await consumer.run({
      eachMessage: async ({ topic, message }) => {
        try {
          const data = JSON.parse(message.value.toString());
          let notification;
          switch (topic) {
            case 'trades.executed':
              notification = { userId: data.BuyerUserId, type: 'ORDER_FILLED', title: 'Trade Executed', message: `Your order for ${data.Quantity} ${data.Symbol} was filled at $${data.Price}`, data };
              await Notification.create(notification); pushToUser(data.BuyerUserId, notification);
              notification = { ...notification, userId: data.SellerUserId, message: `Your sell order for ${data.Quantity} ${data.Symbol} was filled at $${data.Price}` };
              await Notification.create(notification); pushToUser(data.SellerUserId, notification);
              break;
            case 'payments.completed':
              notification = { userId: data.UserId, type: data.EventType === 'DepositCompleted' ? 'DEPOSIT' : 'WITHDRAWAL', title: data.EventType, message: `Transaction of $${data.Amount} processed`, data };
              await Notification.create(notification); pushToUser(data.UserId, notification);
              break;
            case 'risk.alerts':
              notification = { userId: data.userId, type: 'RISK_ALERT', title: `Risk Alert: ${data.alertType}`, message: data.message, data };
              await Notification.create(notification); pushToUser(data.userId, notification);
              break;
          }
        } catch (err) { console.error('Notification processing error:', err.message); }
      }
    });
    console.log('✅ Kafka consumer started');
  } catch (err) { console.error('Kafka error, retrying...'); setTimeout(startKafka, 10000); }
}
startKafka();
