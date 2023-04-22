﻿using Confluent.Kafka;

namespace SagaBank.Kafka.Serializers;

public sealed class KafkaJsonDeserializer<T> : IDeserializer<T>
{
    private static readonly Lazy<KafkaJsonDeserializer<T>> _instance = new();
    public static KafkaJsonDeserializer<T> Instance => _instance.Value;

    public T? Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        => System.Text.Json.JsonSerializer.Deserialize<T>(data);
}