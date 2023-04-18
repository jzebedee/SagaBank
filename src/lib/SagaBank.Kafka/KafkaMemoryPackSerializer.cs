using Confluent.Kafka;

namespace SagaBank.Kafka;

public sealed class KafkaMemoryPackSerializer<T> : ISerializer<T>
{
    private static readonly Lazy<KafkaMemoryPackSerializer<T>> _instance = new();
    public static KafkaMemoryPackSerializer<T> Instance => _instance.Value;

    public byte[] Serialize(T data, SerializationContext context)
        => MemoryPack.MemoryPackSerializer.Serialize<T>(data);
}
