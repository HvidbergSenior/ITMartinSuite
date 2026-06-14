using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicKnowledgeService
{
    Task<MagicKnowledgeDashboardModel>
        GetDashboardAsync();

    Task<MagicSetKnowledge?>
        GetAsync(
            string setCode);

    Task UpdateAsync(MagicSetKnowledge set);
    
    Task<List<MagicSetSymbolDefinition>> GetSetDefinitionsAsync();
}