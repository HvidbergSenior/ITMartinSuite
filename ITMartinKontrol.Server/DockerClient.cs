using System.Net.Sockets;
using System.Text.Json;

namespace ITMartinKontrol.Server;

// Thin wrapper over the Docker Engine HTTP API, spoken over the unix socket
// that docker-compose.yaml mounts into this container. Deliberately no
// Docker.DotNet dependency - we only need three calls.
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

    public record ContainerState(string Name, string Status, bool Running, bool Exists);

    public async Task<ContainerState> InspectAsync(string name, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"containers/{name}/json", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new ContainerState(name, "missing", false, false);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var state = doc.RootElement.GetProperty("State");
        var status = state.GetProperty("Status").GetString() ?? "unknown";
        return new ContainerState(name, status, status == "running", true);
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
}
