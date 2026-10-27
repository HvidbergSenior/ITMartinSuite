using System.Security.Cryptography;

namespace ITMartinCharacter.Domain;

// A spell or ability: what it is called and what it does (same shape as on the Game Master card).
public sealed record Spell(string Name, string Text);

// A line in the help thread on one step. From = "player" or "master" (user 2026-10-27: "a cooperation between game master and
// player ... game master should be able to help remote as well").
public sealed record Comment(long Id, string Step, string From, string FromName, string Text, DateTime At);

// One character, made by a player with the game master helping - at the same table or from another place.
// PlayerCode opens it for the player on any device, MasterCode for the game master. FigureCode is given when the figure is
// "Ready for adventure" and is what Game Master imports with. AdventureNotes, Hp and Gear come back from Game Master after play,
// so the figure carries on to the next adventure.
public sealed class Figure
{
    public string Id { get; set; } = "";
    /// <summary>"dnd", "mystery" or "coop" - the three game types in Game Master (user 2026-10-27).</summary>
    public string Kind { get; set; } = GameKinds.Dnd;
    public string PlayerName { get; set; } = "";
    public string PlayerCustomerNo { get; set; } = "";
    public string MasterName { get; set; } = "";
    public string PlayerCode { get; set; } = "";
    public string MasterCode { get; set; } = "";
    public string FigureCode { get; set; } = "";

    public string Name { get; set; } = "";
    public string People { get; set; } = "";
    public string Class { get; set; } = "";
    public string Background { get; set; } = "";
    /// <summary>STR DEX CON INT WIS CHA before the people bonus; 0 = not set.</summary>
    public int[] Abilities { get; set; } = new int[6];
    /// <summary>"array" (15 14 13 12 10 8), "roll" (4d6, drop the lowest - the server rolls) or "manual".</summary>
    public string AbilityMethod { get; set; } = "";
    public List<int> Rolls { get; set; } = [];
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public string Gear { get; set; } = "";
    public List<Spell> Spells { get; set; } = [];
    public string Skills { get; set; } = "";
    public string Look { get; set; } = "";
    public string Story { get; set; } = "";
    public string AdventureNotes { get; set; } = "";
    public string MasterNotes { get; set; } = "";
    public int Picture { get; set; }
    /// <summary>The step the player is on, so the game master can follow along.</summary>
    public string Step { get; set; } = Steps.Start;
    public bool Ready { get; set; }
    public bool Locked { get; set; }
    public List<Comment> Comments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class Viewers
{
    public const string Player = "player", Master = "master";
}

public static class GameKinds
{
    public const string Dnd = "dnd", Mystery = "mystery", Coop = "coop";
    public static readonly string[] All = [Dnd, Mystery, Coop];
}

public static class Steps
{
    public const string Start = "start", People = "people", Class = "class", Background = "background", Abilities = "abilities",
        Hp = "hp", Gear = "gear", Spells = "spells", Skills = "skills", Picture = "picture", Story = "story", Ready = "ready";

    /// <summary>D&amp;D gets the whole guided way; murder mystery and cooperative skip the dice parts (user: "free form").</summary>
    public static IReadOnlyList<string> For(string kind) => kind == GameKinds.Dnd
        ? [Start, People, Class, Background, Abilities, Hp, Gear, Spells, Picture, Story, Ready]
        : [Start, People, Class, Skills, Gear, Picture, Story, Ready];
}

// The free D&D 5e SRD 5.1 (CC-BY-4.0, Wizards of the Coast): peoples with their ability bonus, classes with their hit die.
// Any other text works too - the game master's world decides (user 2026-10-27: "setup can be everything like humans, orcs,
// old people, sci fi").
public static class Srd
{
    public static readonly string[] AbilityNames = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];
    public static readonly int[] StandardArray = [15, 14, 13, 12, 10, 8];

