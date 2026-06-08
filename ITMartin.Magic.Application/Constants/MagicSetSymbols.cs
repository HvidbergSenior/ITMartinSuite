using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Constants;

public static class MagicSetSymbols
{
    public static readonly IReadOnlyList<
        MagicSetSymbolDefinition> All =
    [
        new("ARN", "Arabian Nights", "scimitar"),
        new("ATQ", "Antiquities", "anvil"),
        new("LEG", "Legends", "classical column"),
        new("DRK", "The Dark", "crescent moon"),
        new("FEM", "Fallen Empires", "crown"),
        new("ICE", "Ice Age", "snowflake"),
        new("ALL", "Alliances", "banner"),
        new("MIR", "Mirage", "palm tree"),
        new("VIS", "Visions", "eye"),
        new("WTH", "Weatherlight", "shooting star"),
        new("TMP", "Tempest", "cloud"),
        new("STH", "Stronghold", "castle"),
        new("EXO", "Exodus", "bridge"),
        new("USG", "Urza's Saga", "gear"),
        new("ULG", "Urza's Legacy", "hammer"),
        new("UDS", "Urza's Destiny", "mask"),
        new("MMQ", "Mercadian Masques", "mask"),
        new("NEM", "Nemesis", "crossed swords"),
        new("PCY", "Prophecy", "crystal ball"),
        new("INV", "Invasion", "shield"),
        new("PLS", "Planeshift", "portal"),
        new("APC", "Apocalypse", "explosion")
    ];
}