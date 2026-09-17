using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.LibraryFinishing;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

// Config keys (Nas:Host, Nas:SshUser, Nas:SshKeyPath, Nas:RemoteLibraryBase,
// Nas:ComposeDir, Nas:ComposeFileName) default to this suite's actual home
// NAS - same values already proven working by hand throughout the
// 2026-09-06/07 sessions - but are real IConfiguration reads (same direct
// pattern LibraryPolishService already uses for Claude:ApiKey), overridable
// via Nas__Host etc. environment variables in docker-compose.yaml without a
// code change.
//
// Risk containment, since this is the one service in this suite that
// shells out to a shared production NAS other real family/business apps
// depend on:
//   - PushToNasAsync is additive-only: writes a new
//     /volume1/docker/filesorter/library/<slug>/ folder. Never deletes or
//     overwrites anything outside that one folder.
//   - WireGalleryAsync never round-trips the ~1000-line docker-compose.yaml
//     through a YAML library (which would reformat/reorder it) - it splices
//     exact new lines via GalleryComposeEditor, is idempotent (no-op if
//     already wired), and only pushes + restarts gallery-web if the file
//     actually changed.
//   - Every remote command runs with a real timeout (see RunProcessAsync) -
//     same "never a bare WaitForExit()" rule the runbook's ffmpeg/ffprobe
//     finding established, now applied to network calls, which can hang far
//     more easily than a local process.
public sealed class NasDeliveryService : INasDeliveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NasDeliveryService> _logger;

    public NasDeliveryService(IConfiguration configuration, ILogger<NasDeliveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string Host => _configuration["Nas:Host"] ?? "10.0.0.126";
    private string SshUser => _configuration["Nas:SshUser"] ?? "martinhvidberg";
    private string SshKeyPath => _configuration["Nas:SshKeyPath"] ?? "~/.ssh/id_ed25519";
    private string RemoteLibraryBase => _configuration["Nas:RemoteLibraryBase"] ?? "/volume1/docker/filesorter/library";
    private string RemoteTransferScratch => _configuration["Nas:RemoteTransferScratch"] ?? "/volume1/docker/martinsuite/tmp-transfer";
    // Fallback only - WireGalleryAsync asks the running gallery-web container which compose file it came from.
    private string ComposeDir => _configuration["Nas:ComposeDir"] ?? "/volume1/homes/MartinHvidberg/martinsuite-magic";
    private string ComposeFileName => _configuration["Nas:ComposeFileName"] ?? "docker-compose.yaml";

    public async Task<NasPushResult> PushToNasAsync(string libraryPath, string librarySlug, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath))
            return new NasPushResult { Success = false, Error = $"Local library path does not exist: {libraryPath}" };

        var remotePath = $"{RemoteLibraryBase}/{librarySlug}";
        var tempDir = Path.Combine(Path.GetTempPath(), $"nas-push-{librarySlug}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tarPath = Path.Combine(tempDir, "lib.tar.gz");

        try
        {
            // Local tar - no timeout drama here, it's the same machine's disk.
            //
            // --ignore-failed-read so one unreadable path cannot throw away an
            // entire delivery. Learned on ToshibaTest 2026-09-09: the USB drive
            // dropped off the bus mid-write and left 10 corrupt directories in
            // a generated SmartFolders/Lignende folder. tar aborted on them
            // after archiving ~70 GB, the whole archive was discarded, and the
            // delivery retried from scratch - even though every byte of real
            // content (Billeder, Videoer, Dokumenter, _Galleri) was readable
            // and verified fine. Skipping an unreadable file and still
            // delivering everything else is strictly better than delivering
            // nothing; the failure is still visible because tar's stderr is
            // logged below and the delivery verification runs afterwards.
            var tarResult = await RunProcessAsync("tar", $"--ignore-failed-read -czf \"{tarPath}\" -C \"{libraryPath}\" .", TimeSpan.FromHours(2), cancellationToken);
            if (tarResult.ExitCode != 0)
                return new NasPushResult { Success = false, Error = $"tar failed: {tarResult.StdErr}" };

            if (!string.IsNullOrWhiteSpace(tarResult.StdErr))
                _logger.LogWarning("tar reported unreadable paths while archiving {LibraryPath} (archive still created): {Errors}", libraryPath, tarResult.StdErr);

            // -O forces the legacy scp protocol - this Synology account
            // doesn't serve the SFTP-based one modern OpenSSH defaults to
            // (confirmed 2026-09-06, "subsystem request failed").
            var scpResult = await RunProcessAsync(
                "scp",
                $"-O -i \"{SshKeyPath}\" \"{tarPath}\" {SshUser}@{Host}:{RemoteTransferScratch}/",
                TimeSpan.FromHours(2), cancellationToken);
            if (scpResult.ExitCode != 0)
                return new NasPushResult { Success = false, Error = $"scp failed: {scpResult.StdErr}" };

            // Extract into a scratch dir the SSH user owns, then cp -r into
            // place - extracting a tarball straight onto the NAS target
            // fails (GNU tar tries to chmod() the pre-existing target
            // directory, "Operation not permitted" when it's not owned by
            // this account - confirmed 2026-09-06).
            var remoteExtractDir = $"{RemoteTransferScratch}/extract-{librarySlug}";
            var remoteScript =
                $"mkdir -p {remoteExtractDir} {remotePath} && " +
                $"tar -xzf {RemoteTransferScratch}/lib.tar.gz -C {remoteExtractDir} && " +
                $"cp -r {remoteExtractDir}/. {remotePath}/ && " +
                $"rm -rf {remoteExtractDir} {RemoteTransferScratch}/lib.tar.gz";

            var sshResult = await RunProcessAsync(
                "ssh", $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"{remoteScript}\"",
                TimeSpan.FromHours(2), cancellationToken);
            if (sshResult.ExitCode != 0)
                return new NasPushResult { Success = false, Error = $"remote extract/merge failed: {sshResult.StdErr}" };

            _logger.LogInformation("Pushed {LibraryPath} to {Host}:{RemotePath}", libraryPath, Host, remotePath);
            return new NasPushResult { Success = true, RemotePath = remotePath };
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort local cleanup */ }
        }
    }

    public async Task<GalleryWireResult> WireGalleryAsync(string librarySlug, string displayName, string password, CancellationToken cancellationToken = default)
    {
        // 1) Which compose file actually runs gallery-web? Ask the container,
        //    never trust a configured path: on 2026-09-17 the configured dir
        //    held a stale copy with a dead mount, and recreating from it took
        //    every gallery down. Docker records the file it was started from.
        var probe = await RunProcessAsync(
            "ssh", $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"docker inspect gallery-web --format '{{{{index .Config.Labels \\\"com.docker.compose.project.working_dir\\\"}}}}|{{{{index .Config.Labels \\\"com.docker.compose.project.config_files\\\"}}}}'\"",
            TimeSpan.FromMinutes(1), cancellationToken);
        var composeDir = ComposeDir;
        var composeRemotePath = $"{ComposeDir}/{ComposeFileName}";
        if (probe.ExitCode == 0 && probe.StdOut.Contains('|'))
        {
            var parts = probe.StdOut.Trim().Split('|', 2);
            if (parts[0].Length > 1 && parts[1].Length > 1) { composeDir = parts[0]; composeRemotePath = parts[1].Split(',')[0]; }
        }
        else
        {
            return new GalleryWireResult { Success = false, Error = "gallery-web is not running on the NAS - cannot tell which compose file to edit. Start it first." };
        }

        var catResult = await RunProcessAsync(
            "ssh", $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"cat {composeRemotePath}\"",
            TimeSpan.FromMinutes(2), cancellationToken);
        if (catResult.ExitCode != 0)
            return new GalleryWireResult { Success = false, Error = $"could not read {composeRemotePath}: {catResult.StdErr}" };

        var containerPath = $"/library/{librarySlug}";
        var hostPath = $"{RemoteLibraryBase}/{librarySlug}";

        GalleryComposeEditor.AddGalleryResult edit;
        try
        {
            edit = GalleryComposeEditor.AddGallery(catResult.StdOut, librarySlug, displayName, containerPath, hostPath, password);
        }
        catch (Exception ex)
        {
            return new GalleryWireResult { Success = false, Error = $"could not edit compose file: {ex.Message}" };
        }

        if (edit.AlreadyWired)
        {
            _logger.LogInformation("Gallery {Slug} already wired in {ComposeFile} - nothing to do", librarySlug, composeRemotePath);
            return new GalleryWireResult { Success = true, AlreadyWired = true, AssignedIndex = edit.AssignedIndex };
        }

        // 2) Every host path gallery-web mounts must exist, or the recreate
        //    fails and the container stays dead. Check them all up front.
        var mounts = GalleryWebHostMounts(edit.Yaml);
        if (mounts.Count > 0)
        {
            var check = string.Join(" ", mounts.Select(m => $"[ -e '{m}' ] || echo MISSING {m};"));
            var mountResult = await RunProcessAsync("ssh", $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"{check}\"", TimeSpan.FromMinutes(1), cancellationToken);
            var missing = mountResult.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.StartsWith("MISSING")).ToList();
            if (missing.Count > 0)
                return new GalleryWireResult { Success = false, Error = "refusing to restart gallery-web: mounted paths do not exist on the NAS - " + string.Join("; ", missing) };
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"compose-{Guid.NewGuid():N}.yaml");
        var backupRemote = $"{composeRemotePath}.bak-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        try
        {
            await File.WriteAllTextAsync(tempFile, edit.Yaml, cancellationToken);

            // 3) Back up, then replace.
            await RunProcessAsync("ssh", $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"cp {composeRemotePath} {backupRemote}\"", TimeSpan.FromMinutes(1), cancellationToken);
            var scpResult = await RunProcessAsync(
                "scp", $"-O -i \"{SshKeyPath}\" \"{tempFile}\" {SshUser}@{Host}:{composeRemotePath}",
                TimeSpan.FromMinutes(5), cancellationToken);
            if (scpResult.ExitCode != 0)
                return new GalleryWireResult { Success = false, Error = $"scp of updated compose file failed: {scpResult.StdErr}" };

            // 4) Recreate; if that fails, put the backup back and recreate
            //    again so the galleries are never left down.
            var restartResult = await RunProcessAsync(
                "ssh",
                $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"cd {composeDir} && docker compose up -d --force-recreate --timeout 10 gallery-web && sleep 3 && docker ps --filter name=^gallery-web$ --format '{{{{.Status}}}}'\"",
                TimeSpan.FromMinutes(5), cancellationToken);
            var up = restartResult.ExitCode == 0 && restartResult.StdOut.Contains("Up");
            if (!up)
            {
                _logger.LogError("gallery-web did not come up after wiring {Slug} - rolling back {ComposeFile} from {Backup}", librarySlug, composeRemotePath, backupRemote);
                await RunProcessAsync("ssh",
                    $"-i \"{SshKeyPath}\" {SshUser}@{Host} \"cp {backupRemote} {composeRemotePath} && cd {composeDir} && docker compose up -d --force-recreate --timeout 10 gallery-web\"",
                    TimeSpan.FromMinutes(5), cancellationToken);
                return new GalleryWireResult { Success = false, Error = $"gallery-web restart failed and the compose file was rolled back: {restartResult.StdErr}" };
            }

            _logger.LogInformation("Wired gallery {Slug} into {ComposeFile} as index {Index}, restarted gallery-web (backup {Backup})", librarySlug, composeRemotePath, edit.AssignedIndex, backupRemote);
            return new GalleryWireResult { Success = true, AlreadyWired = false, AssignedIndex = edit.AssignedIndex };
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort local cleanup */ }
        }
    }

    // Host-side paths in the gallery-web service's volumes: block ("/volume1/...:/library/x:ro").
    public static List<string> GalleryWebHostMounts(string composeYaml)
    {
        var result = new List<string>();
        var inGallery = false; var inVolumes = false;
        foreach (var raw in composeYaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("  ") && !line.StartsWith("   ") && line.TrimEnd().EndsWith(':'))
            {
                inGallery = line.Trim() == "gallery-web:";
                inVolumes = false;
                continue;
            }
            if (!inGallery) continue;
            var t = line.Trim();
            if (t == "volumes:") { inVolumes = true; continue; }
            if (inVolumes)
            {
                if (!t.StartsWith("- ")) { if (t.Length > 0 && !line.StartsWith("      ")) inVolumes = false; continue; }
                var spec = t[2..].Trim().Trim('"', '\'');
                var host = spec.Split(':')[0];
                if (host.StartsWith('/')) result.Add(host);
            }
        }
        return result;
    }

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr);

    // Every network call here has a real timeout and gets killed on expiry -
    // the same "never a bare WaitForExit()" rule the runbook's ffmpeg/
    // ffprobe finding established for local process calls applies at least
    // as hard to ssh/scp against a remote NAS over the network.
    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{FileName} {Arguments}", fileName, RedactPasswords(arguments));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ProcessResult(-1, "", $"{fileName} timed out after {timeout}");
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;
        return new ProcessResult(process.ExitCode, stdOut, stdErr);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* best effort */ }
    }

    // Gallery passwords flow through ssh/scp command-line arguments - never
    // logged in plain text, same discipline as every other secret in this
    // suite.
    private static string RedactPasswords(string arguments) =>
        System.Text.RegularExpressions.Regex.Replace(arguments, @"(Galleries__\d+__Password=)[^\s""]+", "$1***");
}
