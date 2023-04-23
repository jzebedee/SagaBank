﻿using Confluent.Kafka;
using System.Text.Json;

namespace SagaBank.Kafka;

public sealed class KafkaJsonSerializer<T> : ISerializer<T>
{
    private static readonly Lazy<KafkaJsonSerializer<T>> _instance = new();
    public static KafkaJsonSerializer<T> Instance => _instance.Value;

    private static JsonSerializerOptions KeyOptions = new()
    {
        IncludeFields = true,
    };

    public byte[] Serialize(T data, SerializationContext context)
    {
        using var ms = new MemoryStream();
        JsonSerializer.Serialize<T>(ms, data, context is { Component: MessageComponentType.Key } ? KeyOptions : default);
        return ms.ToArray();
    }
}
