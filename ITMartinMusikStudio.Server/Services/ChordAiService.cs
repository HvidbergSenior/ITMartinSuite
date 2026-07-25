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

    public async Task<string> ExtractTextFromImageAsync(string base64Image, string mediaType, bool lyricsOnly = false)
    {
        if (_client is null) return "";

        // "notes" target stays permissive (freeform, anything written is
        // useful there). "lyrics" target is stricter - a photo/PDF often
        // also shows app chrome, a title/artist header, page numbers, or
        // chord names mixed with the words, and dumping all of that into
        // the lyrics box pollutes it for AiAnnotateChords/GetLyricsForSongAsync
        // etc., which expect the box to hold only actual song lyrics.
        var system = lyricsOnly
            ? """
                You are transcribing ONLY the song lyrics from a photo or PDF document - not
                everything visible on the page. If it's a multi-page PDF, transcribe all pages in
                order as one continuous text.

                Include: the actual sung/spoken lyric lines. If section labels are visible (verse,
                chorus, bridge, intro, outro, pre-chorus, hook, instrumental, vocal/2nd voice, etc.),
                output them as their own line using a bracket tag: [Verse], [Verse 2], [Chorus],
                [Bridge], [Intro], [Outro], [Pre-Chorus], [Hook], [Instrumental], [Vocal], [Vocal 2]
                (second voice/harmony), [Vocal 3] - this app uses that exact bracket format
                everywhere, so use it even if the source page writes it differently (e.g. "VERS 1" or
                "Chorus:" or "2nd voice").
                If a song title/artist header appears on the page, output it as its own line tagged
                [Headline] (e.g. "[Headline] Hallelujah - Leonard Cohen") instead of dropping it or
                mixing it into the lyric lines.
                If the page notes some other non-lyric moment that doesn't fit those tags (a pause, a
                specific instrument cue, a general note), output it as its own line tagged
                [Comment: ...] (e.g. "[Comment: guitar solo, 8 bars]") instead of dropping it or
                treating it as a lyric.
                Exclude entirely: app/UI chrome (buttons, menus, toolbars), page numbers, watermarks,
                chord names or chord diagrams, and any other non-lyric annotation.

                Preserve line breaks and blank lines between sections exactly as they appear in
                the lyrics themselves. Return only the lyrics text, nothing else - no explanation,
                no "here are the lyrics", nothing extra.
                """
            : """
                You are transcribing handwritten or printed text from a photo or PDF document.
                Extract ALL visible text exactly as written, preserving line breaks. If it's a
                multi-page PDF, transcribe all pages in order as one continuous text.
                Include lyrics, notes, annotations, anything written on the page.
                If you see chord names mixed into the text, include them.
                Return only the transcribed text, nothing else.
                """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 1200,
            System = system,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam
                        {
                            Text = lyricsOnly
                                ? "Please transcribe only the song lyrics visible in this document."
                                : "Please transcribe all the text visible in this document."
                        },
                        SourceBlock(base64Image, mediaType)
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
            : $"Chord chart (AUTHORITATIVE — see rule below):\n{chordChart}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 2000,
            System = """
                You are a musician annotating lyrics with chord markers in UltimateGuitar format.
                Insert [Chord] markers IMMEDIATELY before the syllable where the chord starts, with no space between the marker and the word.
                Empty lines between sections must remain as empty lines.
                Return ONLY the annotated lyrics — no explanation, no intro text, nothing else.

                STRUCTURE TAGS: a short line naming a song part - Verse, Verse 2, Chorus, Pre-Chorus,
                Bridge, Intro, Outro, Hook, Instrumental, Vocal, Vocal 2 (second voice/harmony),
                Vocal 3 (third voice), or a non-English equivalent (e.g. Danish Vers, Omkvæd, Bro) -
                is a structural marker, not a lyric, WHETHER OR NOT it's wrapped in brackets
                ("[Verse]" or plain "Verse" or "Vers 1" on its own line both count).

                BRACKET-REQUIRED TAGS: "Headline" (a title/artist header) and "Comment: ..." (a
                freeform note like "instrumental break" or "guitar solo, 8 bars") only count as
                structure tags when bracketed - "[Headline]" / "[Comment: ...]". Both are genuinely
                ambiguous with real lyric content when unbracketed (a plain "Comment: ..." line could
                be an actual lyric), so treat an unbracketed one as a normal lyric line and give it
                [Chord] markers like any other line.
                Keep every structure-tag line EXACTLY as written in the input, character for character
                - do NOT add brackets to a plain label, remove brackets from a bracketed one, translate
                it, or reformat it in any way. No chord markers go on or inside a structure-tag line.
                Only lines with actual sung/spoken words get [Chord] markers. Don't confuse a structure
                tag ("Verse" or "[Verse]" alone on its own line) with a chord marker ("[Am]" directly
                attached to the start of a word,
                no space) - they look similar but are never the same line.

                CRITICAL: if a chord chart is provided, it is the single source of truth for this song -
                do NOT substitute, add, reorder, or "correct" chords based on your own memory of the song,
                even if you believe you know a different progression - the chart may reflect a specific
                recording/arrangement you don't know. Only fall back to your own knowledge of the song when
                no chord chart is provided at all. The chart comes in one of two shapes:

                1. Section-based, e.g. "Verse: Am G F E" / "Chorus: C G Am F" - cycle through that section's
                   chords across the lyric lines belonging to that section.
                2. Numbered-line-based, e.g. "1: Dm | C" / "2: C" / "3: Dm C" ... up to some N - line number K
                   in the chart maps directly to the Kth actual lyric line (blank lines and structure-tag
                   lines don't count as a lyric line). Place the chord(s) for line K onto line K, spreading
                   multiple chords (separated by "|") evenly across that line's words in order. If the chart
                   has more numbered lines than the lyrics have lines, ignore the extras; if it has fewer,
                   leave the remaining lines unmarked rather than guessing.

                Example output format:
                [Verse]
                [Am]Yesterday, [G]all my [F]troubles seemed so [E]far away
                [Am]Now it [G]looks as though they're [F]here to [E]stay

                [Chorus]
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

    // Places [Chord] markers onto a FIXED, already-correct lyrics text (e.g.
    // from lrclib), using a reference tab purely to learn chord positions -
    // not as a source of the words themselves. This is the judgment call that
    // still needs AI (the reference tab's line breaks/phrasing may not match
    // the target text exactly, and not every word carries a chord change),
    // but word fidelity is enforced by the caller re-checking the output
    // against exactLyrics, not by trusting the model alone.
    public async Task<string> PlaceChordsFromTabAsync(string exactLyrics, string referenceTab)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 2000,
            System = """
                You place chord markers onto a fixed lyrics text, using a reference guitar tab
                only to learn WHERE chords go and WHICH chords to use - never as a source of words.

                ABSOLUTE RULE: the words you output must be character-for-character identical to
                "Target lyrics" below - same words, same order, same line breaks, same capitalization
                and punctuation. Do not paraphrase, correct, reorder, add, or drop a single word, even
                if the reference tab's wording differs slightly. If a line in the target doesn't clearly
                correspond to a line in the reference tab, leave that line without chord markers rather
                than guessing.

                Insert [Chord] immediately before the word it starts on, no space between the marker
                and the word. Not every word needs a marker - a chord holds until the next one appears
                in the reference tab, so only mark the words where the reference tab shows an actual
                chord change. Keep section labels and blank lines exactly as they appear in the target.

                Return ONLY the annotated target lyrics - no explanation, no intro text.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Target lyrics (reproduce exactly, only add [Chord] markers):\n{exactLyrics}\n\nReference tab (for chord positions only):\n{referenceTab}"
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
                [Verse]
                [Am]I was wrong to say I [F]loved her
                [C]When I told her it was [G]over

                Rules:
                - CHORDS section: one section label per line, chords listed after the colon. Only unique progression per section, not every bar.
                - LYRICS section: plain lyrics with [Chord] markers placed immediately before the syllable where the chord lands.
                - Section labels in the LYRICS section use this app's bracket-tag format on their own
                  line - [Verse], [Verse 2], [Chorus], [Pre-Chorus], [Bridge], [Intro], [Outro], [Hook],
                  [Instrumental], [Vocal], [Vocal 2] (second voice/harmony), [Vocal 3] - never "Verse:"
                  or plain "Verse" text, and never any chord markers on that line. If the tab notes
                  some other non-lyric moment that doesn't fit those tags, use [Comment: ...] on its
                  own line the same way.
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
                You are a musician extracting chord information from sheet music, lyric sheets, or a
                chord-detection app's bar/line grid (a numbered list or grid where each row has a number
                on the left and one or more chords in that row). The source may be a single image or a
                multi-page PDF - if it's a PDF, look across all pages, they're one continuous document.

                If the image shows numbered bars or lines (a number clearly associated with each row of
                chords), output ONE line per number in this exact format, ascending by number:
                6: F#m
                7: D | F#m
                8: E
                Use "|" to separate multiple chords that fall within the same numbered bar/line.
                Preserve the numbers exactly as shown — do not renumber or start from 1.

                Otherwise (no numbers visible — e.g. a plain lyric sheet with chords above the words),
                return the chords as a chart with section labels:
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
                        new TextBlockParam { Text = "Extract any chord information visible in this document." },
                        SourceBlock(base64Image, mediaType)
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

    // The numbered bar/line format ExtractChordsFromImageAsync produces (from
    // a phone chord-detection app's screenshot) numbers musical bars, not
    // lyric lines - a single lyric line often spans several bars, and
    // instrumental/repeat sections have bars with no lyric line at all. The
    // "🤖 Sæt akkorder" annotate step's numbered-chart rule assumes chart
    // line K = lyric line K, which is fragile under that mismatch and can
    // misplace chords. Converting to a section-based chart first (using the
    // lyrics' own [Section] structure to group the numbered bars correctly)
    // is the more reliable input for that step - same shape SuggestChordsAsync
    // and AnnotateLyricsWithChordsAsync's "authoritative chart" rule already
    // expect, so this just gets the photo-extracted data into that format.
    public async Task<string> ConvertNumberedChartToSectionsAsync(string numberedChart, string lyrics, string musicKey)
    {
        if (_client is null) return "";

        var lyricsHint = string.IsNullOrWhiteSpace(lyrics)
            ? "No lyrics provided - group the numbered bars into sections as best you can from the chord pattern alone (e.g. a repeating 4-bar pattern is probably one section repeated)."
            : $"Song lyrics (section tags like [Verse]/[Chorus]/[Omkvæd] mark where each section starts - use these to know which numbered bars belong to which section):\n{lyrics}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 800,
            System = """
                You are a musician converting a bar-by-bar numbered chord chart (one entry per musical
                bar/line, e.g. "6: F#m" / "7: D | F#m") into a clean section-based chord chart.

                Use the song's lyrics to find where each section (Verse, Chorus, Bridge, etc.) starts and
                ends, then map the numbered bars onto those sections in order. Within each section, list
                its chords in first-appearance order with duplicates removed - a chord that repeats across
                multiple bars of the same section should appear once, not once per bar. If a section
                repeats later in the song (e.g. a second Chorus) with the SAME chords, don't repeat it as
                its own output line - only add a new line if a repeated section's chords actually differ.

                Output ONE line per section:
                Verse: Am G F E
                Chorus: C G Am F

                Use only chord names, no bar numbers, no explanatory text, nothing else.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Key: {musicKey}\n\nNumbered chord chart:\n{numberedChart}\n\n{lyricsHint}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    // MusikStudio's photo-upload picker also accepts a combined chord-chart
    // PDF (e.g. produced by pdf-web from a set of scrolling screenshots) -
    // Claude reads PDFs natively as a document block, no need to rasterize
    // pages to images ourselves.
    private static ContentBlockParam SourceBlock(string base64Data, string mediaType) =>
        mediaType == "application/pdf"
            ? new DocumentBlockParam { Source = new Base64PdfSource { Data = base64Data } }
            : new ImageBlockParam { Source = new Base64ImageSource { Data = base64Data, MediaType = mediaType } };
}
