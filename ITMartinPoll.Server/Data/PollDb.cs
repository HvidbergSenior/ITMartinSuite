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
    public DbSet<DatePoll>            DatePolls         => Set<DatePoll>();
    public DbSet<DatePollDate>        DatePollDates     => Set<DatePollDate>();
    public DbSet<DatePollResponse>    DatePollResponses => Set<DatePollResponse>();
    public DbSet<DatePollChatMessage> DatePollChat      => Set<DatePollChatMessage>();
    public DbSet<DatePollImage>       DatePollImages    => Set<DatePollImage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // DatePollResponse.DateOption doesn't match EF's naming convention for
        // the DateId scalar (it'd expect "DateOptionId") - without this, EF
        // invents a shadow "DateOptionId" column that doesn't exist in the
        // actual table (raw-SQL migrated, not EF Migrations), breaking every
        // query that touches DatePollDate.Responses.
        b.Entity<DatePollResponse>()
            .HasOne(r => r.DateOption)
            .WithMany(d => d.Responses)
            .HasForeignKey(r => r.DateId);
    }
}
