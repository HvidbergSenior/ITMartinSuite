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
using System.IO.Compression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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

// ── ZIP of images -> PDF ─────────────────────────────────────────────────────
// Default order is a natural sort on each entry's leading number ("1_x.jpg"
// before "2_y.jpg", "10_z.jpg" after both) - a plain alphabetical sort would
// put "10" before "2". If the user typed an ordering instruction, ask Claude
// to order the filenames instead (cheap: it only ever sees names, never image
// bytes) and fall back to the natural sort if that fails or looks wrong.
var zipImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

app.MapPost("/api/pdf/zip-to-pdf", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    if (!IsValidClientId(form["clientId"])) return Results.BadRequest();

    var zipFile = form.Files.GetFile("zipfile");
    if (zipFile is null) return Results.BadRequest("Ingen ZIP-fil");

    var entries = new List<(string Name, byte[] Bytes)>();
    using (var zipStream = zipFile.OpenReadStream())
    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
    {
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // folder entry
            if (!zipImageExts.Contains(Path.GetExtension(entry.Name))) continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            entries.Add((entry.Name, ms.ToArray()));
        }
    }

    if (entries.Count == 0) return Results.BadRequest("Ingen billeder fundet i ZIP-filen");

    var instructions = form["instructions"].ToString();
    var overlapping = string.Equals(form["overlapping"], "true", StringComparison.OrdinalIgnoreCase);
    List<(string Name, byte[] Bytes)> ordered;

    if (overlapping)
    {
        var precise = string.Equals(form["precise"], "true", StringComparison.OrdinalIgnoreCase);
        var preciseResult = precise && claude is not null
            ? await BuildPreciseOrderAsync(claude, entries, instructions)
            : null;

        if (preciseResult is not null)
        {
            // AI already ordered and cropped each image individually based on
            // its actual content - no flat crop needed on top of this.
            ordered = preciseResult;
        }
        else
        {
            // Scrolling screenshots: filename order is meaningless, only reading
            // the actual visible content can tell what follows what.
            ordered = claude is not null
                ? await OrderScreenshotsByContentAsync(claude, entries, instructions) ?? NaturalSortEntries(entries)
                : NaturalSortEntries(entries);

            // Overlap trim: crop the top N% off every image except the first, so the
            // portion each new screenshot repeats from the previous one drops out.
            // Not pixel-precise (no boundary detection - just a flat percentage the
            // user can nudge) - kept as the default/fallback since a uniform overlap
            // is sometimes exactly what's there, and it's simpler when it's enough.
            if (int.TryParse(form["cropPercent"], out var cropPercent) && cropPercent > 0)
            {
                ordered = CropTopExceptFirst(ordered, Math.Clamp(cropPercent, 0, 60));
            }
        }
    }
    else
    {
        ordered = NaturalSortEntries(entries);

        if (!string.IsNullOrWhiteSpace(instructions) && claude is not null)
        {
            try
            {
                var response = await claude.Messages.Create(new MessageCreateParams
                {
                    Model = Model.ClaudeHaiku4_5,
                    MaxTokens = 500,
                    System = """
                        You are given a list of image filenames from a zip file, and a
                        person's instruction for how to order them into a single PDF.
                        Return ONLY a valid JSON array of the filenames in the correct
                        order, no markdown fences. Every filename from the input must
                        appear exactly once - no additions, no omissions, no renaming.
                        """,
                    Messages = [new() { Role = Role.User, Content = $"Filnavne: {string.Join(", ", entries.Select(e => e.Name))}\n\nInstruktion: {instructions}" }]
                });

                var namesInOrder = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ExtractText(response));
                if (namesInOrder is { Count: > 0 } &&
                    namesInOrder.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(entries.Select(e => e.Name)))
                {
                    var byName = entries.ToDictionary(e => e.Name, e => e.Bytes, StringComparer.OrdinalIgnoreCase);
                    ordered = namesInOrder.Select(n => (n, byName[n])).ToList();
                }
                // If the AI's answer doesn't contain exactly the same set of
                // filenames, silently keep the natural-sort fallback rather than
                // risk dropping or duplicating a page.
            }
            catch
            {
                // Ordering is a nice-to-have on top of a working default - a
                // failed AI call should never block generating the PDF.
            }
        }
    }

    using var outStream = new MemoryStream();
    QuestPDF.Fluent.Document.Create(container =>
    {
        foreach (var (_, imgBytes) in ordered)
        {
            container.Page(page =>
            {
                page.Margin(0);
                page.Content().Image(imgBytes).FitArea();
            });
        }
    }).GeneratePdf(outStream);

    return await SaveOutputAsync(form["clientId"]!, outStream.ToArray(), $"ZIP til PDF ({ordered.Count} sider)");
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

