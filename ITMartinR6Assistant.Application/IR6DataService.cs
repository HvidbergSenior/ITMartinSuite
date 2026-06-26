using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Application;

public interface IR6DataService
{
    Task<R6GameData> GetData();
    Task<List<R6Map>> GetMaps();
    Task<R6Map?> GetMap(string name);
    Task<BombSite?> GetSite(string mapName, string siteName);
}
