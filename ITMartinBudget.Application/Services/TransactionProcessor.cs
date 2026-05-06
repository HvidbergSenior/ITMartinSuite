using System.Text.RegularExpressions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class TransactionProcessor : ITransactionProcessor
{
    private readonly ICategoryService _category;

    public TransactionProcessor(
        ICategoryService category)
    {
        _category = category;
    }

    public async Task ProcessAsync(BankTransaction tx)
    {
        // =====================================
        // NORMALIZE DESCRIPTION
        // =====================================

        tx.Description =
            NormalizeDescription(tx.Description);

        // =====================================
        // TRANSACTION TYPE
        // =====================================

        tx.TransactionType =
            tx.Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift;

        // =====================================
        // CATEGORY
        // =====================================

        tx.Category =
            await _category.DetectAsync(tx.Description);
    }
    private string NormalizeDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // =====================================
        // LOWERCASE
        // =====================================

        text = text.ToLowerInvariant();

        // =====================================
        // REMOVE LONG IDS
        // =====================================

        text = Regex.Replace(
            text,
            @"p[a-z0-9]{6,}",
            "");

        text = Regex.Replace(
            text,
            @"\d{4,}",
            "");

        // =====================================
        // KEEP IMPORTANT SYMBOLS
        // =====================================

        text = Regex.Replace(
            text,
            @"[^a-zæøå0-9\.\/\- ]",
            " ");

        // =====================================
        // NORMALIZE SPACES
        // =====================================

        text = Regex.Replace(
            text,
            @"\s+",
            " ")
            .Trim();

        // =====================================
        // REMOVE NOISE WORDS
        // =====================================

        var noiseWords = new[]
        {
            "dk",
            "vdk",
            "visa",
            "mastercard",
            "betaling",
            "konto",
            "fra",
            "til",
            "aps",
            "a/s",
            "as",
            "bs"
        };

        foreach (var noise in noiseWords)
        {
            text = Regex.Replace(
                text,
                $@"\b{Regex.Escape(noise)}\b",
                "");
        }

        // =====================================
        // FINAL SPACE NORMALIZATION
        // =====================================

        text = Regex.Replace(
            text,
            @"\s+",
            " ")
            .Trim();

        return text;
    }
}