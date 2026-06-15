using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class TransactionCategorizerTests
{
    private TransactionCategorizer _categorizer = null!;

    [SetUp]
    public void SetUp()
    {
        _categorizer = new TransactionCategorizer();
    }

    private static BankTransaction Transaction(string normalizedDescription) =>
        new()
        {
            NormalizedDescription = normalizedDescription,
            Description = normalizedDescription,
            Date = new DateTime(2024, 1, 15),
            Amount = -100m
        };

    // =====================================
    // Grocery
    // =====================================

    [TestCase("netto")]
    [TestCase("NETTO")]
    [TestCase("køb netto aarhus")]
    public void Categorizes_netto_as_everyday_grocery(string description)
    {
        var t = Transaction(description);

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Dagligvarer);
        t.BudgetGroup.Should().Be(BudgetGroup.EverydayGrocery);
    }

    [Test]
    public void Categorizes_rema_as_everyday_grocery()
    {
        var t = Transaction("rema 1000 aarhus c");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Dagligvarer);
        t.BudgetGroup.Should().Be(BudgetGroup.EverydayGrocery);
        t.Title.Should().Be("Rema 1000");
    }

    [Test]
    public void Categorizes_foetex_as_everyday_grocery()
    {
        var t = Transaction("foetex aarhus");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Dagligvarer);
        t.BudgetGroup.Should().Be(BudgetGroup.EverydayGrocery);
    }

    // =====================================
    // Fuel
    // =====================================

    [Test]
    public void Categorizes_circle_k_as_fuel()
    {
        var t = Transaction("circle k viby j");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Braendstof);
        t.BudgetGroup.Should().Be(BudgetGroup.Fuel);
        t.Title.Should().Be("Circle K");
    }

    [Test]
    public void Categorizes_shell_as_fuel()
    {
        var t = Transaction("shell tankstop");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Braendstof);
        t.BudgetGroup.Should().Be(BudgetGroup.Fuel);
    }

    [Test]
    public void Categorizes_exact_best_romedal_as_fuel()
    {
        var t = Transaction("vdk best romedal 0624");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Braendstof);
        t.BudgetGroup.Should().Be(BudgetGroup.Fuel);
        t.Title.Should().Be("Best");
    }

    // =====================================
    // Sets all fields on match
    // =====================================

    [Test]
    public void Sets_title_category_and_budget_group_on_match()
    {
        var t = Transaction("netto");

        _categorizer.Categorize(t);

        t.Title.Should().NotBeNullOrWhiteSpace();
        t.Category.Should().NotBe(default(Category));
        t.BudgetGroup.Should().NotBe(BudgetGroup.Unknown);
    }

    // =====================================
    // No match — leaves transaction untouched
    // =====================================

    [Test]
    public void Does_not_modify_transaction_when_no_rule_matches()
    {
        var t = Transaction("xyzzy unknown merchant 99999");
        var originalTitle = t.Title;
        var originalCategory = t.Category;
        var originalBudgetGroup = t.BudgetGroup;

        _categorizer.Categorize(t);

        t.Title.Should().Be(originalTitle);
        t.Category.Should().Be(originalCategory);
        t.BudgetGroup.Should().Be(originalBudgetGroup);
    }

    // =====================================
    // Housing
    // =====================================

    [Test]
    public void Categorizes_jyske_realkredit_as_husleje()
    {
        var t = Transaction("termin jyske realkredit december 2024");

        _categorizer.Categorize(t);

        t.Category.Should().Be(Category.Husleje);
        t.BudgetGroup.Should().Be(BudgetGroup.RealkreditBolig);
    }
}
