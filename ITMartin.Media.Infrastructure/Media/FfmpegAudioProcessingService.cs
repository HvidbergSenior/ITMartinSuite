using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegAudioProcessingService
    : FfmpegServiceBase,
        IAudioEnhancementService
{
    public async Task<string> ReduceNoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "audiodenoise");

        File.Copy(
            inputPath,
            outputPath,
            overwrite: true);

        return await Task.FromResult(
            outputPath);
    }

    public async Task<string> RemoveHumAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "humremoved");

        File.Copy(
            inputPath,
            outputPath,
            overwrite: true);

        return await Task.FromResult(
            outputPath);
    }

    public async Task<string> NormalizeAudioAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "normalized");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -af loudnorm \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> EnhanceSpeechAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "speech");

        File.Copy(
            inputPath,
            outputPath,
            overwrite: true);

        return await Task.FromResult(
            outputPath);
    }
}