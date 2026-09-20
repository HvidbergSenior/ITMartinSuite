using System.Text.Json;

namespace ITMartinKontrol.Server;

// Host-level numbers. CPU and RAM come from the host's /proc when compose
// mounts it at /host/proc (read-only); disk from whatever path
// Kontrol:DiskPath points at. Without the mounts we fall back to the sum of
// container CPU and Docker's memory limit, so the page still works.
public sealed record HostUsage(double CpuPct, double MemUsedMb, double MemTotalMb, double DiskUsedGb, double DiskTotalGb, string Source);

public sealed record Sample(DateTime T, double Cpu, double Mem, double Disk, Dictionary<string, double[]> C); // C[name] = [cpu%, memMb]

public sealed class Sampler(DockerClient docker, IConfiguration cfg, ILogger<Sampler> log) : BackgroundService
{
    private readonly string _proc = cfg["Kontrol:HostProc"] ?? "/host/proc";
    private readonly string _disk = cfg["Kontrol:DiskPath"] ?? "/host/disk";
    private readonly string _file = Path.Combine(cfg["Kontrol:DataDir"] ?? "/data", "samples.json");
    private const int Keep = 48 * 60; // 1-minute samples, 48 h
    private readonly object _gate = new();
    private List<Sample> _samples = [];
    private (double idle, double total)? _lastCpu;

    public HostUsage LastHost { get; private set; } = new(0, 0, 0, 0, 0, "none");
    public Dictionary<string, DockerClient.ContainerUsage> LastContainers { get; private set; } = [];
    public DateTime LastAt { get; private set; }

    public List<Sample> History() { lock (_gate) return _samples.ToList(); }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (File.Exists(_file))
                _samples = JsonSerializer.Deserialize<List<Sample>>(File.ReadAllText(_file)) ?? [];
        }
        catch (Exception ex) { log.LogWarning(ex, "samples.json unreadable, starting empty"); }

        // Prime the /proc/stat delta so the first real sample has a baseline.
        ReadHostCpu();
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var n = 0;
        while (!ct.IsCancellationRequested)
        {
            try { await TakeSampleAsync(ct); n++; if (n % 5 == 0) Persist(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { log.LogWarning(ex, "sample failed"); }
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
        Persist();
    }

    public async Task TakeSampleAsync(CancellationToken ct)
    {
        var containers = await docker.ListAllAsync(ct);
        var usage = new Dictionary<string, DockerClient.ContainerUsage>();
        foreach (var c in containers.Where(c => c.Running))
            if (await docker.StatsAsync(c.Name, ct) is { } u) usage[c.Name] = u;

        var host = ReadHost(usage);
        LastHost = host;
        LastContainers = usage;
        LastAt = DateTime.UtcNow;

        var s = new Sample(DateTime.UtcNow, host.CpuPct, host.MemUsedMb, host.DiskUsedGb,
            usage.ToDictionary(k => k.Key, v => new[] { v.Value.CpuPct, v.Value.MemMb }));
        lock (_gate)
        {
            _samples.Add(s);
            if (_samples.Count > Keep) _samples.RemoveRange(0, _samples.Count - Keep);
        }
    }

    private HostUsage ReadHost(Dictionary<string, DockerClient.ContainerUsage> usage)
    {
        double? cpu = ReadHostCpu();
        var (memUsed, memTotal) = ReadHostMem();
        var (diskUsed, diskTotal) = ReadDisk();
        var src = cpu is null ? "docker" : "proc";
        cpu ??= Math.Round(usage.Values.Sum(u => u.CpuPct) / Math.Max(1, Environment.ProcessorCount), 1);
        if (memTotal <= 0)
        {
            memTotal = usage.Values.Select(u => u.MemLimitMb).DefaultIfEmpty(0).Max();
            memUsed = usage.Values.Sum(u => u.MemMb);
        }
        return new HostUsage(Math.Round(cpu.Value, 1), Math.Round(memUsed, 0), Math.Round(memTotal, 0), Math.Round(diskUsed, 1), Math.Round(diskTotal, 1), src);
    }

    private double? ReadHostCpu()
    {
        try
        {
            var line = File.ReadLines(Path.Combine(_proc, "stat")).FirstOrDefault(l => l.StartsWith("cpu "));
            if (line is null) return null;
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(double.Parse).ToArray();
            var idle = f[3] + (f.Length > 4 ? f[4] : 0);
            var total = f.Sum();
            var prev = _lastCpu;
            _lastCpu = (idle, total);
            if (prev is null) return null;
            var dt = total - prev.Value.total;
            return dt <= 0 ? 0 : (1 - (idle - prev.Value.idle) / dt) * 100.0;
        }
        catch { return null; }
    }

    private (double used, double total) ReadHostMem()
    {
        try
        {
            double total = 0, avail = 0;
            foreach (var l in File.ReadLines(Path.Combine(_proc, "meminfo")))
            {
                if (l.StartsWith("MemTotal:")) total = Kb(l);
                else if (l.StartsWith("MemAvailable:")) avail = Kb(l);
            }
            return total > 0 ? ((total - avail) / 1024, total / 1024) : (0, 0);
        }
        catch { return (0, 0); }
        static double Kb(string l) => double.Parse(l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
    }

    private (double used, double total) ReadDisk()
    {
        try
        {
            if (!Directory.Exists(_disk)) return (0, 0);
            var d = new DriveInfo(_disk);
            return ((d.TotalSize - d.AvailableFreeSpace) / 1073741824.0, d.TotalSize / 1073741824.0);
        }
        catch { return (0, 0); }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            List<Sample> copy; lock (_gate) copy = _samples.ToList();
            File.WriteAllText(_file + ".tmp", JsonSerializer.Serialize(copy));
            File.Move(_file + ".tmp", _file, overwrite: true);
        }
        catch (Exception ex) { log.LogWarning(ex, "persist failed"); }
    }
}
