using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Services;

// Minimal demo-tier seed — just enough for a visitor to see a populated
// group with members and tasks, not full coverage of every entity. Only
// runs when Club:SeedDemoData=true (the demo compose service), never on
// the real club-web pointed at production data. Idempotent.
public static class DemoSeeder
{
    public const string DemoSlug = "demo";

    public static async Task SeedAsync(ClubDbContext db)
    {
        if (await db.Groups.AnyAsync(g => g.Slug == DemoSlug))
            return;

        var group = new Group
        {
            Slug = DemoSlug,
            Name = "Familien Demo",
            Description = "Et eksempel på en gruppe i IDAG",
            InviteCode = SecretHasher.Hash("demo1234"),
            AdminPin = SecretHasher.Hash("demo1234"),
        };
        db.Groups.Add(group);

        var members = new[]
        {
            new Member { GroupId = group.Id, Name = "Mette", Pin = SecretHasher.Hash("1234"), Role = "Forælder" },
            new Member { GroupId = group.Id, Name = "Jonas", Pin = SecretHasher.Hash("1234"), Role = "Forælder" },
            new Member { GroupId = group.Id, Name = "Ida",   Pin = SecretHasher.Hash("1234"), Role = "Barn" },
        };
        db.Members.AddRange(members);

        var mainTasks = new[]
        {
            new MainTask { GroupId = group.Id, Title = "Rydde op på værelset", IsDaily = true, SortOrder = 0 },
            new MainTask { GroupId = group.Id, Title = "Lektier", IsDaily = true, SortOrder = 1 },
            new MainTask { GroupId = group.Id, Title = "Tage skraldet ud", IsDaily = true, SortOrder = 2 },
            new MainTask { GroupId = group.Id, Title = "Planlægge fødselsdag", DefinitionOfDone = "Gæsteliste og kage bestilt", IsDaily = false, SortOrder = 3 },
            new MainTask { GroupId = group.Id, Title = "Booke sommerferie", IsDaily = false, SortOrder = 4 },
        };
        db.MainTasks.AddRange(mainTasks);

        // A couple of today's tasks already claimed, so the demo doesn't
        // land on an all-empty "Ingen åbne opgaver" list for every card -
        // Jonas (the auto-logged-in demo viewer) has taken one himself, and
        // "Planlægge fødselsdag" shows as a fælles opgave both parents share.
        db.Assignments.AddRange(
            new Assignment
            {
                GroupId = group.Id,
                MainTaskId = mainTasks[0].Id,
                Title = mainTasks[0].Title,
                AssignedToNames = "Jonas",
                CreatedByName = "Jonas",
            },
            new Assignment
            {
                GroupId = group.Id,
                MainTaskId = mainTasks[3].Id,
                Title = mainTasks[3].Title,
                AssignedToNames = "Mette;Jonas",
                CreatedByName = "Mette",
            });

        db.Posts.Add(new BulletinPost
        {
            GroupId = group.Id,
            MemberId = members[0].Id,
            Content = "Velkommen til vores familiegruppe! Her holder vi styr på opgaver, kalender og beskeder.",
            PostedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}
