using System.Security.Cryptography;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Ai.Services;

public sealed class ClaudeMagazineIdentificationService : IMagazineIdentificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Tool IdentifyTool = new()
    {
        Name = "identify_magazine",
        Description = "Report the identified magazine details from the cover image",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["title"] = JsonDocument.Parse("""{"type":"string","description":"Magazine title as printed on the cover"}""").RootElement,
                ["issueDate"] = JsonDocument.Parse("""{"type":"string","description":"Issue date as printed, e.g. 'April 1952' or 'Nr. 12 1965'"}""").RootElement,
                ["year"] = JsonDocument.Parse("""{"type":"integer","description":"Publication year as a number"}""").RootElement,
                ["publisher"] = JsonDocument.Parse("""{"type":"string","description":"Publisher name if visible"}""").RootElement,
                ["country"] = JsonDocument.Parse("""{"type":"string","enum":["Danish","American","English","Other"],"description":"Country of origin: Danish for Danish publications, American for US, English for UK/British, Other for rest"}""").RootElement,
                ["condition"] = JsonDocument.Parse("""{"type":"string","enum":["Mint","Good","Fair","Poor"],"description":"Physical condition based on what you can see in the image"}""").RootElement,
                ["valueRating"] = JsonDocument.Parse("""{"type":"string","enum":["Unknown","Common","Interesting","Valuable"],"description":"Collector value estimate: Valuable = rare, historically significant, or highly sought. Interesting = some collector interest. Common = widely available. Unknown = cannot determine."}""").RootElement,
                ["aiReasoning"] = JsonDocument.Parse("""{"type":"string","description":"Brief explanation of value rating and any notable characteristics of this magazine"}""").RootElement
            },
            Required = ["title", "country", "valueRating", "aiReasoning"]
        }
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeMagazineIdentificationService> _logger;

    public ClaudeMagazineIdentificationService(
        IConfiguration configuration,
        ILogger<ClaudeMagazineIdentificationService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<MagazineIdentificationResult?> IdentifyAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);

            using (var image = Image.Load(bytes))
            {
                const int MaxWidth = 1920;
                if (image.Width > MaxWidth)
                {
                    var ratio = (double)MaxWidth / image.Width;
                    image.Mutate(x => x.Resize(MaxWidth, (int)(image.Height * ratio)));
                }
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms,
                    new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 },
                    cancellationToken);
                bytes = ms.ToArray();
            }

            var mime = GetMimeType(imagePath);
            var base64 = Convert.ToBase64String(bytes);

            var request = new MessageCreateParams
            {
                Model = Model.ClaudeOpus4_8,
                MaxTokens = 1024,
                System = """
                    You are an expert in vintage magazines and periodicals from 1920-1990, specialising in Danish, American, and British publications.
                    When shown a magazine cover, identify it and assess its collector value.
                    Valuable magazines include: early issues, wartime editions, celebrity covers that became famous, first appearances,
                    limited print runs, or magazines covering historically significant events.
                    Danish magazines of historical interest include Se og Hør, Hjemmet, Billed-Bladet, Familie Journalen, Politiken's Weekend.
                    Respond only in English.
                    """,
                Tools = [IdentifyTool],
                ToolChoice = new ToolChoiceTool { Name = "identify_magazine" },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new TextBlockParam { Text = "Please identify this magazine and assess its collector value. Call the identify_magazine tool." },
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource { Data = base64, MediaType = mime }
                            }
                        }
                    }
                ]
            };

            var response = await _client.Messages.Create(request, cancellationToken);

            ToolUseBlock? toolUse = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
            }

            if (toolUse is null) return null;

            var json = JsonSerializer.Serialize(toolUse.Input);
            _logger.LogInformation("Magazine AI response: {Json}", json);

            return JsonSerializer.Deserialize<MagazineIdentificationResult>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Magazine identification failed for {Path}", imagePath);
            throw;
        }
    }

    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
}
