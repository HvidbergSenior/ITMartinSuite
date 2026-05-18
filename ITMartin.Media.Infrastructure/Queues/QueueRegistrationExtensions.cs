// File: ITMartin.Media.Infrastructure/Queues/QueueRegistrationExtensions.cs

using ITMartin.Media.Application.Abstractions.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.Queues;

public static class QueueRegistrationExtensions
{
    public static IServiceCollection AddInMemoryQueue<T>(
        this IServiceCollection services)
        where T : class
    {
        services.AddSingleton<InMemoryQueue<T>>();

        services.AddSingleton<IQueueProducer<T>>(provider =>
            provider.GetRequiredService<InMemoryQueue<T>>());

        services.AddSingleton<IQueueConsumer<T>>(provider =>
            provider.GetRequiredService<InMemoryQueue<T>>());

        return services;
    }
}