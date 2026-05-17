using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFileSorter.Server.Endpoints;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/test/checkpoints", async (
            [FromServices] MediaDbContext dbContext) =>
        {
            var checkpoints = await dbContext.WorkflowCheckpoints
                .ToListAsync();

            var ordered = checkpoints
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(20);

            return Results.Ok(ordered);
        });

        return app;
    }
}