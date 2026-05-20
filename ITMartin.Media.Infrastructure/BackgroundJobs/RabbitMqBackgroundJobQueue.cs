using System.Text;
using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class RabbitMqBackgroundJobQueue
    : IBackgroundJobQueue,
      IDisposable
{
    private readonly IConnection _connection;

    private readonly IModel _channel;

    public RabbitMqBackgroundJobQueue()
    {
        var factory =
            new ConnectionFactory
            {
                HostName = "localhost"
            };
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
    }

    public Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var json =
            JsonSerializer.Serialize(job);

        var body =
            Encoding.UTF8.GetBytes(json);

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: job.Queue,
            basicProperties: null,
            body: body);

        return Task.CompletedTask;
    }

    public async Task<BackgroundJob?> DequeueAsync(
        string queue,
        CancellationToken cancellationToken)
    {
        var consumer =
            new EventingBasicConsumer(
                _channel);

        var completionSource =
            new TaskCompletionSource<BackgroundJob?>();

        consumer.Received += (_, eventArgs) =>
        {
            var json =
                Encoding.UTF8.GetString(
                    eventArgs.Body.ToArray());

            var job =
                JsonSerializer.Deserialize<BackgroundJob>(
                    json);

            _channel.BasicAck(
                eventArgs.DeliveryTag,
                false);

            completionSource.SetResult(job);
        };

        _channel.BasicConsume(
            queue: queue,
            autoAck: false,
            consumer: consumer);

        return await completionSource.Task;
    }

    public void Dispose()
    {
        _channel.Dispose();

        _connection.Dispose();
    }
}