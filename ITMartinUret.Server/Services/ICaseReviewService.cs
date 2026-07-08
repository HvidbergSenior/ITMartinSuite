namespace ITMartinUret.Server.Services;

public enum RiskLevel { None, Low, Medium, High }

public record RiskCheckResult(RiskLevel Level, List<string> Flags, string Explanation);

public interface ICaseReviewService
{
    Task<RiskCheckResult> CheckRiskAsync(string company, string body, CancellationToken ct = default);
    Task<string> RewriteAsFactualAsync(string body, CancellationToken ct = default);
    Task<string> SummarizeDocumentAsync(string company, byte[] fileBytes, string fileName, CancellationToken ct = default);
    Task<string> SuggestActionsAsync(string company, string body, CancellationToken ct = default);
}
