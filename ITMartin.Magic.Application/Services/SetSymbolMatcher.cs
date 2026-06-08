using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Constants;

namespace ITMartin.Magic.Application.Services;

public static class SetSymbolMatcher
{
    public static IEnumerable<string>
        Match(
            MagicCardAnalysisResult result)
    {
        if (!result.SetSymbolVisible ||
            string.IsNullOrWhiteSpace(
                result.VisibleSetSymbolDescription))
        {
            return [];
        }

        return MagicSetSymbols.All
            .Where(x =>
                result.VisibleSetSymbolDescription
                    .Contains(
                        x.Description,
                        StringComparison
                            .OrdinalIgnoreCase))
            .Select(x =>
                x.SetCode);
    }
}