using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTestHub.Server.Services;

public sealed class TestHubService(TestHubDbContext db)
{
    // ── Apps ──────────────────────────────────────────────────────────────

    public Task<List<AppEntry>> GetAppsAsync() =>
        db.Apps.Include(a => a.Steps).OrderBy(a => a.SortOrder).ToListAsync();

    public Task<AppEntry?> GetAppAsync(Guid id) =>
        db.Apps.Include(a => a.Steps.OrderBy(s => s.Order))
               .FirstOrDefaultAsync(a => a.Id == id);

    public async Task SaveAppAsync(AppEntry app)
    {
        if (db.Entry(app).State == EntityState.Detached)
            db.Apps.Update(app);
        await db.SaveChangesAsync();
    }

    public async Task AddStepAsync(Guid appId, string instruction, string? expectedResult)
    {
        var maxOrder = await db.Steps.Where(s => s.AppEntryId == appId)
                                     .MaxAsync(s => (int?)s.Order) ?? 0;
        db.Steps.Add(new TestStep
        {
            AppEntryId     = appId,
            Order          = maxOrder + 1,
            Instruction    = instruction,
            ExpectedResult = expectedResult
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteStepAsync(Guid stepId)
    {
        var step = await db.Steps.FindAsync(stepId);
        if (step is not null) db.Steps.Remove(step);
        await db.SaveChangesAsync();
    }

    public async Task MoveStepAsync(Guid stepId, int direction)
    {
        var step = await db.Steps.FindAsync(stepId);
        if (step is null) return;

        var sibling = await db.Steps
            .Where(s => s.AppEntryId == step.AppEntryId &&
                        (direction > 0 ? s.Order > step.Order : s.Order < step.Order))
            .OrderBy(s => direction > 0 ? s.Order : -s.Order)
            .FirstOrDefaultAsync();

        if (sibling is null) return;

        (step.Order, sibling.Order) = (sibling.Order, step.Order);
        await db.SaveChangesAsync();
    }

    // ── Testers ───────────────────────────────────────────────────────────

    public Task<List<Tester>> GetTestersAsync() =>
        db.Testers.OrderBy(t => t.Name).ToListAsync();

    public async Task<Tester> GetOrCreateTesterAsync(string name)
    {
        var tester = await db.Testers
            .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower());

        if (tester is not null) return tester;

        var colors = new[] { "#6366f1","#f5a623","#2ecc71","#e74c3c","#9b59b6","#1abc9c","#e67e22","#3498db" };
        var count  = await db.Testers.CountAsync();
        tester = new Tester { Name = name, Color = colors[count % colors.Length] };
        db.Testers.Add(tester);
        await db.SaveChangesAsync();
        return tester;
    }

    // ── Rounds ────────────────────────────────────────────────────────────

    public Task<List<TestRound>> GetRoundsAsync() =>
        db.Rounds.OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task<TestRound> CreateRoundAsync(string name)
    {
        var round = new TestRound { Name = name };
        db.Rounds.Add(round);
        await db.SaveChangesAsync();
        return round;
    }

    public Task<TestRound?> GetRoundAsync(Guid id) =>
        db.Rounds
          .Include(r => r.Assignments)
              .ThenInclude(a => a.App)
          .Include(r => r.Assignments)
              .ThenInclude(a => a.Tester)
          .Include(r => r.Assignments)
              .ThenInclude(a => a.Results)
          .FirstOrDefaultAsync(r => r.Id == id);

    public async Task ArchiveRoundAsync(Guid id)
    {
        var round = await db.Rounds.FindAsync(id);
        if (round is not null) { round.IsActive = false; await db.SaveChangesAsync(); }
    }

    public async Task DeleteRoundAsync(Guid id)
    {
        var round = await db.Rounds
            .Include(r => r.Assignments).ThenInclude(a => a.Results)
            .Include(r => r.Assignments).ThenInclude(a => a.Feedbacks)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (round is null) return;
        db.Rounds.Remove(round);
        await db.SaveChangesAsync();
    }

    // ── Assignments ───────────────────────────────────────────────────────

    public async Task<TestAssignment> CreateAssignmentAsync(Guid roundId, Guid appId, Guid testerId)
    {
        var existing = await db.Assignments.FirstOrDefaultAsync(a =>
            a.TestRoundId == roundId && a.AppEntryId == appId && a.TesterId == testerId);
        if (existing is not null) return existing;

        var assignment = new TestAssignment
        {
            TestRoundId = roundId,
            AppEntryId  = appId,
            TesterId    = testerId
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId)
    {
        var a = await db.Assignments.FindAsync(assignmentId);
        if (a is not null) db.Assignments.Remove(a);
        await db.SaveChangesAsync();
    }

    public Task<List<TestAssignment>> GetTesterAssignmentsAsync(Guid testerId) =>
        db.Assignments
          .Include(a => a.App)
          .Include(a => a.Round)
          .Include(a => a.Results)
          .Where(a => a.TesterId == testerId && a.Round!.IsActive)
          .OrderBy(a => a.App!.SortOrder)
          .ToListAsync();

    public Task<TestAssignment?> GetAssignmentAsync(Guid id) =>
        db.Assignments
          .Include(a => a.App).ThenInclude(app => app!.Steps.OrderBy(s => s.Order))
          .Include(a => a.Tester)
          .Include(a => a.Round)
          .Include(a => a.Results)
          .Include(a => a.Feedbacks).ThenInclude(f => f.Tester)
          .FirstOrDefaultAsync(a => a.Id == id);

    // ── Step results ──────────────────────────────────────────────────────

    public async Task RecordResultAsync(Guid assignmentId, Guid stepId, StepStatus status, string? note)
    {
        var existing = await db.Results.FirstOrDefaultAsync(r =>
            r.TestAssignmentId == assignmentId && r.TestStepId == stepId);

        if (existing is not null)
        {
            existing.Status = status;
            existing.Note   = note;
        }
        else
        {
            db.Results.Add(new StepResult
            {
                TestAssignmentId = assignmentId,
                TestStepId       = stepId,
                Status           = status,
                Note             = note
            });
        }

        var assignment = await db.Assignments
            .Include(a => a.App).ThenInclude(app => app!.Steps)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);

        if (assignment is not null)
        {
            assignment.StartedAt ??= DateTime.UtcNow;
            var totalSteps    = assignment.App?.Steps.Count ?? 0;
            var resolvedCount = await db.Results.CountAsync(r => r.TestAssignmentId == assignmentId);
            if (totalSteps > 0 && resolvedCount >= totalSteps)
                assignment.CompletedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    // ── Feedback ──────────────────────────────────────────────────────────

    public async Task AddFeedbackAsync(Guid assignmentId, Guid appId, Guid testerId, string text, FeedbackType type)
    {
        db.Feedbacks.Add(new Feedback
        {
            TestAssignmentId = assignmentId,
            AppEntryId       = appId,
            TesterId         = testerId,
            Text             = text,
            Type             = type
        });
        await db.SaveChangesAsync();
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

    public Task<TestRound?> GetActiveRoundAsync() =>
        db.Rounds
          .Include(r => r.Assignments).ThenInclude(a => a.App)
          .Include(r => r.Assignments).ThenInclude(a => a.Tester)
          .Include(r => r.Assignments).ThenInclude(a => a.Results)
          .Where(r => r.IsActive)
          .OrderByDescending(r => r.CreatedAt)
          .AsSplitQuery()
          .FirstOrDefaultAsync();

    public Task<List<Feedback>> GetRecentFeedbackAsync(int count = 10) =>
        db.Feedbacks
          .Include(f => f.Tester)
          .OrderByDescending(f => f.CreatedAt)
          .Take(count)
          .ToListAsync();

    public async Task<Dictionary<Guid, string>> GetAppNamesAsync()
    {
        var apps = await db.Apps.ToListAsync();
        return apps.ToDictionary(a => a.Id, a => a.Name);
    }
}
