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

        var allowedTypes = new[]
        {
            "core",
            "expansion",
            "masters",
            "eternal",
            "commander",
            "draft_innovation",
            "starter",
            "funny"
        };
        var excludedTypes = new[]
        {
            "token",
            "promo",
            "memorabilia",
            "minigame"
        };

        var newSets =
            response.Data
                .Where(x =>
                    !excludedTypes.Contains(
                        x.SetType,
                        StringComparer.OrdinalIgnoreCase))
                .Where(x =>
                    !existingCodes.Contains(x.Code))
                .Select(set => new MagicSetKnowledge
                {
                    SetCode = set.Code,
                    SetName = set.Name,
                    SetType = set.SetType,
                    ReleaseYear = set.ReleasedAt.Year,

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

        await _db.SaveChangesAsync(cancellationToken);
    }
}