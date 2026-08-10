using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartin.IndexServer.Services;

public sealed record LauncherApp(string Name, string ProjectPath, string? Url, bool HasLaunchProfile);

public sealed record LauncherAppStatus(string Name, string ProjectPath, string? Url, bool HasLaunchProfile, bool Running, int? Pid);

// Dev-only: lets this same portal, when run locally (never when deployed to
// the NAS - gated by IsDevelopment() in Program.cs), start/stop any other
// app in the solution as a plain `dotnet run` child process. Repo root is
// auto-detected (walk up from this app's own folder to the .sln) rather than
// hardcoded, so this works unmodified on any machine the repo is cloned to.
public sealed class LocalLauncherService
{
    private static readonly Regex ProjectLineRegex = new(
        """^Project\("\{[0-9A-Fa-f-]+\}"\)\s*=\s*"([^"]+)",\s*"([^"]+\.csproj)",""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly ConcurrentDictionary<string, Process> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<string> _repoRoot;
    private readonly Lazy<List<LauncherApp>> _apps;

    public LocalLauncherService()
    {
        _repoRoot = new Lazy<string>(FindRepoRoot);
        _apps = new Lazy<List<LauncherApp>>(DiscoverApps);
    }

    public string RepoRoot => _repoRoot.Value;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        // Fallback for `dotnet run` (bin/Debug/net8.0 is deep under the repo) -
        // same upward walk from the current working directory instead.
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the .sln file by walking up from the running executable.");
    }

    private List<LauncherApp> DiscoverApps()
    {
        var slnPath = Directory.GetFiles(RepoRoot, "*.sln").First();
        var slnText = File.ReadAllText(slnPath);
        var apps = new List<LauncherApp>();

        foreach (Match m in ProjectLineRegex.Matches(slnText))
        {
            var name = m.Groups[1].Value;
            var relPath = m.Groups[2].Value.Replace('\\', Path.DirectorySeparatorChar);
            var fullCsprojPath = Path.Combine(RepoRoot, relPath);
            if (!File.Exists(fullCsprojPath)) continue;

            // Only runnable web apps have a Program.cs sibling worth launching -
            // this also naturally excludes class libraries/test projects, which
            // .sln lists identically to Web projects with no way to tell apart
            // except by trying to find their own launchSettings.json.
            var projectDir = Path.GetDirectoryName(fullCsprojPath)!;
            var launchSettingsPath = Path.Combine(projectDir, "Properties", "launchSettings.json");
            var url = TryReadLaunchUrl(launchSettingsPath);

            apps.Add(new LauncherApp(name, fullCsprojPath, url, url is not null));
        }

        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? TryReadLaunchUrl(string launchSettingsPath)
    {
        if (!File.Exists(launchSettingsPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
            if (!doc.RootElement.TryGetProperty("profiles", out var profiles)) return null;

            // Prefer "http" (matches how every app in this suite is actually run
            // locally per CLAUDE.md/project convention), else whatever's first.
            JsonElement? chosen = null;
            if (profiles.TryGetProperty("http", out var httpProfile)) chosen = httpProfile;
            else foreach (var p in profiles.EnumerateObject()) { chosen = p.Value; break; }

            if (chosen is null || !chosen.Value.TryGetProperty("applicationUrl", out var urlProp)) return null;

            // "https://localhost:X;http://localhost:Y" - take the first URL.
            return urlProp.GetString()?.Split(';').FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public List<LauncherAppStatus> GetStatuses()
    {
        CleanupExited();
        return _apps.Value.Select(a =>
        {
            var running = _running.TryGetValue(a.Name, out var proc);
            return new LauncherAppStatus(a.Name, a.ProjectPath, a.Url, a.HasLaunchProfile, running, running ? proc!.Id : null);
        }).ToList();
    }

    public bool Start(string name)
    {
        var app = _apps.Value.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (app is null || !app.HasLaunchProfile) return false;
        if (_running.ContainsKey(app.Name)) return true; // already running

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{app.ProjectPath}\" --launch-profile http",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot,
        };
        // Every app here is meant to run locally the same way a developer
        // would from a terminal - Development so it picks up local dev
        // conventions (matches how this launcher itself is gated).
        psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += (_, _) => _running.TryRemove(app.Name, out _);

        if (!process.Start()) return false;
        _running[app.Name] = process;
        return true;
    }

    public bool Stop(string name)
    {
        if (!_running.TryRemove(name, out var process)) return false;
        try
        {
            // A plain Kill() on the `dotnet run` wrapper can leave its actual
            // apphost child process running and still holding the port (seen
            // firsthand with FileSorter.Server this session) - kill the whole
            // tree instead.
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited or otherwise gone - fine, that's the goal either way.
        }
        return true;
    }

    private void CleanupExited()
    {
        foreach (var (name, proc) in _running)
        {
            try
            {
                if (proc.HasExited) _running.TryRemove(name, out _);
            }
            catch
            {
                _running.TryRemove(name, out _);
            }
        }
    }
}
