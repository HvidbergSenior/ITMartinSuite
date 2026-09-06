using FluentAssertions;
using ITMartinBudget.Application.Helpers;

namespace ITMartinBudget.Tests.Helpers;

// Covers the bug found live 2026-09-06: a browser auto-capitalized the
// Konto-id upload field, and since LedgerId is matched with plain
// case-sensitive equality everywhere, "Bogshoppen" silently forked into a
// brand-new, disconnected ledger next to the real "bogshoppen" instead of
// merging into it.
[TestFixture]
public class LedgerIdNormalizerTests
{
    [TestCase("bogshoppen", "bogshoppen")]
    [TestCase("Bogshoppen", "bogshoppen")]
    [TestCase("BOGSHOPPEN", "bogshoppen")]
    [TestCase("  bogshoppen  ", "bogshoppen")]
    [TestCase("Hvidberg", "hvidberg")]
    public void Normalizes_to_a_single_canonical_casing(string input, string expected)
    {
        LedgerIdNormalizer.Normalize(input).Should().Be(expected);
    }

    [Test]
    public void Null_input_normalizes_to_empty_string_rather_than_throwing()
    {
        LedgerIdNormalizer.Normalize(null).Should().Be(string.Empty);
    }

    [Test]
    public void Differently_cased_inputs_normalize_to_the_same_value()
    {
        var a = LedgerIdNormalizer.Normalize("Bogshoppen");
        var b = LedgerIdNormalizer.Normalize("bogshoppen");
        var c = LedgerIdNormalizer.Normalize("BOGSHOPPEN");

        a.Should().Be(b).And.Be(c);
    }
}
