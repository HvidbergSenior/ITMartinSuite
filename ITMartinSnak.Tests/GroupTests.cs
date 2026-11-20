using System.Threading.Channels;
using FluentAssertions;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

namespace ITMartinSnak.Tests;

// The group (Application) with the real JSON files (Infrastructure) and recording fakes for live feed and push.
public class GroupTests
{
    private string _dir = "";
    private RecordingLive _live = null!;
    private RecordingPush _push = null!;

    private sealed class RecordingLive : ILiveFeed
    {
        public List<string> Kinds = [];
        public void Send(string room, string kind, object data) => Kinds.Add(kind);
        public (Guid Id, ChannelReader<string> Reader) Join(string room) => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<(string? Except, string Title, string Body)> Sent = [];
        public List<string> Forgotten = [];
        public string PublicKey => "test";
        public void Subscribe(string room, string endpoint, string p256dh, string auth, string name) { }
        public void Unsubscribe(string endpoint) { }
        public void SendToOthers(string room, string? exceptEndpoint, string title, string body, string tag, string url) => Sent.Add((exceptEndpoint, title, body));
        public void ForgetRoom(string room) => Forgotten.Add(room);
        public Dictionary<string, int> Phones = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, int> PhonesPerName(string room) => Phones;
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "snak-test-" + Guid.NewGuid().ToString("N"));
        _live = new RecordingLive();
        _push = new RecordingPush();
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

    private Group NewGroup() => new(new JsonGroupStore(_dir), _live, _push, TimeProvider.System, new RoomContext("abc", "Test", "/r/x", _ => { }));

    [Test]
    public void Messages_survive_a_restart_and_come_after_the_last_seen_id()
    {
        var g = NewGroup();
        g.Write("Martin", "Hej alle").Should().NotBeNull();
        g.Write("Sonja", "  Hej!  ")!.Text.Should().Be("Hej!");

        var again = NewGroup();
        again.MessagesAfter(0).Select(m => m.Name).Should().Equal("Martin", "Sonja");
        again.MessagesAfter(1).Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Test]
    public void A_message_needs_a_name_and_text_and_goes_live_and_to_the_other_phones()
    {
        var g = NewGroup();
        g.Write("", "hej").Should().BeNull();
        g.Write("Martin", "   ").Should().BeNull();
        g.Write(new string('x', 50), "hej", "https://push/martin")!.Name.Should().HaveLength(GroupRules.MaxName);

        _live.Kinds.Should().Equal("besked");
        _push.Sent.Should().ContainSingle().Which.Except.Should().Be("https://push/martin");   // not back to the sender
    }

    [Test]
    public void Taking_a_task_twice_lets_go_of_it_again()
    {
        var g = NewGroup();
        var t = g.AddTask("Køb kaffe", "", "lørdag")!;

        g.ToggleTaker(t.Id, "Jan")!.Takers.Should().Equal("Jan");
        g.ToggleTaker(t.Id, "Sonja")!.Takers.Should().Equal("Jan", "Sonja");
        g.ToggleTaker(t.Id, "jan")!.Takers.Should().Equal("Sonja");
        NewGroup().Tasks().Single().Takers.Should().Equal("Sonja");
        _push.Sent.Select(x => x.Title).Should().Equal("Ny opgave · Test", "Opgave taget · Test", "Opgave taget · Test");   // letting go is not pushed
    }

    [Test]
    public void Tasks_can_be_edited_finished_and_deleted()
    {
        var g = NewGroup();
        var t = g.AddTask("Rydde op", "", "")!;
        g.AddTask(" ", "", "").Should().BeNull();

        g.EditTask(t.Id, "Rydde op i haven", "Tag handsker med", "søndag")!.When.Should().Be("søndag");
        g.EditTask(t.Id, "  ", "", "").Should().BeNull();
        g.SetDone(t.Id, true)!.Done.Should().BeTrue();
        g.DeleteTask(t.Id).Should().BeTrue();
        g.Tasks().Should().BeEmpty();
        g.DeleteTask(t.Id).Should().BeFalse();
    }

