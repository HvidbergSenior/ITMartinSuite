using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Infrastructure;

public static class MagicPersistenceDependencyInjection
{
    public static IServiceCollection
        AddMagicPersistence(
            this IServiceCollection services,
            string connectionString)
    {
        services.AddDbContext<MagicDbContext>(
            options =>
                options.UseSqlite(
                    connectionString));

        return services;
    }
}