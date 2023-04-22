using Confluent.Kafka;

namespace SagaBank.Kafka.Serializers;

public sealed class KafkaMemoryPackDeserializer<T> : IDeserializer<T>
{
    private static readonly Lazy<KafkaMemoryPackDeserializer<T>> _instance = new();
    public static KafkaMemoryPackDeserializer<T> Instance => _instance.Value;

    public T? Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        => MemoryPack.MemoryPackSerializer.Deserialize<T>(data);
}