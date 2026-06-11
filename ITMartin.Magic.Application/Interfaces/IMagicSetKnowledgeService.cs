using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicSetKnowledgeService
{
    Task<List<MagicSetKnowledge>> SearchBySymbolAsync(
        string symbolDescription,
        CancellationToken cancellationToken);

    Task<List<string>> GetOldFrameSetsAsync(
        CancellationToken cancellationToken);

    Task<List<string>> GetWhiteBorderSetsAsync(
        CancellationToken cancellationToken);
}