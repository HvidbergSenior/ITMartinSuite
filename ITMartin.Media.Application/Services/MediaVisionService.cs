using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class MediaVisionService
    : IMediaVisionService
{
    private readonly IAiAnalysisService _aiAnalysisService;

    private readonly IAiCacheService _cacheService;

    public MediaVisionService(
        IAiAnalysisService aiAnalysisService,
        IAiCacheService cacheService)
    {
        _aiAnalysisService = aiAnalysisService;
        _cacheService = cacheService;
    }

    public async Task ProcessAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null)
    {
        var images =
            files
                .Where(x =>
                    x.Type == MediaType.Image)
                .ToList();

        int total = images.Count;

        int done = 0;

        foreach (var file in images)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(file.Hash))
                    continue;

                var cached =
                    await _cacheService
                        .GetAsync(file.Hash);

                if (cached != null)
                {
                    file.AiDescription =
                        cached.Description;

                    file.AiTags =
                        cached.Tags;

                    file.AiConfidence =
                        (float?)cached.Confidence;

                    file.AiProcessed = true;

                    Console.WriteLine(
                        $"AI CACHE HIT: {file.FileName}");
                }
                else
                {
                    var path =
                        file.NormalizedPath ??
                        file.FullPath;

                    var result =
                        await _aiAnalysisService
                            .AnalyzeImageAsync(path);

                    file.AiDescription =
                        result.Description;

                    file.AiTags =
                        result.Tags;

                    file.AiConfidence =
                        (float?)result.Confidence;

                    file.AiProcessed = true;

                    await _cacheService.SaveAsync(
                        file.Hash,
                        result);

                    Console.WriteLine(
                        $"AI VISION DONE: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"AI VISION ERROR: {ex}");
            }

            done++;

            if (progress != null)
            {
                await progress(
                    done,
                    total,
                    file.FileName);
            }
        }
    }
}