using ITMartinAdhd.Application.Models;

namespace ITMartinAdhd.Application.Interfaces;

public interface IAdhdClaudeService
{
    Task<ParsedItemModel> ParseNaturalLanguageAsync(string input);
    Task<ParsedItemModel> AnalyzeItemPhotoAsync(byte[] imageBytes, string mimeType);
}
