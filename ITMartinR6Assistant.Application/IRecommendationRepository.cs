
using ITMartinR6Assistant.Domain.Entities;

namespace ITMartinR6Assistant.Application;

public interface IRecommendationRepository
{
    Task<List<string>> GetMaps();

    Task<List<SiteRecommendation>> GetRecommendations(string map);
}