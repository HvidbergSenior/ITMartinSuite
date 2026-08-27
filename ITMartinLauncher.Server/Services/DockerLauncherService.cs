using System.Diagnostics;

namespace ITMartinLauncher.Server.Services;

public enum LaunchStatus { Stopped, Starting, Running, Error }

public sealed class DockerLauncherService(IHttpClientFactory httpClientFactory, ILogger<DockerLauncherService> logger)
{
    // Tracks "we told Docker to start this" separately from "the app is
    // actually answering health checks yet" - Docker reports a container
    // running the instant its process starts, well before an ASP.NET app
    // inside it has finished booting and can serve a request.
    private readonly HashSet<string> _starting = [];
    private readonly object _lock = new();

    public async Task<LaunchStatus> GetStatusAsync(LaunchableApp app, CancellationToken cancellationToken)
    {
        var isRunning = await IsContainerRunningAsync(app.ContainerName, cancellationToken);
        if (!isRunning) return LaunchStatus.Stopped;

        lock (_lock)
        {
            if (!_starting.Contains(app.ContainerName)) return LaunchStatus.Running;
        }

        var healthy = await IsHealthyAsync(app.HealthCheckUrl, cancellationToken);
        if (healthy)
        {
            lock (_lock) { _starting.Remove(app.ContainerName); }
            return LaunchStatus.Running;
        }

        return LaunchStatus.Starting;
    }

    public async Task StartAsync(LaunchableApp app, CancellationToken cancellationToken)
    {
        lock (_lock) { _starting.Add(app.ContainerName); }

        var (exitCode, output) = await RunDockerAsync($"start {app.ContainerName}", cancellationToken);
        if (exitCode != 0)
        {
            logger.LogError("docker start {Container} failed: {Output}", app.ContainerName, output);
            lock (_lock) { _starting.Remove(app.ContainerName); }
            throw new InvalidOperationException($"Kunne ikke starte {app.Name}: {output}");
        }
    }

    public async Task StopAsync(LaunchableApp app, CancellationToken cancellationToken)
    {
        await RunDockerAsync($"stop {app.ContainerName}", cancellationToken);
        lock (_lock) { _starting.Remove(app.ContainerName); }
    }

    private async Task<bool> IsContainerRunningAsync(string containerName, CancellationToken cancellationToken)
    {
        var (exitCode, output) = await RunDockerAsync(
            $"inspect -f {{{{.State.Running}}}} {containerName}", cancellationToken);
        return exitCode == 0 && output.Trim() == "true";
    }

    private async Task<bool> IsHealthyAsync(string healthCheckUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync(healthCheckUrl, cancellationToken);
            return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string Output)> RunDockerAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
    }
}
