using FluentAssertions;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;
using ITMartinHusk.Infrastructure;

namespace ITMartinHusk.Tests;

// Husk for everyone (2026-11-10): one list per person; Martin's file stays where it was; the example is never saved.
public class PersonerTests
{
    private static readonly DateTime Tue = new(2026, 11, 10, 12, 0, 0);
    private string _dir = "";

    private sealed class Push : IPushSender
    {
        public List<string> To = [];
        public PushResult Answer = PushResult.Sent;
        public string PublicKey => "key";
        public Task<PushResult> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default) { To.Add(to.Endpoint); return Task.FromResult(Answer); }
    }

    [SetUp] public void SetUp() => Directory.CreateDirectory(_dir = Path.Combine(Path.GetTempPath(), "husk-test-" + Guid.NewGuid().ToString("N")));
    [TearDown] public void TearDown() => Directory.Delete(_dir, true);

    private (HuskSpaces Spaces, HuskStoreFactory Stores, Push Push) New()
    {
        var stores = new HuskStoreFactory(new HuskInfraOptions(_dir));
        var push = new Push();
        return (new HuskSpaces(stores, push, () => Tue), stores, push);
    }

    [Test]
    public void Ids_are_checked_so_a_kundenummer_can_only_name_its_own_folder()
    {
        HuskPeople.Kundenummer("kunde-12345678").Should().Be("12345678");
        HuskPeople.Kundenummer("kunde-../../x").Should().BeNull();
        HuskPeople.Kundenummer("kunde-1234567").Should().BeNull();
        HuskPeople.IsValid("martin").Should().BeTrue();
        HuskPeople.IsValid("eksempel-").Should().BeFalse();
        HuskPeople.IsValid("").Should().BeFalse();
        var who = new HuskPerson();
        who.Set("kunde-12345678");
        who.IsMartin.Should().BeFalse();
        FluentActions.Invoking(() => who.Set(HuskPeople.Martin)).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Min_Husk_is_for_Martin_the_example_and_a_customer_with_the_paid_tick()
    {
        var free = new HuskPerson(); free.Set("kunde-12345678");
        free.MinHusk.Should().BeFalse();
        var paid = new HuskPerson(); paid.Set("kunde-12345678"); paid.SetPaid(true);
        paid.MinHusk.Should().BeTrue();
        var martin = new HuskPerson(); martin.Set(HuskPeople.Martin);
        martin.MinHusk.Should().BeTrue();
        var demo = new HuskPerson(); demo.Set(HuskPeople.Demo("ab12"));
        demo.MinHusk.Should().BeTrue();
        HuskParts.IsMinHuskPath("/bolde").Should().BeTrue();
        HuskParts.IsMinHuskPath("/Alt").Should().BeTrue();
        HuskParts.IsMinHuskPath("/tilfoej").Should().BeFalse();
        HuskParts.IsMinHuskPath("/").Should().BeFalse();
    }

    [Test]
    public void Martin_keeps_husk_json_and_each_customer_gets_an_own_list()
    {
        var (spaces, stores, _) = New();
        spaces.For(HuskPeople.Martin).Balls.Add("Q-Park", "", "Ring", "", null);
        spaces.For(HuskPeople.Kunde("12345678")).Balls.Add("Tandlæge", "", "Ring", "", null);

        File.Exists(Path.Combine(_dir, "husk.json")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, "personer", "12345678", "husk.json")).Should().BeTrue();
        spaces.For(HuskPeople.Martin).Balls.All().Select(b => b.Title).Should().Equal("Q-Park");
        spaces.For(HuskPeople.Kunde("12345678")).Balls.All().Select(b => b.Title).Should().Equal("Tandlæge");
        spaces.For(HuskPeople.Kunde("87654321")).Balls.All().Should().BeEmpty();
        stores.Saved().Should().Equal(HuskPeople.Martin, HuskPeople.Kunde("12345678"));   // the empty one has no file yet

        // A new start reads the same lists back.
        var again = new HuskSpaces(new HuskStoreFactory(new HuskInfraOptions(_dir)), new Push(), () => Tue);
        again.For(HuskPeople.Kunde("12345678")).Balls.All().Select(b => b.Title).Should().Equal("Tandlæge");
    }

    [Test]
    public void The_example_is_made_up_kept_in_memory_and_never_sends()
    {
        var (spaces, stores, push) = New();
        var demo = spaces.For(HuskPeople.Demo("abc123"));
        demo.Balls.All().Should().HaveCount(5);
        demo.Husk.Today().Cards.Should().ContainSingle(c => c.Kind == CardKind.Tomorrow);
        demo.Balls.Next().Should().NotBeNull();
        demo.Husk.Subscribe(new PushSignup("demo-phone", "p", "a"));

        stores.Saved().Should().Equal(HuskPeople.Martin);
        Directory.GetFiles(_dir, "*", SearchOption.AllDirectories).Should().BeEmpty();
        spaces.For(HuskPeople.Demo("other")).Balls.All().Should().HaveCount(5);   // every visitor gets a fresh one
        spaces.TickAllAsync().GetAwaiter().GetResult();
        push.To.Should().NotContain("demo-phone");
    }

    [Test]
    public async Task Each_person_reminds_only_their_own_phones_and_a_gone_phone_is_removed()
    {
        var (spaces, _, push) = New();
        var martin = spaces.For(HuskPeople.Martin);
        var kunde = spaces.For(HuskPeople.Kunde("12345678"));
        martin.Husk.Subscribe(new PushSignup("martin-phone", "p", "a"));
        kunde.Husk.Subscribe(new PushSignup("kunde-phone", "p", "a"));
        kunde.Husk.AddPost("Kommunen").Should().BeNull();

        await spaces.TickAllAsync();
        push.To.Should().Equal("kunde-phone");

        push.Answer = PushResult.Gone;
        (await kunde.Husk.TestAsync("kunde-phone")).Should().BeFalse();
        kunde.Husk.IsSubscribed("kunde-phone").Should().BeFalse();
        martin.Husk.IsSubscribed("martin-phone").Should().BeTrue();
    }
}
