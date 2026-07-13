using Anthropic;
using Anthropic.Models.Messages;
using iText.Forms;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Http.Features;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 200L * 1024 * 1024);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 200L * 1024 * 1024);

builder.Services.AddRazorComponents();
builder.Services.AddHostedService<OutputCleanupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var claudeKey = app.Configuration["Claude:ApiKey"];
AnthropicClient? claude = string.IsNullOrWhiteSpace(claudeKey) ? null : new AnthropicClient { ApiKey = claudeKey };

// Every generated file goes here instead of straight to the browser - lets the
// page show a shared "results" gallery (see everything you made, download the
// ones you want) instead of a single forced one-shot download. Old files get
// swept by OutputCleanupService below.
//
// This app has no login or invite link - anyone can use it. So each browser
// gets a random client ID (generated client-side, no user action needed) that
// scopes the results gallery to just that visitor's own files. Otherwise the
// gallery would be a public list of everyone's PDFs - names, addresses,
// signatures included.
var outputRoot = app.Configuration["OutputRoot"] ?? "/app/data/output";
Directory.CreateDirectory(outputRoot);

static bool IsValidClientId(string? clientId) =>
    !string.IsNullOrEmpty(clientId) && clientId.Length <= 64 && clientId.All(char.IsLetterOrDigit);

async Task<IResult> SaveOutputAsync(string clientId, byte[] bytes, string label)
{
    var id = Guid.NewGuid().ToString("N");
    var path = Path.Combine(outputRoot, $"{clientId}__{id}.pdf");
    await File.WriteAllBytesAsync(path, bytes);
    return Results.Ok(new
    {
        id,
        label,
        sizeKb = Math.Round(bytes.Length / 1024.0, 1),
        createdAt = DateTime.UtcNow
    });
}

// ── Draft/polish content with Claude, for the Create tool ───────────────────

app.MapPost("/api/pdf/draft", async (HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<DraftRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Notes))
        return Results.BadRequest();
    if (claude is null) return Results.Problem("Claude er ikke konfigureret på serveren.");

    var response = await claude.Messages.Create(new MessageCreateParams
    {
        Model = Model.ClaudeSonnet4_6,
        MaxTokens = 1500,
        System = """
            You turn rough notes into a clean, ready-to-print document in Danish.
            Return ONLY valid JSON, no markdown fences, matching exactly:
            {"title": "...", "body": "..."}
            The body should use \n\n between paragraphs. Keep the person's own words
            and facts - tidy the structure and phrasing, don't invent new content.
            """,
        Messages = [new() { Role = Role.User, Content = body.Notes }]
    });

    return Results.Content(ExtractText(response), "application/json");
});

// ── Create: title + body text -> PDF ─────────────────────────────────────────

app.MapPost("/api/pdf/create", async (HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<CreateRequest>();
    if (body is null || !IsValidClientId(body.ClientId)) return Results.BadRequest();

    var bytes = CreatePdf(body.Title ?? "Dokument", body.Body ?? "");
    return await SaveOutputAsync(body.ClientId!, bytes, body.Title ?? "Dokument");
});

// ── Merge: several files, in the given order -> one PDF ─────────────────────

app.MapPost("/api/pdf/merge", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var files = form.Files.OrderBy(f => f.Name).ToList(); // field names sent as order-0, order-1, ...
    if (files.Count == 0) return Results.BadRequest("Ingen filer");

    using var outStream = new MemoryStream();
    using (var writer = new PdfWriter(outStream))
    using (var mergedDoc = new PdfDocument(writer))
    {
        var merger = new iText.Kernel.Utils.PdfMerger(mergedDoc);
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            using var reader = new PdfReader(new MemoryStream(ms.ToArray()));
            using var srcDoc = new PdfDocument(reader);
            merger.Merge(srcDoc, 1, srcDoc.GetNumberOfPages());
        }
    }
    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), $"Flettet ({files.Count} filer)");
});

// ── Split: one file, a page range -> one PDF with just those pages ──────────

app.MapPost("/api/pdf/split", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest("Ingen fil");
    if (!int.TryParse(form["from"], out var from) || !int.TryParse(form["to"], out var to))
        return Results.BadRequest("Ugyldigt sideinterval");

    using var srcMs = new MemoryStream();
    await file.CopyToAsync(srcMs);

    using var outStream = new MemoryStream();
    using (var reader = new PdfReader(new MemoryStream(srcMs.ToArray())))
    using (var srcDoc = new PdfDocument(reader))
    using (var writer = new PdfWriter(outStream))
    using (var destDoc = new PdfDocument(writer))
    {
        from = Math.Clamp(from, 1, srcDoc.GetNumberOfPages());
        to = Math.Clamp(to, from, srcDoc.GetNumberOfPages());
        srcDoc.CopyPagesTo(from, to, destDoc);
    }
    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), $"Sider {from}-{to}");
});

