using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class TransactionGroupingServiceTests
{
    private TransactionGroupingService _service;

    [SetUp]
    public void Setup()
    {
        _service = new TransactionGroupingService();
    }

    [Test]
    public void GetGroupingKey_Should_Be_Deterministic()
    {
        var tx = new BankTransaction
        {
            Date = new DateTime(2024, 1, 1),
            Amount = 100,
            Description = "NETFLIX"
        };

        var key1 = _service.GetGroupingKey(tx);
        var key2 = _service.GetGroupingKey(tx);

        Assert.That(key1, Is.EqualTo(key2));
    }

    [Test]
    public void GetGroupingKey_Should_Handle_Null_Description()
    {
        var tx = new BankTransaction
        {
            Date = DateTime.Now,
            Amount = 100,
            Description = null
        };

        var key = _service.GetGroupingKey(tx);

        Assert.That(key, Is.Not.Null);
    }
}