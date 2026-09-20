using System.Net.Sockets;
using System.Text.Json;

namespace ITMartinKontrol.Server;

// Thin wrapper over the Docker Engine HTTP API, spoken over the unix socket
// that docker-compose.yaml mounts into this container. Deliberately no
// Docker.DotNet dependency - we only need a handful of calls.
public sealed class DockerClient
{
    private readonly HttpClient _http;

    public DockerClient(IConfiguration cfg)
    {
        var socket = cfg["Kontrol:DockerSocket"] ?? "/var/run/docker.sock";
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await s.ConnectAsync(new UnixDomainSocketEndPoint(socket), ct);
                return new NetworkStream(s, ownsSocket: true);
            }
        };
        // Host is ignored by the socket connect but HttpClient needs an absolute URI.
        _http = new HttpClient(handler) { BaseAddress = new Uri("http://docker/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    public record ContainerInfo(string Name, string Status, bool Running, string Image, string Ports, DateTime? StartedAt);
    public record ContainerUsage(double CpuPct, double MemMb, double MemLimitMb);

    public async Task<List<ContainerInfo>> ListAllAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("containers/json?all=1", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<ContainerInfo>();
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var name = c.GetProperty("Names")[0].GetString()?.TrimStart('/') ?? "";
            var state = c.GetProperty("State").GetString() ?? "unknown";
            var ports = c.TryGetProperty("Ports", out var p)
                ? string.Join(", ", p.EnumerateArray()
                    .Where(x => x.TryGetProperty("PublicPort", out _))
                    .Select(x => x.GetProperty("PublicPort").GetInt32().ToString()).Distinct())
                : "";
            list.Add(new ContainerInfo(name, c.GetProperty("Status").GetString() ?? state, state == "running",
                c.GetProperty("Image").GetString() ?? "", ports, null));
        }
        return list.OrderBy(x => x.Name).ToList();
    }

    // One-shot stats. CPU% is computed the way `docker stats` does it.
    public async Task<ContainerUsage?> StatsAsync(string name, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"containers/{name}/stats?stream=false&one-shot=false", ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var r = doc.RootElement;
            var cpu = r.GetProperty("cpu_stats");
            var pre = r.GetProperty("precpu_stats");
            double cpuDelta = cpu.GetProperty("cpu_usage").GetProperty("total_usage").GetDouble()
                            - pre.GetProperty("cpu_usage").GetProperty("total_usage").GetDouble();
            double sysDelta = (cpu.TryGetProperty("system_cpu_usage", out var s1) ? s1.GetDouble() : 0)
                            - (pre.TryGetProperty("system_cpu_usage", out var s0) ? s0.GetDouble() : 0);
            double cpus = cpu.TryGetProperty("online_cpus", out var oc) ? oc.GetDouble() : 1;
            var pct = sysDelta > 0 && cpuDelta > 0 ? cpuDelta / sysDelta * cpus * 100.0 : 0;

            var mem = r.GetProperty("memory_stats");
            double usage = mem.TryGetProperty("usage", out var u) ? u.GetDouble() : 0;
            // Exclude page cache like `docker stats` does (cgroup v2: inactive_file; v1: cache).
            if (mem.TryGetProperty("stats", out var st))
            {
                if (st.TryGetProperty("inactive_file", out var inf)) usage -= inf.GetDouble();
                else if (st.TryGetProperty("cache", out var cache)) usage -= cache.GetDouble();
            }
            double limit = mem.TryGetProperty("limit", out var l) ? l.GetDouble() : 0;
            return new ContainerUsage(Math.Round(pct, 1), Math.Round(Math.Max(0, usage) / 1048576, 1), Math.Round(limit / 1048576, 0));
        }
        catch { return null; }
    }

    // Docker answers 204 on success and 304 if already in the requested state;
    // both mean "done" from the caller's point of view.
    public async Task<bool> StartAsync(string name, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync($"containers/{name}/start", null, ct);
        return resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotModified;
    }

    public async Task<bool> StopAsync(string name, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync($"containers/{name}/stop?t=10", null, ct);
        return resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotModified;
    }

    public async Task<bool> RestartAsync(string name, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsync($"containers/{name}/restart?t=10", null, ct);
        return resp.IsSuccessStatusCode;
    }
}
