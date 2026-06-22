using System.Text;
using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class RabbitMqBackgroundJobQueue
    : IBackgroundJobQueue,
      IDisposable
{
    private readonly IConnection _connection;

    private readonly IModel _channel;

    public RabbitMqBackgroundJobQueue(
        IConfiguration configuration)
    {
        var factory =
            new ConnectionFactory
            {
                HostName =
                    configuration["RabbitMq:Host"]
                    ?? "rabbitmq"
            };
        Console.WriteLine(
            $"RabbitMQ Host: {factory.HostName}");
        _connection =
            factory.CreateConnection();

        _channel =
            _connection.CreateModel();

        _channel.QueueDeclare(
            queue: "workflow",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(
            0,
            1,
            false);
    }

    public Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var json =
            JsonSerializer.Serialize(job);

        var body =
            Encoding.UTF8.GetBytes(json);

        var properties =
            _channel.CreateBasicProperties();

        properties.Persistent =
            true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: job.Queue,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public void Subscribe(
        string queue,
        Func<BackgroundJob, Task> handler)
    {
        var consumer =
            new EventingBasicConsumer(
                _channel);

        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                var json =
                    Encoding.UTF8.GetString(
                        eventArgs.Body.ToArray());

                var job =
                    JsonSerializer.Deserialize<
                        BackgroundJob>(json);

                if (job is null)
                {
                    _channel.BasicNack(
                        eventArgs.DeliveryTag,
                        false,
                        false);

                    return;
                }

                if (job.CreatedAt != default &&
                    DateTimeOffset.UtcNow - job.CreatedAt > TimeSpan.FromMinutes(10))
                {
                    _channel.BasicAck(
                        eventArgs.DeliveryTag,
                        false);

                    return;
                }

                await handler(job);

                _channel.BasicAck(
                    eventArgs.DeliveryTag,
                    false);
            }
            catch
            {
                _channel.BasicNack(
                    eventArgs.DeliveryTag,
                    false,
                    true);
            }
        };

        _channel.BasicConsume(
            queue: queue,
            autoAck: false,
            consumer: consumer);
    }

    public void Dispose()
    {
        _channel.Dispose();

        _connection.Dispose();
    }
}