using ITMartinAdhd.Application.Models;

namespace ITMartinAdhd.Application.Interfaces;

public interface IAdhdClaudeService
{
    Task<ParsedItemModel> ParseNaturalLanguageAsync(string input);
}
