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

public sealed class ClaudeReceiptExtractionService
    : IReceiptExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleDecimalConverter() }
    };

    private static readonly Tool ReportReceiptTool = new()
    {
        Name = "report_receipt",
        Description = "Report the extracted receipt data",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["merchantName"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Store or merchant name" }),
                ["purchaseDate"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Date of purchase in ISO 8601 format" }),
                ["totalAmount"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Total amount paid" }),
                ["vatAmount"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "VAT / tax amount" }),
                ["currency"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Currency code e.g. DKK, EUR, USD" }),
                ["items"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "Every purchased product on the receipt, one entry per product. Do not report a discount/coupon line as its own separate entry — fold it into the product it discounts.",
                        "items": {
                            "type": "object",
                            "properties": {
                                "description":    { "type": "string" },
                                "amount":         { "type": "number" },
                                "discountAmount":  { "type": "number", "description": "If a discount, coupon, or member price applies to this product, the discount as a negative number. Omit if no discount applies." },
                                "discountLabel":   { "type": "string", "description": "Exact wording of the discount as printed on the receipt, e.g. Rabat, Tilbud, Fordelspris, Medlemsrabat, Pluskupon. Omit if no discount applies." },
                                "rawText":         { "type": "string", "description": "The exact text as printed on the receipt for this product's line(s), verbatim, including quantity/unit-price notation and its discount line if any (e.g. 'BASIC SOKKER 3STK 3 X 70,00 LINJERABAT -110,00'). Used so the user can compare the app's reading against what the receipt actually says — copy it letter-for-letter, don't paraphrase or clean it up." },
                                "suspicious":  { "type": "boolean", "description": "True if the price looks wrong or unusually high for this item type (e.g. bananas at 150 DKK)" }
                            },
                            "required": ["description"]
                        }
                    }
                    """).RootElement,
                ["loyaltyAccount"] = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "description": "Only if the receipt shows a store loyalty/membership program section separate from the per-item discounts above (e.g. 'LidlPlus konto', a Fordelskort/Bilka Plus/Føtex Plus summary, a Coop medlem section, or a REMA 1000 Æ summary) — typically a running total saved, points balance, or member number. Omit entirely if the receipt has no such section.",
                        "properties": {
                            "programName":       { "type": "string", "description": "Name of the loyalty program as printed, e.g. LidlPlus, Føtex Fordelskort, Coop medlem" },
                            "accountIdentifier": { "type": "string", "description": "Member/card number if printed on the receipt" },
                            "totalSaved":        { "type": "number", "description": "Total saved via this loyalty program, if printed as its own figure separate from item discounts" }
                        }
                    }
                    """).RootElement
            },
            Required = [],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeReceiptExtractionService> _logger;

    public ClaudeReceiptExtractionService(
        IConfiguration configuration,
        ILogger<ClaudeReceiptExtractionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ReceiptExtractionResult> ExtractAsync(
        string receiptText,
        ReceiptExtractionResult? template = null,
        CancellationToken cancellationToken = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1024,
            System = """
                You are a receipt extraction system for Danish grocery receipts from any store (Føtex, Bilka, Netto, Lidl, Rema 1000, Coop, Sport24, etc.).
                Report one item per purchased product. If a discount, coupon, or member price applies to a product (printed as e.g. 'Rabat', 'Tilbud', 'Fordelspris', 'Medlemsrabat', 'Lidl Plus-kupon', 'Linjerabat'), attach it to that product via discountAmount/discountLabel instead of reporting it as a separate item.
                amount must always be the ORIGINAL pre-discount price for the item. If the receipt has a quantity/unit-price layout (e.g. 'Antal: 3 x 70'), the original amount is quantity times unit price (210 in that example) — use that, never a column that already has the discount subtracted out of it.
                Some receipts print the item's final column as the amount already AFTER its own discount (i.e. the discount is already subtracted from the number in that column). If so, add the discount back on top of that printed number to get the original pre-discount amount — do not treat an already-discounted number as the original and then subtract the discount from it again.
                Sanity check before reporting: amount + discountAmount must equal the actual final price paid for that line, and must never be negative. If your numbers would make it negative, you have the original/discount reversed — re-derive amount from quantity x unit price instead.
                Set suspicious=true if the price seems obviously wrong for the item (e.g. bananas at 150 DKK, bread at 500 DKK) or if you are unsure whether you resolved a printed-amount-vs-discount ambiguity correctly.
                If the receipt separately shows a store loyalty/membership account section (e.g. 'LidlPlus konto', Fordelskort/Plus summary, Coop medlem, REMA 1000 Æ) distinct from the per-item discounts, report it via loyaltyAccount.
                For every item, also copy its rawText verbatim from the receipt (including its quantity/unit-price notation and discount line) so the user can compare your reading against the original — do not paraphrase this field.
                Omit fields you cannot determine — never guess.
                """,
            Tools = [ReportReceiptTool],
            ToolChoice = new ToolChoiceTool { Name = "report_receipt" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = template is null
                        ? $"""
                           Extract the receipt data from the following text and call the report_receipt tool.

                           Receipt text:

                           {receiptText}
                           """
                        : $"""
                           Extract the receipt data from the following text and call the report_receipt tool.

                           Use this verified receipt from the same store as a structural reference — it was corrected by hand, so match its conventions for how items, quantities, discounts, and loyalty info are resolved:
                           {JsonSerializer.Serialize(template, JsonOptions)}

                           Receipt text:

                           {receiptText}
                           """
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu))
            {
                toolUse = tu;
                break;
            }
        }

        if (toolUse is null)
            throw new InvalidOperationException("Claude did not call the report_receipt tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude receipt response: {Json}", json);

        var result = JsonSerializer.Deserialize<ReceiptExtractionResult>(json, JsonOptions);

        return result ?? throw new InvalidOperationException("Failed to deserialize receipt result.");
    }

    public async Task<ReceiptExtractionResult> ExtractFromImageAsync(
        string imagePath,
        ReceiptExtractionResult? template = null,
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
                using var ms = new System.IO.MemoryStream();
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
                You are a receipt extraction system for Danish grocery receipts from any store (Føtex, Bilka, Netto, Lidl, Rema 1000, Coop, Sport24, etc.).
                Report one item per purchased product. If a discount, coupon, or member price applies to a product (printed as e.g. 'Rabat', 'Tilbud', 'Fordelspris', 'Medlemsrabat', 'Lidl Plus-kupon', 'Linjerabat'), attach it to that product via discountAmount/discountLabel instead of reporting it as a separate item.
                amount must always be the ORIGINAL pre-discount price for the item. If the receipt has a quantity/unit-price layout (e.g. 'Antal: 3 x 70'), the original amount is quantity times unit price (210 in that example) — use that, never a column that already has the discount subtracted out of it.
                Some receipts print the item's final column as the amount already AFTER its own discount (i.e. the discount is already subtracted from the number in that column). If so, add the discount back on top of that printed number to get the original pre-discount amount — do not treat an already-discounted number as the original and then subtract the discount from it again.
                Sanity check before reporting: amount + discountAmount must equal the actual final price paid for that line, and must never be negative. If your numbers would make it negative, you have the original/discount reversed — re-derive amount from quantity x unit price instead.
                Set suspicious=true if the price seems obviously wrong for the item (e.g. bananas at 150 DKK, bread at 500 DKK) or if you are unsure whether you resolved a printed-amount-vs-discount ambiguity correctly.
                If the receipt separately shows a store loyalty/membership account section (e.g. 'LidlPlus konto', Fordelskort/Plus summary, Coop medlem, REMA 1000 Æ) distinct from the per-item discounts, report it via loyaltyAccount.
                For every item, also copy its rawText verbatim from the receipt (including its quantity/unit-price notation and discount line) so the user can compare your reading against the original — do not paraphrase this field.
                Omit fields you cannot determine — never guess.
                """,
            Tools = [ReportReceiptTool],
            ToolChoice = new ToolChoiceTool { Name = "report_receipt" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam
                        {
                            Text = template is null
                                ? "Extract the receipt data from this image and call the report_receipt tool."
                                : $"""
                                   Extract the receipt data from this image and call the report_receipt tool.

                                   Use this verified receipt as a structural reference — follow the same format for items, discounts, and grouping:
                                   {JsonSerializer.Serialize(template, JsonOptions)}
                                   """
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
            throw new InvalidOperationException("Claude did not call the report_receipt tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude receipt image response: {Json}", json);

        var result = JsonSerializer.Deserialize<ReceiptExtractionResult>(json, JsonOptions);
        return result ?? throw new InvalidOperationException("Failed to deserialize receipt result.");
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
