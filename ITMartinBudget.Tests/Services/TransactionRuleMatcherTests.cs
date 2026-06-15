using FluentAssertions;
using ITMartinBudget.Application.Helpers;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class TransactionRuleMatcherTests
{
    private static TransactionRule Rule(
        string pattern,
        ComparingType type,
        string title = "Test") =>
        new()
        {
            Pattern = pattern,
            Title = title,
            ComparingType = type,
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.EverydayGrocery
        };

    // =====================================
    // Exact
    // =====================================

    [Test]
    public void Exact_match_returns_rule_when_text_equals_pattern()
    {
        var rules = new List<TransactionRule>
        {
            Rule("vdk best romedal 0624", ComparingType.Exact)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "vdk best romedal 0624", rules);

        result.Should().NotBeNull();
        result!.Pattern.Should().Be("vdk best romedal 0624");
    }

    [Test]
    public void Exact_match_is_case_insensitive()
    {
        var rules = new List<TransactionRule>
        {
            Rule("VDK BEST ROMEDAL 0624", ComparingType.Exact)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "vdk best romedal 0624", rules);

        result.Should().NotBeNull();
    }

    [Test]
    public void Exact_match_does_not_match_partial_text()
    {
        var rules = new List<TransactionRule>
        {
            Rule("netto", ComparingType.Exact)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "netto supermarked aarhus", rules);

        result.Should().BeNull();
    }

    // =====================================
    // Word
    // =====================================

    [Test]
    public void Word_match_matches_when_pattern_is_whole_word()
    {
        var rules = new List<TransactionRule>
        {
            Rule("netto", ComparingType.Word)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "køb netto aarhus", rules);

        result.Should().NotBeNull();
    }

    [Test]
    public void Word_match_does_not_match_when_pattern_is_substring_of_word()
    {
        var rules = new List<TransactionRule>
        {
            Rule("ingo", ComparingType.Word)
        };

        // "ingo" is inside "ingotankstation" — should not match as whole word
        var result = TransactionRuleMatcher.FindBestMatch(
            "ingotankstation", rules);

        result.Should().BeNull();
    }

    [Test]
    public void Word_match_is_case_insensitive()
    {
        var rules = new List<TransactionRule>
        {
            Rule("shell", ComparingType.Word)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "SHELL TANKSTOP", rules);

        result.Should().NotBeNull();
    }

    // =====================================
    // Contains
    // =====================================

    [Test]
    public void Contains_match_matches_when_pattern_appears_anywhere()
    {
        var rules = new List<TransactionRule>
        {
            Rule("rema", ComparingType.Contains)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "rema 1000 aarhus c", rules);

        result.Should().NotBeNull();
    }

    [Test]
    public void Contains_match_is_case_insensitive()
    {
        var rules = new List<TransactionRule>
        {
            Rule("circle k", ComparingType.Contains)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "CIRCLE K VIBY J", rules);

        result.Should().NotBeNull();
    }

    // =====================================
    // Priority: Exact > Word > Contains
    // =====================================

    [Test]
    public void Exact_takes_priority_over_word_and_contains_for_same_text()
    {
        var rules = new List<TransactionRule>
        {
            Rule("netto", ComparingType.Contains, "Contains Rule"),
            Rule("netto", ComparingType.Word, "Word Rule"),
            Rule("netto", ComparingType.Exact, "Exact Rule")
        };

        var result = TransactionRuleMatcher.FindBestMatch("netto", rules);

        result!.Title.Should().Be("Exact Rule");
    }

    [Test]
    public void Word_takes_priority_over_contains_when_no_exact_match()
    {
        var rules = new List<TransactionRule>
        {
            Rule("ingo", ComparingType.Contains, "Contains Rule"),
            Rule("ingo", ComparingType.Word, "Word Rule")
        };

        var result = TransactionRuleMatcher.FindBestMatch("ingo tankstop", rules);

        result!.Title.Should().Be("Word Rule");
    }

    [Test]
    public void Returns_contains_rule_when_word_boundary_does_not_match()
    {
        var rules = new List<TransactionRule>
        {
            Rule("go", ComparingType.Word, "Word Rule"),
            Rule("go", ComparingType.Contains, "Contains Rule")
        };

        // "go on" as whole word would match, but "gocard" would not match word
        var result = TransactionRuleMatcher.FindBestMatch("gocard payment", rules);

        result!.Title.Should().Be("Contains Rule");
    }

    // =====================================
    // No match
    // =====================================

    [Test]
    public void Returns_null_when_no_rules_match()
    {
        var rules = new List<TransactionRule>
        {
            Rule("netto", ComparingType.Contains),
            Rule("rema", ComparingType.Word)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            "fakta supermarked", rules);

        result.Should().BeNull();
    }

    [Test]
    public void Returns_null_for_empty_rule_list()
    {
        var result = TransactionRuleMatcher.FindBestMatch(
            "netto", new List<TransactionRule>());

        result.Should().BeNull();
    }

    [Test]
    public void Returns_null_for_empty_input_text()
    {
        var rules = new List<TransactionRule>
        {
            Rule("netto", ComparingType.Contains)
        };

        var result = TransactionRuleMatcher.FindBestMatch(
            string.Empty, rules);

        result.Should().BeNull();
    }
}
