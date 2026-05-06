using ITMartinR6Assistant.Domain.Entities;

namespace ITMartinR6Assistant.Application.Services;

public class RecommendationService
{
    private readonly IRecommendationRepository _repository;

    public RecommendationService(IRecommendationRepository repository)
    {
        _repository = repository;
    }

    public Task<List<string>> GetMaps()
    {
        return _repository.GetMaps();
    }

    public Task<List<SiteRecommendation>> GetRecommendations(string map)
    {
        return _repository.GetRecommendations(map);
    }
}