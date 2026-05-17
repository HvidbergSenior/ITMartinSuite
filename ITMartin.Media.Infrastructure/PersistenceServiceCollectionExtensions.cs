using ITMartin.Media.Application.Abstractions.Distributed;
using ITMartin.Media.Application.Abstractions.Queues;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Services.Workflows;
using ITMartin.Media.Infrastructure.Distributed;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Queues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMediaPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MediaDb")
            ?? "Data Source=media.db";

        services.AddDbContext<Persistence.MediaDbContext>(options =>
        {
            options.UseSqlite(
                connectionString,
                builder =>
                {
                    builder.MigrationsAssembly(
                        typeof(Persistence.MediaDbContext).Assembly.FullName);
                });
        });

        services.AddScoped<IWorkflowCheckpointStore, EfWorkflowCheckpointStore>();

        services.AddScoped<IScanSessionStore, EfScanSessionStore>();

        services.AddSingleton<IDistributedCoordinator, InMemoryDistributedCoordinator>();

        services.AddSingleton(typeof(IQueueTransport<>), typeof(ChannelQueueTransport<>));

        services.AddScoped<WorkflowRecoveryService>();

        return services;
    }
}