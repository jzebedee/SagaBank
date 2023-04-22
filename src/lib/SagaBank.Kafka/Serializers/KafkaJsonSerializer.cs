using Confluent.Kafka;

namespace SagaBank.Kafka;

public sealed class KafkaJsonSerializer<T> : ISerializer<T>
{
    private static readonly Lazy<KafkaJsonSerializer<T>> _instance = new();
    public static KafkaJsonSerializer<T> Instance => _instance.Value;

    public byte[] Serialize(T data, SerializationContext context)
    {
        using var ms = new MemoryStream();
        System.Text.Json.JsonSerializer.Serialize<T>(ms, data);
        return ms.ToArray();
    }
}