    [Test]
    public void Only_the_admin_code_logs_in_and_ten_wrong_tries_lock_for_a_while()
    {
        var a = new Access("RicoAdmin", "", TimeProvider.System);
        a.Login(" RicoAdmin ", "1.2.3.4").Should().Be(LoginResult.Admin);
        a.IsAdmin(a.AdminToken).Should().BeTrue();
        a.IsApi("").Should().BeFalse();   // no API key set = the API is closed

        for (var i = 0; i < AccessRules.MaxWrongTries; i++) a.Login("forkert", "5.6.7.8").Should().Be(LoginResult.Wrong);
        a.Login("RicoAdmin", "5.6.7.8").Should().Be(LoginResult.Locked);
        a.Login("RicoAdmin", "9.9.9.9").Should().Be(LoginResult.Admin);   // other addresses are not affected

        new Access("abc", "", TimeProvider.System).Login("abc", "x").Should().Be(LoginResult.Wrong);   // a code shorter than 6 never opens
    }

    // 2026-11-20 (user: "i want short names"): a room made without a short address gets one later; taken or odd ones are refused.
    [Test]
    public void A_short_address_can_be_set_later_changed_and_removed()
    {
        var rooms = NewRooms();
        var a = rooms.Create("Loppemarked", members: "Martin, Lukas");
        var b = rooms.Create("Andet", slug: "andet");
        rooms.SetSlug(a.Id, "Loppemarked").Should().BeNull();
        rooms.BySlug("loppemarked")!.Id.Should().Be(a.Id);
        rooms.SetSlug(a.Id, "andet").Should().Be("Adressen /andet er allerede brugt.");
        rooms.SetSlug(a.Id, "æ/ø").Should().StartWith("Den nemme adresse");
        rooms.SetSlug(b.Id, "andet").Should().BeNull();   // its own address again is fine
        rooms.SetSlug(a.Id, "").Should().BeNull();
        rooms.BySlug("loppemarked").Should().BeNull();
        rooms.SetSlug("nope", "x1").Should().Be("Rummet findes ikke.");
    }

    // ---- rooms (2026-11-04) ----

    private Rooms NewRooms(TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new Rooms(new JsonRoomStore(_dir, clock), _live, _push, clock, "https://snak.test/");
    }

    [Test]
    public void A_room_has_a_secret_link_and_its_messages_stay_in_that_room()
    {
        var rooms = NewRooms();
        var jytte = rooms.Create("  Jytte – høretelefoner ", "kunde-7");
        var per = rooms.Create("");
        RoomRules.ValidKey(jytte.Key).Should().BeTrue();
        jytte.Key.Should().NotBe(per.Key);
        per.Title.Should().Be("Snak med Martin");
        rooms.Link(jytte).Should().Be("https://snak.test/r/" + jytte.Key);

        rooms.GroupOf(jytte).Write("Jytte", "Hvordan lader jeg dem op?");
        rooms.GroupOf(per).MessagesAfter(0).Should().BeEmpty();

        var again = NewRooms();
        again.ByKey(jytte.Key)!.Title.Should().Be("Jytte – høretelefoner");
        again.GroupOf(again.ByKey(jytte.Key)!).MessagesAfter(0).Single().Text.Should().Be("Hvordan lader jeg dem op?");
        again.ByKey("ikke-en-rigtig-noegle").Should().BeNull();
        again.ByKey(jytte.Key[..^1] + (jytte.Key[^1] == 'a' ? 'b' : 'a')).Should().BeNull();
    }