    public static readonly IReadOnlyDictionary<string, int[]> PeopleBonus = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Human"] = [1, 1, 1, 1, 1, 1],
        ["Elf"] = [0, 2, 0, 0, 0, 0],
        ["Dwarf"] = [0, 0, 2, 0, 0, 0],
        ["Halfling"] = [0, 2, 0, 0, 0, 0],
        ["Gnome"] = [0, 0, 0, 2, 0, 0],
        ["Half-Elf"] = [0, 0, 0, 0, 0, 2],
        ["Half-Orc"] = [2, 0, 1, 0, 0, 0],
        ["Dragonborn"] = [2, 0, 0, 0, 0, 1],
        ["Tiefling"] = [0, 0, 0, 1, 0, 2],
    };

    public static readonly IReadOnlyDictionary<string, int> HitDie = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Barbarian"] = 12, ["Fighter"] = 10, ["Paladin"] = 10, ["Ranger"] = 10, ["Bard"] = 8, ["Cleric"] = 8, ["Druid"] = 8,
        ["Monk"] = 8, ["Rogue"] = 8, ["Warlock"] = 8, ["Sorcerer"] = 6, ["Wizard"] = 6,
    };

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    /// <summary>The scores with the people bonus added (unknown people = no bonus).</summary>
    public static int[] Final(Figure f)
    {
        var bonus = PeopleBonus.TryGetValue(f.People.Trim(), out var b) ? b : new int[6];
        return f.Abilities.Select((s, i) => s == 0 ? 0 : s + bonus[i]).ToArray();
    }

    /// <summary>Level 1 hit points: the class's hit die at its highest + the CON modifier; null when class or CON is unknown.</summary>
    public static int? StartHp(Figure f)
    {
        if (!HitDie.TryGetValue(f.Class.Trim(), out var die)) return null;
        var con = Final(f)[2];
        return con == 0 ? null : Math.Max(1, die + Modifier(con));
    }
}

public static class FigureRules
{
    public const int MaxName = 60, MaxShort = 60, MaxLong = 3000, MaxSpells = 40, MaxComment = 1000, MaxComments = 500;
    public const int MaxHpLimit = 999, MinHp = -99, MaxPictureBytes = 600_000;
    public static readonly TimeSpan KeepIdle = TimeSpan.FromDays(400);

    // No 0/O/1/I/L - codes are read aloud and typed on phones.
    private const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // The text fields and who may write them. MasterNotes is the game master's own; AdventureNotes comes from Game Master.
    public static readonly IReadOnlyDictionary<string, int> TextFields = new Dictionary<string, int>
    {
        ["name"] = MaxName, ["people"] = MaxShort, ["class"] = MaxShort, ["background"] = MaxShort, ["gear"] = MaxLong,
        ["skills"] = MaxLong, ["look"] = 300, ["story"] = MaxLong, ["masterNotes"] = MaxLong, ["adventureNotes"] = MaxLong,
        ["masterName"] = 30, ["playerName"] = 30,
    };

    public static string Cut(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length > max ? t[..max] : t;
    }

    /// <summary>Like Cut but keeps spaces at the end - the other person may be typing "Thorin " right now.</summary>
    public static string Clip(string? s, int max)
    {
        var t = s ?? "";
        return t.Length > max ? t[..max] : t;
    }

    public static string NewCode(int length) =>
        new(Enumerable.Range(0, length).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());