// "Numbers to the left" in practice means "the number that identifies this
// file's position", which is usually the last digit run before the extension
// ("IMG_0610.png" -> 610, "1_cover.jpg" -> 1) rather than strictly the first
// character - a leading-only match would miss "IMG_0610.png" entirely and
// silently fall back to alphabetical, which only looks right by accident
// when every number happens to be the same zero-padded width.
// Sends every screenshot to Claude vision in one request (same
// ImageBlockParam/ToolChoice/TryPickToolUse pattern as ITMartinClub.Server's
// MatchOcrService) and asks it to read the visible lyrics/chords/lines to
// work out reading order - something only actual content-reading can do,
// since these filenames carry no ordering information at all.
static async Task<List<(string Name, byte[] Bytes)>?> OrderScreenshotsByContentAsync(
    AnthropicClient claude, List<(string Name, byte[] Bytes)> entries, string? instructions)
{
    try
    {
        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "These are scrolling screenshots of the same document (e.g. a song's lyrics/chords), " +
                       "each one named below immediately before its image. Consecutive screenshots overlap - " +
                       "the top of one repeats the bottom of the previous. Work out the correct reading order " +
                       "from the actual visible content." +
                       (string.IsNullOrWhiteSpace(instructions) ? "" : $" Extra guidance: {instructions}")
            }
        };

        foreach (var (name, bytes) in entries)
        {
            content.Add(new TextBlockParam { Text = $"Image: {name}" });
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GuessMime(name) } });
        }

        var orderTool = new Tool
        {
            Name = "order_screenshots",
            Description = "Report the correct reading order of these screenshots based on their visible content, so each one continues naturally from the previous.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["order"] = System.Text.Json.JsonDocument.Parse("""
                        {
                            "type": "array",
                            "description": "The filenames in correct reading order, first to last.",
                            "items": { "type": "string" }
                        }
                        """).RootElement
                },
                Required = ["order"],
            },
        };

        var response = await claude.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            Tools = [orderTool],
            ToolChoice = new ToolChoiceTool { Name = "order_screenshots" },
            Messages = [new() { Role = Role.User, Content = content }]
        });

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        if (toolUse is null) return null;

        var json = System.Text.Json.JsonSerializer.Serialize(toolUse.Input);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ScreenshotOrderResult>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed?.Order is not { Count: > 0 } order) return null;
        if (!order.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(entries.Select(e => e.Name))) return null;

        var byName = entries.ToDictionary(e => e.Name, e => e.Bytes, StringComparer.OrdinalIgnoreCase);
        return order.Select(n => (n, byName[n])).ToList();
    }
    catch
    {
        // Same reasoning as every other AI-assisted step here - a failed
        // call falls back to the caller's default rather than blocking.
        return null;
    }
}

// "Precise" mode: order by content, strip static chrome, then crop the
// overlap. Many chord/lyric apps print a row/line number in the margin -
// when that's legible, reading those numbers (an OCR task vision models are
// actually reliable at) and using uniform row-height arithmetic to crop
// exactly at a row boundary is far more accurate than searching for the
// best-matching pixel window: real test data showed that search aliasing
// onto a coincidentally-similar-but-wrong-length region whenever the content
// is repetitive (e.g. the same few chord symbols recurring throughout a
// song), over- or under-cropping by whole rows. Falls back to the pixel-
// similarity search when no row numbers can be read.
static async Task<List<(string Name, byte[] Bytes)>?> BuildPreciseOrderAsync(
    AnthropicClient claude, List<(string Name, byte[] Bytes)> entries, string? instructions)
{
    var ordered = await OrderScreenshotsByContentAsync(claude, entries, instructions);
    if (ordered is null) return null;

    ordered = StripFixedChrome(ordered);

    var rowRanges = await ReadRowRangesAsync(claude, ordered, instructions);
    if (rowRanges is not null)
    {
        return ApplyRowRangeCrop(ordered, rowRanges);
    }

    if (!string.IsNullOrWhiteSpace(instructions))
    {
        var range = await IdentifyContentRangeAsync(claude, ordered, instructions);
        if (range is not null)
        {
            var (first, last) = range.Value;
            var firstIdx = ordered.FindIndex(e => string.Equals(e.Name, first, StringComparison.OrdinalIgnoreCase));
            var lastIdx = ordered.FindIndex(e => string.Equals(e.Name, last, StringComparison.OrdinalIgnoreCase));
            if (firstIdx >= 0 && lastIdx >= firstIdx)
                ordered = ordered.GetRange(firstIdx, lastIdx - firstIdx + 1);
        }
    }

    return RemoveOverlapByPixelMatch(ordered);
}

// Asks Claude to read the printed row/line number at the top and bottom of
// each image, plus (from free-text guidance, if any) the overall row range
// to keep. Reading a short printed integer is a reliable task; returns null
// if no tool_use came back, the filename set doesn't match, or any image
// reports no legible numbering (LastRow < FirstRow signals "not numbered").
static async Task<RowRangeResult?> ReadRowRangesAsync(
    AnthropicClient claude, List<(string Name, byte[] Bytes)> ordered, string? instructions)
{
    try
    {
        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "These images are shown in reading order, each named immediately before its image. " +
                       "If each row/line of content has a number printed next to it (e.g. in the left margin of " +
                       "a chord or lyric chart), report the number at the very top and at the very bottom of " +
                       "each image - the actual printed number, not a guess. If an image has no such numbering, " +
                       "report firstRow and lastRow both as 0 for it." +
                       (string.IsNullOrWhiteSpace(instructions) ? "" : $" Also, based on this guidance: \"{instructions}\", report which row numbers should be kept overall as wantedFirstRow/wantedLastRow - use 1 and 999999 if no specific range is named.")
            }
        };
        foreach (var (name, bytes) in ordered)
        {
            content.Add(new TextBlockParam { Text = $"Image: {name}" });
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GuessMime(name) } });
        }

        var tool = new Tool
        {
            Name = "read_row_ranges",
            Description = "Report the visible row-number range in each image, and (if guidance names one) the overall row range to keep.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["wantedFirstRow"] = System.Text.Json.JsonDocument.Parse("""{"type":"integer"}""").RootElement,
                    ["wantedLastRow"] = System.Text.Json.JsonDocument.Parse("""{"type":"integer"}""").RootElement,
                    ["images"] = System.Text.Json.JsonDocument.Parse("""
                        {
                            "type": "array",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "filename": { "type": "string" },
                                    "firstRow": { "type": "integer" },
                                    "lastRow": { "type": "integer" }
                                },
                                "required": ["filename", "firstRow", "lastRow"]
                            }
                        }
                        """).RootElement
                },
                Required = ["images"],
            },
        };

        var response = await claude.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1000,
            Tools = [tool],
            ToolChoice = new ToolChoiceTool { Name = "read_row_ranges" },
            Messages = [new() { Role = Role.User, Content = content }]
        });

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        if (toolUse is null) return null;

        var json = System.Text.Json.JsonSerializer.Serialize(toolUse.Input);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<RowRangeResult>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed?.Images is not { Count: > 0 } images) return null;
        if (!images.Select(i => i.Filename).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(ordered.Select(e => e.Name)))
            return null;
        if (images.Any(i => i.LastRow < i.FirstRow))
            return null; // any unnumbered image means we can't trust the whole set

        return parsed;
    }
    catch
    {
        // Row-number reading is a nice-to-have on top of a working default -
        // a failed call falls back to page-level range + pixel matching.
        return null;
    }
}

// Crops each image to exactly the rows still needed, using the assumption
// that rows have uniform height within an image (true for a grid/table
// layout) to convert "skip N rows" into a pixel offset.
static List<(string Name, byte[] Bytes)> ApplyRowRangeCrop(
    List<(string Name, byte[] Bytes)> ordered, RowRangeResult ranges)
{
    var byName = ranges.Images.ToDictionary(i => i.Filename, i => i, StringComparer.OrdinalIgnoreCase);
    var wantedFirst = ranges.WantedFirstRow > 0 ? ranges.WantedFirstRow : 1;
    var wantedLast = ranges.WantedLastRow > 0 ? ranges.WantedLastRow : int.MaxValue;
    var nextWantedRow = wantedFirst;

    var result = new List<(string Name, byte[] Bytes)>();
    foreach (var (name, bytes) in ordered)
    {
        if (!byName.TryGetValue(name, out var range))
        {
            result.Add((name, bytes));
            continue;
        }

        if (range.LastRow < nextWantedRow || range.FirstRow > wantedLast)
        {
            continue; // entirely outside the wanted range - drop this page
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            var totalRows = range.LastRow - range.FirstRow + 1;
            var rowHeight = (double)image.Height / totalRows;

            var skipRows = Math.Max(0, nextWantedRow - range.FirstRow);
            var top = (int)(skipRows * rowHeight);

            var dropFromEnd = Math.Max(0, range.LastRow - wantedLast);
            var bottom = (int)(dropFromEnd * rowHeight);

            var height = image.Height - top - bottom;

            if (height > 0 && (top > 0 || bottom > 0))
            {
                image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(0, top, image.Width, height)));
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                result.Add((name, ms.ToArray()));
            }
            else if (height > 0)
            {
                result.Add((name, bytes));
            }

            nextWantedRow = Math.Min(range.LastRow, wantedLast) + 1;
        }
        catch
        {
            result.Add((name, bytes));
            nextWantedRow = Math.Min(range.LastRow, wantedLast) + 1;
        }
    }
    return result;
}

// Many apps have a static header (status bar, toolbar) and/or footer (e.g. a
// player bar) that never scrolls, so it's pixel-identical across every
// screenshot regardless of scroll position - real content, this is not. Left
// in place, it also fools overlap detection: a fixed region always "matches"
// perfectly, making the matcher think there's far more duplicated content
// than there really is. Detected once (from the first two images) and
// stripped from every image before overlap matching runs.
static List<(string Name, byte[] Bytes)> StripFixedChrome(List<(string Name, byte[] Bytes)> ordered)
{
    if (ordered.Count < 2) return ordered;

    var (headerPx, footerPx) = DetectFixedChrome(ordered[0].Bytes, ordered[1].Bytes);
    if (headerPx <= 0 && footerPx <= 0) return ordered;

    var result = new List<(string Name, byte[] Bytes)>();
    foreach (var (name, bytes) in ordered)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            var top = Math.Min(headerPx, image.Height / 3);
            var bottom = Math.Min(footerPx, image.Height / 3);
            var height = image.Height - top - bottom;
            if (height > 0 && (top > 0 || bottom > 0))
            {
                image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(0, top, image.Width, height)));
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                result.Add((name, ms.ToArray()));
                continue;
            }
        }
        catch
        {
            // Fall through to using the image untouched.
        }
        result.Add((name, bytes));
    }
    return result;
}

// Finds how many pixel rows at the very top / bottom are (near-)identical
// between two screenshots - a real scrolled position can't produce that, so
// a long identical run can only be static chrome. Capped safely by the
// caller (never more than a third of the image from either side).
static (int HeaderPx, int FooterPx) DetectFixedChrome(byte[] aBytes, byte[] bBytes)
{
    const int w = 64;
    var aGrid = DownscaleGrayscale(aBytes, w);
    var bGrid = DownscaleGrayscale(bBytes, w);
    var aH = aGrid.GetLength(0);
    var bH = bGrid.GetLength(0);
    var h = Math.Min(aH, bH);
    var width = aGrid.GetLength(1);

    int RowDiff(int ay, int by)
    {
        var diff = 0;
        for (var x = 0; x < width; x++) diff += Math.Abs(aGrid[ay, x] - bGrid[by, x]);
        return diff / width;
    }

    var headerRows = 0;
    for (var y = 0; y < h; y++)
    {
        if (RowDiff(y, y) > 3) break;
        headerRows = y + 1;
    }

    var footerRows = 0;
    for (var y = 0; y < h; y++)
    {
        var ay = aH - 1 - y;
        var by = bH - 1 - y;
        if (ay < 0 || by < 0) break;
        if (RowDiff(ay, by) > 3) break;
        footerRows = y + 1;
    }

    using var aImg = SixLabors.ImageSharp.Image.Load(aBytes);
    var scale = (double)aImg.Height / aH;
    return ((int)(headerRows * scale), (int)(footerRows * scale));
}

// Asks which image (by filename, from the already-ordered list) contains the
// start and end of the wanted content, when the free-text guidance names a
// range (e.g. "only line 1-56, the rest is noise"). A page-level in/out
// judgment is something a vision model can actually do reliably, unlike a
// fractional pixel offset.
static async Task<(string First, string Last)?> IdentifyContentRangeAsync(
    AnthropicClient claude, List<(string Name, byte[] Bytes)> ordered, string instructions)
{
    try
    {
        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "These screenshots are shown below in reading order, each named immediately before its " +
                       "image. Guidance from the user describes which content should be kept and says the rest " +
                       $"is noise to exclude: \"{instructions}\". Identify the FIRST image (in the order shown) " +
                       "that contains the start of the wanted content, and the LAST image that contains the end " +
                       "of the wanted content. If every image is within the wanted range, or the guidance doesn't " +
                       "actually name a range to trim, return the first and last filenames from the full list."
            }
        };
        foreach (var (name, bytes) in ordered)
        {
            content.Add(new TextBlockParam { Text = $"Image: {name}" });
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GuessMime(name) } });
        }

        var rangeTool = new Tool
        {
            Name = "identify_content_range",
            Description = "Report which image contains the start and which contains the end of the wanted content.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["firstContentFilename"] = System.Text.Json.JsonDocument.Parse("""{"type":"string"}""").RootElement,
                    ["lastContentFilename"] = System.Text.Json.JsonDocument.Parse("""{"type":"string"}""").RootElement
                },
                Required = ["firstContentFilename", "lastContentFilename"],
            },
        };

        var response = await claude.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 300,
            Tools = [rangeTool],
            ToolChoice = new ToolChoiceTool { Name = "identify_content_range" },
            Messages = [new() { Role = Role.User, Content = content }]
        });

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        if (toolUse is null) return null;

        var json = System.Text.Json.JsonSerializer.Serialize(toolUse.Input);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ContentRangeResult>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(parsed?.FirstContentFilename) || string.IsNullOrWhiteSpace(parsed?.LastContentFilename))
            return null;

        return (parsed.FirstContentFilename, parsed.LastContentFilename);
    }
    catch
    {
        // Range filtering is a nice-to-have on top of a working default - a
        // failed call just means no pages get dropped, not a blocked PDF.
        return null;
    }
}

// Deterministically finds and removes the duplicated overlap between every
// consecutive pair of images by comparing downscaled grayscale row profiles
// (a cheap 1D signal per image) and sliding them against each other to find
// the longest confident match - the classic "de-duplicate scrolled
// screenshots" technique. Crops only ever come off the top of the SECOND
// image in a pair, so the very first image is always left untouched.
static List<(string Name, byte[] Bytes)> RemoveOverlapByPixelMatch(List<(string Name, byte[] Bytes)> ordered)
{
    if (ordered.Count < 2) return ordered;

    var result = new List<(string Name, byte[] Bytes)> { ordered[0] };
    for (var i = 1; i < ordered.Count; i++)
    {
        var (name, bytes) = ordered[i];
        try
        {
            var cropPixels = DetectOverlapPixels(ordered[i - 1].Bytes, bytes);
            Console.WriteLine($"[precise-crop] {ordered[i - 1].Name} -> {name}: crop {cropPixels}px off top");
            if (cropPixels > 0)
            {
                using var image = SixLabors.ImageSharp.Image.Load(bytes);
                var top = Math.Min(cropPixels, image.Height - 1);
                image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(0, top, image.Width, image.Height - top)));
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                result.Add((name, ms.ToArray()));
                continue;
            }
        }
        catch
        {
            // If comparison/crop fails for this pair, use the image
            // uncropped rather than dropping it from the document.
        }
        result.Add((name, bytes));
    }
    return result;
}

// Returns how many pixels (in b's own coordinate space) to crop off b's top
// so it no longer duplicates a's bottom - 0 if no confident match is found.
// Compares a full downscaled 2D grayscale grid (not just one brightness
// number per row) - collapsing each row to a single average threw away the
// horizontal position of the text, so lots of unrelated rows of similar-
// density lyrics text looked equally "close", which is why the first version
// of this kept locking onto tiny, coincidental matches near the search floor
// instead of the real overlap.
static int DetectOverlapPixels(byte[] aBytes, byte[] bBytes)
{
    const int downscaleWidth = 64;
    try
    {
        var aGrid = DownscaleGrayscale(aBytes, downscaleWidth);
        var bGrid = DownscaleGrayscale(bBytes, downscaleWidth);
        var aH = aGrid.GetLength(0);
        var bH = bGrid.GetLength(0);
        var w = aGrid.GetLength(1);
        if (aH < 6 || bH < 6) return 0;

        var shortest = Math.Min(aH, bH);
        // Fixed chrome (header/player-bar) is stripped by the caller before
        // this runs, so the false-long-match problem that caused is gone -
        // search a normal range for genuine scroll overlap.
        var minLen = Math.Max(3, (int)(shortest * 0.03));
        var maxLen = (int)(shortest * 0.7);

        var overallBestLen = 0;
        var overallBestScore = double.MaxValue;

        for (var len = minLen; len <= maxLen; len++)
        {
            long diff = 0;
            for (var k = 0; k < len; k++)
            {
                var ay = aH - len + k;
                for (var x = 0; x < w; x++)
                    diff += Math.Abs(aGrid[ay, x] - bGrid[k, x]);
            }
            var score = (double)diff / (len * w);
            if (score < overallBestScore) { overallBestScore = score; overallBestLen = len; }
        }

        const double acceptThreshold = 33.0;
        var bestLen = overallBestScore < acceptThreshold ? overallBestLen : 0;

        if (bestLen == 0) return 0;
        using var bImage = SixLabors.ImageSharp.Image.Load(bBytes);
        var scale = (double)bImage.Height / bH;
        return (int)(bestLen * scale);
    }
    catch
    {
        return 0;
    }
}

static byte[,] DownscaleGrayscale(byte[] bytes, int targetWidth)
{
    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
    image.Mutate(ctx => ctx
        .Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(targetWidth, 0),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
        })
        .Grayscale());

    var grid = new byte[image.Height, image.Width];
    for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            grid[y, x] = image[x, y].R;
    return grid;
}

static string GuessMime(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
{
    ".png" => "image/png",
    ".gif" => "image/gif",
    ".webp" => "image/webp",
    _ => "image/jpeg"
};

static List<(string Name, byte[] Bytes)> CropTopExceptFirst(List<(string Name, byte[] Bytes)> ordered, int cropPercent)
{
    var result = new List<(string Name, byte[] Bytes)> { ordered[0] };
    for (var i = 1; i < ordered.Count; i++)
    {
        var (name, bytes) = ordered[i];
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            var cropHeight = image.Height * cropPercent / 100;
            if (cropHeight > 0 && cropHeight < image.Height)
            {
                image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(0, cropHeight, image.Width, image.Height - cropHeight)));
                using var ms = new MemoryStream();
                image.SaveAsPng(ms); // always re-encode as PNG - simplest, QuestPDF reads it fine regardless of the source format
                result.Add((name, ms.ToArray()));
                continue;
            }
        }
        catch
        {
            // If this image can't be decoded/cropped for some reason, use it
            // uncropped rather than dropping it from the document entirely.
        }
        result.Add((name, bytes));
    }
    return result;
}

static List<(string Name, byte[] Bytes)> NaturalSortEntries(List<(string Name, byte[] Bytes)> entries) =>
    entries
        .OrderBy(e =>
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(e.Name, @"\d+");
            return matches.Count > 0 ? int.Parse(matches[^1].Value) : int.MaxValue;
        })
        .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

static string ExtractText(Message response)
{
    var text = new System.Text.StringBuilder();
    foreach (var block in response.Content)
        if (block.TryPickText(out var t)) text.Append(t.Text);
    return text.ToString().Trim();
}

record DraftRequest(string Notes);
record CreateRequest(string? ClientId, string? Title, string? Body);
record ScreenshotOrderResult(List<string> Order);
record ContentRangeResult(string? FirstContentFilename, string? LastContentFilename);
record RowRangeItem(string Filename, int FirstRow, int LastRow);
record RowRangeResult(int WantedFirstRow, int WantedLastRow, List<RowRangeItem> Images);

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
