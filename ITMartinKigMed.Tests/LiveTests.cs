using FluentAssertions;
using ITMartinKigMed.Application;
using ITMartinKigMed.Domain;
using ITMartinKigMed.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinKigMed.Tests;

public class LiveTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MemoryStore : ISessionStore
    {
        public LiveSession Saved = new();
        public readonly Dictionary<string, LiveSession> Archived = [];
        public LiveSession Load() => Saved;
        public void Save(LiveSession session) => Saved = session;
        public void Archive(LiveSession session, string name) => Archived[name] = session;
    }

    private sealed class NoPictures : IPictureStore
    {
        public Task<string> SaveAsync(Stream jpeg, CancellationToken ct = default) => Task.FromResult("x.jpg");
        public string? PathOf(string name) => name == "a.jpg" ? "/data/a.jpg" : null;
    }

    private sealed class Probe : IMediaProbe
    {
        public bool On;
        public Task<bool> VideoReadyAsync(CancellationToken ct) => Task.FromResult(On);
    }

    private static (LiveHub Hub, MemoryStore Store, Clock Clock) NewHub()
    {
        var store = new MemoryStore();
        var clock = new Clock(T0);
        return (new LiveHub(store, new NoPictures(), clock, NullLogger<LiveHub>.Instance), store, clock);
    }

    [Test]
    public void Going_live_after_a_break_starts_clean_but_pressing_again_keeps_the_questions()
    {
        var s = new LiveSession();
        s.Ask("v1", "Old", "from last time", T0);
        s.GoLive("Koder Kig med", T0);
        s.Questions.Should().BeEmpty();
        s.Ask("v1", "", "hej", T0);
        s.GoLive("Ny titel", T0.AddMinutes(5));
        s.Questions.Should().ContainSingle().Which.Name.Should().Be("Gæst");
        s.StartedAt.Should().Be(T0);
        s.Title.Should().Be("Ny titel");
    }

    [Test]
    public void Only_one_question_is_answered_at_a_time()
    {
        var s = new LiveSession();
        s.Ask("a", "A", "første", T0);
        s.Ask("b", "B", "anden", T0);
        s.SetStatus(s.Questions[0].Id, QuestionStatus.Answering);
        s.SetStatus(s.Questions[1].Id, QuestionStatus.Answering);
        s.Questions.Select(q => q.Status).Should().Equal(QuestionStatus.Done, QuestionStatus.Answering);
    }

    [Test]
    public void A_new_poll_closes_the_old_and_votes_can_change_but_count_once()
    {
        var s = new LiveSession();
        s.StartPoll("Kun én", ["ja", " "]).Should().BeFalse();
        s.StartPoll("Første", ["ja", "nej"]).Should().BeTrue();
        var first = s.Polls[0];
        s.StartPoll("Anden", ["rød", "grøn", "blå"]);
        s.Vote("v", first.Id, 0).Should().BeFalse();
        var poll = s.Polls[1];
        s.Vote("v", poll.Id, 0).Should().BeTrue();
        s.Vote("v", poll.Id, 2).Should().BeTrue();
        s.Vote("v", poll.Id, 3).Should().BeFalse();
        poll.Counts().Should().Equal(0, 0, 1);
    }

    [Test]
    public void Ideas_are_capped_per_viewer_and_pictures_are_only_public_once_shown()
    {
        var s = new LiveSession();
        for (var i = 0; i < LiveSession.MaxIdeasPerViewer; i++) s.SendIdea("v", "V", $"idé {i}", null, T0).Should().BeTrue();
        s.SendIdea("v", "V", "en til", null, T0).Should().BeFalse();
        s.SendIdea("w", "W", "", null, T0).Should().BeFalse();
        s.SendIdea("w", "W", "", "a.jpg", T0).Should().BeTrue();
        s.PictureIsShown("a.jpg").Should().BeFalse();
        s.ShowIdea(s.Ideas.Last().Id, true);
        s.PictureIsShown("a.jpg").Should().BeTrue();
    }

    [Test]
    public void The_floor_goes_only_to_a_raised_hand_and_its_token_works_once_given()
    {
        var f = new Floor();
        f.Give("x", T0).Should().BeFalse();
        f.Raise("x", "Xenia", T0).Should().BeTrue();
        f.Raise("x", "Xenia", T0).Should().BeFalse();
        f.Give("x", T0).Should().BeTrue();
        f.Hands.Should().BeEmpty();
        f.TokenOk(f.OnStage!.Token).Should().BeTrue();
        f.TokenOk("wrong").Should().BeFalse();
        f.End("someone else");
        f.OnStage.Should().NotBeNull();
        f.End();
        f.TokenOk(null).Should().BeFalse();
    }

    [Test]
    public void Viewers_are_throttled_but_Martin_is_not()
    {
        var (hub, _, clock) = NewHub();
        hub.Ask("v", "V", "en").Should().BeTrue();
        hub.Ask("v", "V", "to").Should().BeFalse();
        hub.Ask("", "", "Martin siger", fromMartin: true).Should().BeTrue();
        clock.Now = T0.AddSeconds(4);
        hub.Ask("v", "V", "to").Should().BeTrue();
        hub.Read(s => s.Questions.Count).Should().Be(3);
    }

    [Test]
    public void Reactions_are_limited_to_eight_per_five_seconds()
    {
        var (hub, _, clock) = NewHub();
        for (var i = 0; i < 12; i++) hub.React("v", "👍");
        hub.React("v", "💩");
        hub.Read(s => s.Reactions["👍"]).Should().Be(8);
        clock.Now = T0.AddSeconds(6);
        hub.React("v", "👍");
        hub.Read(s => s.Reactions["👍"]).Should().Be(9);
    }

    [Test]
    public void Ending_archives_the_session_and_clears_the_floor()
    {
        var (hub, store, _) = NewHub();
        hub.GoLive("Test");
        hub.RaiseHand("v", "V");
        hub.GiveFloor("v");
        hub.EndLive();
        hub.OnStage.Should().BeNull();
        store.Archived.Should().ContainKey("2026-10-12_1800.json");
        store.Saved.Live.Should().BeFalse();
        hub.EndLive();
        store.Archived.Should().HaveCount(1);
    }

    [Test]
    public void Unshown_pictures_are_for_the_studio_only()
    {
        var (hub, _, _) = NewHub();
        hub.SendIdea("v", "V", "", "a.jpg");
        hub.PictureFile("a.jpg", owner: false).Should().BeNull();
        hub.PictureFile("a.jpg", owner: true).Should().Be("/data/a.jpg");
        hub.PictureFile("../etc.jpg", owner: true).Should().BeNull();
    }

    [Test]
    public async Task No_video_for_ten_minutes_while_live_ends_the_session()
    {
        var (hub, store, clock) = NewHub();
        var probe = new Probe { On = true };
        var video = new LiveVideo(hub, probe, clock);
        hub.GoLive("Test");
        await video.CheckAsync(default);
        video.IsLive.Should().BeTrue();

        probe.On = false;
        clock.Now = T0.AddMinutes(9);
        await video.CheckAsync(default);
        video.IsLive.Should().BeFalse();
        hub.Read(s => s.Live).Should().BeTrue();

        clock.Now = T0.AddMinutes(11);
        await video.CheckAsync(default);
        hub.Read(s => s.Live).Should().BeFalse();
        store.Archived.Should().HaveCount(1);
    }

    [Test]
    public void Old_session_json_still_loads()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kigmed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "session.json"),
            """{"Live":true,"Title":"Bogshoppen","Next":"","StartedAt":"2026-10-05T12:50:00Z","Questions":[{"Id":"7c9e6679-7425-40de-944b-e07fc1f90ae7","Viewer":"v","Name":"Jytte","Text":"Hej","At":"2026-10-05T12:51:00Z","FromMartin":false,"Status":"færdig"}],"Ideas":[],"Polls":[{"Id":"1c9e6679-7425-40de-944b-e07fc1f90ae7","Text":"?","Options":["ja","nej"],"Votes":{"v":1},"Open":true}],"Reactions":{"👍":3}}""");
        try
        {
            var s = new JsonSessionStore(dir, NullLogger<JsonSessionStore>.Instance).Load();
            s.Live.Should().BeTrue();
            s.Questions.Single().Status.Should().Be(QuestionStatus.Done);
            s.Polls.Single().Counts().Should().Equal(0, 1);
            s.Reactions["👍"].Should().Be(3);
        }
        finally { Directory.Delete(dir, true); }
    }
}