    public static string CleanCode(string? code) => (code ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

    /// <summary>A kundenummer is 8 digits ("4821 7730" is fine); "" when the text is not one.</summary>
    public static string CleanCustomerNo(string? s)
    {
        var d = new string((s ?? "").Where(char.IsAsciiDigit).ToArray());
        return d.Length == 8 && (s ?? "").All(c => char.IsAsciiDigit(c) || c is ' ' or '-') ? d : "";
    }

    public static Figure New(string? playerName, string? kind, DateTime nowUtc, string customerNo = "") => new()
    {
        Id = Guid.NewGuid().ToString("N")[..12],
        Kind = GameKinds.All.Contains(kind) ? kind! : GameKinds.Dnd,
        PlayerName = Cut(playerName, 30),
        PlayerCustomerNo = customerNo,
        PlayerCode = NewCode(8),
        MasterCode = NewCode(8),
        Hp = 10,
        MaxHp = 10,
        CreatedAt = nowUtc,
        UpdatedAt = nowUtc,
    };

    /// <summary>
    /// The game master may always change the figure; the player only until it is ready for adventure (then it is locked,
    /// and only the game master can open it again - the same rule as the card in Game Master).
    /// </summary>
    public static bool MayEdit(Figure f, string viewer) => viewer == Viewers.Master || !f.Locked;

    /// <summary>One text field. False = not a field, or not this viewer's to write.</summary>
    public static bool SetText(Figure f, string viewer, string field, string? value)
    {
        if (!TextFields.TryGetValue(field, out var max)) return false;
        if (field is "masterNotes" or "masterName" or "adventureNotes" && viewer != Viewers.Master) return false;
        if (field is not ("masterNotes" or "masterName" or "adventureNotes") && !MayEdit(f, viewer)) return false;
        var v = Clip(value, max);
        switch (field)
        {
            case "name": f.Name = v; break;
            case "people": f.People = v; break;
            case "class": f.Class = v; break;
            case "background": f.Background = v; break;
            case "gear": f.Gear = v; break;
            case "skills": f.Skills = v; break;
            case "look": f.Look = v; break;
            case "story": f.Story = v; break;
            case "masterNotes": f.MasterNotes = v; break;
            case "adventureNotes": f.AdventureNotes = v; break;
            case "masterName": f.MasterName = v; break;
            case "playerName": if (v.Trim().Length > 0) f.PlayerName = v; break;
        }
        return true;
    }

    public static bool SetKind(Figure f, string viewer, string? kind)
    {
        if (!MayEdit(f, viewer) || !GameKinds.All.Contains(kind)) return false;
        f.Kind = kind!;
        if (!Steps.For(f.Kind).Contains(f.Step)) f.Step = Steps.Start;
        return true;
    }

    public static bool SetStep(Figure f, string viewer, string? step)
    {
        if (viewer != Viewers.Player || !Steps.For(f.Kind).Contains(step)) return false;
        f.Step = step!;
        return true;
    }

    /// <summary>
    /// The ability scores. "array" must use 15 14 13 12 10 8 once each, "roll" the server's six rolls once each, "manual" any
    /// 3-18 (the game master's house rules). 0 = not placed yet, so a half-done assignment can be saved.
    /// </summary>
    public static bool SetAbilities(Figure f, string viewer, string? method, int[]? scores)
    {
        if (!MayEdit(f, viewer) || scores is not { Length: 6 }) return false;
        var placed = scores.Where(s => s != 0).ToList();
        switch (method)
        {
            case "array":
                if (!IsSubBag(placed, Srd.StandardArray)) return false;
                break;
            case "roll":
                if (f.Rolls.Count != 6 || !IsSubBag(placed, f.Rolls)) return false;
                break;
            case "manual":
                if (placed.Any(s => s is < 3 or > 18)) return false;
                break;
            default: return false;
        }
        f.AbilityMethod = method;
        f.Abilities = scores.ToArray();
        return true;
    }

    // Every placed score is taken from the pool, each at most as many times as it is there.
    private static bool IsSubBag(IEnumerable<int> placed, IEnumerable<int> pool)
    {
        var left = pool.ToList();
        foreach (var s in placed) if (!left.Remove(s)) return false;
        return true;
    }

    /// <summary>Six rolls of 4d6, the lowest die dropped each time. Rolling again clears what was placed.</summary>
    public static bool RollAbilities(Figure f, string viewer, Func<int, int> random)
    {
        if (!MayEdit(f, viewer)) return false;
        f.Rolls = Enumerable.Range(0, 6).Select(_ =>
        {
            var d = Enumerable.Range(0, 4).Select(_ => random(6) + 1).OrderBy(x => x).ToList();
            return d.Skip(1).Sum();
        }).ToList();
        f.AbilityMethod = "roll";
        f.Abilities = new int[6];
        return true;
    }

    public static bool SetHp(Figure f, string viewer, int hp, int maxHp)
    {
        if (!MayEdit(f, viewer)) return false;
        f.MaxHp = Math.Clamp(maxHp, 1, MaxHpLimit);
        f.Hp = Math.Clamp(hp, MinHp, f.MaxHp);
        return true;
    }

    public static bool SetSpells(Figure f, string viewer, IEnumerable<Spell>? spells)
    {
        if (!MayEdit(f, viewer)) return false;
        f.Spells = (spells ?? [])
            .Select(s => new Spell(Clip(s.Name, 60), Clip(s.Text, MaxLong)))
            .Take(MaxSpells).ToList();
        return true;
    }

    /// <summary>"Ready for adventure": the figure gets its code for Game Master and is locked for the player.</summary>
    public static bool MakeReady(Figure f, string viewer, Func<string, bool> codeTaken)
    {
        if (!MayEdit(f, viewer) || Cut(f.Name, MaxName).Length == 0) return false;
        if (f.FigureCode.Length == 0)
        {
            string c;
            do c = NewCode(6); while (codeTaken(c));
            f.FigureCode = c;
        }
        f.Ready = true;
        f.Locked = true;
        f.Step = Steps.Ready;
        return true;
    }

    /// <summary>The game master opens the figure for changes again (fx after a level up), or locks it.</summary>
    public static bool SetLocked(Figure f, string viewer, bool locked)
    {
        if (viewer != Viewers.Master) return false;
        f.Locked = locked;
        return true;
    }

    public static Comment? NewComment(Figure f, string viewer, string? step, string? text, DateTime nowUtc)
    {
        var t = Cut(text, MaxComment);
        if (t.Length == 0) return null;
        var s = Steps.For(f.Kind).Contains(step) ? step! : f.Step;
        var name = viewer == Viewers.Master ? (f.MasterName.Trim() is { Length: > 0 } m ? m : "Game master") : f.PlayerName;
        var c = new Comment((f.Comments.Count == 0 ? 0 : f.Comments[^1].Id) + 1, s, viewer, name, t, nowUtc);
        f.Comments.Add(c);
        if (f.Comments.Count > MaxComments) f.Comments.RemoveRange(0, f.Comments.Count - MaxComments);
        return c;
    }

    /// <summary>After play: Game Master sends hit points, gear and what happened, so the figure carries on.</summary>
    public static void BackFromAdventure(Figure f, int hp, int maxHp, string? gear, string? notes)
    {
        f.MaxHp = Math.Clamp(maxHp, 1, MaxHpLimit);
        f.Hp = Math.Clamp(hp, MinHp, f.MaxHp);
        f.Gear = Clip(gear, MaxLong);
        f.AdventureNotes = Clip(notes, MaxLong);
    }

    /// <summary>A character picture must be a JPEG (the phone shrinks it before sending) and not too big.</summary>
    public static bool IsPicture(byte[] bytes) =>
        bytes.Length is > 100 and <= MaxPictureBytes && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    /// <summary>
    /// The figure as one text for the Game Master card: people/class line is separate there, so this is abilities,
    /// background, skills, look and story.
    /// </summary>
    public static string Sheet(Figure f)
    {
        var parts = new List<string>();
        var fin = Srd.Final(f);
        if (fin.Any(s => s > 0))
            parts.Add(string.Join("  ", fin.Select((s, i) => s == 0 ? null : $"{Srd.AbilityNames[i]} {s} ({(Srd.Modifier(s) >= 0 ? "+" : "")}{Srd.Modifier(s)})")
                .Where(x => x is not null)));
        if (f.Background.Trim().Length > 0) parts.Add("Background: " + f.Background.Trim());
        if (f.Skills.Trim().Length > 0) parts.Add("Skills: " + f.Skills.Trim());
        if (f.Look.Trim().Length > 0) parts.Add("Looks: " + f.Look.Trim());
        if (f.Story.Trim().Length > 0) parts.Add(f.Story.Trim());
        return string.Join("\n\n", parts);
    }
}
