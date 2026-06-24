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
        if (!string.IsNullOrWhiteSpace(musicKey)) context.Append($"\nKey: {musicKey}");
        if (tempo.HasValue) context.Append($"\nTempo: {tempo} BPM");
        if (!string.IsNullOrWhiteSpace(lyrics))
            context.Append($"\nFirst lines of lyrics:\n{string.Join("\n", lyrics.Split('\n').Take(8))}");

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 600,
            System = """
                You are a musician helping suggest chord progressions.
                Reply with a chord chart in this format:
                Verse: Am G F E
                Chorus: C G Am F
                Bridge: Dm Am E Am
                Use only chord names, one section per line. Label sections clearly.
                Keep it concise — one chord progression per section is enough.
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
