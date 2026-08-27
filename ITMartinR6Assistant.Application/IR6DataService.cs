using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Application;

public interface IR6DataService
{
    Task<R6GameData> GetData();
    Task<List<R6Map>> GetMaps();
    Task<R6Map?> GetMap(string name);
    Task<BombSite?> GetSite(string mapName, string siteName);

    // Persists the whole object back to disk and notifies subscribers - used
    // by the in-app editor so operator/map/battle-plan changes are shared
    // live with everyone and survive a restart, same as TeamSettings.
    Task SaveAsync(R6GameData data);
    event Action? OnDataChanged;
}
