using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// "Skriv til mig" -> a Snak room (2026-11-05).
public class SnakStartTests
{
    private sealed class FakeSnak(string? link = "https://snak.itmartin.dk/r/abc") : ISnakRooms
    {
        public List<(string Title, string Name, string Text)> Made { get; } = [];
        public Task<string?> CreateAsync(string title, string name, string firstMessage, CancellationToken ct = default)
        {
            Made.Add((title, name, firstMessage));
            return Task.FromResult(link);
        }
    }

    [Test]
    public async Task Name_and_message_make_a_room_with_the_message_in_it()
    {
        var snak = new FakeSnak();
        var r = await new SnakStartUseCases(snak, TimeProvider.System).StartAsync("  Anna ", " Printeren driller ", "1.2.3.4");
        r.Link.Should().Be("https://snak.itmartin.dk/r/abc");
        snak.Made.Should().ContainSingle().Which.Should().Be(("Anna", "Anna", "Printeren driller"));
    }

    [TestCase("", "hej")]
    [TestCase("Anna", "   ")]
    public async Task Missing_name_or_message_makes_no_room(string name, string text)
    {
        var snak = new FakeSnak();
        var r = await new SnakStartUseCases(snak, TimeProvider.System).StartAsync(name, text, "1.2.3.4");
        r.Link.Should().BeNull();
        r.Error.Should().NotBeNullOrEmpty();
        snak.Made.Should().BeEmpty();
    }

    [Test]
    public async Task One_address_gets_at_most_the_daily_number_of_rooms()
    {
        var snak = new FakeSnak();
        var uc = new SnakStartUseCases(snak, TimeProvider.System);
        for (var i = 0; i < SnakStartRules.PerDay; i++) (await uc.StartAsync("Anna", "hej", "1.2.3.4")).Link.Should().NotBeNull();
        (await uc.StartAsync("Anna", "hej", "1.2.3.4")).Error.Should().Contain("mange");
        (await uc.StartAsync("Bo", "hej", "5.6.7.8")).Link.Should().NotBeNull();   // someone else is not stopped
    }

    [Test]
    public async Task Snak_down_gives_a_friendly_line_and_does_not_count()
    {
        var r = await new SnakStartUseCases(new FakeSnak(link: null), TimeProvider.System).StartAsync("Anna", "hej", "1.2.3.4");
        r.Link.Should().BeNull();
        r.Error.Should().Contain("ring");
    }
}
