using ITMartinDreamReader.Server.Data;
using ITMartinDreamReader.Server.Data.Entities;
using ITMartinDreamReader.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 30 * 1024 * 1024);

builder.Services.AddDbContext<DreamDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DreamDb")
        ?? "Data Source=/app/data/dreams.db"));

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("fal");
builder.Services.AddSingleton<DreamAiService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DreamDbContext>();
    db.Database.EnsureCreated();

    // Migration: EnsureCreated() only creates tables on a brand-new db file -
    // it won't add a new column to an already-existing Categories table (see
    // the karaoke-web incident this same night). Add it manually if missing,
    // then reseed with the new layered category set. Safe to wipe existing
    // categories/entries here - this is pre-launch, only test data exists.
    var hasLayerColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Categories') WHERE name = 'Layer'").AsEnumerable().First() > 0;

    if (!hasLayerColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Categories ADD COLUMN Layer TEXT NOT NULL DEFAULT ''");

    var hasFunnyColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Entries') WHERE name = 'AiFunny'").AsEnumerable().First() > 0;
    if (!hasFunnyColumn)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Entries ADD COLUMN AiFunny TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE Entries ADD COLUMN ImageFileName TEXT NULL");
    }

    // Reseed whenever categories are missing their Layer (covers both a
    // fresh db and a previous partial-failure retry that added the column
    // but didn't finish cleaning up - checked by column state, not a flag,
    // so it self-heals regardless of which step failed last time).
    if (!hasLayerColumn || db.Categories.Any(c => c.Layer == ""))
    {
        db.Database.ExecuteSqlRaw("DELETE FROM DreamCategoryDreamEntry");
        db.Database.ExecuteSqlRaw("DELETE FROM Entries");
        db.Database.ExecuteSqlRaw("DELETE FROM Categories");
    }

    // Idempotent per-category seeding: only inserts categories that don't
    // already exist (by Name+Layer), so adding a new layer/category later
    // doesn't require wiping the table and doesn't touch user-added custom
    // tags from AddCustomTag in Index.razor.
    var seedCategories = new List<DreamCategory>
    {
        // Hvem? (Who?)
        new() { Name = "Familie", Emoji = "👨‍👩‍👧", Layer = "Who" },
        new() { Name = "Kolleger", Emoji = "💼", Layer = "Who" },
        new() { Name = "Venner", Emoji = "👥", Layer = "Who" },
        new() { Name = "Partner/kæreste", Emoji = "💑", Layer = "Who" },
        new() { Name = "Fremmede", Emoji = "🚶", Layer = "Who" },
        new() { Name = "Kendte/berømtheder", Emoji = "⭐", Layer = "Who" },
        new() { Name = "Alene", Emoji = "🧍", Layer = "Who" },
        new() { Name = "Kæledyr/dyr", Emoji = "🐾", Layer = "Who" },

        // Hvor? (Where?)
        new() { Name = "Hjem", Emoji = "🏠", Layer = "Where" },
        new() { Name = "Arbejde", Emoji = "🏢", Layer = "Where" },
        new() { Name = "Ferie/rejse", Emoji = "✈️", Layer = "Where" },
        new() { Name = "Skole", Emoji = "🏫", Layer = "Where" },
        new() { Name = "Natur", Emoji = "🌳", Layer = "Where" },
        new() { Name = "Vand/hav", Emoji = "🌊", Layer = "Where" },
        new() { Name = "Barndomshjem", Emoji = "🧸", Layer = "Where" },
        new() { Name = "Ukendt sted", Emoji = "❓", Layer = "Where" },

        // Hvad sker der? (Doing / what happens)
        new() { Name = "Flyver", Emoji = "🕊️", Layer = "Doing" },
        new() { Name = "Falder", Emoji = "🪂", Layer = "Doing" },
        new() { Name = "Jagtet/flygter", Emoji = "🏃", Layer = "Doing" },
        new() { Name = "Kæmper", Emoji = "⚔️", Layer = "Doing" },
        new() { Name = "Leder efter noget", Emoji = "🔍", Layer = "Doing" },
        new() { Name = "Fest/fejrer", Emoji = "🎉", Layer = "Doing" },
        new() { Name = "Eksamen/test", Emoji = "📝", Layer = "Doing" },
        new() { Name = "Tænder falder ud", Emoji = "🦷", Layer = "Doing" },
        new() { Name = "Fanget/kan ikke bevæge sig", Emoji = "🔒", Layer = "Doing" },
        new() { Name = "Død", Emoji = "💀", Layer = "Doing" },
        new() { Name = "Spøgelser/overnaturligt", Emoji = "👻", Layer = "Doing" },
        new() { Name = "Sejr/succes", Emoji = "🏆", Layer = "Doing" },

        // Følelse? (Feeling?)
        new() { Name = "Frygt", Emoji = "😨", Layer = "Feeling" },
        new() { Name = "Glæde", Emoji = "😊", Layer = "Feeling" },
        new() { Name = "Forvirring", Emoji = "😵", Layer = "Feeling" },
        new() { Name = "Sorg", Emoji = "😢", Layer = "Feeling" },
        new() { Name = "Spænding", Emoji = "🤩", Layer = "Feeling" },
        new() { Name = "Ro", Emoji = "😌", Layer = "Feeling" },

        // Andres reaktion? (Reception - how others felt about you in the dream)
        new() { Name = "Folk heppede på mig", Emoji = "🙌", Layer = "Reception" },
        new() { Name = "Folk var utilfredse med mig", Emoji = "😠", Layer = "Reception" },
        new() { Name = "Nogen var forelsket i mig", Emoji = "💘", Layer = "Reception" },
        new() { Name = "Jeg følte mig afvist", Emoji = "🚫", Layer = "Reception" },
        new() { Name = "Jeg følte mig støttet", Emoji = "🤝", Layer = "Reception" },
        new() { Name = "Jeg følte mig bedømt", Emoji = "👀", Layer = "Reception" },
        new() { Name = "Jeg følte mig ignoreret", Emoji = "🙈", Layer = "Reception" },
        new() { Name = "Jeg følte mig beundret", Emoji = "🌟", Layer = "Reception" },
    };

    var existingKeys = db.Categories.Select(c => new { c.Name, c.Layer }).ToHashSet();
    var toAdd = seedCategories.Where(c => !existingKeys.Any(e => e.Name == c.Name && e.Layer == c.Layer)).ToList();
    if (toAdd.Count > 0)
    {
        db.Categories.AddRange(toAdd);
        db.SaveChanges();
    }
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/dream-image", (string file, IConfiguration cfg) =>
{
    var root = cfg["DreamImages:Root"] ?? "/app/data/images";
    var full = Path.GetFullPath(Path.Combine(root, file));
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (!File.Exists(full)) return Results.NotFound();
    return Results.File(full, "image/jpeg", file);
});

app.MapRazorComponents<ITMartinDreamReader.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