    [Test]
    public void The_overview_puts_rooms_waiting_for_Martin_first()
    {
        var rooms = NewRooms();
        var a = rooms.Create("Svaret");
        var b = rooms.Create("Venter");
        rooms.GroupOf(a).Write("Per", "Hej");
        rooms.GroupOf(a).Write("Martin", "Hej Per", host: true);
        rooms.GroupOf(b).Write("Alexander", "Hvornår passer det?");

        var o = rooms.Overview();
        o.First().Title.Should().Be("Venter");
        o.First().Waiting.Should().BeTrue();
        o.Single(x => x.Title == "Svaret").Waiting.Should().BeFalse();
    }

    private sealed class FakeClock(DateTime start) : TimeProvider
    {
        public DateTime Now = start;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    // 2026-11-18 Easy-Pass "Planlæg turen sammen": named people, the plan as tasks on their days, melts after the last day.
    [Test]
    public void A_planned_trip_room_has_the_steps_and_melts_after_its_last_day()
    {
        var clock = new FakeClock(new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc));
        var rooms = NewRooms(clock);
        var r = rooms.CreatePlanned("Tur: Kystmuseet 12.12", "easypass", "Ali, Lise",
            [("Ring og spørg", "Har I handicaptoilet?", "2026-12-10", ""), ("Tag af sted", "", "2026-12-12", "10:00")], "2026-12-12");
        r.MeltOn.Should().Be("2026-12-12");
        r.KunChat.Should().BeFalse();
        r.Members.Should().Equal("Ali", "Lise");
        rooms.GroupOf(r).Tasks().Select(t => t.Day).Should().Equal("2026-12-10", "2026-12-12");

        clock.Now = new DateTime(2026, 12, 12, 20, 0, 0, DateTimeKind.Utc);   // the trip day itself: still open
        rooms.MeltDue().Should().Be(0);
        clock.Now = new DateTime(2026, 12, 13, 8, 0, 0, DateTimeKind.Utc);
        rooms.MeltDue().Should().Be(1);
        rooms.ById(r.Id)!.IsDone.Should().BeTrue();
        clock.Now = clock.Now.AddDays(15);
        rooms.DeleteFinished().Should().Be(1);
    }

    [Test]
    public void A_melt_day_must_be_a_real_day_within_a_year() 
    {
        var today = new DateOnly(2026, 12, 1);
        RoomRules.CleanMeltOn("2026-11-30", today).Should().Be("");
        RoomRules.CleanMeltOn("i morgen", today).Should().Be("");
        RoomRules.CleanMeltOn("2028-01-01", today).Should().Be("");
        RoomRules.CleanMeltOn("2026-12-24", today).Should().Be("2026-12-24");
    }

    [Test]
    public void A_finished_room_is_deleted_after_14_days_unless_someone_writes_again()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        var rooms = NewRooms(clock);
        var done = rooms.Create("Klaret");
        var back = rooms.Create("Kommer igen");
        rooms.MarkDone(done.Id, "Martin").Should().BeTrue();
        rooms.MarkDone(back.Id, "Martin");
        rooms.GroupOf(done).MessagesAfter(0).Single().Text.Should().Be(RoomRules.DoneText);

        clock.Now = clock.Now.AddDays(2);
        rooms.GroupOf(back).Write("Jytte", "Én ting mere");   // opens it again
        rooms.ById(back.Id)!.IsDone.Should().BeFalse();

