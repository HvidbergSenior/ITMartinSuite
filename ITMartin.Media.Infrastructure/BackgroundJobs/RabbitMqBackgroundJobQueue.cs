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

    // IModel is documented as not safe for concurrent use across threads.
    // EnqueueAsync can be called concurrently from many request threads while
    // the consumer acks/nacks on its own dispatch thread - every call that
    // touches _channel must go through this lock.
    private readonly object _channelLock = new();

    public RabbitMqBackgroundJobQueue(
        IConfiguration configuration)
    {
        var factory =
            new ConnectionFactory
            {
                HostName  = configuration["RabbitMq:Host"] ?? "rabbitmq",
                UserName  = configuration["RabbitMq:User"] ?? "guest",
                Password  = configuration["RabbitMq:Password"] ?? "guest",
                Port      = int.TryParse(configuration["RabbitMq:Port"], out var p) ? p : 5672,
                // EventingBasicConsumer + an async handler is a known-broken
                // combination: the Received event fires the handler fire-and-forget
                // on the dispatch thread, so its continuation (including the
                // BasicAck call) resumes on a thread-pool thread. IModel isn't
                // safe for concurrent use across threads, so that Ack can desync
                // the channel and silently stop further deliveries. DispatchConsumersAsync
                // + AsyncEventingBasicConsumer below properly awaits the handler
                // on the dispatch thread instead.
                DispatchConsumersAsync = true
            };
        Console.WriteLine(
            $"RabbitMQ Host: {factory.HostName}");

        // The broker may not be ready yet at startup (compose ordering,
        // container restart race). A bare CreateConnection() with no retry
        // throws BrokerUnreachableException straight out of the constructor
        // and takes the whole host down unhandled - this is exactly what
        // killed filesorter-worker on the NAS for 7 weeks unnoticed (see
        // CLAUDE.md). Retry with backoff instead of failing on the first try.
        const int maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection();
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Console.WriteLine(
                    $"RabbitMQ connection attempt {attempt}/{maxAttempts} failed: {ex.Message}");

                if (attempt < maxAttempts)
                    Thread.Sleep(delay);
            }
        }

        if (lastError is not null)
            throw lastError;

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

        lock (_channelLock)
        {
            var properties =
                _channel.CreateBasicProperties();

            properties.Persistent =
                true;

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: job.Queue,
                basicProperties: properties,
                body: body);
        }

        return Task.CompletedTask;
    }

    public void Subscribe(
        string queue,
        Func<BackgroundJob, Task> handler)
    {
        var consumer =
            new AsyncEventingBasicConsumer(
                _channel);

        consumer.Received += async (_, eventArgs) =>
        {
            BackgroundJob? job;

            try
            {
                var json =
                    Encoding.UTF8.GetString(
                        eventArgs.Body.ToArray());

                job =
                    JsonSerializer.Deserialize<
                        BackgroundJob>(json);
            }
            catch (JsonException ex)
            {
                // A message that will never become valid JSON is not worth
                // retrying - requeuing it loops forever and, with
                // BasicQos prefetch=1, blocks every other queued job behind
                // it. Drop it instead of looping.
                Console.WriteLine(
                    $"Dropping unparseable job message: {ex.Message}");

                lock (_channelLock)
                {
                    _channel.BasicNack(
                        eventArgs.DeliveryTag,
                        false,
                        false);
                }

                return;
            }

            if (job is null)
            {
                lock (_channelLock)
                {
                    _channel.BasicNack(
                        eventArgs.DeliveryTag,
                        false,
                        false);
                }

                return;
            }

            if (job.CreatedAt != default &&
                DateTimeOffset.UtcNow - job.CreatedAt > TimeSpan.FromMinutes(10))
            {
                Console.WriteLine(
                    $"Dropping expired job {job.Type} (queued {DateTimeOffset.UtcNow - job.CreatedAt} ago)");

                lock (_channelLock)
                {
                    _channel.BasicAck(
                        eventArgs.DeliveryTag,
                        false);
                }

                return;
            }

            try
            {
                await handler(job);

                lock (_channelLock)
                {
                    _channel.BasicAck(
                        eventArgs.DeliveryTag,
                        false);
                }
            }
            catch
            {
                lock (_channelLock)
                {
                    _channel.BasicNack(
                        eventArgs.DeliveryTag,
                        false,
                        true);
                }
            }
        };

        lock (_channelLock)
        {
            _channel.BasicConsume(
                queue: queue,
                autoAck: false,
                consumer: consumer);
        }
    }

    public void Dispose()
    {
        _channel.Dispose();

        _connection.Dispose();
    }
}
