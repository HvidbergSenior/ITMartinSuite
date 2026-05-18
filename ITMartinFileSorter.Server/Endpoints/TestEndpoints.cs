using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFileSorter.Server.Endpoints;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/test/checkpoints",
            async (
                [FromServices] MediaDbContext dbContext) =>
            {
                var checkpoints =
                    await dbContext.WorkflowCheckpoints
                        .ToListAsync();

                var ordered =
                    checkpoints
                        .OrderByDescending(
                            x => x.CreatedAtUtc)
                        .Take(20);

                return Results.Ok(ordered);
            });

        app.MapPost(
            "/api/runtime/test-queue",
            async (
                IBackgroundJobQueue queue,
                CancellationToken cancellationToken) =>
            {
                await queue.EnqueueAsync(
                    new BackgroundJob
                    {
                        Queue = "thumbnails",
                        Payload =
                            """
                            {
                                "FilePath":"C:\\test.jpg"
                            }
                            """
                    },
                    cancellationToken);

                return Results.Ok();
            });
        app.MapPost(
            "/api/test/workflow",
            async (
                IWorkflowExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var workflow =
                    new TestWorkflowDefinition();

                var context =
                    new WorkflowExecutionContext
                    {
                        WorkflowId = Guid.NewGuid(),
                        WorkflowName = workflow.Name,
                        CancellationToken = cancellationToken
                    };

                await executor.ExecuteAsync(
                    workflow,
                    context,
                    cancellationToken);

                return Results.Ok(context.WorkflowId);
            });
        return app;
    }
}