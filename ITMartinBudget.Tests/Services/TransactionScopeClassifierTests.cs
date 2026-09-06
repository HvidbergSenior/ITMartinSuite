using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Tests.Services;

// Zero coverage existed for this before - the one piece of code that decides
// whether real money on Bogshoppen's mixed business/private account counts
// as business revenue/expense or the owner's own private spending.
[TestFixture]
public class TransactionScopeClassifierTests
{
    private TransactionScopeClassifier _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new TransactionScopeClassifier();

    private static BankTransaction Tx(string description, string rawDetails = "") =>
        new() { Description = description, RawDetails = rawDetails };

    // ── Business signals ─────────────────────────────────────────────────

    [TestCase("Flatpay udbetaling")]
    [TestCase("SHIFT4 settlement")]
    public void Card_payment_processors_are_classified_as_business_revenue(string description)
    {
        var tx = Tx(description);
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
        tx.BusinessCategory.Should().Be(BusinessCategory.Revenue);
    }

    [TestCase("NewSec husleje")]
    [TestCase("Fællesregnskab NewSec")]
    [TestCase("Husleje september")]
    public void Rent_related_text_is_classified_as_business_rent(string description)
    {
        var tx = Tx(description);
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
        tx.BusinessCategory.Should().Be(BusinessCategory.Rent);
    }

    [TestCase("Betaling til Bogshoppen")]
    [TestCase("Faktura CVR-NR 12345678")]
    [TestCase("Kundenr 998877")]
    public void Generic_business_signals_are_classified_as_business_other(string description)
    {
        var tx = Tx(description);
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
        tx.BusinessCategory.Should().Be(BusinessCategory.Other);
    }

    // ── The "priv" override, checked first ───────────────────────────────

    [Test]
    public void An_explicit_priv_marker_wins_even_over_a_business_rent_match()
    {
        // The exact real-world case that motivated adding this override:
        // the owner's own private rent, paid from the same account, with
        // "husleje" in the text - without "priv" this would misclassify as
        // a business expense and inflate the shop's apparent costs.
        var tx = Tx("Husleje privat lejlighed");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Private);
        tx.BusinessCategory.Should().Be(BusinessCategory.PrivateDraw);
    }

    [Test]
    public void Priv_override_also_beats_a_generic_business_signal()
    {
        var tx = Tx("Overførsel priv, faktura CVR-nr 123");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Private);
    }

    // ── Private signals ───────────────────────────────────────────────────

    [TestCase("Codan Forsikring")]
    [TestCase("Betaling fra Codan")]
    [TestCase("A-kasse august")]
    [TestCase("Telenor mobilabonnement")]
    [TestCase("Pure Gym medlemskab")]
    [TestCase("Modstrøm elregning")]
    [TestCase("Rema 1000")]
    [TestCase("Netto indkøb")]
    [TestCase("Føtex")]
    [TestCase("Lidl")]
    [TestCase("Kvickly")]
    [TestCase("Matas")]
    [TestCase("Apotek")]
    [TestCase("MobilePay til Anders")]
    public void Known_private_signals_are_classified_as_a_private_draw(string description)
    {
        var tx = Tx(description);
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Private);
        tx.BusinessCategory.Should().Be(BusinessCategory.PrivateDraw);
    }

    // ── No match ──────────────────────────────────────────────────────────

    [Test]
    public void An_unrecognized_description_is_left_Unknown_not_guessed()
    {
        var tx = Tx("Overførsel 4829102");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Unknown);
        tx.BusinessCategory.Should().Be(BusinessCategory.Unknown);
    }

    [Test]
    public void An_empty_description_and_raw_details_is_left_Unknown()
    {
        var tx = Tx("");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Unknown);
    }

    // ── Matching is case-insensitive and checks RawDetails too ──────────────

    [Test]
    public void Matching_is_case_insensitive()
    {
        var tx = Tx("FLATPAY UDBETALING");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
    }

    [Test]
    public void RawDetails_is_searched_as_well_as_Description()
    {
        // Bogshoppen's raw/positional bank export carries the real signal in
        // extra reference columns rather than the short description - the
        // classifier has to see both fields, not just Description.
        var tx = Tx(description: "Overførsel", rawDetails: "Flatpay ApS udbetaling");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
        tx.BusinessCategory.Should().Be(BusinessCategory.Revenue);
    }

    // ── First-match-wins ordering ─────────────────────────────────────────

    [Test]
    public void First_matching_rule_wins_when_text_could_match_more_than_one()
    {
        // "flatpay" (business/revenue, earlier in the rule list) should win
        // over "rema 1000" (private, later) if both happened to appear.
        var tx = Tx("Flatpay udbetaling ifm Rema 1000 event-salg");
        _sut.Classify(tx);
        tx.Scope.Should().Be(TransactionScope.Business);
        tx.BusinessCategory.Should().Be(BusinessCategory.Revenue);
    }
}
