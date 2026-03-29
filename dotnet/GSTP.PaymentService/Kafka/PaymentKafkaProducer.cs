using System.Text.Json;
using Confluent.Kafka;

namespace GSTP.PaymentService.Kafka;

public class PaymentKafkaProducer
{
    private readonly IProducer<string, string> _producer;
    public PaymentKafkaProducer(IConfiguration config)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            Acks = Acks.All
        }).Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message)
    {
        await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(message)
        });
    }
}
