using ITMartinSalgsveje.Application;
using ITMartinSalgsveje.Domain;

namespace ITMartinSalgsveje.Tests.Services;

[TestFixture]
public class SalesRoutesTests
{
    private static readonly Guid Team = Guid.NewGuid();
    private static Member Person(string name, string? role = null) => new() { GroupId = Team, Name = name, Role = role };

    [Test]
    public void A_channel_needs_a_name_and_a_known_kind()
    {
        var problems = SalesChannelRules.Problems(new SalesChannel { Name = " ", Kind = "Noget" });
        Assert.That(problems, Has.Count.EqualTo(2));
    }

    [Test]
    public void Booking_after_the_date_is_a_problem()
    {
        var c = new SalesChannel { Name = "Ry", NextDate = new DateTime(2027, 5, 1), BookBy = new DateTime(2027, 5, 10) };
        Assert.That(SalesChannelRules.Problems(c).Any(p => p.Contains("frist")), Is.True);
    }

    [Test]
    public void Planned_comes_first_soonest_date_on_top_then_ideas_then_tried()
    {
        var order = SalesChannelRules.InOrder(
        [
            new SalesChannel { Name = "Prøvet", Status = SalesStatus.Tried },
            new SalesChannel { Name = "Idé", Status = SalesStatus.Idea },
            new SalesChannel { Name = "Senere", Status = SalesStatus.Planned, NextDate = new DateTime(2027, 3, 1) },
            new SalesChannel { Name = "Snart", Status = SalesStatus.Planned, NextDate = new DateTime(2027, 1, 30) },
        ]).Select(c => c.Name);
        Assert.That(order, Is.EqualTo(new[] { "Snart", "Senere", "Idé", "Prøvet" }));
    }

    [Test]
    public void Book_soon_when_deadline_is_within_a_week()
    {
        var today = new DateTime(2026, 11, 4);
        Assert.That(new SalesChannel { BookBy = today.AddDays(5) }.BookSoon(today), Is.True);
        Assert.That(new SalesChannel { BookBy = today.AddDays(30) }.BookSoon(today), Is.False);
        Assert.That(new SalesChannel { BookBy = today, Status = SalesStatus.Tried }.BookSoon(today), Is.False);
    }

    [Test]
    public async Task Saving_makes_a_hovedopgave_and_tasks_land_under_the_channel()
    {
        var (_, routes) = TestBoard.CreateRoutes();
        var martin = Person("Martin");
        var c = new SalesChannel { GroupId = Team, Name = "Shoppe Loppe", Kind = SalesKinds.Marked };
        Assert.That((await routes.SaveAsync(martin, c)).Saved, Is.True);
        Assert.That(c.MainTaskId, Is.Not.Null);

        Assert.That(await routes.AddTaskAsync(martin, c.Id, "Book stand", "Vibz", new DateTime(2026, 11, 10)), Is.Empty);
        var card = (await routes.CardsAsync(Team)).Single();
        Assert.That(card.Open, Has.Count.EqualTo(1));
        var task = card.Open[0];
        Assert.That(task.AssigneeLabel, Is.EqualTo("Vibz"));

        await routes.CompleteTaskAsync(martin, task.Id);
        card = (await routes.CardsAsync(Team)).Single();
        Assert.That(card.Open, Is.Empty);
        Assert.That(card.Done, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_task_without_title_is_refused()
    {
        var (_, routes) = TestBoard.CreateRoutes();
        var c = new SalesChannel { GroupId = Team, Name = "Tillidsbod", Kind = SalesKinds.Tillidsbod };
        await routes.SaveAsync(Person("Martin"), c);
        Assert.That(await routes.AddTaskAsync(Person("Martin"), c.Id, "  ", "", null), Is.Not.Empty);
    }

    [Test]
    public async Task Only_admins_delete_and_the_starter_list_comes_only_once()
    {
        var (_, routes) = TestBoard.CreateRoutes();
        var admin = Person("Martin", "Admin");
        var n = await routes.AddStarterListAsync(admin);
        Assert.That(n, Is.EqualTo(StarterList.For(Team).Count));
        Assert.That(await routes.AddStarterListAsync(admin), Is.EqualTo(0));

        var first = (await routes.CardsAsync(Team)).First().Channel;
        Assert.That(await routes.DeleteAsync(Person("Rico"), first.Id), Is.False);
        Assert.That(await routes.DeleteAsync(admin, first.Id), Is.True);
    }
}
