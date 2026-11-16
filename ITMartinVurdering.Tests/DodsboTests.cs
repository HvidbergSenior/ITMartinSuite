using FluentAssertions;
using ITMartinVurdering.Application;
using ITMartinVurdering.Domain;

namespace ITMartinVurdering.Tests;

// Dødsbo og loft (2026-11-10): a photo per thing; ONE AI call values up to 8; own price wins; the total counts only what sells.
public class DodsboTests
{
    private static readonly DateTime Tue = new(2026, 11, 10, 15, 0, 0);

    private sealed class MemoryStore : ISagStore
    {
        public readonly Dictionary<string, Sag> Sager = [];
        public readonly Dictionary<string, byte[]> Photos = [];
        public List<Sag> All() => Sager.Values.ToList();
        public Sag? Get(string id) => Sager.GetValueOrDefault(id);
        public void Save(Sag sag) => Sager[sag.Id] = sag;
        public void SavePhoto(string sagId, string tingId, byte[] jpeg) => Photos[sagId + "/" + tingId] = jpeg;
        public byte[]? Photo(string sagId, string tingId) => Photos.GetValueOrDefault(sagId + "/" + tingId);
        public void DeletePhoto(string sagId, string tingId) => Photos.Remove(sagId + "/" + tingId);
    }

    private sealed class FakeValuer : IThingValuer
    {
        public int Calls, Left = 5;
        public List<int> Sizes = [];
        public bool Enabled => true;
        public int CallsLeftToday() => Left;
        public Task<List<ThingValue>> ValueAsync(IReadOnlyList<ThingPhoto> photos, CancellationToken ct)
        {
            Calls++; Left--; Sizes.Add(photos.Count);
            return Task.FromResult(photos.Select(p => p.Index == 0
                ? new ThingValue(0, "Rubinglas-vase, böhmisk", "Se efter stempel i bunden", 600, 300, "sælg", "lauritz.com", "Eftertragtet glas", "böhmisk rubinglas vase",
                    "Slebet rubinglas blev lavet i Böhmen fra 1800-tallet.")
                : new ThingValue(p.Index, "Plasticdukke", "", null, null, "giv væk", "Loppemarked", "Lav værdi", "dukke")).ToList());
        }
    }

    private static (DodsboUseCases Uses, MemoryStore Store, FakeValuer Ai, Sag Sag) New()
    {
        var store = new MemoryStore();
        var ai = new FakeValuer();
        var uses = new DodsboUseCases(store, ai, () => Tue);
        var (sag, error) = uses.New("Mariannes ting");
        error.Should().BeNull();
        return (uses, store, ai, sag!);
    }

    // Vareinfo's "Møbler og ting" (2026-11-10): one Tjek Sag per day, made by itself.
    [Test]
    public void Today_is_one_Tjek_sag_per_day_made_by_itself()
    {
        var store = new MemoryStore();
        var day = Tue;
        var uses = new DodsboUseCases(store, new FakeValuer(), () => day);
        var first = uses.Today();
        first.Name.Should().Be("Tjek – 10. november");
        uses.Today().Id.Should().Be(first.Id);
        day = Tue.AddDays(1);
        uses.Today().Id.Should().NotBe(first.Id);
        store.All().Should().HaveCount(2);
    }

    [Test]
    public void A_sag_needs_a_name_and_an_empty_photo_is_refused()
    {
        var (uses, _, _, sag) = New();
        uses.New("  ").Error.Should().Contain("navn");
        uses.AddPhoto(sag.Id, [], null).Ok.Should().BeFalse();
        uses.AddPhoto("findes-ikke", [1], null).Ok.Should().BeFalse();
    }

