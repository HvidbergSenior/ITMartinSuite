using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class BudgetServiceTests
{
    private BudgetService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = new BudgetService();
    }

    [Test]
    public void GetYearTotals_Should_Only_Include_Udgift_As_Expenses()
    {
        // Arrange

        var year = 2025;

        var transactions = new List<BankTransaction>
        {
            new()
            {
                Date = new DateTime(2025, 1, 1),
                Amount = -1000,
                TransactionType = TransactionType.Udgift,
                Category = Category.Food,
                Description = "Netto"
            },

            // SHOULD NOT COUNT
            new()
            {
                Date = new DateTime(2025, 1, 2),
                Amount = -1330.03m,
                TransactionType = TransactionType.MobilePay,
                Category = Category.Housing,
                Description = "MobilePay"
            }
        };

        // Act

        var result = _service.GetYearTotals(transactions, year);

        // Assert

        result.Expenses.Should().Be(1000);
    }

    [Test]
    public void GetBudgetOverview_Should_Only_Include_Indkomst_And_Udgift()
    {
        // Arrange

        var year = 2025;

        var transactions = new List<BankTransaction>
        {
            new()
            {
                Date = new DateTime(2025, 1, 1),
                Amount = -1000,
                TransactionType = TransactionType.Udgift,
                Category = Category.Food,
                Description = "Netto"
            },

            // SHOULD NOT COUNT
            new()
            {
                Date = new DateTime(2025, 1, 2),
                Amount = -1330.03m,
                TransactionType = TransactionType.MobilePay,
                Category = Category.Housing,
                Description = "MobilePay"
            }
        };

        // Act

        var result = _service
            .GetBudgetOverview(transactions, year)
            .ToList();

        // Assert

        result.Should().HaveCount(1);

        result.Single().Total.Should().Be(1000);
    }

    [Test]
    public void Expense_Totals_Should_Match_Overview_Totals()
    {
        // Arrange

        var year = 2025;

        var transactions = new List<BankTransaction>
        {
            new()
            {
                Date = new DateTime(2025, 1, 1),
                Amount = -5000,
                TransactionType = TransactionType.Udgift,
                Category = Category.FixedExpenses,
                Description = "Spotify"
            },

            new()
            {
                Date = new DateTime(2025, 1, 2),
                Amount = -2500,
                TransactionType = TransactionType.Udgift,
                Category = Category.Food,
                Description = "Netto"
            }
        };

        // Act

        var totals = _service.GetYearTotals(transactions, year);

        var overview = _service
            .GetBudgetOverview(transactions, year)
            .ToList();

        var fixedExpenses = overview
            .Where(x => x.Category == Category.FixedExpenses)
            .Sum(x => x.Total);

        var variableExpenses = overview
            .Where(x => x.TransactionType == TransactionType.Udgift)
            .Where(x => x.Category != Category.FixedExpenses)
            .Sum(x => x.Total);

        // Assert

        totals.Expenses.Should()
            .Be(fixedExpenses + variableExpenses);
    }

    [Test]
    public void TransactionProcessor_Should_Set_Udgift_For_Negative_Amounts()
    {
        // Arrange

        var processor = new TransactionProcessor(null!);

        var tx = new BankTransaction
        {
            Amount = -100,
            Description = "Netto"
        };

        // Act

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        // Assert

        tx.TransactionType.Should().Be(TransactionType.Udgift);
    }

    [Test]
    public void TransactionProcessor_Should_Set_Indkomst_For_Positive_Amounts()
    {
        // Arrange

        var tx = new BankTransaction
        {
            Amount = 100,
            Description = "Salary"
        };

        // Act

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        // Assert

        tx.TransactionType.Should().Be(TransactionType.Indkomst);
    }
}
