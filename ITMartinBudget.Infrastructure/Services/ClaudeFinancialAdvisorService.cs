using Anthropic;
using Anthropic.Models.Messages;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinBudget.Infrastructure.Services;

public sealed class ClaudeFinancialAdvisorService : IFinancialAdvisorService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeFinancialAdvisorService> _logger;

    private const string SystemPrompt = """
        Du er en venlig, konkret og ærlig personlig økonomi-rådgiver for en dansk familie.
        Du modtager en opsummering af familiens økonomi baseret på rigtige banktransaktioner.
        Dit svar skal:
        - Skrives på dansk
        - Være 4-6 afsnit
        - Starte med en kort, ærlig vurdering af situationen
        - Identificere de 2-3 vigtigste steder at spare
        - Give konkrete og realistiske råd (ikke bare "spar mere")
        - Skelne mellem hvad der er let at ændre vs. hvad der kræver større ændringer
        - Have en opmuntrende tone uden at bagatellisere problemer
        - Undgå finansjargon og lister med bullets — skriv i naturlige afsnit
        """;

    public ClaudeFinancialAdvisorService(
        IConfiguration configuration,
        ILogger<ClaudeFinancialAdvisorService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"]
            ?? throw new InvalidOperationException("Missing Claude:ApiKey configuration");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string> GetAdviceAsync(
        ForecastViewModel forecast,
        ForwardBudgetViewModel budget,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(forecast, budget);

        _logger.LogInformation("Requesting financial advice from Claude");

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 1024,
            System = SystemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = prompt
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                return textBlock.Text;
        }

        return "Ingen analyse tilgængelig.";
    }

    private static string BuildPrompt(ForecastViewModel forecast, ForwardBudgetViewModel budget)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Her er en oversigt over familiens økonomi baseret på de seneste måneders banktransaktioner:");
        sb.AppendLine();

        // Monthly history
        sb.AppendLine("=== Månedlig økonomi (faktiske tal) ===");
        foreach (var m in forecast.History)
        {
            sb.AppendLine($"{m.Label}: Indkomst {m.Income:N0} kr, Udgifter {m.Expenses:N0} kr, Resultat {m.Net:+0;-0;0} kr");
        }
        sb.AppendLine();

        // Trend
        var incomeTrend = forecast.IncomeSlope >= 0 ? $"stiger +{forecast.IncomeSlope:N0} kr/md" : $"falder {forecast.IncomeSlope:N0} kr/md";
        var expenseTrend = forecast.ExpenseSlope >= 0 ? $"stiger +{forecast.ExpenseSlope:N0} kr/md" : $"falder {forecast.ExpenseSlope:N0} kr/md";
        sb.AppendLine($"Tendens: Indkomst {incomeTrend}. Udgifter {expenseTrend}.");
        sb.AppendLine($"Gennemsnitlig månedlig indkomst: {forecast.AvgIncome:N0} kr");
        sb.AppendLine($"Gennemsnitlig månedlige udgifter: {forecast.AvgExpenses:N0} kr");
        sb.AppendLine($"Gennemsnitligt månedligt overskud/underskud: {forecast.AvgIncome - forecast.AvgExpenses:+N0;-N0;0} kr");
        sb.AppendLine();

        // Fixed income
        sb.AppendLine("=== Fast indkomst ===");
        foreach (var inc in budget.IncomeItems)
            sb.AppendLine($"  {inc.Title}: {inc.ExpectedAmount:N0} kr/md");
        sb.AppendLine();

        // Fixed expenses
        sb.AppendLine("=== Faste udgifter (svære at ændre) ===");
        foreach (var exp in budget.FixedExpenses)
            sb.AppendLine($"  {exp.Title}: {exp.MonthlyAmount:N0} kr/md");
        sb.AppendLine();

        // Adjustable expenses
        sb.AppendLine("=== Påvirkelige udgifter (her kan spares) ===");
        foreach (var item in forecast.CuttableExpenses)
            sb.AppendLine($"  {item.DisplayName}: ca. {item.MonthlyAverage:N0} kr/md ({item.TransactionCount} transaktioner)");
        sb.AppendLine();

        // Projections
        if (forecast.Projected.Any())
        {
            sb.AppendLine("=== Prognose næste 3 måneder (baseret på tendens) ===");
            foreach (var p in forecast.Projected)
                sb.AppendLine($"{p.Label}: Indkomst {p.Income:N0} kr, Udgifter {p.Expenses:N0} kr, Resultat {p.Net:+0;-0;0} kr");
            sb.AppendLine();
        }

        sb.AppendLine("Giv mig en ærlig og konkret vurdering af situationen og dine bedste råd til, hvor vi kan forbedre os.");

        return sb.ToString();
    }
}
