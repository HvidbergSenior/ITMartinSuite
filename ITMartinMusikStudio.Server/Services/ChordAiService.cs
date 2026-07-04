using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinMusikStudio.Server.Services;

public sealed class ChordAiService
{
    private readonly AnthropicClient? _client;

    public ChordAiService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<string> SuggestChordsAsync(string title, string musicKey, int? tempo, string lyrics)
    {
        if (_client is null) return "";

        var context = new System.Text.StringBuilder();
        context.Append($"Song title: {title}");
        if (tempo.HasValue) context.Append($"\nTempo: {tempo} BPM");
        if (!string.IsNullOrWhiteSpace(lyrics))
            context.Append($"\nFirst lines of lyrics:\n{string.Join("\n", lyrics.Split('\n').Take(8))}");

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 600,
            System = """
                You are a musician helping suggest chord progressions.
                If you recognise the song title and artist, use the ACTUAL chord structure from that song.
                Each section (Verse, Chorus, Bridge) may have DIFFERENT chords — preserve that structure.
                Include slash chords (inversions) exactly as they appear in the song — e.g. A/D, G/B, D/F# — do NOT simplify them to just the root chord.
                If the song is unknown, suggest appropriate chords.
                Use both major and minor chords as the song requires — do not default to all-minor or all-major.
                Reply with a chord chart, one section per line, like this:
                Verse: D A/D Bm A G D/G Em A
                Chorus: G A F#m Bm G A D
                Use only chord names. Label sections clearly. No explanatory text.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Suggest a chord chart for this song:\n{context}"
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }

    public async Task<string> ExtractTextFromImageAsync(string base64Image, string mediaType)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 1200,
            System = """
                You are transcribing handwritten or printed text from a photo.
                Extract ALL visible text exactly as written, preserving line breaks.
                Include lyrics, notes, annotations, anything written on the page.
                If you see chord names mixed into the text, include them.
                Return only the transcribed text, nothing else.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "Please transcribe all the text visible in this image." },
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = base64Image, MediaType = mediaType }
                        }
                    }
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }

    public async Task<string> GetRhymeSuggestionsAsync(string wordOrLine, string lyrics)
    {
        if (_client is null) return "";

        var context = string.IsNullOrWhiteSpace(lyrics) ? "" : $"\n\nSong lyrics so far:\n{lyrics}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 400,
            System = """
                You are a lyric writing assistant helping a songwriter find rhymes and word alternatives.
                Keep responses short and practical — this is a helper tool, not a verse generator.
                Format clearly with sections. Use the song's existing lyrics for context if provided.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"I'm writing a song and need help with: \"{wordOrLine}\"\n\nGive me:\n• 6-8 rhymes (perfect and near-rhymes)\n• 4-5 alternative words/phrases with a similar feel\n• 1 brief note on what emotion/theme fits best{context}"
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }

    public async Task<string> GetInstrumentTipsAsync(string chordChart, int? tempo, string musicKey)
    {
        if (_client is null) return "";

        var bpm = tempo.HasValue ? $"{tempo} BPM" : "unknown tempo";
        var chart = string.IsNullOrWhiteSpace(chordChart) ? "No chord chart yet" : chordChart;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 800,
            System = """
                You are a music teacher covering both guitar and piano.
                Give concrete, practical notation — arrows (↓↑) for strumming, finger labels (T i m a) and string numbers for picking, left/right hand patterns for piano.
                Be educational: explain WHY a pattern fits these chords and this tempo.
                Keep it focused and specific to the chords given.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"My song is in {musicKey} at {bpm}.\n\nChord chart:\n{chart}\n\n## Guitar\nSuggest:\n1. A strumming pattern with rhythm notation (↓↑)\n2. A fingerpicking pattern — which strings, which fingers (T i m a)\n3. One tip on transitioning between the main chords\n\n## Piano\nSuggest:\n1. A left hand pattern (bass note + chord, or arpeggiated)\n2. A right hand voicing or melody idea\n3. One tip on how to make it feel natural at this tempo"
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }

    public async Task<string> GetSingingTipsAsync(string title, string lyrics, string chordChart, int? tempo, string musicKey)
    {
        if (_client is null) return "";

        var bpm = tempo.HasValue ? $"{tempo} BPM" : "unknown tempo";
        var lyricsSection = string.IsNullOrWhiteSpace(lyrics) ? "No lyrics yet" : lyrics.Split('\n').Take(16).Aggregate((a, b) => a + "\n" + b);
        var chart = string.IsNullOrWhiteSpace(chordChart) ? "No chord chart yet" : chordChart;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 600,
            System = """
                You are a singing coach giving practical, educational advice for a guitarist-vocalist.
                Focus on phrasing, breath placement, dynamics, and melody direction.
                Suggest specific notes relative to the key (e.g. "start on the 5th").
                Keep it concrete and actionable — not generic singing advice.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Song: \"{title}\"\nKey: {musicKey}, Tempo: {bpm}\n\nChords:\n{chart}\n\nLyrics (first lines):\n{lyricsSection}\n\nGive me:\n1. Where to breathe (phrase breaks)\n2. Melody direction suggestion (where to go high/low)\n3. Dynamic tips (quiet/loud sections)\n4. One specific technique tip for this style"
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }

    public async Task<string> GetFingerpickPatternAsync(string title, string chordChart, int? tempo, string musicKey)
    {
        if (_client is null) return "";
        var chart = string.IsNullOrWhiteSpace(chordChart) ? "ukendt" : chordChart;
        var bpm = tempo.HasValue ? $"{tempo} BPM" : "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du er guitarlærer. Svar ALTID på dansk.
                Giv et praktisk fingerpickingmønster til sangen.
                Brug: T=tommelfinger, i=pegefinger, m=langfinger, a=ringfinger. Strengnumre: 6=lav E, 1=høj e.
                Svar PRÆCIST i dette format (ingen andet):
                FINGRE: T i m a i m a i
                STRENGE: 6 2 3 1 2 3 1 2
                TIP: [1-2 sætninger om mønstret + om det kræver bare fingre eller om plektor kan bruges som erstatning]
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Sang: {title}\nToneart: {musicKey}{(string.IsNullOrEmpty(bpm) ? "" : $"\nTempo: {bpm}")}\nakkorder:\n{chart}\n\nGiv et fingerpickingmønster til guitar."
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> GetStrumPatternAsync(string title, string chordChart, int? tempo, string musicKey)
    {
        if (_client is null) return "";
        var chart = string.IsNullOrWhiteSpace(chordChart) ? "ukendt" : chordChart;
        var bpm = tempo.HasValue ? $"{tempo} BPM" : "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 400,
            System = """
                Du er guitarlærer. Svar ALTID på dansk.
                Giv et praktisk strummingmønster til guitar.
                Brug: ↓=nedslag, ↑=opslag, –=pause/spring over.
                Svar PRÆCIST i dette format (ingen andet):
                SLAG: ↓ – ↓↑ – ↓ ↑ ↓↑ –
                TAK:  1 + 2 + 3 + 4 +
                TIP: [1-2 sætninger om mønstret]
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Sang: {title}\nToneart: {musicKey}{(string.IsNullOrEmpty(bpm) ? "" : $"\nTempo: {bpm}")}\nakkorder:\n{chart}\n\nGiv et strummingmønster til guitar."
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> GetKeyForVoiceAsync(string title, string currentKey, string lyrics)
    {
        if (_client is null) return "";
        var lyricsHint = string.IsNullOrWhiteSpace(lyrics) ? "" : $"\n\nFirst lines:\n{string.Join("\n", lyrics.Split('\n').Take(6))}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                You are a singing coach and music theory expert.
                Help the user find the best key to sing a song in.
                Consider typical male baritone/tenor range (G2–C5) and female mezzo-soprano (A3–F5).
                Be specific: name the key, say what the highest note is, suggest a capo position if on guitar.
                Danish is fine. Keep it short and practical.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Song: \"{title}\"\nCurrent key in the app: {currentKey}{lyricsHint}\n\nWhat key should I sing this in? Give options for both a male and female voice, and suggest a guitar capo position."
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> GetChordFingeringAsync(string chord)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 400,
            System = """
                You are a guitar teacher explaining how to play a single chord.
                Always include:
                1. Fret notation (6 digits, x=mute, 0=open, e.g. x02220 = Am), from low E to high e
                2. Which fingers to use (1=index 2=middle 3=ring 4=pinky)
                3. For slash chords (e.g. A/D): explain the bass note and how to voice it on guitar
                4. One practical tip if the chord is tricky
                Keep it short — 4-6 lines max. Danish is fine.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"How do I play this chord on guitar: {chord}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> GetChordTransitionsAsync(string chordChart, string musicKey)
    {
        if (_client is null) return "";
        var chart = string.IsNullOrWhiteSpace(chordChart) ? "ukendt" : chordChart;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 700,
            System = """
                You are a guitar and piano teacher. For each chord transition in the song, suggest:
                1. A passing note or short lick on guitar (e.g. "walk down: A - G# - G")
                2. A piano voicing or arpeggio that bridges the two chords smoothly
                Format as a list of transitions: Am → F: ... then Am → G: ...
                Keep it short and practical. Danish is fine.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Key: {musicKey}\nChords/progression:\n{chart}\n\nSuggest passing notes and transitions between the chords — both guitar and piano."
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> GetLyricsForSongAsync(string title)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1200,
            System = """
                You are a music practice assistant helping a singer-guitarist learn songs.
                Provide a practice sheet with the song structure and key lyric phrases to help them remember the song.
                Format with section labels (Verse 1, Chorus, Bridge etc.), the opening line of each section,
                and a note on the melody direction. This is a memory aid for practice, not a full transcription.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Give me a practice sheet for: {title}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> AnnotateLyricsWithChordsAsync(string lyrics, string chordChart, string musicKey, string title, string artist)
    {
        if (_client is null) return "";

        var songLabel = string.IsNullOrWhiteSpace(artist) ? title : $"{title} — {artist}";
        var chordHint = string.IsNullOrWhiteSpace(chordChart)
            ? "No chord chart provided — use your knowledge of the song."
            : $"Chord chart:\n{chordChart}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 2000,
            System = """
                You are a musician annotating lyrics with chord markers in UltimateGuitar format.
                Insert [Chord] markers IMMEDIATELY before the syllable where the chord starts, with no space between the marker and the word.
                Keep section labels (Verse:, Chorus:, Bridge: etc.) as plain text lines with no chord markers.
                Empty lines between sections must remain as empty lines.
                Return ONLY the annotated lyrics — no explanation, no intro text, nothing else.

                Example output format:
                Verse:
                [Am]Yesterday, [G]all my [F]troubles seemed so [E]far away
                [Am]Now it [G]looks as though they're [F]here to [E]stay

                Chorus:
                [C]I be[G]lieve in [Am]yesterday
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Song: {songLabel}\nKey: {musicKey}\n\n{chordHint}\n\nPlain lyrics to annotate:\n{lyrics}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public record UgTabResult(string Chords, string Lyrics);

    public async Task<UgTabResult> ParseUltimateGuitarTabAsync(string rawTab)
    {
        if (_client is null) return new UgTabResult("", "");

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 2000,
            System = """
                You are a musician parsing a raw Ultimate Guitar tab into two structured outputs.
                The tab may have chords written above lyrics, or inline chord markers like [Am].

                Return EXACTLY this format with the two delimiters — nothing else:

                ===CHORDS===
                Verse: Am F C G
                Chorus: C G Am F

                ===LYRICS===
                Verse:
                [Am]I was wrong to say I [F]loved her
                [C]When I told her it was [G]over

                Rules:
                - CHORDS section: one section label per line, chords listed after the colon. Only unique progression per section, not every bar.
                - LYRICS section: plain lyrics with [Chord] markers placed immediately before the syllable where the chord lands.
                - Keep section labels (Verse, Chorus, Bridge, etc.) as plain text lines with no chord markers.
                - Empty lines between sections must remain as empty lines.
                - Do NOT include tab notation, finger numbers, or any non-lyric content in the LYRICS section.
                - If no lyrics are present, return empty LYRICS section.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Parse this Ultimate Guitar tab:\n\n{rawTab}"
                }
            ]
        });

        var text = "";
        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) { text = tb.Text.Trim(); break; }

        var chordsStart = text.IndexOf("===CHORDS===", StringComparison.Ordinal);
        var lyricsStart = text.IndexOf("===LYRICS===", StringComparison.Ordinal);

        var chords = "";
        var lyrics = "";

        if (chordsStart >= 0 && lyricsStart > chordsStart)
            chords = text[(chordsStart + 12)..lyricsStart].Trim();
        else if (chordsStart >= 0)
            chords = text[(chordsStart + 12)..].Trim();

        if (lyricsStart >= 0)
            lyrics = text[(lyricsStart + 12)..].Trim();

        return new UgTabResult(chords, lyrics);
    }

    public async Task<string> ExtractChordsFromImageAsync(string base64Image, string mediaType)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 800,
            System = """
                You are a musician extracting chord information from sheet music or lyric sheets.
                Look for chord names written above lyrics or in chord diagrams.
                Return the chords as a chart with section labels:
                Verse: Am G F E
                Chorus: C G Am F
                If you see chord diagrams (fretboard diagrams), name the chords shown.
                If no chords are visible, say "No chords found in image."
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "Extract any chord information visible in this image." },
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = base64Image, MediaType = mediaType }
                        }
                    }
                }
            ]
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();
        }
        return "";
    }
}