        clock.Now = clock.Now.AddDays(13);
        rooms.DeleteFinished().Should().Be(1);
        rooms.ById(done.Id).Should().BeNull();
        rooms.ById(back.Id).Should().NotBeNull();
        Directory.Exists(Path.Combine(_dir, "rooms", done.Id)).Should().BeFalse();
        _push.Forgotten.Should().Equal(done.Id);
    }

    [Test]
    public void The_old_group_moves_into_its_own_room_and_nothing_is_lost()
    {
        Directory.CreateDirectory(_dir);
        new JsonGroupStore(_dir).SaveMessages([new Message(1, "Rico", "Hej drenge", DateTime.UtcNow)]);

        var rooms = NewRooms();
        var rico = rooms.ById(RoomRules.LegacyRoomId)!;
        rico.Title.Should().Be("Rico og venner");
        rooms.GroupOf(rico).MessagesAfter(0).Single().Text.Should().Be("Hej drenge");
        File.Exists(Path.Combine(_dir, "messages.json")).Should().BeFalse();
        NewRooms().Overview().Should().ContainSingle();   // a second start does not move it again
    }

    private static MemoryStream Bytes(int n) => new(Enumerable.Repeat((byte)'x', n).ToArray());

    [Test]
    public async Task A_file_is_stored_under_its_own_name_downloads_again_and_survives_a_restart()
    {
        var g = NewGroup();
        var (f, err) = await g.AddFileAsync(@"C:\fakepath\..\Hold 36 program.pdf", 1000, Bytes(1000), "Sonja", "phone-1");
        err.Should().BeNull();
        f!.Name.Should().Be("Hold 36 program.pdf");
        f.StoredName.Should().Be("1.pdf");
        _live.Kinds.Should().Contain("filer");
        _push.Sent.Should().Contain(p => p.Except == "phone-1" && p.Body.Contains("Hold 36 program.pdf"));

        var again = NewGroup();
        var back = again.Files().Single();
        await using var s = again.OpenFile(back)!;
        s.Length.Should().Be(1000);
    }

    [Test]
    public async Task Programs_too_big_and_nameless_files_are_refused_and_nothing_is_left_behind()
    {
        var g = NewGroup();
        (await g.AddFileAsync("virus.exe", 10, Bytes(10), "Sonja", null)).Error.Should().Contain("kan ikke");
        (await g.AddFileAsync("stor.pdf", FileRules.MaxSize + 1, Bytes(10), "Sonja", null)).Error.Should().Contain("90 MB");
        (await g.AddFileAsync("a.pdf", 10, Bytes(10), " ", null)).Error.Should().Contain("navn");
        // The browser says small, the bytes are bigger: stopped while writing.
        (await g.AddFileAsync("lyver.pdf", 10, Bytes((int)FileRules.MaxSize + 10), "Sonja", null)).Error.Should().NotBeNull();
        g.Files().Should().BeEmpty();
        Directory.GetFiles(Path.Combine(_dir, "files")).Should().BeEmpty();
    }

    [Test]
    public async Task Only_the_one_who_added_a_file_or_Martin_can_delete_it()
    {
        var g = NewGroup();
        var f = (await g.AddFileAsync("noter.docx", 5, Bytes(5), "Sonja", null)).File!;
        g.DeleteFile(f.Id, "Bent", host: false).Should().BeFalse();
        g.DeleteFile(f.Id, "sonja", host: false).Should().BeTrue();
        g.Files().Should().BeEmpty();
        File.Exists(Path.Combine(_dir, "files", f.StoredName)).Should().BeFalse();

        var f2 = (await g.AddFileAsync("b.png", 5, Bytes(5), "Sonja", null)).File!;
        f2.Id.Should().Be(1);   // ids count from what is left
        g.DeleteFile(f2.Id, "Martin", host: true).Should().BeTrue();
    }

    [Test]
    public void A_group_room_cannot_be_closed_as_a_finished_case()
    {
        var rooms = NewRooms();
        var hold = rooms.Create("Hold 36", group: true);
        rooms.MarkDone(hold.Id, "Martin").Should().BeFalse();
        NewRooms().ById(hold.Id)!.Gruppe.Should().BeTrue();
        NewRooms().Overview().Single().Gruppe.Should().BeTrue();
    }

    [Test]
    public void A_pasted_course_list_becomes_members_and_workshops_with_Andet_never_doubled()
    {
        var members = RosterRules.ParseMembers("""
            Evald Merida Jensen – Psykoterapi
            • Helena Cristina Daniele Christensen - Eventbureau

            Thomas schaumburg | Pædagogiskbrobygning - støtte til børn
            evald merida jensen – twice
            Rasmus Gravesen
            """);
        members.Select(m => m.Name).Should().Equal("Evald Merida Jensen", "Helena Cristina Daniele Christensen", "Thomas schaumburg", "Rasmus Gravesen");
        members[2].About.Should().Be("Pædagogiskbrobygning - støtte til børn");
        members[3].About.Should().BeEmpty();

        var topics = RosterRules.ParseTopics("Prissætning\nRegnskab\nandet\nprissætning\n");
        topics.Should().Equal("Prissætning", "Regnskab");
        RosterRules.TopicFor(topics, "regnskab").Should().Be("Regnskab");
        RosterRules.TopicFor(topics, "Hacket emne").Should().BeEmpty();
    }

    [Test]
    public void A_groups_roster_is_kept_and_goes_live_to_the_room()
    {
        var rooms = NewRooms();
        var hold = rooms.Create("Hold 36", group: true);
        rooms.SetRoster(hold.Id, "Martin Hvidberg – Digitale hjælpere", "Regnskab").Should().BeTrue();
        _live.Kinds.Should().Contain("holdet");
        var again = NewRooms().ById(hold.Id)!;
        again.Members.Should().Equal("Martin Hvidberg");   // the door's name buttons
        again.About!["Martin Hvidberg"].Should().Be("Digitale hjælpere");
        again.Topics.Should().Equal("Regnskab");
        again.Gruppe.Should().BeTrue();
    }

    // ---- shared rooms + today + reminders (2026-11-08, the room around Sonja) ----

    [Test]
    public void A_shared_room_has_names_an_easy_address_and_its_own_admin_code()
    {
        var rooms = NewRooms();
        var r = rooms.Create("Omkring Sonja", null, "Vibz, Dorthe; Mette, Bent, dorthe", "TestKode99", "Sonja");
        r.Members.Should().Equal("Vibz", "Dorthe", "Mette", "Bent");
        r.Slug.Should().Be("sonja");
        r.CodeHash.Should().NotContain("TestKode99");
        rooms.EasyLink(r).Should().Be("https://snak.test/sonja");
        NewRooms().BySlug("SONJA")!.Id.Should().Be(r.Id);   // survives a restart

        var act = () => rooms.Create("To", slug: "sonja");
        act.Should().Throw<InvalidOperationException>();
        RoomRules.CleanSlug("api").Should().BeEmpty();

        var a = new Access("RicoAdmin", "", TimeProvider.System);
        a.RoomLogin(r, "TestKode99", "1.1.1.1").Should().Be(LoginResult.Admin);
        a.RoomLogin(r, "forkert", "1.1.1.1").Should().Be(LoginResult.Wrong);
        a.IsRoomAdmin(r, null, r.CodeHash).Should().BeTrue();
        a.IsRoomAdmin(rooms.Create("Andet rum"), null, r.CodeHash).Should().BeFalse();   // only that room
    }

    [Test]
    public void A_task_given_to_someone_has_their_name_on_it_and_they_get_told()
    {
        var g = NewGroup();
        var t = g.AddTask("Ring til Sonja om pillerne", "", "08:00", to: "Dorthe", daily: true)!;
        t.Takers.Should().Equal("Dorthe");
        _push.Sent.Last().Title.Should().StartWith("Ny opgave til Dorthe");

        g.EditTask(t.Id, t.Title, "", "08:00")!.Takers.Should().Equal("Dorthe");   // null = keep
        g.EditTask(t.Id, t.Title, "", "08:00", to: "Bent")!.Takers.Should().Equal("Bent");
        _push.Sent.Last().Title.Should().StartWith("Opgave til Bent");
        g.EditTask(t.Id, t.Title, "", "08:00", to: "")!.Takers.Should().BeEmpty();
    }

    [Test]
    public void Today_shows_daily_and_todays_tasks_and_a_daily_tick_only_lasts_that_day()
    {
        var today = new DateOnly(2026, 11, 8);
        var daily = GroupRules.NewTask([], "Medicin", "", "08:00", DateTime.UtcNow, daily: true)!;
        var monday = GroupRules.NewTask([], "Læge", "", "10:00", DateTime.UtcNow, day: "2026-11-09")!;
        var free = GroupRules.NewTask([], "Køb mælk", "", "", DateTime.UtcNow)!;
        GroupRules.IsToday(daily, today).Should().BeTrue();
        GroupRules.IsToday(monday, today).Should().BeFalse();
        GroupRules.IsToday(monday, today.AddDays(1)).Should().BeTrue();
        GroupRules.IsToday(free, today).Should().BeTrue();

        var ticked = GroupRules.SetDone(daily, true, today);
        ticked.Done.Should().BeFalse();
        ticked.DoneOn.Should().Be("2026-11-08");   // tomorrow it is open again
        GroupRules.SetDone(free, true, today).Done.Should().BeTrue();
        GroupRules.IsToday(GroupRules.SetDone(free, true, today), today).Should().BeFalse();
    }

    [Test]
    public void Reminders_go_out_an_hour_and_half_an_hour_before_and_only_once()
    {
        // 07:00 UTC = 08:00 in Copenhagen (winter time).
        var clock = new FakeClock(new DateTime(2026, 11, 8, 7, 0, 0, DateTimeKind.Utc));
        var g = new Group(new JsonGroupStore(_dir), _live, _push, clock, new RoomContext("abc", "Sonja", "/r/x", _ => { }));
        g.AddTask("Medicin", "", "09:00", to: "Mette", daily: true);
        g.AddTask("Uden klokkeslæt", "", "", daily: true);
        _push.Sent.Clear();

        g.SendReminders().Should().Be(1);
        _push.Sent.Single().Body.Should().Be("Om en time (kl. 09:00): Medicin – Mette");
        g.SendReminders().Should().Be(0);   // not twice

        clock.Now = clock.Now.AddMinutes(31);
        g.SendReminders().Should().Be(1);
        _push.Sent.Last().Body.Should().StartWith("Om 30 minutter");

        g.SetDone(1, true);   // done today: no more
        clock.Now = clock.Now.AddDays(1).AddMinutes(-31);
        g.SendReminders().Should().Be(1);   // next day it is open again
    }

    [Test]
    public void Seen_only_moves_forward_and_is_kept_on_disk()
    {
        var g = NewGroup();
        g.Write("Per", "Hej");
        g.Write("Dorthe", "Hej Per");
        g.MarkSeen("Per", 2, "iphone", true, "til", false)!.MessageId.Should().Be(2);
        g.MarkSeen("per", 1, "iphone", true, "til", false)!.MessageId.Should().Be(2);   // a stale page never un-reads
        g.MarkSeen("Per", 99, "iphone", true, "til", false)!.MessageId.Should().Be(2);  // never past the last message
        _live.Kinds.Should().Contain("set");
        NewGroup().SeenList().Should().ContainSingle(s => s.Name == "Per" && s.MessageId == 2 && s.Device == "iphone" && s.HomeScreen);
    }

    [Test]
    public void Seen_cleans_what_the_page_says_about_the_phone()
    {
        var s = SeenRules.Clean(" Bent ", 3, DateTime.UtcNow, "Nokia", false, "måske", true)!;
        (s.Name, s.Device, s.Push, s.InApp).Should().Be(("Bent", "", "", true));
        SeenRules.Clean("", 1, DateTime.UtcNow, "iphone", true, "til", false).Should().BeNull();
    }

    [Test]
    public void Links_are_one_per_line_and_only_web_addresses()
    {
        var l = RosterRules.ParseLinks("Kursets side – https://acceleratorkursus.dk\nLog ind hold 36: https://acceleratorkursus.dk/vinter2026\nhttps://www.talentmatch.dk\njavascript:alert(1)\nIngen adresse");
        l.Select(x => x.Title).Should().Equal("Kursets side", "Log ind hold 36", "talentmatch.dk");
        l[1].Url.Should().Be("https://acceleratorkursus.dk/vinter2026");
    }

    [Test]
    public void Calendar_reads_the_course_feed_in_order()
    {
        const string ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:20261111T073000Z\r\nDTEND:20261111T140000Z\r\nLOCATION:Damagervej 10\\, 8260 Viby J\r\nSUMMARY:Forretningsstrategi – Jeanette\r\n  Refstrup\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nDTSTART;TZID=Europe/Copenhagen:20261109T080000\r\nDTEND;TZID=Europe/Copenhagen:20261109T150000\r\nLOCATION:Vestergade 49B\\n8000 Aarhus C\r\nSUMMARY:Intro\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20261218\r\nSUMMARY:Afslutning\r\nEND:VEVENT\r\nBEGIN:VEVENT\r\nSUMMARY:no start\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var e = CalendarRules.Parse(ics);
        e.Select(x => x.Title).Should().Equal("Intro", "Forretningsstrategi – Jeanette Refstrup", "Afslutning");
        e[0].StartUtc.Should().Be(new DateTime(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc));   // 08:00 Danish winter time
        e[0].Place.Should().Be("Vestergade 49B, 8000 Aarhus C");
        e[1].Place.Should().Be("Damagervej 10, 8260 Viby J");
        e[2].AllDay.Should().BeTrue();
        CalendarRules.CleanUrl("webcal://calendar.google.com/x/basic.ics").Should().Be("https://calendar.google.com/x/basic.ics");
        CalendarRules.CleanUrl("javascript:alert(1)").Should().Be("");
    }

    [Test]
    public void Own_ticks_are_per_person_and_kept()
    {
        var g = NewGroup();
        var t = g.AddTask("Skriv din pitch", "", "", day: "2026-11-11")!;
        g.SetMyDone("Per", t.Id, true).Should().Equal(t.Id);
        g.MyDone("per").Should().Equal(t.Id);
        g.MyDone("Bent").Should().BeEmpty();                 // nobody else's tick
        g.Tasks().Single().Done.Should().BeFalse();          // the shared task is not touched
        NewGroup().MyDone("Per").Should().Equal(t.Id);       // on disk
        g.SetMyDone("Per", t.Id, false).Should().BeEmpty();
        g.SetMyDone("Per", 999, true).Should().BeNull();
    }

    [Test]
    public void Links_can_be_a_mail_or_a_phone()
    {
        var l = RosterRules.ParseLinks("Mail Michael – michael@talentmatch.dk\nRing til Michael – 24 24 55 77\nRing til Henrik: +45 20235184\nIngen tal 123");
        l.Select(x => x.Url).Should().Equal("mailto:michael@talentmatch.dk", "tel:24245577", "tel:+4520235184");
        l[1].Title.Should().Be("Ring til Michael");
    }

    [Test]
    public void Profiles_are_cleaned_and_only_what_is_given()
    {
        var g = NewGroup();
        var (p, err) = g.SaveProfile("Per", " per@firma.dk ", "24 24 55 77", "www.firma.dk", "", "Laver kaffe");
        err.Should().BeNull();
        (p!.Mail, p.Phone, p.Web, p.LinkedIn).Should().Be(("per@firma.dk", "24245577", "https://www.firma.dk/", ""));
        g.SaveProfile("Per", "ikke en mail", "", "", "", "").Error.Should().Contain("Mail");
        g.SaveProfile("Per", "", "123", "", "", "").Error.Should().Contain("8 cifre");
        g.SaveProfile("Per", "", "", "javascript:alert(1)", "", "").Error.Should().NotBeNull();
        NewGroup().Profiles().Should().ContainSingle(x => x.Name == "Per");
        g.SaveProfile("Per", "", "", "", "", "");   // all empty = the card goes
        g.Profiles().Should().BeEmpty();
    }
}