    [Test]
    public async Task One_press_is_one_AI_call_for_at_most_eight_photos()
    {
        var (uses, _, ai, sag) = New();
        for (var i = 0; i < 10; i++) uses.AddPhoto(sag.Id, [1, 2, 3], i == 0 ? "mormors" : null).Ok.Should().BeTrue();

        var first = await uses.ValueWaitingAsync(sag.Id);
        first.Message.Should().Contain("8 ting er vurderet").And.Contain("2 venter");
        ai.Calls.Should().Be(1);
        ai.Sizes.Should().Equal(DodsboRules.PerCall);

        await uses.ValueWaitingAsync(sag.Id);
        ai.Sizes.Should().Equal(8, 2);
        (await uses.ValueWaitingAsync(sag.Id)).Message.Should().Contain("ikke nogen nye");
        ai.Calls.Should().Be(2);   // nothing waiting = no call
    }

    [Test]
    public async Task The_answer_is_cleaned_and_the_total_counts_only_what_sells_with_own_price_first()
    {
        var (uses, store, _, sag) = New();
        uses.AddPhoto(sag.Id, [1], null);
        uses.AddPhoto(sag.Id, [1], null);
        await uses.ValueWaitingAsync(sag.Id);

        var s = store.Get(sag.Id)!;
        var vase = s.Ting.Single(t => t.What.StartsWith("Rubinglas"));
        (vase.LowKr, vase.HighKr, vase.Verdict, vase.Where).Should().Be((300, 600, Verdict.Saelg, "Lauritz.com"));   // swapped, place normalised
        DodsboRules.Range(vase.LowKr, vase.HighKr).Should().Be("300–600 kr.");
        DodsboRules.Range(0, 0).Should().Be("");   // nothing worth selling yet
        vase.Story.Should().StartWith("Slebet rubinglas");
        s.Total().Should().Be((300, 600));   // the doll is "giv væk" - not counted

        uses.Own(sag.Id, vase.Id, 450, "Solgt til naboen").Ok.Should().BeTrue();
        store.Get(sag.Id)!.Total().Should().Be((450, 450));
        uses.Own(sag.Id, vase.Id, -1, null).Ok.Should().BeFalse();
    }

    [Test]
    public async Task The_daily_cap_stops_the_call_and_unknown_verdicts_become_ask_an_expert()
    {
        var (uses, store, ai, sag) = New();
        uses.AddPhoto(sag.Id, [1], null);
        ai.Left = 0;
        (await uses.ValueWaitingAsync(sag.Id)).Message.Should().Contain("grænse");
        ai.Calls.Should().Be(0);

        DodsboRules.ParseVerdict("noget andet").Should().BeNull();
        var t = new Ting();
        t.Valued("x", "", 0, null, DodsboRules.ParseVerdict("??") ?? Verdict.SpoergEkspert, "Amazon", "", "", Tue);
        (t.LowKr, t.Verdict, t.Where).Should().Be(((int?)null, Verdict.SpoergEkspert, ""));
    }

    [Test]
    public void A_share_link_shows_the_sag_until_it_is_stopped_and_keeps_its_token_when_prices_change()
    {
        var (uses, _, _, sag) = New();
        uses.Shared(null).Should().BeNull();
        var token = uses.Share(sag.Id, prices: false)!;
        token.Length.Should().BeGreaterThanOrEqualTo(16);
        uses.Shared(token)!.Id.Should().Be(sag.Id);
        uses.Shared(token[..^1] + "x").Should().BeNull();
        uses.Share(sag.Id, prices: true).Should().Be(token);   // a link already sent keeps working
        uses.Shared(token)!.SharePrices.Should().BeTrue();
        uses.StopSharing(sag.Id);
        uses.Shared(token).Should().BeNull();
        DodsboRules.Range(800, 2050).Should().Be("800–2.050 kr.");   // Danish thousands
    }

    [Test]
    public void Removing_a_thing_removes_its_photo()
    {
        var (uses, store, _, sag) = New();
        uses.AddPhoto(sag.Id, [1], null);
        var id = store.Get(sag.Id)!.Ting[0].Id;
        uses.Remove(sag.Id, id).Ok.Should().BeTrue();
        store.Photos.Should().BeEmpty();
        store.Get(sag.Id)!.Ting.Should().BeEmpty();
    }
}
