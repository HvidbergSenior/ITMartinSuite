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
    private readonly IConfiguration _configuration;

    // Connection is established lazily, on first actual use (EnqueueAsync or
    // Subscribe) rather than in the constructor. This type is registered as a
    // DI singleton, so an eager connection attempt here used to run the
    // moment ANY request first needed IBackgroundJobQueue - including just
    // rendering a page that merely offers a "Start" button, never mind
    // whether the broker was reachable. With the broker down, that meant
    // every page load blocked for the full ~20s retry budget below and then
    // 500'd on an unhandled BrokerUnreachableException (confirmed
    // 2026-09-03: RabbitMQ wasn't running and Index.razor's homepage crashed
    // outright, unrelated to whatever the user actually wanted to do).
    // Deferring the connection means a page loads fine either way, and only
    // an action that genuinely needs the queue (clicking Start, or the
    // Worker's Subscribe) pays for - and fails on - the connection attempt.
    private IConnection? _connection;

    private IModel? _channel;

    // Guards lazy connection setup (EnsureConnected) AND every call that
    // touches _channel once connected - IModel is documented as not safe for
    // concurrent use across threads, and EnqueueAsync can be called
    // concurrently from many request threads while the consumer acks/nacks on
    // its own dispatch thread.
    private readonly object _channelLock = new();

    public RabbitMqBackgroundJobQueue(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // The broker may not be ready yet at startup (compose ordering, container
    // restart race), or may be down entirely. A bare CreateConnection() with
    // no retry throws BrokerUnreachableException straight out - this is
    // exactly what killed filesorter-worker on the NAS for 7 weeks unnoticed
    // (see CLAUDE.md). Retry with backoff instead of failing on the first
    // try; still throws after exhausting attempts, but only to the caller
    // that actually needed the queue right now, not to every page load.
    private void EnsureConnected()
    {
        if (_channel is not null) return;

        lock (_channelLock)
        {
            if (_channel is not null) return;

            var factory =
                new ConnectionFactory
                {
                    HostName  = _configuration["RabbitMq:Host"] ?? "rabbitmq",
                    UserName  = _configuration["RabbitMq:User"] ?? "guest",
                    Password  = _configuration["RabbitMq:Password"] ?? "guest",
                    Port      = int.TryParse(_configuration["RabbitMq:Port"], out var p) ? p : 5672,
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

            const int maxAttempts = 10;
            var delay = TimeSpan.FromSeconds(2);
            Exception? lastError = null;
            IConnection? connection = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    connection = factory.CreateConnection();
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

            var channel = connection!.CreateModel();

            channel.QueueDeclare(
                queue: "workflow",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            channel.BasicQos(
                0,
                1,
                false);

            _connection = connection;
            _channel = channel;
        }
    }

    public Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        EnsureConnected();

        var json =
            JsonSerializer.Serialize(job);

        var body =
            Encoding.UTF8.GetBytes(json);

        lock (_channelLock)
        {
            var properties =
                _channel!.CreateBasicProperties();

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
        EnsureConnected();

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
                    _channel!.BasicNack(
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
                    _channel!.BasicNack(
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
                    _channel!.BasicAck(
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
                    _channel!.BasicAck(
                        eventArgs.DeliveryTag,
                        false);
                }
            }
            catch
            {
                lock (_channelLock)
                {
                    _channel!.BasicNack(
                        eventArgs.DeliveryTag,
                        false,
                        true);
                }
            }
        };

        lock (_channelLock)
        {
            _channel!.BasicConsume(
                queue: queue,
                autoAck: false,
                consumer: consumer);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();

        _connection?.Dispose();
    }
}
