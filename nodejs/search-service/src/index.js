const express = require('express');
const { Client } = require('@elastic/elasticsearch');
const { Kafka } = require('kafkajs');
const cors = require('cors'); const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3008;
app.use(cors()); app.use(helmet()); app.use(express.json());

const esClient = new Client({ node: process.env.ELASTICSEARCH_URL || 'http://localhost:9200' });

// Ensure index exists
async function ensureIndex() {
  try {
    const exists = await esClient.indices.exists({ index: 'stocks' });
    if (!exists) {
      await esClient.indices.create({
        index: 'stocks',
        body: {
          mappings: {
            properties: {
              symbol: { type: 'keyword' },
              companyName: { type: 'text', analyzer: 'standard' },
              sector: { type: 'keyword' },
              industry: { type: 'keyword' },
              price: { type: 'float' },
              marketCap: { type: 'long' },
              updatedAt: { type: 'date' }
            }
          }
        }
      });
      // Seed sample data
      const stocks = [
        { symbol: 'AAPL', companyName: 'Apple Inc.', sector: 'Technology', industry: 'Consumer Electronics', price: 178.50, marketCap: 2800000000000 },
        { symbol: 'GOOGL', companyName: 'Alphabet Inc.', sector: 'Technology', industry: 'Internet Services', price: 141.80, marketCap: 1780000000000 },
        { symbol: 'MSFT', companyName: 'Microsoft Corporation', sector: 'Technology', industry: 'Software', price: 378.90, marketCap: 2810000000000 },
        { symbol: 'AMZN', companyName: 'Amazon.com Inc.', sector: 'Consumer Cyclical', industry: 'E-Commerce', price: 185.20, marketCap: 1920000000000 },
        { symbol: 'TSLA', companyName: 'Tesla Inc.', sector: 'Automotive', industry: 'Electric Vehicles', price: 245.60, marketCap: 780000000000 },
        { symbol: 'NVDA', companyName: 'NVIDIA Corporation', sector: 'Technology', industry: 'Semiconductors', price: 875.30, marketCap: 2160000000000 },
        { symbol: 'META', companyName: 'Meta Platforms Inc.', sector: 'Technology', industry: 'Social Media', price: 505.40, marketCap: 1300000000000 },
        { symbol: 'JPM', companyName: 'JPMorgan Chase & Co.', sector: 'Financial', industry: 'Banking', price: 198.70, marketCap: 570000000000 },
        { symbol: 'NFLX', companyName: 'Netflix Inc.', sector: 'Technology', industry: 'Streaming', price: 620.00, marketCap: 270000000000 },
        { symbol: 'AMD', companyName: 'Advanced Micro Devices', sector: 'Technology', industry: 'Semiconductors', price: 178.50, marketCap: 290000000000 },
      ];
      for (const stock of stocks) {
        await esClient.index({ index: 'stocks', id: stock.symbol, body: { ...stock, updatedAt: new Date() } });
      }
      console.log('✅ Elasticsearch index created and seeded');
    }
  } catch (err) {
    console.error('ES index error:', err.message);
    setTimeout(ensureIndex, 10000);
  }
}

app.get('/health', (req, res) => res.json({ service: 'Search Service', status: 'running' }));

app.get('/api/v1/search/stocks', async (req, res) => {
  try {
    const { q, sector, industry, minPrice, maxPrice, page = 1, limit = 20 } = req.query;
    const must = [];
    if (q) must.push({ multi_match: { query: q, fields: ['symbol^3', 'companyName^2', 'sector', 'industry'], fuzziness: 'AUTO' } });
    if (sector) must.push({ match: { sector } });
    if (industry) must.push({ match: { industry } });
    if (minPrice || maxPrice) must.push({ range: { price: { ...(minPrice && { gte: parseFloat(minPrice) }), ...(maxPrice && { lte: parseFloat(maxPrice) }) } } });

    const result = await esClient.search({
      index: 'stocks',
      body: { query: must.length > 0 ? { bool: { must } } : { match_all: {} }, from: (page - 1) * limit, size: parseInt(limit), sort: [{ _score: 'desc' }, { marketCap: 'desc' }] }
    });

    res.json({ success: true, data: result.hits.hits.map(h => ({ ...h._source, score: h._score })), total: result.hits.total.value });
  } catch (err) {
    res.status(500).json({ success: false, error: err.message });
  }
});

app.get('/api/v1/search/autocomplete', async (req, res) => {
  try {
    const { q } = req.query;
    if (!q) return res.json({ success: true, data: [] });
    const result = await esClient.search({
      index: 'stocks',
      body: { query: { bool: { should: [{ prefix: { symbol: { value: q.toUpperCase(), boost: 3 } } }, { match_phrase_prefix: { companyName: { query: q, boost: 1 } } }] } }, size: 10 }
    });
    res.json({ success: true, data: result.hits.hits.map(h => ({ symbol: h._source.symbol, companyName: h._source.companyName })) });
  } catch (err) { res.status(500).json({ success: false, error: err.message }); }
});

app.listen(PORT, () => { console.log(`🚀 Search Service on port ${PORT}`); ensureIndex(); });
