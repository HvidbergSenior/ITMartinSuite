using ITMartinTestHub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTestHub.Server.Data;

// Minimal demo-tier seed - one test round with an app, a tester, and an
// assignment with feedback, so a visitor sees the tracker populated. Only
// runs when TestHub:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(TestHubDbContext db)
    {
        if (await db.Rounds.AnyAsync())
            return;

        var round = new TestRound { Name = "Demo-testrunde" };
        db.Rounds.Add(round);

        var app = new AppEntry { Name = "Demo App", Url = "https://example.com", Description = "Eksempel-app til demoformål." };
        db.Apps.Add(app);

        var tester = new Tester { Name = "Anna" };
        db.Testers.Add(tester);
        await db.SaveChangesAsync();

        var assignment = new TestAssignment
        {
            TestRoundId = round.Id,
            AppEntryId = app.Id,
            TesterId = tester.Id,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        db.Feedbacks.Add(new Feedback
        {
            TestAssignmentId = assignment.Id,
            AppEntryId = app.Id,
            TesterId = tester.Id,
            Text = "Eksempel-feedback til demoformål — fungerede fint på min telefon.",
        });

        await db.SaveChangesAsync();
    }
}
