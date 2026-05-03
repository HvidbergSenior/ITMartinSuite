using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Moq;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class CategoryServiceTests
{
    private Mock<ICategoryRuleRepository> _repoMock;
    private CategoryService _service;

    [SetUp]
    public void Setup()
    {
        _repoMock = new Mock<ICategoryRuleRepository>();

        _repoMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<CategoryRule>
            {
                new CategoryRule
                {
                    Keyword = "netflix",
                    SubCategory = SubCategory.StreamingTjenester
                }
            });

        _service = new CategoryService(_repoMock.Object);
    }

    [Test]
    public async Task DetectAsync_Should_Return_MobilePay_Incoming()
    {
        var tx = new BankTransaction
        {
            Description = "mobilepay test",
            Amount = 100
        };

        var result = await _service.DetectAsync("anything", tx);

        Assert.That(result, Is.EqualTo(SubCategory.MobilePayFraAndre));
    }

    [Test]
    public async Task DetectAsync_Should_Return_MobilePay_Outgoing()
    {
        var tx = new BankTransaction
        {
            Description = "mobilepay test",
            Amount = -100
        };

        var result = await _service.DetectAsync("anything", tx);

        Assert.That(result, Is.EqualTo(SubCategory.MobilePayTilAndre));
    }

    [Test]
    public async Task DetectAsync_Should_Use_Engine_For_Normal_Transactions()
    {
        var tx = new BankTransaction
        {
            Description = "netflix subscription",
            Amount = -99
        };

        var result = await _service.DetectAsync("netflix", tx);

        Assert.That(result, Is.EqualTo(SubCategory.StreamingTjenester));
    }
}