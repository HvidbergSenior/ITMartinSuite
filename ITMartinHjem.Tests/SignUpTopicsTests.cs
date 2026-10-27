using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// Front-page sign-up ticks (2026-10-27) and the app names on /admin/tal.
public class SignUpTopicsTests
{
    [Test]
    public void Interests_keep_only_known_topics_and_the_note()
    {
        var t = TrialRules.Topics;
        TrialRules.Interests("itmartin.dk", [t[1], "<script>", t[1], t[2]], " gamle diske ").Should().Be($"{t[1]}, {t[2]} | gamle diske");
        TrialRules.Interests("itmartin.dk", [t[0]], null).Should().Be(t[0]);
        TrialRules.Interests("itmartin.dk", null, "kun tekst").Should().Be("kun tekst");
        TrialRules.Interests("ElPriser", [], "  ").Should().Be("ElPriser");   // said nothing = the app they came from, as before
        TrialRules.Interests("x", null, new string('a', 400)).Should().HaveLength(TrialRules.MaxNote);
    }

    [Test]
    public void Known_app_names_are_never_the_website_itself()
    {
        VisitRules.KnownApps.Keys.Should().NotContain(VisitRules.Site).And.OnlyContain(h => VisitRules.DomainOf(h) != null);
        VisitRules.NotCounted.Should().Contain("kontrol.itmartin.dk");
    }
}
