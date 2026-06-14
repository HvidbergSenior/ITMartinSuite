using System.Text.Json;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Magic.Infrastructure.BackgroundJobs;

public sealed class ProcessMediaHandler
    : IBackgroundJobHandler
{
    private readonly CardScanWorkflowRunner _runner;

    public string JobType =>
        BackgroundJobTypes.ProcessMedia;

    public ProcessMediaHandler(
        CardScanWorkflowRunner runner,
        MagicDbContext db)
    {
        _runner = runner;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<CardScanJobPayload>(
                job.Payload);
        Console.WriteLine(
            $"PAYLOAD SET: {payload.SetCode}");
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