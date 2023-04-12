using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using SagaBank.Kafka;
using SagaBank.Kafka.Extensions;

var builder = WebApplication.CreateBuilder(args);

var kafkaSection = builder.Configuration.GetSection("Kafka");
builder.Services.AddKafkaProducer(configure => kafkaSection.GetSection(nameof(ProducerConfig)).Bind(configure));
builder.Services.AddKafkaConsumer(configure => kafkaSection.GetSection(nameof(ConsumerConfig)).Bind(configure));

var app = builder.Build();

app.MapGet("/", () => $"Hello World! It's {DateTimeOffset.Now}");
app.MapGet("/produce", ([FromServices] Producer producer) => producer.ProduceDummyData(kafkaSection["Topic"]));
app.MapGet("/consume", ([FromServices] Consumer consumer) => consumer.ConsumeDummyData(kafkaSection["Topic"]));

app.Run();
