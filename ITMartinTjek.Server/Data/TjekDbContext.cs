using Microsoft.EntityFrameworkCore;

namespace ITMartinTjek.Server.Data;

public sealed class TjekDbContext(DbContextOptions<TjekDbContext> options) : DbContext(options)
{
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
}

// One saved check run. Browser and deep-script results are stored as the JSON the
// client produced, so the page can re-render an old run exactly as it looked.
public sealed class Snapshot
{
    public int Id { get; set; }
    public string Device { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    // "browser" or "deep"
    public string Kind { get; set; } = "browser";
    public string Json { get; set; } = "[]";
    public int Ok { get; set; }
    public int Warn { get; set; }
    public int Bad { get; set; }
    public string Summary { get; set; } = "";
}
