using FluentAssertions;
using ITMartinStrategi.Application;
using ITMartinStrategi.Domain;
using ITMartinStrategi.Infrastructure;
using NUnit.Framework;

namespace ITMartinStrategi.Tests;

[TestFixture]
public class ChallengeTests
{
    private static readonly JsonWingspanCards Cards = new(Path.Combine(TestContext.CurrentContext.TestDirectory, "wingspan-start.json"));

    private sealed class MemoryStore : IChallengeStore
    {
        private readonly List<WingspanChallenge> _all = [];
        public IReadOnlyList<WingspanChallenge> All() => _all;
        public WingspanChallenge? Get(Guid id) => _all.FirstOrDefault(c => c.Id == id);
        public void Save(WingspanChallenge c) { _all.RemoveAll(x => x.Id == c.Id); _all.Add(c); }
        public void Delete(Guid id) => _all.RemoveAll(c => c.Id == id);
    }

    [TestCase("Red-backed fairy", "Red-Backed Fairywren")]
    [TestCase("Common Chaff", "Common Chaffinch")]
    [TestCase("common green magpie", "Common Green Magpie")]
    [TestCase("Eurasian Hopoe", "Eurasian Hoopoe")]
    [TestCase("Northern flicker", "Northern Flicker")]
    public void Short_and_misspelt_names_find_the_bird(string typed, string name) =>
        Cards.Find(typed).Bird!.Name.Should().Be(name);

    [Test]
    public void Two_birds_with_the_same_start_are_asked_about_not_guessed()
    {
        var (bird, suggestions) = Cards.Find("Red-backed");
        bird.Should().BeNull();
        suggestions.Should().Contain("Red-Backed Fairywren");
    }

    [Test]
    public void The_users_week_plans_an_engine_with_the_chaffinch_and_the_flicker_first()
    {
        var uc = new ChallengeUseCases(Cards, new MemoryStore());
        var week = uc.Create("2026 week 44", "Red-backed fairy, Common Chaff, common green magpie, Eurasian Hoopoe, Northern flicker");
        week.StartBirds.Should().Equal("Red-Backed Fairywren", "Common Chaffinch", "Common Green Magpie", "Eurasian Hoopoe", "Northern Flicker");

        var plan = uc.Plan(week);
        plan.Row.Should().Be("grassland", "4 of the 5 can live there, so the chaffinch tucks behind 4 birds (forest: 3)");
        plan.Order[0].Bird.Name.Should().Be("Northern Flicker", "cheap, and its invertebrates pay for the magpie and hoopoe");
        plan.Order[^1].Bird.Name.Should().Be("Red-Backed Fairywren", "game end - last");
        plan.Combos.Should().Contain(c => c.StartsWith("Common Chaffinch +"));
        plan.Combos.Should().Contain(c => c.StartsWith("Northern Flicker gets invertebrate"));
        plan.Combos.Should().NotContain(c => c.StartsWith("Eurasian Hoopoe gets"), "3 food - not a bird that pays for others");
        TestContext.Out.WriteLine($"ROW {plan.Row}: {plan.RowWhy}");
        foreach (var c in plan.Combos) TestContext.Out.WriteLine("COMBO " + c);
        foreach (var a in plan.Order) TestContext.Out.WriteLine($"ORDER {a.Bird.Name} [{a.Bird.Tier}] {a.Role} | {a.When} | {a.Note}");
    }

    [Test]
    public void Attempts_are_kept_and_the_best_is_known()
    {
        var uc = new ChallengeUseCases(Cards, new MemoryStore());
        var w = uc.Create("", "Northern Flicker");
        uc.AddAttempt(w.Id, 92, "too slow");
        uc.AddAttempt(w.Id, 118, "forest engine");
        uc.AddSeen(w.Id, "Common Chaffinch, Northern Flicker");
        var saved = uc.Week(w.Id)!;
        saved.Best!.Score.Should().Be(118);
        saved.SeenBirds.Should().Equal("Common Chaffinch");
    }
}
