namespace ITMartinFamily.Application.Interfaces;

public interface IFamilyClaudeService
{
    Task<(string Name, string Location, bool Success)> ParseItemAsync(string input, CancellationToken ct = default);
    Task<(string Name, string Location, bool Success)> AnalyzePhotoAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
