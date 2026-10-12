using FluentAssertions;
using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Tests;

// Stem's other poll kinds through the use cases, on a real (in-memory) poll.db with the production schema upgrade.
public class FeatureTests
{
    private SqliteConnection _conn = null!;
    private DbContextOptions<PollDb> _options = null!;
    private readonly List<int> _signals = [];

    private sealed class Factory(DbContextOptions<PollDb> o) : IDbContextFactory<PollDb>
    {
        public PollDb CreateDbContext() => new(o);
    }
    private sealed class Signal(List<int> seen) : IChangeSignal { public void Changed(int channel) => seen.Add(channel); }
    private sealed class NoFiles : IImageFiles
    {
        public Task<string> SaveAsync(Stream content, string originalName, string prefix = "", CancellationToken ct = default) => Task.FromResult(prefix + "x.jpg");
    }

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<PollDb>().UseSqlite(_conn).Options;
        using var db = new PollDb(_options);
        PollSchema.Upgrade(db);   // the same start-up SQL as production
    }

    [TearDown] public void TearDown() => _conn.Dispose();

    private EfAggregateStore<T> Store<T>(Func<PollDb, IQueryable<T>> whole) where T : class => new(new Factory(_options), whole);

    [Test]
    public async Task Date_poll_answers_for_several_names_one_preferred_date_and_no_duplicate_dates()
    {
        var store = Store(Aggregates.DatePoll);
        var uc = new DatePolls(store, new NoFiles(), new Signal(_signals), TimeProvider.System);
        (await uc.SaveAsync(0, new DatePolls.Form("Sommerfest", "", null, null, null, [new(2026, 6, 6), new(2026, 6, 13)], []))).Should().BeNull();
        var p = (await store.ListAsync()).Single();
        var (d1, d2) = (p.Dates[0].Id, p.Dates[1].Id);
        var names = DatePollRules.ParseNames("Vibz, Eigil , vibz");
        names.Should().Equal("Vibz", "Eigil");

        await uc.RespondAsync(p.Id, d1, names, "Yes");
        await uc.TogglePreferredAsync(p.Id, d2, names);   // preferred on a date not answered yet = Maybe + preferred
        await uc.TogglePreferredAsync(p.Id, d1, names);   // moves the preference
        await uc.SaveCommentAsync(p.Id, d1, names, " Gerne om eftermiddagen ");

        p = (await uc.GetAsync(p.Id))!;
        var r1 = p.Dates.Single(d => d.Id == d1).Responses;
        r1.Should().HaveCount(2).And.OnlyContain(r => r.Status == "Yes" && r.IsPreferred && r.Comment == "Gerne om eftermiddagen");
        p.Dates.Single(d => d.Id == d2).Responses.Should().OnlyContain(r => r.Status == "Maybe" && !r.IsPreferred);

        (await uc.SuggestDateAsync(p.Id, new DateTime(2026, 6, 13))).Should().Be("Den dato er allerede på listen");
        (await uc.SuggestDateAsync(p.Id, new DateTime(2026, 6, 20))).Should().BeNull();
        await uc.SetHostAsync(p.Id, "Vibz", true, "Hos os i haven");
        await uc.SendChatAsync(p.Id, "", "Hvem tager kage med?");
        p = (await uc.GetAsync(p.Id))!;
        p.Dates.Should().HaveCount(3);
        p.Hosts.Should().ContainSingle().Which.Place.Should().Be("Hos os i haven");
        p.ChatMessages.Should().ContainSingle().Which.SenderName.Should().Be("Anonym");
        _signals.Should().OnlyContain(c => c == ChangeChannels.DatePoll(p.Id)).And.HaveCount(7);

        await uc.RemoveDateAsync(p.Id, new DateTime(2026, 6, 6));   // admin: the week and its answers go
        (await uc.GetAsync(p.Id))!.Dates.Should().HaveCount(2);
    }

    [Test]
    public async Task Poll_options_are_only_remade_when_they_change_so_votes_survive_an_edit()
    {
        var store = Store(Aggregates.Poll);
        var editor = new PollEditor(store, new NoFiles(), TimeProvider.System);
        (await editor.SaveAsync(0, new PollEditor.Form("Kaffe?", "", null, null, false, false, ["Ja", " "]))).Should().Be("Minimum 2 svarmuligheder");
        await editor.SaveAsync(0, new PollEditor.Form("Kaffe?", "", null, null, false, false, ["Ja", "Nej"]));
        var poll = (await store.ListAsync()).Single();
        await store.ChangeAsync(poll.Id, p => { PackageRules.Choose(p, "Ole 12", p.Options[0].Id, "", DateTime.UtcNow); return Task.CompletedTask; });

        await editor.SaveAsync(poll.Id, new PollEditor.Form("Kaffe i dag?", "", null, null, false, false, ["Ja", "Nej"]));
        (await store.GetAsync(poll.Id))!.Votes.Should().ContainSingle("same options - the vote stays");
        await editor.SaveAsync(poll.Id, new PollEditor.Form("Kaffe i dag?", "", null, null, false, false, ["Ja", "Nej", "Te"]));
        var after = (await store.GetAsync(poll.Id))!;
        after.Options.Select(o => o.Label).Should().Equal("Ja", "Nej", "Te");
        after.Votes.Should().BeEmpty("re-made options take their votes with them");
    }

    [Test]
    public async Task Package_household_changes_its_answer_and_a_comment_waits_for_the_choice()
    {
        var polls = Store(Aggregates.Poll);
        var poll = await polls.AddAsync(new Poll { Title = "Ladestander?", CreatedAt = DateTime.UtcNow, Options = [new() { Label = "Ja" }, new() { Label = "Nej" }] });
        var uc = new Packages(Store(Aggregates.Package), polls, Store(Aggregates.Session), TimeProvider.System);
        var label = PackageRules.VoterLabel(" Martin ", " 12 ");
        label.Should().Be("Martin 12");

        (await uc.CommentAsync(poll.Id, label, "kun hvis billig")).Should().BeFalse();
        await uc.ChooseAsync(poll.Id, label, poll.Options[0].Id, "kun hvis billig");
        await uc.ChooseAsync(poll.Id, label, poll.Options[1].Id, "");
        var votes = (await polls.GetAsync(poll.Id))!.Votes;
        votes.Should().ContainSingle().Which.Should().Match<Vote>(v => v.OptionId == poll.Options[1].Id && v.Comment == "kun hvis billig");
    }

    [Test]
    public async Task Help_sign_up_needs_a_name_and_a_time_and_guest_tasks_are_shared()
    {
        var store = Store(Aggregates.Help);
        var e = await store.AddAsync(new HelpEvent { Title = "Flytning", CreatedAt = DateTime.UtcNow, IsActive = true, Slots = [new() { Label = "Lørdag 10-12" }] });
        var uc = new HelpEvents(store, new Signal(_signals));

        var t1 = await uc.AddTaskAsync(e.Id, "Bære kasser", "Jan");
        (await uc.AddTaskAsync(e.Id, "bære kasser", "Sonja")).Should().Be(t1, "the same label is the same task");
        (await uc.SignUpAsync(e.Id, "J", "", "", [e.Slots[0].Id], [])).Should().StartWith("Skriv dit navn");
        (await uc.SignUpAsync(e.Id, "Jan", "", "", [], [])).Should().StartWith("Sæt kryds");
        (await uc.SignUpAsync(e.Id, "Jan", "12345678", "", [e.Slots[0].Id], [t1!.Value])).Should().BeNull();
        await uc.SendChatAsync(e.Id, "Martin", "Tak!", organiser: true);

        var loaded = (await uc.GetAsync(e.Id, isAdmin: false))!;
        loaded.Signups.Should().ContainSingle().Which.TaskList().Should().Equal(t1.Value);
        loaded.Chat.Should().ContainSingle().Which.IsOrganiser.Should().BeTrue();
        await store.ChangeAsync(e.Id, x => { x.IsActive = false; return Task.CompletedTask; });
        (await uc.GetAsync(e.Id, isAdmin: false)).Should().BeNull("closed events are only for the admin");
        (await store.DeleteAsync(e.Id)).Should().BeTrue();
        (await store.ListAsync()).Should().BeEmpty();
    }
}
