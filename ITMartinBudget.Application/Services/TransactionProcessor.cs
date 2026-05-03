using System.Globalization;
using System.Text.RegularExpressions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class TransactionProcessor : ITransactionProcessor
{
    private readonly ITransactionGroupingService _grouping;
    private readonly ICategoryService _category;
    private readonly INameNormalizer _nameNormalizer;

    public TransactionProcessor(
        ITransactionGroupingService grouping,
        ICategoryService category,
        INameNormalizer nameNormalizer)
    {
        _grouping = grouping;
        _category = category;
        _nameNormalizer = nameNormalizer;
    }

    public async Task ProcessAsync(BankTransaction tx)
    {
        // Normalize MobilePay
        if (tx.IsMobilePay)
        {
            var name = ExtractMobilePayName(tx.Description);
            tx.MobilePayName = _nameNormalizer.Normalize(name);
        }

        // Grouping
        var groupingKey = _grouping.GetGroupingKey(tx);
        tx.NormalizedDescription = groupingKey;

        // Classification
        tx.SubCategory = await _category.DetectAsync(groupingKey, tx);

        // Map category
        tx.Category = CategoryMapper.Map(tx.SubCategory);
    }

    private string? ExtractMobilePayName(string description)
    {
        var cleaned = description
            .Replace("mobilepay", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        cleaned = Regex.Replace(cleaned, @"[^a-zA-ZæøåÆØÅ ]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? null
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());
    }
}