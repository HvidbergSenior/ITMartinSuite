using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// Fixed weekly chat hours shown by themselves (2026-11-13).
public class ChatHoursTests
{
    private static readonly ChatHours H = ChatHours.Parse("fre 12-13; man 10:30-12; bad; lør 9-8");

    [Test] public void Skips_bad_parts() => H.Slots.Should().HaveCount(2);

    [Test] public void Open_inside_a_slot() =>
        H.Describe(new DateTime(2026, 11, 13, 12, 15, 0)).Should().Be((true, "Jeg kan snakke nu – til kl. 13"));

    [Test] public void Later_today() =>
        H.Describe(new DateTime(2026, 11, 13, 8, 0, 0)).Text.Should().Be("Næste gang jeg kan snakke: i dag kl. 12–13");

    [Test] public void After_friday_slot_next_is_monday() =>
        H.Describe(new DateTime(2026, 11, 13, 14, 0, 0)).Text.Should().Be("Næste gang jeg kan snakke: mandag kl. 10:30–12");

    [Test] public void Tomorrow() =>
        H.Describe(new DateTime(2026, 11, 15, 20, 0, 0)).Text.Should().Be("Næste gang jeg kan snakke: i morgen kl. 10:30–12");

    [Test] public void Week_starts_monday() => H.Week().Should().Be("mandag kl. 10:30–12 · fredag kl. 12–13");

    [Test] public void Empty_shows_nothing() => ChatHours.Parse("").Describe(DateTime.Now).Text.Should().BeEmpty();
}
