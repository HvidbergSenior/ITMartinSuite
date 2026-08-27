using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package4.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

namespace ITMartinVlog.Server.Services;

// Every version of a clip a user produces (original + each effect/optimize
// pass) is kept as its own file under {source folder}/.vlogstudio/{clip name}/
// so nothing is ever silently overwritten - versions can be played, compared,
// and deleted individually, matching the "alle versioner skal gemmes" requirement.
public sealed record VlogVersion(string Id, string Label, string FilePath, DateTimeOffset CreatedAt);

public sealed class VlogEffectOptions
{
    public bool WhiteBalance { get; init; }
    public bool ExposureContrast { get; init; }
    public bool SaturationVibrance { get; init; }
    public bool ColorGrade { get; init; }
    public bool Sharpen { get; init; }
    public bool NoiseReduction { get; init; }
    public bool Deflicker { get; init; }

    public bool AudioNoiseReduction { get; init; }
    public bool WindNoiseReduction { get; init; }
    public bool HumRemoval { get; init; }
    public bool AudioEq { get; init; }
    public bool DeEss { get; init; }
    public bool AudioCompression { get; init; }
    public bool LoudnessNormalization { get; init; }

    public static VlogEffectOptions Optimize() => new()
    {
        WhiteBalance = true,
        ExposureContrast = true,
        Deflicker = true,
        NoiseReduction = true,
        AudioNoiseReduction = true,
        HumRemoval = true,
        WindNoiseReduction = true,
        LoudnessNormalization = true
    };
}

public sealed class VlogEditorService
{
    private readonly Package4WorkflowOrchestrator _orchestrator;
    private readonly Package4WorkflowRunner _runner;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public VlogEditorService(Package4WorkflowOrchestrator orchestrator, Package4WorkflowRunner runner)
    {
        _orchestrator = orchestrator;
        _runner = runner;
    }

    public static string WorkspaceRoot(string sourceFilePath) =>
        Path.Combine(
            Path.GetDirectoryName(sourceFilePath) ?? ".",
            ".vlogstudio",
            Path.GetFileNameWithoutExtension(sourceFilePath));

    public List<VlogVersion> LoadVersions(string sourceFilePath)
    {
        var root = WorkspaceRoot(sourceFilePath);
        var manifestPath = Path.Combine(root, "versions.json");

        if (!File.Exists(manifestPath))
        {
            Directory.CreateDirectory(root);
            var originalCopy = Path.Combine(root, "v0-original" + Path.GetExtension(sourceFilePath));
            if (!File.Exists(originalCopy))
            {
                File.Copy(sourceFilePath, originalCopy);
            }

            var seeded = new List<VlogVersion> { new("v0", "Original", originalCopy, DateTimeOffset.UtcNow) };
            SaveVersions(sourceFilePath, seeded);
            return seeded;
        }

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<List<VlogVersion>>(json) ?? [];
    }

    public void SaveVersions(string sourceFilePath, List<VlogVersion> versions)
    {
        var root = WorkspaceRoot(sourceFilePath);
        Directory.CreateDirectory(root);
        var manifestPath = Path.Combine(root, "versions.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(versions, JsonOptions));
    }

    // For results produced outside the Package4 workflow (currently just the
    // "Udtræk lyd" direct-ffmpeg action) - registers an already-written file
    // as a version so it shows up in the same history/delete list as effect runs.
    public VlogVersion RegisterExternalVersion(string sourceFilePath, string label, string producedFilePath)
    {
        var root = WorkspaceRoot(sourceFilePath);
        var versions = LoadVersions(sourceFilePath);
        var newId = "v" + versions.Count;
        var destFile = Path.Combine(root, $"{newId}-{Sanitize(label)}{Path.GetExtension(producedFilePath)}");
        File.Copy(producedFilePath, destFile, overwrite: true);

        var newVersion = new VlogVersion(newId, label, destFile, DateTimeOffset.UtcNow);
        versions.Add(newVersion);
        SaveVersions(sourceFilePath, versions);
        return newVersion;
    }

    public void DeleteVersion(string sourceFilePath, string versionId)
    {
        if (versionId == "v0") return;

        var versions = LoadVersions(sourceFilePath);
        var version = versions.FirstOrDefault(v => v.Id == versionId);
        if (version is null) return;

        versions.Remove(version);
        SaveVersions(sourceFilePath, versions);

        try { File.Delete(version.FilePath); } catch { /* best effort */ }
    }

    public async Task<VlogVersion> ApplyEffectsAsync(
        string sourceFilePath,
        string currentVersionFilePath,
        string label,
        VlogEffectOptions options,
        CancellationToken cancellationToken)
    {
        var root = WorkspaceRoot(sourceFilePath);
        var runFolder = Path.Combine(root, "runs", Guid.NewGuid().ToString("N"));
        var inputFolder = Path.Combine(runFolder, "input");
        Directory.CreateDirectory(inputFolder);

        var stagedInputName = "clip" + Path.GetExtension(currentVersionFilePath);
        var stagedInput = Path.Combine(inputFolder, stagedInputName);
        File.Copy(currentVersionFilePath, stagedInput);

        var request = new StartPackage4Request
        {
            SourceLibraryPath = inputFolder,
            WorkingDirectory = runFolder,

            EnableWhiteBalance = options.WhiteBalance,
            EnableExposureContrast = options.ExposureContrast,
            EnableSaturationVibrance = options.SaturationVibrance,
            EnableColorGrade = options.ColorGrade,
            EnableSharpen = options.Sharpen,
            EnableNoiseReduction = options.NoiseReduction,
            EnableDeflicker = options.Deflicker,
            EnableStabilization = false,
            EnableStabilizedCrop = false,

            EnableAudioNoiseReduction = options.AudioNoiseReduction,
            EnableWindNoiseReduction = options.WindNoiseReduction,
            EnableHumRemoval = options.HumRemoval,
            EnableAudioEq = options.AudioEq,
            EnableDeEss = options.DeEss,
            EnableAudioCompression = options.AudioCompression,
            EnableLoudnessNormalization = options.LoudnessNormalization,

            EnableTrim = false
        };

        var result = await _orchestrator.StartAsync(request, cancellationToken);
        await _runner.ExecuteAsync(result.WorkflowId, result.State, cancellationToken);

        var item = result.State.Items.FirstOrDefault();
        if (item is null || item.Failed || item.EnhancedOutputPath is null)
        {
            throw new InvalidOperationException(item?.FailureReason ?? "Effekten fejlede - intet output blev produceret.");
        }

        var versions = LoadVersions(sourceFilePath);
        var newId = "v" + versions.Count;
        var destFile = Path.Combine(root, $"{newId}-{Sanitize(label)}.mp4");
        File.Copy(item.EnhancedOutputPath, destFile, overwrite: true);

        var newVersion = new VlogVersion(newId, label, destFile, DateTimeOffset.UtcNow);
        versions.Add(newVersion);
        SaveVersions(sourceFilePath, versions);

        try { Directory.Delete(runFolder, recursive: true); } catch { /* best effort cleanup */ }

        return newVersion;
    }

    private static string Sanitize(string label)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(label.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return clean.Length > 40 ? clean[..40] : clean;
    }
}
