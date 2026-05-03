using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Domain;

[TestFixture]
public class BankTransactionTests
{
    [Test]
    public void TransactionType_Should_Be_Income_For_Positive_Amount()
    {
        var tx = new BankTransaction
        {
            Amount = 100,
            SubCategory = SubCategory.Ukendt
        };

        Assert.That(tx.TransactionType, Is.EqualTo(TransactionType.Indkomst));
    }

    [Test]
    public void TransactionType_Should_Be_Expense_For_Negative_Amount()
    {
        var tx = new BankTransaction
        {
            Amount = -100,
            SubCategory = SubCategory.Ukendt
        };

        Assert.That(tx.TransactionType, Is.EqualTo(TransactionType.Udgift));
    }

    [Test]
    public void TransactionType_Should_Be_Transfer_For_Transfer_SubCategory()
    {
        var tx = new BankTransaction
        {
            Amount = -100,
            SubCategory = SubCategory.OverførselTilAndre
        };

        Assert.That(tx.TransactionType, Is.EqualTo(TransactionType.Overførsel));
    }

    [Test]
    public void TransactionType_Should_Be_Income_For_Løn()
    {
        var tx = new BankTransaction
        {
            Amount = 0,
            SubCategory = SubCategory.Løn
        };

        Assert.That(tx.TransactionType, Is.EqualTo(TransactionType.Indkomst));
    }

    [Test]
    public void IsMobilePay_Should_Be_True_When_Description_Contains_MobilePay()
    {
        var tx = new BankTransaction
        {
            Description = "MobilePay John"
        };

        Assert.That(tx.IsMobilePay, Is.True);
    }

    [Test]
    public void IsMobilePay_Should_Be_False_When_Not_MobilePay()
    {
        var tx = new BankTransaction
        {
            Description = "Netflix subscription"
        };

        Assert.That(tx.IsMobilePay, Is.False);
    }
}