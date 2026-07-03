using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Server.Data;

public class PollDb(DbContextOptions<PollDb> opts) : DbContext(opts)
{
    public DbSet<Poll>         Polls         => Set<Poll>();
    public DbSet<PollOption>   Options       => Set<PollOption>();
    public DbSet<Vote>         Votes         => Set<Vote>();
    public DbSet<ImageSession> Sessions      => Set<ImageSession>();
    public DbSet<SessionImage> SessionImages => Set<SessionImage>();
    public DbSet<ImageRating>  ImageRatings  => Set<ImageRating>();
}
