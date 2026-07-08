using System.Net.Http.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class MagicSetImportService
    : IMagicSetImportService
{
    private readonly HttpClient _httpClient;
    private readonly MagicDbContext _db;

    public MagicSetImportService(
        HttpClient httpClient,
        MagicDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    public async Task ImportAsync(
        CancellationToken cancellationToken)
    {
        var response =
            await _httpClient.GetFromJsonAsync<ScryfallSetsResponse>(
                "sets",
                cancellationToken);

        if (response is null)
        {
            return;
        }

        var existingCodes =
            (await _db.Sets
                .Select(x => x.SetCode)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "core",
            "expansion",
            "masters"
        };

        var eligible = response.Data
            .Where(x => allowedTypes.Contains(x.SetType) && !x.Digital)
            .ToList();

        // Remove sets that no longer match allowed types
        var eligibleCodes = eligible.Select(x => x.Code).ToHashSet();
        var toRemove = await _db.Sets
            .Where(x => !eligibleCodes.Contains(x.SetCode))
            .ToListAsync(cancellationToken);
        _db.Sets.RemoveRange(toRemove);

        var newSets = eligible
            .Where(x => !existingCodes.Contains(x.Code))
            .Select(set => new MagicSetKnowledge
            {
                SetCode = set.Code,
                SetName = set.Name,
                SetType = set.SetType,
                ReleaseYear = set.ReleasedAt.Year,
                IconSvgUri = set.IconSvgUri,

                SymbolDescription = "",
                SymbolKeywords = "",
                SymbolColor = "",
                SymbolShape = "",

                FrameStyle = "",
                CopyrightStyle = "",
                CopyrightYear = null,

                HasSetSymbol = true,
                UsesOldFrame = false,
                UsesWhiteBorder = false,
                UsesBlackBorder = true,

                HasCollectorNumbers = true,
                HasFoils = true
            });

        _db.Sets.AddRange(newSets);

        // Refresh icon URLs for existing sets
        var iconMap = eligible.ToDictionary(x => x.Code, x => x.IconSvgUri);
        var existing = await _db.Sets
            .Where(x => existingCodes.Contains(x.SetCode) && x.IconSvgUri == "")
            .ToListAsync(cancellationToken);

        foreach (var set in existing)
            if (iconMap.TryGetValue(set.SetCode, out var uri))
                set.IconSvgUri = uri;

        await _db.SaveChangesAsync(cancellationToken);
    }
}