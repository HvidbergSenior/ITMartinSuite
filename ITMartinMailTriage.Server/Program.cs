using ITMartin.Ai;
using ITMartinMailTriage.Server.Components;
using ITMartinMailTriage.Server.Data;
using ITMartinMailTriage.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

Directory.CreateDirectory("data");
builder.Services.AddDbContext<MailTriageDbContext>(o =>
    o.UseSqlite(builder.Configuration["ConnectionStrings:MailTriageDb"] ?? "Data Source=data/mailtriage.db"));

builder.Services.AddAi();
builder.Services.AddScoped<IMailSyncService, GmailSyncService>();
builder.Services.AddScoped<IMailSyncService, OutlookSyncService>();
builder.Services.AddScoped<MailTriageOrchestrator>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MailTriageDbContext>();
    db.Database.EnsureCreated();
    if (!db.Profile.Any())
    {
        db.Profile.Add(new TriageProfile());
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Manual trigger for a sync+score run - the UI's "Refresh" button calls this.
app.MapPost("/api/run", async (MailTriageOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.RunAsync(cancellationToken: ct);
    return Results.Ok(result);
});

app.Run();
