using System.Text.Json;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.BackgroundJobs;

public sealed class ProcessMediaHandler
    : IBackgroundJobHandler
{
    private readonly CardScanWorkflowRunner _runner;
    private readonly ILogger<ProcessMediaHandler> _logger;

    public string JobType =>
        BackgroundJobTypes.ProcessMedia;

    public ProcessMediaHandler(
        CardScanWorkflowRunner runner,
        MagicDbContext db,
        ILogger<ProcessMediaHandler> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<CardScanJobPayload>(
                job.Payload);
        _logger.LogDebug("Processing card scan job — set: {SetCode}", payload?.SetCode);
        if (payload is null)
        {
            throw new InvalidOperationException(
                "Invalid payload.");
        }

        await _runner.ExecuteAsync(
            new CardScanContext
            {
                ImagePath = payload.ImagePath,
                SetCode = payload.SetCode
            },
            cancellationToken);
        
    }
}