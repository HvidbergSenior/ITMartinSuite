using System.Net.Http.Json;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        foreach (var set in response.Data)
        {
            if (await _db.Sets.AnyAsync(
                    x => x.SetCode == set.Code,
                    cancellationToken))
            {
                continue;
            }

            _db.Sets.Add(
                new MagicSetKnowledge
                {
                    SetCode = set.Code,
                    SetName = set.Name,
                    ReleaseYear = set.Released_At.Year,

                    SymbolDescription = "",
                    SymbolKeywords = "",

                    HasSetSymbol = true,
                    UsesOldFrame = false,
                    UsesWhiteBorder = false,
                    UsesBlackBorder = true,
                    HasCollectorNumbers = true,
                    HasFoils = true
                });
        }

        await _db.SaveChangesAsync(
            cancellationToken);
    }
}