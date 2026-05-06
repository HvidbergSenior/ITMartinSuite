using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class CategoryServiceTests
{
    private CategoryService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = new CategoryService();
    }

    // =========================
    // INCOME
    // =========================

    [TestCase("LØNOVERFØRSEL")]
    [TestCase("LØN")]
    [TestCase("OVERSKYDENDE SKAT")]
    public async Task Income_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Income);
    }

    // =========================
    // FOOD
    // =========================

    [TestCase("DK føtex Skejby")]
    [TestCase("DK LIDL")]
    [TestCase("DK REMA1000 Aarhus")]
    [TestCase("DK Netto Randersvej")]
    [TestCase("VD SHAWARMA KING APS")]
    [TestCase("VD MCDONALD'S HK 25")]
    public async Task Food_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Food);
    }

    // =========================
    // TRANSPORT
    // =========================

    [TestCase("DK CIRCLE K TILST")]
    [TestCase("VD OK AARHUS, SKEJBY")]
    [TestCase("VD EasyPark A/S")]
    [TestCase("MobilePay Rejsekort")]
    public async Task Transport_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Transport);
    }

    // =========================
    // BILLS
    // =========================

    [TestCase("VDK Spotify")]
    [TestCase("VDK NETFLIX.COM")]
    [TestCase("VDK One.com")]
    [TestCase("BS AKADEMIKERNES A-KASSE")]
    [TestCase("BS ALKA FORSIKRING")]
    [TestCase("VD APPLE.COM/BILL")]
    public async Task Bills_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Bills);
    }

    // =========================
    // HEALTH
    // =========================

    [TestCase("VD SKEJBY APOTEK")]
    [TestCase("VD VERI APOTEK")]
    [TestCase("DK www.fitnessunited.dk")]
    public async Task Health_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Health);
    }

    // =========================
    // SHOPPING
    // =========================

    [TestCase("VD QUINT BRUUNS")]
    [TestCase("VD Arket Aarhus")]
    [TestCase("VD ADIDASDK")]
    [TestCase("DK Elgiganten")]
    public async Task Shopping_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Shopping);
    }

    // =========================
    // TRAVEL
    // =========================

    [TestCase("VD HOTELCOM")]
    [TestCase("VD 12GO")]
    [TestCase("VD MPOS SINHCAFE3")]
    public async Task Travel_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Travel);
    }

    // =========================
    // SAVINGS
    // =========================

    [TestCase("BoligOpsparing")]
    [TestCase("Børneopsparing")]
    public async Task Savings_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Savings);
    }

    // =========================
    // TRANSFERS
    // =========================

    [TestCase("MobilePay Bent Møller")]
    [TestCase("VDK MOB.PAY*EIGIL")]
    [TestCase("Til 7633 0008318157")]
    public async Task Transfers_Should_Classify_Correctly(string description)
    {
        var result = await Detect(description);

        result.Should().Be(Category.Transfer);
    }

    // =========================
    // DEFAULT
    // =========================

    [Test]
    public async Task Unknown_Should_Become_Other()
    {
        var result = await Detect("RANDOM UNKNOWN SHOP");

        result.Should().Be(Category.Other);
    }

    private async Task<Category> Detect(string description)
    {
        var tx = new BankTransaction
        {
            Description = description,
            Amount = -100
        };

        return await _service.DetectAsync(description.ToLowerInvariant());
    }
}