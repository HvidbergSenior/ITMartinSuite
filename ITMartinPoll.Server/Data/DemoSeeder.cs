using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Server.Data;

// Minimal demo-tier seed - one example of each poll type (simple choice,
// image-rating session, date-availability poll) so a visitor sees the full
// feature set immediately. Only runs when Poll:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(PollDb db)
    {
        if (await db.Polls.AnyAsync() || await db.DatePolls.AnyAsync())
            return;

        var poll = new Poll
        {
            Title = "Hvor skal vi holde julefrokost?",
            Body = "Afstemning lukker om en uge.",
            Deadline = DateTime.UtcNow.AddDays(7),
            Options =
            [
                new PollOption { Label = "Restaurant i byen", SortOrder = 0 },
                new PollOption { Label = "Hjemme hos en af os", SortOrder = 1 },
                new PollOption { Label = "Lej et lokale", SortOrder = 2 },
            ],
        };
        db.Polls.Add(poll);
        await db.SaveChangesAsync();

        db.Votes.AddRange(
            new Vote { PollId = poll.Id, OptionId = poll.Options[0].Id, VoterName = "Anna" },
            new Vote { PollId = poll.Id, OptionId = poll.Options[0].Id, VoterName = "Bo" },
            new Vote { PollId = poll.Id, OptionId = poll.Options[1].Id, VoterName = "Cecilie" });

        var datePoll = new DatePoll
        {
            Title = "Hvornår passer det med en gåtur?",
            Description = "Svar Ja/Nej/Måske på de datoer, der passer dig.",
            Dates =
            [
                new DatePollDate { Date = DateTime.UtcNow.AddDays(3), SortOrder = 0 },
                new DatePollDate { Date = DateTime.UtcNow.AddDays(5), SortOrder = 1 },
                new DatePollDate { Date = DateTime.UtcNow.AddDays(7), SortOrder = 2 },
            ],
        };
        db.DatePolls.Add(datePoll);
        await db.SaveChangesAsync();

        db.DatePollResponses.AddRange(
            new DatePollResponse { DateId = datePoll.Dates[0].Id, VoterName = "Anna", Status = "Yes" },
            new DatePollResponse { DateId = datePoll.Dates[1].Id, VoterName = "Anna", Status = "Maybe" },
            new DatePollResponse { DateId = datePoll.Dates[0].Id, VoterName = "Bo", Status = "No" });

        await db.SaveChangesAsync();
    }
}