// ── Rotate: one file, a page number (or 0 = all), degrees -> PDF ────────────

app.MapPost("/api/pdf/rotate", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest("Ingen fil");
    int.TryParse(form["page"], out var pageNum); // 0 = all pages
    int.TryParse(form["degrees"], out var degrees);

    using var srcMs = new MemoryStream();
    await file.CopyToAsync(srcMs);

    using var outStream = new MemoryStream();
    using (var reader = new PdfReader(new MemoryStream(srcMs.ToArray())))
    using (var writer = new PdfWriter(outStream))
    using (var doc = new PdfDocument(reader, writer))
    {
        var pages = pageNum > 0 ? [pageNum] : Enumerable.Range(1, doc.GetNumberOfPages());
        foreach (var p in pages)
        {
            if (p < 1 || p > doc.GetNumberOfPages()) continue;
            var page = doc.GetPage(p);
            page.SetRotation((page.GetRotation() + degrees + 360) % 360);
        }
    }
    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), "Roteret");
});

// ── Fill & Sign: one file, a list of text/signature stamps placed at exact
// coordinates (picked by clicking the real rendered page client-side) ───────
// One request does everything - name, date, signature - one download.

app.MapPost("/api/pdf/stamp", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var file = form.Files.FirstOrDefault(f => f.Name == "file");
    if (file is null) return Results.BadRequest("Ingen fil");

    var stampsJson = form["stamps"].ToString();
    var stamps = System.Text.Json.JsonSerializer.Deserialize<List<StampItem>>(
        string.IsNullOrWhiteSpace(stampsJson) ? "[]" : stampsJson,
        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    if (stamps.Count == 0) return Results.BadRequest("Ingen placeringer angivet");

    using var srcMs = new MemoryStream();
    await file.CopyToAsync(srcMs);

    using var outStream = new MemoryStream();
    using (var reader = new PdfReader(new MemoryStream(srcMs.ToArray())))
    using (var writer = new PdfWriter(outStream))
    using (var pdfDoc = new PdfDocument(reader, writer))
    {
        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        foreach (var stamp in stamps)
        {
            if (stamp.Page < 1 || stamp.Page > pdfDoc.GetNumberOfPages()) continue;
            var page = pdfDoc.GetPage(stamp.Page);
            var size = page.GetPageSize();
            var pdfCanvas = new PdfCanvas(page);

            if (stamp.Type == "image" && !string.IsNullOrEmpty(stamp.ImageBase64))
            {
                var imgBytes = Convert.FromBase64String(stamp.ImageBase64);
                var img = iText.IO.Image.ImageDataFactory.Create(imgBytes);
                var w = stamp.Width > 0 ? stamp.Width : 150f;
                var h = stamp.Height > 0 ? stamp.Height : 60f;
                // stamp.X/Y is the top-left corner as picked on screen; iText images
                // are placed by their bottom-left corner, so shift Y down by height.
                pdfCanvas.AddImageFittedIntoRectangle(img, new iText.Kernel.Geom.Rectangle(stamp.X, stamp.Y - h, w, h), false);
            }
            else if (!string.IsNullOrEmpty(stamp.Text))
            {
                var canvas = new Canvas(pdfCanvas, size);
                canvas.ShowTextAligned(
                    new Paragraph(stamp.Text).SetFont(font).SetFontSize(stamp.FontSize > 0 ? stamp.FontSize : 13),
                    stamp.X, stamp.Y, TextAlignment.LEFT);
                canvas.Close();
            }
        }
    }
    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), "Udfyldt & signeret");
});

// ── Form fields: read a PDF's AcroForm field names -> JSON list ─────────────

app.MapPost("/api/pdf/form-fields", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();

    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest("Ingen fil");

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);

    using var reader = new PdfReader(new MemoryStream(ms.ToArray()));
    using var pdfDoc = new PdfDocument(reader);
    var acroForm = PdfAcroForm.GetAcroForm(pdfDoc, false);
    if (acroForm is null) return Results.Ok(Array.Empty<object>());

    var fields = acroForm.GetAllFormFields()
        .Select(kv => new { name = kv.Key, value = kv.Value.GetValueAsString() })
        .ToList();
    return Results.Ok(fields);
});

// ── Fill form: file + field values (JSON in a form field) -> filled PDF ─────

app.MapPost("/api/pdf/fill-form", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest("Ingen fil");
    var valuesJson = form["values"].ToString();
    var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
        string.IsNullOrWhiteSpace(valuesJson) ? "{}" : valuesJson) ?? [];
    var flatten = form["flatten"] == "true";

    using var srcMs = new MemoryStream();
    await file.CopyToAsync(srcMs);

    using var outStream = new MemoryStream();
    using (var reader = new PdfReader(new MemoryStream(srcMs.ToArray())))
    using (var writer = new PdfWriter(outStream))
    using (var pdfDoc = new PdfDocument(reader, writer))
    {
        var acroForm = PdfAcroForm.GetAcroForm(pdfDoc, true);
        foreach (var (key, value) in values)
        {
            var field = acroForm.GetField(key);
            field?.SetValue(value);
        }
        if (flatten) acroForm.FlattenFields();
    }
    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), "Udfyldt formular");
});

