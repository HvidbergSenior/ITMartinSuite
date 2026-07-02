using ITMartinCloudOverblik.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinCloudOverblik.Server.Data;

public sealed class CloudDbContext(DbContextOptions<CloudDbContext> options) : DbContext(options)
{
    public DbSet<AuditLead> Leads => Set<AuditLead>();
}
