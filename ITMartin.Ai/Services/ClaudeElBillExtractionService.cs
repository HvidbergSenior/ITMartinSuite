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

public sealed class ClaudeElBillExtractionService : IElBillExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleDecimalConverter() }
    };

    private static readonly Tool ReportBillTool = new()
    {
        Name = "report_el_bill",
        Description = "Report the extracted electricity bill data",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["gridCompanyName"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Name of the grid/distribution company (netselskab), e.g. Radius, Cerius, N1, Trefor El-net" }),
                ["supplierName"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Name of the electricity supplier/retailer (elleverandør), e.g. Norlys, Andel Energi, Vindstød, OK" }),
                ["nettarifOrePerKwh"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Grid transport tariff (nettarif/transport) in øre per kWh, ex VAT. If multiple time-of-day rates are shown (lav/høj/spidslast), report the most common or average one." }),
                ["elafgiftOrePerKwh"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Electricity tax (elafgift) in øre per kWh, ex VAT" }),
                ["supplierMarkupOrePerKwh"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "The supplier's own markup/tillæg on top of the spot price, in øre per kWh, ex VAT" }),
                ["gridMonthlySubscriptionKr"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Fixed monthly subscription fee charged by the grid company, in DKK" }),
                ["supplierMonthlySubscriptionKr"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Fixed monthly subscription fee charged by the supplier, in DKK" }),
                ["totalAmountKr"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Total amount due on the bill, in DKK" }),
                ["billingPeriod"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "The billing period covered, e.g. '2026-06-01 to 2026-06-30'" }),
            },
            Required = [],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeElBillExtractionService> _logger;

    public ClaudeElBillExtractionService(
        IConfiguration configuration,
        ILogger<ClaudeElBillExtractionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ElBillExtractionResult> ExtractFromImageAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);

        const int MaxBytes = 4 * 1024 * 1024;
        if (bytes.Length > MaxBytes)
        {
            using var image = Image.Load(bytes);
            const int MaxDimension = 1600;
            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                var ratio = Math.Min((double)MaxDimension / image.Width, (double)MaxDimension / image.Height);
                image.Mutate(x => x.Resize(
                    Math.Max(1, (int)(image.Width * ratio)),
                    Math.Max(1, (int)(image.Height * ratio))));
            }
            var quality = 85;
            byte[] resized;
            do
            {
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms,
                    new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality },
                    cancellationToken);
                resized = ms.ToArray();
                quality -= 10;
            } while (resized.Length > MaxBytes && quality > 20);
            bytes = resized;
        }

        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(imagePath).ToLowerInvariant();
        var mime = ext == ".png" ? "image/png" : "image/jpeg";

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1024,
            System = """
                You are an extraction system for Danish electricity bills (elregninger).
                Danish bills combine several separate cost components on one invoice: the
                grid company's transport tariff (nettarif), the state electricity tax
                (elafgift), the supplier's own markup (tillæg) on top of the spot price,
                and one or two monthly subscription fees (abonnement - one from the grid
                company, one from the supplier). Identify the grid company and supplier by
                name, and extract each cost component separately - do not merge them into
                one number. Numbers may use Danish comma decimals (e.g. "23,56").
                Omit fields you cannot determine — never guess.
                """,
            Tools = [ReportBillTool],
            ToolChoice = new ToolChoiceTool { Name = "report_el_bill" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam
                        {
                            Text = "Extract the electricity bill data from this image and call the report_el_bill tool."
                        },
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

        if (toolUse is null)
            throw new InvalidOperationException("Claude did not call the report_el_bill tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude el-bill response: {Json}", json);

        var result = JsonSerializer.Deserialize<ElBillExtractionResult>(json, JsonOptions);
        return result ?? throw new InvalidOperationException("Failed to deserialize el-bill result.");
    }
}

// Handles both "14.95" (JSON number or dot string) and "14,95" (Danish comma string)
file sealed class FlexibleDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDecimal();

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()?.Replace(',', '.');
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}
