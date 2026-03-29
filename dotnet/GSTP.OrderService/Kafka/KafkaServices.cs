using System.Text.Json;
using Confluent.Kafka;

namespace GSTP.OrderService.Kafka;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message)
    {
        var json = JsonSerializer.Serialize(message);
        var kafkaMessage = new Message<string, string> { Key = key, Value = json };
        var result = await _producer.ProduceAsync(topic, kafkaMessage);
        _logger.LogInformation("Produced to {Topic} [Partition: {Partition}, Offset: {Offset}]",
            topic, result.Partition.Value, result.Offset.Value);
    }
}

public class KafkaConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IServiceProvider sp, IConfiguration config, ILogger<KafkaConsumerService> logger)
    {
        _serviceProvider = sp;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken); // Wait for Kafka to be ready
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = "order-service",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe("trades.executed");

        _logger.LogInformation("Order Service Kafka consumer started, subscribing to trades.executed");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                _logger.LogInformation("Received trade event: {Value}", result.Message.Value);

                // Update order status based on trade execution
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.OrderDbContext>();
                var tradeEvent = JsonSerializer.Deserialize<TradeExecutedEvent>(result.Message.Value);

                if (tradeEvent != null)
                {
                    var buyOrder = await db.Orders.FindAsync(tradeEvent.BuyOrderId);
                    var sellOrder = await db.Orders.FindAsync(tradeEvent.SellOrderId);

                    if (buyOrder != null)
                    {
                        buyOrder.FilledQuantity += tradeEvent.Quantity;
                        buyOrder.Status = buyOrder.FilledQuantity >= buyOrder.Quantity ? "FILLED" : "PARTIAL_FILL";
                        buyOrder.UpdatedAt = DateTime.UtcNow;
                        db.OrderFills.Add(new Models.OrderFill
                        {
                            OrderId = buyOrder.Id, TradeId = tradeEvent.TradeId,
                            FillPrice = tradeEvent.Price, FillQuantity = tradeEvent.Quantity
                        });
                    }
                    if (sellOrder != null)
                    {
                        sellOrder.FilledQuantity += tradeEvent.Quantity;
                        sellOrder.Status = sellOrder.FilledQuantity >= sellOrder.Quantity ? "FILLED" : "PARTIAL_FILL";
                        sellOrder.UpdatedAt = DateTime.UtcNow;
                        db.OrderFills.Add(new Models.OrderFill
                        {
                            OrderId = sellOrder.Id, TradeId = tradeEvent.TradeId,
                            FillPrice = tradeEvent.Price, FillQuantity = tradeEvent.Quantity
                        });
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error consuming trade event"); }
        }
    }
}

public class TradeExecutedEvent
{
    public Guid TradeId { get; set; }
    public Guid BuyOrderId { get; set; }
    public Guid SellOrderId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public Guid BuyerUserId { get; set; }
    public Guid SellerUserId { get; set; }
}
