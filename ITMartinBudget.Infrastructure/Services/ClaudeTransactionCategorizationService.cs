using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BudgetCategory = ITMartinBudget.Domain.Enums.Category;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class ClaudeTransactionCategorizationService
    : IClaudeTransactionCategorizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool CategorizeTool = new()
    {
        Name = "categorize_transaction",
        Description = "Categorize a Danish bank transaction",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["title"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = "Short human-friendly Danish title for the transaction (e.g. 'Netto', 'Netflix', 'Løn fra arbejdsgiver')"
                }),
                ["budgetGroup"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    description = """
                        BudgetGroup enum value. Choose the best match:
                        1=FixedIncome (løn, pensionsudbetaling),
                        2=IncomeFromKommuneAndStat (dagpenge, SU, kontanthjælp),
                        5=ExternalTransfer (overførsel til/fra person),
                        6=OverførslerTilFraOpsparingsKonto (opsparing, investering, aktiedepot),
                        7=Refund (tilbagebetaling, refusion),
                        8=GiftIncome (gave modtaget),
                        9=GiftExpense (gave givet),
                        10=EverydayGrocery (Netto, Føtex, Aldi, Lidl, Spar, dagligvarer),
                        11=RestaurantCafe (restaurant, café, pizza, takeaway, Just Eat),
                        12=Fuel (benzin, Q8, Circle K, Shell),
                        13=Parking (parkering, EasyPark, AutoPay),
                        14=OffentligTransport (bus, tog, DSB, Rejsekort, Metro),
                        15=CarRepair (bilreparation, mekaniker),
                        16=HomeRepair (Bauhaus, Jem & Fix, håndværker, maler),
                        17=GeneralShopping (tøj, Zara, H&M, IKEA, diverse indkøb),
                        19=PersonalCare (frisør, apotek, skønhed, tandlæge),
                        20=Entertainment (bio, sport, koncert, ZOO, forlystelse),
                        22=Tax (skat, SKAT, restskat),
                        23=InterestsAndStock (aktier, renter, kurtage, investeringer),
                        25=Subscriptions (Netflix, Spotify, Adobe, abonnement),
                        26=Uncategorized (ukendt / kan ikke bestemmes),
                        27=Traveling (rejse, hotel, fly, Airbnb, ferie),
                        28=PaymentChildren (udgift til børn, lommepenge),
                        30=CarMaintenance (bilservice, syn, dæk, AutoMester),
                        33=Forsikring (forsikring, Alka, Tryg, Codan),
                        35=RealkreditBolig (husleje, boliglån, Totalkredit, Realkredit),
                        36=FagforeningAKasse (fagforening, a-kasse, IDA, HK, 3F),
                        40=FromChildren (penge modtaget fra børn)
                        """
                }),
                ["category"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    description = """
                        Category enum value. Choose the best match:
                        1=Løn, 10=BoligVedligehold, 11=Regninger, 12=Forsikring,
                        13=TelefonTvInternet, 20=Opsparing, 21=Overfoersel,
                        30=Dagligvarer, 31=Takeaway, 32=Restaurant, 33=Cafe,
                        40=Streaming, 41=KoncertBio, 42=Gaming, 43=Apps, 45=Fritid,
                        50=Parkering, 51=Braendstof, 52=OffentligTransport, 53=BilVedligehold,
                        60=Toej, 61=Elektronik, 62=Bolig,
                        70=Sundhed, 80=Boern, 81=Kaeledyr, 82=Gaver,
                        90=RejserUdflugter, 100=Pension, 101=Refund, 102=Skat,
                        103=Gebyrer, 104=Renter, 105=KommuneAndStat,
                        121=Subscription, 999=Andet
                        """
                }),
                ["recurringIntervalMonths"] = JsonSerializer.SerializeToElement(new
                {
                    type = "number",
                    description = "If recurring: 0.5=twice/month, 1=monthly, 3=quarterly, 6=biannual, 12=annual. Use 0 if not recurring or unknown."
                })
            },
            Required = ["title", "budgetGroup", "category", "recurringIntervalMonths"]
        }
    };

    private const string SystemPrompt = """
        You are a Danish personal finance categorization assistant.
        You receive Danish bank transaction descriptions and amounts and must categorize them accurately.
        Negative amounts are expenses, positive amounts are income.
        Always call the categorize_transaction tool with your answer.
        Be concise with the title — use the merchant/payee name when identifiable.
        """;

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeTransactionCategorizationService> _logger;

    public ClaudeTransactionCategorizationService(
        IConfiguration configuration,
        ILogger<ClaudeTransactionCategorizationService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude:ApiKey configuration");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ClaudeCategorizationResult> CategorizeAsync(
        string description,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 256,
            System = SystemPrompt,
            Tools = [CategorizeTool],
            ToolChoice = new ToolChoiceTool { Name = "categorize_transaction" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Categorize this transaction: \"{description}\" | Amount: {amount:F2} DKK"
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu))
            {
                toolUse = tu;
                break;
            }
        }

        if (toolUse is null)
            throw new InvalidOperationException("Claude did not call the categorize_transaction tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude categorization: {Description} → {Json}", description, json);

        var raw = JsonSerializer.Deserialize<CategorizationRaw>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize categorization result.");

        var budgetGroup = Enum.IsDefined(typeof(BudgetGroup), raw.BudgetGroup)
            ? (BudgetGroup)raw.BudgetGroup
            : BudgetGroup.Uncategorized;

        var category = Enum.IsDefined(typeof(BudgetCategory), raw.Category)
            ? (BudgetCategory)raw.Category
            : BudgetCategory.Andet;

        return new ClaudeCategorizationResult(
            raw.Title,
            category,
            budgetGroup,
            raw.RecurringIntervalMonths);
    }

    private sealed class CategorizationRaw
    {
        public string Title { get; set; } = string.Empty;
        public int BudgetGroup { get; set; }
        public int Category { get; set; }
        public decimal RecurringIntervalMonths { get; set; }
    }
}
