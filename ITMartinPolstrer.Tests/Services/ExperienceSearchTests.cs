using FluentAssertions;
using ITMartinPolstrer.Core.Data.Entities;
using ITMartinPolstrer.Core.Services;

namespace ITMartinPolstrer.Tests.Services;

[TestFixture]
public class ExperienceSearchTests
{
    private static Piece Piece(string title, string instructions = "", string techniques = "", string customer = "", params string[] notes)
    {
        var p = new Piece { Title = title, Instructions = instructions, Techniques = techniques, Customer = customer };
        var t = DateTime.UtcNow;
        foreach (var n in notes)
            p.Steps.Add(new Step { Note = n, At = t = t.AddMinutes(1) });
        return p;
    }

    [Test]
    public void Matches_case_insensitively_across_title_instructions_techniques_customer_and_notes()
    {
        var pieces = new[]
        {
            Piece("Lænestol", instructions: "Behold NAKKERULLEN"),
            Piece("Sofa", techniques: "fjedre, nakkerulle"),
            Piece("Skammel", customer: "Nakkerulle-Niels"),
            Piece("Bænk", notes: "Syet ny nakkerulle i dag"),
            Piece("Stol uden noget"),
        };

        var hits = ExperienceSearch.Find(pieces, "nakkerulle");

        hits.Select(h => h.Piece.Title).Should().BeEquivalentTo("Lænestol", "Sofa", "Skammel", "Bænk");
    }

    [Test]
    public void Reports_which_part_matched_so_the_page_can_show_the_right_snippet()
    {
        var p = Piece("Stol", instructions: "fjedre skal efterses", notes: ["Fjedre skiftet", "Betræk på"]);

        var hit = ExperienceSearch.Find([p], "fjedre").Single();

        hit.InstructionsHit.Should().BeTrue();
        hit.Steps.Select(s => s.Note).Should().Equal("Fjedre skiftet");
    }

    [Test]
    public void Empty_or_whitespace_query_returns_nothing()
    {
        ExperienceSearch.Find([Piece("Stol")], "   ").Should().BeEmpty();
    }

    [Test]
    public void Tags_are_deduplicated_case_insensitively_and_ordered_by_use()
    {
        var pieces = new[]
        {
            Piece("A", techniques: "Fjedre, kantsyning"),
            Piece("B", techniques: "fjedre"),
            Piece("C", techniques: "nakkerulle, FJEDRE"),
        };

        ExperienceSearch.Tags(pieces).Should().Equal("Fjedre", "kantsyning", "nakkerulle");
    }

    [Test]
    public void Snippet_windows_around_the_match_in_a_long_note()
    {
        var text = new string('a', 200) + " fjedre " + new string('b', 200);

        var s = ExperienceSearch.Snippet(text, "fjedre");

        s.Should().StartWith("…").And.EndWith("…").And.Contain("fjedre");
        s.Length.Should().BeLessThan(150);
    }

    [Test]
    public void Snippet_returns_short_text_untouched()
    {
        ExperienceSearch.Snippet("kort note om fjedre", "fjedre").Should().Be("kort note om fjedre");
    }
}
