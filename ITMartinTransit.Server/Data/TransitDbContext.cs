using Microsoft.EntityFrameworkCore;

namespace ITMartinTransit.Server.Data;

public class TransitDbContext(DbContextOptions<TransitDbContext> options) : DbContext(options)
{
    public DbSet<TransitPerson> Persons => Set<TransitPerson>();
}
