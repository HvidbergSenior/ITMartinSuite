using ITMartinLaes.Infrastructure.External;
using ITMartinLaes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLaes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder: laes.db, books/ (the PDFs), keys/ (Laes__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Laes:DataDir"] ?? "/app/data";

    public static IServiceCollection AddLaesInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(dataDir);
        services.AddDbContextFactory<LaesDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "laes.db")};Default Timeout=30"));
        services.AddScoped<IBookRepository, BookRepository>();
        // A full path: Results.File treats a relative one as a web path (2026-10-25: "data\books\x.pdf" not found).
        var books = Path.GetFullPath(Path.Combine(dataDir, "books"));
        services.AddSingleton<IBookFiles>(new FileBookStore(books));
        services.AddSingleton<IPageImages>(sp => new PopplerPageImages(books, sp.GetRequiredService<ILogger<PopplerPageImages>>()));
        services.AddSingleton<IPdfText, PdfPigText>();
        services.AddScoped<IBookSummarizer, ClaudeBookSummarizer>();
        return services;
    }

    /// <summary>Creates laes.db the first time - call once at start.</summary>
    public static void UpgradeLaesDatabase(this IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<LaesDb>>().CreateDbContext();
        db.Database.EnsureCreated();
    }
}