// ── Images -> PDF: photograph a document/receipt, turn it into a PDF ────────
// One page per image, in upload order, fitted to the page.

app.MapPost("/api/pdf/images-to-pdf", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var files = form.Files.OrderBy(f => f.Name).ToList(); // order-0, order-1, ...
    if (files.Count == 0) return Results.BadRequest("Ingen billeder");

    var images = new List<byte[]>();
    foreach (var file in files)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        images.Add(ms.ToArray());
    }

    using var stream = new MemoryStream();
    QuestPDF.Fluent.Document.Create(container =>
    {
        foreach (var imgBytes in images)
        {
            container.Page(page =>
            {
                page.Margin(0);
                page.Content().Image(imgBytes).FitArea();
            });
        }
    }).GeneratePdf(stream);

    return await SaveOutputAsync(form["clientId"]!, stream.ToArray(), $"Billeder til PDF ({images.Count} sider)");
});

// ── Results gallery: list / download / delete generated files ───────────────

app.MapGet("/api/pdf/outputs", (string? clientId) =>
{
    if (!IsValidClientId(clientId)) return Results.Unauthorized();
    var prefix = $"{clientId}__";
    var files = Directory.GetFiles(outputRoot, $"{prefix}*.pdf")
        .Select(p => new FileInfo(p))
        .OrderByDescending(f => f.CreationTimeUtc)
        .Select(f => new
        {
            id = Path.GetFileNameWithoutExtension(f.Name)[prefix.Length..],
            sizeKb = Math.Round(f.Length / 1024.0, 1),
            createdAt = f.CreationTimeUtc
        });
    return Results.Ok(files);
});

app.MapGet("/api/pdf/download/{id}", (string id, string? clientId) =>
{
    if (!IsValidClientId(clientId) || !IsValidId(id)) return Results.Unauthorized();
    var path = Path.Combine(outputRoot, $"{clientId}__{id}.pdf");
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(File.ReadAllBytes(path), "application/pdf", $"{id}.pdf");
});

app.MapDelete("/api/pdf/outputs/{id}", (string id, string? clientId) =>
{
    if (!IsValidClientId(clientId) || !IsValidId(id)) return Results.Unauthorized();
    var path = Path.Combine(outputRoot, $"{clientId}__{id}.pdf");
    if (File.Exists(path)) File.Delete(path);
    return Results.Ok();
});

app.MapRazorComponents<ITMartinPdf.Server.App>();

app.Run();

static bool IsValidId(string id) => id.Length == 32 && id.All(char.IsLetterOrDigit);

static byte[] CreatePdf(string title, string body)
{
    using var stream = new MemoryStream();
    QuestPDF.Fluent.Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(12).LineHeight(1.4f));

            page.Header().PaddingBottom(14).Text(title).FontSize(22).Bold();

            page.Content().Column(col =>
            {
                col.Spacing(12);
                foreach (var para in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                    col.Item().Text(para.Trim());
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Side ");
                x.CurrentPageNumber();
                x.Span(" af ");
                x.TotalPages();
            });
        });
    }).GeneratePdf(stream);
    return stream.ToArray();
}

static string ExtractText(Message response)
{
    var text = new System.Text.StringBuilder();
    foreach (var block in response.Content)
        if (block.TryPickText(out var t)) text.Append(t.Text);
    return text.ToString().Trim();
}

record DraftRequest(string Notes);
record CreateRequest(string? ClientId, string? Title, string? Body);

record StampItem
{
    public int Page { get; init; } = 1;
    public float X { get; init; }
    public float Y { get; init; }
    public string Type { get; init; } = "text"; // "text" | "image"
    public string? Text { get; init; }
    public float FontSize { get; init; }
    public string? ImageBase64 { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
}

// Every result is kept for 24h so there's time to come back and grab it, then
// swept away automatically - matches the "not everything needs to live
// forever" spirit of the rest of the suite (see StarRealms's CleanupService).
public sealed class OutputCleanupService(IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var root = config["OutputRoot"] ?? "/app/data/output";
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    var cutoff = DateTime.UtcNow.AddHours(-24);
                    foreach (var file in Directory.GetFiles(root, "*.pdf"))
                    {
                        if (File.GetCreationTimeUtc(file) < cutoff)
                            File.Delete(file);
                    }
                }
            }
            catch { /* best-effort cleanup, never crash the app over it */ }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
