using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegAudioProcessingService
    : FfmpegServiceBase,
        IAudioEnhancementService
{
    public async Task<string> ReduceNoiseAsync(
        string inputPath,
        RestorationProfile profile,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "audiodenoise");

        var filter =
            profile switch
            {
                RestorationProfile.VHSAggressive
                    => "highpass=f=120,afftdn=nr=24",

                RestorationProfile.FamilyArchive
                    => "highpass=f=80,afftdn=nr=12",

                _ => "afftdn"
            };

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -af \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
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