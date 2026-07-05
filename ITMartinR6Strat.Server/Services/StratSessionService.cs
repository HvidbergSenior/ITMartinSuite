using System.Collections.Concurrent;
using ITMartinR6Strat.Server.Data;
using ITMartinR6Strat.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITMartinR6Strat.Server.Services;

public sealed class StratSessionService(IHubContext<StratHub> hub, StratAiService ai)
{
    private readonly ConcurrentDictionary<string, StratSession> _sessions = new();

    public StratSession Create(string[] mapPool, string hostToken)
    {
        var code = GenerateCode();
        var session = new StratSession { Code = code, HostToken = hostToken, MapPool = mapPool };
        _sessions[code] = session;
        return session;
    }

    public StratSession? Get(string code) =>
        _sessions.TryGetValue(code.ToUpper(), out var s) ? s : null;

    public async Task SetMapAsync(string code, string mapId)
    {
        var s = Require(code);
        s.SelectedMap = mapId;
        s.Step        = StratStep.WaitingSide;
        s.Plan        = null;
        s.SitePlanCache.Clear();
        s.Picks.Clear();
        await Broadcast(s);
    }

    public async Task SetSideAsync(string code, string side)
    {
        var s = Require(code);
        s.Side        = side;
        s.Step        = StratStep.BanPhase;
        s.SitePlanCache.Clear();
        s.Picks.Clear();
        await Broadcast(s);

        // Pre-generate all site plans in background so they're instant when site is selected
        _ = PreGeneratePlansAsync(s);
    }

    public async Task SetSiteAsync(string code, string site)
    {
        var s = Require(code);
        s.SelectedSite = site;
        s.Step         = StratStep.PickPhase;
        s.Picks.Clear();

        if (s.SitePlanCache.TryGetValue(site, out var cached))
        {
            s.Plan = cached;
            await Broadcast(s);
        }
        else
        {
            s.Generating = true;
            await Broadcast(s);
            s.Plan = await ai.GeneratePlanAsync(R6Data.MapName(s.SelectedMap!), s.Side!, site);
            s.Generating = false;
            await Broadcast(s);
        }
    }

    public async Task PickOperatorAsync(string code, string playerToken, int roleIndex, string operatorId)
    {
        var s = Require(code);
        s.Picks.RemoveAll(p => p.PlayerToken == playerToken);
        s.Picks.Add(new PlayerPick { PlayerToken = playerToken, RoleIndex = roleIndex, OperatorId = operatorId });
        await Broadcast(s);
    }

    public async Task ResetAsync(string code)
    {
        var s = Require(code);
        s.Step         = StratStep.WaitingMap;
        s.SelectedMap  = null;
        s.Side         = null;
        s.SelectedSite = null;
        s.Plan         = null;
        s.Generating   = false;
        s.SitePlanCache.Clear();
        s.Picks.Clear();
        await Broadcast(s);
    }

    private async Task PreGeneratePlansAsync(StratSession s)
    {
        if (s.SelectedMap is null || s.Side is null) return;
        var map = R6Data.FindMap(s.SelectedMap);
        if (map is null) return;

        await Parallel.ForEachAsync(map.Sites, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (site, ct) =>
        {
            var plan = await ai.GeneratePlanAsync(map.Name, s.Side!, site, ct);
            s.SitePlanCache[site] = plan;
        });
    }

    private async Task Broadcast(StratSession s) =>
        await hub.Clients.Group(s.Code).SendAsync("SessionUpdated");

    private StratSession Require(string code) =>
        _sessions.TryGetValue(code.ToUpper(), out var s)
            ? s : throw new InvalidOperationException("Session not found");

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 4)
            .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }
}
