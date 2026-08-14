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

        // Only the set types someone actually plays with: mainline
        // core/expansion sets and reprint "masters" sets. Commander
        // precons, promos, duel decks, box toppers, starter/planeswalker
        // decks, Un-sets, planechase/archenemy/vanguard oversized cards,
        // and similar are real Scryfall set types but not what "which set
        // is this card from" means for a normal collection.
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "core", "expansion", "masters",
        };

        // Scryfall doesn't flag "Universes Beyond" licensed crossovers with
        // any set-level boolean - they're regular "expansion"/"commander"/
        // "eternal" sets as far as the API is concerned, indistinguishable
        // by type from real Magic sets. Exact set codes (pulled from a full
        // review of every non-digital Scryfall set) are precise where a
        // name-keyword match would risk both false positives (a real Magic
        // set that happens to share a word) and false negatives (a crossover
        // whose name doesn't match any guessed keyword). Add the full family
        // of codes for a franchise (base set + art series + tokens + promos
        // + commander + minigames etc.), not just the main expansion, since
        // Scryfall gives each of those its own set entry.
        var crossoverSetCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Assassin's Creed
            "acr", "aacr", "macr", "tacr",
            // Avatar: The Last Airbender (both the original and Eternal sub-line)
            "tla", "atla", "ftla", "tle", "atle", "ttle", "jtla", "ptla", "ttla",
            // Cowboy Bebop
            "pcbb",
            // Doctor Who
            "who", "twho",
            // Fallout
            "pip", "tpip",
            // Final Fantasy
            "fin", "afin", "fic", "tfic", "pfin", "rfin", "afic", "tfin", "fca",
            // Jurassic World
            "rex", "trex",
            // Marvel (Super Heroes, Spider-Man, Universe, Legends inserts)
            "msh", "amsh", "msc", "tmsc", "fmsc", "tmsh",
            "mar", "spm", "aspm", "spe", "pspm", "tspm", "lmar",
            // My Little Pony
            "ptg",
            // Star Trek
            "trk", "trc", "ttrk",
            // Teenage Mutant Ninja Turtles
            "tmt", "atmt", "tmc", "ftmc", "ttmc", "pza", "ttmt",
            // Transformers
            "bot", "tbot",
            // Warhammer 40,000
            "40k", "t40k",
            // Lord of the Rings / The Hobbit (Tolkien license)
            "hob", "hoc", "thob",
            "altr", "ltc", "tltc", "pltc", "fltr", "pltr", "altc", "tltr", "ltr", "mltr",
            // Duel Masters crossover promos
            "pmda",
        };

        var eligible = response.Data
            .Where(x => allowedTypes.Contains(x.SetType) && !x.Digital)
            .Where(x => !crossoverSetCodes.Contains(x.Code))
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

        // Refresh icon URLs and set type for existing sets. Sets imported
        // before SetType tracking was added were never backfilled, so their
        // SetType stayed blank forever.
        var iconMap = eligible.ToDictionary(x => x.Code, x => x.IconSvgUri);
        var typeMap = eligible.ToDictionary(x => x.Code, x => x.SetType);
        var existing = await _db.Sets
            .Where(x => existingCodes.Contains(x.SetCode))
            .ToListAsync(cancellationToken);

        foreach (var set in existing)
        {
            if (set.IconSvgUri == "" && iconMap.TryGetValue(set.SetCode, out var uri))
                set.IconSvgUri = uri;
            if (typeMap.TryGetValue(set.SetCode, out var setType))
                set.SetType = setType;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}